using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Horizon.Shuttles;

/// <summary>
/// The "someone is right next to the shuttle" rule of regular FTL, shared by the server (which enforces it) and the
/// client (which shows the FTL zone on the NAV tab).
/// </summary>
public static class FtlProximity
{
    /// <summary>How close another grid may be, measured from the shuttle's bounds.</summary>
    public const float Range = 150f;

    /// <summary>Free-floating dynamic grids lighter than this never block a jump.</summary>
    public const float MassThreshold = 50f;

    /// <summary>Shuttles with at most this many tiles skip the check.</summary>
    public const int ExemptTiles = 64;

    /// <summary>
    /// Whether <paramref name="body"/> blocks a jump of a neighbouring shuttle: stations, big ships and asteroids do,
    /// small free-floating dynamic pieces do not.
    /// </summary>
    public static bool Blocks(PhysicsComponent body)
    {
        return body.Mass >= MassThreshold || body.BodyType != BodyType.Dynamic;
    }
}
