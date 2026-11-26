using Content.Shared._Horizon.Marketplace;

namespace Content.Client._Horizon.Marketplace;

public sealed class MarketplaceSystem : SharedMarketplaceSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarketplaceListedItemComponent, ExamineRangeBypassEvent>(OnExamineBypass);
    }

    /// <summary>
    /// Listed items only reach the client while a marketplace console is open, and the server validates the request anyway.
    /// </summary>
    private void OnExamineBypass(Entity<MarketplaceListedItemComponent> ent, ref ExamineRangeBypassEvent args)
    {
        args.Allowed = true;
    }
}
