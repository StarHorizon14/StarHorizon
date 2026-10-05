using Content.Client.Gameplay;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Horizon.Cinematic;

/// <summary>
/// Gameplay state with the entire HUD hidden, reachable via the engine's "scene"
/// console command (e.g. "scene CinematicState") for recording footage without any UI chrome.
/// Switch back to normal gameplay with "scene GameplayState".
/// </summary>
public sealed class CinematicState : GameplayState
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _prevHeldItemShow;

    protected override void Startup()
    {
        base.Startup();
        SetHudVisible(false);

        // ShowHandItemOverlay (the mini icon of the item in your active hand that follows the
        // cursor) is a screen-space Overlay, not a HUD widget, so it isn't covered by SetHudVisible.
        // It already reads this CVar every draw, so toggling it off/on is enough to hide/restore it.
        _prevHeldItemShow = _cfg.GetCVar(CCVars.HudHeldItemShow);
        _cfg.SetCVar(CCVars.HudHeldItemShow, false);
    }

    protected override void Shutdown()
    {
        _cfg.SetCVar(CCVars.HudHeldItemShow, _prevHeldItemShow);
        SetHudVisible(true);
        base.Shutdown();
    }

    private void SetHudVisible(bool visible)
    {
        var screen = UserInterfaceManager.ActiveScreen;
        if (screen == null)
            return;

        HideIfPresent<GameTopMenuBar>(screen, visible);
        HideIfPresent<ActionsBar>(screen, visible);
        HideIfPresent<AlertsUI>(screen, visible);
        HideIfPresent<HotbarGui>(screen, visible);
        HideIfPresent<GhostGui>(screen, visible);
        // Inventory stays visible in CinematicState — keep it out of the hide list.

        if (screen is InGameScreen inGameScreen)
        {
            // The chat's LineEdit relies on getting a FrameUpdate() every frame to clear its
            // internal "ignore next keystroke" flag (used to swallow the FocusChat keybind's own
            // key press). Control.DoFrameUpdateRecursive() skips that entirely while Visible is
            // false, so setting Visible = false here left that flag stuck and ate the first
            // character of the next message typed. Hide it visually via Modulate instead, which
            // keeps Visible = true (and FrameUpdate running) while still drawing nothing.
            inGameScreen.ChatBox.Modulate = visible ? Color.White : Color.Transparent;
        }
    }

    private static void HideIfPresent<T>(UIScreen screen, bool visible) where T : UIWidget, new()
    {
        if (screen.TryGetWidget<T>(out var widget))
            widget.Visible = visible;
    }
}
