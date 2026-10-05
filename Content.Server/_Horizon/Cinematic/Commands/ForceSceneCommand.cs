using System.Linq;
using Content.Server._Horizon.Cinematic;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Horizon.Cinematic.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class ForceSceneCommand : IConsoleCommand
{
    // Content.Server doesn't reference Robust.Client, so unlike the engine's local "scene" command
    // we can't reflect over Robust.Client.State.State subclasses to build this list - it has to be
    // maintained by hand. This is every concrete State subclass in the engine and content as of
    // writing (LoadingScreen<TResult> excluded - it's an open generic and can't be instantiated by
    // name at all). Some of these (the replay/launcher/debug ones) will misbehave if forced outside
    // their normal context - that's on the admin using them.
    private static readonly string[] KnownScenes =
    {
        "GameplayState",
        "CinematicState",
        "MappingState",
        "LobbyState",
        "MainScreen",
        "QueueState",
        "GameplayStateBase",
        "ReplaySpectateEntityState",
        "ReplayGhostState",
        "ReplayLoadingFailed",
        "LauncherConnecting",
        "DefaultState",
        "DebugBuiltinConnectionScreenState",
    };

    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public string Command => "forcescene";
    public string Description => "Forces a specific player's client to switch to the named UI scene/state.";
    public string Help => "forcescene <username> <sceneClassName>\n" +
                           "sceneClassName is matched the same way the engine's local \"scene\" console command " +
                           "matches state classes: by the end of the class's full name (e.g. CinematicState, GameplayState).";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!_playerManager.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError($"Can't find player named {args[0]}");
            return;
        }

        _entManager.System<ForceSceneSystem>().ForceScene(session, args[1]);
        shell.WriteLine($"Told {args[0]}'s client to switch to scene '{args[1]}'.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(c => c.Name);
            return CompletionResult.FromHintOptions(options, "<username>");
        }

        if (args.Length == 2)
            return CompletionResult.FromHintOptions(KnownScenes, "<sceneClassName>");

        return CompletionResult.Empty;
    }
}
