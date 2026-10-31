using System.Numerics;
using Content.Shared._Horizon.GravityGun;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.GravityGun;

/// <summary>
/// Client side of <see cref="GridDragGunComponent"/>, copied from the griddrag command: while the use key is held over a
/// grid with the gun in hand, the grid is dragged with the cursor.
/// </summary>
public sealed class GridDragGunSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityUid? _dragging;
    private Vector2 _localPosition;
    private MapCoordinates? _lastMousePosition;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        if (_player.LocalEntity is not { } player ||
            !_hands.TryGetActiveItem(player, out var item) ||
            !HasComp<GridDragGunComponent>(item) ||
            _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use) != BoundKeyState.Down)
        {
            StopDragging();
            return;
        }

        var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);

        if (_dragging == null)
        {
            if (!_mapManager.TryFindGridAt(mousePos, out var gridUid, out _) || LooseThingUnder(mousePos, player))
                return;

            StartDragging(gridUid, Vector2.Transform(mousePos.Position, _transform.GetInvWorldMatrix(gridUid)));
        }

        if (!TryComp(_dragging, out TransformComponent? xform) || xform.MapID != mousePos.MapId)
        {
            StopDragging();
            return;
        }

        var localToWorld = Vector2.Transform(_localPosition, _transform.GetWorldMatrix(xform));
        if (localToWorld.EqualsApprox(mousePos.Position, 0.01f))
            return;

        var requestedGridOrigin = mousePos.Position - _transform.GetWorldRotation(xform).RotateVec(_localPosition);
        _lastMousePosition = new MapCoordinates(requestedGridOrigin, mousePos.MapId);

        RaiseNetworkEvent(new GridDragGunPositionRequest
        {
            Grid = GetNetEntity(_dragging.Value),
            WorldPosition = requestedGridOrigin,
        });
    }

    /// <summary>
    /// A click on something loose (an item to tether) is not a grab of the grid under it.
    /// </summary>
    private bool LooseThingUnder(MapCoordinates mousePos, EntityUid player)
    {
        var box = Box2.CenteredAround(mousePos.Position, new Vector2(0.6f, 0.6f));
        foreach (var uid in _lookup.GetEntitiesIntersecting(mousePos.MapId, box, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (uid != player && !Transform(uid).Anchored && !HasComp<MapGridComponent>(uid))
                return true;
        }

        return false;
    }

    private void StartDragging(EntityUid grid, Vector2 localPosition)
    {
        _dragging = grid;
        _localPosition = localPosition;

        RaiseNetworkEvent(new GridDragGunStartRequest
        {
            Grid = GetNetEntity(grid),
            LocalPoint = localPosition,
        });
    }

    private void StopDragging()
    {
        if (_dragging is not { } grid)
            return;

        var velocity = Vector2.Zero;
        if (_lastMousePosition != null && TryComp(grid, out TransformComponent? xform) &&
            HasComp<PhysicsComponent>(grid) && xform.MapID == _lastMousePosition.Value.MapId)
        {
            var distance = _lastMousePosition.Value.Position - _transform.GetWorldPosition(xform);
            if (distance.LengthSquared() > 0f)
                velocity = distance / (float) _timing.TickPeriod.TotalSeconds * 0.25f;
        }

        RaiseNetworkEvent(new GridDragGunStopRequest
        {
            Grid = GetNetEntity(grid),
            LinearVelocity = velocity,
        });

        _dragging = null;
        _localPosition = Vector2.Zero;
        _lastMousePosition = null;
    }
}
