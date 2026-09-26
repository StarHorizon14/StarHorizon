using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.GameTicking;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.ShiftStartEffect;

/// <summary>
/// Проигрывает эффект появления.
/// Не применяется к наблюдателям/призракам.
/// </summary>
public sealed class ShiftStartEffectSystem : EntitySystem
{
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly EntProtoId ShadowEchoProto = "ShiftStartShadowEcho";
    private static readonly Vector2 ZoomedIn = SharedContentEyeSystem.DefaultZoom / 2f;
    private static readonly TimeSpan EffectDuration = TimeSpan.FromSeconds(3);

    private const float EchoVerticalOffset = 0.3f;

    private readonly Dictionary<EntityUid, TimeSpan> _activeZoomEffects = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Silent)
            return;

        if (HasComp<GhostComponent>(ev.Mob))
            return;

        SpawnShadowEcho(ev.Mob);
        ApplyCameraZoom(ev.Mob);
    }

    private void SpawnShadowEcho(EntityUid mob)
    {
        var coords = Transform(mob).Coordinates.Offset(new Vector2(0, EchoVerticalOffset));
        Spawn(ShadowEchoProto, coords);
    }

    private void ApplyCameraZoom(EntityUid mob)
    {
        if (!TryComp<ContentEyeComponent>(mob, out var eye))
            return;

        _contentEye.SetZoom(mob, ZoomedIn, ignoreLimits: true, eye: eye);
        _activeZoomEffects[mob] = _timing.CurTime + EffectDuration;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_activeZoomEffects.Count == 0)
            return;

        var now = _timing.CurTime;
        List<EntityUid>? finished = null;

        foreach (var (uid, expiresAt) in _activeZoomEffects)
        {
            if (now < expiresAt)
                continue;

            finished ??= new List<EntityUid>();
            finished.Add(uid);
        }

        if (finished == null)
            return;

        foreach (var uid in finished)
        {
            _activeZoomEffects.Remove(uid);

            if (Deleted(uid) || !TryComp<ContentEyeComponent>(uid, out var eye))
                continue;

            _contentEye.SetZoom(uid, SharedContentEyeSystem.DefaultZoom, ignoreLimits: true, eye: eye);
        }
    }
}
