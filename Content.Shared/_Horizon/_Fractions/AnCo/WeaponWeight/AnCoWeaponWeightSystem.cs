using Content.Shared._Horizon._Fractions.AnCo.WeaponWeight.Components;
using Content.Shared.Clothing;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;

namespace Content.Shared._Horizon._Fractions.AnCo.WeaponWeight;

/// <summary>
/// Система веса оружия AnCo.
/// Блокирует поднятие оружия если его вес превышает подъёмность скафандра.
/// При попытке поднять слишком тяжёлое оружие - станит игрока.
/// При снятии скафандра - дропает слишком тяжёлое оружие из рук.
/// </summary>
public sealed class AnCoWeaponWeightSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Блокировка поднятия слишком тяжёлого оружия + стан
        SubscribeLocalEvent<AnCoWeaponWeightComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);

        // При снятии скафандра - проверить и дропнуть тяжёлое оружие
        SubscribeLocalEvent<AnCoLiftStrengthComponent, ClothingGotUnequippedEvent>(OnArmorUnequipped);

        // Examine
        SubscribeLocalEvent<AnCoWeaponWeightComponent, ExaminedEvent>(OnWeaponExamined);
        SubscribeLocalEvent<AnCoLiftStrengthComponent, ExaminedEvent>(OnArmorExamined);
    }

    /// <summary>
    /// Проверка при попытке поднять оружие.
    /// </summary>
    private void OnPickupAttempt(Entity<AnCoWeaponWeightComponent> entity, ref GettingPickedUpAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var liftStrength = GetUserLiftStrength(args.User);

        if (entity.Comp.Weight > liftStrength)
        {
            args.Cancel();

            _popup.PopupClient(Loc.GetString("anco-weapon-weight-too-heavy",
                ("weight", entity.Comp.Weight),
                ("liftStrength", liftStrength)),
                args.User, args.User, PopupType.MediumCaution);
        }
    }

    /// <summary>
    /// При снятии скафандра - дропаем слишком тяжёлое оружие.
    /// </summary>
    private void OnArmorUnequipped(Entity<AnCoLiftStrengthComponent> entity, ref ClothingGotUnequippedEvent args)
    {
        var user = args.Wearer;

        // Получаем новую подъёмность (после снятия скафандра)
        var newLiftStrength = GetUserLiftStrength(user);

        // Проверяем все предметы в руках
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!TryComp<AnCoWeaponWeightComponent>(held, out var weightComp))
                continue;

            // Если оружие слишком тяжёлое без скафандра - дропаем
            if (weightComp.Weight > newLiftStrength)
            {
                _hands.TryDrop(user, held, checkActionBlocker: false);

                _popup.PopupClient(Loc.GetString("anco-weapon-weight-dropped"),
                    user, user, PopupType.MediumCaution);
            }
        }
    }

    /// <summary>
    /// Получает подъёмность пользователя из его экипировки.
    /// Проверяет слот outerClothing на наличие компонента AnCoLiftStrength.
    /// </summary>
    public int GetUserLiftStrength(EntityUid user)
    {
        // Проверяем есть ли скафандр в слоте outerClothing
        if (!_inventory.TryGetSlotEntity(user, "outerClothing", out var outerClothing))
            return 0;

        // Проверяем есть ли на скафандре компонент подъёмности
        if (!TryComp<AnCoLiftStrengthComponent>(outerClothing, out var liftComp))
            return 0;

        return liftComp.LiftStrength;
    }

    /// <summary>
    /// Показывает вес оружия при осмотре.
    /// </summary>
    private void OnWeaponExamined(Entity<AnCoWeaponWeightComponent> entity, ref ExaminedEvent args)
    {
        var message = Loc.GetString("anco-weapon-weight-examine", ("weight", entity.Comp.Weight));
        args.PushMarkup(message);
    }

    /// <summary>
    /// Показывает подъёмность скафандра при осмотре.
    /// </summary>
    private void OnArmorExamined(Entity<AnCoLiftStrengthComponent> entity, ref ExaminedEvent args)
    {
        var message = Loc.GetString("anco-lift-strength-examine", ("liftStrength", entity.Comp.LiftStrength));
        args.PushMarkup(message);
    }
}
