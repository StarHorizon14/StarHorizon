namespace Content.Server._Horizon.Shuttles.Events;

/// <summary>
/// Raised on a shuttle right before FTL moves it to another map: when it enters hyperspace and when it arrives.
/// Moving to another map clears every physics joint on the grid, so this is the last chance to prepare for that.
/// </summary>
[ByRefEvent]
public readonly record struct FTLGridMapChangingEvent(EntityUid Grid);
