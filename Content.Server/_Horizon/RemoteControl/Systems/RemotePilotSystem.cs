using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared._Horizon.RemoteControl.Components;
using Content.Shared._Horizon.RemoteControl.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mind.Components;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.RemoteControl.Systems;

public sealed class RemotePilotSystem : SharedRemotePilotSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RemoteControlSystem _remoteControl = default!;

    private static readonly TimeSpan RangeCheckInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextRangeCheck;

    public override void Initialize()
    {
        base.Initialize();

        // Horizon: get the mind out before the mind system ghosts or loses it along with the deleted body
        SubscribeLocalEvent<UnderControlComponent, EntityTerminatingEvent>(OnControlledTerminating, before: [typeof(MindSystem)]);
        SubscribeLocalEvent<UnderControlComponent, MindRemovedMessage>(OnControlledMindRemoved);
        SubscribeLocalEvent<RemotePilotComponent, MindRemovedMessage>(OnPilotMindRemoved);
    }

    private void OnControlledTerminating(Entity<UnderControlComponent> ent, ref EntityTerminatingEvent args)
    {
        _remoteControl.ReturnToBody(ent);
    }

    /// <summary>
    /// The mind left the controlled entity some other way (ghosted, admin moved it...), the link is gone.
    /// </summary>
    private void OnControlledMindRemoved(Entity<UnderControlComponent> ent, ref MindRemovedMessage args)
    {
        if (!TerminatingOrDeleted(ent))
            RemCompDeferred<UnderControlComponent>(ent);
    }

    /// <summary>
    /// A remote pilot is only a puppet for the mind, like a ghost: once the mind leaves, it goes away.
    /// </summary>
    private void OnPilotMindRemoved(Entity<RemotePilotComponent> ent, ref MindRemovedMessage args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        // If you just delete it, the mech won't update its appearance, so it will remain closed
        if (TryComp<MechPilotComponent>(ent, out var mechPilot))
        {
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, 0f, new MechExitEvent(), mechPilot.Mech, target: mechPilot.Mech)
            {
                Broadcast = false,
                Hidden = true,
            });
        }

        QueueDel(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRangeCheck)
            return;

        _nextRangeCheck = _timing.CurTime + RangeCheckInterval;

        var query = EntityQueryEnumerator<UnderControlComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(comp.OriginalBody))
                continue;

            // A remote pilot sits inside the mech, so its map position is the mech's
            var hostPos = _transform.GetMapCoordinates(uid);
            var bodyPos = _transform.GetMapCoordinates(comp.OriginalBody);

            var distance = hostPos.MapId == bodyPos.MapId
                ? (hostPos.Position - bodyPos.Position).Length()
                : float.PositiveInfinity;

            if (distance > comp.MaxRange)
            {
                _popup.PopupEntity(Loc.GetString("remote-control-signal-lost"), uid, uid);
                _remoteControl.ReturnToBody(uid);
                continue;
            }

            var loss = comp.MaxRange > comp.NoiseStartRange
                ? Math.Clamp((distance - comp.NoiseStartRange) / (comp.MaxRange - comp.NoiseStartRange), 0f, 1f)
                : 0f;

            // Only resend when it visibly changes
            if (MathF.Abs(loss - comp.SignalLoss) < 0.02f)
                continue;

            comp.SignalLoss = loss;
            Dirty(uid, comp);
        }
    }
}
