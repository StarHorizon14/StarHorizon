using Content.Client.Audio;
using Content.Client.Gameplay;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Playtime;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.Inventory.Widgets;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Robust.Client.Audio;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using PickerWindow = Content.Client._NF.LateJoin.Windows.PickerWindow;

namespace Content.Client._Horizon.Hub;

public sealed class HubGameplayState : GameplayState
{
    [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ClientsidePlaytimeTrackingManager _playtimeTracking = default!;

    private ClientGameTicker _gameTicker = default!;
    private ContentAudioSystem _contentAudioSystem = default!;

    private LobbyGui? _lobby;
    private bool _expanded;
    private PickerWindow? _pickerWindow;

    public HubGameplayState()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Startup()
    {
        base.Startup();

        _gameTicker = _entityManager.System<ClientGameTicker>();
        _contentAudioSystem = _entityManager.System<ContentAudioSystem>();

        if (_uiManager.ActiveScreen == null)
            return;

        var screenRoot = _uiManager.ActiveScreen.Parent!;

        _lobby = new LobbyGui();
        screenRoot.AddChild(_lobby);
        LayoutContainer.SetAnchorPreset(_lobby, LayoutContainer.LayoutPreset.Wide);

        _lobby.Background.Visible = false;
        _lobby.VoteContainer.Visible = false;

        DisableSplitContainerClickBlocking(_lobby);

        _uiManager.GetUIController<LobbyUIController>().ActivateLobbyGui(_lobby);

        _lobby.CharacterPreview.CharacterSetupButton.OnPressed += OnSetupPressed;
        _lobby.ReadyButton.OnPressed += OnReadyPressed;
        _lobby.ReadyButton.OnToggled += OnReadyToggled;
        _lobby.CollapseButton.OnPressed += _ => SetExpanded(false);
        _lobby.ExpandButton.OnPressed += _ => SetExpanded(true);
        _lobby.AHelpButton.OnPressed += OnAHelpPressed;

        _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
        _gameTicker.LobbyStatusUpdated += UpdateLobbyUi;
        _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;
        _contentAudioSystem.LobbySoundtrackChanged += UpdateLobbySoundtrackInfo;

        _contentAudioSystem.StartLobbyMusic();

        UpdateLobbyUi();
        SetExpanded(true);
    }

    protected override void Shutdown()
    {
        _pickerWindow?.Close();
        _pickerWindow = null;

        if (_lobby != null)
        {
            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;
            _contentAudioSystem.LobbySoundtrackChanged -= UpdateLobbySoundtrackInfo;

            _contentAudioSystem.EndLobbyMusic();

            SetExpanded(false);

            _uiManager.GetUIController<LobbyUIController>().DeactivateLobbyGui(_lobby);

            _lobby.Parent?.RemoveChild(_lobby);

            _lobby = null;
        }

        base.Shutdown();
    }

    private void SetExpanded(bool expanded)
    {
        if (_lobby == null)
            return;

        _expanded = expanded;

        _lobby.Chat.Visible = expanded;
        _lobby.Chat.Main = expanded;

        _lobby.DevInfoBanner.Visible = expanded;
        _lobby.LobbySong.Visible = expanded;

        var chatController = _uiManager.GetUIController<ChatUIController>();
        foreach (var chat in chatController.Chats)
        {
            if (chat != _lobby.Chat)
                chat.Main = !expanded;
        }

        ApplyHudVisibility();
    }

    private void ApplyHudVisibility()
    {
        if (_uiManager.ActiveScreen == null)
            return;

        if (_uiManager.ActiveScreen.GetWidget<HotbarGui>() is { } hotbar)
            hotbar.Visible = !_expanded;

        if (_uiManager.ActiveScreen.GetWidget<InventoryGui>() is { } inventory)
            inventory.Visible = !_expanded;

        if (_uiManager.ActiveScreen.GetWidget<ActionsBar>() is { } actions)
            actions.Visible = !_expanded;

        if (_uiManager.ActiveScreen.GetWidget<GameTopMenuBar>() is { } topMenuBar)
            topMenuBar.Visible = false;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_lobby == null)
            return;

        ApplyHudVisibility();

        if (_gameTicker.IsGameStarted)
        {
            _lobby.StartTime.Text = string.Empty;
            var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
            _lobby.StationTime.Text = Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
            return;
        }

        _lobby.StationTime.Text = Loc.GetString("lobby-state-player-status-round-not-started");

        if (_gameTicker.Paused)
            return;

        if (_gameTicker.StartTime < _gameTiming.CurTime)
        {
            _lobby.StartTime.Text = Loc.GetString("lobby-state-soon");
            return;
        }

        var difference = _gameTicker.StartTime - _gameTiming.CurTime;
        var text = difference.TotalHours >= 1
            ? $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}"
            : $"{difference.Minutes}:{difference.Seconds:D2}";

