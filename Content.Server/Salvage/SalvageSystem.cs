using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio;
using Content.Shared.Salvage;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.Chat.Managers;
using Content.Server.Gravity;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Construction.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Content.Server.Labels;
using Content.Server.Popups;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.Server.Salvage
{
    public sealed partial class SalvageSystem : SharedSalvageSystem
    {
        [Dependency] private readonly IChatManager _chat = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly IEntityManager _entManager = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AnchorableSystem _anchorable = default!;
        [Dependency] private readonly BiomeSystem _biome = default!;
        [Dependency] private readonly DungeonSystem _dungeon = default!;
        [Dependency] private readonly GravitySystem _gravity = default!;
        [Dependency] private readonly LabelSystem _labelSystem = default!;
        [Dependency] private readonly MapLoaderSystem _loader = default!;
        [Dependency] private readonly MetaDataSystem _metaData = default!;
        [Dependency] private readonly RadioSystem _radioSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly ShuttleSystem _shuttle = default!;
        [Dependency] private readonly ShuttleConsoleSystem _shuttleConsoles = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly PopupSystem _popup = default!; // WD EDIT

        private EntityQuery<MapGridComponent> _gridQuery;
        private EntityQuery<TransformComponent> _xformQuery;

        public override void Initialize()
        {
            base.Initialize();

            _gridQuery = GetEntityQuery<MapGridComponent>();
            _xformQuery = GetEntityQuery<TransformComponent>();

            InitializeExpeditions();
            InitializeMagnet();
            InitializeRunner();
        }

        private void Report(EntityUid source, string channelName, string messageKey, params (string, object)[] args)
        {
            var message = args.Length == 0 ? Loc.GetString(messageKey) : Loc.GetString(messageKey, args);
            var channel = _prototypeManager.Index<RadioChannelPrototype>(channelName);
            _radioSystem.SendRadioMessage(source, message, channel, source);
        }

        public override void Update(float frameTime)
        {
            UpdateExpeditions();
            UpdateMagnet();
            UpdateRunner();
        }
    }
}

