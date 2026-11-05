using System.Numerics;
using Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Horizon._Fractions.AnCo.WeaponCaseHack;

public sealed class AnCoWeaponCaseHackWindow : DefaultWindow
{
    public event Action<bool, int>? OnChoose;

    private readonly AnCoWeaponCaseHackMap _map = new();
    private readonly Label _attemptsLabel = new();
    private readonly Label _ruleLabel = new() { FontColorOverride = Color.White };
    private readonly Button _buttonB = new();
    private readonly Button _buttonC = new();
    private int _nodeIndex;

    public AnCoWeaponCaseHackWindow()
    {
        Title = Loc.GetString("anco-weapon-case-hack-window-title");
        MinSize = new Vector2(320, 520);

        _map.MinSize = new Vector2(300, 300);
        _map.VerticalExpand = true;

        _buttonB.OnPressed += _ => OnChoose?.Invoke(true, _nodeIndex);
        _buttonC.OnPressed += _ => OnChoose?.Invoke(false, _nodeIndex);

        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 4,
            Children =
            {
                _attemptsLabel,
                _map,
                new Label { Text = Loc.GetString("anco-weapon-case-hack-condition-header") },
                _ruleLabel,
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    SeparationOverride = 4,
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Vertical,
                            HorizontalExpand = true,
                            Children = { new Label { Text = Loc.GetString("anco-weapon-case-hack-track-b") }, _buttonB }
                        },
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Vertical,
                            HorizontalExpand = true,
                            Children = { new Label { Text = Loc.GetString("anco-weapon-case-hack-track-c") }, _buttonC }
                        },
                    }
                },
            }
        });
    }

    public void UpdateState(AnCoHackBoundUserInterfaceState state)
    {
        _nodeIndex = state.NodeIndex;
        _attemptsLabel.Text = Loc.GetString("anco-weapon-case-hack-attempts",
            ("attempts", state.Attempts), ("max", state.MaxAttempts));
        _ruleLabel.Text = Loc.GetString(state.RuleLocId);
        _buttonB.Text = state.SerialB;
        _buttonC.Text = state.SerialC;
        _map.Update(state);
    }
}

/// <summary>
/// Схема дорожек: ломаная от зелёного старта. Видна только пройденная часть и текущая развилка,
/// чьи выходы нарисованы одинаковыми обрезанными стабами (какой верный - неизвестно). Жёлтая точка - конец
/// пройденного тупика. Карта подгоняется под размер окна и растёт по мере прохождения.
/// </summary>
public sealed class AnCoWeaponCaseHackMap : Control
{
    private static readonly Color Track = Color.FromHex("#b22222");
    private static readonly Color Node = Color.FromHex("#3fb4e6");
    private static readonly Color NodeActive = Color.White;
    private static readonly Color DeadEnd = Color.FromHex("#ffe600");
    private static readonly Color Goal = Color.FromHex("#7ed321");

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

        var pos = Vector2.Zero;
        var next = new Vector2(0, AnCoHackGraph.FirstLeg);
        _segments.Add((pos, next));
        pos = next;

        foreach (var node in state.Passed)
        {
            _nodes.Add(pos);

            var dead = pos + AnCoHackGraph.Step(node.CorrectIsB ? node.RightExit : node.LeftExit) * node.DeadEndLength;
            _segments.Add((pos, dead));
            _deadEnds.Add(dead);

            next = pos + AnCoHackGraph.Step(node.CorrectIsB ? node.LeftExit : node.RightExit) * node.LegLength;
            _segments.Add((pos, next));
            pos = next;
        }

        // Текущая развилка: оба выхода обрезаны на одинаковой длине.
        _current = pos;
        _segments.Add((pos, pos + AnCoHackGraph.Step(state.CurrentLeftExit) * AnCoHackGraph.StubLength));
        _segments.Add((pos, pos + AnCoHackGraph.Step(state.CurrentRightExit) * AnCoHackGraph.StubLength));
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
