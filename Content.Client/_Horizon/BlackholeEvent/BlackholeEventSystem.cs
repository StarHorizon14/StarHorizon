using Content.Client.Gameplay;
using Content.Shared._Horizon.BlackholeEvent;
using Robust.Client.State;

namespace Content.Client._Horizon.BlackholeEvent;

public sealed class BlackholeEventSystem : EntitySystem
{
    [Dependency] private readonly IStateManager _stateManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<BlackholeEventStateMessage>(OnState);
    }

    private void OnState(BlackholeEventStateMessage message)
    {
        if (message.Active)
            _stateManager.RequestStateChange<BlackholeState>();
        else if (_stateManager.CurrentState is BlackholeState)
            _stateManager.RequestStateChange<GameplayState>();
    }
}
