using System.Numerics;
using Content.Shared._Horizon.Shuttles;

namespace Content.Client._Horizon.Shuttles;

/// <summary>Sends planet camera requests (see <see cref="PlanetCameraEvent"/>) from the shuttle console MAP tab.</summary>
public sealed class PlanetCameraClientSystem : EntitySystem
{
    public void Send(Vector2? position)
    {
        RaiseNetworkEvent(new PlanetCameraEvent(position));
    }
}
