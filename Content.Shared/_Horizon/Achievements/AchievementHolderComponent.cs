using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared._Horizon.Achievements;

/// <summary>
/// Marks an entity that a player has inhabited and carries their achievements,
/// shown in the "Achievements" tab of the character info window.
/// The achievements themselves are stored per account on the server and survive between rounds.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AchievementHolderComponent : Component
{
    [ViewVariables]
    public NetUserId UserId;

    [ViewVariables, AutoNetworkedField]
    public List<string> Achievements = new();
}
