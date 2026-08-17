using Robust.Shared.Utility;

namespace Content.Server._Horizon.StationEvents.Components;

[RegisterComponent]
public sealed partial class TraderShuttleArrivalRuleComponent : Component
{
    [DataField]
    public ResPath ShuttlePath = new("/Maps/_Horizon/Lostvoid/Shuttle/kvazar.yml");

    [DataField]
    public string TargetStationName = "Заброшеная станция";

    [DataField]
    public EntityUid? ShuttleGrid;
}
