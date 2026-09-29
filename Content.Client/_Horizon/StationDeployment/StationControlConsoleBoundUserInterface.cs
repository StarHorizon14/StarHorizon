using Content.Shared._Horizon.StationDeployment;
using Content.Shared._NF.Bank.Events;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon.StationDeployment;

public sealed class StationControlConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private StationControlConsoleWindow? _window;

    public StationControlConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<StationControlConsoleWindow>();
        _window.OnNameChange += name => SendMessage(new StationControlConsoleRenameMessage(name));
        _window.OnWithdraw += amount => SendMessage(new StationBankWithdrawMessage(amount, null, null));
        _window.OnDeposit += amount => SendMessage(new StationBankDepositMessage(amount, null, null));
        _window.OnIffColorChange += colorHex => SendMessage(new StationControlConsoleSetIffColorMessage(colorHex));
        _window.OnPurchaseUpgrade += purchaseId => SendMessage(new StationControlConsolePurchaseUpgradeMessage(purchaseId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not StationControlConsoleBuiState cast || _window == null)
            return;

        _window.UpdateState(cast.StationName, cast.Balance, cast.BankEnabled, cast.Deposit, cast.IffColorHex, cast.Upgrades);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _window?.Close();
    }
}
