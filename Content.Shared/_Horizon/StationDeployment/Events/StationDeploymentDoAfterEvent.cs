using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.StationDeployment.Events;

[Serializable, NetSerializable]
public sealed partial class StationDeploymentDoAfterEvent : SimpleDoAfterEvent;
