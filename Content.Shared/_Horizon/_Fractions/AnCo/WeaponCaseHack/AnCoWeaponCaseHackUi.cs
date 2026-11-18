using Robust.Shared.Serialization;

namespace Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;

[Serializable, NetSerializable]
public enum AnCoHackUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class AnCoHackBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly int Attempts;
    public readonly int MaxAttempts;

    /// <summary>Localization key of the condition shown at the current node.</summary>
    public readonly string RuleLocId;

    public readonly string SerialB;
    public readonly string SerialC;

    /// <summary>Уже пройденные развилки: только они раскрывают геометрию карты.</summary>
    public readonly List<AnCoHackPassedNode> Passed;

    /// <summary>Выходы текущей развилки. Клиент рисует их обрезанными стабами, не зная, какой верный.</summary>
    public readonly AnCoHackDir CurrentLeftExit;
    public readonly AnCoHackDir CurrentRightExit;

    /// <summary>Игровое время взрыва кейса; null, пока не дан первый ответ.</summary>
    public readonly TimeSpan? Deadline;

    public AnCoHackBoundUserInterfaceState(
        int attempts,
        int maxAttempts,
        string ruleLocId,
        string serialB,
        string serialC,
        List<AnCoHackPassedNode> passed,
        AnCoHackDir currentLeftExit,
        AnCoHackDir currentRightExit,
        TimeSpan? deadline)
    {
        Attempts = attempts;
        MaxAttempts = maxAttempts;
        RuleLocId = ruleLocId;
        SerialB = serialB;
        SerialC = serialC;
        Passed = passed;
        CurrentLeftExit = currentLeftExit;
        CurrentRightExit = currentRightExit;
        Deadline = deadline;
    }

    /// <summary>Индекс текущего узла (= число пройденных).</summary>
    public int NodeIndex => Passed.Count;
}

/// <summary>
/// The hacker moved in the given direction at the given node.
/// </summary>
[Serializable, NetSerializable]
public sealed class AnCoHackChooseMessage : BoundUserInterfaceMessage
{
    public readonly AnCoHackDir Direction;

    /// <summary>Node the choice was made at; the server ignores stale messages.</summary>
    public readonly int NodeIndex;

    public AnCoHackChooseMessage(AnCoHackDir direction, int nodeIndex)
    {
        Direction = direction;
        NodeIndex = nodeIndex;
    }
}
