using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._Horizon.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class CastawayAmbienceCommand : IConsoleCommand
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public string Command => "castawayambience";
    public string Description => "Enables or disables the Castaway game rule's ambient music for everyone.";
    public string Help => "castawayambience <true|false>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !bool.TryParse(args[0], out var enabled))
        {
            shell.WriteLine(Help);
            return;
        }

        _cfg.SetCVar(CCVars.CastawayAmbienceEnabled, enabled);
        shell.WriteLine($"Castaway ambience {(enabled ? "enabled" : "disabled")}.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(new[] { "true", "false" }, "<enabled>");

        return CompletionResult.Empty;
    }
}
