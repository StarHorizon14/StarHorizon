using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Shuttles;

/// <summary>
/// Sent by a shuttle console MAP tab that shows the planet camera: asks the server to load (and send) the terrain
/// around <see cref="Position"/> on the planet map the player is on, so panning the camera reveals real ground.
/// A null position ends the request.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlanetCameraEvent(Vector2? position) : EntityEventArgs
{
    public readonly Vector2? Position = position;
}
