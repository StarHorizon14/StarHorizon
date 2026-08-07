using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Audio;

/// <summary>
/// Sent by an admin command to a specific client to silence all currently playing audio.
/// </summary>
[Serializable, NetSerializable]
public sealed class MuteClientEvent : EntityEventArgs
{
    /// <summary>
    /// How long the audio should take to fade out, in seconds. 0 mutes instantly.
    /// </summary>
    public float FadeSeconds;

    /// <summary>
    /// If true, only silences audio played via the admin "playglobalsound" command, leaving everything else untouched.
    /// </summary>
    public bool OnlyGlobalSound;

    public MuteClientEvent(float fadeSeconds, bool onlyGlobalSound = false)
    {
        FadeSeconds = fadeSeconds;
        OnlyGlobalSound = onlyGlobalSound;
    }
}
