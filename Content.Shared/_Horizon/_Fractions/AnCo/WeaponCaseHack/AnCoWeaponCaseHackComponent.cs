namespace Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;

/// <summary>
/// Component for AnCo weapon cases that can be hacked via the "tracks" minigame.
/// At every node the player chooses between track B and track C; the serial number of
/// exactly one of them satisfies the shown condition. Uses LockComponent for actual locking.
/// </summary>
[RegisterComponent]
public sealed partial class AnCoWeaponCaseHackComponent : Component
{
    /// <summary>
    /// Number of attempts the hacker has.
    /// </summary>
    [DataField]
    public int MaxAttempts = 3;

    /// <summary>
    /// Number of nodes (forks) on the way to the lock.
    /// </summary>
    [DataField]
    public int NodeCount = 6;

    /// <summary>
    /// Remaining attempts. Server-side state, not networked.
    /// </summary>
    [ViewVariables]
    public int Attempts;

    /// <summary>
    /// Index of the node the hacker currently stands at (0 = right after the start).
    /// </summary>
    [ViewVariables]
    public int NodeIndex;

    /// <summary>
    /// Generated track graph. Empty until the first hack attempt.
    /// </summary>
    [ViewVariables]
    public List<AnCoHackNode> Nodes = new();

    /// <summary>
    /// Dead ends the hacker has already hit; these are shown on the map.
    /// </summary>
    [ViewVariables]
    public List<AnCoHackDeadEnd> DeadEnds = new();

    /// <summary>
    /// Explosion intensity (total energy).
    /// </summary>
    [DataField]
    public float ExplosionIntensity = 5f;

    /// <summary>
    /// Explosion slope (how fast intensity drops off).
    /// </summary>
    [DataField]
    public float ExplosionSlope = 1f;

    /// <summary>
    /// Maximum explosion tile intensity.
    /// </summary>
    [DataField]
    public float ExplosionMaxTileIntensity = 3f;

    /// <summary>
    /// Explosion prototype ID.
    /// </summary>
    [DataField]
    public string ExplosionPrototype = "Default";
}

/// <summary>
/// A fork of the track graph: the current track A splits into B and C.
/// </summary>
public sealed class AnCoHackNode
{
    public string RuleId = string.Empty;
    public string SerialB = string.Empty;
    public string SerialC = string.Empty;

    /// <summary>
    /// True if track B continues to the next node (its serial satisfies the rule), otherwise track C does.
    /// The other track is a dead end.
    /// </summary>
    public bool CorrectIsB;
}
