using Robust.Shared.Audio;

namespace Content.Shared._Horizon._Fractions.AnCo.Nanites;

/// <summary>
/// Держит зацикленный звук, привязанный к метаболизму реагента.
/// Навешивается через EntityEffect на каждый тик метаболизма и продлевается,
/// пока реагент есть в крови; когда реагент заканчивается — звук плавно затухает и останавливается.
/// Звук проигрывается локально: слышен только самой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class MetabolismSoundComponent : Component
{
    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Сущность, проигрывающая зацикленный звук.
    /// </summary>
    [DataField]
    public EntityUid? SoundEntity;

    /// <summary>
    /// Момент времени, после которого звук начинает затухать, если не был продлён.
    /// </summary>
    [DataField]
    public TimeSpan ExpiresAt;

    /// <summary>
    /// Если true — звук уже затухает и SoundEntity будет остановлена/удалена по завершении fade.
    /// </summary>
    [DataField]
    public bool FadingOut;

    /// <summary>
    /// Текущий линейный коэффициент громкости (1 — полная громкость, 0 — тишина).
    /// </summary>
    [DataField]
    public float FadeGain = 1f;
}
