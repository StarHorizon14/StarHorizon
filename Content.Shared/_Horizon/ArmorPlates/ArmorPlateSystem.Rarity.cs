using System.Numerics;
using Content.Shared._NF.Weapons.Rarity;
using Content.Shared.Examine;
using Robust.Shared.Random;

namespace Content.Shared._Horizon.ArmorPlates;

public sealed partial class ArmorPlateSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private void InitializeRarity()
    {
        SubscribeLocalEvent<RareArmorPlateComponent, MapInitEvent>(OnRareMapInit);
        SubscribeLocalEvent<RareArmorPlateComponent, ExaminedEvent>(OnRareExamined);
    }

    private void OnRareMapInit(Entity<RareArmorPlateComponent> ent, ref MapInitEvent args)
    {
        // Случайные улучшения выдаёт только сервер, клиент получит готовые значения.
        if (_net.IsClient || ent.Comp.Rolled || ent.Comp.Rarity == WeaponRarity.Common)
            return;

        if (!TryComp<ArmorPlateComponent>(ent, out var plate))
            return;

        // По одному улучшению за уровень редкости, каждое следующее сильнее предыдущего.
        var levels = (int) ent.Comp.Rarity;
        for (var level = 0; level < levels; level++)
        {
            var scale = MathF.Pow(ent.Comp.LevelScaling, level);
            switch (_random.Next(3))
            {
                case 0:
                    ent.Comp.ProtectionBonus += Roll(ent.Comp.ProtectionRoll) * scale;
                    break;
                case 1:
                    ent.Comp.DurabilityBonus += Roll(ent.Comp.DurabilityRoll) * scale;
                    break;
                default:
                    ent.Comp.SlowdownReduction += Roll(ent.Comp.SlowdownRoll) * scale;
                    break;
            }
        }

        ent.Comp.SlowdownReduction = MathF.Min(ent.Comp.SlowdownReduction, 1f);
        ent.Comp.Rolled = true;
        Dirty(ent);

        // Прочность и замедление меняем в самой пластине один раз, защита считается на лету
        // (её бонус складывается с износом, см. GetProtectionFactor).
        plate.MaxDurability *= 1f + ent.Comp.DurabilityBonus;
        plate.WalkSpeedModifier = ReduceSlowdown(plate.WalkSpeedModifier, ent.Comp.SlowdownReduction);
        plate.SprintSpeedModifier = ReduceSlowdown(plate.SprintSpeedModifier, ent.Comp.SlowdownReduction);
        Dirty(ent.Owner, plate);
    }

    private float Roll(Vector2 range)
    {
        return _random.NextFloat(MathF.Min(range.X, range.Y), MathF.Max(range.X, range.Y));
    }

    private static float ReduceSlowdown(float speedModifier, float reduction)
    {
        if (speedModifier >= 1f)
            return speedModifier;

        return 1f - (1f - speedModifier) * (1f - reduction);
    }

    private void OnRareExamined(Entity<RareArmorPlateComponent> ent, ref ExaminedEvent args)
    {
        var locId = ent.Comp.Rarity switch
        {
            WeaponRarity.Common => null,
            WeaponRarity.Uncommon => "armor-plate-rarity-uncommon",
            WeaponRarity.Rare => "armor-plate-rarity-rare",
            WeaponRarity.Epic => "armor-plate-rarity-epic",
            _ => "armor-plate-rarity-legendary",
        };

        if (locId == null)
            return;

        // Выше остальных строк пластины, как у редкого оружия.
        args.PushMarkup(Loc.GetString(locId), 10);

        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.DurabilityBonus > 0f)
        {
            args.PushMarkup(Loc.GetString("armor-plate-rarity-durability",
                ("value", MathF.Round(ent.Comp.DurabilityBonus * 100, 1))), 9);
        }

        if (ent.Comp.SlowdownReduction > 0f)
        {
            args.PushMarkup(Loc.GetString("armor-plate-rarity-slowdown",
                ("value", MathF.Round(ent.Comp.SlowdownReduction * 100, 1))), 9);
        }
    }
}
