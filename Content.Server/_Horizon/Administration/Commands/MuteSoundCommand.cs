using System.Linq;
using Content.Server._Horizon.Audio;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Horizon.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class MuteSoundCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public string Command => "mutesound";
    public string Description => "Silences currently playing audio, either globally or for the given players.";
    public string Help => "mutesound <fadeSeconds> <onlyGlobalSound> [username] [username2] [...]\n" +
                           "fadeSeconds: how long the audio should take to fade out; 0 mutes instantly.\n" +
                           "onlyGlobalSound: if true, only mutes audio played via the admin \"playglobalsound\" command instead of everything.\n" +
                           "username: if omitted, mutes every connected player instead of specific ones.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!float.TryParse(args[0], out var fadeSeconds) || fadeSeconds < 0f)
        {
            shell.WriteError($"'{args[0]}' is not a valid fade time.");
            return;
        }

        if (!bool.TryParse(args[1], out var onlyGlobalSound))
        {
            shell.WriteError($"'{args[1]}' is not a valid boolean.");
            return;
        }

        var muteSystem = _entManager.System<MuteSoundSystem>();

        if (args.Length == 2)
        {
            foreach (var session in _playerManager.Sessions)
            {
                muteSystem.Mute(session, fadeSeconds, onlyGlobalSound);
            }

            return;
        }

        for (var i = 2; i < args.Length; i++)
        {
            var username = args[i];

            if (!_playerManager.TryGetSessionByUsername(username, out var session))
            {
                shell.WriteError($"Can't find player named {username}");
                continue;
            }

            muteSystem.Mute(session, fadeSeconds, onlyGlobalSound);
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint("<fadeSeconds>");

        if (args.Length == 2)
            return CompletionResult.FromHintOptions(new[] { "true", "false" }, "<onlyGlobalSound>");

        if (args.Length > 2)
        {
            var options = _playerManager.Sessions.Select(c => c.Name);
            return CompletionResult.FromHintOptions(options, $"<username {args.Length - 2}>");
        }

        return CompletionResult.Empty;
    }
}
