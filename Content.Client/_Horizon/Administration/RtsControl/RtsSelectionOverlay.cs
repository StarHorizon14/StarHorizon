using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.Administration.RtsControl;

/// <summary>
///     Draws the selection box, selected creatures and the last order marker.
/// </summary>
public sealed class RtsSelectionOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly RtsControlSystem _system;
    private readonly SharedTransformSystem _xform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public RtsSelectionOverlay(RtsControlSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _xform = _entManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        // Selected creatures.
        foreach (var uid in _system.Selected)
        {
            if (!_entManager.TryGetComponent(uid, out TransformComponent? xform) || xform.MapID != args.MapId)
                continue;

            handle.DrawCircle(_xform.GetWorldPosition(xform), 0.45f, Color.Lime.WithAlpha(0.8f), false);
        }

        // Drag box.
        if (_system.DragStart is { } start && start.MapId == args.MapId)
        {
            var current = _eye.PixelToMap(_input.MouseScreenPosition);
            if (current.MapId == args.MapId)
            {
                var box = new Box2(Vector2.Min(start.Position, current.Position),
                    Vector2.Max(start.Position, current.Position));
                handle.DrawRect(box, Color.Lime.WithAlpha(0.15f));
                handle.DrawRect(box, Color.Lime.WithAlpha(0.9f), false);
            }
        }

        // Last order marker.
        if (_system.OrderMarker is { } marker &&
            marker.MapId == args.MapId &&
            _timing.CurTime < _system.OrderMarkerExpires)
        {
            handle.DrawCircle(marker.Position, 0.4f, Color.Red.WithAlpha(0.9f), false);
            handle.DrawCircle(marker.Position, 0.1f, Color.Red.WithAlpha(0.9f));
        }
    }
}
