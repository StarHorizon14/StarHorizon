using System.Numerics;
using Content.Server._Horizon.Traits.Components;
using Content.Server._Horizon.Weapons;
using Content.Server.Medical;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Drunk;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Horizon.Traits.Systems;

public sealed class SubstanceDependencySystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> PoisonDamage = "Poison";
    private static readonly ProtoId<DamageTypePrototype> CellularDamage = "Cellular";

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SubstanceDependencyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SubstanceDependencyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<SubstanceDependencyComponent, GetRecoilModifiersEvent>(OnGetRecoil);
    }

    private void OnStartup(Entity<SubstanceDependencyComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextCraving = RandomDelay(ent.Comp.CravingInterval);
        ent.Comp.NextIncident = RandomDelay(ent.Comp.IncidentInterval);
    }

    private void OnRefreshSpeed(Entity<SubstanceDependencyComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.ActiveEffects.Contains(DependencyEffect.Slowdown))
            args.ModifySpeed(ent.Comp.SlowdownModifier);
    }

    private void OnGetRecoil(Entity<SubstanceDependencyComponent> ent, ref GetRecoilModifiersEvent args)
    {
        if (ent.Comp.ActiveEffects.Contains(DependencyEffect.AimPenalty))
            args.Modifier *= ent.Comp.AimSpreadModifier;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SubstanceDependencyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_mobState.IsDead(uid))
                continue;

            comp.SinceDose += TimeSpan.FromSeconds(frameTime);
            comp.NextSecond -= frameTime;

            if (comp.NextSecond > 0)
                continue;

            comp.NextSecond += 1f;
            Tick((uid, comp));
        }
    }

    private void Tick(Entity<SubstanceDependencyComponent> ent)
    {
        if (HasSubstance(ent))
        {
            Satisfy(ent);
            return;
        }

        UpdateStages(ent);
        ApplyEffects(ent);
        TickCraving(ent);
    }

    private bool HasSubstance(Entity<SubstanceDependencyComponent> ent)
    {
        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream) ||
            !_solution.ResolveSolution(ent.Owner, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out var solution))
            return false;

        foreach (var (reagentId, _) in solution.Contents)
        {
            if (ent.Comp.Reagents.Contains(reagentId.Prototype))
                return true;

            if (!_proto.TryIndex<ReagentPrototype>(reagentId.Prototype, out var reagent) ||
                reagent.Metabolisms == null)
                continue;

            foreach (var group in ent.Comp.MetabolismGroups)
            {
                if (reagent.Metabolisms.ContainsKey(group))
                    return true;
            }
        }

        return false;
    }

    private void Satisfy(Entity<SubstanceDependencyComponent> ent)
    {
        var comp = ent.Comp;
        var suffering = comp.ReachedStages > 0;

        comp.SinceDose = TimeSpan.Zero;
        comp.ReachedStages = 0;

        if (suffering && comp.ReliefMessage is { } relief)
            _popup.PopupEntity(Loc.GetString(relief), ent, ent, PopupType.Medium);

        if (comp.ActiveEffects.Count == 0)
            return;

        comp.ActiveEffects.Clear();
        _speedModifier.RefreshMovementSpeedModifiers(ent);
    }

    private void UpdateStages(Entity<SubstanceDependencyComponent> ent)
    {
        var comp = ent.Comp;
        var reached = 0;
        foreach (var stage in comp.Stages)
        {
            if (comp.SinceDose >= stage.Time)
                reached++;
        }

        if (reached == comp.ReachedStages)
            return;

        var worsened = reached > comp.ReachedStages;
        comp.ReachedStages = reached;

        comp.ActiveEffects.Clear();
        for (var i = 0; i < reached; i++)
        {
            foreach (var effect in comp.Stages[i].Effects)
            {
                comp.ActiveEffects.Add(effect);
            }
        }

        _speedModifier.RefreshMovementSpeedModifiers(ent);

        if (worsened && reached > 0 && comp.Stages[reached - 1].Message is { } message)
            _popup.PopupEntity(Loc.GetString(message), ent, ent, PopupType.LargeCaution);
    }

    private void ApplyEffects(Entity<SubstanceDependencyComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.ActiveEffects.Count == 0)
            return;

        if (comp.ActiveEffects.Contains(DependencyEffect.Clouding))
            _status.TryUpdateStatusEffectDuration(ent, SharedDrunkSystem.Woozy, TimeSpan.FromSeconds(3));

        if (comp.ActiveEffects.Contains(DependencyEffect.Jitter))
            _jitter.DoJitter(ent, TimeSpan.FromSeconds(3), true, 5f, 8f);

        if (comp.ActiveEffects.Contains(DependencyEffect.Poison))
            Damage(ent, PoisonDamage);

        if (comp.ActiveEffects.Contains(DependencyEffect.Cellular))
            Damage(ent, CellularDamage);

        var canVomit = comp.ActiveEffects.Contains(DependencyEffect.Vomit);
        var canSleep = comp.ActiveEffects.Contains(DependencyEffect.Sleep);
        if (!canVomit && !canSleep)
            return;

        comp.NextIncident -= 1f;
        if (comp.NextIncident > 0)
            return;

        comp.NextIncident = RandomDelay(comp.IncidentInterval);

        if (canVomit && (!canSleep || _random.Prob(0.5f)))
        {
            _vomit.Vomit(ent);
            return;
        }

        var duration = TimeSpan.FromSeconds(RandomDelay(comp.SleepDuration));
        _status.TryAddStatusEffectDuration(ent, SleepingSystem.StatusEffectForcedSleeping, duration);
    }

    private void Damage(Entity<SubstanceDependencyComponent> ent, ProtoId<DamageTypePrototype> type)
    {
        var damage = new DamageSpecifier(_proto.Index(type), ent.Comp.DamagePerSecond);
        _damageable.TryChangeDamage(ent, damage, interruptsDoAfters: false);
    }

    private void TickCraving(Entity<SubstanceDependencyComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.SinceDose < comp.CravingStart || comp.CravingMessages.Count == 0)
            return;

        comp.NextCraving -= 1f;
        if (comp.NextCraving > 0)
            return;

        comp.NextCraving = RandomDelay(comp.CravingInterval);
        _popup.PopupEntity(Loc.GetString(_random.Pick(comp.CravingMessages)), ent, ent, PopupType.MediumCaution);
    }

    private float RandomDelay(Vector2 range)
    {
        return _random.NextFloat(range.X, range.Y);
    }
}
