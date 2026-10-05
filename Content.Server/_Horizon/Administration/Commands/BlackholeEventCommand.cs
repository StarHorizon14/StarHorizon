using Content.Server._Horizon.BlackholeEvent;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Horizon.Administration;

[AdminCommand(AdminFlags.Round)]
public sealed class BlackholeEventCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override string Command => "blackholeevent";

    public override string Description => "Toggles the blackhole lockdown scene, blocking all players from entering the round.";

    public override string Help => $"{Command} [true|false]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Loc.GetString("shell-need-between-arguments", ("lower", 0), ("upper", 1)));
            return;
        }

        var system = _entManager.System<BlackholeEventSystem>();
        var enabled = !system.Active;

        if (args.Length == 1 && !bool.TryParse(args[0], out enabled))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-boolean"));
            return;
        }

        system.SetActive(enabled);
        shell.WriteLine(enabled ? "Blackhole event enabled." : "Blackhole event disabled.");
    }
}
