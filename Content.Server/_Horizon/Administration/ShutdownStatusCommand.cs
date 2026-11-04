using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Horizon.Administration;

[AdminCommand(AdminFlags.Admin)]
public sealed class ShutdownStatusCommand : IConsoleCommand
{
    [Dependency] private readonly GameShutdownController _shutdownController = default!;

    public string Command => "shutdownstatus";
    public string Description => "Показывает статус таймера автоматического рестарта сервера.";
    public string Help => "Usage: shutdownstatus";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var status = _shutdownController.GetStatus();

        if (!status.Enabled)
        {
            shell.WriteLine("Таймер рестарта: ВЫКЛЮЧЕН");
            return;
        }

        if (status.RestartUtc == null)
        {
            shell.WriteLine("Таймер рестарта: ВКЛЮЧЕН (раунд не идёт, ожидание старта раунда)");
            return;
        }

        var now = status.NowUtc!.Value;
        var elapsed = now - status.RoundStartUtc!.Value;
        var timeUntilRestart = status.RestartUtc.Value - now;
        var timeUntilShuttle = status.ShuttleCallUtc!.Value - now;

        // Формат как в лаунчере: Ч:ММ
        shell.WriteLine($"Таймер раунда:    {(int)elapsed.TotalHours}:{elapsed.Minutes:00}");
        shell.WriteLine($"Старт раунда UTC: {status.RoundStartUtc:dd.MM.yyyy HH:mm:ss}");
        shell.WriteLine($"Рестарт UTC:      {status.RestartUtc:dd.MM.yyyy HH:mm:ss}");
        shell.WriteLine($"Вызов шаттла UTC: {status.ShuttleCallUtc:dd.MM.yyyy HH:mm:ss}");
        shell.WriteLine($"До рестарта:      {FormatTimeSpan(timeUntilRestart)}");
        shell.WriteLine($"До вызова шаттла: {(status.ShuttleCalled ? "вызван" : FormatTimeSpan(timeUntilShuttle))}");
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalSeconds < 0)
            return "уже прошло";

        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}д {ts.Hours}ч {ts.Minutes}м";

        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}ч {ts.Minutes}м {ts.Seconds}с";

        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}м {ts.Seconds}с";

        return $"{ts.Seconds}с";
    }
}
