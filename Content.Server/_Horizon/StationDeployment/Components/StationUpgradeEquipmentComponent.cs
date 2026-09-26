using Content.Shared._NF.BindToStation;
using Robust.Shared.Audio;

namespace Content.Server._Horizon.StationDeployment.Components;

/// <summary>
/// Marks a piece of station upgrade equipment (bought via the station control console, delivered as
/// a flatpack and unpacked by the crew) that must be activated with the station owner's ID card
/// before it works. Which station it's bound to comes from <see cref="StationBoundObjectComponent"/>,
/// carried over from the purchase's flatpack when it's unpacked.
/// </summary>
[RegisterComponent]
public sealed partial class StationUpgradeEquipmentComponent : Component
{
    [DataField]
    public bool Installed;

    [DataField]
    public SoundSpecifier ActivateSound = new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/airlock_deny.ogg");
}
