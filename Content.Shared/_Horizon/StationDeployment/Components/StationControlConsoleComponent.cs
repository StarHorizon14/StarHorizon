using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.StationDeployment.Components;

/// <summary>
/// The station control console: lets a player rename the deployed station and
/// deposit/withdraw from its own bank account (the one credited by capsule sales).
/// </summary>
[RegisterComponent]
public sealed partial class StationControlConsoleComponent : Component
{
    [DataField]
    public ProtoId<StackPrototype> CashType = "Credit";

    public static string CashSlotId = "station-control-console-cashSlot";

    [DataField]
    public ItemSlot CashSlot = new();

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}
