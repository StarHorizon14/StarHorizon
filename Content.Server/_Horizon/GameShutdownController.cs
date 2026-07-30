using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Robust.Server;
using Content.Shared._Horizon.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Horizon;

public sealed class GameShutdownController
{
    [Dependency] private readonly IEntityManager _entityManager = null!;
    [Dependency] private readonly IConfigurationManager _cfg = null!;
    [Dependency] private readonly IBaseServer _server = null!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("ShutdownController");
    private DateTime? _nextRestartTimeMsk;
    private bool _shuttleCalled;
    private bool _enabled;

    // Московское время (UTC+3)
    private static readonly TimeZoneInfo MskTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "MSK", TimeSpan.FromHours(3), "Moscow Standard Time", "MSK");

    public void Init()
    {
        _enabled = _cfg.GetCVar(HorizonCCVars.ShutdownEnabled);
        if (!_enabled)
            return;

        CalculateNextRestartTime();
    }

    /// <summary>
    /// Получает текущее время по МСК (UTC+3).
    /// </summary>
    private static DateTime GetCurrentMskTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MskTimeZone);
    }

    /// <summary>
    /// Рассчитывает время следующего рестарта по МСК на основе CVars.
    /// </summary>
    private void CalculateNextRestartTime()
    {
        var timeStr = _cfg.GetCVar(HorizonCCVars.ShutdownTime);
        var intervalDays = _cfg.GetCVar(HorizonCCVars.ShutdownIntervalDays);
        var intervalHours = _cfg.GetCVar(HorizonCCVars.ShutdownIntervalHours);

        if (!TimeSpan.TryParse(timeStr, out var timeOfDay))
        {
            _sawmill.Error($"Invalid shutdown.time format: '{timeStr}'. Expected 'HH:mm' (e.g. '12:00')");
            _enabled = false;
            return;
        }

        var nowMsk = GetCurrentMskTime();
        var totalInterval = TimeSpan.FromDays(intervalDays) + TimeSpan.FromHours(intervalHours);

        // Если интервал не задан, используем 24 часа
        if (totalInterval <= TimeSpan.Zero)
            totalInterval = TimeSpan.FromDays(1);

        // Находим базовое время сегодня
        var todayAtTime = nowMsk.Date + timeOfDay;

        // Если это время уже прошло сегодня, добавляем интервал
        if (todayAtTime <= nowMsk)
            todayAtTime += totalInterval;

        _nextRestartTimeMsk = todayAtTime;
        _shuttleCalled = false;

        _sawmill.Info($"Следующий рестарт запланирован на {todayAtTime:dd.MM.yyyy HH:mm:ss} МСК " +
                      $"(интервал: {intervalDays}д {intervalHours}ч)");
    }

    /// <summary>
    /// Получает информацию о текущем состоянии таймера рестарта.
    /// </summary>
    public ShutdownStatus GetStatus()
    {
        if (!_enabled || _nextRestartTimeMsk == null)
            return new ShutdownStatus(false, null, null, null, false);

        var nowMsk = GetCurrentMskTime();
        var shuttleTime = TimeSpan.FromMinutes(_cfg.GetCVar(HorizonCCVars.ShutdownShuttleTime));
        var shuttleCallTime = _nextRestartTimeMsk.Value - shuttleTime;

        return new ShutdownStatus(
            true,
            nowMsk,
            _nextRestartTimeMsk.Value,
            shuttleCallTime,
            _shuttleCalled);
    }

    public record ShutdownStatus(
        bool Enabled,
        DateTime? CurrentTimeMsk,
        DateTime? RestartTimeMsk,
        DateTime? ShuttleCallTimeMsk,
        bool ShuttleCalled);

    /// <summary>
    /// Включает таймер рестарта.
    /// </summary>
    public void Enable()
    {
        _enabled = true;
        CalculateNextRestartTime();
        _sawmill.Info("Таймер рестарта включен.");
    }

    /// <summary>
    /// Выключает таймер рестарта.
    /// </summary>
    public void Disable()
    {
        _enabled = false;
        _nextRestartTimeMsk = null;
        _shuttleCalled = false;
        _sawmill.Info("Таймер рестарта выключен.");
    }

    public void Update()
    {
        if (!_enabled || _nextRestartTimeMsk == null)
            return;

        var nowMsk = GetCurrentMskTime();
        var timeUntilRestart = _nextRestartTimeMsk.Value - nowMsk;
        var shuttleTime = TimeSpan.FromMinutes(_cfg.GetCVar(HorizonCCVars.ShutdownShuttleTime));

        // Вызываем шаттл эвакуации за shuttleTime до рестарта
        if (timeUntilRestart <= shuttleTime &&
            timeUntilRestart > TimeSpan.Zero &&
            !_shuttleCalled)
        {
            if (_entityManager.EntitySysManager.TryGetEntitySystem(out RoundEndSystem? roundEndSystem))
            {
                roundEndSystem.RequestRoundEnd(timeUntilRestart, null, false,
                    "shutdown-shuttle-called-announcement",
                    "shutdown-shuttle-sender");

                _shuttleCalled = true;
                _sawmill.Info($"Шаттл эвакуации вызван. Рестарт в {_nextRestartTimeMsk.Value:HH:mm:ss} МСК");
            }
        }

        // Если время рестарта наступило
        if (nowMsk >= _nextRestartTimeMsk.Value)
        {
            if (_entityManager.EntitySysManager.TryGetEntitySystem(out RoundEndSystem? roundEndSystem))
                roundEndSystem.EndRound();

            _sawmill.Info($"Рестарт выполнен в {nowMsk:HH:mm:ss} МСК");

            // Рассчитываем следующий рестарт
            CalculateNextRestartTime();
        }
    }
}
