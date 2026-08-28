namespace Content.Server._Horizon.StationEvents.Components;

/// <summary>
/// Spawns a procedural planet + dungeon (generated the same way the salvage expedition console
/// does), reachable by any shuttle as a public FTL destination for the duration of the event.
/// Always generated at the highest (Extreme) difficulty.
/// </summary>
[RegisterComponent]
public sealed partial class LostvoidExpeditionRuleComponent : Component
{
    /// <summary>
    /// Map generated for the current run of the event, if any.
    /// </summary>
    [DataField]
    public EntityUid? MapUid;

    /// <summary>
    /// Whether new shuttles have already been blocked from FTLing to the expedition
    /// (starts 5 minutes before the event ends).
    /// </summary>
    [DataField]
    public bool ArrivalsBlocked;
}
