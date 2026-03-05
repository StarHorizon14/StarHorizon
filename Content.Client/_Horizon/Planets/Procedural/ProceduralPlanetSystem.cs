using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Horizon.Planets.Procedural;

[UsedImplicitly]
public sealed class ProceduralPlanetSystem : EntitySystem
{
    [Dependency]
    private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new PlanetBackgroundOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay(new PlanetBackgroundOverlay());
    }
}
