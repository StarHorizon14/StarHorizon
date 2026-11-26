using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
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

        InitializeWeight();
    }

    private void OnMapInit(Entity<ArmorPlateComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.Broken && ent.Comp.Damage >= ent.Comp.MaxDurability)
            ent.Comp.Broken = true;

        _appearance.SetData(ent, ArmorPlateVisuals.Broken, ent.Comp.Broken);
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

        var modifiers = DamageSpecifier.PenetrateArmor(plate.Comp.Modifiers, armorPenetration);
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

    public void BreakPlate(Entity<ArmorPlateComponent> plate, EntityUid? wearer = null)
    {
        if (plate.Comp.Broken)
            return;

        plate.Comp.Broken = true;
        plate.Comp.Damage = plate.Comp.MaxDurability;
        Dirty(plate);

        _appearance.SetData(plate, ArmorPlateVisuals.Broken, true);

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
            var percent = ent.Comp.MaxDurability > 0 ? durability / ent.Comp.MaxDurability : 0f;
            var color = percent > 0.66f ? "lime" : percent > 0.33f ? "yellow" : "red";

            args.PushMarkup(Loc.GetString("armor-plate-examine-durability",
                ("color", color),
                ("current", MathF.Ceiling(durability)),
                ("max", ent.Comp.MaxDurability)));

            if (!args.IsInDetailsRange)
                return;

            args.PushMarkup(Loc.GetString("armor-plate-examine-resists"));

            foreach (var (type, coefficient) in ent.Comp.Modifiers.Coefficients)
            {
                args.PushMarkup(Loc.GetString("armor-coefficient-value",
                    ("type", Loc.GetString("armor-damage-type-" + type.ToLower())),
                    ("value", MathF.Round((1f - coefficient) * 100, 1))));
            }

            foreach (var (type, flat) in ent.Comp.Modifiers.FlatReduction)
            {
                args.PushMarkup(Loc.GetString("armor-reduction-value",
                    ("type", Loc.GetString("armor-damage-type-" + type.ToLower())),
                    ("value", flat)));
            }

            args.PushMarkup(Loc.GetString("armor-plate-examine-hint"));
        }
    }
}
