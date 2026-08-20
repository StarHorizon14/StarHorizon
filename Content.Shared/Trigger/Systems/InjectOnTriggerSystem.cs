using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Trigger.Components.Effects;

namespace Content.Shared.Trigger.Systems;

public sealed class InjectOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InjectOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<InjectOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;
        if (target == null || !TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        if (!_solutionContainer.TryGetSolution((ent.Owner, null), ent.Comp.Solution, out var soln, out var solution))
            return;

        var removed = _solutionContainer.SplitSolution(soln.Value, solution.Volume);
        args.Handled |= _bloodstream.TryAddToChemicals((target.Value, bloodstream), removed);
    }
}
