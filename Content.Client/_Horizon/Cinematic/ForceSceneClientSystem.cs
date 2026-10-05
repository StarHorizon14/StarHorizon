using Content.Shared._Horizon.Cinematic;
using Robust.Client.State;
using Robust.Shared.Reflection;

namespace Content.Client._Horizon.Cinematic;

/// <summary>
/// Client-side half of the "forcescene" admin command: receives the targeted network event and
/// switches the local UI/game State, the same way the engine's local "scene" console command does.
/// </summary>
public sealed class ForceSceneClientSystem : EntitySystem
{
    [Dependency] private readonly IReflectionManager _reflection = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ForceSceneEvent>(OnForceScene);
    }

    private void OnForceScene(ForceSceneEvent ev)
    {
        foreach (var type in _reflection.GetAllChildren(typeof(State)))
        {
            if (type.FullName == null || !type.FullName.EndsWith(ev.SceneName))
                continue;

            _stateManager.RequestStateChange(type);
            return;
        }

        Logger.Warning($"forcescene: no scene class ending with '{ev.SceneName}' found.");
    }
}
