using Content.Shared._Horizon._Fractions.AnCo.Biofabricator;
using Content.Shared.Cloning.Events;

namespace Content.Server._NF.Traits.Assorted;

public sealed class UnclonableSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnclonableComponent, CloningAttemptEvent>(OnCloningAttempt);
        SubscribeLocalEvent<UnclonableComponent, AnCoMemoryCardBindAttemptEvent>(OnMemoryCardBindAttempt);
    }

    private void OnCloningAttempt(Entity<UnclonableComponent> ent, ref CloningAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnMemoryCardBindAttempt(Entity<UnclonableComponent> ent, ref AnCoMemoryCardBindAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
