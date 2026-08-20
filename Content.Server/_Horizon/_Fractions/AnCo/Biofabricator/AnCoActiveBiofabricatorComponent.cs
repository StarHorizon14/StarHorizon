namespace Content.Server._Horizon._Fractions.AnCo.Biofabricator;

/// <summary>
/// Marker component for Biofabricators that are currently restoring a body, so <see cref="AnCoBiofabricatorSystem.Update"/>
/// only has to iterate over active machines.
/// </summary>
[RegisterComponent]
public sealed partial class AnCoActiveBiofabricatorComponent : Component
{
}
