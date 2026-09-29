using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.SlotMachine;

[Serializable, NetSerializable]
public sealed partial class SlotMachineDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class SlotMachineEmagDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CoinFlipperDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ClawGameDoAfterEvent : SimpleDoAfterEvent;
