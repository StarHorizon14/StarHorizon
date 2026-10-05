using Robust.Client.UserInterface;

namespace Content.Client._Horizon.TheEndEvent;

/// <summary>
/// Fake "connection lost" scene shown once <see cref="TheEndOverlay"/> fully covers the
/// screen. Visually a copy of <c>LauncherConnecting</c>, but the connection isn't actually
/// dropped — the exit button is a dead end and the status always reads as an error.
/// </summary>
public sealed class TheEndConnectingState : Robust.Client.State.State
{
    [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;

    private TheEndConnectingGui? _control;

    protected override void Startup()
    {
        _control = new TheEndConnectingGui();
        _userInterfaceManager.StateRoot.AddChild(_control);
    }

    protected override void Shutdown()
    {
        _control?.Orphan();
        _control = null;
    }
}
