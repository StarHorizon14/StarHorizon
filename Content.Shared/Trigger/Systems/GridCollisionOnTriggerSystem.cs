using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle;
using Content.Shared.Damage;
using Content.Shared.Gravity;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Shared.Trigger.Systems;

public sealed class GridCollisionOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;

    private const float MinThrowVelocity = 0.07f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridCollisionOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<GridCollisionOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid)
            return;

        var direction = _random.NextAngle().ToVec() * ent.Comp.ThrowStrength;
        ThrowEntitiesOnGrid(ent, gridUid, direction);

        if (ent.Comp.AffectMobs)
            _gravity.StartGridShake(gridUid);

        args.Handled = true;
    }

    private void ThrowEntitiesOnGrid(Entity<GridCollisionOnTriggerComponent> ent, EntityUid gridUid, Vector2 direction)
    {
        var physicsQuery = GetEntityQuery<PhysicsComponent>();
        var movedByPressureQuery = GetEntityQuery<MovedByPressureComponent>();
        var mobStateQuery = GetEntityQuery<MobStateComponent>();

        var toAffect = new List<(EntityUid Uid, PhysicsComponent Physics)>();
        var gridXform = Transform(gridUid);
        var childEnumerator = gridXform.ChildEnumerator;

        while (childEnumerator.MoveNext(out var uid))
        {
            if (!physicsQuery.TryGetComponent(uid, out var physics) || (physics.BodyType & BodyType.Static) != 0)
                continue;

            if (!ent.Comp.AffectMobs && mobStateQuery.HasComponent(uid))
                continue;

            if (_buckle.IsBuckled(uid))
                continue;

            if (movedByPressureQuery.TryGetComponent(uid, out var moved) && !moved.Enabled)
                continue;

            toAffect.Add((uid, physics));
        }

        var damageableQuery = GetEntityQuery<DamageableComponent>();
        var projQuery = GetEntityQuery<ProjectileComponent>();
        var minsq = MinThrowVelocity * MinThrowVelocity;

        foreach (var (uid, physics) in toAffect)
        {
            if (direction.LengthSquared() > minsq)
                _throwing.TryThrow(uid, direction, physics, Transform(uid), projQuery, direction.Length(), playSound: false);
            else
                _physics.ApplyLinearImpulse(uid, direction * physics.Mass, body: physics);

            if (!damageableQuery.TryGetComponent(uid, out var damageable))
                continue;

            _damageable.TryChangeDamage(uid, ent.Comp.Damage, ent.Comp.IgnoreResistances, origin: ent.Owner, damageable: damageable);

            if (ent.Comp.KnockdownTime > TimeSpan.Zero)
                _stun.TryKnockdown(uid, ent.Comp.KnockdownTime, true);
        }
    }
}
