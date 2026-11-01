using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Robust.Server.GameObjects;
using Content.Server.Emp;
using Content.Shared.Mech.Equipment.Components;
using Content.Server._Horizon.Mech.Components;
using Content.Server.Power.Components;

namespace Content.Server.Mech.Systems;

/// <inheritdoc/>
public sealed partial class MechSystem
{
    private void InitializeADT()
    {
        SubscribeLocalEvent<MechComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<MechComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<MechComponent, MechEquipmentDestroyedEvent>(OnEquipmentDestroyed);
        SubscribeLocalEvent<MechComponent, MechTurnLightsEvent>(OnTurnLightsEvent);
        SubscribeLocalEvent<MechComponent, MechInhaleEvent>(OnToggleInhale);

        SubscribeLocalEvent<MechEquipmentComponentsComponent, MechEquipmentGotSelectedEvent>(OnComponentSelected);
        SubscribeLocalEvent<MechEquipmentComponentsComponent, MechEquipmentGotDeselectedEvent>(OnComponentDeselected);
    }

    private const float BatterySyncInterval = 1f;
    private float _batterySyncAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _batterySyncAccumulator += frameTime;
        if (_batterySyncAccumulator < BatterySyncInterval)
            return;

        _batterySyncAccumulator = 0;

        // Self-recharging batteries (e.g. microreactor cells) change their charge without going through
        // TryChangeEnergy, so the mech's cached energy goes stale - and at 0 it never gets resynced.
        var query = EntityQueryEnumerator<MechComponent>();
        while (query.MoveNext(out var uid, out var mech))
        {
            if (!TryComp<BatteryComponent>(mech.BatterySlot.ContainedEntity, out var battery))
                continue;

            // FixedPoint2 only keeps two decimals, so compare with a matching tolerance
            if (MathHelper.CloseTo(battery.CurrentCharge, mech.Energy.Float(), 0.01f)
                && MathHelper.CloseTo(battery.MaxCharge, mech.MaxEnergy.Float(), 0.01f))
                continue;

            var wasEmpty = mech.Energy <= 0;
            mech.Energy = battery.CurrentCharge;
            mech.MaxEnergy = battery.MaxCharge;
            Dirty(uid, mech);
            UpdateUserInterface(uid, mech);

            if (wasEmpty != mech.Energy <= 0)
                _actionBlocker.UpdateCanMove(uid);
        }
    }

    private void OnToggleInhale(EntityUid uid, MechComponent component, MechInhaleEvent args)
    {
        if (component.Airtight)
        {
            component.Airtight = false;
            return;
        }
        component.Airtight = true;
    }

    private void OnDamageModify(EntityUid uid, MechComponent component, DamageModifyEvent args)
    {
        if (component.Modifiers != null)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, component.Modifiers);
    }

    private void OnTurnLightsEvent(EntityUid uid, MechComponent component, MechTurnLightsEvent args)
    {
        if (HasComp<PointLightComponent>(uid))
        {
            RemComp<PointLightComponent>(uid);
            _audio.PlayPvs(component.MechLightsOffSound, uid);
        }
        else
        {
            AddComp<PointLightComponent>(uid);
            _audio.PlayPvs(component.MechLightsOnSound, uid);
        }
    }

    private void OnEquipmentDestroyed(EntityUid uid, MechComponent component, ref MechEquipmentDestroyedEvent args)
    {
        Spawn("EffectSparks", Transform(uid).Coordinates);
        QueueDel(component.CurrentSelectedEquipment);
        _audio.PlayPvs(component.EquipmentDestroyedSound, uid);
    }

    private void OnEmpPulse(EntityUid uid, MechComponent comp, ref EmpPulseEvent args)
    {
        var damage = args.EnergyConsumption / 100;
        TryChangeEnergy(uid, -FixedPoint2.Min(comp.Energy, damage), comp);
        Spawn("EffectEmpPulse", Transform(uid).Coordinates);
    }

    private void OnComponentSelected(EntityUid uid, MechEquipmentComponentsComponent comp, ref MechEquipmentGotSelectedEvent args)
        => EntityManager.AddComponents(args.Mech, comp.Components);

    private void OnComponentDeselected(EntityUid uid, MechEquipmentComponentsComponent comp, ref MechEquipmentGotDeselectedEvent args)
        => EntityManager.RemoveComponents(args.Mech, comp.Components);

    public override void UpdateUserInterfaceByEquipment(EntityUid equipmentUid)
    {
        base.UpdateUserInterfaceByEquipment(equipmentUid);

        if (!TryComp<MechEquipmentComponent>(equipmentUid, out var comp))
        {
            Log.Error("Could not find mech equipment owner to update UI.");
            return;
        }
        if (!comp.EquipmentOwner.HasValue)
            return;
        UpdateUserInterface(comp.EquipmentOwner.Value);
    }
}
