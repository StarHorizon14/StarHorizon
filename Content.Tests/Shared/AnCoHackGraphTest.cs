using System.Collections.Generic;
using System.Linq;
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

        // Ветки не пересекаются: все узлы и концы тупиков в разных точках сетки.
        [Test]
        public void TestLayoutHasNoOverlaps()
        {
            var random = new RobustRandom();
            var clean = 0;
            const int runs = 200;
            for (var i = 0; i < runs; i++)
            {
                var nodes = AnCoHackGraph.Generate(random, 6);
                var points = new HashSet<(int, int)> { (0, 0) };
                var pos = (X: 0, Y: AnCoHackGraph.FirstLeg);
                for (var y = 1; y <= AnCoHackGraph.FirstLeg; y++)
                    points.Add((0, y));

                var ok = true;
                foreach (var node in nodes)
                {
                    var step = AnCoHackGraph.Step(node.CorrectDir);
                    for (var s = 1; s <= node.LegLength; s++)
                        ok &= points.Add((pos.X + (int) step.X * s, pos.Y + (int) step.Y * s));

                    var dead = AnCoHackGraph.Step(node.DeadEndDir);
                    for (var s = 1; s <= node.DeadEndLength; s++)
                        ok &= points.Add((pos.X + (int) dead.X * s, pos.Y + (int) dead.Y * s));

                    pos = (pos.X + (int) step.X * node.LegLength, pos.Y + (int) step.Y * node.LegLength);
                }

                if (ok)
                    clean++;
            }

            // Генератор перебирает раскладки; без наложений должны быть практически все.
            Assert.That(clean, Is.EqualTo(runs));
        }
    }
}
