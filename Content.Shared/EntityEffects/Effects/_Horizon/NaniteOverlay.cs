using Content.Shared._Horizon._Fractions.AnCo.Nanites;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects._Horizon;

/// <summary>
///     Отображает визуальный оверлей нанитов поверх существа, пока реагент
///     метаболизируется в его крови.
/// </summary>
public sealed partial class NaniteOverlay : EntityEffect
{
    public override void Effect(EntityEffectBaseArgs args)
    {
        args.EntityManager.System<NaniteOverlaySystem>().Refresh(args.TargetEntity);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        null;
}
