using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Automender;

[Serializable, NetSerializable]
public sealed partial class AutomenderDoAfterEvent : SimpleDoAfterEvent
{
}
