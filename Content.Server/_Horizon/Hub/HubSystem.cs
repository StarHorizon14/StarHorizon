using System.Linq;
using Content.Server.Antag;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared._Horizon.CCVar;
using Content.Shared._Horizon.Hub;
using Content.Shared.Clothing;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Horizon.Hub;

public sealed class HubSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    private static readonly ResPath HubGridPath = new("/Maps/_NF/Admin/minicentcomm.yml");
    private static readonly EntProtoId HubHeadsetPrototype = "ClothingHeadsetGrey";

    private sealed class HubRecord
    {
        public required MapId Map;
        public required EntityUid Grid;
        public required EntityUid Entity;
        public required HumanoidCharacterProfile DressedFrom;
    }

    private readonly Dictionary<ICommonSession, HubRecord> _hubs = new();
    private readonly HashSet<ICommonSession> _pendingHubs = new();

    private (MapId Map, EntityUid Grid)? _sharedHub;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnPlayerBeforeSpawn);
        SubscribeLocalEvent<PlayerProfileUpdatedEvent>(OnPlayerProfileUpdated);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRoundStartSpawning, before: new[] { typeof(AntagSelectionSystem) });

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        SpawnHub(ev.PlayerSession);
    }

    private void OnRoundStartSpawning(RulePlayerSpawningEvent ev)
    {
        if (_hubs.Count == 0)
            return;

        var ticker = EntityManager.System<GameTicker>();
        var hubSessions = ev.PlayerPool.Where(session => _hubs.ContainsKey(session)).ToList();

        foreach (var session in hubSessions)
        {
            ev.PlayerPool.Remove(session);
            ticker.MakeJoinGame(session, EntityUid.Invalid, lateJoin: false);
        }
    }

    private void OnPlayerBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (_hubs.ContainsKey(ev.Player))
            RaiseNetworkEvent(new HubLeftEvent(), ev.Player);

        DespawnHub(ev.Player);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
        {
            _pendingHubs.Remove(args.Session);
            DespawnHub(args.Session);
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        var sessions = _playerManager.Sessions.ToArray();

        _hubs.Clear();
        _pendingHubs.Clear();
        _sharedHub = null;

        Timer.Spawn(0, () =>
        {
            foreach (var session in sessions)
            {
                if (session.Status == SessionStatus.InGame)
                    SpawnHub(session);
            }
        });
    }

    private void OnPlayerProfileUpdated(PlayerProfileUpdatedEvent ev)
    {
        if (!_hubs.TryGetValue(ev.Session, out var hub))
            return;

        if (ev.Profile is not HumanoidCharacterProfile profile)
            return;

        var normalizedNew = profile.WithBankBalance(0);
        var normalizedOld = hub.DressedFrom.WithBankBalance(0);
        if (normalizedNew.MemberwiseEquals(normalizedOld))
        {
            hub.DressedFrom = profile;
            return;
        }

        var coords = new EntityCoordinates(hub.Grid, Transform(hub.Entity).LocalPosition);

        DetachMind(hub.Entity);
        if (Exists(hub.Entity))
            QueueDel(hub.Entity);

        var entity = SpawnCharacter(coords, profile, ev.Session);
        AttachCharacter(ev.Session, entity);
        hub.Entity = entity;
        hub.DressedFrom = profile;
    }

    private void AttachCharacter(ICommonSession session, EntityUid entity)
    {
        _playerManager.SetAttachedEntity(session, entity);

        var mind = _mind.GetOrCreateMind(session.UserId);
        _mind.TransferTo(mind.Owner, entity, mind: mind.Comp);
    }

    private void DetachMind(EntityUid entity)
    {
        if (!TryComp<MindContainerComponent>(entity, out var container) || container.Mind is not { } mindId)
            return;

        _mind.WipeMind(mindId);
    }

    private async void SpawnHub(ICommonSession session)
    {
        if (!_cfg.GetCVar(HorizonCCVars.HubEnabled))
            return;

        if (_hubs.ContainsKey(session) || !_pendingHubs.Add(session))
            return;

        try
        {
            await _userDb.WaitLoadComplete(session);
        }
        catch (OperationCanceledException)
        {
            _pendingHubs.Remove(session);
            return;
        }

        _pendingHubs.Remove(session);

        if (session.Status != SessionStatus.InGame || _hubs.ContainsKey(session))
            return;

        MapId mapId;
        EntityUid gridUid;

        if (_cfg.GetCVar(HorizonCCVars.HubShared))
        {
            if (!EnsureSharedHub())
            {
                Log.Error($"Failed to load shared hub grid from {HubGridPath} for {session}");
                return;
            }

            (mapId, gridUid) = _sharedHub!.Value;
        }
        else
        {
            var loadOptions = new DeserializationOptions { InitializeMaps = true };
            if (!_loader.TryLoadGrid(HubGridPath, out var map, out var grid, loadOptions))
            {
                Log.Error($"Failed to load hub grid from {HubGridPath} for {session}");
                return;
            }

            _metaData.SetEntityName(map.Value.Owner, $"Hub-{session.Name}");
            mapId = map.Value.Comp.MapId;
            gridUid = grid.Value.Owner;
        }

        var profile = EntityManager.System<GameTicker>().GetPlayerProfile(session);
        var coords = GetSpawnCoordinates(gridUid);

        var entity = SpawnCharacter(coords, profile, session);

        AttachCharacter(session, entity);

        _hubs[session] = new HubRecord
        {
            Map = mapId,
            Grid = gridUid,
            Entity = entity,
            DressedFrom = profile,
        };

        RaiseNetworkEvent(new HubEnteredEvent(), session);
    }

    private EntityCoordinates GetSpawnCoordinates(EntityUid gridUid)
    {
        var points = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<HubSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == gridUid)
                points.Add(xform.Coordinates);
        }

        if (points.Count > 0)
            return _random.Pick(points);

        var spawnPos = Comp<MapGridComponent>(gridUid).LocalAABB.Center;
        return new EntityCoordinates(gridUid, spawnPos);
    }

    private bool EnsureSharedHub()
    {
        if (_sharedHub != null && _mapManager.MapExists(_sharedHub.Value.Map))
            return true;

        var loadOptions = new DeserializationOptions { InitializeMaps = true };
        if (!_loader.TryLoadGrid(HubGridPath, out var map, out var grid, loadOptions))
            return false;

        _metaData.SetEntityName(map.Value.Owner, "Hub");
        _sharedHub = (map.Value.Comp.MapId, grid.Value.Owner);
        return true;
    }

    private EntityUid SpawnCharacter(EntityCoordinates coords, HumanoidCharacterProfile profile, ICommonSession session)
    {
        EntityUid entity;

        if (!_cfg.GetCVar(HorizonCCVars.HubEnableGear))
        {
            entity = _stationSpawning.SpawnPlayerMob(coords, job: null, profile: profile, station: null, session: session);
        }
        else
        {
            var jobId = GetPreferredJobWithLoadout(profile);
            entity = _stationSpawning.SpawnPlayerMob(coords, job: jobId, profile: profile, station: null, session: session);
            EquipHeadset(entity);
        }

        return entity;
    }

    private void EquipHeadset(EntityUid entity)
    {
        if (_inventory.TryGetSlotEntity(entity, "ears", out var existing))
        {
            _inventory.TryUnequip(entity, entity, "ears", force: true, silent: true);
            QueueDel(existing.Value);
        }

        var headset = Spawn(HubHeadsetPrototype, Transform(entity).Coordinates);
        if (!_inventory.TryEquip(entity, entity, headset, "ears", force: true, silent: true))
            QueueDel(headset);
    }

    private ProtoId<JobPrototype>? GetPreferredJobWithLoadout(HumanoidCharacterProfile profile)
    {
        var ordered = profile.JobPriorities
            .Where(kv => kv.Value != JobPriority.Never)
            .OrderByDescending(kv => kv.Value);

        foreach (var (jobId, _) in ordered)
        {
            if (!_prototypeManager.TryIndex(jobId, out var job))
                continue;

            if (_prototypeManager.HasIndex<RoleLoadoutPrototype>(LoadoutSystem.GetJobPrototype(job.ID)))
                return jobId;
        }

        return null;
    }

    private void DespawnHub(ICommonSession session)
    {
        if (!_hubs.Remove(session, out var hub))
            return;

        DetachMind(hub.Entity);
        if (Exists(hub.Entity))
            QueueDel(hub.Entity);

        DeleteHubMapIfPrivate(hub.Map);
    }

    private void DeleteHubMapIfPrivate(MapId map)
    {
        var isSharedHub = _sharedHub != null && _sharedHub.Value.Map == map;
        if (!isSharedHub && _mapManager.MapExists(map))
            _map.DeleteMap(map);
    }
}
