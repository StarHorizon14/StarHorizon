using Content.Server.Administration.Logs;
using Content.Shared._Horizon.TheEndEvent;
using Content.Shared.Database;
using Robust.Server.Player;

namespace Content.Server._Horizon.TheEndEvent;

/// <summary>
/// Admin-toggleable event: ramps a screen-covering static overlay up on every client over
/// a fixed duration. Once it fully covers the screen, clients are told to switch to the
/// fake "connection lost" scene.
/// See <see cref="Content.Server._Horizon.Administration.TheEndEventCommand"/> for the trigger.
/// </summary>
public sealed class TheEndEventSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    private const float RampDurationSeconds = 20f;

    public bool Active { get; private set; }
    private float _elapsed;
    private bool _reached;

    public void SetActive(bool active)
    {
        if (Active == active)
            return;

        Active = active;
        _elapsed = 0f;
        _reached = false;

        _adminLog.Add(LogType.AdminMessage, LogImpact.Extreme,
            $"The end event {(active ? "activated" : "deactivated")}");

        if (!active)
        {
            var resetEv = new TheEndEventProgressMessage(0f);
            foreach (var session in _playerManager.Sessions)
                RaiseNetworkEvent(resetEv, session.Channel);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Active || _reached)
            return;

        _elapsed += frameTime;
        var intensity = Math.Clamp(_elapsed / RampDurationSeconds, 0f, 1f);

        var progressEv = new TheEndEventProgressMessage(intensity);
        foreach (var session in _playerManager.Sessions)
            RaiseNetworkEvent(progressEv, session.Channel);

        if (intensity >= 1f)
        {
            _reached = true;
            var reachedEv = new TheEndEventReachedMessage();
            foreach (var session in _playerManager.Sessions)
                RaiseNetworkEvent(reachedEv, session.Channel);
        }
    }
}
