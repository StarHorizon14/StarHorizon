using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Horizon._Fractions.AnCo.Nanites;

/// <summary>
/// Управляет зацикленным звуком метаболизма реагента: запускает его на первом тике,
/// продлевает при последующих тиках, и плавно затухает/останавливает,
/// когда реагент перестаёт метаболизироваться.
/// </summary>
public sealed class MetabolismSoundSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    /// <summary>
    /// Запас времени, добавляемый к ожидаемому тику метаболизма реагента,
    /// чтобы не начать затухание звука между двумя тиками.
    /// </summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Длительность плавного затухания громкости после окончания реагента.
    /// </summary>
    public static readonly TimeSpan FadeOutDuration = TimeSpan.FromSeconds(2);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<MetabolismSoundComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.FadingOut)
            {
                if (_timing.CurTime >= comp.ExpiresAt)
                    comp.FadingOut = true;
                else
                    continue;
            }

            comp.FadeGain -= frameTime / (float) FadeOutDuration.TotalSeconds;

            if (comp.FadeGain <= 0f)
            {
                _audio.Stop(comp.SoundEntity);
                RemComp<MetabolismSoundComponent>(uid);
                continue;
            }

            _audio.SetGain(comp.SoundEntity, comp.FadeGain);
        }
    }

    /// <summary>
    /// Запускает (если ещё не запущен) и продлевает зацикленный звук метаболизма на цели.
    /// </summary>
    public void Refresh(EntityUid uid, SoundSpecifier sound)
    {
        var comp = EnsureComp<MetabolismSoundComponent>(uid);

        if (comp.SoundEntity == null || comp.FadingOut)
        {
            if (comp.SoundEntity != null)
                _audio.Stop(comp.SoundEntity);

            var played = _audio.PlayEntity(sound, uid, uid, AudioParams.Default.WithLoop(true));
            comp.SoundEntity = played?.Entity;
            comp.Sound = sound;
            comp.FadingOut = false;
            comp.FadeGain = 1f;
        }

        comp.ExpiresAt = _timing.CurTime + GracePeriod;
    }
}
