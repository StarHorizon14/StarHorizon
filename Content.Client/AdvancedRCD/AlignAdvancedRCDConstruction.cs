using System.Numerics;
using Content.Client.Hands.Systems;
using Content.Shared.AdvancedRCD;
using Content.Shared.AdvancedRCD.Components;
using Content.Shared.AdvancedRCD.Systems;
using Content.Shared.Physics;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Client.AdvancedRCD;

public sealed class AlignAdvancedRCDConstruction : PlacementMode
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    private readonly SharedMapSystem _mapSystem;
    private readonly SharedAdvancedRCDSystem _advRcdSystem;
    private readonly SharedTransformSystem _transformSystem;
    private readonly EntityLookupSystem _lookup;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    private readonly HandsSystem _hands;

    private const float SearchBoxSize = 2f;
    private const float PlaceColorBaseAlpha = 0.5f;

    private HashSet<EntityUid> _intersectingEntities = new();

    public AlignAdvancedRCDConstruction(PlacementManager pMan) : base(pMan)
    {
        IoCManager.InjectDependencies(this);
        _mapSystem = _entityManager.System<SharedMapSystem>();
        _advRcdSystem = _entityManager.System<SharedAdvancedRCDSystem>();
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _lookup = _entityManager.System<EntityLookupSystem>();
        _hands = _entityManager.System<HandsSystem>();

        // Use same transparent color for both valid and invalid placement
        ValidPlaceColor = ValidPlaceColor.WithAlpha(PlaceColorBaseAlpha);
        InvalidPlaceColor = ValidPlaceColor.WithAlpha(PlaceColorBaseAlpha);
    }

    public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
    {
        var mouseCoords = ScreenToCursorGrid(mouseScreen);
        MouseCoords = mouseCoords.AlignWithClosestGridTile(SearchBoxSize, _entityManager, _mapManager);

        var gridId = _transformSystem.GetGrid(MouseCoords);

        if (!_entityManager.TryGetComponent<MapGridComponent>(gridId, out var mapGrid))
            return;

        CurrentTile = _mapSystem.GetTileRef(gridId.Value, mapGrid, MouseCoords);

        float tileSize = mapGrid.TileSize;
        GridDistancing = tileSize;

        MouseCoords = new EntityCoordinates(MouseCoords.EntityId, new Vector2(
            CurrentTile.X + tileSize / 2 + pManager.PlacementOffset.X,
            CurrentTile.Y + tileSize / 2 + pManager.PlacementOffset.Y));
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;

        if (!_entityManager.TryGetComponent<TransformComponent>(player, out var xform))
            return false;

        // Determine if player is carrying an Advanced RCD in their active hand
        if (!_hands.TryGetActiveItem(player.Value, out var activeEnt))
            return false;

        var heldEntity = activeEnt;

        if (!_entityManager.TryGetComponent<AdvancedRCDComponent>(heldEntity, out var advRcd))
            return false;

        // Check range
        if (!_transformSystem.InRange(xform.Coordinates, position, advRcd.Range))
            return false;

        var gridUid = _transformSystem.GetGrid(position);
        if (!_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var mapGrid))
            return false;

        var tile = _mapSystem.GetTileRef(gridUid.Value, mapGrid, position);
        var posVector = _mapSystem.TileIndicesFor(gridUid.Value, mapGrid, position);

        // Check if we have a selected RCD prototype or entity prototype from boards
        bool isEntityFromBoard = false;

        if (advRcd.ProtoId != null)
        {
            // Get the RCD prototype
            if (!_prototypeManager.TryIndex<AdvancedRCDPrototype>(advRcd.ProtoId.Value, out var rcdProto))
                return false;

            // Deconstruct mode doesn't need placement validation
            if (rcdProto.IsDeconstruct)
                return false;

            // Check if we can afford (RCD prototype)
            if (!_advRcdSystem.CanAfford(heldEntity.Value, rcdProto))
                return false;
        }
        else if (advRcd.SelectedEntityProto != null)
        {
            isEntityFromBoard = true;

            // Check if we can afford (entity from board)
            if (!_advRcdSystem.CanAffordEntity(heldEntity.Value, advRcd, advRcd.SelectedEntityProto.Value))
                return false;
        }
        else
        {
            // No selection
            return false;
        }

        // Can't build on empty tile
        if (tile.Tile.IsEmpty)
            return false;

        // Check for obstructions (skip for entities from boards - they have different rules)
        if (!isEntityFromBoard)
        {
            _intersectingEntities.Clear();
            _lookup.GetLocalEntitiesIntersecting(gridUid.Value, posVector, _intersectingEntities, -0.05f, LookupFlags.Uncontained);

            foreach (var ent in _intersectingEntities)
            {
                if (_entityManager.TryGetComponent<FixturesComponent>(ent, out var fixtures))
                {
                    foreach (var fixture in fixtures.Fixtures.Values)
                    {
                        if (!fixture.Hard || fixture.CollisionLayer <= 0)
                            continue;

                        if ((fixture.CollisionLayer & (int)CollisionGroup.Impassable) != 0)
                            return false;
                    }
                }
            }
        }

        return true;
    }
}
