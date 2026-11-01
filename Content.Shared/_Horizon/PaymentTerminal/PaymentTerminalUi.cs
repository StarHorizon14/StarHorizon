using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.PaymentTerminal;

[Serializable, NetSerializable]
public enum PaymentTerminalUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum PaymentTerminalMode : byte
{
    /// <summary>
    /// The owner sets the price once and it stays until they clear it; buyers only add a tip.
    /// </summary>
    Fixed,

    /// <summary>
    /// The buyer enters both the price and the tip.
    /// </summary>
    Free,
}

/// <summary>
/// What the keypad is currently entering. Each step is confirmed with the enter key.
/// </summary>
[Serializable, NetSerializable]
public enum PaymentTerminalStage : byte
{
    Price,
    Tip,

    /// <summary>
    /// Price and tip are confirmed, waiting for an ID swipe.
    /// </summary>
    Ready,
}

[Serializable, NetSerializable]
public sealed class PaymentTerminalBoundUserInterfaceState(
    string? ownerName,
    PaymentTerminalMode mode,
    PaymentTerminalStage stage,
    int price,
    int tip,
    int tax,
    float taxRate) : BoundUserInterfaceState
{
    public readonly string? OwnerName = ownerName;
    public readonly PaymentTerminalMode Mode = mode;
    public readonly PaymentTerminalStage Stage = stage;
    public readonly int Price = price;
    public readonly int Tip = tip;
    public readonly int Tax = tax;
    public readonly float TaxRate = taxRate;
}

[Serializable, NetSerializable]
public sealed class PaymentTerminalKeypadMessage(int digit) : BoundUserInterfaceMessage
{
    public readonly int Digit = digit;
}

[Serializable, NetSerializable]
public sealed class PaymentTerminalClearMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PaymentTerminalEnterMessage : BoundUserInterfaceMessage;
