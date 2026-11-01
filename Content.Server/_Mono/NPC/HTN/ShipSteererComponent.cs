using Robust.Shared.Map;

namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Added to entities that are steering their ship parent.
/// </summary>
[RegisterComponent]
public sealed partial class ShipSteererComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public ShipSteeringStatus Status = ShipSteeringStatus.Moving;

    /// <summary>
    /// End target that we're trying to move to.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityCoordinates Coordinates;

    /// <summary>
    /// Whether to keep facing target if backing off due to RangeTolerance.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool AlwaysFaceTarget = false;

    /// <summary>
    /// Whether to avoid shipgun projectiles.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool AvoidProjectiles = false;

    /// <summary>
    /// If AlwaysFaceTarget is true, how much of a difference in angle (in radians) to accept.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float AlwaysFaceTargetOffset = 0.0333f; // Mono RotationTolerance

    /// <summary>
    /// Whether to avoid obstacles.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool AvoidCollisions = true;

    /// <summary>
    /// Try to evade collisions this far into the future even if stationary.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float BaseEvasionTime = 8f;

    /// <summary>
    /// How unwilling we are to use brake to adjust our velocity. Higher means less willing.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float BrakeThreshold = 0.75f;

    /// <summary>
    /// How much larger to consider the ship for collision evasion purposes.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float EvasionBuffer = 8f;

    /// <summary>
    /// How many evasion sectors to init on the outer ring.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int EvasionSectorCount = 24;

    /// <summary>
    /// How many layers of evasion sectors to have.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int EvasionSectorDepth = 2;

    /// <summary>
    /// Whether to consider the movement finished if we collide with target.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool FinishOnCollide = true;

    /// <summary>
    /// How much to enlarge grid search bounds for collision evasion (lateral padding).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float GridSearchBuffer = 128f;

    /// <summary>
    /// How much to enlarge grid search forward distance for collision evasion.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float GridSearchDistanceBuffer = 128f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float GridObstacleClearance = 14f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float MinObstructorScanDistance = 400f;

    /// <summary>
    /// Up to how fast can we be going before being considered in range, if not null.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float? InRangeMaxSpeed = null;

    /// <summary>
    /// Global angle to rotate to while in range.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Angle? InRangeRotation = null;

    /// <summary>
    /// Whether to try to match velocity with target.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool LeadingEnabled = true;

    /// <summary>
    /// Max rotation rate to be considered stationary, if not null.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float? MaxRotateRate = null;

    /// <summary>
    /// Check for obstacles for collision avoidance at most this far.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float MaxObstructorDistance = 900f;

    /// <summary>
    /// Ignore obstacles this close to our destination grid if moving to a grid, + other grid's radius.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float MinObstructorDistance = 20f;

    /// <summary>
    /// Don't finish early even if we've completed our order.
    /// Use to keep doing collision detection when we're supposed to finish on plan finish.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool NoFinish = false;

    /// <summary>
    /// What movement behavior to use.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public ShipSteeringMode Mode = ShipSteeringMode.GoToRange;

    /// <summary>
    /// How much to angularly offset our movement target on orbit movement mode.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Angle OrbitOffset = Angle.FromDegrees(30f);

    /// <summary>
    /// In what radius to search for projectiles in for collision evasion.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float ProjectileSearchBounds = 896f;

    /// <summary>
    /// How close are we trying to get to the coordinates before being considered in range.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float Range = 5f;

    /// <summary>
    /// At most how far to stay from the desired range. If null, will consider the movement finished while in range.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float? RangeTolerance = null;

    /// <summary>
    /// Accumulator for an integral of our rotational offset to target.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float RotationCompensation = 0f;

    /// <summary>
    /// How fast to accumulate the rotational offset integral, rad/s/rad (also affected by sqrt of angular acceleration).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float RotationCompensationGain = 0.03f;

    /// <summary>
    /// Target rotation in relation to movement direction.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float TargetRotation = 0f;

    /// <summary>
    /// Controls how much to ease in when turning with really high angular accelerations.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float TurnEaseIn = 0.2f;

    // Horizon: blocked-path handling

    /// <summary>
    /// Whether to come to a full stop before finishing, and to stop and give up when the path is blocked:
    /// a collision can't be evaded for <see cref="EmergencyAbortTime"/> or we make no progress for <see cref="ProgressTimeout"/>.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool FullStop = false;

    /// <summary>
    /// How long we may be emergency braking (no evasion direction is clear) before giving up.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float EmergencyAbortTime = 4f;

    /// <summary>
    /// How long we may go without getting <see cref="ProgressDistance"/> closer to the target before giving up.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float ProgressTimeout = 30f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float ProgressDistance = 10f;

    [ViewVariables]
    public float EmergencyTime;

    [ViewVariables]
    public float ProgressTimer;

    [ViewVariables]
    public float BestDistance = float.PositiveInfinity;

    /// <summary>
    /// We are braking to a full stop, after which <see cref="Status"/> becomes
    /// <see cref="ShipSteeringStatus.Blocked"/> if <see cref="HaltBlocked"/>, else <see cref="ShipSteeringStatus.InRange"/>.
    /// </summary>
    [ViewVariables]
    public bool Halting;

    [ViewVariables]
    public bool HaltBlocked;
}

public enum ShipSteeringStatus : byte
{
    /// <summary>
    /// Moving towards target
    /// </summary>
    Moving,

    /// <summary>
    /// Meeting set end conditions
    /// </summary>
    InRange,

    /// <summary>
    /// Horizon: gave up because the path is blocked, and came to a full stop
    /// </summary>
    Blocked
}

public enum ShipSteeringMode
{
    GoToRange,
    Orbit,
    OrbitCW
}
