using Content.Shared._Horizon.Planets.Procedural;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._Horizon.Planets;

[AnyCommand]
public sealed class AddPlanetCommand : IConsoleCommand
{
    public string Command => "addplanet";
    public string Description => "Добавляет процедурную планету на текущую карту.";
    public string Help => "Использование: addplanet <size> <shader>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player?.AttachedEntity == null)
        {
            shell.WriteError("У вас должна быть прикрепленная сущность!");
            return;
        }

        var entManager = IoCManager.Resolve<IEntityManager>();
        var xformSystem = entManager.System<SharedTransformSystem>();
        var mapSystem = entManager.System<SharedMapSystem>();

        var xform = entManager.GetComponent<TransformComponent>(player.AttachedEntity.Value);
        var mapId = xform.MapID;

        if (mapId == MapId.Nullspace)
        {
            shell.WriteError("Вы должны находиться на карте!");
            return;
        }

        var mapUid = mapSystem.GetMap(mapId);

        var size = args.Length > 0 && float.TryParse(args[0], out var s) ? s : 500f;
        var shader = args.Length > 1 ? args[1] : "PlanetShader";

        var planetData = new ProceduralPlanetData
        {
            Size = size,
            ShaderName = shader,
            Seed = Random.Shared.Next(0, 10000)
        };

        var component = entManager.EnsureComponent<MapPlanetBackgroundComponent>(mapUid);
        component.Planets.Add(planetData);

        // Синхронизация с клиентом
        entManager.Dirty(mapUid, component);

        shell.WriteLine($"Планета добавлена на карту {mapId} (UID: {mapUid}, Размер: {size}, Шейдер: {shader})");
    }
}
