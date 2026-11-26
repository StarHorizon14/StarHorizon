using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.NPC.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._Horizon.Administration.RtsControl;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Administration.RtsControl;

/// <summary>
///     Lets admins order many NPCs to move to a point at once.
/// </summary>
/// <summary>
///     Outcome of an RTS move order: how many creatures took it and why the others were skipped.
/// </summary>
public record struct RtsOrderResult(int Requested, int Accepted, int NoAi, int Incapacitated, int PlayerControlled, int OverLimit, bool InvalidTarget)
{
    public int Skipped => Requested - Accepted;
}

public sealed class RtsControlSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminManager _admin = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private const int MaxUnits = 100;
    private const float Spacing = 1f;
    private const float ArriveRange = 0.5f;
    private const float FallbackRange = 2f;
    // Orders have no total time limit. Instead we give up when a creature stops making progress.
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(8);
    private const float MinProgress = 0.5f;
    private const int MaxStuckStrikes = 3;

    // Within this distance of an attack target the normal AI is trusted to take over (its vision is ~10 tiles).
    private const float HandOffRange = 6f;
    private static readonly TimeSpan RetargetInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RtsMoveOrderEvent>(OnMoveOrder);
        SubscribeLocalEvent<RtsMoveOrderComponent, ComponentShutdown>(OnOrderShutdown);
    }

    private void OnMoveOrder(RtsMoveOrderEvent ev, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
            return;

        var target = GetCoordinates(ev.Target);
        var units = new List<EntityUid>(ev.Units.Count);
        foreach (var netUnit in ev.Units)
        {
            if (TryGetEntity(netUnit, out var uid))
                units.Add(uid.Value);
        }

        RtsOrderResult result;
        if (ev.AttackTarget is { } netTarget &&
            TryGetEntity(netTarget, out var attackTarget) &&
            HasComp<MobStateComponent>(attackTarget))
        {
            result = OrderAttack(units, attackTarget.Value);
        }
        else
        {
            result = OrderMove(units, target);
        }

        if (result.InvalidTarget)
        {
            _chat.DispatchServerMessage(args.SenderSession, Loc.GetString("rts-control-invalid-target"));
            return;
        }

        _chat.DispatchServerMessage(args.SenderSession, Loc.GetString("rts-control-result",
            ("accepted", result.Accepted),
            ("requested", result.Requested),
            ("noAi", result.NoAi),
            ("incapacitated", result.Incapacitated),
            ("player", result.PlayerControlled),
            ("limit", result.OverLimit)));

        if (result.Accepted == 0)
            return;

        _adminLog.Add(LogType.AdminCommands,
            LogImpact.Low,
            $"{ToPrettyString(args.SenderSession.AttachedEntity):player} ordered {result.Accepted} creatures to move to {target}");
    }

    /// <summary>
    ///     Orders the given creatures to move to <paramref name="target"/>, spread in a formation around it.
    ///     No permission checks here, the caller is responsible for them.
    /// </summary>
    /// <returns>How many creatures accepted the order and why the others did not.</returns>
    public RtsOrderResult OrderMove(IEnumerable<EntityUid> requested, EntityCoordinates target)
    {
        var result = new RtsOrderResult();

        if (!target.IsValid(EntityManager))
        {
            result.InvalidTarget = true;
            return result;
        }

        var units = CollectUnits(requested, ref result);
        result.Accepted = units.Count;
        if (units.Count == 0)
            return result;

        var columns = (int) MathF.Ceiling(MathF.Sqrt(units.Count));
        var rows = (int) MathF.Ceiling(units.Count / (float) columns);

        for (var i = 0; i < units.Count; i++)
        {
            var (uid, htn) = units[i];

            var offset = new Vector2(
                (i % columns - (columns - 1) / 2f) * Spacing,
                (i / columns - (rows - 1) / 2f) * Spacing);

            var coords = target.Offset(offset);

            var order = EnsureComp<RtsMoveOrderComponent>(uid);
            ClearAttack(uid, order);
            order.Target = coords;
            order.CenterTarget = target;
            order.FellBack = false;
            order.NextProgressCheck = _timing.CurTime + ProgressInterval;
            order.LastCheckPosition = Transform(uid).Coordinates;

            _htn.SetHTNEnabled((uid, htn), false);
            _npc.WakeNPC(uid, htn); // Steering only runs for active NPCs.

            var steering = _steering.Register(uid, coords);
            steering.Range = ArriveRange;
            steering.ArriveOnLineOfSight = false;

            // Admin orders let any creature open, pry and climb its way through obstacles (doors etc).
            steering.Flags |= PathFlags.Interact | PathFlags.Prying | PathFlags.Climbing;
        }

        return result;
    }

    /// <summary>
    ///     Orders the given creatures to attack <paramref name="target"/>. They walk towards it and, once it is in
    ///     sight, their normal AI (melee, guns) takes over and fights it.
    ///     No permission checks here, the caller is responsible for them.
    /// </summary>
    public RtsOrderResult OrderAttack(IEnumerable<EntityUid> requested, EntityUid target)
    {
        var result = new RtsOrderResult();

        if (TerminatingOrDeleted(target) || !HasComp<MobStateComponent>(target))
        {
            result.InvalidTarget = true;
            return result;
        }

        var units = CollectUnits(requested, ref result);
        result.Accepted = units.Count;
        var targetCoords = Transform(target).Coordinates;

        foreach (var (uid, htn) in units)
        {
            if (uid == target)
            {
                result.Accepted--;
                continue;
            }

            var order = EnsureComp<RtsMoveOrderComponent>(uid);
            ClearAttack(uid, order);
            order.AttackTarget = target;
            order.Target = targetCoords;
            order.CenterTarget = targetCoords;
            order.FellBack = true; // No formation slots when attacking, nothing to fall back to.
            order.NextRetarget = TimeSpan.Zero;
            order.StuckStrikes = 0;
            order.NextProgressCheck = _timing.CurTime + ProgressInterval;
            order.LastCheckPosition = Transform(uid).Coordinates;

            // The target is now an enemy no matter what faction it belongs to.
            _faction.AggroEntity(uid, target);

            // Update() decides every tick whether to walk (far) or leave the fight to the AI (close).
            _htn.SetHTNEnabled((uid, htn), false);
            _npc.WakeNPC(uid, htn);
        }

        return result;
    }

    private void ClearAttack(EntityUid uid, RtsMoveOrderComponent order)
    {
        if (order.AttackTarget is { } old && !TerminatingOrDeleted(old))
            _faction.DeAggroEntity(uid, old);

        order.AttackTarget = null;
    }

    private List<(EntityUid Uid, HTNComponent Htn)> CollectUnits(IEnumerable<EntityUid> requested, ref RtsOrderResult result)
    {
        var units = new List<(EntityUid Uid, HTNComponent Htn)>();
        foreach (var uid in requested)
        {
            result.Requested++;

            if (units.Count >= MaxUnits)
            {
                result.OverLimit++;
                continue;
            }

            if (TerminatingOrDeleted(uid))
                continue;

            if (!TryComp<HTNComponent>(uid, out var htn) || !HasComp<MobStateComponent>(uid))
            {
                result.NoAi++;
                continue;
            }

            if (_mobState.IsIncapacitated(uid))
            {
                result.Incapacitated++;
                continue;
            }

            // Never take control away from a player.
            if (HasComp<ActorComponent>(uid) || TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
            {
                result.PlayerControlled++;
                continue;
            }

            units.Add((uid, htn));
        }

        return units;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toFinish = new List<EntityUid>();
        var query = EntityQueryEnumerator<RtsMoveOrderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var order, out var xform))
        {
            if (_mobState.IsIncapacitated(uid))
            {
                toFinish.Add(uid);
                continue;
            }

            if (order.AttackTarget is { } attackTarget)
            {
                var state = UpdateAttack(uid, order, xform, attackTarget);
                if (state == AttackState.Finished)
                    toFinish.Add(uid);

                // Close enough: the normal AI is in charge (and its own steering is none of our business).
                if (state != AttackState.Walking)
                    continue;
            }

            if (!TryComp<NPCSteeringComponent>(uid, out var steering))
            {
                toFinish.Add(uid);
                continue;
            }

            // The formation slot may be inside a wall or otherwise unreachable:
            // retry once towards the exact clicked point instead of giving up.
            if (steering.Status == SteeringStatus.NoPath && !order.FellBack)
            {
                order.FellBack = true;
                order.Target = order.CenterTarget;
                steering.Status = SteeringStatus.Moving;
                steering.FailedPathCount = 0;
                steering.CurrentPath.Clear();
                steering.Coordinates = order.CenterTarget;
                steering.Range = FallbackRange;
                order.NextProgressCheck = _timing.CurTime + ProgressInterval;
                order.LastCheckPosition = xform.Coordinates;
                continue;
            }

            if (steering.Status != SteeringStatus.Moving)
            {
                toFinish.Add(uid);
                continue;
            }

            if (_timing.CurTime < order.NextProgressCheck)
                continue;

            // Stuck detection: if the creature barely moved since the last check, give up.
            if (!order.LastCheckPosition.TryDistance(EntityManager, xform.Coordinates, out var moved) ||
                moved < MinProgress)
            {
                // Crowded doorways and jams are common with many units: retry the path a few times before giving up.
                if (++order.StuckStrikes >= MaxStuckStrikes)
                {
                    toFinish.Add(uid);
                    continue;
                }

                steering.FailedPathCount = 0;
                steering.CurrentPath.Clear();
                steering.Coordinates = EntityCoordinates.Invalid;
                _steering.Register(uid, order.Target, steering);
                order.NextProgressCheck = _timing.CurTime + ProgressInterval;
                order.LastCheckPosition = xform.Coordinates;
                continue;
            }

            order.StuckStrikes = 0;
            order.LastCheckPosition = xform.Coordinates;
            order.NextProgressCheck = _timing.CurTime + ProgressInterval;
        }

        foreach (var uid in toFinish)
        {
            RemComp<RtsMoveOrderComponent>(uid);
        }
    }

    /// <summary>
    ///     Walks towards the attack target while it is far away, hands over to the normal AI when it is close.
    /// </summary>
    private enum AttackState
    {
        /// <summary>The target is gone or dead, the order is over.</summary>
        Finished,

        /// <summary>The target is close, the normal AI is fighting it.</summary>
        HandedOver,

        /// <summary>The target is far, we are steering the creature towards it.</summary>
        Walking,
    }

    private AttackState UpdateAttack(EntityUid uid, RtsMoveOrderComponent order, TransformComponent xform, EntityUid target)
    {
        if (TerminatingOrDeleted(target) ||
            !TryComp<MobStateComponent>(target, out var targetState) ||
            _mobState.IsDead(target, targetState))
        {
            return AttackState.Finished;
        }

        var targetXform = Transform(target);
        if (targetXform.MapID != xform.MapID || !TryComp<HTNComponent>(uid, out var htn))
            return AttackState.Finished;

        var distance = (targetXform.WorldPosition - xform.WorldPosition).Length();

        if (distance <= HandOffRange)
        {
            // Close enough for the AI to see the target: let it fight.
            if (!htn.Enabled)
            {
                _steering.Unregister(uid);
                _htn.SetHTNEnabled((uid, htn), true);
            }

            order.NextProgressCheck = _timing.CurTime + ProgressInterval;
            order.LastCheckPosition = xform.Coordinates;
            order.StuckStrikes = 0;
            return AttackState.HandedOver;
        }

        // Too far: take control back and keep walking towards the (possibly moving) target.
        if (htn.Enabled)
        {
            _htn.SetHTNEnabled((uid, htn), false);
            _npc.WakeNPC(uid, htn);
            order.NextRetarget = TimeSpan.Zero;
        }

        if (_timing.CurTime >= order.NextRetarget)
        {
            order.NextRetarget = _timing.CurTime + RetargetInterval;
            order.Target = targetXform.Coordinates;

            var steering = _steering.Register(uid, order.Target);
            steering.Range = HandOffRange - 1f;
            steering.ArriveOnLineOfSight = false;
            steering.Flags |= PathFlags.Interact | PathFlags.Prying | PathFlags.Climbing;
        }

        return AttackState.Walking;
    }

    private void OnOrderShutdown(Entity<RtsMoveOrderComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (ent.Comp.AttackTarget is { } attackTarget && !TerminatingOrDeleted(attackTarget))
            _faction.DeAggroEntity(ent.Owner, attackTarget);

        _steering.Unregister(ent);

        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent, htn), true, 1f);
    }
}
