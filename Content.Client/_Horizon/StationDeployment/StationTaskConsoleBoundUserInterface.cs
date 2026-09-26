using Content.Shared._Horizon.StationDeployment;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon.StationDeployment;

public sealed class StationTaskConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private StationTaskConsoleWindow? _window;

    public StationTaskConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<StationTaskConsoleWindow>();
        _window.OnSummonPressed += () => SendMessage(new StationOrderSummonCapsuleMessage());
        _window.OnRecallPressed += () => SendMessage(new StationOrderRecallCapsuleMessage());
        _window.OnOrderCancelPressed += orderId => SendMessage(new StationOrderCancelMessage(orderId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not StationTaskConsoleBuiState cast || _window == null)
            return;

        _window.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _window?.Close();
    }
}
