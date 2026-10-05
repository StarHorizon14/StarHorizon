using Content.Server.Temperature.Components;
using Content.Shared._Horizon.Traits;

namespace Content.Server._Horizon.Traits;

/// <summary>
/// Shifts the entity's temperature damage thresholds and scales temperature damage, relative to its species values.
/// Traits can't just add a TemperatureComponent: every mob already has one, so it would be skipped.
/// </summary>
public sealed partial class ModifyTemperature : BaseTraitEffect
{
    /// <summary>
    /// Added to the heat damage threshold, in kelvin.
    /// </summary>
    [DataField]
    public float HeatThresholdChange;

    /// <summary>
    /// Added to the cold damage threshold, in kelvin. Negative values let you withstand colder temperatures.
    /// </summary>
    [DataField]
    public float ColdThresholdChange;

    [DataField]
    public float HeatDamageModifier = 1f;

    [DataField]
    public float ColdDamageModifier = 1f;

    public override void DoEffect(EntityUid uid, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent<TemperatureComponent>(uid, out var temperature))
            return;

        temperature.HeatDamageThreshold += HeatThresholdChange;
        temperature.ColdDamageThreshold += ColdThresholdChange;
        temperature.HeatDamage *= HeatDamageModifier;
        temperature.ColdDamage *= ColdDamageModifier;
    }
}
