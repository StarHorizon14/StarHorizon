// SPDX-FileCopyrightText: 2025 ark1368
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared._Crescent.ShipShields;
using Content.Shared.Examine;
using Content.Shared.Explosion.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;

namespace Content.Server._Crescent.ShipShields;
public partial class ShipShieldsSystem
{
    private const float MAX_EMP_DAMAGE = 10000f;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public void InitializeEmitters()
    {
        SubscribeLocalEvent<ShipShieldEmitterComponent, ShieldDeflectedEvent>(OnShieldDeflected);
        SubscribeLocalEvent<ShipShieldEmitterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ShipShieldEmitterComponent, ComponentRemove>(OnEmitterRemoved);
        SubscribeLocalEvent<ShipShieldComponent, ComponentRemove>(OnShieldRemoved);
    }

    /// <summary>
    /// Called when the shield generator (emitter) is removed/deleted.
    /// Cleans up the shield dome entity and grid component.
    /// </summary>
    private void OnEmitterRemoved(Entity<ShipShieldEmitterComponent> owner, ref ComponentRemove remove)
    {
        var emitter = owner.Comp;

        // Clean up via direct shield reference first (most reliable)
        if (emitter.Shield is { } shieldUid && Exists(shieldUid))
        {
            _pvsSys.RemoveGlobalOverride(shieldUid);
            QueueDel(shieldUid);
        }

        // Also clean up ShipShieldedComponent on the grid
        if (emitter.Shielded is { } shieldedUid && TryComp<ShipShieldedComponent>(shieldedUid, out _))
        {
            RemComp<ShipShieldedComponent>(shieldedUid);
        }

        emitter.Shield = null;
        emitter.Shielded = null;
    }

    /// <summary>
    /// Called when the shield dome entity itself is deleted directly.
    /// Cleans up stale references on the grid and emitter.
    /// </summary>
    private void OnShieldRemoved(Entity<ShipShieldComponent> owner, ref ComponentRemove remove)
    {
        var shield = owner.Comp;
        var shieldUid = owner.Owner;

        _pvsSys.RemoveGlobalOverride(shieldUid);

        // Clean up ShipShieldedComponent on the grid
        if (Exists(shield.Shielded) && TryComp<ShipShieldedComponent>(shield.Shielded, out _))
        {
            RemComp<ShipShieldedComponent>(shield.Shielded);
        }

        // Clean up emitter's stale reference
        if (shield.Source is { } sourceUid
            && Exists(sourceUid)
            && TryComp<ShipShieldEmitterComponent>(sourceUid, out var emitter)
            && emitter.Shield == shieldUid)
        {
            emitter.Shield = null;
            emitter.Shielded = null;
        }
    }

    private void OnShieldDeflected(EntityUid uid, ShipShieldEmitterComponent component, ShieldDeflectedEvent args)
    {
        if (TryComp<EmpOnTriggerComponent>(args.Deflected, out var emp))
        {
            component.Damage += Math.Clamp(emp.EnergyConsumption, 0f, MAX_EMP_DAMAGE);
            _trigger.Trigger(args.Deflected);
        }

        if (TryComp<ExplosiveComponent>(args.Deflected, out var exp))
        {
            component.Damage += exp.TotalIntensity;
        }

        if (TryComp<ProjectileComponent>(args.Deflected, out var proj))
        {
            component.Damage += (float) proj.Damage.GetTotal();
            proj.ProjectileSpent = true;
        }
        else if (TryComp<PhysicsComponent>(args.Deflected, out var phys))
        {
            component.Damage += phys.FixturesMass;
        }

        QueueDel(args.Deflected);
    }

    private void OnExamined(EntityUid uid, ShipShieldEmitterComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (component.Damage == 0f)
        {
            args.PushMarkup(Loc.GetString("shield-emitter-examine-undamaged"));
            return;
        }

        var additionalLoad = (float) Math.Clamp(Math.Pow(component.Damage, component.DamageExp), 0f, component.MaxDraw);
        var ratio = additionalLoad / component.BaseDraw;
        ratio = (float) Math.Ceiling(ratio * 100);

        args.PushMarkup(Loc.GetString("shield-emitter-examine-damaged", ("percent", ratio)));
    }

    private void AdjustEmitterLoad(EntityUid uid, ShipShieldEmitterComponent? emitter = null, ApcPowerReceiverComponent? receiver = null)
    {
        if (!Resolve(uid, ref emitter, ref receiver))
            return;

        /// Raise damage to the power of the growth exponent
        var additionalLoad = (float) Math.Clamp(Math.Pow(emitter.Damage, emitter.DamageExp), 0f, emitter.MaxDraw);

        receiver.Load = emitter.BaseDraw + additionalLoad;
    }
}
