using Content.Shared._Horizon._Fractions.AnCo.Nanites;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects._Horizon;

/// <summary>
///     Проигрывает зацикленный звук локально существу, у которого метаболизируется реагент.
///     Звук слышен только самой цели, продлевается на каждый тик метаболизма
///     и плавно затухает, когда реагент заканчивается.
/// </summary>
public sealed partial class PlayMetabolismSound : EntityEffect
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    public override void Effect(EntityEffectBaseArgs args)
    {
        args.EntityManager.System<MetabolismSoundSystem>().Refresh(args.TargetEntity, Sound);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        null;
}
