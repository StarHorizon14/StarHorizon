using Content.Shared._Horizon.Durability;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Horizon.ArmorPlates;

/// <summary>
/// Логика бронепластин.
/// Урон, приходящий носителю, ретранслируется на надетую одежду; если одежда надета
/// в комбинезон, РПС (пояс) или верхнюю одежду, то лежащие в её контейнерах пластины
/// снижают урон по своим резистам и принимают поглощённую часть на себя.
/// </summary>
public sealed partial class ArmorPlateSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;

    /// <summary>
    /// Слоты, в которых одежда с пластинами даёт защиту: комбинезон, РПС/пояс, верхняя одежда.
    /// </summary>
    private readonly List<Entity<ArmorPlateComponent>> _plates = new();
    private readonly List<Entity<ArmorPlateComponent>> _activeCheck = new();

    public const SlotFlags AllowedSlots = SlotFlags.INNERCLOTHING | SlotFlags.BELT | SlotFlags.OUTERCLOTHING;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnClothingDamageModify);
        SubscribeLocalEvent<ArmorPlateComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ArmorPlateComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ArmorPlateComponent, RefreshNameModifiersEvent>(OnRefreshName);
        SubscribeLocalEvent<ArmorPlateComponent, PriceCalculationEvent>(OnPriceCalculation);

        InitializeWeight();
        InitializeRarity();
    }

    private void OnMapInit(Entity<ArmorPlateComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.Broken && ent.Comp.Damage >= ent.Comp.MaxDurability)
            ent.Comp.Broken = true;

        _appearance.SetData(ent, ArmorPlateVisuals.Broken, ent.Comp.Broken);

        if (ent.Comp.Broken)
            _nameModifier.RefreshNameModifiers(ent.Owner);
    }

    private void OnRefreshName(Entity<ArmorPlateComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (ent.Comp.Broken)
            args.AddModifier("armor-plate-broken-name");
    }

    private void OnPriceCalculation(Entity<ArmorPlateComponent> ent, ref PriceCalculationEvent args)
    {
        if (!TryComp<StaticPriceComponent>(ent, out var price))
            return;

        var fraction = DurabilityFormat.Fraction(ent.Comp.Durability, ent.Comp.MaxDurability);
        DurabilityFormat.ApplyWearToPrice(ref args, price.Price, fraction, ent.Comp.BrokenPriceFraction);
    }

    private void OnClothingDamageModify(EntityUid uid, ClothingComponent clothing, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (args.Args.Damage.GetTotal() <= 0)
            return;

        if (!IsWornInAllowedSlot(uid))
            return;

        _plates.Clear();
        GetActivePlates(uid, _plates);

        foreach (var plate in _plates)
        {
            args.Args.Damage = AbsorbDamage(plate, args.Owner, args.Args.Damage, args.Args.ArmorPenetration);

            if (args.Args.Damage.GetTotal() <= 0)
                return;
        }
    }

    /// <summary>
    /// Надета ли одежда в слот, в котором пластины работают (комбинезон, РПС/пояс, верхняя одежда).
    /// </summary>
    public bool IsWornInAllowedSlot(EntityUid clothing)
    {
        return _inventory.TryGetContainingSlot(clothing, out var slot) && (slot.SlotFlags & AllowedSlots) != 0;
    }

    /// <summary>
    /// Собирает пластины вещи, которые будут защищать (без проверки того, надета ли вещь).
    /// Сначала пластины в слотах предметов (ItemSlots), затем в остальных контейнерах (хранилище и т.д.).
    /// Сломанные пластины пропускаются, лимит — <see cref="ArmorPlateHolderComponent.MaxActivePlates"/> (по умолчанию 1).
    /// </summary>
    public void GetActivePlates(EntityUid clothing, List<Entity<ArmorPlateComponent>> result)
    {
        if (!TryComp<ContainerManagerComponent>(clothing, out var manager))
            return;

        TryComp<ArmorPlateHolderComponent>(clothing, out var holder);
        var maxPlates = holder?.MaxActivePlates ?? 1;
        if (maxPlates <= 0)
            return;

        var containers = new List<BaseContainer>();
        foreach (var container in _container.GetAllContainers(clothing, manager))
        {
            if (holder?.Containers != null && !holder.Containers.Contains(container.ID))
                continue;

            containers.Add(container);
        }

        if (TryComp<ItemSlotsComponent>(clothing, out var itemSlots))
            containers.Sort((a, b) => IsItemSlot((clothing, itemSlots), b).CompareTo(IsItemSlot((clothing, itemSlots), a)));

        var used = 0;
        foreach (var container in containers)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (!TryComp<ArmorPlateComponent>(contained, out var plate) || plate.Broken)
                    continue;

                result.Add((contained, plate));
                if (++used >= maxPlates)
                    return;
            }
        }
    }

    /// <summary>
    /// Защищает ли пластина носителя прямо сейчас: целая, лежит в одежде,
    /// одежда надета в подходящий слот и пластина входит в число активных.
    /// </summary>
    public bool IsPlateActive(EntityUid plate)
    {
        if (!TryComp<ArmorPlateComponent>(plate, out var comp) || comp.Broken)
            return false;

        if (!_container.TryGetContainingContainer(plate, out var container))
            return false;

        var clothing = container.Owner;
        if (!HasComp<ClothingComponent>(clothing) || !IsWornInAllowedSlot(clothing))
            return false;

        _activeCheck.Clear();
        GetActivePlates(clothing, _activeCheck);

        foreach (var active in _activeCheck)
        {
            if (active.Owner == plate)
                return true;
        }

        return false;
    }

    private bool IsItemSlot(Entity<ItemSlotsComponent> itemSlots, BaseContainer container)
    {
        return _itemSlots.TryGetSlot(itemSlots, container.ID, out var slot, itemSlots.Comp) && slot.ContainerSlot == container;
    }

    /// <summary>
    /// Пропускает урон через пластину. Возвращает урон, который дойдёт до носителя.
    /// </summary>
    public DamageSpecifier AbsorbDamage(Entity<ArmorPlateComponent> plate, EntityUid wearer, DamageSpecifier damage, float armorPenetration = 0f)
    {
        if (plate.Comp.Broken)
            return damage;

        var modifiers = DamageSpecifier.PenetrateArmor(GetEffectiveModifiers(plate), armorPenetration);
        var reduced = DamageSpecifier.ApplyModifierSet(damage, modifiers);
        var absorbed = DamageSpecifier.GetPositive(damage - reduced);
        var absorbedTotal = absorbed.GetTotal().Float();

        if (absorbedTotal <= 0f)
            return damage;

        var wear = absorbedTotal * plate.Comp.WearMultiplier;
        var durability = plate.Comp.Durability;

        // Не хватает прочности — пластина поглощает только часть урона и ломается.
        if (wear > durability && wear > 0f)
        {
            var fraction = durability / wear;
            reduced = damage - absorbed * fraction;
            wear = durability;
        }

        // Изменение прочности и эффекты — только на сервере, клиент получит состояние.
        if (_net.IsServer)
        {
            plate.Comp.Damage = MathF.Min(plate.Comp.MaxDurability, plate.Comp.Damage + wear);

            if (plate.Comp.Damage >= plate.Comp.MaxDurability)
                BreakPlate(plate, wearer);
            else
            {
                _audio.PlayPvs(plate.Comp.HitSound, wearer);
                Dirty(plate);
            }
        }

        return reduced;
    }

    /// <summary>
    /// Насколько хорошо пластина защищает при текущей прочности: 1 — полностью,
    /// ниже <see cref="ArmorPlateComponent.DegradeBelow"/> линейно падает до <see cref="ArmorPlateComponent.WornEfficiency"/>.
    /// </summary>
    public static float GetEfficiency(ArmorPlateComponent plate)
    {
        if (plate.Broken)
            return 0f;

        if (plate.WornEfficiency >= 1f || plate.DegradeBelow <= 0f || plate.MaxDurability <= 0f)
            return 1f;

        var left = DurabilityFormat.Fraction(plate.Durability, plate.MaxDurability);
        if (left >= plate.DegradeBelow)
            return 1f;

        var worn = Math.Clamp(plate.WornEfficiency, 0f, 1f);
        return worn + (1f - worn) * (left / plate.DegradeBelow);
    }

    /// <summary>
    /// Множитель силы резистов: износ и бонус редкости складываются.
    /// Например, редкость +30% и износ −20% дают ×1.1.
    /// </summary>
    public static float GetProtectionFactor(ArmorPlateComponent plate, RareArmorPlateComponent? rare)
    {
        if (plate.Broken)
            return 0f;

        return MathF.Max(0f, GetEfficiency(plate) + (rare?.ProtectionBonus ?? 0f));
    }

    /// <summary>
    /// Резисты пластины с учётом износа и редкости.
    /// </summary>
    public DamageModifierSet GetEffectiveModifiers(Entity<ArmorPlateComponent> plate)
    {
        TryComp<RareArmorPlateComponent>(plate, out var rare);
        return GetEffectiveModifiers(plate.Comp, GetProtectionFactor(plate.Comp, rare));
    }

    /// <summary>
    /// Резисты пластины, каждый из которых усилен или ослаблен в <paramref name="factor"/> раз.
    /// </summary>
    /// <summary>
    /// Максимальное снижение урона, до которого бонус редкости может поднять резист.
    /// </summary>
    public const float MaxReduction = 0.95f;

    public static DamageModifierSet GetEffectiveModifiers(ArmorPlateComponent plate, float factor)
    {
        if (MathF.Abs(factor - 1f) < 0.0001f)
            return plate.Modifiers;

        var result = new DamageModifierSet
        {
            IgnoreArmorPierceFlags = plate.Modifiers.IgnoreArmorPierceFlags,
        };

        // Снижение урона (1 - коэффициент) масштабируем, уязвимости (> 1) не трогаем.
        // Бонус редкости не может сделать пластину неуязвимой: не больше MaxReduction, если база не выше.
        foreach (var (type, coefficient) in plate.Modifiers.Coefficients)
        {
            if (coefficient >= 1f)
            {
                result.Coefficients[type] = coefficient;
                continue;
            }

            var scaled = 1f - (1f - coefficient) * factor;
            result.Coefficients[type] = MathF.Max(scaled, MathF.Min(coefficient, 1f - MaxReduction));
        }

        foreach (var (type, flat) in plate.Modifiers.FlatReduction)
        {
            result.FlatReduction[type] = flat > 0f ? flat * factor : flat;
        }

        return result;
    }

    public void BreakPlate(Entity<ArmorPlateComponent> plate, EntityUid? wearer = null)
    {
        if (plate.Comp.Broken)
            return;

        plate.Comp.Broken = true;
        plate.Comp.Damage = plate.Comp.MaxDurability;
        Dirty(plate);

        _appearance.SetData(plate, ArmorPlateVisuals.Broken, true);
        _nameModifier.RefreshNameModifiers(plate.Owner);

        var target = wearer ?? plate.Owner;
        _audio.PlayPvs(plate.Comp.BreakSound, target);

        if (wearer != null)
        {
            _popup.PopupEntity(Loc.GetString("armor-plate-broken-popup", ("plate", plate.Owner)),
                wearer.Value, wearer.Value, PopupType.MediumCaution);
        }
    }

    /// <summary>
    /// Восстанавливает пластину (на будущее: ремонт).
    /// </summary>
    public void RepairPlate(Entity<ArmorPlateComponent> plate, float amount)
    {
        plate.Comp.Damage = MathF.Max(0f, plate.Comp.Damage - amount);
        if (plate.Comp.Broken && plate.Comp.Damage < plate.Comp.MaxDurability)
        {
            plate.Comp.Broken = false;
            _appearance.SetData(plate, ArmorPlateVisuals.Broken, false);
            _nameModifier.RefreshNameModifiers(plate.Owner);
        }

        Dirty(plate);
    }

    private void OnExamined(Entity<ArmorPlateComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(ArmorPlateComponent)))
        {
            if (ent.Comp.WalkSpeedModifier < 1f || ent.Comp.SprintSpeedModifier < 1f)
            {
                args.PushMarkup(Loc.GetString("armor-plate-examine-slowdown",
                    ("value", MathF.Round((1f - MathF.Min(ent.Comp.WalkSpeedModifier, ent.Comp.SprintSpeedModifier)) * 100, 1))));
            }

            if (ent.Comp.Broken)
            {
                args.PushMarkup(Loc.GetString("armor-plate-examine-broken"));
                return;
            }

            var durability = ent.Comp.Durability;
            var fraction = DurabilityFormat.Fraction(durability, ent.Comp.MaxDurability);

            args.PushMarkup(Loc.GetString("armor-plate-examine-durability",
                ("color", DurabilityFormat.Color(fraction)),
                ("bar", DurabilityFormat.Bar(fraction)),
                ("current", MathF.Ceiling(durability)),
                ("max", ent.Comp.MaxDurability)));

            var efficiency = GetEfficiency(ent.Comp);
            if (efficiency < 1f)
            {
                args.PushMarkup(Loc.GetString("armor-plate-examine-worn",
                    ("value", MathF.Round((1f - efficiency) * 100, 1))));
            }

            if (!args.IsInDetailsRange)
                return;

            args.PushMarkup(Loc.GetString("armor-plate-examine-resists"));

            // Показываем резисты уже с учётом износа, а рядом — итоговое изменение,
            // как у характеристик оружия: зелёным если лучше базы, красным если хуже.
            var modifiers = GetEffectiveModifiers(ent);
            foreach (var (type, coefficient) in modifiers.Coefficients)
            {
                var value = MathF.Round((1f - coefficient) * 100, 1);
                var baseValue = MathF.Round((1f - ent.Comp.Modifiers.Coefficients[type]) * 100, 1);

                args.PushMarkup(Loc.GetString("armor-coefficient-value",
                    ("type", Loc.GetString("armor-damage-type-" + type.ToLower())),
                    ("value", value)) + FormatChange(value - baseValue, "armor-plate-examine-change-percent"));
            }

            foreach (var (type, flat) in modifiers.FlatReduction)
            {
                var baseFlat = ent.Comp.Modifiers.FlatReduction[type];

                args.PushMarkup(Loc.GetString("armor-reduction-value",
                    ("type", Loc.GetString("armor-damage-type-" + type.ToLower())),
                    ("value", MathF.Round(flat, 1))) + FormatChange(MathF.Round(flat - baseFlat, 1), "armor-plate-examine-change-flat"));
            }

            args.PushMarkup(Loc.GetString("armor-plate-examine-hint"));
        }
    }

    /// <summary>
    /// Текст изменения резиста относительно базы пластины: « (+5%)» зелёным или « (−5%)» красным.
    /// Пустая строка, если изменения нет.
    /// </summary>
    private string FormatChange(float change, string locId)
    {
        if (MathF.Abs(change) < 0.05f)
            return string.Empty;

        return " " + Loc.GetString(locId,
            ("color", change > 0 ? "lime" : "red"),
            ("plus", change > 0 ? "+" : ""),
            ("value", change));
    }
}
