using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Shared._Horizon.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Horizon;

/// <summary>
/// Автоматическая смена экипажа: раунд завершается, когда таймер раунда
/// в лаунчере (UTC now - round_start_time) достигает <see cref="HorizonCCVars.ShutdownRoundHours"/> часов.
/// </summary>
public sealed class GameShutdownController
{
    [Dependency] private readonly IEntityManager _entityManager = null!;
    [Dependency] private readonly IConfigurationManager _cfg = null!;
    [Dependency] private readonly IGameTiming _timing = null!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("ShutdownController");
    private bool _shuttleCalled;
    private bool _enabled;

    /// <summary>
    /// Время старта раунда (UTC), для которого последний раз сбрасывалось состояние.
    /// </summary>
    private DateTime? _trackedRoundStartUtc;

    public void Init()
    {
        _enabled = _cfg.GetCVar(HorizonCCVars.ShutdownEnabled);
    }

    private TimeSpan GetRoundDuration()
    {
        var hours = _cfg.GetCVar(HorizonCCVars.ShutdownRoundHours);
        return TimeSpan.FromHours(hours > 0 ? hours : 48);
    }

    private bool TryGetRoundStart(out GameTicker ticker, out DateTime roundStartUtc)
    {
        roundStartUtc = default;

        if (!_entityManager.EntitySysManager.TryGetEntitySystem(out GameTicker? t))
        {
            ticker = null!;
            return false;
        }

        ticker = t;

        if (ticker.RunLevel != GameRunLevel.InRound)
            return false;

        roundStartUtc = ticker.RoundStartDateTimeUtc;
        return true;
    }

    /// <summary>
    /// Получает информацию о текущем состоянии таймера рестарта.
    /// </summary>
    public ShutdownStatus GetStatus()
    {
        if (!_enabled || !TryGetRoundStart(out _, out var roundStart))
            return new ShutdownStatus(_enabled, null, null, null, null, _shuttleCalled);

        var restart = roundStart + GetRoundDuration();
        var shuttleCall = restart - TimeSpan.FromMinutes(_cfg.GetCVar(HorizonCCVars.ShutdownShuttleTime));

        return new ShutdownStatus(true, DateTime.UtcNow, roundStart, restart, shuttleCall, _shuttleCalled);
    }

    /// <summary>
    /// Момент принудительного рестарта в игровом времени (<see cref="IGameTiming.CurTime"/>),
    /// в той же шкале, что и <c>RoundEndSystem.ExpectedCountdownEnd</c>. Null, если таймер выключен,
    /// раунд не идёт или лимит уже достигнут.
    /// </summary>
    public TimeSpan? GetRestartGameTime()
    {
        if (!_enabled || !TryGetRoundStart(out _, out var roundStart))
            return null;

        var remaining = roundStart + GetRoundDuration() - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? _timing.CurTime + remaining : null;
    }

    /// <param name="Enabled">Включён ли таймер.</param>
    /// <param name="NowUtc">Текущее время (UTC), null если раунд не идёт.</param>
    /// <param name="RoundStartUtc">round_start_time, как его видит лаунчер.</param>
    /// <param name="RestartUtc">Момент, когда таймер раунда в лаунчере покажет лимит.</param>
    /// <param name="ShuttleCallUtc">Момент вызова шаттла.</param>
    public record ShutdownStatus(
        bool Enabled,
        DateTime? NowUtc,
        DateTime? RoundStartUtc,
        DateTime? RestartUtc,
        DateTime? ShuttleCallUtc,
        bool ShuttleCalled);

    /// <summary>
    /// Включает таймер рестарта.
    /// </summary>
    public void Enable()
    {
        _enabled = true;
        _trackedRoundStartUtc = null;
        _sawmill.Info("Таймер рестарта включен.");
    }

    /// <summary>
    /// Выключает таймер рестарта.
    /// </summary>
    public void Disable()
    {
        _enabled = false;
        _trackedRoundStartUtc = null;
        _shuttleCalled = false;
        _sawmill.Info("Таймер рестарта выключен.");
    }

    public void Update()
    {
        if (!_enabled)
            return;

        // Раунд не идёт (лобби/пост-раунд) — ждём новый.
        if (!TryGetRoundStart(out _, out var roundStart))
            return;

        // Начался новый раунд — сбрасываем состояние шаттла.
        if (_trackedRoundStartUtc != roundStart)
        {
            _trackedRoundStartUtc = roundStart;
            _shuttleCalled = false;

            _sawmill.Info($"Новый раунд (старт {roundStart:dd.MM.yyyy HH:mm:ss} UTC). " +
                          $"Рестарт при {GetRoundDuration().TotalHours:0.##}ч раунда: " +
                          $"{roundStart + GetRoundDuration():dd.MM.yyyy HH:mm:ss} UTC");
        }

        var nowUtc = DateTime.UtcNow;
        var restartUtc = roundStart + GetRoundDuration();
        var timeUntilRestart = restartUtc - nowUtc;
        var shuttleTime = TimeSpan.FromMinutes(_cfg.GetCVar(HorizonCCVars.ShutdownShuttleTime));

        if (!_entityManager.EntitySysManager.TryGetEntitySystem(out RoundEndSystem? roundEndSystem))
            return;

        // Вызываем шаттл эвакуации за shuttleTime до рестарта
        if (timeUntilRestart <= shuttleTime &&
            timeUntilRestart > TimeSpan.Zero &&
            !_shuttleCalled)
        {
            roundEndSystem.RequestRoundEnd(timeUntilRestart, null, false,
                "shutdown-shuttle-called-announcement",
                "shutdown-shuttle-sender");

            _shuttleCalled = true;
            _sawmill.Info($"Шаттл эвакуации вызван. Рестарт в {restartUtc:HH:mm:ss} UTC");
        }

        // Таймер раунда в лаунчере достиг лимита
        if (timeUntilRestart <= TimeSpan.Zero)
        {
            roundEndSystem.EndRound();
            _sawmill.Info($"Рестарт выполнен в {nowUtc:HH:mm:ss} UTC (раунд длился {nowUtc - roundStart:hh\\:mm\\:ss})");
        }
    }
}
