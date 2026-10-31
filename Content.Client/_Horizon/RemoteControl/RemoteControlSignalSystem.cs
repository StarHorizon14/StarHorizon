using Robust.Client.Graphics;

namespace Content.Client._Horizon.RemoteControl;

/// <summary>
/// Keeps the remote control interference overlay registered; it only draws while the local player
/// controls something with a weakening signal.
/// </summary>
public sealed class RemoteControlSignalSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private RemoteControlSignalOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new RemoteControlSignalOverlay();
        _overlayMan.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay(_overlay);
    }
}
