using System.Numerics;
using Content.Server.Administration;
using Content.Shared._Horizon.Mirage;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Horizon.Mirage;

/// <summary>
/// Makes an entity show a mirage of the area around another entity.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed class MirageLinkCommand : LocalizedEntityCommands
{
    [Dependency] private readonly MirageBorderSystem _mirage = default!;

    public override string Command => "miragelink";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 && args.Length != 6)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            shell.WriteLine(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var borderNet) || !EntityManager.TryGetEntity(borderNet, out var border))
        {
            shell.WriteError(Loc.GetString("shell-invalid-entity-uid", ("uid", args[0])));
            return;
        }

        if (!NetEntity.TryParse(args[1], out var targetNet) || !EntityManager.TryGetEntity(targetNet, out var target))
        {
            shell.WriteError(Loc.GetString("shell-invalid-entity-uid", ("uid", args[1])));
            return;
        }

        var comp = EntityManager.EnsureComponent<MirageBorderComponent>(border.Value);
        _mirage.SetTarget((border.Value, comp), target);

        if (args.Length == 6)
        {
            if (!float.TryParse(args[2], out var offsetX) || !float.TryParse(args[3], out var offsetY)
                || !float.TryParse(args[4], out var sizeX) || !float.TryParse(args[5], out var sizeY))
            {
                shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
                return;
            }

            _mirage.SetArea((border.Value, comp), new Vector2(offsetX, offsetY), new Vector2(sizeX, sizeY));
        }

        shell.WriteLine(Loc.GetString("cmd-miragelink-success", ("border", border.Value), ("target", target.Value)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint(Loc.GetString("cmd-miragelink-hint-border")),
            2 => CompletionResult.FromHint(Loc.GetString("cmd-miragelink-hint-target")),
            3 or 4 => CompletionResult.FromHint(Loc.GetString("cmd-miragelink-hint-offset")),
            5 or 6 => CompletionResult.FromHint(Loc.GetString("cmd-miragelink-hint-size")),
            _ => CompletionResult.Empty,
        };
    }
}
