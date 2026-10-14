using Robust.Shared.Network;

namespace Content.Server._Horizon.Achievements;

/// <summary>
/// Marks an entity that a player has inhabited, so their achievements show up when it's examined.
/// </summary>
[RegisterComponent, Access(typeof(AchievementSystem))]
public sealed partial class AchievementHolderComponent : Component
{
    [ViewVariables]
    public NetUserId UserId;
}
