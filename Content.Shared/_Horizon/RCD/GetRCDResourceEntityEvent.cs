namespace Content.Shared._Horizon.RCD;

/// <summary>
/// Raised on an RCD to find the entity whose charges and grid access it should use.
/// Lets an RCD mounted on something else (e.g. a mech) keep its state on the equipment item.
/// </summary>
[ByRefEvent]
public record struct GetRCDResourceEntityEvent(EntityUid Resource);
