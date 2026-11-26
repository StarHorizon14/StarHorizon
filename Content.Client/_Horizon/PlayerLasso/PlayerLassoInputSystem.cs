using Content.Client.Hands.Systems;
using Content.Shared._Horizon.PlayerLasso.Components;
using Content.Shared._Horizon.PlayerLasso.Systems;
using Content.Shared.CombatMode;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.PlayerLasso;

/// <summary>
/// Sends reel/loosen requests for a tied <see cref="PlayerLassoComponent"/>: UseSecondary reels
/// the rope in, Use lets it out, while the holder is in combat mode.
/// </summary>
public sealed class PlayerLassoInputSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InputSystem _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var local = _player.LocalEntity;
        var activeItem = _hands.GetActiveHandEntity();

        if (!TryComp<PlayerLassoComponent>(activeItem, out var lasso) || lasso.Anchor == null)
            return;

        var inCombatMode = TryComp<CombatModeComponent>(local, out var combatMode) && combatMode.IsInCombatMode;
        var reelDown = inCombatMode && _input.CmdStates.GetState(EngineKeyFunctions.UseSecondary) == BoundKeyState.Down;
        var looseDown = inCombatMode && _input.CmdStates.GetState(EngineKeyFunctions.Use) == BoundKeyState.Down;

        var state = reelDown ? LassoPullState.Reeling : looseDown ? LassoPullState.Loosening : LassoPullState.None;

        if (lasso.PullState == state)
            return;

        RaisePredictiveEvent(new RequestLassoReelMessage(state));
    }
}
