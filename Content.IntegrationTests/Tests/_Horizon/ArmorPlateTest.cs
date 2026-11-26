using Content.Shared._Horizon.ArmorPlates;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Horizon;

[TestFixture]
public sealed class ArmorPlateTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ArmorPlateTestDummy
  components:
  - type: Inventory
  - type: ContainerContainer
  - type: Damageable
    damageContainer: Biological
  - type: MovementSpeedModifier

- type: entity
  id: ArmorPlateTestBelt
  components:
  - type: Item
    size: Ginormous
  - type: Clothing
    slots: [belt]
  - type: Storage
    maxItemSize: Normal
    grid:
    - 0,0,7,1
  - type: ContainerContainer
    containers:
      storagebase: !type:Container
  - type: UserInterface
    interfaces:
      enum.StorageUiKey.Key:
        type: StorageBoundUserInterface

- type: entity
  id: ArmorPlateTestBackpack
  components:
  - type: Item
    size: Ginormous
  - type: Clothing
    slots: [back]
  - type: Storage
    maxItemSize: Normal
    grid:
    - 0,0,7,1
  - type: ContainerContainer
    containers:
      storagebase: !type:Container
  - type: UserInterface
    interfaces:
      enum.StorageUiKey.Key:
        type: StorageBoundUserInterface

- type: entity
  id: ArmorPlateTestVest
  parent: ArmorPlateSlotBase
  components:
  - type: Item
    size: Huge
  - type: Clothing
    slots: [outerClothing]
