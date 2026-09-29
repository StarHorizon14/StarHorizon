using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Horizon.TheEndEvent;

/// <summary>
/// Fullscreen static-noise vignette that closes in on the player as the end event ramps up,
/// independent of any entity component — driven directly by <see cref="TheEndEventSystem"/>.
/// Drawn in screen space against the viewport's pixel bounds so it reliably covers every
/// corner regardless of camera zoom or aspect ratio (world space left gaps at wide aspects).
/// </summary>
public sealed class TheEndOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> DistortionStaticShader = "DistortionStatic";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly ShaderInstance _shader;

    public float Intensity;

    // The shader always leaves a clean "peephole" around dist=0 by design (clearRadius can't
    // go below MinClearRadius), so a straight ramp of Intensity alone never covers the exact
    // center of the screen. Ramping MinClearRadius down into negative territory alongside it
    // pushes clearRadius below zero once Intensity nears 1, closing that last gap.
    private const float MaxClearRadius = 1.5f;
    private const float EdgeWidth = 0.15f;
    private const float MinClearRadiusAtFull = -EdgeWidth;

    public TheEndOverlay()
    {
        IoCManager.InjectDependencies(this);

        _shader = _prototypeManager.Index(DistortionStaticShader).InstanceUnique();
        _shader.SetParameter("MaxClearRadius", MaxClearRadius);
        _shader.SetParameter("EdgeWidth", EdgeWidth);
        _shader.SetParameter("MaxAlpha", 1.0f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Intensity > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = new UIBox2(args.ViewportBounds.Left, args.ViewportBounds.Top, args.ViewportBounds.Right, args.ViewportBounds.Bottom);

        _shader.SetParameter("MinClearRadius", MinClearRadiusAtFull * Intensity);
        _shader.SetParameter("Intensity", Intensity);
        handle.UseShader(_shader);
        handle.DrawRect(bounds, Color.White);
        handle.UseShader(null);
    }
}
