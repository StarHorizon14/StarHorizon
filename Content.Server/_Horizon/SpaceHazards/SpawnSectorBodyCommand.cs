using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Shared._Horizon.SpaceHazards;
using Content.Shared.Administration;
using Content.Shared.Salvage.Expeditions.Modifiers;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Horizon.SpaceHazards;

/// <summary>
/// Spawns a sector star, black hole or planet on the caller's map, for testing and mapping.
/// </summary>
[AdminCommand(AdminFlags.Spawn)]
public sealed class SpawnSectorBodyCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public string Command => "spawnsectorbody";
    public string Description => "Spawns a sector star, black hole or planet.";
    public string Help => "spawnsectorbody <star|blackhole|planet|expedition> [x y] [option]\n" +
                          "Without x y it spawns at your position.\n" +
                          "planet = decorative background planet; option = kind 0-7 (0 LandRivers, 1 TerranDry, " +
                          "2 LandMasses, 3 NoAtmosphere, 4 GasPlanet, 5 GasPlanetLayers, 6 IceWorld, 7 LavaWorld; random if omitted).\n" +
                          "expedition = planet you can start expeditions from; option = biome " +
                          "(Caves, Grasslands, Snow, Lava, Shadow, ArdoniumWastes; Grasslands if omitted).\n" +
                          "Examples: spawnsectorbody expedition Snow | spawnsectorbody blackhole 5000 -3000";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        var type = args[0].ToLowerInvariant();
        var proto = type switch
        {
            "star" => "SectorCelestialStar",
            "blackhole" => "SectorCelestialBlackHole",
            "planet" => "SectorBackgroundPlanet",
            "expedition" => "ExpeditionPlanet",
            _ => null,
        };

        if (proto == null)
        {
            shell.WriteError($"Unknown type '{args[0]}'. Use star, blackhole, planet or expedition.");
            return;
        }

        var rest = args.Skip(1).ToList();
        Vector2? coords = null;
        if (rest.Count >= 2 && float.TryParse(rest[0], out var x) && float.TryParse(rest[1], out var y))
        {
            coords = new Vector2(x, y);
            rest = rest.Skip(2).ToList();
        }

        if (rest.Count > 1)
        {
            shell.WriteError(Help);
            return;
        }

        var option = rest.Count == 1 ? rest[0] : null;

        if (shell.Player?.AttachedEntity is not { } attached)
        {
            shell.WriteError("You need an attached entity so the map can be determined.");
            return;
        }

        var xformSys = _entMan.System<SharedTransformSystem>();
        var attachedXform = _entMan.GetComponent<TransformComponent>(attached);
        var mapId = attachedXform.MapID;
        if (mapId == MapId.Nullspace)
        {
            shell.WriteError("You must be on a map.");
            return;
        }

        var pos = coords ?? xformSys.GetWorldPosition(attachedXform);

        if (type == "expedition" && option != null && !_protos.HasIndex<SalvageBiomeModPrototype>(option))
        {
            shell.WriteError($"Unknown biome '{option}'. Use Caves, Grasslands, Snow, Lava, Shadow or ArdoniumWastes.");
            return;
        }

        var ent = _entMan.SpawnEntity(proto, new MapCoordinates(pos, mapId));

        if (type == "planet" && option != null && _entMan.TryGetComponent(ent, out SectorBackgroundPlanetComponent? planet))
        {
            if (!byte.TryParse(option, out var kind) || kind > (byte) PixelPlanetKind.LavaWorld)
            {
                shell.WriteError("planet kind must be 0-7; spawned with a random kind.");
            }
            else
            {
                planet.PlanetKind = (PixelPlanetKind) kind;
                _entMan.Dirty(ent, planet);
            }
        }
        else if (type == "expedition" && _entMan.TryGetComponent(ent, out ExpeditionPlanetComponent? expedition))
        {
            _entMan.System<ExpeditionPlanetSystem>().SetupPlanet(ent, expedition, option ?? "Grasslands", 0.01f + _random.NextFloat() * 9.99f);
        }

        shell.WriteLine($"Spawned {proto} ({ent}) at {pos} on map {mapId}.");
    }
}
