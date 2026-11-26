using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Marketplace;

/// <summary>
/// Marks an entity that is currently stored as a marketplace listing.
/// Such entities may be examined remotely by players browsing the marketplace.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MarketplaceListedItemComponent : Component
{
    [ViewVariables]
    public int ListingId;
}

/// <summary>
/// Raised on an entity being examined to let systems skip the usual range and occlusion checks.
/// </summary>
[ByRefEvent]
public record struct ExamineRangeBypassEvent(EntityUid Examiner, bool Allowed = false);
