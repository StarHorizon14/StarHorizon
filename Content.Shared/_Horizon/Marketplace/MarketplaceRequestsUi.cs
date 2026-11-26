using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Marketplace;

[Serializable, NetSerializable]
public enum MarketplaceOfferStatus : byte
{
    /// <summary>
    /// Waiting for the requester to accept or reject it.
    /// </summary>
    Pending,

    /// <summary>
    /// Rejected, or the request was closed. The item waits for its owner to take it back.
    /// </summary>
    Returned,
}

/// <summary>
/// A request ("buy order"): a player describes what they want, how many and the reward they pay for it.
/// The reward is held from the requester's account until the request is fulfilled or cancelled.
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceRequestData
{
    public int Id;
    public string Title = string.Empty;
    public string Description = string.Empty;
    public int Quantity;
    public int Reward;

    public string RequesterName = string.Empty;
    public string RequesterIdName = string.Empty;
    public string GridName = string.Empty;
    public TimeSpan CreatedAt;

    public bool IsOwn;
    public int OfferCount;

    /// <summary>
    /// Whether the viewer already has a pending offer on this request.
    /// </summary>
    public bool HasOwnOffer;

    /// <summary>
    /// Offers made on this request. Only sent to the requester.
    /// </summary>
    public List<MarketplaceOfferData> Offers = new();
}

/// <summary>
/// An item offered to fulfil a request.
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceOfferData
{
    public int Id;
    public int RequestId;
    public string RequestTitle = string.Empty;
    public int RequestReward;

    public string Name = string.Empty;
    public string? ProtoId;
    public NetEntity? DisplayEntity;
    public bool Stackable;
    public int Count;

    public string OffererName = string.Empty;
    public string OffererIdName = string.Empty;
    public string GridName = string.Empty;

    public MarketplaceOfferStatus Status;
}

[Serializable, NetSerializable]
public sealed class MarketplaceCreateRequestMessage(string title, string description, int quantity, int reward) : BoundUserInterfaceMessage
{
    public readonly string Title = title;
    public readonly string Description = description;
    public readonly int Quantity = quantity;
    public readonly int Reward = reward;
}

[Serializable, NetSerializable]
public sealed class MarketplaceCancelRequestMessage(int requestId) : BoundUserInterfaceMessage
{
    public readonly int RequestId = requestId;
}

/// <summary>
/// Offer the item in the console's slot for a request.
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceMakeOfferMessage(int requestId, int amount) : BoundUserInterfaceMessage
{
    public readonly int RequestId = requestId;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class MarketplaceAcceptOfferMessage(int offerId) : BoundUserInterfaceMessage
{
    public readonly int OfferId = offerId;
}

[Serializable, NetSerializable]
public sealed class MarketplaceRejectOfferMessage(int offerId) : BoundUserInterfaceMessage
{
    public readonly int OfferId = offerId;
}

/// <summary>
/// Take back an offered item, whether it is still pending or was returned.
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceWithdrawOfferMessage(int offerId) : BoundUserInterfaceMessage
{
    public readonly int OfferId = offerId;
}
