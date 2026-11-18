using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared._Horizon.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.NPC.Systems;

// Horizon-Start
public sealed partial class NPCSystem
{
    [Dependency] private IPlayerManager _horizonPlayers = default!;
    [Dependency] private SharedTransformSystem _horizonTransform = default!;

    private const float HorizonDistanceCheckInterval = 2f;

    private bool _horizonPauseEnabled;
    private float _horizonPauseDistance;
    private float _horizonPauseTimer;

    private readonly HashSet<EntityUid> _horizonDistanceSlept = new();
    private readonly List<(MapId Map, Vector2 Pos)> _horizonPlayerPositions = new();
    private readonly List<EntityUid> _horizonScratch = new();

    private void InitializeHorizonDistancePause()
    {
        Subs.CVar(_configurationManager, HorizonCCVars.NpcPauseWhenNoPlayersInRange, OnHorizonPauseToggled, true);
        Subs.CVar(_configurationManager, HorizonCCVars.NpcPlayerPauseDistance, v => _horizonPauseDistance = v, true);
    }

    private void OnHorizonPauseToggled(bool enabled)
    {
        _horizonPauseEnabled = enabled;
        if (enabled)
            return;

        foreach (var uid in _horizonDistanceSlept)
        {
            if (TryComp<HTNComponent>(uid, out var htn) && !_mobState.IsIncapacitated(uid))
                WakeNPC(uid, htn);
        }

        _horizonDistanceSlept.Clear();
    }

    private void UpdateHorizonDistancePause(float frameTime)
    {
        if (!_horizonPauseEnabled)
            return;

        _horizonPauseTimer += frameTime;
        if (_horizonPauseTimer < HorizonDistanceCheckInterval)
            return;
        _horizonPauseTimer = 0f;

        _horizonPlayerPositions.Clear();
        foreach (var session in _horizonPlayers.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } ent
                || HasComp<GhostComponent>(ent)
                || TryComp<MobStateComponent>(ent, out var state) && state.CurrentState != MobState.Alive
                || !TryComp(ent, out TransformComponent? xform)
                || xform.MapID == MapId.Nullspace)
                continue;

            _horizonPlayerPositions.Add((xform.MapID, _horizonTransform.GetWorldPosition(xform)));
        }

        _horizonScratch.Clear();
        var active = EntityQueryEnumerator<ActiveNPCComponent, HTNComponent, TransformComponent>();
        while (active.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (HasComp<ActorComponent>(uid)
                || TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
                continue;

            if (!HorizonPlayerInRange(xform))
                _horizonScratch.Add(uid);
        }

        foreach (var uid in _horizonScratch)
        {
            SleepNPC(uid);
            _horizonDistanceSlept.Add(uid);
        }

        _horizonScratch.Clear();
        foreach (var uid in _horizonDistanceSlept)
        {
            if (!TryComp<HTNComponent>(uid, out _) || !TryComp(uid, out TransformComponent? xform))
            {
                _horizonScratch.Add(uid);
                continue;
            }

            if (HorizonPlayerInRange(xform))
                _horizonScratch.Add(uid);
        }

        foreach (var uid in _horizonScratch)
        {
            _horizonDistanceSlept.Remove(uid);

            if (!TryComp<HTNComponent>(uid, out var htn))
                continue;

            if (_mobState.IsIncapacitated(uid)
                || HasComp<ActorComponent>(uid)
                || TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
                continue;

            WakeNPC(uid, htn);
        }
    }

    private bool HorizonPlayerInRange(TransformComponent xform)
    {
        var map = xform.MapID;
        var pos = _horizonTransform.GetWorldPosition(xform);
        var rangeSq = _horizonPauseDistance * _horizonPauseDistance;

        foreach (var (playerMap, playerPos) in _horizonPlayerPositions)
        {
            if (playerMap == map && Vector2.DistanceSquared(playerPos, pos) <= rangeSq)
                return true;
        }

        return false;
    }
}
// Horizon-End
