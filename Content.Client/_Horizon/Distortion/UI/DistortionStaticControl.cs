using Content.Shared._Horizon.Distortion.Components;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Horizon.Distortion.UI;

/// <summary>
/// Transparent overlay control that glitches out a shuttle console's screen with a
/// vignette of static, scaling with how close the shuttle's grid currently is to a
/// distortion field. Place it as the topmost layer over a console window's contents.
/// Layered with a fainter, farther-reaching ring confined to the edges of the control
/// as an early warning.
/// </summary>
public sealed class DistortionStaticControl : Control
{
    private static readonly ProtoId<ShaderPrototype> DistortionStaticShader = "DistortionStatic";

    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly ShaderInstance _innerShader;
    private readonly ShaderInstance _outerShader;

    /// <summary>
    /// The console entity whose grid should be checked for interference.
    /// </summary>
    public EntityUid? Console;

    public DistortionStaticControl()
    {
        IoCManager.InjectDependencies(this);

        _innerShader = _proto.Index(DistortionStaticShader).InstanceUnique();

        _outerShader = _proto.Index(DistortionStaticShader).InstanceUnique();
        _outerShader.SetParameter("MinClearRadius", 0.75f);
        _outerShader.SetParameter("MaxClearRadius", 1.3f);
        _outerShader.SetParameter("EdgeWidth", 0.25f);
        _outerShader.SetParameter("MaxAlpha", 0.45f);

        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var (intensity, outerIntensity) = GetIntensity();

        if (outerIntensity > 0f)
        {
            _outerShader.SetParameter("Intensity", outerIntensity);
            handle.UseShader(_outerShader);
            handle.DrawRect(PixelSizeBox, Color.White);
        }

        if (intensity > 0f)
        {
            _innerShader.SetParameter("Intensity", intensity);
            handle.UseShader(_innerShader);
            handle.DrawRect(PixelSizeBox, Color.White);
        }

        handle.UseShader(null);
    }

    private (float Intensity, float OuterIntensity) GetIntensity()
    {
        if (Console is not { } console || !_entManager.EntityExists(console))
            return (0f, 0f);

        // The console itself may be directly affected (e.g. a handheld mass scanner
        // picking up interference on its own), or it may be a console mounted on a
        // shuttle whose whole grid is affected.
        var intensity = 0f;
        var outerIntensity = 0f;

        if (_entManager.TryGetComponent(console, out DistortionAffectedComponent? direct))
        {
            intensity = direct.Intensity;
            outerIntensity = direct.OuterIntensity;
        }

        if (_entManager.TryGetComponent(console, out TransformComponent? xform) &&
            xform.GridUid is { } grid &&
            _entManager.TryGetComponent(grid, out DistortionAffectedComponent? affected))
        {
            intensity = MathF.Max(intensity, affected.Intensity);
            outerIntensity = MathF.Max(outerIntensity, affected.OuterIntensity);
        }

        return (intensity, outerIntensity);
    }
}
