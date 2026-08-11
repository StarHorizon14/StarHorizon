using Content.Server.Administration;
using Content.Server.RoundEnd;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Shuttles.Commands;

/// <summary>
/// Cancels the emergency evacuation shuttle countdown without a chat announcement or sound.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class SilentCancelEvacCommand : LocalizedEntityCommands
{
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;

    public override string Command => "silentcancelevac";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _roundEndSystem.CancelRoundEndCountdown(shell.Player?.AttachedEntity, checkCooldown: false, silent: true);
    }
}
