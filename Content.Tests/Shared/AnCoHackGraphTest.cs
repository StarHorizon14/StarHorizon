using System.Collections.Generic;
using System;
using System.Linq;
using System.Numerics;
using Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;
using NUnit.Framework;
using Robust.Shared.Random;

namespace Content.Tests.Shared
{
    [TestFixture]
    public sealed class AnCoHackGraphTest
    {
        [Test]
        public void TestGeneratedNodes()
        {
            var random = new RobustRandom();
            for (var i = 0; i < 100; i++)
            {
                var nodes = AnCoHackGraph.Generate(random, 6);
                Assert.That(nodes, Has.Count.EqualTo(6));
                Assert.That(nodes.Select(n => n.RuleId).Distinct().Count(), Is.EqualTo(6));

                foreach (var node in nodes)
                {
                    // Два разных выхода, левый по порядку раньше правого.
                    Assert.That(node.LeftExit, Is.LessThan(node.RightExit));
                    Assert.That(node.LegLength, Is.InRange(AnCoHackGraph.MinLeg, AnCoHackGraph.MaxLeg));
                    Assert.That(node.DeadEndLength, Is.InRange(AnCoHackGraph.MinDeadEnd, AnCoHackGraph.MaxDeadEnd));
                    Assert.That(node.CorrectDir, Is.Not.EqualTo(node.DeadEndDir));

                    // Узлы стоят по диагонали или строго выше; линии - только горизонтали и вертикали.
                    foreach (var dir in new[] { node.LeftExit, node.RightExit })
                    {
                        var step = AnCoHackGraph.Step(dir);
                        Assert.That(step.Y, Is.EqualTo(1f));
                        Assert.That(Math.Abs(step.X), Is.LessThanOrEqualTo(1f));

                        var path = AnCoHackGraph.PathPoints(Vector2.Zero, dir, 2);
                        for (var p = 1; p < path.Count; p++)
                            Assert.That(path[p].X == path[p - 1].X || path[p].Y == path[p - 1].Y, Is.True);
                    }
                }
            }
        }

        // Ровно один из серийников подходит условию узла, и он соответствует CorrectIsB.
        [Test]
        public void TestExactlyOneSerialMatches()
        {
            var random = new RobustRandom();
            for (var i = 0; i < 100; i++)
            {
                foreach (var node in AnCoHackGraph.Generate(random, 6))
                {
                    var rule = AnCoSerialRules.All.Single(r => r.LocId == node.RuleId);
                    var b = rule.Predicate(node.SerialB);
                    var c = rule.Predicate(node.SerialC);
                    Assert.That(b, Is.Not.EqualTo(c));
                    Assert.That(b, Is.EqualTo(node.CorrectIsB));
                }
            }
        }

        // Ветки не пересекаются: все вершины и единичные отрезки ломаных различны.
        [Test]
        public void TestLayoutHasNoOverlaps()
        {
            var random = new RobustRandom();
            for (var run = 0; run < 200; run++)
            {
                var nodes = AnCoHackGraph.Generate(random, 6);
                var points = new HashSet<(float, float)> { (0f, 0f) };
                var segments = new HashSet<((float, float), (float, float))>();

                bool Add(ref Vector2 pos, AnCoHackDir dir, int length)
                {
                    var path = AnCoHackGraph.PathPoints(pos, dir, length);
                    var ok = true;
                    for (var i = 1; i < path.Count; i++)
                    {
                        var a = (path[i - 1].X, path[i - 1].Y);
                        var b = (path[i].X, path[i].Y);
                        ok &= segments.Add(a.CompareTo(b) <= 0 ? (a, b) : (b, a));
                        ok &= points.Add(b);
                    }

                    pos = path[^1];
                    return ok;
                }

                var cursor = Vector2.Zero;
                Assert.That(Add(ref cursor, AnCoHackDir.Up, AnCoHackGraph.FirstLeg), Is.True);

                var positions = new List<Vector2>();
                foreach (var node in nodes)
                {
                    positions.Add(cursor);
                    Assert.That(Add(ref cursor, node.CorrectDir, node.LegLength), Is.True);
                }

                for (var i = 0; i < nodes.Count; i++)
                {
                    var pos = positions[i];
                    Assert.That(Add(ref pos, nodes[i].DeadEndDir, nodes[i].DeadEndLength), Is.True);
                }
            }
        }
    }
}
