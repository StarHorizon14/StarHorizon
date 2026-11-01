using System.Numerics;
using Content.Server._Mono.NPC.HTN.Operators;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Mono.Shuttles;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Mono.Shuttles;

public sealed partial class ShuttleConsoleAutopilotSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RadarConsoleSystem _radarConsole = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // Horizon: consoles whose autopilot NPC is awake. Console NPCs sleep while idle so they
    // don't eat into the global NPC update budget.
    private readonly HashSet<EntityUid> _awakeConsoles = new();
    private readonly List<EntityUid> _toSleep = new();
    private float _sleepCheckAccumulator;
    private const float SleepCheckInterval = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleConsoleComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(HTNSystem) });
        SubscribeLocalEvent<ShuttleConsoleComponent, ShuttleConsoleAutopilotPositionMessage>(OnAutopilotMessage);
        SubscribeLocalEvent<ShuttleConsoleComponent, SteeringDoneEvent>(OnSteeringDone);
    }

    private void OnMapInit(Entity<ShuttleConsoleComponent> ent, ref MapInitEvent args)
    {
        // HTN wakes every NPC on MapInit; the autopilot only needs to run when given a destination.
        if (HasComp<HTNComponent>(ent))
            _npc.SleepNPC(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _sleepCheckAccumulator += frameTime;
        if (_sleepCheckAccumulator < SleepCheckInterval)
            return;
        _sleepCheckAccumulator = 0f;

        // Put consoles back to sleep once their autopilot run is over. Deleted consoles get dropped here too.
        _toSleep.Clear();
        foreach (var uid in _awakeConsoles)
        {
            if (!TryComp<HTNComponent>(uid, out var htn) || !TryComp<ShuttleConsoleComponent>(uid, out var console))
            {
                _toSleep.Add(uid);
                continue;
            }

            if (htn.Plan == null && htn.PlanningJob == null && !htn.Blackboard.ContainsKey(console.AutopilotTargetKey))
                _toSleep.Add(uid);
        }

        foreach (var uid in _toSleep)
        {
            _awakeConsoles.Remove(uid);
            _npc.SleepNPC(uid);
        }
    }

    private void OnAutopilotMessage(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleAutopilotPositionMessage args)
    {
        // Horizon: broken consoles can't fly
        if (ent.Comp.Broken || !TryComp<HTNComponent>(ent, out var htn))
            return;

        // Autopilot only works within the shuttle's current map, on an enabled shuttle.
        var xform = Transform(ent);
        if (xform.MapID != args.Coordinates.MapId
            || !TryComp<ShuttleComponent>(xform.GridUid, out var shuttle)
            || !shuttle.Enabled)
            return;

        var blackboard = htn.Blackboard;
        blackboard.SetValue(ent.Comp.AutopilotTargetKey, _transform.ToCoordinates(args.Coordinates));
        blackboard.SetValue(ent.Comp.AutopilotRotationKey, args.Angle + MathF.PI);

        _npc.WakeNPC(ent, htn);
        _awakeConsoles.Add(ent);

        // Show destination on the Nav radar via the existing Frontier target marker.
        SetAutopilotNavTarget(ent, args.Coordinates.Position);
    }

    private void OnSteeringDone(Entity<ShuttleConsoleComponent> ent, ref SteeringDoneEvent args)
    {
        _audio.PlayPvs(ent.Comp.AutopilotDoneSound, ent);
        // Horizon: say so when the autopilot stopped because the path is blocked
        var message = args.Blocked ? "shuttle-console-autopilot-popup-blocked" : "shuttle-console-autopilot-popup-done";
        _popup.PopupEntity(Loc.GetString(message), ent, args.Blocked ? PopupType.MediumCaution : PopupType.Medium);
        HideAutopilotNavTarget(ent);
    }

    private void SetAutopilotNavTarget(EntityUid console, Vector2 mapPosition)
    {
        if (!TryComp<RadarConsoleComponent>(console, out var radar))
            return;

        _radarConsole.SetTarget((console, radar), NetEntity.Invalid, mapPosition);
        _radarConsole.SetHideTarget((console, radar), false);

        if (Transform(console).GridUid is { } gridUid)
            _shuttleConsole.RefreshShuttleConsoles(gridUid);
    }

    private void HideAutopilotNavTarget(EntityUid console)
    {
        if (!TryComp<RadarConsoleComponent>(console, out var radar))
            return;

        _radarConsole.SetHideTarget((console, radar), true);

        if (Transform(console).GridUid is { } gridUid)
            _shuttleConsole.RefreshShuttleConsoles(gridUid);
    }
}
