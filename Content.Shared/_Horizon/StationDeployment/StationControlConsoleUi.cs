using Content.Shared._Horizon.StationDeployment.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.StationDeployment;

[Serializable, NetSerializable]
public enum StationControlConsoleUiKey : byte
{
    Key
}

/// <summary>
/// One purchasable upgrade as shown in the console - whether it's unlocked (development level met)
/// and affordable (enough station funds) are precomputed server-side.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct StationUpgradePurchaseUiEntry(
    ProtoId<StationUpgradePurchasePrototype> Id,
    int RequiredLevel,
    int CurrentLevel,
    int Price,
    bool Unlocked,
    bool Affordable,
    int? Limit,
    int Purchased);

[Serializable, NetSerializable]
public sealed class StationControlConsoleBuiState : BoundUserInterfaceState
{
    public readonly string StationName;

    /// <summary>
    /// The station's own bank account balance (credited by capsule sales) and whether the ATM
    /// controls should currently be enabled, plus the value of any cash inserted into the slot.
    /// </summary>
    public readonly int Balance;
    public readonly bool BankEnabled;
    public readonly int Deposit;

    /// <summary>
    /// The station grid's current IFF color, as a hex string (e.g. "#FFFFFFFF").
    /// </summary>
    public readonly string IffColorHex;

    public readonly List<StationUpgradePurchaseUiEntry> Upgrades;

    public StationControlConsoleBuiState(
        string stationName,
        int balance,
        bool bankEnabled,
        int deposit,
        string iffColorHex,
        List<StationUpgradePurchaseUiEntry> upgrades)
    {
        StationName = stationName;
        Balance = balance;
        BankEnabled = bankEnabled;
        Deposit = deposit;
        IffColorHex = iffColorHex;
        Upgrades = upgrades;
    }
}

[Serializable, NetSerializable]
public sealed class StationControlConsoleRenameMessage : BoundUserInterfaceMessage
{
    public readonly string Name;

    public StationControlConsoleRenameMessage(string name)
    {
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class StationControlConsoleSetIffColorMessage : BoundUserInterfaceMessage
{
    public readonly string ColorHex;

    public StationControlConsoleSetIffColorMessage(string colorHex)
    {
        ColorHex = colorHex;
    }
}

[Serializable, NetSerializable]
public sealed class StationControlConsolePurchaseUpgradeMessage : BoundUserInterfaceMessage
{
    public readonly ProtoId<StationUpgradePurchasePrototype> PurchaseId;

    public StationControlConsolePurchaseUpgradeMessage(ProtoId<StationUpgradePurchasePrototype> purchaseId)
    {
        PurchaseId = purchaseId;
    }
}
