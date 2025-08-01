using System.Numerics;
using Content.Server._White.Throwing;
using Content.Server.Inventory;
using Content.Server.Stack;
using Content.Server.Stunnable;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems; // Shitmed Change
using Content.Shared._Shitmed.Body.Events; // Shitmed Change
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stacks;
using Content.Shared.Throwing;
using Robust.Shared.GameStates;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;
using Content.Shared._White.Hands;

namespace Content.Server.Hands.Systems
{
    public sealed class HandsSystem : SharedHandsSystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly StackSystem _stackSystem = default!;
        [Dependency] private readonly VirtualItemSystem _virtualItemSystem = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        [Dependency] private readonly PullingSystem _pullingSystem = default!;
        [Dependency] private readonly ThrowingSystem _throwingSystem = default!;
        [Dependency] private readonly SharedBodySystem _bodySystem = default!; // Shitmed Change
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<HandsComponent, DisarmedEvent>(OnDisarmed, before: new[] { typeof(StunSystem) });

            SubscribeLocalEvent<HandsComponent, PullStartedMessage>(HandlePullStarted);
            SubscribeLocalEvent<HandsComponent, PullStoppedMessage>(HandlePullStopped);

            SubscribeLocalEvent<HandsComponent, BodyPartAddedEvent>(HandleBodyPartAdded);
            SubscribeLocalEvent<HandsComponent, BodyPartRemovedEvent>(HandleBodyPartRemoved);

            SubscribeLocalEvent<HandsComponent, ComponentGetState>(GetComponentState);

