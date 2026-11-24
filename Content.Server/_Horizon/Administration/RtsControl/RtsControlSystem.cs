using System.Numerics;
using Content.Server.Administration.Logs;
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
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Administration.RtsControl;

/// <summary>
///     Lets admins order many NPCs to move to a point at once.
/// </summary>
public sealed class RtsControlSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminManager _admin = default!;
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

        var count = OrderMove(units, target);
        if (count == 0)
            return;

        _adminLog.Add(LogType.AdminCommands,
            LogImpact.Low,
            $"{ToPrettyString(args.SenderSession.AttachedEntity):player} ordered {count} creatures to move to {target}");
    }

    /// <summary>
    ///     Orders the given creatures to move to <paramref name="target"/>, spread in a formation around it.
    ///     No permission checks here, the caller is responsible for them.
    /// </summary>
    /// <returns>How many creatures accepted the order.</returns>
    public int OrderMove(IEnumerable<EntityUid> requested, EntityCoordinates target)
    {
        if (!target.IsValid(EntityManager))
            return 0;

        // Collect valid units first so the formation is dense.
        var units = new List<(EntityUid Uid, HTNComponent Htn)>();
        foreach (var uid in requested)
        {
            if (units.Count >= MaxUnits)
                break;

            if (TerminatingOrDeleted(uid))
                continue;

            if (!TryComp<HTNComponent>(uid, out var htn) || !HasComp<MobStateComponent>(uid))
                continue;

            if (_mobState.IsIncapacitated(uid))
                continue;

            // Never take control away from a player.
            if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
                continue;

            units.Add((uid, htn));
        }

        if (units.Count == 0)
            return 0;

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

        return units.Count;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toFinish = new List<EntityUid>();
        var query = EntityQueryEnumerator<RtsMoveOrderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var order, out var xform))
        {
            if (_mobState.IsIncapacitated(uid) ||
                !TryComp<NPCSteeringComponent>(uid, out var steering))
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
                toFinish.Add(uid);
                continue;
            }

            order.LastCheckPosition = xform.Coordinates;
            order.NextProgressCheck = _timing.CurTime + ProgressInterval;
        }

        foreach (var uid in toFinish)
        {
            RemComp<RtsMoveOrderComponent>(uid);
        }
    }

    private void OnOrderShutdown(Entity<RtsMoveOrderComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _steering.Unregister(ent);

        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent, htn), true, 1f);
    }
}
