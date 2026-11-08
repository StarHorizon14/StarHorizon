using System.Numerics;
using Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon._Fractions.AnCo.WeaponCaseHack;

/// <summary>
/// Схема дорожек: узлы по диагонали, линии к ним только горизонтальные и вертикальные, старт - зелёный. Видна только пройденная часть и текущая развилка,
/// чьи выходы нарисованы одинаковыми обрезанными стабами (какой верный - неизвестно). Янтарная точка - конец
/// пройденного тупика. Карта подгоняется под размер окна и растёт по мере прохождения.
/// </summary>
public sealed class AnCoWeaponCaseHackMap : Control
{
    private static readonly Color Track = AnCoConsoleTheme.Heading;
    private static readonly Color Node = AnCoConsoleTheme.Accent;
    private static readonly Color NodeActive = AnCoConsoleTheme.Highlight;
    private static readonly Color DeadEnd = AnCoConsoleTheme.Warning;
    private static readonly Color Goal = AnCoConsoleTheme.Ok;

    private const float Margin = 20f;
    private const float MaxUnit = 26f;

    // Геометрия в единицах сетки, y растёт вверх.
    private readonly List<(Vector2 From, Vector2 To)> _segments = new();
    private readonly List<Vector2> _nodes = new();
    private readonly List<Vector2> _deadEnds = new();
    private Vector2 _current;

    public void Update(AnCoHackBoundUserInterfaceState state)
    {
        _segments.Clear();
        _nodes.Clear();
        _deadEnds.Clear();

        // Магистраль от старта до первой развилки - вертикаль.
        var pos = Vector2.Zero;
        pos = AddPath(pos, AnCoHackDir.Up, AnCoHackGraph.FirstLeg);

        foreach (var node in state.Passed)
        {
            _nodes.Add(pos);

            _deadEnds.Add(AddPath(pos, node.CorrectIsB ? node.RightExit : node.LeftExit, node.DeadEndLength));
            pos = AddPath(pos, node.CorrectIsB ? node.LeftExit : node.RightExit, node.LegLength);
        }

        // Текущая развилка: оба выхода обрезаны на одинаковой длине.
        _current = pos;
        AddPath(pos, state.CurrentLeftExit, AnCoHackGraph.StubLength);
        AddPath(pos, state.CurrentRightExit, AnCoHackGraph.StubLength);
    }

    /// <summary>
    /// Добавляет ломаную (горизонталь в сторону, затем вертикаль вверх) и возвращает её конец.
    /// </summary>
    private Vector2 AddPath(Vector2 from, AnCoHackDir dir, int length)
    {
        var points = AnCoHackGraph.PathPoints(from, dir, length);
        for (var i = 1; i < points.Count; i++)
            _segments.Add((points[i - 1], points[i]));

        return points[^1];
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_segments.Count == 0)
            return;

        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var (from, to) in _segments)
        {
            min = Vector2.Min(min, Vector2.Min(from, to));
            max = Vector2.Max(max, Vector2.Max(from, to));
        }

        var margin = Margin * UIScale;
        var avail = new Vector2(PixelWidth - margin * 2, PixelHeight - margin * 2);
        var size = Vector2.Max(max - min, new Vector2(1, 1));
        var unit = MathF.Min(MathF.Min(avail.X / size.X, avail.Y / size.Y), MaxUnit * UIScale);

        // Центрируем фрагмент в окне; y переворачиваем (на экране вверх = меньше y).
        var origin = new Vector2(
            (PixelWidth - size.X * unit) / 2f - min.X * unit,
            (PixelHeight + size.Y * unit) / 2f + min.Y * unit);

        Vector2 ToScreen(Vector2 p) => new(origin.X + p.X * unit, origin.Y - p.Y * unit);

        foreach (var (from, to) in _segments)
            DrawThick(handle, ToScreen(from), ToScreen(to), Track);

        var nodeHalf = 5f * UIScale;
        foreach (var node in _nodes)
        {
            var p = ToScreen(node);
            handle.DrawRect(new UIBox2(p - new Vector2(nodeHalf), p + new Vector2(nodeHalf)), Node);
        }

        foreach (var dead in _deadEnds)
        {
            var p = ToScreen(dead);
            handle.DrawRect(new UIBox2(p - new Vector2(nodeHalf), p + new Vector2(nodeHalf)), DeadEnd);
        }

        var start = ToScreen(Vector2.Zero);
        var goalHalf = 7f * UIScale;
        handle.DrawRect(new UIBox2(start - new Vector2(goalHalf), start + new Vector2(goalHalf)), Goal);

        var cur = ToScreen(_current);
        handle.DrawRect(new UIBox2(cur - new Vector2(nodeHalf), cur + new Vector2(nodeHalf)), NodeActive);
    }

    private void DrawThick(DrawingHandleScreen handle, Vector2 a, Vector2 b, Color color)
    {
        // DrawLine рисует линию в 1 пиксель; утолщаем смещёнными копиями.
        var t = MathF.Max(1f, UIScale);
        handle.DrawLine(a, b, color);
        handle.DrawLine(a + new Vector2(t, 0), b + new Vector2(t, 0), color);
        handle.DrawLine(a + new Vector2(0, t), b + new Vector2(0, t), color);
    }
}
