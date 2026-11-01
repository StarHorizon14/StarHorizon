using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Robust.Shared.Network; // Horizon
using Robust.Shared.Timing;

namespace Content.Shared.RCD.Systems;

public sealed class RCDAmmoSystem : EntitySystem
{
    [Dependency] private readonly SharedChargesSystem _sharedCharges = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!; // Horizon

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RCDAmmoComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RCDAmmoComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnExamine(EntityUid uid, RCDAmmoComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var examineMessage = Loc.GetString("rcd-ammo-component-on-examine", ("charges", comp.Charges));
        args.PushText(examineMessage);
    }

    private void OnAfterInteract(EntityUid uid, RCDAmmoComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_timing.IsFirstTimePredicted)
            return;

        if (args.Target is not { Valid: true } target ||
            !HasComp<RCDComponent>(target) ||
            !TryComp<LimitedChargesComponent>(target, out var charges))
            return;

        var current = _sharedCharges.GetCurrentCharges((target, charges));
        var user = args.User;

        // ## Frontier - Shipyard RCD ammo only fits in shipyard RCD.
        // At this point RCDComponent is guaranteed
        EnsureComp<RCDComponent>(target, out var rcdComponent);

        // Horizon start
        if (!rcdComponent.GenericUse)
            return;
        // Horizon end

        if (rcdComponent.IsShipyardRCD && !comp.IsShipyardRCDAmmo || !rcdComponent.IsShipyardRCD && comp.IsShipyardRCDAmmo)
        {
            _popup.PopupClient(Loc.GetString("rcd-component-wrong-ammo-type"), target, user);
            return;
        }

        args.Handled = true;
        var count = Math.Min(charges.MaxCharges - current, comp.Charges);
        if (count <= 0)
        {
            _popup.PopupClient(Loc.GetString("rcd-ammo-component-after-interact-full"), target, user);
            return;
        }

        _popup.PopupClient(Loc.GetString("rcd-ammo-component-after-interact-refilled"), target, user);
        _sharedCharges.AddCharges(target, count);
        comp.Charges -= count;
        Dirty(uid, comp);

        // prevent having useless ammo with 0 charges
        if (comp.Charges <= 0)
            QueueDel(uid);
    }

    /// <summary>
    /// Horizon: moves charges from an ammo cartridge into an entity with limited charges, without user interaction.
    /// Used by mech RCD equipment to reload from the mech's storage.
    /// </summary>
    /// <returns>The amount of charges transferred.</returns>
    public int TryTransferCharges(Entity<RCDAmmoComponent> ammo, Entity<LimitedChargesComponent> target, bool shipyard)
    {
        if (ammo.Comp.IsShipyardRCDAmmo != shipyard || ammo.Comp.Charges <= 0)
            return 0;

        var current = _sharedCharges.GetCurrentCharges((target.Owner, target.Comp));
        var count = Math.Min(target.Comp.MaxCharges - current, ammo.Comp.Charges);
        if (count <= 0)
            return 0;

        _sharedCharges.AddCharges(target.Owner, count);
        ammo.Comp.Charges -= count;
        Dirty(ammo);

        if (ammo.Comp.Charges <= 0 && _net.IsServer)
            QueueDel(ammo);

        return count;
    }
}
