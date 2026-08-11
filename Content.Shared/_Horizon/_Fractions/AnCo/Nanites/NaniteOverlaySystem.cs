using Robust.Shared.Timing;

namespace Content.Shared._Horizon._Fractions.AnCo.Nanites;

/// <summary>
/// Снимает <see cref="NaniteOverlayComponent"/>, если он не был продлён очередным
/// тиком метаболизма реагента (т.е. реагент закончился в крови существа).
/// </summary>
public sealed class NaniteOverlaySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Запас времени, добавляемый к ожидаемому тику метаболизма реагента,
    /// чтобы не снять оверлей между двумя тиками.
    /// </summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(2);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<NaniteOverlayComponent>();
        while (query.MoveNext(out var uid, out var overlay))
        {
            if (_timing.CurTime < overlay.ExpiresAt)
                continue;

            RemComp<NaniteOverlayComponent>(uid);
        }
    }

    public void Refresh(EntityUid uid)
    {
        var overlay = EnsureComp<NaniteOverlayComponent>(uid);
        overlay.ExpiresAt = _timing.CurTime + NaniteOverlaySystem.GracePeriod;
        Dirty(uid, overlay);
    }
}
