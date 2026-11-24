using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._Horizon.Shuttles.Components;
using Content.Server._Horizon.Shuttles.Systems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Horizon.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Shuttle;

/// <summary>
/// Horizon: landing pads. A shuttle can fly over the landing pad tiles of another grid,
/// land there, ride along with that grid and take off again.
/// </summary>
[TestFixture]
public sealed class LandingTest
{
    private const string PadTile = "FloorLandingPad";
    private const string FloorTile = "Plating";

    /// <summary>
    /// Half size of the host grid and of the pad in its middle, in tiles.
    /// </summary>
    private const int HostHalfSize = 12;
    private const int PadHalfSize = 6;

    private static readonly Vector2 HostPosition = new(200f, 200f);

    /// <summary>
    /// A shuttle over the pad does not touch the host, a shuttle over its normal tiles does.
    /// </summary>
    [Test]
    public async Task PadDoesNotCollide()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var (host, shuttle) = await Setup(pair, map.MapId);
        var physics = server.System<SharedPhysicsSystem>();
        var xformSys = server.System<SharedTransformSystem>();

        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(physics.GetContactingEntities(shuttle), Does.Not.Contain(host), "Shuttle collides with the pad under it");
            Assert.That((xformSys.GetWorldPosition(shuttle) - PadCenter()).Length(), Is.LessThan(0.05f), "Shuttle was pushed off the pad");
        });

        // Now over the host's normal floor.
        await server.WaitPost(() =>
        {
            xformSys.SetWorldPosition(shuttle, HostPosition + new Vector2(PadHalfSize + 3.3f, 0.3f));
            physics.WakeBody(shuttle);
        });

        var touched = false;
        for (var i = 0; i < 30 && !touched; i++)
        {
            await server.WaitRunTicks(1);
            await server.WaitPost(() => touched = physics.GetContactingEntities(shuttle).Contains(host));
        }

        Assert.That(touched, "Shuttle does not collide with the host's normal tiles");
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Whoever stands on the shuttle's floor over the pad is on the shuttle, whoever stands on the bare pad is on the host.
    /// </summary>
    [Test]
    public async Task ShuttleFloorWinsOverPad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var (host, shuttle) = await Setup(pair, map.MapId);

        await server.WaitAssertion(() =>
        {
            var onShuttle = entMan.SpawnEntity(null, new MapCoordinates(PadCenter(), map.MapId));
            var onPad = entMan.SpawnEntity(null, new MapCoordinates(HostPosition + new Vector2(PadHalfSize - 0.5f, 0.5f), map.MapId));
            var onFloor = entMan.SpawnEntity(null, new MapCoordinates(HostPosition + new Vector2(HostHalfSize - 0.5f, 0.5f), map.MapId));

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<TransformComponent>(onShuttle).GridUid, Is.EqualTo(shuttle));
                Assert.That(entMan.GetComponent<TransformComponent>(onPad).GridUid, Is.EqualTo(host));
                Assert.That(entMan.GetComponent<TransformComponent>(onFloor).GridUid, Is.EqualTo(host));
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Landing welds the shuttle to the host and blocks FTL, the shuttle then follows the host, take off undoes it.
    /// </summary>
    [Test]
    public async Task LandRideAndTakeOff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var (host, shuttle) = await Setup(pair, map.MapId);
        var physics = server.System<SharedPhysicsSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var landing = server.System<ShuttleLandingSystem>();
        var shuttleSys = server.System<ShuttleSystem>();
        EntityUid console = default;

        await server.WaitAssertion(() =>
        {
            console = entMan.SpawnEntity("ComputerShuttle", new EntityCoordinates(shuttle, new Vector2(0.5f, 0.5f)));
            entMan.GetComponent<ApcPowerReceiverComponent>(console).NeedsPower = false;

            Assert.That(shuttleSys.CanFTL(shuttle, out _), "Test shuttle cannot FTL even before landing");
            Assert.That(landing.IsLanded(shuttle), Is.False);
        });

        // Land through the console, the way a pilot does.
        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(console, new ShuttleConsoleLandingRequest { Land = true });

            Assert.That(landing.IsLanded(shuttle), "Shuttle did not land on the pad");
            Assert.That(entMan.GetComponent<JointComponent>(shuttle).JointCount, Is.EqualTo(1));
            Assert.That(shuttleSys.CanFTL(shuttle, out _), Is.False, "A landed shuttle can still FTL");
        });

        var offset = await GetOffset(pair, host, shuttle);

        // The host flies away, the shuttle rides along.
        for (var i = 0; i < 20; i++)
        {
            await server.WaitPost(() => physics.SetLinearVelocity(host, new Vector2(4f, 2f)));
            await server.WaitRunTicks(5);
        }

        await server.WaitAssertion(() =>
        {
            Assert.That((xformSys.GetWorldPosition(host) - HostPosition).Length(), Is.GreaterThan(5f), "Host did not move, nothing was tested");
            physics.SetLinearVelocity(host, Vector2.Zero);
        });

        await server.WaitRunTicks(60);
        var movedOffset = await GetOffset(pair, host, shuttle);
        Assert.That((movedOffset - offset).Length(), Is.LessThan(0.3f), "Shuttle did not stay in place on the moving host");

        // Take off: the engines spin up first, like for FTL.
        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(console, new ShuttleConsoleLandingRequest { Land = false });

            Assert.That(landing.IsLanded(shuttle), "Shuttle left the pad before its engines spun up");
            Assert.That(shuttleSys.CanFTL(shuttle, out _), Is.False, "Shuttle can FTL while taking off");
        });

        await server.WaitRunTicks((int) ((shuttleSys.DefaultStartupTime + 1f) * 30));

        await server.WaitAssertion(() =>
        {
            Assert.That(landing.IsLanded(shuttle), Is.False);
            Assert.That(!entMan.TryGetComponent<JointComponent>(shuttle, out var joints) || joints.JointCount == 0, "The weld was left behind");
            Assert.That(shuttleSys.CanFTL(shuttle, out _), "Shuttle cannot FTL after take off");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A shuttle with no pad under it cannot land.
    /// </summary>
    [Test]
    public async Task CannotLandWithoutPad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var (host, shuttle) = await Setup(pair, map.MapId);
        var xformSys = server.System<SharedTransformSystem>();
        var landing = server.System<ShuttleLandingSystem>();

        await server.WaitAssertion(() =>
        {
            // Open space.
            xformSys.SetWorldPosition(shuttle, HostPosition + new Vector2(100f, 0f));
            Assert.That(landing.TryLand(shuttle, out var reason), Is.False);
            Assert.That(reason, Is.Not.Null);

            // Only a corner of the shuttle over the pad.
            xformSys.SetWorldPosition(shuttle, HostPosition + new Vector2(-HostHalfSize - 1.5f, 0f));
            Assert.That(landing.TryLand(shuttle, out _), Is.False);
            Assert.That(landing.IsLanded(shuttle), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The host jumps through FTL and the landed shuttle arrives with it, in the same place on the pad.
    /// </summary>
    [Test]
    public async Task LandedShuttleFollowsHostThroughFTL()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var (host, shuttle) = await Setup(pair, map.MapId);
        var xformSys = server.System<SharedTransformSystem>();
        var landing = server.System<ShuttleLandingSystem>();
        var shuttleSys = server.System<ShuttleSystem>();
        var target = new Vector2(900f, 900f);

        await server.WaitAssertion(() =>
        {
            Assert.That(landing.TryLand(shuttle, out var reason), $"Shuttle did not land: {reason}");
        });

        var offset = await GetOffset(pair, host, shuttle);

        await server.WaitPost(() =>
        {
            shuttleSys.FTLToCoordinates(
                host,
                entMan.GetComponent<ShuttleComponent>(host),
                new EntityCoordinates(map.MapUid, target),
                Angle.FromDegrees(90),
                startupTime: 1f,
                hyperspaceTime: 8f);
        });

        // In hyperspace the shuttle must be on the FTL map together with the host.
        var sawHyperspace = false;
        for (var i = 0; i < 40 * 30 / 5; i++)
        {
            await server.WaitRunTicks(5);

            var done = false;
            await server.WaitAssertion(() =>
            {
                if (!entMan.TryGetComponent<FTLComponent>(host, out var ftl))
                {
                    done = true;
                    return;
                }

                if (ftl.State != FTLState.Travelling)
                    return;

                sawHyperspace = true;
                Assert.That(entMan.GetComponent<TransformComponent>(shuttle).MapUid,
                    Is.EqualTo(entMan.GetComponent<TransformComponent>(host).MapUid),
                    "Shuttle was left behind when the host entered FTL");
            });

            if (done)
                break;
        }

        await server.WaitRunTicks(30);
        var arrivedOffset = await GetOffset(pair, host, shuttle);

        await server.WaitAssertion(() =>
        {
            Assert.That(sawHyperspace, "Host never reached hyperspace, nothing was tested");
            Assert.That(entMan.HasComponent<FTLComponent>(host), Is.False, "Host did not finish FTL in time");
            Assert.That((xformSys.GetWorldPosition(host) - target).Length(), Is.LessThan(1f), "Host did not arrive at the target");
            Assert.That(entMan.GetComponent<TransformComponent>(shuttle).MapUid, Is.EqualTo(map.MapUid));
            Assert.That(landing.IsLanded(shuttle), "Shuttle is no longer landed after the jump");
            Assert.That(entMan.GetComponent<JointComponent>(shuttle).JointCount, Is.EqualTo(1));
            Assert.That((arrivedOffset - offset).Length(), Is.LessThan(0.3f), "Shuttle is not where it landed on the host");
        });

        await pair.CleanReturnAsync();
    }

    private static Vector2 PadCenter()
    {
        // A little off the tile lines, shuttles do not land aligned to the host's tiles.
        return HostPosition + new Vector2(0.3f, 0.3f);
    }

    /// <summary>
    /// Position of the shuttle in the host's local space.
    /// </summary>
    private static async Task<Vector2> GetOffset(TestPair pair, EntityUid host, EntityUid shuttle)
    {
        var xformSys = pair.Server.System<SharedTransformSystem>();
        var offset = Vector2.Zero;

        await pair.Server.WaitPost(() =>
        {
            offset = Vector2.Transform(xformSys.GetWorldPosition(shuttle), xformSys.GetInvWorldMatrix(host));
        });

        return offset;
    }

    /// <summary>
    /// Makes a host grid with a pad in the middle and a small shuttle hovering over the middle of the pad.
    /// </summary>
    private static async Task<(EntityUid Host, EntityUid Shuttle)> Setup(TestPair pair, MapId mapId)
    {
        var server = pair.Server;
        EntityUid host = default;
        EntityUid shuttle = default;

        await server.WaitPost(() =>
        {
            var mapSys = server.System<SharedMapSystem>();
            var xformSys = server.System<SharedTransformSystem>();
            var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
            var pad = new Tile(tileDefs[PadTile].TileId);
            var floor = new Tile(tileDefs[FloorTile].TileId);

            var hostGrid = server.MapMan.CreateGridEntity(mapId);
            host = hostGrid.Owner;
            var hostTiles = new List<(Vector2i, Tile)>();

            for (var x = -HostHalfSize; x < HostHalfSize; x++)
            {
                for (var y = -HostHalfSize; y < HostHalfSize; y++)
                {
                    var isPad = x >= -PadHalfSize && x < PadHalfSize && y >= -PadHalfSize && y < PadHalfSize;
                    hostTiles.Add((new Vector2i(x, y), isPad ? pad : floor));
                }
            }

            mapSys.SetTiles(host, hostGrid.Comp, hostTiles);
            xformSys.SetWorldPosition(host, HostPosition);

            var shuttleGrid = server.MapMan.CreateGridEntity(mapId);
            shuttle = shuttleGrid.Owner;
            var shuttleTiles = new List<(Vector2i, Tile)>();

            for (var x = -2; x < 2; x++)
            {
                for (var y = -2; y < 2; y++)
                {
                    shuttleTiles.Add((new Vector2i(x, y), floor));
                }
            }

            mapSys.SetTiles(shuttle, shuttleGrid.Comp, shuttleTiles);
            xformSys.SetWorldPosition(shuttle, PadCenter());
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<PhysicsComponent>(host).BodyType, Is.EqualTo(BodyType.Dynamic));
            Assert.That(entMan.GetComponent<PhysicsComponent>(shuttle).BodyType, Is.EqualTo(BodyType.Dynamic));
        });

        return (host, shuttle);
    }
}
