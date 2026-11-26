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
    [TestCase(50, false)]
    [TestCase(50, true)]
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
                mice.Add(entMan.SpawnEntity("MobMouse", new EntityCoordinates(map.Grid, -4f + i % 8, -5f + i / 8)));
            }

            target = new EntityCoordinates(map.Grid, 18.5f, 0.5f);
        });

        // Let the pathfinding graph build.
        await pair.RunTicksSync(30);

        Content.Server._Horizon.Administration.RtsControl.RtsOrderResult order = default;
        await server.WaitPost(() => order = rts.OrderMove(mice, target));
        Assert.That(order.Accepted, Is.EqualTo(Mobs), "Not all mice accepted the order");

        // 60 seconds of game time. After arriving the mice resume their normal wandering,
        // so record the closest approach of each mouse instead of the final position.
        var best = new float[mice.Count];
        System.Array.Fill(best, float.MaxValue);
        for (var step = 0; step < 60; step++)
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
            if (best[i] < 6f)
                arrived++;
        }

        Assert.That(arrived, Is.EqualTo(Mobs), $"Only {arrived}/{Mobs} mice reached the target");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Hostile creatures are ordered to attack a far away target. They should walk to it and start fighting it.
    ///     The target is invulnerable so it cannot die early (the test map has no air) and the order stays active.
    /// </summary>
    [TestCase(3, false)]
    [TestCase(3, true)]
    [TestCase(10, true)]
    [TestCase(3, false, false)]
    public async Task OrderAttack(int attackers, bool wall, bool giveOrder = true)
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, FailureLogLevel = LogLevel.Error });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var rts = server.System<RtsControlSystem>();
        var units = new List<EntityUid>();
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            for (var x = -5; x < 30; x++)
            {
                for (var y = -6; y < 6; y++)
                {
                    mapSys.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            if (wall)
            {
                for (var y = -6; y < 6; y++)
                {
                    // A wall with a one tile gap at y = 0 (no door: big mobs can't open airlocks on their own).
                    if (y != 0)
                        entMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, 10.5f, y + 0.5f));
                }
            }

            for (var i = 0; i < attackers; i++)
            {
                units.Add(entMan.SpawnEntity(wall ? "MobKobold" : "MobGorilla", new EntityCoordinates(map.Grid, -4f + i % 4, -3f + i / 4)));
            }

            target = entMan.SpawnEntity("MobMouse", new EntityCoordinates(map.Grid, 25.5f, 0.5f));
            entMan.AddComponent<Content.Shared.Damage.Components.GodmodeComponent>(target);

            // A mouse that stands still, otherwise it wanders off the floor into space.
            server.System<Content.Server.NPC.HTN.HTNSystem>().SetHTNEnabled((target, entMan.GetComponent<Content.Server.NPC.HTN.HTNComponent>(target)), false);
        });

        await pair.RunTicksSync(30);

        if (giveOrder)
        {
            Content.Server._Horizon.Administration.RtsControl.RtsOrderResult order = default;
            await server.WaitPost(() => order = rts.OrderAttack(units, target));
            Assert.That(order.Accepted, Is.EqualTo(attackers), "Not all attackers accepted the order");
        }

        // Closest approach of every attacker to the target over 60 seconds.
        var best = new float[units.Count];
        System.Array.Fill(best, float.MaxValue);
        for (var step = 0; step < 60; step++)
        {
            await pair.RunTicksSync(30);
            await server.WaitPost(() =>
            {
                var targetPos = xformSys.GetWorldPosition(target);
                for (var i = 0; i < units.Count; i++)
                {
                    best[i] = MathF.Min(best[i], Vector2.Distance(xformSys.GetWorldPosition(units[i]), targetPos));
                }
            });
        }

        var arrived = 0;
        for (var i = 0; i < units.Count; i++)
        {
            TestContext.Out.WriteLine($"attacker {i}: closest approach {best[i]:F1}");
            if (best[i] < 3f)
                arrived++;
        }

        if (!giveOrder)
        {
            // Control case: without the order the far away target is left alone, so the other cases prove something.
            Assert.That(arrived, Is.EqualTo(0), "Attackers reached the target without any order");
        }
        else
        {
            Assert.That(arrived, Is.EqualTo(attackers), $"Only {arrived}/{attackers} attackers reached the target");
        }

        await pair.CleanReturnAsync();
    }
}
