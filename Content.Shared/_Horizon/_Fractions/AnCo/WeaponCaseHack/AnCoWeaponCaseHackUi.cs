using Robust.Shared.Serialization;

namespace Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;

[Serializable, NetSerializable]
public enum AnCoHackUiKey : byte
{
    Key
}

/// <summary>
/// A dead end the hacker has already found: at node <see cref="Node"/> track B (or C) leads nowhere.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct AnCoHackDeadEnd(int Node, bool IsB);

[Serializable, NetSerializable]
public sealed class AnCoHackBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly int Attempts;
    public readonly int MaxAttempts;
    public readonly int NodeIndex;
    public readonly int NodeCount;

    /// <summary>Localization key of the condition shown at the current node.</summary>
    public readonly string RuleLocId;

    public readonly string SerialB;
    public readonly string SerialC;
    public readonly List<AnCoHackDeadEnd> DeadEnds;

    public AnCoHackBoundUserInterfaceState(
        int attempts,
        int maxAttempts,
        int nodeIndex,
        int nodeCount,
        string ruleLocId,
        string serialB,
        string serialC,
        List<AnCoHackDeadEnd> deadEnds)
    {
        Attempts = attempts;
        MaxAttempts = maxAttempts;
        NodeIndex = nodeIndex;
        NodeCount = nodeCount;
        RuleLocId = ruleLocId;
        SerialB = serialB;
        SerialC = serialC;
        DeadEnds = deadEnds;
    }
}

/// <summary>
/// The hacker chose track B or C at the given node.
/// </summary>
[Serializable, NetSerializable]
public sealed class AnCoHackChooseMessage : BoundUserInterfaceMessage
{
    public readonly bool ChooseB;

    /// <summary>Node the choice was made at; the server ignores stale messages.</summary>
    public readonly int NodeIndex;

    public AnCoHackChooseMessage(bool chooseB, int nodeIndex)
    {
        ChooseB = chooseB;
        NodeIndex = nodeIndex;
    }
}
