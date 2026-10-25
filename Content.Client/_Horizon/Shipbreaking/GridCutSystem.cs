using Robust.Client.Graphics;

namespace Content.Client._Horizon.Shipbreaking;

public sealed class GridCutSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new GridCutOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<GridCutOverlay>();
    }
}
