using System.Numerics;
using Content.Shared._Horizon.Mirage;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Horizon.Mirage;

/// <summary>
/// Draws the rendered mirage areas on top of their borders.
/// Drawn below FOV, so walls still block the view of a mirage.
/// </summary>
public sealed class MirageBorderOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    private readonly IEntityManager _entManager;
    private readonly MirageBorderSystem _mirage;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public MirageBorderOverlay(IEntityManager entManager, MirageBorderSystem mirage)
    {
        _entManager = entManager;
        _mirage = mirage;
        _transform = entManager.System<SharedTransformSystem>();
        _shader = IoCManager.Resolve<IPrototypeManager>().Index(UnshadedShader).Instance();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // Don't draw mirages inside of mirages, that would never end well.
        return _mirage.Views.Count > 0 && !_mirage.IsMirageViewport(args.Viewport);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        handle.UseShader(_shader);

        foreach (var (uid, view) in _mirage.Views)
        {
            if (!_entManager.TryGetComponent<MirageBorderComponent>(uid, out var border)
                || !_entManager.TryGetComponent<TransformComponent>(uid, out var xform)
                || xform.MapID != args.MapId)
                continue;

            var (pos, rot) = _transform.GetWorldPositionRotation(xform);
            handle.SetTransform(pos, rot);
            handle.DrawTextureRect(view.Viewport.RenderTarget.Texture, Box2.CenteredAround(border.Offset, Vector2.Clamp(border.Size, Vector2.One, new Vector2(SharedMirageBorderSystem.MaxSize))));
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }
}
