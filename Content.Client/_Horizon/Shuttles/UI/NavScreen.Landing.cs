using Content.Shared._Horizon.Shuttles;
using Content.Shared.Shuttles.BUIStates;

namespace Content.Client.Shuttles.UI;

// Landing pads: the "Land" / "Take off" button of the shuttle console.
public sealed partial class NavScreen
{
    /// <summary>
    /// Raised when the pilot asks to land (true) or to take off (false).
    /// </summary>
    public event Action<bool>? OnLandingRequest;

    private ShuttleLandingState _landingState;

    private void HorizonInitialize()
    {
        LandingToggle.OnPressed += _ =>
        {
            if (_landingState == ShuttleLandingState.TakingOff)
                return;

            OnLandingRequest?.Invoke(_landingState == ShuttleLandingState.None);
        };
    }

    private void HorizonUpdateState(NavInterfaceState state)
    {
        _landingState = state.LandingState;
        LandingToggle.Disabled = _landingState == ShuttleLandingState.TakingOff;
        LandingToggle.Text = Loc.GetString(_landingState switch
        {
            ShuttleLandingState.Landed => "shuttle-console-landing-takeoff",
            ShuttleLandingState.TakingOff => "shuttle-console-landing-taking-off",
            _ => "shuttle-console-landing-land",
        });
    }
}
