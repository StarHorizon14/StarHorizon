using Content.Server.Cargo.Systems;
using Content.Shared._Horizon.Durability;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Horizon;

[TestFixture]
public sealed class WeaponWearTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WeaponWearTestBlade
  name: test blade
  components:
  - type: StaticPrice
    price: 1000
  - type: WeaponWear
    maxDurability: 100
    malfunctionFrom: 0.5
    malfunctionChance: 0.1
    malfunctionCurve: 2
";

    [Test]
    public async Task WearBreaksAndRepairs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var wearSys = ent.System<WeaponWearSystem>();
            var uid = ent.SpawnEntity("WeaponWearTestBlade", map.GridCoords);
            var comp = ent.GetComponent<WeaponWearComponent>(uid);
            var weapon = new Entity<WeaponWearComponent>(uid, comp);
            var pricing = ent.System<PricingSystem>();
            var baseName = ent.GetComponent<MetaDataComponent>(uid).EntityName;

            Assert.That(pricing.GetPrice(uid), Is.EqualTo(1000).Within(0.01));

            // До половины износа осечек нет.
            wearSys.AddWear(weapon, 40);
            Assert.That(wearSys.GetMalfunctionChance(comp), Is.EqualTo(0f));

            // 75% износа: прогресс 0.5, кривая 2 => 0.1 * 0.25.
            wearSys.AddWear(weapon, 35);
            Assert.That(wearSys.GetMalfunctionChance(comp), Is.EqualTo(0.025f).Within(0.0001f));

            // Характеристики: прогресс штрафа 0.5 => разброс +25%, темп -10%, скорость -5%.
            var mods = WeaponWearSystem.GetCombinedModifiers(null, comp);
            Assert.That(mods.Accuracy, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(mods.FireRate, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(mods.ProjectileSpeed, Is.EqualTo(0.95f).Within(0.0001f));

            // Цена: 0.2 + 0.8 * 0.25 = 0.4 от полной.
            Assert.That(pricing.GetPrice(uid), Is.EqualTo(400).Within(0.01));

            // Износ не уходит выше максимума, оружие ломается.
            wearSys.AddWear(weapon, 1000);
            Assert.That(comp.Broken, Is.True);
            Assert.That(comp.Durability, Is.EqualTo(0f));
            Assert.That(pricing.GetPrice(uid), Is.EqualTo(200).Within(0.01));
            Assert.That(ent.GetComponent<MetaDataComponent>(uid).EntityName, Is.Not.EqualTo(baseName));

            // Ремонт возвращает оружие в строй.
            wearSys.Repair(weapon, 60);
            Assert.That(comp.Broken, Is.False);
            Assert.That(comp.Durability, Is.EqualTo(60f).Within(0.01f));
            Assert.That(ent.GetComponent<MetaDataComponent>(uid).EntityName, Is.EqualTo(baseName));
        });

        await pair.CleanReturnAsync();
    }
}
