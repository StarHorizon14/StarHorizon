using Content.Shared._Horizon.Castaway;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Shared.Random.Rules;

/// <summary>
/// Returns true if the attached entity is a Castaway survivor and the Castaway ambience hasn't been disabled.
/// </summary>
public sealed partial class HasCastawaySurvivorRule : RulesRule
{
    public override bool Check(EntityManager entManager, EntityUid uid)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        if (!cfg.GetCVar(CCVars.CastawayAmbienceEnabled))
            return Inverted;

        return entManager.HasComponent<CastawaySurvivorComponent>(uid) != Inverted;
    }
}
