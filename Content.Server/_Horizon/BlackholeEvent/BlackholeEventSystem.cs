using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Shared._Horizon.BlackholeEvent;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Horizon.BlackholeEvent;

/// <summary>
/// Admin-toggleable lockdown: while active, players cannot enter the round.
/// Instead of the gameplay state, clients are shown a full-screen blackhole scene.
/// See <see cref="Content.Server.GameTicking.GameTicker.PlayerJoinGame"/> for the intercept point.
/// </summary>
public sealed class BlackholeEventSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    public bool Active { get; private set; }

    public void SetActive(bool active)
    {
        if (Active == active)
            return;

        Active = active;
        _adminLog.Add(LogType.AdminMessage, LogImpact.Extreme,
            $"Blackhole event {(active ? "activated" : "deactivated")}");

        var ev = new BlackholeEventStateMessage(active);
        foreach (var session in _playerManager.Sessions)
        {
            RaiseNetworkEvent(ev, session.Channel);
        }

        if (!active)
        {
            // Let anyone who was held back actually join the round now.
            var gameTicker = EntityManager.System<GameTicker>();
            foreach (var session in _playerManager.Sessions)
            {
                if (session.Status != SessionStatus.InGame)
                    continue;

                if (gameTicker.PlayerGameStatuses.TryGetValue(session.UserId, out var status) &&
                    status == PlayerGameStatus.JoinedGame)
                {
                    RaiseNetworkEvent(new TickerJoinGameEvent(), session.Channel);
                }
            }
        }
    }
}
