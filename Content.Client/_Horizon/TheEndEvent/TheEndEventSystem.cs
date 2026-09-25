using Content.Shared._Horizon.TheEndEvent;
using Robust.Client.Graphics;
using Robust.Client.State;

namespace Content.Client._Horizon.TheEndEvent;

public sealed class TheEndEventSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;

    private TheEndOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new TheEndOverlay();
        _overlayMan.AddOverlay(_overlay);

        SubscribeNetworkEvent<TheEndEventProgressMessage>(OnProgress);
        SubscribeNetworkEvent<TheEndEventReachedMessage>(OnReached);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnProgress(TheEndEventProgressMessage message)
    {
        _overlay.Intensity = message.Intensity;
    }

    private void OnReached(TheEndEventReachedMessage message)
    {
        // The fake connecting scene is its own visual — the static overlay has done its job
        // getting there and would otherwise keep drawing (and fighting for screen space) on
        // top of it.
        _overlay.Intensity = 0f;
        _stateManager.RequestStateChange<TheEndConnectingState>();
    }
}
