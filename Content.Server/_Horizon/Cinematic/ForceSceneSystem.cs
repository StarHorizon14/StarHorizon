using Content.Shared._Horizon.Cinematic;
using Robust.Shared.Player;

namespace Content.Server._Horizon.Cinematic;

public sealed class ForceSceneSystem : EntitySystem
{
    public void ForceScene(ICommonSession session, string sceneName)
    {
        RaiseNetworkEvent(new ForceSceneEvent { SceneName = sceneName }, session);
    }
}
