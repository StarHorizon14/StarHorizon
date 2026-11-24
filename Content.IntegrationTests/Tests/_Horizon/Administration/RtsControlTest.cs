using System.Collections.Generic;
using System.Numerics;
using Content.Server._Horizon.Administration.RtsControl;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Horizon.Administration;

[TestFixture]
[TestOf(typeof(RtsControlSystem))]
public sealed class RtsControlTest
{

    /// <summary>
    ///     Mice are ordered through a wall that only has a closed airlock in it. All of them should arrive.
    /// </summary>
    [TestCase(1, false)]
    [TestCase(8, false)]
    [TestCase(1, true)]
    [TestCase(8, true)]
    public async Task OrderThroughDoor(int Mobs, bool wall)
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, FailureLogLevel = LogLevel.Error });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var rts = server.System<RtsControlSystem>();
        var mice = new List<EntityUid>();
        EntityCoordinates target = default;

        await server.WaitPost(() =>
        {
            // Floor: x in [-5, 25), y in [-6, 6).
            for (var x = -5; x < 25; x++)
            {
                for (var y = -6; y < 6; y++)
                {
                    mapSys.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            // Wall column at x = 10 with an airlock at y = 0.
            for (var y = -6; y < 6; y++)
            {
                if (!wall)
                    continue;

                if (y == 0)
                    entMan.SpawnEntity("Airlock", new EntityCoordinates(map.Grid, 10.5f, 0.5f));
                else
                    entMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, 10.5f, y + 0.5f));
            }

            for (var i = 0; i < Mobs; i++)
            {
                mice.Add(entMan.SpawnEntity("MobMouse", new EntityCoordinates(map.Grid, 2f + i % 3, 0.5f + (i / 3) - 1)));
            }

            target = new EntityCoordinates(map.Grid, 18.5f, 0.5f);
        });

        // Let the pathfinding graph build.
        await pair.RunTicksSync(30);

        int accepted = 0;
        await server.WaitPost(() => accepted = rts.OrderMove(mice, target));
        Assert.That(accepted, Is.EqualTo(Mobs), "Not all mice accepted the order");

        // 30 seconds of game time. After arriving the mice resume their normal wandering,
        // so record the closest approach of each mouse instead of the final position.
        var best = new float[mice.Count];
        System.Array.Fill(best, float.MaxValue);
        for (var step = 0; step < 30; step++)
        {
            await pair.RunTicksSync(30);
            await server.WaitPost(() =>
            {
                var targetPos = xformSys.ToMapCoordinates(target).Position;
                for (var i = 0; i < mice.Count; i++)
                {
                    var d = Vector2.Distance(xformSys.GetWorldPosition(mice[i]), targetPos);
                    best[i] = MathF.Min(best[i], d);
                }
            });
        }

        var arrived = 0;
        for (var i = 0; i < mice.Count; i++)
        {
            TestContext.Out.WriteLine($"mouse {i}: closest approach {best[i]:F1}");
            if (best[i] < 4f)
                arrived++;
        }

        Assert.That(arrived, Is.EqualTo(Mobs), $"Only {arrived}/{Mobs} mice reached the target");

        await pair.CleanReturnAsync();
    }
}
