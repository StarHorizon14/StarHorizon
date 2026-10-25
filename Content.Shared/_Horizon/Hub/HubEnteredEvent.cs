using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Hub;

[Serializable, NetSerializable]
public sealed class HubEnteredEvent : EntityEventArgs;
