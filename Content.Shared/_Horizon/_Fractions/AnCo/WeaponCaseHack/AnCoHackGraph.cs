using System.Numerics;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;

/// <summary>
/// Направление дорожки (вид сверху, схема растёт вверх). Единица длины - один шаг сетки.
/// </summary>
[Serializable, NetSerializable]
public enum AnCoHackDir : byte
{
    Left,
    Up,
    Right,
}

/// <summary>
/// Развилка, которую игрок уже прошёл. Именно эти данные уходят клиенту: геометрия будущих узлов не раскрывается.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct AnCoHackPassedNode(
    AnCoHackDir LeftExit,
    AnCoHackDir RightExit,
    bool CorrectIsB,
    int LegLength,
    int DeadEndLength);

/// <summary>
/// Развилка графа: текущая дорожка A делится на B (левый выход) и C (правый выход).
/// Один выход - верный (ведёт к следующему узлу), второй - тупик.
/// </summary>
public sealed class AnCoHackNode
{
    public string RuleId = string.Empty;
    public string SerialB = string.Empty;
    public string SerialC = string.Empty;

    /// <summary>True, если верна дорожка B (её серийник удовлетворяет условию).</summary>
    public bool CorrectIsB;

    public AnCoHackDir LeftExit;
    public AnCoHackDir RightExit;

    /// <summary>Длина верного звена до следующего узла (или до замка).</summary>
    public int LegLength;

    /// <summary>Длина тупика.</summary>
    public int DeadEndLength;

    public AnCoHackDir CorrectDir => CorrectIsB ? LeftExit : RightExit;
    public AnCoHackDir DeadEndDir => CorrectIsB ? RightExit : LeftExit;

    public AnCoHackPassedNode ToPassed() => new(LeftExit, RightExit, CorrectIsB, LegLength, DeadEndLength);
}

/// <summary>
/// Генератор графа дорожек: ломаная от старта до замка, на каждом узле второй выход - тупик.
/// </summary>
public static class AnCoHackGraph
{
    /// <summary>Длина вертикального участка от старта до первого узла.</summary>
    public const int FirstLeg = 2;

    /// <summary>Длина обрезанных стабов текущей развилки, которые видит игрок.</summary>
    public const int StubLength = 1;

    public const int MinLeg = 2;
    public const int MaxLeg = 4;
    public const int MinDeadEnd = 1;
    public const int MaxDeadEnd = 3;

    private const int MaxLayoutTries = 200;

    // Пары выходов слева направо: влево+вверх, влево+вправо, вверх+вправо.
    private static readonly (AnCoHackDir Left, AnCoHackDir Right)[] ExitPairs =
    {
        (AnCoHackDir.Left, AnCoHackDir.Up),
        (AnCoHackDir.Left, AnCoHackDir.Right),
        (AnCoHackDir.Up, AnCoHackDir.Right),
    };

    /// <summary>
    /// Смещение за один шаг; y растёт вверх.
    /// </summary>
    public static Vector2 Step(AnCoHackDir dir)
    {
        return dir switch
        {
            AnCoHackDir.Left => new Vector2(-1, 1),
            AnCoHackDir.Right => new Vector2(1, 1),
            _ => new Vector2(0, 1),
        };
    }

    public static List<AnCoHackNode> Generate(IRobustRandom random, int nodeCount)
    {
        // Условия не повторяются в пределах одного графа.
        var rules = new List<AnCoSerialRule>(AnCoSerialRules.All);
        random.Shuffle(rules);
        var count = Math.Min(nodeCount, rules.Count);

        List<AnCoHackNode> nodes = new();
        for (var attempt = 0; attempt < MaxLayoutTries; attempt++)
        {
            nodes = new List<AnCoHackNode>(count);
            for (var i = 0; i < count; i++)
                nodes.Add(CreateNode(random, rules[i]));

            if (IsLayoutClean(nodes))
                break;
        }

        return nodes;
    }

    private static AnCoHackNode CreateNode(IRobustRandom random, AnCoSerialRule rule)
    {
        var correctIsB = random.Prob(0.5f);
        var matching = AnCoSerialRules.GenerateSerial(random, rule, true);
        var other = AnCoSerialRules.GenerateSerial(random, rule, false);
        var pair = ExitPairs[random.Next(ExitPairs.Length)];

        return new AnCoHackNode
        {
            RuleId = rule.LocId,
            SerialB = correctIsB ? matching : other,
            SerialC = correctIsB ? other : matching,
            CorrectIsB = correctIsB,
            LeftExit = pair.Left,
            RightExit = pair.Right,
            LegLength = random.Next(MinLeg, MaxLeg + 1),
            DeadEndLength = random.Next(MinDeadEnd, MaxDeadEnd + 1),
        };
    }

    /// <summary>
    /// Проверяет, что ветки не пересекаются и не накладываются друг на друга.
    /// </summary>
    private static bool IsLayoutClean(List<AnCoHackNode> nodes)
    {
        var points = new HashSet<(int, int)>();
        // Середины единичных шагов в удвоенных координатах: ловит наложения и X-пересечения диагоналей.
        var mids = new HashSet<(int, int)>();

        bool Walk(ref (int X, int Y) pos, AnCoHackDir dir, int length)
        {
            var step = Step(dir);
            var dx = (int) step.X;
            var dy = (int) step.Y;
            for (var i = 0; i < length; i++)
            {
                var next = (pos.X + dx, pos.Y + dy);
                if (!mids.Add((pos.X + next.Item1, pos.Y + next.Item2)))
                    return false;

                // Точки магистрали и тупиков не должны совпадать, кроме узла, где ветка начинается.
                if (!points.Add(next))
                    return false;

                pos = next;
            }

            return true;
        }

        // Магистраль: старт -> узлы -> замок. Запоминаем положение узлов.
        var cursor = (X: 0, Y: 0);
        points.Add(cursor);
        if (!Walk(ref cursor, AnCoHackDir.Up, FirstLeg))
            return false;

        var nodePositions = new List<(int X, int Y)>();
        foreach (var node in nodes)
        {
            nodePositions.Add(cursor);
            if (!Walk(ref cursor, node.CorrectDir, node.LegLength))
                return false;
        }

        // Тупики: ни одна их точка не должна лежать на уже занятых.
        for (var i = 0; i < nodes.Count; i++)
        {
            var pos = nodePositions[i];
            if (!Walk(ref pos, nodes[i].DeadEndDir, nodes[i].DeadEndLength))
                return false;
        }

        return true;
    }
}
