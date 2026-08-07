using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Castaway;

/// <summary>
/// Marker component for players spawned by the Castaway game rule.
/// Used to select a custom ambient music track for them while adrift in space.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CastawaySurvivorComponent : Component
{
}
