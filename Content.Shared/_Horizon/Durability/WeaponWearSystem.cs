using Content.Shared._NF.Weapons.Rarity;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Components;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Horizon.Durability;

/// <summary>
/// Износ оружия: тратит прочность при стрельбе и ударах, вызывает осечки у изношенного
/// огнестрела и блокирует сломанное оружие.
/// Износ и осечки считает только сервер, клиент получает итог через состояние компонента,
/// а блокировку выстрела/удара предсказывает сам.
/// </summary>
public sealed class WeaponWearSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;

    private static readonly TimeSpan BlockedPopupCooldown = TimeSpan.FromSeconds(1.5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeaponWearComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<WeaponWearComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<WeaponWearComponent, AttemptMeleeEvent>(OnMeleeAttempt);
        SubscribeLocalEvent<WeaponWearComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<WeaponWearComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<WeaponWearComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WeaponWearComponent, RefreshNameModifiersEvent>(OnRefreshName);
        SubscribeLocalEvent<WeaponWearComponent, PriceCalculationEvent>(OnPriceCalculation);
        SubscribeLocalEvent<WeaponWearComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnMapInit(Entity<WeaponWearComponent> ent, ref MapInitEvent args)
    {
        // Оружие может быть заспавнено уже сломанным (например, с картой).
        if (ent.Comp.Broken)
            _nameModifier.RefreshNameModifiers(ent.Owner);
    }

    #region Характеристики

    /// <summary>
    /// Насколько износ ухудшает характеристики сейчас (0 — не ухудшает, 1 — максимальный штраф).
    /// </summary>
    public static float GetStatPenaltyProgress(WeaponWearComponent comp)
    {
        if (comp.MaxDurability <= 0f)
            return 0f;

        var worn = comp.Wear / comp.MaxDurability;
        if (worn <= comp.StatPenaltyFrom)
            return 0f;

        var span = 1f - comp.StatPenaltyFrom;
        return span > 0f ? Math.Clamp((worn - comp.StatPenaltyFrom) / span, 0f, 1f) : 1f;
    }

    /// <summary>
    /// Итоговые множители характеристик оружия: бонусы редкости и штрафы износа складываются.
    /// Например, +30% от редкости и −20% от износа дают +10%.
    /// Точность: меньше — лучше (это множитель разброса и отдачи).
    /// </summary>
    public static WeaponStatModifiers GetCombinedModifiers(RareWeaponComponent? rare, WeaponWearComponent? wear)
    {
        var accuracy = rare?.AccuracyModifier ?? 1f;
        var fireRate = rare?.FireRateModifier ?? 1f;
        var speed = rare?.ProjectileSpeedModifier ?? 1f;

        if (wear != null)
        {
            var progress = GetStatPenaltyProgress(wear);
            accuracy += wear.MaxAccuracyPenalty * progress;
            fireRate -= wear.MaxFireRatePenalty * progress;
            speed -= wear.MaxProjectileSpeedPenalty * progress;
        }

        return new WeaponStatModifiers(
            MathF.Max(accuracy, MinModifier),
            MathF.Max(fireRate, MinModifier),
            MathF.Max(speed, MinModifier));
    }

    private const float MinModifier = 0.1f;

    private void OnGunRefreshModifiers(Entity<WeaponWearComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (GetStatPenaltyProgress(ent.Comp) <= 0f)
            return;

        // Редкость уже умножила характеристики на свои множители (порядок обработчиков не важен,
        // умножение коммутативно). Домножаем на отношение итогового множителя к множителю редкости,
        // чтобы в сумме получилось «редкость + износ», а не «редкость × износ».
        TryComp<RareWeaponComponent>(ent, out var rare);
        var rarity = GetCombinedModifiers(rare, null);
        var combined = GetCombinedModifiers(rare, ent.Comp);

        var accuracy = combined.Accuracy / rarity.Accuracy;
        args.MinAngle *= accuracy;
        args.MaxAngle *= accuracy;
        args.AngleIncrease *= accuracy;
        args.AngleDecay *= 1 / accuracy;

        args.FireRate *= combined.FireRate / rarity.FireRate;
        args.ProjectileSpeed *= combined.ProjectileSpeed / rarity.ProjectileSpeed;
    }

    /// <summary>
    /// Пересчитывает характеристики оружия, если износ на них влияет или влиял до изменения.
    /// </summary>
    private void RefreshStats(Entity<WeaponWearComponent> ent, float progressBefore)
    {
        if (progressBefore <= 0f && GetStatPenaltyProgress(ent.Comp) <= 0f)
            return;

        if (HasComp<GunComponent>(ent))
            _gun.RefreshModifiers(ent.Owner);
    }

    #endregion

    #region Последствия поломки

    private void OnRefreshName(Entity<WeaponWearComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (ent.Comp.Broken)
            args.AddModifier("weapon-wear-broken-name");
    }

    private void OnPriceCalculation(Entity<WeaponWearComponent> ent, ref PriceCalculationEvent args)
    {
        if (!TryComp<StaticPriceComponent>(ent, out var price))
            return;

        var fraction = DurabilityFormat.Fraction(ent.Comp.Durability, ent.Comp.MaxDurability);
        DurabilityFormat.ApplyWearToPrice(ref args, price.Price, fraction, ent.Comp.BrokenPriceFraction);
    }

    #endregion

    #region Блокировка

    private void OnShotAttempted(Entity<WeaponWearComponent> ent, ref ShotAttemptedEvent args)
    {
        string? reason = null;

        if (ent.Comp.Broken)
            reason = "weapon-wear-broken-blocked";
        else if (ent.Comp.StalledUntil > _timing.CurTime)
            reason = "weapon-wear-stalled-blocked";

        if (reason == null)
            return;

        args.Cancel();

        var now = _timing.CurTime;
        if (now < ent.Comp.NextBlockedPopup)
            return;

        ent.Comp.NextBlockedPopup = now + BlockedPopupCooldown;
        _popup.PopupClient(Loc.GetString(reason, ("weapon", ent.Owner)), ent, args.User, PopupType.SmallCaution);
    }

    private void OnMeleeAttempt(Entity<WeaponWearComponent> ent, ref AttemptMeleeEvent args)
    {
        if (!ent.Comp.Broken)
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("weapon-wear-broken-blocked", ("weapon", ent.Owner));
    }

    #endregion

    #region Износ

    private void OnGunShot(Entity<WeaponWearComponent> ent, ref GunShotEvent args)
    {
        if (_net.IsClient || args.Ammo.Count == 0 || !CanWear(ent))
            return;

        // Изнашиваем за каждый выстрел, а не за каждую дробину или снаряд.
        for (var i = 0; i < args.Ammo.Count; i++)
        {
            AddWear(ent, ent.Comp.WearPerUse, args.User);
        }

        if (!ent.Comp.Broken)
            TryMalfunction(ent, args.User);
    }

    private void OnMeleeHit(Entity<WeaponWearComponent> ent, ref MeleeHitEvent args)
    {
        // Взмах по воздуху оружие не тупит.
        if (_net.IsClient || !args.IsHit || args.HitEntities.Count == 0 || !CanWear(ent))
            return;

        AddWear(ent, ent.Comp.WearPerUse, args.User);
    }

    /// <summary>
    /// Встроенное несъёмное оружие (боргов и т.п.) заменить нельзя, поэтому оно не изнашивается.
    /// </summary>
    private bool CanWear(EntityUid uid)
    {
        return !HasComp<UnremoveableComponent>(uid);
    }

    /// <summary>
    /// Добавляет износ с учётом <see cref="WeaponWearComponent.WearChance"/>. Только сервер.
    /// </summary>
    public void AddWear(Entity<WeaponWearComponent> ent, float amount, EntityUid? user = null)
    {
        if (ent.Comp.Broken || amount <= 0f)
            return;

        if (ent.Comp.WearChance < 1f && !_random.Prob(ent.Comp.WearChance))
            return;

        var progressBefore = GetStatPenaltyProgress(ent.Comp);
        ent.Comp.Wear = MathF.Min(ent.Comp.MaxDurability, ent.Comp.Wear + amount);
        Dirty(ent);
        RefreshStats(ent, progressBefore);

        if (ent.Comp.Broken)
            OnBroken(ent, user);
    }

    private void OnBroken(Entity<WeaponWearComponent> ent, EntityUid? user)
    {
        // Ломаем «чисто»: снимаем осечку, чтобы после ремонта оружие сразу работало.
        ent.Comp.StalledUntil = TimeSpan.Zero;
        Dirty(ent);

        _audio.PlayPvs(ent.Comp.BreakSound, ent);
        _nameModifier.RefreshNameModifiers(ent.Owner);

        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString("weapon-wear-broken-popup", ("weapon", ent.Owner)),
                ent, user.Value, PopupType.MediumCaution);
        }
    }

    /// <summary>
    /// Снимает износ. Если оружие было сломано, оно снова начинает работать.
    /// </summary>
    public void Repair(Entity<WeaponWearComponent> ent, float amount)
    {
        if (amount <= 0f)
            return;

        var wasBroken = ent.Comp.Broken;
        var progressBefore = GetStatPenaltyProgress(ent.Comp);
        ent.Comp.Wear = MathF.Max(0f, ent.Comp.Wear - amount);
        Dirty(ent);
        RefreshStats(ent, progressBefore);

        if (wasBroken && !ent.Comp.Broken)
            _nameModifier.RefreshNameModifiers(ent.Owner);
    }

    #endregion

    #region Осечки

    /// <summary>
    /// Шанс осечки на один выстрел при текущем износе.
    /// </summary>
    public float GetMalfunctionChance(WeaponWearComponent comp)
    {
        if (comp.MaxDurability <= 0f || comp.MalfunctionChance <= 0f)
            return 0f;

        var worn = comp.Wear / comp.MaxDurability;
        if (worn < comp.MalfunctionFrom)
            return 0f;

        var span = 1f - comp.MalfunctionFrom;
        var progress = span > 0f ? Math.Clamp((worn - comp.MalfunctionFrom) / span, 0f, 1f) : 1f;
        return comp.MalfunctionChance * MathF.Pow(progress, MathF.Max(comp.MalfunctionCurve, 0.01f));
    }

    private void TryMalfunction(Entity<WeaponWearComponent> ent, EntityUid user)
    {
        var chance = GetMalfunctionChance(ent.Comp);
        if (chance <= 0f || !_random.Prob(chance))
            return;

        // Оружие с затвором: затвор открывается и выбрасывает патрон, стрелок передёргивает его сам.
        if (TryComp<ChamberMagazineAmmoProviderComponent>(ent, out var chamber) && chamber.BoltClosed == true)
        {
            _gun.SetBoltClosed(ent, chamber, false, user);
            _popup.PopupEntity(Loc.GetString("weapon-wear-jam-bolt", ("weapon", ent.Owner)),
                ent, user, PopupType.MediumCaution);
            return;
        }

        // Остальное оружие (энергетическое, револьверы, помпы без затвора) просто временно отказывает.
        ent.Comp.StalledUntil = _timing.CurTime + ent.Comp.StallDuration;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("weapon-wear-jam-stall", ("weapon", ent.Owner)),
            ent, user, PopupType.MediumCaution);
    }

    #endregion

    private void OnExamined(Entity<WeaponWearComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(WeaponWearComponent), -10))
        {
            if (ent.Comp.Broken)
            {
                args.PushMarkup(Loc.GetString("weapon-wear-examine-broken"));
                return;
            }

            var fraction = DurabilityFormat.Fraction(ent.Comp.Durability, ent.Comp.MaxDurability);
            args.PushMarkup(Loc.GetString("durability-examine-bar",
                ("color", DurabilityFormat.Color(fraction)),
                ("bar", DurabilityFormat.Bar(fraction)),
                ("percent", DurabilityFormat.Percent(fraction))));

            if (GetMalfunctionChance(ent.Comp) > 0f)
                args.PushMarkup(Loc.GetString("weapon-wear-examine-unreliable"));
        }
    }
}

/// <summary>
/// Итоговые множители характеристик оружия. Точность — множитель разброса и отдачи (меньше — лучше).
/// </summary>
public readonly record struct WeaponStatModifiers(float Accuracy, float FireRate, float ProjectileSpeed);
