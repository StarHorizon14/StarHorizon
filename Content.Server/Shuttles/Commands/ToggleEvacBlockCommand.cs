using Content.Server.Administration;
using Content.Shared._Horizon.CCVar;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.Shuttles.Commands;

/// <summary>
/// Toggles whether the emergency shuttle can be called via the communications console.
/// Does not affect other means of calling/recalling the shuttle (e.g. admin commands).
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class ToggleEvacBlockCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override string Command => "toggleevacblock";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var blocked = !_cfg.GetCVar(HorizonCCVars.EvacBlocked);
        _cfg.SetCVar(HorizonCCVars.EvacBlocked, blocked);

        shell.WriteLine(blocked
            ? "Вызов эвакуационного шаттла через коммуникационную консоль заблокирован."
            : "Вызов эвакуационного шаттла через коммуникационную консоль разблокирован.");
    }
}
