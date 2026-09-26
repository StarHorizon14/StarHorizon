using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Horizon.StationDeployment.Components;

/// <summary>
/// A hand-held kit that deploys a new station grid after being linked to an ID card.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationDeploymentKitComponent : Component
{
    /// <summary>
    /// The ID card that was swiped on this kit, binding the future station to it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedIdCard;

    /// <summary>
    /// Path to the pre-built grid map file to load when deploying.
    /// </summary>
    [DataField]
    public ResPath StationGridPath = new("/Maps/_Horizon/CustomStation/station.yml");

    /// <summary>
    /// Id of the <c>gameMap</c> prototype carrying the <see cref="Content.Shared.Station.StationConfig"/> for the deployed station.
    /// </summary>
    [DataField]
    public string StationGameMapId = "DeployedStation";

    /// <summary>
    /// Key in the gameMap's Stations dictionary to look up the StationConfig.
    /// </summary>
    [DataField]
    public string StationConfigKey = "DeployedStation";

    /// <summary>
    /// Radius (in metres) around the deploy position that must be clear of other grids.
    /// </summary>
    [DataField]
    public float DeployRange = 150f;

    /// <summary>
    /// Minimum mass a nearby dynamic grid must have to block deployment.
    /// </summary>
    [DataField]
    public float MinShipMass = 50f;

    /// <summary>
    /// How long the deploy DoAfter takes.
    /// </summary>
    [DataField]
    public TimeSpan DeployDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier SwipeSound = new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/airlock_deny.ogg");

    [DataField]
    public SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
}
