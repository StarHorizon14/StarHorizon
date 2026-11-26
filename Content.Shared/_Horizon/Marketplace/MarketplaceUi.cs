using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Marketplace;

[Serializable, NetSerializable]
public enum MarketplaceConsoleUiKey : byte
{
    Key,
}

/// <summary>
/// A single listing as shown to clients.
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceListingData
{
    public int Id;
    public string Name = string.Empty;
    public string? ProtoId;

    /// <summary>
    /// Entity shown in the UI and used for examining. May not have reached the client yet.
    /// </summary>
    public NetEntity? DisplayEntity;

    public bool Stackable;

    /// <summary>
    /// Stack type of the listed items, used to show when a new listing would merge with an existing one.
    /// </summary>
    public string? StackType;
    public int Count;
    public int UnitPrice;

    public string SellerName = string.Empty;
    public string SellerIdName = string.Empty;
    public string GridName = string.Empty;

    /// <summary>
    /// Whether the listing belongs to the character viewing the UI.
    /// </summary>
    public bool IsOwn;

    public TimeSpan ListedAt;
}

/// <summary>
/// Sent to each viewer separately, since balance and ownership differ per player.
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceConsoleState : BoundUserInterfaceMessage
{
    public List<MarketplaceListingData> Listings;
    public int Balance;
    public bool HasAccount;
    public int PendingPayout;
    public float TaxRate;
    public int MaxUnitPrice;
    public string GridName;

    /// <summary>
    /// The item currently in the console's slot, if any.
    /// </summary>
    public NetEntity? SlotEntity;
    public string? SlotName;
    public bool SlotStackable;
    public string? SlotStackType;
    public int SlotCount;
    public string? SlotDenyReason;

    /// <summary>
    /// Open requests. Offers are only filled in for the viewer's own requests.
    /// </summary>
    public List<MarketplaceRequestData> Requests = new();

    /// <summary>
    /// Offers the viewer made, including returned ones waiting to be taken back.
    /// </summary>
    public List<MarketplaceOfferData> MyOffers = new();

    public int MaxRequestsPerPlayer;

    public MarketplaceConsoleState(
        List<MarketplaceListingData> listings,
        int balance,
        bool hasAccount,
        int pendingPayout,
        float taxRate,
        int maxUnitPrice,
        string gridName,
        NetEntity? slotEntity,
        string? slotName,
        bool slotStackable,
        string? slotStackType,
        int slotCount,
        string? slotDenyReason)
    {
        Listings = listings;
        Balance = balance;
        HasAccount = hasAccount;
        PendingPayout = pendingPayout;
        TaxRate = taxRate;
        MaxUnitPrice = maxUnitPrice;
        GridName = gridName;
        SlotEntity = slotEntity;
        SlotName = slotName;
        SlotStackable = slotStackable;
        SlotStackType = slotStackType;
        SlotCount = slotCount;
        SlotDenyReason = slotDenyReason;
    }
}

/// <summary>
/// List the item in the console's slot.
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceListItemMessage(int amount, int unitPrice) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
    public readonly int UnitPrice = unitPrice;
}

[Serializable, NetSerializable]
public sealed class MarketplaceBuyMessage(int listingId, int amount, int expectedUnitPrice) : BoundUserInterfaceMessage
{
    public readonly int ListingId = listingId;
    public readonly int Amount = amount;

    /// <summary>
    /// Guards against buying a listing whose price changed while the UI was open.
    /// </summary>
    public readonly int ExpectedUnitPrice = expectedUnitPrice;
}

[Serializable, NetSerializable]
public sealed class MarketplaceWithdrawMessage(int listingId) : BoundUserInterfaceMessage
{
    public readonly int ListingId = listingId;
}

[Serializable, NetSerializable]
public sealed class MarketplaceEjectMessage : BoundUserInterfaceMessage;

/// <summary>
/// Asks the server for the full examine text of an item shown in the UI (for the hover tooltip).
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceExamineRequestMessage(NetEntity item) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed class MarketplaceExamineResponseMessage(NetEntity item, Robust.Shared.Utility.FormattedMessage text) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
    public readonly Robust.Shared.Utility.FormattedMessage Text = text;
}
