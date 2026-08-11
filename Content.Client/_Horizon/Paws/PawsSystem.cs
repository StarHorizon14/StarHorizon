using Content.Client.Items.Systems;
using Content.Shared._Horizon.Paws;
using Content.Shared.Hands;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._Horizon.Paws;

/// <summary>
/// Клиентская система для отображения лап вместо предметов в руках.
/// Подписывается на GetInhandVisualsEvent ПОСЛЕ ItemSystem и заменяет/добавляет спрайты с -cat постфиксом.
/// </summary>
public sealed class PawsSystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _resCache = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Подписываемся ПОСЛЕ ItemSystem через SpriteComponent, чтобы можно было модифицировать или добавить слои
        SubscribeLocalEvent<SpriteComponent, GetInhandVisualsEvent>(OnGetInhandVisuals, after: new[] { typeof(ItemSystem) });
    }

    private void OnGetInhandVisuals(EntityUid uid, SpriteComponent sprite, GetInhandVisualsEvent args)
    {
        // args.User - владелец предмета (кот)
        // Проверяем, есть ли у владельца компонент лап
        if (!TryComp<PawsComponent>(args.User, out var paws))
            return;

        if (!TryComp<ItemComponent>(uid, out var item))
            return;

        // Получаем RSI предмета
        RSI? rsi = null;
        if (item.RsiPath != null)
            rsi = _resCache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / item.RsiPath).RSI;
        else
            rsi = sprite.BaseRSI;

        if (rsi == null)
            return;

        var location = args.Location.ToString().ToLowerInvariant();

        // Если ItemSystem уже добавил слои - пытаемся заменить на -cat версии
        if (args.Layers.Count > 0)
        {
            for (var i = 0; i < args.Layers.Count; i++)
            {
                var (key, layer) = args.Layers[i];
                var state = layer.State;

                if (string.IsNullOrEmpty(state))
                    continue;

                var catState = $"{state}-cat";

                // Проверяем, есть ли -cat версия в RSI
                if (rsi.TryGetState(catState, out _))
                {
                    // Заменяем слой на версию с -cat
                    var newLayer = new PrototypeLayerData
                    {
                        RsiPath = layer.RsiPath,
                        State = catState,
                        MapKeys = layer.MapKeys
                    };
                    args.Layers[i] = (key, newLayer);
                }
            }
        }
        else
        {
            // ItemSystem не добавил слои (нет обычного inhand спрайта)
            // Пытаемся добавить -cat версию напрямую

            // Формируем имя состояния с учётом HeldPrefix
            var baseKey = $"inhand-{location}";
            var state = (item.HeldPrefix == null)
                ? baseKey
                : $"{item.HeldPrefix}-{baseKey}";

            var catState = $"{state}-cat";

            // Проверяем, есть ли -cat версия
            if (rsi.TryGetState(catState, out _))
            {
                var layer = new PrototypeLayerData
                {
                    RsiPath = rsi.Path.ToString(),
                    State = catState,
                    MapKeys = new HashSet<string> { catState }
                };
                args.Layers.Add((catState, layer));
            }
        }
    }
}
