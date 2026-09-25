using Content.Server._Horizon.TheEndEvent;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Horizon.Administration;

[AdminCommand(AdminFlags.Round)]
public sealed class TheEndEventCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override string Command => "theendevent";

    public override string Description => "Ramps a screen-covering static effect on every client over 20 seconds, then switches them to a fake connection-lost scene.";

    public override string Help => $"{Command} [true|false]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Loc.GetString("shell-need-between-arguments", ("lower", 0), ("upper", 1)));
            return;
        }

        var system = _entManager.System<TheEndEventSystem>();
        var enabled = !system.Active;

        if (args.Length == 1 && !bool.TryParse(args[0], out enabled))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-boolean"));
            return;
        }

        system.SetActive(enabled);
        shell.WriteLine(enabled ? "The end event started." : "The end event stopped.");
    }
}
