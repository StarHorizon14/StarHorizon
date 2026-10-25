using Content.Shared._Horizon.ShiftStartEffect;
using Robust.Client.Graphics;

namespace Content.Client._Horizon.ShiftStartEffect;

public sealed class ShiftStartIntroSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private ShiftStartIntroOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ShiftStartIntroEvent>(OnIntro);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<ShiftStartIntroOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_overlay is not { Finished: true })
            return;

        _overlayMan.RemoveOverlay(_overlay);
        _overlay = null;
    }

    private void OnIntro(ShiftStartIntroEvent ev)
    {
        _overlayMan.RemoveOverlay<ShiftStartIntroOverlay>();
        _overlay = new ShiftStartIntroOverlay(ev);
        _overlayMan.AddOverlay(_overlay);
    }
}
