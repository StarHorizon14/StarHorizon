using Content.Client.UserInterface.Fragments;
using Content.Shared._Horizon.Marketplace;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon.Marketplace;

/// <summary>
/// PDA program for a marketplace network: view listings and requests, post requests.
/// </summary>
public sealed partial class MarketplaceCartridgeUi : UIFragment
{
    private MarketplaceCartridgeFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var blackMarket = fragmentOwner is { } owner
                          && entMan.TryGetComponent<MarketplaceCartridgeComponent>(owner, out var cartridge)
                          && cartridge.BlackMarket;

        _fragment = new MarketplaceCartridgeFragment(blackMarket ? MarketplaceTheme.BlackMarket : MarketplaceTheme.Sector);
        _fragment.OnCreateRequest += (title, description, quantity, reward) =>
            userInterface.SendMessage(new CartridgeUiMessage(
                new MarketplaceCartridgeCreateRequestEvent(title, description, quantity, reward)));
        _fragment.OnAcceptOffer += (offerId, teleporter) =>
            userInterface.SendMessage(new CartridgeUiMessage(new MarketplaceCartridgeAcceptOfferEvent(offerId, teleporter)));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is MarketplaceCartridgeState cast)
            _fragment?.UpdateState(cast);
    }
}
