using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.SlimeAppearance;

/// <summary>
/// Lets a slime person reshape their appearance mid-round: sex, markings (including hair), skin and eye color.
/// The change applies after standing still for <see cref="DoAfterTime"/>, then goes on cooldown.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeAppearanceChangeComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionSlimeChangeAppearance";

    [DataField]
    public EntityUid? ActionEntity;

    [DataField]
    public TimeSpan DoAfterTime = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The appearance waiting for the do-after to finish.
    /// </summary>
    [ViewVariables]
    public MarkingSet? PendingMarkings;

    [ViewVariables]
    public Sex PendingSex;

    [ViewVariables]
    public Color PendingSkinColor;

    [ViewVariables]
    public Color PendingEyeColor;

    [ViewVariables]
    public DoAfterId? DoAfter;
}

public sealed partial class SlimeChangeAppearanceActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class SlimeAppearanceDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum SlimeAppearanceUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SlimeAppearanceBoundUserInterfaceState(
    MarkingSet markings,
    string species,
    Sex sex,
    Color skinColor,
    Color eyeColor) : BoundUserInterfaceState
{
    public readonly MarkingSet Markings = markings;
    public readonly string Species = species;
    public readonly Sex Sex = sex;
    public readonly Color SkinColor = skinColor;
    public readonly Color EyeColor = eyeColor;
}

[Serializable, NetSerializable]
public sealed class SlimeAppearanceApplyMessage(MarkingSet markings, Sex sex, Color skinColor, Color eyeColor) : BoundUserInterfaceMessage
{
    public readonly MarkingSet Markings = markings;
    public readonly Sex Sex = sex;
    public readonly Color SkinColor = skinColor;
    public readonly Color EyeColor = eyeColor;
}