            SubscribeLocalEvent<HandsComponent, BeforeExplodeEvent>(OnExploded);
            SubscribeLocalEvent<HandsComponent, BodyPartEnabledEvent>(HandleBodyPartEnabled); // Shitmed Change
            SubscribeLocalEvent<HandsComponent, BodyPartDisabledEvent>(HandleBodyPartDisabled); // Shitmed Change

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.ThrowItemInHand, new PointerInputCmdHandler(HandleThrowItem))
                .Register<HandsSystem>();
        }

        public override void Shutdown()
        {
            base.Shutdown();

            CommandBinds.Unregister<HandsSystem>();
        }

        private void GetComponentState(EntityUid uid, HandsComponent hands, ref ComponentGetState args)
        {
            args.State = new HandsComponentState(hands);
        }


        private void OnExploded(Entity<HandsComponent> ent, ref BeforeExplodeEvent args)
        {
            if (ent.Comp.DisableExplosionRecursion)
                return;

            foreach (var hand in ent.Comp.Hands.Values)
            {
                if (hand.HeldEntity is { } uid)
                    args.Contents.Add(uid);
            }
        }

        private void OnDisarmed(EntityUid uid, HandsComponent component, DisarmedEvent args)
        {
            if (args.Handled)
                return;

            if (!_random.Prob(args.DisarmProbability)) // WWDP shove
                return;

            // Break any pulls
            if (TryComp(uid, out PullerComponent? puller) && TryComp(puller.Pulling, out PullableComponent? pullable))
                _pullingSystem.TryStopPull(puller.Pulling.Value, pullable, ignoreGrab: true); // Goobstation edit added check for grab

            var offsetRandomCoordinates = _transformSystem.GetMoverCoordinates(args.Target).Offset(_random.NextVector2(1f, 1.5f));

            // WWDP edit start
            if (TryGetActiveItem(args.Target, out var item))
            {
                args.DisarmObject = item.Value;

                if (args.PickupToHands)
                {
                    if (!TryDrop(args.Target, item.Value))
                        return;

                    if (TryPickupAnyHand(args.Source, item.Value, checkActionBlocker: false)
                        && TryGetEmptyHand(args.Source, out var userEmptyHand))
                        SetActiveHand(args.Source, userEmptyHand);
                }
                args.Handled = true;
                return;
            }
            // WWDP edit end

            if (!ThrowHeldItem(args.Target, offsetRandomCoordinates))
                return;

            args.Handled = true; // Successful disarm.
        }

        // Shitmed Change Start
        private void TryAddHand(EntityUid uid, HandsComponent component, Entity<BodyPartComponent> part, string slot)
        {
            if (part.Comp is null
                || part.Comp.PartType != BodyPartType.Hand)
                return;

            // If this annoys you, which it should.
            // Ping Smugleaf.
            var location = part.Comp.Symmetry switch
            {
                BodyPartSymmetry.None => HandLocation.Middle,
                BodyPartSymmetry.Left => HandLocation.Left,
                BodyPartSymmetry.Right => HandLocation.Right,
                _ => throw new ArgumentOutOfRangeException(nameof(part.Comp.Symmetry))
            };

            if (part.Comp.Enabled
                && _bodySystem.TryGetParentBodyPart(part, out var _, out var parentPartComp)
                && parentPartComp.Enabled)
                AddHand(uid, slot, location);
        }

        private void HandleBodyPartAdded(EntityUid uid, HandsComponent component, ref BodyPartAddedEvent args)
        {
            TryAddHand(uid, component, args.Part, args.Slot);
        }

        private void HandleBodyPartRemoved(EntityUid uid, HandsComponent component, ref BodyPartRemovedEvent args)
        {
            if (args.Part.Comp is null
                || args.Part.Comp.PartType != BodyPartType.Hand)
                return;
            RemoveHand(uid, args.Slot);
        }

        private void HandleBodyPartEnabled(EntityUid uid, HandsComponent component, ref BodyPartEnabledEvent args) =>
            TryAddHand(uid, component, args.Part, SharedBodySystem.GetPartSlotContainerId(args.Part.Comp.ParentSlot?.Id ?? string.Empty));

        private void HandleBodyPartDisabled(EntityUid uid, HandsComponent component, ref BodyPartDisabledEvent args)
        {
            if (TerminatingOrDeleted(uid)
                || args.Part.Comp is null
                || args.Part.Comp.PartType != BodyPartType.Hand)
                return;

            RemoveHand(uid, SharedBodySystem.GetPartSlotContainerId(args.Part.Comp.ParentSlot?.Id ?? string.Empty));
        }

        // Shitmed Change End

        #region pulling

        private void HandlePullStarted(EntityUid uid, HandsComponent component, PullStartedMessage args)
        {
            if (args.PullerUid != uid)
                return;

            if (TryComp<PullerComponent>(args.PullerUid, out var pullerComp) && !pullerComp.NeedsHands)
                return;

            if (!_virtualItemSystem.TrySpawnVirtualItemInHand(args.PulledUid, uid))
            {
                DebugTools.Assert("Unable to find available hand when starting pulling??");
            }
        }

        private void HandlePullStopped(EntityUid uid, HandsComponent component, PullStoppedMessage args)
        {
            if (args.PullerUid != uid)
                return;

            // Try find hand that is doing this pull.
            // and clear it.
            foreach (var hand in component.Hands.Values)
            {
                if (hand.HeldEntity == null
                    || !TryComp(hand.HeldEntity, out VirtualItemComponent? virtualItem)
                    || virtualItem.BlockingEntity != args.PulledUid)
                {
                    continue;
                }

                TryDrop(args.PullerUid, hand, handsComp: component);
                break;
            }
        }

        #endregion

        #region interactions

        private bool HandleThrowItem(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
        {
            if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player) || !coordinates.IsValid(EntityManager))
                return false;

            // Goobstation start
            if (TryGetActiveItem(player, out var item) && TryComp<VirtualItemComponent>(item, out var virtComp))
            {
                var userEv = new VirtualItemDropAttemptEvent(virtComp.BlockingEntity, player, item.Value, true);
                RaiseLocalEvent(player, userEv);

                var targEv = new VirtualItemDropAttemptEvent(virtComp.BlockingEntity, player, item.Value, true);
                RaiseLocalEvent(virtComp.BlockingEntity, targEv);

                if (userEv.Cancelled || targEv.Cancelled)
                    return false;
            }
            // Goobstation end

            return ThrowHeldItem(player, coordinates);
        }

        /// <summary>
        /// Throw the player's currently held item.
        /// </summary>
        public bool ThrowHeldItem(EntityUid player, EntityCoordinates coordinates, float minDistance = 0.1f)
        {
            if (ContainerSystem.IsEntityInContainer(player) ||
                !TryComp(player, out HandsComponent? hands) ||
                hands.ActiveHandEntity is not { } throwEnt ||
                !_actionBlockerSystem.CanThrow(player, throwEnt))
                return false;
            // Goobstation start added throwing for grabbed mobs, mnoved direction.
            var direction = _transformSystem.ToMapCoordinates(coordinates).Position - _transformSystem.GetWorldPosition(player);

            if (TryComp<VirtualItemComponent>(throwEnt, out var virt))
            {
                var userEv = new VirtualItemThrownEvent(virt.BlockingEntity, player, throwEnt, direction);
                RaiseLocalEvent(player, userEv);

                var targEv = new VirtualItemThrownEvent(virt.BlockingEntity, player, throwEnt, direction);
                RaiseLocalEvent(virt.BlockingEntity, targEv);

                if (userEv.Cancelled || targEv.Cancelled)
                    return false;
            }
            // Goobstation end

            if (_timing.CurTime < hands.NextThrowTime)
                return false;
            hands.NextThrowTime = _timing.CurTime + hands.ThrowCooldown;

            if (EntityManager.TryGetComponent(throwEnt, out StackComponent? stack) && stack.Count > 1 && stack.ThrowIndividually)
            {
                var splitStack = _stackSystem.Split(throwEnt, 1, EntityManager.GetComponent<TransformComponent>(player).Coordinates, stack);

                if (splitStack is not { Valid: true })
                    return false;

                throwEnt = splitStack.Value;
            }

            if (direction == Vector2.Zero)
                return true;

            var length = direction.Length();
            var distance = Math.Clamp(length, minDistance, hands.ThrowRange);
            direction *= distance / length;

            var throwSpeed = hands.BaseThrowspeed;

            // Let other systems change the thrown entity (useful for virtual items)
            // or the throw strength.
            var itemEv = new BeforeGettingThrownEvent(throwEnt, direction, throwSpeed, player);
            RaiseLocalEvent(throwEnt, ref itemEv); // WD EDIT - We don't need two identical events.

            if (itemEv.Cancelled)
                return true;

            var ev = new BeforeThrowEvent(throwEnt, direction, throwSpeed, player);
            RaiseLocalEvent(player, ref ev);

            if (ev.Cancelled)
                return true;

            // This can grief the above event so we raise it afterwards
            if (IsHolding(player, throwEnt, out _, hands) && !TryDrop(player, throwEnt, handsComp: hands))
                return false;
            // WWDP EDIT START
            var deselEv = new HandDeselectedEvent(player);                              // because throwcode is serverside, the HandDeselectedEvent doesn't get raised on client.
            RaiseNetworkEvent(new HandDeselectedNetworkCrutchWrap(GetNetEntity(throwEnt), GetNetEntity(player)));   // This is the best i've came up with.
            // WWDP EDIT END
            _throwingSystem.TryThrow(ev.ItemUid, ev.Direction, ev.ThrowSpeed, ev.PlayerUid, compensateFriction: !HasComp<LandAtCursorComponent>(ev.ItemUid));

            return true;
        }

        #endregion
    }
}
