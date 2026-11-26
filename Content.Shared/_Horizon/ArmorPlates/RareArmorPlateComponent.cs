using System.Numerics;
using Content.Shared._NF.Weapons.Rarity;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.ArmorPlates;

/// <summary>
/// Редкая бронепластина. При появлении получает столько случайных улучшений, каков уровень редкости:
/// каждое улучшение — это либо защита, либо прочность, либо меньшее замедление.
/// Каждое следующее улучшение сильнее предыдущего на <see cref="LevelScaling"/>.
/// </summary>
/// <remarks>
/// Пока нигде не выдаётся сама — только готовые прототипы для админов (см. armor_plates_rare.yml).
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(ArmorPlateSystem))]
public sealed partial class RareArmorPlateComponent : Component
{
    [DataField, AutoNetworkedField]
    public WeaponRarity Rarity = WeaponRarity.Uncommon;

    /// <summary>
    /// Бонус к защите: доля, на которую усиливается каждый резист (0.2 = резист 25% становится 30%).
    /// Складывается со штрафом износа.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ProtectionBonus;

    /// <summary>
    /// Бонус к максимальной прочности (0.2 = +20%). Применяется один раз при появлении.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DurabilityBonus;

    /// <summary>
    /// Насколько уменьшается замедление от пластины (0.3 = замедление на 30% меньше).
    /// Применяется один раз при появлении.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SlowdownReduction;

    /// <summary>
    /// Разброс одного улучшения защиты.
    /// </summary>
    [DataField]
    public Vector2 ProtectionRoll = new(0.06f, 0.12f);

    /// <summary>
    /// Разброс одного улучшения прочности.
    /// </summary>
    [DataField]
    public Vector2 DurabilityRoll = new(0.15f, 0.3f);

    /// <summary>
    /// Разброс одного уменьшения замедления.
    /// </summary>
    [DataField]
    public Vector2 SlowdownRoll = new(0.15f, 0.3f);

    /// <summary>
    /// Во сколько раз каждое следующее улучшение сильнее предыдущего.
    /// </summary>
    [DataField]
    public float LevelScaling = 1.15f;

    /// <summary>
    /// Улучшения уже выданы. Чтобы пластина не улучшалась повторно (например, при загрузке карты).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Rolled;
}
