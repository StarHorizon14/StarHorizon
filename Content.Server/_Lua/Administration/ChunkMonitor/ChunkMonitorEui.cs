// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using System.Linq;
using System.Numerics;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Worldgen;
using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Components.Debris;
using Content.Shared._Lua.Administration.ChunkMonitor;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._Lua.Administration.ChunkMonitor;

public sealed class ChunkMonitorEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    private readonly TransformSystem _xform;
    private readonly EntityQuery<WorldControllerComponent> _controllerQuery;
    private readonly EntityQuery<LoadedChunkComponent> _loadedQuery;
    private readonly EntityQuery<WorldChunkComponent> _worldChunkQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<ActorComponent> _actorQuery;
    private readonly EntityQuery<MapGridComponent> _gridQuery;
    private readonly EntityQuery<ShuttleDeedComponent> _shuttleDeedQuery;
    private readonly EntityQuery<StationMemberComponent> _stationMemberQuery;
    private readonly EntityQuery<OwnedDebrisComponent> _debrisQuery;
    private NetEntity _selectedMap = NetEntity.Invalid;
    private readonly Dictionary<EntityUid, int> _deletedByMapSession = new();
    private ChunkMonitorChunkInfo[] _chunks = [];
    private int _loadedCount;
    private int _unloadedCount;

    public ChunkMonitorEui()
    {
        IoCManager.InjectDependencies(this);
        _xform = _entMan.System<TransformSystem>();
        _controllerQuery = _entMan.GetEntityQuery<WorldControllerComponent>();
        _loadedQuery = _entMan.GetEntityQuery<LoadedChunkComponent>();
        _worldChunkQuery = _entMan.GetEntityQuery<WorldChunkComponent>();
        _xformQuery = _entMan.GetEntityQuery<TransformComponent>();
        _actorQuery = _entMan.GetEntityQuery<ActorComponent>();
        _gridQuery = _entMan.GetEntityQuery<MapGridComponent>();
        _shuttleDeedQuery = _entMan.GetEntityQuery<ShuttleDeedComponent>();
        _stationMemberQuery = _entMan.GetEntityQuery<StationMemberComponent>();
        _debrisQuery = _entMan.GetEntityQuery<OwnedDebrisComponent>();
    }

    public override void Opened()
    {
        base.Opened();
        if (!EnsureAuthorized())
            return;
        var maps = GetMaps();
        if (maps.Length == 0)
        {
            Close();
            return;
        }
        var preferred = Player.AttachedEntity;
        if (preferred != null && _xformQuery.TryGetComponent(preferred.Value, out var xform) && xform.MapUid is { } mapUid)
            _selectedMap = _entMan.GetNetEntity(mapUid);
        if (!_selectedMap.IsValid())
            _selectedMap = maps[0].MapUid;
        _chunks = [];
        _loadedCount = 0;
        _unloadedCount = 0;
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (!EnsureAuthorized())
            return;
        switch (msg)
        {
            case ChunkMonitorEuiMsg.RequestMapData req:
                if (!_entMan.TryGetEntity(req.MapUid, out EntityUid? mapUidNullable) ||
                    mapUidNullable is not { } mapUid ||
                    !_controllerQuery.HasComponent(mapUid))
                    return;
                _selectedMap = req.MapUid;
                RefreshMapData(mapUid);
                StateDirty();
                break;
            case ChunkMonitorEuiMsg.DeleteChunks del:
                {
                    if (!_entMan.TryGetEntity(del.MapUid, out EntityUid? mapEntityNullable) ||
                        mapEntityNullable is not { } mapEntityUid ||
                        !_controllerQuery.TryGetComponent(mapEntityUid, out var controller))
                        return;
                    IReadOnlyDictionary<Vector2i, EntityUid> chunks = controller.Chunks;
                    var deleted = 0;
                    foreach (var coords in del.Chunks)
                    {
                        if (!chunks.TryGetValue(coords, out var chunkUid))
                            continue;
                        if (_loadedQuery.HasComponent(chunkUid))
                            _entMan.RemoveComponent<LoadedChunkComponent>(chunkUid);
                        _entMan.QueueDeleteEntity(chunkUid);
                        deleted++;
                    }
                    if (deleted > 0)
                    {
                        _deletedByMapSession.TryGetValue(mapEntityUid, out var prev);
                        _deletedByMapSession[mapEntityUid] = prev + deleted;
                    }
                    if (_selectedMap == del.MapUid)
                    {
                        RefreshMapData(mapEntityUid);
                        StateDirty();
                    }
                    break;
                }
            case ChunkMonitorEuiMsg.PurgeChunks purge:
                {
                    if (!_entMan.TryGetEntity(purge.MapUid, out EntityUid? mapEntityNullable) ||
                        mapEntityNullable is not { } mapEntityUid ||
                        !_controllerQuery.HasComponent(mapEntityUid) ||
                        !_entMan.TryGetComponent(mapEntityUid, out MapComponent? mapComp))
                        return;
                    foreach (var coords in purge.Chunks)
                    {
                        PurgeChunk(mapEntityUid, mapComp.MapId, coords);
                    }
                    if (_selectedMap == purge.MapUid)
                    {
                        RefreshMapData(mapEntityUid);
                        StateDirty();
                    }
                    break;
                }
        }
    }

    public override EuiStateBase GetNewState()
    {
        var maps = GetMaps();
        var deletedCount = 0;
        if (_entMan.TryGetEntity(_selectedMap, out EntityUid? mapUidNullable) && mapUidNullable is { } mapUid)
            _deletedByMapSession.TryGetValue(mapUid, out deletedCount);
        return new ChunkMonitorEuiState(
            _selectedMap,
            maps,
            _chunks,
            _loadedCount,
            _unloadedCount,
            deletedCount);
    }

    private bool EnsureAuthorized()
    {
        if (_admins.HasAdminFlag(Player, AdminFlags.Admin))
            return true;
        Close();
        return false;
    }

    private ChunkMonitorMapInfo[] GetMaps()
    {
        var list = new List<ChunkMonitorMapInfo>();
        var e = _entMan.EntityQueryEnumerator<MapComponent, MetaDataComponent>();
        while (e.MoveNext(out var uid, out var mapComp, out var meta))
        {
            if (mapComp.MapId == MapId.Nullspace)
                continue;
            list.Add(new ChunkMonitorMapInfo(_entMan.GetNetEntity(uid), meta.EntityName));
        }
        return list.OrderBy(m => m.Name).ToArray();
    }

    private void RefreshMapData(EntityUid mapUid)
    {
        _loadedCount = 0;
        _unloadedCount = 0;
        if (!_controllerQuery.TryGetComponent(mapUid, out var controller))
        {
            _chunks = [];
            return;
        }
        var chunkInfos = new List<ChunkMonitorChunkInfo>(controller.Chunks.Count);
        var entityCountByChunk = new Dictionary<Vector2i, int>();
        var playerCountByChunk = new Dictionary<Vector2i, int>();
        var shuttleCountByChunk = new Dictionary<Vector2i, int>();
        var stationCountByChunk = new Dictionary<Vector2i, int>();
        var debrisCountByChunk = new Dictionary<Vector2i, int>();
        var gridCountByChunk = new Dictionary<Vector2i, int>();
        var xformEnum = _entMan.EntityQueryEnumerator<TransformComponent>();
        while (xformEnum.MoveNext(out var uid, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;
            if (_worldChunkQuery.HasComponent(uid))
                continue;
            var worldPos = _xform.GetWorldPosition(xform);
            var chunk = WorldGen.WorldToChunkCoords(worldPos).Floored();
            Increment(entityCountByChunk, chunk);
            if (_actorQuery.HasComponent(uid))
                Increment(playerCountByChunk, chunk);
            if (!_gridQuery.HasComponent(uid))
                continue;
            if (_stationMemberQuery.HasComponent(uid))
                Increment(stationCountByChunk, chunk);
            else if (_shuttleDeedQuery.HasComponent(uid))
                Increment(shuttleCountByChunk, chunk);
            else if (_debrisQuery.HasComponent(uid))
                Increment(debrisCountByChunk, chunk);
            else
                Increment(gridCountByChunk, chunk);
        }
        foreach (var (coords, chunkUid) in controller.Chunks)
        {
            var loaded = _loadedQuery.HasComponent(chunkUid);
            var status = loaded ? ChunkMonitorChunkStatus.Loaded : ChunkMonitorChunkStatus.Unloaded;
            if (loaded) _loadedCount++; else _unloadedCount++;
            entityCountByChunk.TryGetValue(coords, out var entities);
            playerCountByChunk.TryGetValue(coords, out var players);
            shuttleCountByChunk.TryGetValue(coords, out var shuttles);
            stationCountByChunk.TryGetValue(coords, out var stations);
            debrisCountByChunk.TryGetValue(coords, out var debris);
            gridCountByChunk.TryGetValue(coords, out var grids);
            chunkInfos.Add(new ChunkMonitorChunkInfo(coords, status, entities, players, shuttles, stations, debris, grids));
        }
        _chunks = chunkInfos.ToArray();
    }

    private static void Increment(Dictionary<Vector2i, int> dict, Vector2i key)
    {
        dict.TryGetValue(key, out var value);
        dict[key] = value + 1;
    }

    private void PurgeChunk(EntityUid mapUid, MapId mapId, Vector2i coords)
    {
        var worldMin = new Vector2(coords.X * WorldGen.ChunkSize, coords.Y * WorldGen.ChunkSize);
        var worldMax = worldMin + new Vector2(WorldGen.ChunkSize, WorldGen.ChunkSize);
        var box = new Box2(worldMin, worldMax);
        var grids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(mapId, box, ref grids);
        foreach (var grid in grids)
        {
            if (grid.Owner == mapUid)
                continue;
            _entMan.QueueDeleteEntity(grid.Owner);
        }
        var q = _entMan.EntityQueryEnumerator<TransformComponent>();
        while (q.MoveNext(out var uid, out var xform))
        {
            if (uid == mapUid)
                continue;
            if (xform.MapUid != mapUid)
                continue;
            if (_worldChunkQuery.HasComponent(uid))
                continue;
            if (_gridQuery.HasComponent(uid))
                continue;
            if (!box.Contains(_xform.GetWorldPosition(xform)))
                continue;
            _entMan.QueueDeleteEntity(uid);
        }
    }
}
