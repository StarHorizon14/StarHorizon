using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Horizon.BlackholeEvent;

/// <summary>
/// Fullscreen lockdown scene shown instead of gameplay while the blackhole event is active.
/// </summary>
public sealed class BlackholeState : Robust.Client.State.State
{
    [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;

    private BlackholeControl? _control;

    protected override void Startup()
    {
        _control = new BlackholeControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _userInterfaceManager.StateRoot.AddChild(_control);
        LayoutContainer.SetAnchorPreset(_control, LayoutContainer.LayoutPreset.Wide);
    }

    protected override void Shutdown()
    {
        _control?.Orphan();
        _control = null;
    }
}
