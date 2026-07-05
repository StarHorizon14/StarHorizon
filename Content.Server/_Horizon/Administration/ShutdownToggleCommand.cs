using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Horizon.Administration;

[AdminCommand(AdminFlags.Admin)]
public sealed class ShutdownToggleCommand : IConsoleCommand
{
    [Dependency] private readonly GameShutdownController _shutdownController = default!;

    public string Command => "shutdowntoggle";
    public string Description => "Включает или выключает таймер автоматического рестарта.";
    public string Help => "Usage: shutdowntoggle [on/off]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            var status = _shutdownController.GetStatus();
            shell.WriteLine($"Таймер рестарта: {(status.Enabled ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");
            shell.WriteLine("Используйте: shutdowntoggle on/off");
            return;
        }

        switch (args[0].ToLower())
        {
            case "on":
            case "1":
            case "true":
                _shutdownController.Enable();
                shell.WriteLine("Таймер рестарта ВКЛЮЧЕН.");
                break;

            case "off":
            case "0":
            case "false":
                _shutdownController.Disable();
                shell.WriteLine("Таймер рестарта ВЫКЛЮЧЕН.");
                break;

            default:
                shell.WriteLine("Неверный аргумент. Используйте: shutdowntoggle on/off");
                break;
        }
    }
}
