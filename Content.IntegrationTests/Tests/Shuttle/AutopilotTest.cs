using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server.NPC.HTN;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Shuttles;
using Content.Shared.Maps;
using Content.Shared.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Shuttle;

/// <summary>
/// Mono: checks that the shuttle console autopilot flies its shuttle to the requested point without ramming anything.
/// </summary>
[TestFixture]
public sealed class AutopilotTest
{
    private const int MaxSeconds = 120;

    [Test]
    public async Task AutopilotFliesToTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var (map, console) = await SetupShuttle(pair);
        var target = new Vector2(250f, 0f);

        await StartAutopilot(pair, console, map.MapId, target);
        var result = await Fly(pair, map.Grid.Owner, console, null, stopOnArrival: true);

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var pos = entMan.System<SharedTransformSystem>().GetWorldPosition(map.Grid.Owner);
            Assert.That(result.Arrived, "Autopilot did not finish in time");
            Assert.That((pos - target).Length(), Is.LessThan(150f), $"Shuttle stopped at {pos}, target {target}");
            Assert.That(entMan.HasComponent<ActiveNPCComponent>(console), Is.False, "Console NPC did not go back to sleep");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AutopilotAvoidsObstacle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var (map, console) = await SetupShuttle(pair);
        var obstacle = await SpawnObstacle(pair, map.MapId, new Vector2(150f, 0f), 10, 20);
        var target = new Vector2(300f, 0f);

        await StartAutopilot(pair, console, map.MapId, target);
        var result = await Fly(pair, map.Grid.Owner, console, obstacle, stopOnArrival: true);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(result.Collided, Is.False, "Autopilot flew into the obstacle");
            Assert.That(result.Arrived, "Autopilot did not finish in time");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AutopilotDoesNotRamStationNearDestination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var (map, console) = await SetupShuttle(pair);
        // The destination is just behind a "station" sitting right in the flight path.
        var obstacle = await SpawnObstacle(pair, map.MapId, new Vector2(250f, 0f), 15, 15);

        await StartAutopilot(pair, console, map.MapId, new Vector2(330f, 0f));
        var result = await Fly(pair, map.Grid.Owner, console, obstacle, stopOnArrival: true);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(result.Collided, Is.False, "Autopilot rammed the station near its destination");
            Assert.That(result.Arrived, "Autopilot did not finish in time");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AutopilotStopsWhenDestinationUnreachable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var (map, console) = await SetupShuttle(pair);
        // The destination is in the middle of a big "station", further than the arrival range from any
        // point the autopilot can reach without ramming it, so it has to give up.
        var obstacle = await SpawnObstacle(pair, map.MapId, new Vector2(250f, 0f), 90, 90);

        await StartAutopilot(pair, console, map.MapId, new Vector2(250f, 0f));
        var result = await Fly(pair, map.Grid.Owner, console, obstacle, stopOnArrival: true);

        await pair.Server.WaitAssertion(() =>
        {
            var body = pair.Server.EntMan.GetComponent<PhysicsComponent>(map.Grid);
            Assert.That(result.Collided, Is.False, "Autopilot rammed the station");
            Assert.That(result.Arrived, "Autopilot did not give up on the blocked destination");
            var pos = pair.Server.EntMan.System<SharedTransformSystem>().GetWorldPosition(map.Grid.Owner);
            Assert.That((pos - new Vector2(250f, 0f)).Length(), Is.GreaterThan(120f), "Autopilot reached the destination, so the blocked path wasn't tested");
            Assert.That(body.LinearVelocity.Length(), Is.LessThan(0.5f), "Autopilot did not stop the shuttle");
            Assert.That(pair.Server.EntMan.HasComponent<ActiveNPCComponent>(console), Is.False, "Console NPC did not go back to sleep");
        });

        await pair.CleanReturnAsync();
    }

    private static async Task<(TestMapData Map, EntityUid Console)> SetupShuttle(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid console = default;

        await server.WaitAssertion(() =>
        {
            console = entMan.SpawnEntity("ComputerShuttle", map.GridCoords);
            entMan.GetComponent<ApcPowerReceiverComponent>(console).NeedsPower = false;

            // Idle consoles must not be active NPCs.
            Assert.That(entMan.HasComponent<ActiveNPCComponent>(console), Is.False);

            // Fake some thrusters so the grid can move.
            var shuttle = entMan.GetComponent<ShuttleComponent>(map.Grid);
            var body = entMan.GetComponent<PhysicsComponent>(map.Grid);
            for (var i = 0; i < 4; i++)
            {
                shuttle.LinearThrust[i] = body.Mass * 5f;
                shuttle.BaseLinearThrust[i] = body.Mass * 5f;
            }
            shuttle.AngularThrust = body.Inertia * 5f;
        });

        return (map, console);
    }

    private static async Task<EntityUid> SpawnObstacle(TestPair pair, MapId mapId, Vector2 center, int halfWidth, int halfHeight)
    {
        var server = pair.Server;
        EntityUid obstacle = default;

        await server.WaitPost(() =>
        {
            var mapSys = server.System<SharedMapSystem>();
            var tileDef = server.ResolveDependency<ITileDefinitionManager>()["Plating"];
            var grid = server.MapMan.CreateGridEntity(mapId);
            obstacle = grid.Owner;

            var tiles = new List<(Vector2i, Tile)>();
            for (var x = -halfWidth; x <= halfWidth; x++)
            {
                for (var y = -halfHeight; y <= halfHeight; y++)
                {
                    tiles.Add((new Vector2i(x, y), new Tile(tileDef.TileId)));
                }
            }

            mapSys.SetTiles(grid.Owner, grid.Comp, tiles);
            server.System<SharedTransformSystem>().SetWorldPosition(grid.Owner, center);
        });

        return obstacle;
    }

    private static async Task StartAutopilot(TestPair pair, EntityUid console, MapId mapId, Vector2 target)
    {
        var entMan = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var msg = new ShuttleConsoleAutopilotPositionMessage
            {
                Coordinates = new MapCoordinates(target, mapId),
                Angle = Angle.Zero,
            };
            entMan.EventBus.RaiseLocalEvent(console, msg);

            Assert.That(entMan.HasComponent<ActiveNPCComponent>(console));
        });
    }

    /// <summary>
    /// Runs the simulation, tracking whether the shuttle ever touched the obstacle.
    /// </summary>
    private static async Task<(bool Arrived, bool Collided)> Fly(
        TestPair pair,
        EntityUid shuttle,
        EntityUid console,
        EntityUid? obstacle,
        bool stopOnArrival)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var physics = entMan.System<SharedPhysicsSystem>();
        var arrived = false;
        var collided = false;

        // 30 ticks per second, sampled every 3 ticks
        for (var i = 0; i < MaxSeconds * 10; i++)
        {
            await server.WaitRunTicks(3);
            await server.WaitPost(() =>
            {
                arrived = !entMan.GetComponent<HTNComponent>(console).Blackboard.ContainsKey("Target");

                if (obstacle is not { } obs)
                    return;

                var shipBox = physics.GetWorldAABB(shuttle);
                var obsBox = physics.GetWorldAABB(obs);
                if (shipBox.Intersects(obsBox.Enlarged(0.5f)))
                    collided = true;
            });

            if (collided || arrived && stopOnArrival)
                break;
        }

        // let the sleep check run
        await server.WaitRunTicks(60);
        return (arrived, collided);
    }
}
