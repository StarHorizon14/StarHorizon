using System.Linq;
using System.Numerics;
using Content.Shared.Conveyor;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Horizon.Materials;

public abstract class TileReclaimerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ConveyorComponent> _conveyorQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _conveyorQuery = GetEntityQuery<ConveyorComponent>();

        SubscribeLocalEvent<TileReclaimerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<TileReclaimerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextRecycle = _timing.CurTime + ent.Comp.RecycleDelay;
        DirtyField(ent.Owner, ent.Comp, nameof(TileReclaimerComponent.NextRecycle));
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TileReclaimerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tileReclaimer, out var xform))
        {
            if (tileReclaimer.NextRecycle > _timing.CurTime)
                continue;

            Reclaim((uid, tileReclaimer, xform));
        }
    }

    private void Reclaim(Entity<TileReclaimerComponent, TransformComponent> ent)
    {
        var (uid, reclaimer, xform) = ent;

        reclaimer.NextRecycle += reclaimer.RecycleDelay;
        DirtyField(uid, reclaimer, nameof(TileReclaimerComponent.NextRecycle));

        if (!reclaimer.Enabled || reclaimer.Broken)
            return;

        if (!_conveyorQuery.TryComp(uid, out var conveyor) || conveyor.State == ConveyorState.Off)
            return;

        if (xform.GridUid is not { } reclaimerGrid)
            return;

        var mapId = xform.MapID;
        var worldMatrix = _transform.GetWorldMatrix(uid);
        var bounds = worldMatrix.TransformBox(reclaimer.RecyclingBox);

        var grids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(mapId, bounds, ref grids);

        var shredded = false;
        var intersectingEntities = new HashSet<EntityUid>();

        foreach (var grid in grids)
        {
            if (TerminatingOrDeleted(grid) || grid.Owner == reclaimerGrid)
                continue;

            if (!_whitelist.CheckBoth(grid, reclaimer.Blacklist, reclaimer.Whitelist))
                continue;

            foreach (var tile in _mapSystem.GetTilesIntersecting(grid.Owner, grid.Comp, bounds).ToList())
            {
                var tileDef = _turf.GetContentTileDefinition(tile);
                if (tileDef.Indestructible)
                    continue;

                intersectingEntities.Clear();
                _lookup.GetLocalEntitiesIntersecting(tile.GridUid, tile.GridIndices, intersectingEntities);

                foreach (var entityOnTile in intersectingEntities)
                {
                    if (!_physicsQuery.TryComp(entityOnTile, out var physicsComp))
                    {
                        QueueDel(entityOnTile);
                        continue;
                    }

                    _physics.SetCanCollide(entityOnTile, true, body: physicsComp);
                    _physics.ApplyLinearImpulse(entityOnTile,
                        _physics.GetLinearVelocity(grid.Owner, Transform(entityOnTile).LocalPosition),
                        body: physicsComp);
                }

                _mapSystem.SetTile(grid.Owner, grid.Comp, tile.GridIndices, Tile.Empty);
                SpawnMaterialsFromComposition((uid, null, xform), tileDef, reclaimer.Efficiency, grid.Owner); // Horizon: grid for shipbreaking jobs
                shredded = true;

                if (TerminatingOrDeleted(grid))
                    break;

                // Horizon: towards the machine from the middle of the box, so a box on either side of it pulls inwards.
                var pull = -reclaimer.RecyclingBox.Center;
                if (pull != Vector2.Zero && _physicsQuery.TryComp(grid.Owner, out var gridPhysics) && gridPhysics.Mass > 0f)
                {
                    var direction = _transform.GetWorldRotation(xform).RotateVec(Vector2.Normalize(pull));
                    var speed = Vector2.Dot(gridPhysics.LinearVelocity, direction);
                    var gain = MathF.Min(reclaimer.SlurpStrength / gridPhysics.Mass, reclaimer.SlurpMaxSpeed - speed);
                    if (gain > 0f)
                        _physics.ApplyLinearImpulse(grid.Owner, direction * gain * gridPhysics.Mass, body: gridPhysics);
                }
            }
        }

        if (shredded)
            _audio.PlayPvs(reclaimer.Sound, uid);
    }

    protected virtual void SpawnMaterialsFromComposition(Entity<MaterialStorageComponent?, TransformComponent?> ent,
        ContentTileDefinition tileDefinition,
        float efficiency,
        EntityUid sourceGrid)
    {
    }
}
