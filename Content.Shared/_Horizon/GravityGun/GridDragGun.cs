using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.GravityGun;

/// <summary>
/// Lets a held gun drag whole grids with the mouse, the way the admin griddrag command does: hold the use key over a
/// grid and it follows the cursor, let go and it keeps the speed of the hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GridDragGunComponent : Component
{
    /// <summary>
    /// How far from the user the grabbed point of the grid may be, in metres.
    /// </summary>
    [DataField]
    public float MaxDistance = 15f;
}

/// <summary>
/// The client starts dragging a grid by the point of it under the cursor.
/// </summary>
[Serializable, NetSerializable]
public sealed class GridDragGunStartRequest : EntityEventArgs
{
    public NetEntity Grid;
    public Vector2 LocalPoint;
}

/// <summary>
/// The client asks for the origin of the grid to be at a position.
/// </summary>
[Serializable, NetSerializable]
public sealed class GridDragGunPositionRequest : EntityEventArgs
{
    public NetEntity Grid;
    public Vector2 WorldPosition;
}

/// <summary>
/// The client let go: the grid goes on with this velocity.
/// </summary>
[Serializable, NetSerializable]
public sealed class GridDragGunStopRequest : EntityEventArgs
{
    public NetEntity Grid;
    public Vector2 LinearVelocity;
}
