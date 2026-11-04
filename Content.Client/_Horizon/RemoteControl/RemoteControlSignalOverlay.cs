using Content.Shared._Horizon.RemoteControl.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Horizon.RemoteControl;

/// <summary>
/// Interference static over the screen of a remotely controlled entity.
/// The further it is from the operator's body, the more the static closes in.
/// </summary>
public sealed class RemoteControlSignalOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> DistortionStaticShader = "DistortionStatic";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _shader;
    private float _intensity;

    public RemoteControlSignalOverlay()
    {
        IoCManager.InjectDependencies(this);

        _shader = _prototypeManager.Index(DistortionStaticShader).InstanceUnique();
        _shader.SetParameter("MinClearRadius", 0.1f);
        _shader.SetParameter("MaxAlpha", 0.85f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out UnderControlComponent? control)
            || control.SignalLoss <= 0f)
        {
            return false;
        }

        _intensity = control.SignalLoss;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        _shader.SetParameter("Intensity", _intensity);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
