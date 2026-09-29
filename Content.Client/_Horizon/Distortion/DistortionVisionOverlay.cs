using Content.Shared._Horizon.Distortion.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Horizon.Distortion;

/// <summary>
/// Draws a vignette of static around the edges of the screen for a player standing on a
/// shuttle caught in a distortion field that has its <c>AffectPlayers</c> option enabled.
/// Starts semi-transparent with a clear patch in the middle, but the closer the player
/// gets the more it closes in and darkens, up to 80% opacity at max intensity.
/// Layered with a fainter, farther-reaching ring confined to the very edges of the screen
/// as an early warning.
/// </summary>
public sealed class DistortionVisionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> DistortionStaticShader = "DistortionStatic";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _innerShader;
    private readonly ShaderInstance _outerShader;
    private float _intensity;
    private float _outerIntensity;

    public DistortionVisionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _innerShader = _prototypeManager.Index(DistortionStaticShader).InstanceUnique();
        // Unlike the console overlay, the player's own screen closes the peephole all the
        // way at max intensity (still capped at 80% opacity, same as the shared default).
        _innerShader.SetParameter("MinClearRadius", 0.0f);
        _innerShader.SetParameter("MaxAlpha", 0.8f);

        _outerShader = _prototypeManager.Index(DistortionStaticShader).InstanceUnique();
        _outerShader.SetParameter("MinClearRadius", 0.75f);
        _outerShader.SetParameter("MaxClearRadius", 1.3f);
        _outerShader.SetParameter("EdgeWidth", 0.25f);
        _outerShader.SetParameter("MaxAlpha", 0.45f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var playerEntity = _playerManager.LocalSession?.AttachedEntity;

        if (!_entityManager.TryGetComponent(playerEntity, out DistortionVisionComponent? vision) ||
            (vision.Intensity <= 0f && vision.OuterIntensity <= 0f))
        {
            return false;
        }

        _intensity = vision.Intensity;
        _outerIntensity = vision.OuterIntensity;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;

        if (_outerIntensity > 0f)
        {
            _outerShader.SetParameter("Intensity", _outerIntensity);
            worldHandle.UseShader(_outerShader);
            worldHandle.DrawRect(args.WorldBounds, Color.White);
        }

        if (_intensity > 0f)
        {
            _innerShader.SetParameter("Intensity", _intensity);
            worldHandle.UseShader(_innerShader);
            worldHandle.DrawRect(args.WorldBounds, Color.White);
        }

        worldHandle.UseShader(null);
    }
}
