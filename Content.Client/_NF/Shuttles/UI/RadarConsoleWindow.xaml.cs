using Content.Client.Computer;
using Content.Client.UserInterface.Controls;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Shuttles.UI;

public sealed partial class RadarConsoleWindow : FancyWindow,
    IComputerWindow<NavInterfaceState>
{
    private ShuttleNavControl.RadarModeButton? _lockButton;
    private BoxContainer? _lockHolder;

    public void SetConsole(EntityUid consoleEntity)
    {
        RadarScreen.SetConsole(consoleEntity);

        var fixedNorth = IoCManager.Resolve<IEntityManager>().TryGetComponent<RadarConsoleComponent>(consoleEntity, out var radar)
                         && radar.FixedNorth;

        // Radar locked to north: pan lock button lives in the window UI, next to the azimuth button.
        if (fixedNorth)
        {
            RadarScreen.SetFixedNorth(true);
            EnsureLockButton();
        }
        else if (_lockButton != null)
        {
            _lockButton.Visible = false;
        }
    }

    private void EnsureLockButton()
    {
        if (_lockButton != null)
        {
            _lockButton.Visible = true;
            return;
        }

        _lockButton = new ShuttleNavControl.RadarModeButton(ShuttleNavControl.RadarModeButtonIcon.Lock)
        {
            ModulateSelfOverride = Color.White.WithAlpha(0.55f),
            ToolTip = Loc.GetString("shuttle-console-button-lock"),
            Toggled = RadarScreen.RotationLocked,
        };
        _lockButton.OnPressed += _ =>
        {
            RadarScreen.SetRotationLocked(!RadarScreen.RotationLocked);
            _lockButton.Toggled = RadarScreen.RotationLocked;
        };

        // Azimuth button sits at BottomLeft margin 10 and is 16px wide; the wrapper's left margin puts the lock right after it.
        _lockHolder = new BoxContainer { Margin = new Thickness(18f, 0f, 0f, 0f) };
        _lockHolder.AddChild(_lockButton);
        ContentLayout.AddChild(_lockHolder);
        LayoutContainer.SetAnchorAndMarginPreset(_lockHolder, LayoutContainer.LayoutPreset.BottomLeft, margin: 10);
    }
}
