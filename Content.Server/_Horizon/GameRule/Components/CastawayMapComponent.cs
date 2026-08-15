namespace Content.Server._Horizon.GameRule.Components;

/// <summary>
/// Marks a Castaway player's personal, dedicated map so it can be deleted once their shuttle
/// leaves it via FTL — nobody can fly back to it since it's never registered as an FTL destination.
/// </summary>
[RegisterComponent, Access(typeof(CastawayRuleSystem))]
public sealed partial class CastawayMapComponent : Component;
