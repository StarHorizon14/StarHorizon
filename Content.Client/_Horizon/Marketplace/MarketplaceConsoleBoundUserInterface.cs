using Content.Shared._Horizon.Marketplace;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon.Marketplace;

public sealed class MarketplaceConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MarketplaceConsoleWindow? _window;

    public MarketplaceConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MarketplaceConsoleWindow>();
        if (EntMan.TryGetComponent<MarketplaceConsoleComponent>(Owner, out var console) && console.BlackMarket)
            _window.ApplyTheme(MarketplaceTheme.BlackMarket);

        _window.OnList += (amount, price) => SendMessage(new MarketplaceListItemMessage(amount, price));
        _window.OnBuy += (id, amount, price) => SendMessage(new MarketplaceBuyMessage(id, amount, price));
        _window.OnWithdraw += id => SendMessage(new MarketplaceWithdrawMessage(id));
        _window.OnEject += () => SendMessage(new MarketplaceEjectMessage());
        MarketplaceExamineHub.Clear();
        MarketplaceExamineHub.Requester = item => SendMessage(new MarketplaceExamineRequestMessage(item));
        _window.OnCreateRequest += (title, description, quantity, reward) =>
            SendMessage(new MarketplaceCreateRequestMessage(title, description, quantity, reward));
        _window.OnCancelRequest += id => SendMessage(new MarketplaceCancelRequestMessage(id));
        _window.OnMakeOffer += (id, amount) => SendMessage(new MarketplaceMakeOfferMessage(id, amount));
        _window.OnAcceptOffer += id => SendMessage(new MarketplaceAcceptOfferMessage(id));
        _window.OnRejectOffer += id => SendMessage(new MarketplaceRejectOfferMessage(id));
        _window.OnWithdrawOffer += id => SendMessage(new MarketplaceWithdrawOfferMessage(id));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        MarketplaceExamineHub.Requester = null;
        MarketplaceExamineHub.Clear();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        switch (message)
        {
            case MarketplaceConsoleState state:
                MarketplaceExamineHub.Clear();
                _window?.UpdateState(state);
                break;
            case MarketplaceExamineResponseMessage examine:
                MarketplaceExamineHub.OnResponse(examine.Item, examine.Text);
                break;
        }
    }
}
