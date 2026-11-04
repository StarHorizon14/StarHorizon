using System.Numerics;
using Content.Shared._Horizon.GravityGun;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.GravityGun;

public sealed class GridDragGunSystem : SharedGridDragGunSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private EntityUid? _dragging;
    private Vector2 _localPosition;
    private MapCoordinates? _lastMousePosition;

    private void StartDragging(EntityUid grid, Vector2 localPosition)
    {
        _dragging = grid;
        _localPosition = localPosition;

        if (HasComp<PhysicsComponent>(grid))
        {
            RaiseNetworkEvent(new GridDragVelocityRequest()
            {
                Grid = GetNetEntity(grid),
                LinearVelocity = Vector2.Zero
            });
        }
    }

    private void StopDragging()
    {
        if (_dragging == null) return;

        if (_lastMousePosition != null && TryComp(_dragging.Value, out TransformComponent? xform) &&
            TryComp<PhysicsComponent>(_dragging.Value, out _) &&
            xform.MapID == _lastMousePosition.Value.MapId)
        {
            var tickTime = _gameTiming.TickPeriod;
            var distance = _lastMousePosition.Value.Position - _transformSystem.GetWorldPosition(xform);
            RaiseNetworkEvent(new GridDragVelocityRequest()
            {
                Grid = GetNetEntity(_dragging.Value),
                LinearVelocity = distance.LengthSquared() > 0f ? (distance / (float) tickTime.TotalSeconds) * 0.25f : Vector2.Zero,
            });
        }

        _dragging = null;
        _localPosition = Vector2.Zero;
        _lastMousePosition = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted) return;

        GridDragGunComponent? gun = null;
        var holding = _player.LocalEntity is { } player && _hands.TryGetActiveItem(player, out var item) &&
                      TryComp(item, out gun);
        var state = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);

        if (!holding || state != BoundKeyState.Down)
        {
            StopDragging();
            return;
        }

        var mouseScreenPos = _inputManager.MouseScreenPosition;
        var mousePos = _eyeManager.PixelToMap(mouseScreenPos);

        if (_dragging == null)
        {
            if (HasLooseThingAt(_player.LocalEntity!.Value, mousePos))
                return;

            if (!_mapManager.TryFindGridAt(mousePos, out var gridUid, out var grid) ||
                !CanGrab(_player.LocalEntity!.Value, gun!, gridUid))
                return;

            StartDragging(gridUid, Vector2.Transform(mousePos.Position, _transformSystem.GetInvWorldMatrix(gridUid)));
        }

        if (_dragging != null && !CanGrab(_player.LocalEntity!.Value, gun!, _dragging.Value))
        {
            StopDragging();
            return;
        }

        if (!TryComp(_dragging, out TransformComponent? xform))
        {
            StopDragging();
            return;
        }

        if (xform.MapID != mousePos.MapId)
        {
            StopDragging();
            return;
        }

        var requestedGridOrigin = mousePos.Position - _transformSystem.GetWorldRotation(xform).RotateVec(_localPosition);
        _lastMousePosition = new MapCoordinates(requestedGridOrigin, mousePos.MapId);

        RaiseNetworkEvent(new GridDragRequestPosition()
        {
            Grid = GetNetEntity(_dragging.Value),
            WorldPosition = requestedGridOrigin,
        });
    }

    private bool HasLooseThingAt(EntityUid user, MapCoordinates point)
    {
        foreach (var uid in _lookup.GetEntitiesIntersecting(point, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (uid == user || !HasComp<ItemComponent>(uid) || _mapManager.IsGrid(uid) || Transform(uid).Anchored ||
                _container.IsEntityInContainer(uid))
                continue;

            if (!TryComp<PhysicsComponent>(uid, out var physics) || physics.BodyType == BodyType.Static)
                continue;

            if (_mobState.IsAlive(uid))
                continue;

            return true;
        }

        return false;
    }
}
