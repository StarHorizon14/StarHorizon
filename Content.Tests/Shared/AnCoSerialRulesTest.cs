using System.Collections.Generic;
using System.Linq;
using Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;
using NUnit.Framework;
using Robust.Shared.Random;

namespace Content.Tests.Shared
{
    [TestFixture]
    public sealed class AnCoSerialRulesTest
    {
        public static IEnumerable<AnCoSerialRule> Rules => AnCoSerialRules.All;

        [Test]
        public void TestRuleIdsUnique()
        {
            Assert.That(AnCoSerialRules.All.Select(r => r.Id).Distinct().Count(), Is.EqualTo(AnCoSerialRules.All.Count));
        }

        [Test]
        public void TestSerialFormat()
        {
            var random = new RobustRandom();
            for (var i = 0; i < 200; i++)
                Assert.That(AnCoSerialRules.GenerateSerial(random), Does.Match("^[A-Z]{4}-[0-9]{4}$"));
        }

        // Для каждого условия можно получить серийник, который ему подходит, и серийник, который не подходит.
        [Test]
        public void TestGenerateMatchingAndNonMatching([ValueSource(nameof(Rules))] AnCoSerialRule rule)
        {
            var random = new RobustRandom();
            for (var i = 0; i < 50; i++)
            {
                var yes = AnCoSerialRules.GenerateSerial(random, rule, true);
                var no = AnCoSerialRules.GenerateSerial(random, rule, false);

                Assert.That(rule.Predicate(yes), Is.True, rule.Id);
                Assert.That(rule.Predicate(no), Is.False, rule.Id);
            }
        }
    }
}
