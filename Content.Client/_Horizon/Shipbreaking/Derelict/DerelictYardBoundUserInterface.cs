using Content.Shared._Horizon.Shipbreaking.Derelict;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon.Shipbreaking.Derelict;

public sealed class DerelictYardBoundUserInterface : BoundUserInterface
{
    private DerelictYardMenu? _menu;

    public DerelictYardBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<DerelictYardMenu>();
        _menu.OnSelected += id => SendMessage(new DerelictPreviewRequestMessage(id));
        _menu.OnBuy += id => SendMessage(new DerelictPurchaseMessage(id));
        _menu.OnExtend += group => SendMessage(new DerelictExtendMessage(group));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DerelictYardState yard)
            _menu?.UpdateState(yard);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is DerelictPreviewMessage preview)
            _menu?.SetPreview(preview);
    }
}
