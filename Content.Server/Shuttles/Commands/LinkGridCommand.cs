using Content.Server._NF.Shipyard.Systems;
using Content.Server.Administration;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Access.Components;
using Content.Shared.Administration;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Commands;

/// <summary>
/// Turns a grid into its own station, the same way the shipyard does for vessels that are
/// meant to be standalone stations: StationMemberComponent (via InitializeNewStation), player
/// shuttle IFF, a ShuttleDeedComponent tied to the given player ID card, and
/// LinkedLifecycleGridParentComponent for cleanup — instead of just docking it to an existing
/// station like a regular shuttle purchase does.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class LinkGridCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;

    private static readonly EntProtoId DefaultStationProto = "StandardFrontierVessel";

    public override string Command => "linkgrid";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            shell.WriteError(Loc.GetString("cmd-invalid-arg-number-error"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var gridIdNet))
        {
            shell.WriteError(Loc.GetString("cmd-parse-failure-uid", ("arg", args[0])));
            return;
        }

        if (!_ent.TryGetEntity(gridIdNet, out var uid)
            || !_ent.HasComponent<MapGridComponent>(uid)
            || _ent.HasComponent<MapComponent>(uid))
        {
            shell.WriteError(Loc.GetString("cmd-parse-failure-grid", ("arg", args[0])));
            return;
        }

        if (!NetEntity.TryParse(args[1], out var idCardNet))
        {
            shell.WriteError(Loc.GetString("cmd-parse-failure-uid", ("arg", args[1])));
            return;
        }

        if (!_ent.TryGetEntity(idCardNet, out var idCardUid) || !_ent.HasComponent<IdCardComponent>(idCardUid))
        {
            shell.WriteError($"{args[1]} не является ID-картой (нет IdCardComponent).");
            return;
        }

        if (_ent.HasComponent<ShuttleDeedComponent>(idCardUid))
        {
            shell.WriteError($"На ID-карте {args[1]} уже есть привязанный шаттл.");
            return;
        }

        if (_ent.HasComponent<StationMemberComponent>(uid))
        {
            var existingStation = _station.GetOwningStation(uid.Value);
            shell.WriteError($"Грид {args[0]} уже привязан к станции {existingStation}.");
            return;
        }

        var color = Color.White;
        if (args.Length > 2)
        {
            var parsedColor = Color.TryFromHex(args[2]);
            if (parsedColor is null)
            {
                shell.WriteError($"{args[2]} не является корректным hex-цветом.");
                return;
            }

            color = parsedColor.Value;
        }

        var stationConfig = new StationConfig
        {
            StationPrototype = DefaultStationProto,
            StationComponentOverrides = new ComponentRegistry(),
        };

        var stationName = _ent.GetComponent<MetaDataComponent>(uid.Value).EntityName;
        var station = _station.InitializeNewStation(stationConfig, new[] { uid.Value }, stationName);
        var finalName = _ent.GetComponent<MetaDataComponent>(station).EntityName;

        _shuttle.SetPlayerShuttleIFF(uid.Value, color);

        var owner = shell.Player?.Name ?? "Console";
        _shipyard.RegisterShuttleDeed(idCardUid.Value, uid.Value, finalName, owner);

        shell.WriteLine($"Грид ({args[0]}) привязан к новой станции {station} ({finalName}) и ID-карте {args[1]}.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.Components<MapGridComponent>(args[^1], _ent), "<GridUid>"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.Components<IdCardComponent>(args[^1], _ent), "<IdCardUid>"),
            3 => CompletionResult.FromHint("[Color: hex]"),
            _ => CompletionResult.Empty
        };
    }
}
