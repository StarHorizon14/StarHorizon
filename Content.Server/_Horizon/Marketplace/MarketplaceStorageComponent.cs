using Robust.Shared.Network;

namespace Content.Server._Horizon.Marketplace;

/// <summary>
/// Hidden entity in null-space that holds every listed item of the sector-wide marketplace for the current round.
/// </summary>
[RegisterComponent]
public sealed partial class MarketplaceStorageComponent : Component
{
    public const string ContainerId = "marketplace_storage";

    /// <summary>
    /// Which marketplace network this storage belongs to.
    /// </summary>
    [ViewVariables]
    public string Network = string.Empty;

    /// <summary>
    /// Random per-round value used to derive the codes shown instead of names on anonymous networks.
    /// </summary>
    [ViewVariables]
    public int Salt;

    [ViewVariables]
    public Dictionary<int, MarketplaceListing> Listings = new();

    [ViewVariables]
    public int NextId = 1;

    /// <summary>
    /// Earnings of sellers who were offline when their goods sold. Paid out the next time they open a marketplace console.
    /// </summary>
    [ViewVariables]
    public Dictionary<(NetUserId UserId, string CharacterName), int> PendingPayouts = new();

    [ViewVariables]
    public Dictionary<int, MarketplaceRequest> Requests = new();

    [ViewVariables]
    public int NextRequestId = 1;

    [ViewVariables]
    public Dictionary<int, MarketplaceOffer> Offers = new();

    [ViewVariables]
    public int NextOfferId = 1;
}

/// <summary>
/// Someone looking for an item. The reward is already withdrawn from the requester and held here.
/// </summary>
public sealed class MarketplaceRequest
{
    [ViewVariables] public int Id;
    [ViewVariables] public string Title = string.Empty;
    [ViewVariables] public string Description = string.Empty;
    [ViewVariables] public int Quantity;
    [ViewVariables] public int Reward;
    [ViewVariables] public NetUserId RequesterUserId;
    [ViewVariables] public string RequesterCharacterName = string.Empty;
    [ViewVariables] public string RequesterIdName = string.Empty;
    [ViewVariables] public string GridName = string.Empty;
    [ViewVariables] public TimeSpan CreatedAt;
}

/// <summary>
/// An item offered for a request. The item is held in the marketplace storage until accepted or taken back.
/// </summary>
public sealed class MarketplaceOffer
{
    [ViewVariables] public int Id;
    [ViewVariables] public int RequestId;
    [ViewVariables] public string RequestTitle = string.Empty;
    [ViewVariables] public int RequestReward;
    [ViewVariables] public EntityUid Item;
    [ViewVariables] public string Name = string.Empty;
    [ViewVariables] public string? ProtoId;
    [ViewVariables] public bool Stackable;
    [ViewVariables] public NetUserId OffererUserId;
    [ViewVariables] public string OffererCharacterName = string.Empty;
    [ViewVariables] public string OffererIdName = string.Empty;
    [ViewVariables] public string GridName = string.Empty;
    [ViewVariables] public Content.Shared._Horizon.Marketplace.MarketplaceOfferStatus Status;
}

public sealed class MarketplaceListing
{
    [ViewVariables]
    public int Id;

    /// <summary>
    /// The listed entities. A merged stack listing may hold several stacks once a single stack's limit is reached.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> Items = new();

    [ViewVariables]
    public string Name = string.Empty;

    [ViewVariables]
    public string? ProtoId;

    /// <summary>
    /// Stack type if the listing is a stackable resource, otherwise null.
    /// </summary>
    [ViewVariables]
    public string? StackType;

    [ViewVariables]
    public int UnitPrice;

    [ViewVariables]
    public NetUserId SellerUserId;

    [ViewVariables]
    public string SellerCharacterName = string.Empty;

    [ViewVariables]
    public string SellerIdName = string.Empty;

    [ViewVariables]
    public string GridName = string.Empty;

    [ViewVariables]
    public TimeSpan ListedAt;
}
