using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems.Hypospray;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;

namespace Content.Shared._Horizon._Fractions.AnCo.TitanInjector;

public sealed class TitanSuitInjectorSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TitanSuitInjectorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TitanSuitInjectorComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<TitanSuitInjectorComponent, TitanSuitInjectEvent>(OnInject);
    }

    private void OnMapInit(Entity<TitanSuitInjectorComponent> ent, ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.InjectActionEntity, ent.Comp.InjectAction);
        Dirty(ent);
    }

    private void OnGetItemActions(Entity<TitanSuitInjectorComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.InjectActionEntity, ent.Comp.InjectAction);
    }

    private void OnInject(Entity<TitanSuitInjectorComponent> ent, ref TitanSuitInjectEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        var cartridge = _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.CartridgeSlotId);
        if (cartridge is not { } cartridgeUid || !TryComp<SolutionCartridgeComponent>(cartridgeUid, out var cartridgeComp))
        {
            _popup.PopupClient(Loc.GetString("titan-suit-injector-no-cartridge"), user, user);
            return;
        }

        if (cartridgeComp.Solution.Volume <= 0)
        {
            _popup.PopupClient(Loc.GetString("titan-suit-injector-empty"), user, user);
            return;
        }

        if (!TryComp<BloodstreamComponent>(user, out var bloodstream))
            return;

        var amount = FixedPoint2.Min(ent.Comp.TransferAmount, cartridgeComp.Solution.Volume);
        var removed = cartridgeComp.Solution.SplitSolution(amount);

        if (!_bloodstream.TryAddToChemicals((user, bloodstream), removed))
            return;

        Dirty(cartridgeUid, cartridgeComp);
        args.Handled = true;
        _popup.PopupClient(Loc.GetString("titan-suit-injector-inject"), user, user);
    }
}
