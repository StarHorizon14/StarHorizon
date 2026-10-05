using System.Globalization;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Shuttles.Commands;

/// <summary>
/// Sends an arbitrary entity (not just a grid/shuttle - characters, items, etc.) on an actual FTL trip
/// to the given coordinates, through the hyperspace map with startup/travel time, the same way
/// ftlgrid does for grids. By default this checks whether the FTL is actually allowed, unless the
/// ignoreCheck argument is passed.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class FtlEntityCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    public override string Command => "ftlentity";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 3 or > 5)
        {
            shell.WriteError(Loc.GetString("cmd-invalid-arg-number-error"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var entNet))
        {
            shell.WriteError(Loc.GetString("cmd-parse-failure-uid", ("arg", args[0])));
            return;
        }

        if (!_ent.TryGetEntity(entNet, out var uid) || !_ent.EntityExists(uid))
        {
            shell.WriteError(Loc.GetString("cmd-parse-failure-uid", ("arg", args[0])));
            return;
        }

        if (_ent.HasComponent<MapComponent>(uid))
        {
            shell.WriteError($"Нельзя отправить карту ({args[0]}) в БСС.");
            return;
        }

        if (_ent.HasComponent<FTLComponent>(uid))
        {
            shell.WriteError($"Сущность {args[0]} уже находится в БСС.");
            return;
        }

        if (!float.TryParse(args[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var xPos))
        {
            shell.WriteError(Loc.GetString("cmd-parse-failure-float", ("arg", args[1])));
            return;
        }

        if (!float.TryParse(args[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var yPos))
        {
            shell.WriteError(Loc.GetString("cmd-parse-failure-float", ("arg", args[2])));
            return;
        }

        var entXform = _ent.GetComponent<TransformComponent>(uid.Value);
        var mapId = entXform.MapID;

        if (args.Length > 3)
        {
            if (!int.TryParse(args[3], out var map))
            {
                shell.WriteError(Loc.GetString("cmd-parse-failure-mapid", ("arg", args[3])));
                return;
            }

            mapId = new MapId(map);
        }

        var ignoreCheck = false;
        if (args.Length > 4 && !bool.TryParse(args[4], out ignoreCheck))
        {
            shell.WriteError(Loc.GetString("cmd-parse-failure-bool", ("arg", args[4])));
            return;
        }

        var mapUid = _map.GetMap(mapId);
        if (mapUid == EntityUid.Invalid)
        {
            shell.WriteError(Loc.GetString("cmd-parse-failure-mapid", ("arg", mapId)));
            return;
        }

        if (!ignoreCheck)
        {
            if (!_shuttle.CanFTL(uid.Value, out var reason))
            {
                shell.WriteError($"Совершить БСС невозможно: {reason}");
                return;
            }

            if (!_shuttle.CanFTLTo(uid.Value, mapId, uid.Value))
            {
                shell.WriteError("Совершить БСС на указанную карту невозможно (проверка CanFTLTo не пройдена). Передайте true пятым аргументом, чтобы проигнорировать проверку.");
                return;
            }
        }

        var pos = new EntityCoordinates(mapUid, new Vector2(xPos, yPos));

        if (!ignoreCheck && !_shuttle.FTLFree(uid.Value, pos, entXform.LocalRotation, null))
        {
            shell.WriteError("Целевые координаты заняты (недостаточно места для БСС). Передайте true пятым аргументом, чтобы проигнорировать проверку.");
            return;
        }

        _shuttle.FTLToCoordinates(uid.Value, null, pos, entXform.LocalRotation);

        shell.WriteLine($"Сущность ({args[0]}) отправлена в БСС к ({xPos}, {yPos}) на карте {mapId}.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("<EntityUid>"),
            2 => CompletionResult.FromHint("<x>"),
            3 => CompletionResult.FromHint("<y>"),
            4 => CompletionResult.FromHintOptions(CompletionHelper.MapIds(_ent), "[MapId]"),
            5 => CompletionResult.FromHint("[ignoreCheck: true/false]"),
            _ => CompletionResult.Empty
        };
    }
}
