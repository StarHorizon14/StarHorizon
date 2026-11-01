using Content.Shared._Horizon.PaymentTerminal;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon.PaymentTerminal;

public sealed class PaymentTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private PaymentTerminalMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<PaymentTerminalMenu>();
        _menu.OnKeypadButtonPressed += digit => SendMessage(new PaymentTerminalKeypadMessage(digit));
        _menu.OnClearButtonPressed += () => SendMessage(new PaymentTerminalClearMessage());
        _menu.OnEnterButtonPressed += () => SendMessage(new PaymentTerminalEnterMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PaymentTerminalBoundUserInterfaceState terminalState)
            _menu?.UpdateState(terminalState);
    }
}
