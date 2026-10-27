using Content.Shared.Shuttles.Systems;

namespace Content.Server._Horizon.Shuttles.Components;

/// <summary>
/// The opposite of <see cref="Content.Server.Shuttles.Components.FTLExclusionComponent"/>: marks an area shuttles may start an FTL jump from.
/// It only restricts departures from maps whose <c>FTLDestination</c> has <c>BeaconsOnly</c> set; on such a map, once at
/// least one enabled zone exists, shuttles outside every zone cannot jump.
/// </summary>
[RegisterComponent, Access(typeof(SharedShuttleSystem))]
public sealed partial class FTLInclusionComponent : Component
{
    [DataField]
    public bool Enabled = true;

    [DataField(required: true)]
    public float Range = 32f;
}
