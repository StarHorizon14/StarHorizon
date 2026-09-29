using System.Numerics;
using Content.Shared._Horizon.Mirage;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Horizon.Mirage;

/// <summary>
/// Renders the area around every nearby mirage target into its own viewport,
/// <see cref="MirageBorderOverlay"/> then draws those on top of the borders.
/// </summary>
public sealed class MirageBorderSystem : SharedMirageBorderSystem
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// How much further than <see cref="MirageBorderComponent.ViewRange"/> a mirage is kept rendered,
    /// so it doesn't pop in and out right at the edge.
    /// </summary>
    private const float RangeMargin = 4f;

    internal readonly Dictionary<EntityUid, MirageView> Views = new();

    private readonly HashSet<EntityUid> _active = new();
    private readonly List<EntityUid> _toRemove = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MirageBorderComponent, ComponentShutdown>(OnShutdown);

        _overlay.AddOverlay(new MirageBorderOverlay(EntityManager, this));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<MirageBorderOverlay>();

        foreach (var view in Views.Values)
        {
            view.Viewport.Dispose();
        }

        Views.Clear();
    }

    private void OnShutdown(Entity<MirageBorderComponent> ent, ref ComponentShutdown args)
    {
        RemoveView(ent);
    }

    /// <summary>
    /// Whether the viewport is one of the mirage viewports. Used to stop mirages from rendering inside mirages.
    /// </summary>
    public bool IsMirageViewport(IClydeViewport viewport)
    {
        foreach (var view in Views.Values)
        {
            if (view.Viewport == viewport)
                return true;
        }

        return false;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _active.Clear();

        var eyePos = _eye.CurrentEye.Position;
        if (eyePos.MapId != MapId.Nullspace)
        {
            var query = EntityQueryEnumerator<MirageBorderComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var border, out var xform))
            {
                if (UpdateView((uid, border, xform), eyePos))
                    _active.Add(uid);
            }
        }

        _toRemove.Clear();
        foreach (var uid in Views.Keys)
        {
            if (!_active.Contains(uid))
                _toRemove.Add(uid);
        }

        foreach (var uid in _toRemove)
        {
            RemoveView(uid);
        }
    }

    private bool UpdateView(Entity<MirageBorderComponent, TransformComponent> ent, MapCoordinates eyePos)
    {
        var (uid, border, xform) = ent;

        if (xform.MapID != eyePos.MapId)
            return false;

        if (border.Target is not { } target || !TryComp(target, out TransformComponent? targetXform))
            return false;

        if (targetXform.MapID == MapId.Nullspace)
            return false;

        var borderPos = _transform.GetWorldPosition(xform);
        var range = border.ViewRange + RangeMargin + border.Size.Length() / 2f;
        if ((borderPos - eyePos.Position).LengthSquared() > range * range)
            return false;

        var size = Vector2.Clamp(border.Size, Vector2.One, new Vector2(MaxSize));
        var pixelSize = new Vector2i(
            (int) MathF.Ceiling(size.X * EyeManager.PixelsPerMeter),
            (int) MathF.Ceiling(size.Y * EyeManager.PixelsPerMeter));

        if (Views.TryGetValue(uid, out var view) && view.Viewport.Size != pixelSize)
        {
            RemoveView(uid);
            view = null;
        }

        if (view == null)
        {
            var eye = new FixedEye
            {
                DrawFov = false,
            };

            var viewport = _clyde.CreateViewport(pixelSize, $"Mirage-{uid}");
            viewport.Eye = eye;
            viewport.ClearColor = Color.Transparent;
            viewport.AutomaticRender = true;

            view = new MirageView(viewport, eye);
            Views[uid] = view;
        }

        // Look at the same area around the target as the mirage covers around the border, in the target's frame.
        var (targetPos, targetRot) = _transform.GetWorldPositionRotation(targetXform);
        view.Eye.Position = new MapCoordinates(targetPos + targetRot.RotateVec(border.Offset), targetXform.MapID);
        view.Eye.Rotation = -targetRot;

        return true;
    }

    private void RemoveView(EntityUid uid)
    {
        if (!Views.Remove(uid, out var view))
            return;

        view.Viewport.Dispose();
    }
}

internal sealed class MirageView(IClydeViewport viewport, FixedEye eye)
{
    public readonly IClydeViewport Viewport = viewport;
    public readonly FixedEye Eye = eye;
}