";

    [Test]
    public async Task PlateAbsorbsAndBreaks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var inv = ent.System<InventorySystem>();
            var storage = ent.System<SharedStorageSystem>();
            var damageable = ent.System<DamageableSystem>();
            var piercing = server.ProtoMan.Index<DamageTypePrototype>("Piercing");

            var mob = ent.SpawnEntity("ArmorPlateTestDummy", map.GridCoords);
            var belt = ent.SpawnEntity("ArmorPlateTestBelt", map.GridCoords);
            var back = ent.SpawnEntity("ArmorPlateTestBackpack", map.GridCoords);
            var plate = ent.SpawnEntity("ArmorPlateMetal", map.GridCoords);
            var backPlate = ent.SpawnEntity("ArmorPlateJuggernaut", map.GridCoords);

            Assert.That(inv.TryEquip(mob, belt, "belt", force: true));
            Assert.That(inv.TryEquip(mob, back, "back", force: true));
            Assert.That(storage.Insert(belt, plate, out _, playSound: false));
            Assert.That(storage.Insert(back, backPlate, out _, playSound: false));

            var plateComp = ent.GetComponent<ArmorPlateComponent>(plate);
            var backComp = ent.GetComponent<ArmorPlateComponent>(backPlate);

            // 20 пробивного: металлическая пластина 0.75 => 15 носителю, 5 в пластину.
            var dealt = damageable.TryChangeDamage(mob, new DamageSpecifier(piercing, 20));
            Assert.That(dealt, Is.Not.Null);
            Assert.That(dealt!.GetTotal(), Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(plateComp.Damage, Is.EqualTo(5f).Within(0.01f));
            // Пластина в рюкзаке (слот спины) не работает.
            Assert.That(backComp.Damage, Is.EqualTo(0f));

            // Ломаем: огромный урон, пластина поглощает только оставшиеся 95 прочности.
            dealt = damageable.TryChangeDamage(mob, new DamageSpecifier(piercing, 1000));
            Assert.That(plateComp.Broken, Is.True);
            Assert.That(dealt!.GetTotal(), Is.EqualTo(FixedPoint2.New(905)));

            // Сломанная пластина больше не защищает.
            dealt = damageable.TryChangeDamage(mob, new DamageSpecifier(piercing, 20));
            Assert.That(dealt!.GetTotal(), Is.EqualTo(FixedPoint2.New(20)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlateWorksInItemSlot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var inv = ent.System<InventorySystem>();
            var itemSlots = ent.System<ItemSlotsSystem>();
            var damageable = ent.System<DamageableSystem>();
            var piercing = server.ProtoMan.Index<DamageTypePrototype>("Piercing");

            var mob = ent.SpawnEntity("ArmorPlateTestDummy", map.GridCoords);
            var vest = ent.SpawnEntity("ArmorPlateTestVest", map.GridCoords);
            var plate = ent.SpawnEntity("ArmorPlateMetal", map.GridCoords);

            Assert.That(itemSlots.TryInsert(vest, "armor_plate", plate, null));

            // Жилет не надет — пластина не работает.
            var dealt = damageable.TryChangeDamage(mob, new DamageSpecifier(piercing, 20));
            Assert.That(dealt!.GetTotal(), Is.EqualTo(FixedPoint2.New(20)));

            Assert.That(inv.TryEquip(mob, vest, "outerClothing", force: true));

            dealt = damageable.TryChangeDamage(mob, new DamageSpecifier(piercing, 20));
            Assert.That(dealt!.GetTotal(), Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(ent.GetComponent<ArmorPlateComponent>(plate).Damage, Is.EqualTo(5f).Within(0.01f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WornPlateProtectsLess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var inv = ent.System<InventorySystem>();
            var storage = ent.System<SharedStorageSystem>();
            var damageable = ent.System<DamageableSystem>();
            var piercing = server.ProtoMan.Index<DamageTypePrototype>("Piercing");

            var mob = ent.SpawnEntity("ArmorPlateTestDummy", map.GridCoords);
            var belt = ent.SpawnEntity("ArmorPlateTestBelt", map.GridCoords);
            var plate = ent.SpawnEntity("ArmorPlateMetal", map.GridCoords);

            Assert.That(inv.TryEquip(mob, belt, "belt", force: true));
            Assert.That(storage.Insert(belt, plate, out _, playSound: false));

            var plateComp = ent.GetComponent<ArmorPlateComponent>(plate);

            // Целая пластина защищает полностью: 300 * 0.25 = 75 в пластину, остаётся 25 из 100.
            var dealt = damageable.TryChangeDamage(mob, new DamageSpecifier(piercing, 300));
            Assert.That(dealt!.GetTotal(), Is.EqualTo(FixedPoint2.New(225)));
            Assert.That(plateComp.Damage, Is.EqualTo(75f).Within(0.01f));

            // 25% прочности: эффективность 0.6 + 0.4 * (0.25 / 0.5) = 0.8,
            // пробивной коэффициент 1 - 0.25 * 0.8 = 0.8 => из 20 урона проходит 16.
            Assert.That(ArmorPlateSystem.GetEfficiency(plateComp), Is.EqualTo(0.8f).Within(0.001f));
            dealt = damageable.TryChangeDamage(mob, new DamageSpecifier(piercing, 20));
            Assert.That(dealt!.GetTotal(), Is.EqualTo(FixedPoint2.New(16)));
            Assert.That(plateComp.Damage, Is.EqualTo(79f).Within(0.01f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RarePlateRollsUpgrades()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var plate = ent.SpawnEntity("ArmorPlateMetalLegendary", map.GridCoords);
            var plateComp = ent.GetComponent<ArmorPlateComponent>(plate);
            var rare = ent.GetComponent<RareArmorPlateComponent>(plate);

            // Легендарная: 4 улучшения, каждое > 0.
            Assert.That(rare.Rolled, Is.True);
            Assert.That(rare.ProtectionBonus + rare.DurabilityBonus + rare.SlowdownReduction, Is.GreaterThan(0.25f));
            Assert.That(plateComp.MaxDurability, Is.EqualTo(100f * (1f + rare.DurabilityBonus)).Within(0.01f));
            Assert.That(ArmorPlateSystem.GetProtectionFactor(plateComp, rare), Is.EqualTo(1f + rare.ProtectionBonus).Within(0.0001f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlateSlowsCarrierAnywhere()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        EntityUid mob = default;
        EntityUid back = default;
        EntityUid plate = default;

        await server.WaitAssertion(() =>
        {
            var inv = ent.System<InventorySystem>();
            var storage = ent.System<SharedStorageSystem>();

            mob = ent.SpawnEntity("ArmorPlateTestDummy", map.GridCoords);
            back = ent.SpawnEntity("ArmorPlateTestBackpack", map.GridCoords);
            plate = ent.SpawnEntity("ArmorPlateJuggernaut", map.GridCoords);

            var move = ent.GetComponent<MovementSpeedModifierComponent>(mob);
            Assert.That(move.WalkSpeedModifier, Is.EqualTo(1f).Within(0.001f));

            // Пластина в рюкзаке на полу никого не замедляет.
            Assert.That(storage.Insert(back, plate, out _, playSound: false));
            Assert.That(move.WalkSpeedModifier, Is.EqualTo(1f).Within(0.001f));

            // Надели рюкзак с пластиной — замедление появляется (максимум через интервал проверки).
            Assert.That(inv.TryEquip(mob, back, "back", force: true));
        });

        await pair.RunTicksSync(60);

        await server.WaitAssertion(() =>
        {
            var move = ent.GetComponent<MovementSpeedModifierComponent>(mob);
            var expected = ent.GetComponent<ArmorPlateComponent>(plate).WalkSpeedModifier;
            Assert.That(move.WalkSpeedModifier, Is.EqualTo(expected).Within(0.001f));

            // Сняли рюкзак — замедление пропадает.
            Assert.That(ent.System<InventorySystem>().TryUnequip(mob, "back", force: true));
        });

        await pair.RunTicksSync(60);

        await server.WaitAssertion(() =>
        {
            var move = ent.GetComponent<MovementSpeedModifierComponent>(mob);
            Assert.That(move.WalkSpeedModifier, Is.EqualTo(1f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }
}
