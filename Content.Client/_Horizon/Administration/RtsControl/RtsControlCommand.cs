using Robust.Shared.Console;

namespace Content.Client._Horizon.Administration.RtsControl;

public sealed class RtsControlCommand : LocalizedEntityCommands
{
    [Dependency] private readonly RtsControlSystem _rts = default!;

    public override string Command => "rtscontrol";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        switch (_rts.Toggle())
        {
            case true:
                shell.WriteLine(Loc.GetString("cmd-rtscontrol-enabled"));
                break;
            case false:
                shell.WriteLine(Loc.GetString("cmd-rtscontrol-disabled"));
                break;
            default:
                shell.WriteError(Loc.GetString("cmd-rtscontrol-no-permission"));
                break;
        }
    }
}
