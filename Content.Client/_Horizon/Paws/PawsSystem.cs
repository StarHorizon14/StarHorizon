using Content.Shared._Horizon.Paws;
using Content.Shared.Hands;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;

namespace Content.Client._Horizon.Paws;

/// <summary>
/// Клиентская система для отображения лап вместо предметов в руках.
/// Использует HeldVisualsUpdatedEvent для замены спрайтов после ItemSystem.
/// </summary>
public sealed class PawsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Подписываемся на событие ПОСЛЕ того, как ItemSystem добавил слои
        // Событие вызывается на предмете, а не на владельце!
        SubscribeLocalEvent<HeldVisualsUpdatedEvent>(OnHeldVisualsUpdated);
    }

    private void OnHeldVisualsUpdated(HeldVisualsUpdatedEvent args)
    {
        // args.User - это владелец предмета (кот с лапами)
        // Проверяем, есть ли у владельца компонент лап
        if (!TryComp<PawsComponent>(args.User, out var paws))
            return;

        if (!TryComp<SpriteComponent>(args.User, out var sprite))
            return;

        // Для каждого добавленного слоя пытаемся найти вариант с лапами
        foreach (var layerKey in args.RevealedLayers)
        {
            if (!_sprite.LayerMapTryGet((args.User, sprite), layerKey, out var layerIndex, false))
                continue;

            // Получаем текущее состояние слоя
            var currentState = _sprite.LayerGetRsiState((args.User, sprite), layerIndex);
            if (string.IsNullOrEmpty(currentState.Name))
                continue;

            // Формируем имя состояния с постфиксом -cat
            var catStateName = $"{currentState.Name}-cat";
            var catState = new RSI.StateId(catStateName);

            // Получаем эффективный RSI для слоя
            var rsi = _sprite.LayerGetEffectiveRsi((args.User, sprite), layerIndex);
            if (rsi == null)
                continue;

            // Проверяем, существует ли такое состояние в RSI
            if (rsi.TryGetState(catState, out _))
            {
                // Заменяем состояние на вариант с лапами
                _sprite.LayerSetRsiState((args.User, sprite), layerIndex, catState);
            }
        }
    }
}
