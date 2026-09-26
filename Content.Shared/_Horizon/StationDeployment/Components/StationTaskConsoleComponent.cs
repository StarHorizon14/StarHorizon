using Robust.Shared.Utility;

namespace Content.Shared._Horizon.StationDeployment.Components;

/// <summary>
/// The station task console: lets a player browse active station orders, summon a cargo capsule
/// to a tagged dock port on the console's own grid, and recall it to evaluate/consume its contents.
/// </summary>
[RegisterComponent]
public sealed partial class StationTaskConsoleComponent : Component
{
    /// <summary>
    /// Path to the pre-built cargo capsule grid map file to load on summon.
    /// </summary>
    [DataField]
    public ResPath CapsulePath = new("/Maps/_Horizon/CustomStation/tradedrop.yml");

    /// <summary>
    /// How long (seconds) the capsule's FTL trip to the station takes.
    /// </summary>
    [DataField]
    public float CapsuleTravelTime = 10f;

    /// <summary>
    /// Credits deducted from the station's bank account each time the capsule is summoned.
    /// </summary>
    [DataField]
    public int SummonCost = 10000;

    /// <summary>
    /// Minimum time between capsule summons.
    /// </summary>
    [DataField]
    public TimeSpan SummonCooldown = TimeSpan.FromMinutes(1);
}
