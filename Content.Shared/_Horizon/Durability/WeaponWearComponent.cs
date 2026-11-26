using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Durability;

/// <summary>
/// Износ оружия. Каждый выстрел или удар по цели тратит прочность.
/// Изношенное огнестрельное оружие начинает давать осечки, а полностью
/// изношенное оружие ломается и перестаёт работать, пока его не починят.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(WeaponWearSystem))]
public sealed partial class WeaponWearComponent : Component
{
    /// <summary>
    /// Запас прочности нового оружия.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxDurability = 1000f;

    /// <summary>
    /// Накопленный износ. При <see cref="MaxDurability"/> оружие сломано.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Wear;

    /// <summary>
    /// Сколько прочности тратит один выстрел (не дробина, а именно выстрел) или один удар по цели.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WearPerUse = 1f;

    /// <summary>
    /// Вероятность того, что использование вообще изнашивает оружие.
    /// Позволяет клинкам тупиться медленно, но непредсказуемо.
    /// </summary>
    [DataField]
    public float WearChance = 1f;

    /// <summary>
    /// Доля износа, начиная с которой огнестрел может дать осечку.
    /// </summary>
    [DataField]
    public float MalfunctionFrom = 0.5f;

    /// <summary>
    /// Шанс осечки на выстрел при износе, близком к полному.
    /// Между <see cref="MalfunctionFrom"/> и полным износом шанс растёт по <see cref="MalfunctionCurve"/>.
    /// </summary>
    [DataField]
    public float MalfunctionChance = 0.06f;

    /// <summary>
    /// Степень кривой роста шанса осечки: 1 — линейно, больше 1 — шанс резко растёт только под конец.
    /// </summary>
    [DataField]
    public float MalfunctionCurve = 2f;

    /// <summary>
    /// Сколько оружие без затвора (энергетическое, револьверы и т.п.) не стреляет после осечки.
    /// Оружие с затвором вместо этого открывает затвор — его нужно передёрнуть вручную.
    /// </summary>
    [DataField]
    public TimeSpan StallDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// До какого момента оружие без затвора не может стрелять.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan StalledUntil;

    /// <summary>
    /// Доля износа, начиная с которой у огнестрела падают характеристики.
    /// Между этим порогом и полным износом штрафы растут линейно до максимальных.
    /// </summary>
    [DataField]
    public float StatPenaltyFrom = 0.5f;

    /// <summary>
    /// Насколько при полном износе растут разброс и отдача (0.5 = +50%).
    /// </summary>
    [DataField]
    public float MaxAccuracyPenalty = 0.5f;

    /// <summary>
    /// Насколько при полном износе падает темп стрельбы (0.2 = −20%).
    /// </summary>
    [DataField]
    public float MaxFireRatePenalty = 0.2f;

    /// <summary>
    /// Насколько при полном износе падает начальная скорость снаряда (0.1 = −10%).
    /// </summary>
    [DataField]
    public float MaxProjectileSpeedPenalty = 0.1f;

    /// <summary>
    /// Доля цены, которая остаётся у полностью изношенного оружия при продаже.
    /// </summary>
    [DataField]
    public float BrokenPriceFraction = 0.2f;

    [DataField]
    public SoundSpecifier? BreakSound = new SoundCollectionSpecifier("MetalBreak");

    /// <summary>
    /// Ограничение частоты всплывающих сообщений при попытке выстрелить. Не синхронизируется.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextBlockedPopup;

    public float Durability => MathF.Max(0f, MaxDurability - Wear);

    public bool Broken => MaxDurability > 0f && Wear >= MaxDurability;
}
