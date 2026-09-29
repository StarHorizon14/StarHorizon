using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Parallax;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.TheEndEvent;

/// <summary>
/// Static visual copy of LauncherConnectingGui, right down to the server address and a
/// login tip, so there's nothing visually off to give away that it's fake. No real
/// connection state backs this — the exit button just swaps its own text via
/// <see cref="ConfirmButton"/>, and the status label is hardcoded to an error.
///
/// After it's shown, runs through a scripted sequence:
/// 0-5s: status reads "Error"
/// 5-15s: status counts down "Reconnecting: 10..1"
/// 15-17s: status reads "Connected"
/// 17-37s: status reads "Error" again, and another "Error" line is appended below every
///         0.5s — the layout being centered means the panel naturally grows downward and
///         off-screen as the list gets taller
/// 37-42s: list is cleared, panel snaps back to center, status reads "Emergency
///         extraction" with a trailing dot appended every second (up to 5 dots)
/// 42s: switches to GameplayState.
/// </summary>
public sealed class TheEndConnectingGui : Control
{
    private const float ErrorSeconds = 5f;
    private const float ReconnectingSeconds = 10f;
    private const float ConnectedSeconds = 2f;
    private const float ErrorSpamSeconds = 20f;
    private const float ErrorSpamIntervalSeconds = 0.5f;
    private const float ExtractionSeconds = 5f;

    [Dependency] private readonly IGameController _gameController = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;

    private ConfirmButton _exitButton = default!;
    private Label _connectStatus = default!;
    private BoxContainer _errorList = default!;
    private Control _centeredWrapper = default!;

    private enum Phase
    {
        Error,
        Reconnecting,
        Connected,
        ErrorSpam,
        Extraction,
    }

    private Phase _phase = Phase.Error;
    private float _phaseElapsed;

    public TheEndConnectingGui()
    {
        IoCManager.InjectDependencies(this);

        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);
        Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetSpace;

        AddChild(new ParallaxControl { SpeedX = 20 });

        var titleLabel = new Label
        {
            Margin = new Thickness(8, 0, 0, 0),
            Text = Loc.GetString("connecting-title"),
            StyleClasses = { "LabelHeading" },
            VerticalAlignment = VAlignment.Center,
        };

        _exitButton = new ConfirmButton
        {
            Text = Loc.GetString("connecting-exit"),
            ConfirmationText = Loc.GetString("theend-connecting-no-exit"),
            HorizontalAlignment = HAlignment.Right,
            HorizontalExpand = true,
        };
        _exitButton.OnPressed += OnExitPressed;
        // The stylesheet's confirm-state selector doesn't reliably tint this button (it can
        // render solid white instead of red), so force the color directly on entering the
        // confirming state rather than relying on it.
        _exitButton.OnConfirming += _ => _exitButton.ModulateSelfOverride = Color.Red;

        var statusInProgress = new Label
        {
            Text = Loc.GetString("theend-connecting-in-progress"),
            Align = Label.AlignMode.Center,
        };

        _connectStatus = new Label
        {
            Text = Loc.GetString("theend-connecting-state-error"),
            StyleClasses = { "LabelSubText" },
            Align = Label.AlignMode.Center,
        };

