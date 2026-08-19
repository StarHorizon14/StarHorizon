using Content.Shared._Horizon.Audio;
using Robust.Shared.Player;

namespace Content.Server._Horizon.Audio;

/// <summary>
/// Lets admin commands silence all currently playing audio for a specific client.
/// </summary>
public sealed class MuteSoundSystem : EntitySystem
{
    public void Mute(ICommonSession session, float fadeSeconds, bool onlyGlobalSound = false)
    {
        RaiseNetworkEvent(new MuteClientEvent(fadeSeconds, onlyGlobalSound), session);
    }
}
