using System.Numerics;
using Content.Shared._Horizon.PlayerLasso.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Misc;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;

namespace Content.Shared._Horizon.PlayerLasso.Systems;

/// <summary>
/// Handles <see cref="PlayerLassoGunComponent"/>: the gun shoots a rope end, and the rope ties to the first player
/// that end hits. Anything else, or flying past the rope's length, pulls the rope back into the gun.
/// While the rope is out or tied the gun can't shoot; using it in hand drops the rope.
/// </summary>
public sealed class PlayerLassoGunSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PlayerLassoSystem _lasso = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerLassoGunComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<PlayerLassoGunComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<PlayerLassoGunComponent, UseInHandEvent>(OnUseInHand);

        SubscribeLocalEvent<PlayerLassoProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<PlayerLassoProjectileComponent, ComponentShutdown>(OnProjectileShutdown);
    }

    private void OnShotAttempted(Entity<PlayerLassoGunComponent> ent, ref ShotAttemptedEvent args)
    {
        // One rope per gun: nothing to shoot while it is flying or tied to someone.
        if (ent.Comp.Projectile != null ||
            !TryComp<PlayerLassoComponent>(ent, out var lasso) ||
            lasso.Anchor != null)
        {
            args.Cancel();
        }
    }

    private void OnGunShot(Entity<PlayerLassoGunComponent> ent, ref GunShotEvent args)
    {
        // The client only spawns short-lived local copies of shots, the real rope end comes from the server.
        if (_net.IsClient || !TryComp<PlayerLassoComponent>(ent, out var lasso))
            return;

        foreach (var (shot, _) in args.Ammo)
        {
            if (!TryComp<PlayerLassoProjectileComponent>(shot, out var projectile))
                continue;

            projectile.Gun = ent;
            ent.Comp.Projectile = shot;
            Dirty(ent);

            // Draw the rope between the flying end and the gun.
            var visuals = EnsureComp<JointVisualsComponent>(shot.Value);
            visuals.Sprite = lasso.RopeSprite;
            visuals.Target = GetNetEntity(ent);
            Dirty(shot.Value, visuals);
            break;
        }
    }

    private void OnUseInHand(Entity<PlayerLassoGunComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<PlayerLassoComponent>(ent, out var lasso))
            return;

        if (lasso.Anchor != null)
        {
            _lasso.Release((ent, lasso));
            args.Handled = true;
            return;
        }

        if (ent.Comp.Projectile is { } projectile)
        {
            if (_net.IsServer)
                QueueDel(projectile);

            args.Handled = true;
        }
    }

    private void OnProjectileHit(Entity<PlayerLassoProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (ent.Comp.Gun is not { } gun || !TryComp<PlayerLassoComponent>(gun, out var lasso))
            return;

        // The rope only catches players; it bounces off everything else and comes back.
        if (_mobState.IsAlive(args.Target))
            _lasso.TryTieVictimRemotely((gun, lasso), args.Target, args.Shooter);

        if (_net.IsServer)
            QueueDel(ent);
    }

    private void OnProjectileShutdown(Entity<PlayerLassoProjectileComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Gun is not { } gun ||
            !TryComp<PlayerLassoGunComponent>(gun, out var gunComp) ||
            gunComp.Projectile != ent.Owner)
        {
            return;
        }

        gunComp.Projectile = null;
        Dirty(gun, gunComp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<PlayerLassoGunComponent, PlayerLassoComponent>();
        while (query.MoveNext(out var uid, out var gun, out var lasso))
        {
            _appearance.SetData(uid, SharedTetherGunSystem.TetherVisualsStatus.Key, gun.Projectile == null && lasso.Anchor == null);

            if (gun.Projectile is not { } projectile || TerminatingOrDeleted(projectile))
                continue;

            // The rope ran out: pull the end back into the gun.
            var from = _transform.GetMapCoordinates(_lasso.GetItemEnd(uid));
            var to = _transform.GetMapCoordinates(projectile);

            if (from.MapId != to.MapId || Vector2.Distance(from.Position, to.Position) > lasso.MaxDistance)
                QueueDel(projectile);
        }
    }
}