        _errorList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
        };

        var address = _gameController.LaunchState.Ss14Address ?? _gameController.LaunchState.ConnectAddress;
        var connectingAddress = new Label
        {
            Text = address ?? string.Empty,
            StyleClasses = { "LabelSubText" },
            HorizontalAlignment = HAlignment.Center,
        };

        var tipLabel = new Label
        {
            Text = Loc.GetString("theend-connecting-tip"),
            StyleClasses = { "LabelSubText" },
        };

        var versionLabel = new Label
        {
            Text = Loc.GetString("connecting-version"),
            StyleClasses = { "LabelSubText" },
            HorizontalAlignment = HAlignment.Right,
            HorizontalExpand = true,
        };

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinSize = new Vector2(300, 200),
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { titleLabel, _exitButton },
                },
                new HighDivider(),
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    VerticalExpand = true,
                    Margin = new Thickness(4, 4, 4, 0),
                    Children =
                    {
                        new Control
                        {
                            VerticalExpand = true,
                            Margin = new Thickness(0, 0, 0, 8),
                            Children =
                            {
                                new BoxContainer
                                {
                                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                                    Children = { statusInProgress, _connectStatus, _errorList },
                                },
                            },
                        },
                        connectingAddress,
                    },
                },
                new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = Color.FromHex("#444"),
                        ContentMarginTopOverride = 2,
                    },
                },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Margin = new Thickness(12, 0, 4, 0),
                    VerticalAlignment = VAlignment.Bottom,
                    Children = { tipLabel, versionLabel },
                },
            },
        };

        _centeredWrapper = new Control
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Children =
            {
                new PanelContainer { StyleClasses = { "AngleRect" } },
                content,
            },
        };
        AddChild(_centeredWrapper);

        AddChild(BuildLoginTip());
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_exitButton.IsConfirming && _exitButton.ModulateSelfOverride != null)
            _exitButton.ModulateSelfOverride = null;

        _phaseElapsed += args.DeltaSeconds;

        switch (_phase)
        {
            case Phase.Error:
                if (_phaseElapsed >= ErrorSeconds)
                    EnterPhase(Phase.Reconnecting);
                break;

            case Phase.Reconnecting:
                UpdateReconnectingCountdown();
                if (_phaseElapsed >= ReconnectingSeconds)
                    EnterPhase(Phase.Connected);
                break;

            case Phase.Connected:
                if (_phaseElapsed >= ConnectedSeconds)
                    EnterPhase(Phase.ErrorSpam);
                break;

            case Phase.ErrorSpam:
                // Add another "Error" line every ErrorSpamIntervalSeconds. The panel is
                // centered, so as the list grows taller the panel's top edge rises and its
                // bottom edge sinks — no manual repositioning needed, it grows itself off
                // the bottom of the screen.
                if (_phaseElapsed >= (_errorList.ChildCount + 1) * ErrorSpamIntervalSeconds)
                {
                    _errorList.AddChild(new Label
                    {
                        Text = Loc.GetString("theend-connecting-state-error"),
                        StyleClasses = { "LabelSubText" },
                        Align = Label.AlignMode.Center,
                    });
                }

                if (_phaseElapsed >= ErrorSpamSeconds)
                    EnterPhase(Phase.Extraction);
                break;

            case Phase.Extraction:
                UpdateExtractionDots();
                if (_phaseElapsed >= ExtractionSeconds)
                    _stateManager.RequestStateChange<GameplayState>();
                break;
        }
    }

    private void UpdateExtractionDots()
    {
        var dots = Math.Clamp((int) _phaseElapsed + 1, 1, 5);
        _connectStatus.Text = Loc.GetString("theend-connecting-state-extraction") + new string('.', dots);
    }

    private void UpdateReconnectingCountdown()
    {
        var remaining = Math.Max(0, (int) Math.Ceiling(ReconnectingSeconds - _phaseElapsed));
        _connectStatus.Text = Loc.GetString("theend-connecting-reconnecting", ("seconds", remaining));
    }

    private void EnterPhase(Phase phase)
    {
        _phase = phase;
        _phaseElapsed = 0f;

        switch (phase)
        {
            case Phase.Reconnecting:
                UpdateReconnectingCountdown();
                break;

            case Phase.Connected:
                _connectStatus.Text = Loc.GetString("theend-connecting-state-connected");
                break;

            case Phase.ErrorSpam:
                _connectStatus.Text = Loc.GetString("theend-connecting-state-error");
                break;

            case Phase.Extraction:
                // Clearing the list shrinks the panel back down; since it's centered, that
                // alone snaps it back into view without any manual repositioning.
                _errorList.RemoveAllChildren();
                UpdateExtractionDots();
                break;
        }
    }

    private Control BuildLoginTip()
    {
        // Fixed fake tip instead of a random one from the real dataset — "Tip ???" reads as
        // just another entry in the rotation, not a giveaway that this screen isn't real.
        var tipTitle = new Label
        {
            Text = Loc.GetString("theend-connecting-window-tip"),
            StyleClasses = { "LabelHeading" },
            Align = Label.AlignMode.Center,
        };

        var tipBody = new RichTextLabel { VerticalExpand = true };
        tipBody.SetMessage(Loc.GetString("theend-connecting-tip-body"));

        var panel = new PanelContainer
        {
            StyleClasses = { "AngleRect" },
            Margin = new Thickness(0, 10),
            MaxWidth = 600,
            VerticalExpand = true,
            VerticalAlignment = VAlignment.Bottom,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    VerticalExpand = true,
                    Children =
                    {
                        new StripeBack
                        {
                            Children =
                            {
                                new BoxContainer
                                {
                                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                                    HorizontalAlignment = HAlignment.Center,
                                    Children = { tipTitle },
                                },
                            },
                        },
                        new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Vertical,
                            Margin = new Thickness(5, 5, 5, 5),
                            Children = { tipBody },
                        },
                    },
                },
            },
        };

        return panel;
    }

    private void OnExitPressed(BaseButton.ButtonEventArgs args)
    {
        // Confirmed and pressed a second time — there's still no way out, but nothing
        // actually happens; the button just resets like normal after ResetTime.
    }
}
