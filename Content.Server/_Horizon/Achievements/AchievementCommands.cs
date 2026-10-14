using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Horizon.Achievements;

[AdminCommand(AdminFlags.Admin)]
public sealed class AddAchievementCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly AchievementSystem _achievements = default!;

    public override string Command => "addachievement";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("cmd-addachievement-help"));
            return;
        }

        var name = string.Join(' ', args.Skip(1)).Trim();
        if (name.Length == 0 || name.Length > AchievementSystem.MaxNameLength)
        {
            shell.WriteError(Loc.GetString("achievement-command-bad-name", ("max", AchievementSystem.MaxNameLength)));
            return;
        }

        var player = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (player == null)
        {
            shell.WriteError(Loc.GetString("achievement-command-no-player", ("player", args[0])));
            return;
        }

        if (!_achievements.AddAchievement(player.UserId, name))
        {
            shell.WriteError(Loc.GetString("achievement-command-already-has", ("player", player.Username), ("name", name)));
            return;
        }

        _adminLog.Add(LogType.AdminMessage, LogImpact.Low, $"{shell.Player?.Name ?? "Server"} granted achievement \"{name}\" to {player.Username}");
        shell.WriteLine(Loc.GetString("achievement-command-added", ("player", player.Username), ("name", name)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(_players.Sessions.OrderBy(c => c.Name).Select(c => c.Name), Loc.GetString("achievement-command-hint-player"));

        return CompletionResult.FromHint(Loc.GetString("achievement-command-hint-name"));
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class RemoveAchievementCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly AchievementSystem _achievements = default!;

    public override string Command => "removeachievement";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("cmd-removeachievement-help"));
            return;
        }

        var name = string.Join(' ', args.Skip(1)).Trim();

        var player = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (player == null)
        {
            shell.WriteError(Loc.GetString("achievement-command-no-player", ("player", args[0])));
            return;
        }

        if (!_achievements.RemoveAchievement(player.UserId, name))
        {
            shell.WriteError(Loc.GetString("achievement-command-not-found", ("player", player.Username), ("name", name)));
            return;
        }

        _adminLog.Add(LogType.AdminMessage, LogImpact.Low, $"{shell.Player?.Name ?? "Server"} removed achievement \"{name}\" from {player.Username}");
        shell.WriteLine(Loc.GetString("achievement-command-removed", ("player", player.Username), ("name", name)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(_players.Sessions.OrderBy(c => c.Name).Select(c => c.Name), Loc.GetString("achievement-command-hint-player"));

        return CompletionResult.FromHint(Loc.GetString("achievement-command-hint-name"));
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class ListAchievementsCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly AchievementSystem _achievements = default!;

    public override string Command => "achievements";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("cmd-achievements-help"));
            return;
        }

        var player = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (player == null)
        {
            shell.WriteError(Loc.GetString("achievement-command-no-player", ("player", args[0])));
            return;
        }

        var list = _achievements.GetAchievements(player.UserId);
        shell.WriteLine(list.Count == 0
            ? Loc.GetString("achievement-command-list-empty", ("player", player.Username))
            : Loc.GetString("achievement-command-list", ("player", player.Username), ("achievements", string.Join(", ", list))));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(_players.Sessions.OrderBy(c => c.Name).Select(c => c.Name), Loc.GetString("achievement-command-hint-player"));

        return CompletionResult.Empty;
    }
}
