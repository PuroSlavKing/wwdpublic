using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.CombatMode.Disarm;
using Content.Server.Movement.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._White.CCVar;
using Content.Shared.Actions.Events;
using Content.Shared.Administration.Components;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.Contests;
using Content.Shared.Coordinates;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Mobs.Systems;
using Content.Shared.Speech.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Weapons.Melee;

public sealed class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageExamineSystem _damageExamine = default!;
    [Dependency] private readonly LagCompensationSystem _lag = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly ContestsSystem _contests = default!;
    // WD EDIT START
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly INetConfigurationManager _config = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    // WD EDIT END

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeSpeechComponent, MeleeHitEvent>(OnSpeechHit);
        SubscribeLocalEvent<MeleeWeaponComponent, DamageExamineEvent>(OnMeleeExamineDamage, after: [typeof(GunSystem)]);
    }

    private void OnMeleeExamineDamage(EntityUid uid, MeleeWeaponComponent component, ref DamageExamineEvent args)
    {
        if (component.Hidden)
            return;

        var damageSpec = GetDamage(uid, args.User, component);
        if (damageSpec.Empty)
            return;

        if (!component.DisableClick)
            _damageExamine.AddDamageExamine(args.Message, damageSpec, Loc.GetString("damage-melee"));

        if (component.DisableHeavy)
            return;

        if (damageSpec * component.HeavyDamageBaseModifier != damageSpec)
            _damageExamine.AddDamageExamine(args.Message, damageSpec * component.HeavyDamageBaseModifier, Loc.GetString("damage-melee-heavy"));

        if (component.HeavyStaminaCost == 0)
            return;

        var staminaCostMarkup = FormattedMessage.FromMarkupOrThrow(
            Loc.GetString("damage-stamina-cost",
                ("type", Loc.GetString("damage-melee-heavy")), ("cost", Math.Round(component.HeavyStaminaCost, 2).ToString("0.##"))));
        args.Message.PushNewline();
        args.Message.AddMessage(staminaCostMarkup);
    }

    protected override bool ArcRaySuccessful(EntityUid targetUid, Vector2 position, Angle angle, Angle arcWidth, float range, MapId mapId,
        EntityUid ignore, ICommonSession? session)
    {
        // Originally the client didn't predict damage effects so you'd intuit some level of how far
        // in the future you'd need to predict, but then there was a lot of complaining like "why would you add artifical delay" as if ping is a choice.
        // Now damage effects are predicted but for wide attacks it differs significantly from client and server so your game could be lying to you on hits.
        // This isn't fair in the slightest because it makes ping a huge advantage and this would be a hidden system.
        // Now the client tells us what they hit and we validate if it's plausible.

        // Even if the client is sending entities they shouldn't be able to hit:
        // A) Wide-damage is split anyway
        // B) We run the same validation we do for click attacks.

        // Could also check the arc though future effort + if they're aimbotting it's not really going to make a difference.

        // (This runs lagcomp internally and is what clickattacks use)
        if (!Interaction.InRangeUnobstructed(ignore, targetUid, range + 0.1f))
            return false;

        // TODO: Check arc though due to the aforementioned aimbot + damage split comments it's less important.
        return true;
    }

    protected override bool DoDisarm(EntityUid user, DisarmAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        if (!base.DoDisarm(user, ev, meleeUid, component, session)
            || !TryComp<CombatModeComponent>(user, out var combatMode))
            return false;

        var target = GetEntity(ev.Target!.Value);

        // WD EDIT START
        PhysicalShove(user, target);
        Interaction.DoContactInteraction(user, target);

        if (_mobState.IsIncapacitated(target))
            return true;

        if (!TryComp<HandsComponent>(target, out var targetHandsComponent))
        {
            if (!TryComp<StatusEffectsComponent>(target, out var status) ||
                !status.AllowedEffects.Contains("KnockedDown"))
            {
                if (TryComp<PhysicsComponent>(target, out var physComp)
                    && physComp.BodyType != BodyType.Static
                    && TryComp<DamageOtherOnHitComponent>(target, out var throwComp)
                    && throwComp.StaminaCost > 0)
                    _stamina.TakeStaminaDamage(user, throwComp.StaminaCost);
                return true;
            }
        }
        // WD EDIT END

        EntityUid? inTargetHand = targetHandsComponent?.ActiveHand is { IsEmpty: false }
            ? targetHandsComponent.ActiveHand.HeldEntity!.Value
            : null;

        var attemptEvent = new DisarmAttemptEvent(target, user, inTargetHand);
        RaiseLocalEvent(inTargetHand != null ? inTargetHand.Value : target, attemptEvent);

        if (attemptEvent.Cancelled)
            return true; // WWDP

        var chance = CalculateDisarmChance(user, target, inTargetHand, combatMode);

        // WWDP shove is guaranteed now, disarm chance is rolled on top

        _audio.PlayPvs(combatMode.DisarmSuccessSound, user, AudioParams.Default.WithVariation(0.025f).WithVolume(5f));
        AdminLogger.Add(LogType.DisarmedAction, $"{ToPrettyString(user):user} used disarm on {ToPrettyString(target):target}");

        var staminaDamage = CalculateShoveStaminaDamage(user, target); // WWDP shoving

        var eventArgs = new DisarmedEvent { Target = target, Source = user, DisarmProbability = chance, StaminaDamage = staminaDamage }; // WWDP shoving
        RaiseLocalEvent(target, eventArgs);

        if (!eventArgs.Handled)
        {
            ShoveOrDisarmPopup(disarm: false); // WWDP
            return true;
        }

        ShoveOrDisarmPopup(disarm: true); // WWDP

        _audio.PlayPvs(combatMode.DisarmSuccessSound, user, AudioParams.Default.WithVariation(0.025f).WithVolume(5f));
        AdminLogger.Add(LogType.DisarmedAction, $"{ToPrettyString(user):user} used disarm on {ToPrettyString(target):target}");

        return true;

        // WWDP edit (moved to function)
        void ShoveOrDisarmPopup(bool disarm)
        {
            var filterOther = Filter.PvsExcept(user, entityManager: EntityManager);
            var msgPrefix = "disarm-action-";

            if (!disarm)
            {
                return; // WWDP specific - Less popups; would probably want to remove on upstream
                msgPrefix = "disarm-action-shove-";
            }

            var msgOther = Loc.GetString(
                msgPrefix + "popup-message-other-clients",
                ("performerName", Identity.Entity(user, EntityManager)),
                ("targetName", Identity.Entity(target, EntityManager)));

            var msgUser = Loc.GetString(msgPrefix + "popup-message-cursor", ("targetName", Identity.Entity(target, EntityManager)));

            PopupSystem.PopupEntity(msgOther, user, filterOther, true);
            PopupSystem.PopupEntity(msgUser, target, user);
        }
        // WWDP edit end
    }

    // WWDP Push shove physics yeee
    private void PhysicalShove(EntityUid user, EntityUid target)
    {
        float shoverange = _config.GetCVar(WhiteCVars.ShoveRange);
        float shovespeed = _config.GetCVar(WhiteCVars.ShoveSpeed);
        float shovemass = _config.GetCVar(WhiteCVars.ShoveMassFactor);

        var animated = false;
        var throwInAir = false;

        if (HasComp<ItemComponent>(target)) // Throw items instead of shoving
        {
            animated = true;
            throwInAir = true;
            shoverange = 1.2f; // Constant range, approximately the same as the regular throw
        }

        var force = shoverange * _contests.MassContest(user, target, rangeFactor: shovemass);

        var userPos = user.ToCoordinates().ToMapPos(EntityManager, TransformSystem);
        var targetPos = target.ToCoordinates().ToMapPos(EntityManager, TransformSystem);
        var pushVector = (targetPos - userPos).Normalized() * force;

        _throwing.TryThrow(target, pushVector, force * shovespeed, user, animated: animated, throwInAir: throwInAir);
    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        EntityCoordinates targetCoordinates;
        Angle targetLocalAngle;

        if (session is not { } pSession)
            return Interaction.InRangeUnobstructed(user, target, range);

        (targetCoordinates, targetLocalAngle) = _lag.GetCoordinatesAngle(target, pSession);
        return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform) =>
        _color.RaiseEffect(Color.Red, targets, Filter.Pvs(targetXform.Coordinates, entityMan: EntityManager).RemoveWhereAttachedEntity(o => o == user));

    private float CalculateDisarmChance(EntityUid disarmer, EntityUid disarmed, EntityUid? inTargetHand, CombatModeComponent disarmerComp)
    {
        if (HasComp<DisarmProneComponent>(disarmer))
            return 1.0f;

        if (HasComp<DisarmProneComponent>(disarmed))
            return 0.0f;

        // WD EDIT START
        var chance = 1 - disarmerComp.BaseDisarmFailChance;

        chance *= Math.Clamp(
            _contests.StaminaContest(disarmer, disarmed)
            * _contests.HealthContest(disarmer, disarmed),
            0f,
            1f);

        if (inTargetHand != null && TryComp<DisarmMalusComponent>(inTargetHand, out var malus))
            chance *= 1 - malus.CurrentMalus;

        if (TryComp<ShovingComponent>(disarmer, out var shoving))
            chance *= 1 + shoving.DisarmBonus;

        return chance;
        // WD EDIT END
    }

    // WD EDIT START
    private float CalculateShoveStaminaDamage(EntityUid disarmer, EntityUid disarmed)
    {
        var shovemass = _config.GetCVar(WhiteCVars.ShoveMassFactor);
        var baseStaminaDamage = TryComp<ShovingComponent>(disarmer, out var shoving) ? shoving.StaminaDamage : ShovingComponent.DefaultStaminaDamage;

        return baseStaminaDamage * _contests.MassContest(disarmer, disarmed, false, shovemass);
    }
    // WD EDIT END

    public override void DoLunge(EntityUid user, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, Angle spriteRotation, bool predicted = true) => // WD EDIT
        RaiseNetworkEvent(new MeleeLungeEvent(
                GetNetEntity(user),
                GetNetEntity(weapon),
                angle,
                localPos,
                animation,
                spriteRotation), // WD EDIT
            predicted ? Filter.PvsExcept(user, entityManager: EntityManager) : Filter.Pvs(user, entityManager: EntityManager));

    private void OnSpeechHit(EntityUid owner, MeleeSpeechComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit || !args.HitEntities.Any() || comp.Battlecry is null)
            return;

        _chat.TrySendInGameICMessage(args.User, comp.Battlecry, InGameICChatType.Speak, true, true, checkRadioPrefix: false);  //Speech that isn't sent to chat or adminlogs
    }
}
