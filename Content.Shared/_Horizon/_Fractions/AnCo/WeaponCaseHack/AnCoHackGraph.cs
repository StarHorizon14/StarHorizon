using System.Numerics;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;

/// <summary>
/// Направление выхода (схема растёт вверх). Узлы стоят по диагонали, линии к ним - только горизонтали и вертикали.
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

    public const int MinLeg = 1;
    public const int MaxLeg = 2;
    public const int MinDeadEnd = 1;
    public const int MaxDeadEnd = 2;

    private const int MaxLayoutTries = 2000;

    // Пары выходов слева направо: влево+вверх, влево+вправо, вверх+вправо.
    private static readonly (AnCoHackDir Left, AnCoHackDir Right)[] ExitPairs =
    {
        (AnCoHackDir.Left, AnCoHackDir.Up),
        (AnCoHackDir.Left, AnCoHackDir.Right),
        (AnCoHackDir.Up, AnCoHackDir.Right),
    };

    /// <summary>
    /// Смещение узла за один шаг: узлы стоят по диагонали (влево/вправо и на 1 вверх); y растёт вверх.
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

    /// <summary>
    /// Вершины ломаной от <paramref name="from"/> на <paramref name="length"/> шагов. Линии идут только по горизонтали и
    /// вертикали: сначала в сторону (угол), потом вверх (узел). Выход «вверх» - прямая вертикаль.
    /// Первая вершина - сама <paramref name="from"/>; каждый второй элемент для Left/Right - угол, остальные - узлы.
    /// </summary>
    public static List<Vector2> PathPoints(Vector2 from, AnCoHackDir dir, int length)
    {
        var points = new List<Vector2> { from };
        var step = Step(dir);
        var pos = from;
        for (var i = 0; i < length; i++)
        {
            if (step.X != 0)
                points.Add(new Vector2(pos.X + step.X, pos.Y));

            pos = new Vector2(pos.X + step.X, pos.Y + step.Y);
            points.Add(pos);
        }

        return points;
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
        // Все вершины (углы и узлы) и единичные отрезки должны быть уникальны: отрезки единичной длины
        // пересекаются только в узлах сетки, поэтому этого достаточно, чтобы ветки не накладывались и не пересекались.
        var points = new HashSet<(int, int)>();
        var segments = new HashSet<(int, int, int, int)>();

        bool Walk(ref Vector2 pos, AnCoHackDir dir, int length)
        {
            var path = PathPoints(pos, dir, length);
            for (var i = 1; i < path.Count; i++)
            {
                var a = ((int) path[i - 1].X, (int) path[i - 1].Y);
                var b = ((int) path[i].X, (int) path[i].Y);

                // Отрезок без учёта направления.
                var key = a.CompareTo(b) <= 0 ? (a.Item1, a.Item2, b.Item1, b.Item2) : (b.Item1, b.Item2, a.Item1, a.Item2);
                if (!segments.Add(key))
                    return false;

                // Точка, где ветка начинается, уже занята самой магистралью - её не считаем.
                if (!points.Add(b))
                    return false;
            }

            pos = path[^1];
            return true;
        }

        // Магистраль: старт -> узлы -> замок. Запоминаем положение узлов.
        var cursor = Vector2.Zero;
        points.Add((0, 0));
        if (!Walk(ref cursor, AnCoHackDir.Up, FirstLeg))
            return false;

        var nodePositions = new List<Vector2>();
        foreach (var node in nodes)
        {
            nodePositions.Add(cursor);
            if (!Walk(ref cursor, node.CorrectDir, node.LegLength))
                return false;
        }

        // Тупики: ни одна их точка и ни один отрезок не должны лежать на уже занятых.
        for (var i = 0; i < nodes.Count; i++)
        {
            var pos = nodePositions[i];
            if (!Walk(ref pos, nodes[i].DeadEndDir, nodes[i].DeadEndLength))
                return false;
        }

        return true;
    }
}
