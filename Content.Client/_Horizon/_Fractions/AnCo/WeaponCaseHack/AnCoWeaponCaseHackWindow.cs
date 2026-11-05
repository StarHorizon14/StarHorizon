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
/// Схема дорожек: зелёный старт внизу, красная магистраль вверх, синие узлы, жёлтые точки - найденные тупики,
/// зелёный замок в конце. Какая из веток верная, клиент не знает.
/// </summary>
public sealed class AnCoWeaponCaseHackMap : Control
{
    private static readonly Color Track = Color.FromHex("#b22222");
    private static readonly Color Node = Color.FromHex("#3fb4e6");
    private static readonly Color NodeActive = Color.White;
    private static readonly Color DeadEnd = Color.FromHex("#ffe600");
    private static readonly Color Goal = Color.FromHex("#7ed321");

    private int _nodeIndex;
    private int _nodeCount;
    private List<AnCoHackDeadEnd> _deadEnds = new();

    public void Update(AnCoHackBoundUserInterfaceState state)
    {
        _nodeIndex = state.NodeIndex;
        _nodeCount = state.NodeCount;
        _deadEnds = state.DeadEnds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_nodeCount <= 0)
            return;

        var w = PixelWidth;
        var h = PixelHeight;
        var cx = w / 2f;
        var margin = 24f;
        var bottom = h - margin;
        var top = margin;
        var step = (bottom - top) / (_nodeCount + 1);
        var arm = MathF.Min(60f, w / 2f - margin);

        // Магистраль: старт (внизу) -> узлы -> замок (сверху).
        handle.DrawLine(new Vector2(cx, bottom), new Vector2(cx, top), Track);

        // Старт.
        handle.DrawRect(UIBox2.FromDimensions(cx - 7, bottom - 7, 14, 14), Goal);

        for (var i = 0; i < _nodeCount; i++)
        {
            var y = bottom - step * (i + 1);
            var active = i == _nodeIndex;

            // Ветки B (влево) и C (вправо) от узла.
            handle.DrawLine(new Vector2(cx, y), new Vector2(cx - arm, y - step / 2), Track);
            handle.DrawLine(new Vector2(cx, y), new Vector2(cx + arm, y - step / 2), Track);

            foreach (var dead in _deadEnds)
            {
                if (dead.Node != i)
                    continue;

                var x = dead.IsB ? cx - arm : cx + arm;
                handle.DrawRect(UIBox2.FromDimensions(x - 5, y - step / 2 - 5, 10, 10), DeadEnd);
            }

            handle.DrawRect(UIBox2.FromDimensions(cx - 5, y - 5, 10, 10), active ? NodeActive : Node);
        }

        // Замок.
        handle.DrawRect(UIBox2.FromDimensions(cx - 9, top - 9, 18, 18), Goal);
    }
}
