using System.Linq;
using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Shared.GameTicking;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._White.Silicons.AI.Components;
using Content.Shared._White.Silicons.Borgs.Components;
using Content.Shared.Access.Components;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Lock;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Stunnable;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Tag;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Toolshed;

namespace Content.Server.Silicons.Laws;

/// <inheritdoc/>
public sealed class SiliconLawSystem : SharedSiliconLawSystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!; // WD edit - AiRemoteControl
    [Dependency] private readonly IRobustRandom _random = default!; // WWDP - random roundstart lawset

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconLawBoundComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SiliconLawBoundComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<SiliconLawBoundComponent, ToggleLawsScreenEvent>(OnToggleLawsScreen);
        SubscribeLocalEvent<SiliconLawBoundComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
        SubscribeLocalEvent<SiliconLawBoundComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        SubscribeLocalEvent<SiliconLawProviderComponent, GetSiliconLawsEvent>(OnDirectedGetLaws);
        SubscribeLocalEvent<SiliconLawProviderComponent, IonStormLawsEvent>(OnIonStormLaws);
        SubscribeLocalEvent<SiliconLawProviderComponent, MindAddedMessage>(OnLawProviderMindAdded);
        SubscribeLocalEvent<SiliconLawProviderComponent, MindRemovedMessage>(OnLawProviderMindRemoved);
        SubscribeLocalEvent<SiliconLawProviderComponent, GotEmaggedEvent>(OnEmagLawsAdded);
        SubscribeLocalEvent<SiliconLawUpdaterComponent, GotEmaggedEvent>(OnAiSubvert); // WD edit - AI subvert
    }

    private void OnMapInit(EntityUid uid, SiliconLawBoundComponent component, MapInitEvent args)
    {
        TryRandomizeLaws(uid); // WWDP

        GetLaws(uid, component);
    }

    private void OnMindAdded(EntityUid uid, SiliconLawBoundComponent component, MindAddedMessage args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        // WD edit - AiRemoteControl-Start
        if (HasComp<AiRemoteControllerComponent>(uid) || _tagSystem.HasTag(uid, "StationAi")) // skip a law's notification for remotable and AI
            return;
        // WD edit - AiRemoteControl-End

        var msg = Loc.GetString("laws-notify");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, actor.PlayerSession.Channel, colorOverride: Color.FromHex("#2ed2fd"));

        if (!TryComp<SiliconLawProviderComponent>(uid, out var lawcomp))
            return;

        if (!lawcomp.Subverted)
            return;

        var modifedLawMsg = Loc.GetString("laws-notify-subverted");
        var modifiedLawWrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", modifedLawMsg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, modifedLawMsg, modifiedLawWrappedMessage, default, false, actor.PlayerSession.Channel, colorOverride: Color.Red);
    }

    private void OnLawProviderMindAdded(Entity<SiliconLawProviderComponent> ent, ref MindAddedMessage args)
    {
        if (!ent.Comp.Subverted)
            return;
        EnsureSubvertedSiliconRole(args.Mind);
    }

    private void OnLawProviderMindRemoved(Entity<SiliconLawProviderComponent> ent, ref MindRemovedMessage args)
    {
        if (!ent.Comp.Subverted)
            return;
        RemoveSubvertedSiliconRole(args.Mind);
    }

    private void OnToggleLawsScreen(EntityUid uid, SiliconLawBoundComponent component, ToggleLawsScreenEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor))
            return;
        args.Handled = true;

        _userInterface.TryToggleUi(uid, SiliconLawsUiKey.Key, actor.PlayerSession);
    }

    private void OnBoundUIOpened(EntityUid uid, SiliconLawBoundComponent component, BoundUIOpenedEvent args)
    {
        TryComp(uid, out IntrinsicRadioTransmitterComponent? intrinsicRadio);
        var radioChannels = intrinsicRadio?.Channels;

        var state = new SiliconLawBuiState(GetLaws(uid).Laws, radioChannels);
        _userInterface.SetUiState(args.Entity, SiliconLawsUiKey.Key, state);
    }

    private void OnPlayerSpawnComplete(EntityUid uid, SiliconLawBoundComponent component, PlayerSpawnCompleteEvent args)
    {
        component.LastLawProvider = args.Station;
    }

    private void OnDirectedGetLaws(EntityUid uid, SiliconLawProviderComponent component, ref GetSiliconLawsEvent args)
    {
        if (args.Handled)
            return;

        if (component.Lawset == null)
            component.Lawset = GetLawset(component.Laws);

        args.Laws = component.Lawset;

        args.Handled = true;
    }

    private void OnIonStormLaws(EntityUid uid, SiliconLawProviderComponent component, ref IonStormLawsEvent args)
    {
        // Emagged borgs are immune to ion storm
        if (!HasComp<EmaggedComponent>(uid))
        {
            component.Lawset = args.Lawset;

            // gotta tell player to check their laws
            NotifyLawsChanged(uid);

            // Show the silicon has been subverted.
            component.Subverted = true;

            // new laws may allow antagonist behaviour so make it clear for admins
            if(_mind.TryGetMind(uid, out var mindId, out _))
                EnsureSubvertedSiliconRole(mindId);

        }
    }

    private void OnEmagLawsAdded(EntityUid uid, SiliconLawProviderComponent component, ref GotEmaggedEvent args)
    {

        if (component.Lawset == null)
            component.Lawset = GetLawset(component.Laws);

        // WD edit - AiRemoteControl-Start
        if (HasComp<AiRemoteControllerComponent>(uid)) // You can't emag controllable entities
            return;
        // WD edit - AiRemoteControl-End

        // Show the silicon has been subverted.
        component.Subverted = true;

        // Add the first emag law before the others
        var name = CompOrNull<EmagSiliconLawComponent>(uid)?.OwnerName ?? Name(args.UserUid); // DeltaV: Reuse emagger name if possible
        component.Lawset?.Laws.Insert(0, new SiliconLaw
        {
            LawString = Loc.GetString("law-emag-custom", ("name", name), ("title", Loc.GetString(component.Lawset.ObeysTo))), // DeltaV: pass name from variable
            Order = -1 // Goobstation - AI/borg law changes - borgs obeying AI
        });

        //Add the secrecy law after the others
        component.Lawset?.Laws.Add(new SiliconLaw
        {
            LawString = Loc.GetString("law-emag-secrecy", ("faction", Loc.GetString(component.Lawset.ObeysTo))),
            Order = component.Lawset.Laws.Max(law => law.Order) + 1
        });

        if (TryComp<EmagSiliconLawComponent>(uid, out var emagSiliconLaw)) // WD edit - make emagged AI laws unremovable
            component.UnRemovable = emagSiliconLaw.UnRemovable;
    }

    protected override void OnGotEmagged(EntityUid uid, EmagSiliconLawComponent component, ref GotEmaggedEvent args)
    {
        if (component.RequireOpenPanel && TryComp<WiresPanelComponent>(uid, out var panel) && !panel.Open)
            return;

        base.OnGotEmagged(uid, component, ref args);
        NotifyLawsChanged(uid, component.EmaggedSound);
        if(_mind.TryGetMind(uid, out var mindId, out _))
            EnsureSubvertedSiliconRole(mindId);

        _stunSystem.TryParalyze(uid, component.StunTime, true);

    }

    // WD edit start - AI subvert
    private void OnAiSubvert(Entity<SiliconLawUpdaterComponent> ent, ref GotEmaggedEvent args)
    {
        var query = EntityManager.CompRegistryQueryEnumerator(ent.Comp.Components);

        while (query.MoveNext(out var update))
        {
            if (!TryComp<EmagSiliconLawComponent>(update, out var emagSiliconLaw)
                || !TryComp<SiliconLawProviderComponent>(update, out var siliconLawProvider))
                continue;

            OnGotEmagged(update, emagSiliconLaw, ref args);
            OnEmagLawsAdded(update, siliconLawProvider, ref args);
        }
    }
    // WD edit end

    private void EnsureSubvertedSiliconRole(EntityUid mindId)
    {
        if (!_roles.MindHasRole<SubvertedSiliconRoleComponent>(mindId))
            _roles.MindAddRole(mindId, "MindRoleSubvertedSilicon");
    }

    private void RemoveSubvertedSiliconRole(EntityUid mindId)
    {
        if (_roles.MindHasRole<SubvertedSiliconRoleComponent>(mindId))
            _roles.MindTryRemoveRole<SubvertedSiliconRoleComponent>(mindId);
    }

    // WWDP edit start
    public void TryRandomizeLaws(EntityUid uid)
    {
        if (!TryComp<SiliconLawProviderComponent>(uid, out var lawsProviderComp)
            || !TryComp<RandomizeStartingLawsetComponent>(uid, out var randomLawsComp)
            || randomLawsComp.Lawsets.Count == 0)
            return;

        lawsProviderComp.Laws = _random.Pick(randomLawsComp.Lawsets);
    }
    // WWDP edit end

    public SiliconLawset GetLaws(EntityUid uid, SiliconLawBoundComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new SiliconLawset();

        var ev = new GetSiliconLawsEvent(uid);

        RaiseLocalEvent(uid, ref ev);
        if (ev.Handled)
        {
            component.LastLawProvider = uid;
            return ev.Laws;
        }

        var xform = Transform(uid);

        if (_station.GetOwningStation(uid, xform) is { } station)
        {
            RaiseLocalEvent(station, ref ev);
            if (ev.Handled)
            {
                component.LastLawProvider = station;
                return ev.Laws;
            }
        }

        if (xform.GridUid is { } grid)
        {
            RaiseLocalEvent(grid, ref ev);
            if (ev.Handled)
            {
                component.LastLawProvider = grid;
                return ev.Laws;
            }
        }

        if (component.LastLawProvider == null ||
            Deleted(component.LastLawProvider) ||
            Terminating(component.LastLawProvider.Value))
        {
            component.LastLawProvider = null;
        }
        else
        {
            RaiseLocalEvent(component.LastLawProvider.Value, ref ev);
            if (ev.Handled)
            {
                return ev.Laws;
            }
        }

        RaiseLocalEvent(ref ev);
        return ev.Laws;
    }

    public void NotifyLawsChanged(EntityUid uid, SoundSpecifier? cue = null)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var msg = Loc.GetString("laws-update-notify");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, actor.PlayerSession.Channel, colorOverride: Color.Red);

        if (cue != null && _mind.TryGetMind(uid, out var mindId, out _))
            _roles.MindPlaySound(mindId, cue);
    }

    /// <summary>
    /// Extract all the laws from a lawset's prototype ids.
    /// </summary>
    public SiliconLawset GetLawset(ProtoId<SiliconLawsetPrototype> lawset)
    {
        var proto = _prototype.Index(lawset);
        var laws = new SiliconLawset()
        {
            Laws = new List<SiliconLaw>(proto.Laws.Count)
        };
        foreach (var law in proto.Laws)
        {
            laws.Laws.Add(_prototype.Index<SiliconLawPrototype>(law));
        }
        laws.ObeysTo = proto.ObeysTo;

        return laws;
    }

    /// <summary>
    /// Set the laws of a silicon entity while notifying the player.
    /// </summary>
    public bool SetLaws(List<SiliconLaw> newLaws, EntityUid target, SoundSpecifier? cue = null, bool unRemovable = false)
    {
        if (!TryComp<SiliconLawProviderComponent>(target, out var component)
            || component.UnRemovable)
            return false;

        if (component.Lawset == null)
            component.Lawset = new SiliconLawset();

        component.UnRemovable = unRemovable;
        component.Lawset.Laws = newLaws;
        NotifyLawsChanged(target, cue);
        return true;
    }

    protected override void OnUpdaterInsert(Entity<SiliconLawUpdaterComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        // TODO: Prediction dump this
        if (!TryComp(args.Entity, out SiliconLawProviderComponent? provider))
            return;

        var lawset = GetLawset(provider.Laws).Laws;
        var query = EntityManager.CompRegistryQueryEnumerator(ent.Comp.Components);

        while (query.MoveNext(out var update))
        {
            SetLaws(lawset, update, provider.LawUploadSound, provider.UnRemovable);

            // WD edit - AiRemoteControl-Start
            if (TryComp<StationAiHeldComponent>(update, out var stationAiHeldComp) && stationAiHeldComp.CurrentConnectedEntity != null && HasComp<SiliconLawProviderComponent>(stationAiHeldComp.CurrentConnectedEntity))
            {
                SetLaws(lawset, stationAiHeldComp.CurrentConnectedEntity.Value);
            }
            // WD edit - AiRemoteControl-End
        }
    }

    // WD edit - AiRemoteControl-Start
    public void SetLawsSilent(List<SiliconLaw> newLaws, EntityUid target, SoundSpecifier? cue = null)
    {
        if (!TryComp<SiliconLawProviderComponent>(target, out var component))
            return;

        if (component.Lawset == null)
            component.Lawset = new SiliconLawset();

        component.Lawset.Laws = newLaws;
    }
    // WD edit - AiRemoteControl-End
}

[ToolshedCommand, AdminCommand(AdminFlags.Admin)]
public sealed class LawsCommand : ToolshedCommand
{
    private SiliconLawSystem? _law;

    [CommandImplementation("list")]
    public IEnumerable<EntityUid> List()
    {
        var query = EntityManager.EntityQueryEnumerator<SiliconLawBoundComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            yield return uid;
        }
    }

    [CommandImplementation("get")]
    public IEnumerable<string> Get([PipedArgument] EntityUid lawbound)
    {
        _law ??= GetSys<SiliconLawSystem>();

        foreach (var law in _law.GetLaws(lawbound).Laws)
        {
            yield return $"law {law.LawIdentifierOverride ?? law.Order.ToString()}: {Loc.GetString(law.LawString)}";
        }
    }
}
