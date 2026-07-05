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

        var timeUntilRestart = status.RestartTimeMsk!.Value - status.CurrentTimeMsk!.Value;
        var timeUntilShuttle = status.ShuttleCallTimeMsk!.Value - status.CurrentTimeMsk!.Value;

        shell.WriteLine($"Текущее время МСК:    {status.CurrentTimeMsk:dd.MM.yyyy HH:mm:ss}");
        shell.WriteLine($"Время рестарта:   {status.RestartTimeMsk:dd.MM.yyyy HH:mm:ss}");
        shell.WriteLine($"Вызов шаттла:     {status.ShuttleCallTimeMsk:dd.MM.yyyy HH:mm:ss}");
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
