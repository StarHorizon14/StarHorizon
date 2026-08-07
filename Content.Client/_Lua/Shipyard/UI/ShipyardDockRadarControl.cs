// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Shuttles.UI;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Client._Lua.Shipyard.UI;

public sealed class ShipyardDockRadarControl : ShuttleNavControl
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly SharedTransformSystem _transform;
    private static readonly Color ShipyardWallColorSrgb = Color.ToSrgb(Color.FromHex("#404040"));
    protected override Color RadarEquatorialLineColor => ShipyardWallColorSrgb;
    protected override Color RadarRadialLineColor => ShipyardWallColorSrgb;
    protected override bool ShowRadarPositionMarker => false;
    private EntityCoordinates? _baseCoords;
    private Angle? _baseAngle;
    private Vector2 _pan;
    private bool _panning;
    private bool _mouseDown;
    private Vector2 _mouseDownPos;
    private float _dragAccumulatedPx;
    private const float PanClamp = 150f;
    private const float DragThresholdPx = 6f;

    public ShipyardDockRadarControl() : base()
    {
        IoCManager.InjectDependencies(this);
        _transform = EntManager.System<SharedTransformSystem>();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        const string text = "Выберите стыковочный порт";
        var padding = 6f * UIScale;
        var dims = handle.GetDimensions(Font, text, UIScale);
        var pos = new Vector2(PixelWidth - dims.X - padding, padding);
        handle.DrawString(Font, pos, text, UIScale, Color.White.WithAlpha(0.9f));
        DrawLocalPlayerMarker(handle);
    }

    private void DrawLocalPlayerMarker(DrawingHandleScreen handle)
    {
        if (_coordinates == null || _rotation == null) return;
        var playerEnt = _player.LocalSession?.AttachedEntity;
        if (playerEnt == null) return;
        var xformQuery = EntManager.GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(_coordinates.Value.EntityId, out var anchorXform) || anchorXform.MapID == MapId.Nullspace) { return; }
        if (!xformQuery.TryGetComponent(playerEnt.Value, out var playerXform) || playerXform.MapID != anchorXform.MapID) { return; }
        var worldRot = _rotation.Value;
        var mapPos = _transform.ToMapCoordinates(_coordinates.Value);
        var worldToShuttle = Matrix3Helpers.CreateTranslation(-mapPos.Position) * Matrix3Helpers.CreateRotation(-worldRot);
        var shuttleToView = Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale)) * Matrix3x2.CreateTranslation(MidPointVector);
        var playerWorldPos = _transform.GetWorldPosition(playerEnt.Value);
        var p = Vector2.Transform(playerWorldPos, worldToShuttle * shuttleToView);
        const float radius = 5f;
        var fill = Color.ToSrgb(Color.Cyan).WithAlpha(0.9f);
        var outline = Color.Black.WithAlpha(0.8f);
        handle.DrawCircle(p, radius, fill, filled: true);
        handle.DrawCircle(p, radius, outline, filled: false);
    }

    public new void UpdateState(NavInterfaceState state)
    {
        base.UpdateState(state);
        _baseCoords = EntManager.GetCoordinates(state.Coordinates);
        _baseAngle = state.Angle;
        ApplyPan();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _mouseDown = true;
            _mouseDownPos = args.PointerLocation.Position;
            _panning = false;
            _dragAccumulatedPx = 0f;
            return;
        }
        base.KeyBindDown(args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _mouseDown = false;
            if (_panning)
            {
                _panning = false;
                args.Handle();
                return;
            }
            base.KeyBindUp(args);
            return;
        }
        base.KeyBindUp(args);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_baseCoords == null || _baseAngle == null) return;
        if (_mouseDown && !_panning)
        {
            _dragAccumulatedPx += new Vector2(args.Relative.X, args.Relative.Y).Length();
            if (_dragAccumulatedPx >= DragThresholdPx) _panning = true;
        }
        if (!_panning) return;
        if (MidPoint.X <= 0 || MidPoint.Y <= 0) return;
        // Lua: the radar view is already rendered in the station's local (rotated) space
        // (see base Draw's worldToShuttle), and _baseCoords.Offset() below adds in that same
        // local space too - so the screen-space drag delta needs no extra rotation here.
        var delta = new Vector2(args.Relative.X, -args.Relative.Y) / MidPoint * WorldRange;
        _pan -= delta;
        _pan = new Vector2( Math.Clamp(_pan.X, -PanClamp, PanClamp), Math.Clamp(_pan.Y, -PanClamp, PanClamp));
        ApplyPan();
    }

    private void ApplyPan()
    {
        if (_baseCoords == null) return;
        Offset = Vector2.Zero;
        TargetOffset = Vector2.Zero;
        SetMatrix(_baseCoords.Value.Offset(_pan), _baseAngle);
    }
}