        _lobby.StartTime.Text = Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text));
    }

    private void LobbyLateJoinStatusUpdated()
    {
        if (_lobby != null)
            _lobby.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
    }

    private void UpdateLobbyUi()
    {
        if (_lobby == null)
            return;

        if (_gameTicker.IsGameStarted)
        {
            _lobby.ReadyButton.Text = Loc.GetString("lobby-state-ready-button-join-state");
            _lobby.ReadyButton.ToggleMode = false;
            _lobby.ReadyButton.Pressed = false;
            _lobby.ObserveButton.Disabled = false;
        }
        else
        {
            _lobby.StartTime.Text = string.Empty;
            _lobby.ReadyButton.Text = Loc.GetString(_lobby.ReadyButton.Pressed ? "lobby-state-player-status-ready" : "lobby-state-player-status-not-ready");
            _lobby.ReadyButton.ToggleMode = true;
            _lobby.ReadyButton.Disabled = false;
            _lobby.ReadyButton.Pressed = _gameTicker.AreWeReady;
            _lobby.ObserveButton.Disabled = true;
        }

        var minutesToday = _playtimeTracking.PlaytimeMinutesToday;
        if (minutesToday > 60)
        {
            _lobby.PlaytimeComment.Visible = true;

            var hoursToday = Math.Round(minutesToday / 60f, 1);
            var chosenString = minutesToday switch
            {
                < 180 => "lobby-state-playtime-comment-normal",
                < 360 => "lobby-state-playtime-comment-concerning",
                < 720 => "lobby-state-playtime-comment-grasstouchless",
                _ => "lobby-state-playtime-comment-selfdestructive"
            };

            _lobby.PlaytimeComment.SetMarkup(Loc.GetString(chosenString, ("hours", hoursToday)));
        }
        else
        {
            _lobby.PlaytimeComment.Visible = false;
        }
    }

    private void OnSetupPressed(BaseButton.ButtonEventArgs args)
    {
        SetReady(false);
        _lobby?.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
    }

    private void OnAHelpPressed(BaseButton.ButtonEventArgs args)
    {
        _uiManager.GetUIController<AHelpUIController>().ToggleWindow();
    }

    private void UpdateLobbySoundtrackInfo(LobbySoundtrackChangedEvent ev)
    {
        if (_lobby == null)
            return;

        if (ev.SoundtrackFilename == null)
        {
            _lobby.LobbySong.SetMarkup(Loc.GetString("lobby-state-song-no-song-text"));
            return;
        }

        if (!_resourceCache.TryGetResource<AudioResource>(ev.SoundtrackFilename, out var lobbySongResource))
            return;

        var lobbyStream = lobbySongResource.AudioStream;

        var title = string.IsNullOrEmpty(lobbyStream.Title)
            ? Loc.GetString("lobby-state-song-unknown-title")
            : lobbyStream.Title;

        var artist = string.IsNullOrEmpty(lobbyStream.Artist)
            ? Loc.GetString("lobby-state-song-unknown-artist")
            : lobbyStream.Artist;

        var markup = Loc.GetString("lobby-state-song-text",
            ("songTitle", title),
            ("songArtist", artist));

        _lobby.LobbySong.SetMarkup(markup);
    }

    private void OnReadyPressed(BaseButton.ButtonEventArgs args)
    {
        if (!_gameTicker.IsGameStarted)
            return;

        _pickerWindow ??= new PickerWindow();
        _pickerWindow.OpenCentered();
    }

    private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
    {
        SetReady(args.Pressed);
    }

    private void SetReady(bool newReady)
    {
        if (_gameTicker.IsGameStarted)
            return;

        _consoleHost.ExecuteCommand($"toggleready {newReady}");
    }

    private static void DisableSplitContainerClickBlocking(Control root)
    {
        if (root is SplitContainer splitContainer)
            splitContainer.MouseFilter = Control.MouseFilterMode.Ignore;

        foreach (var child in root.Children)
        {
            DisableSplitContainerClickBlocking(child);
        }
    }
}
