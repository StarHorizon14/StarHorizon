using Content.Server.Body.Components;
using Content.Shared._Horizon.CCVar;
using Content.Shared.Cargo.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Worldgen.Systems.GC;

// Horizon-Start
/// <summary>
/// Периодически удаляет мелочь, дрейфующую в открытом космосе вне гридов: сущности с
/// <see cref="SpaceGarbageComponent"/> и дешёвые предметы. За долгую смену такой мусор копится
/// тысячами и держит память (каждая сущность — около 12 КБ).
/// Перенесено из Monolith14 (CheapEntitiesCleanupSystem и SpaceGarbageCleanupSystem), объединено
/// в одну систему и вынесено на CVar-ы.
/// </summary>
public sealed partial class SpaceLitterCleanupSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private bool _enabled;
    private float _interval;
    private float _maxPrice;
    private TimeSpan _nextCleanup;

    private readonly HashSet<EntityUid> _toDelete = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, HorizonCCVars.SpaceLitterCleanupEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, HorizonCCVars.SpaceLitterCleanupInterval, v => _interval = v, true);
        Subs.CVar(_cfg, HorizonCCVars.SpaceLitterCleanupMaxPrice, v => _maxPrice = v, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        var curTime = _timing.CurTime;
        if (_nextCleanup == TimeSpan.Zero)
        {
            // Первая очистка — через полный интервал после старта, а не сразу.
            _nextCleanup = curTime + TimeSpan.FromSeconds(_interval);
            return;
        }

        if (curTime < _nextCleanup)
            return;

        _nextCleanup = curTime + TimeSpan.FromSeconds(_interval);
        Cleanup();
    }

    private void Cleanup()
    {
        _toDelete.Clear();

        // 1) Помеченный космический мусор (пули, гильзы и т.п.).
        var garbage = EntityQueryEnumerator<SpaceGarbageComponent, TransformComponent>();
        while (garbage.MoveNext(out var uid, out _, out var xform))
        {
            if (IsDriftingInSpace(uid, xform))
                _toDelete.Add(uid);
        }

        // 2) Дешёвые предметы и стопки.
        var priced = EntityQueryEnumerator<StaticPriceComponent, TransformComponent>();
        while (priced.MoveNext(out var uid, out var price, out var xform))
        {
            if (!IsDriftingInSpace(uid, xform))
                continue;

            double total;
            if (TryComp<StackComponent>(uid, out var stack))
                total = price.Price * stack.Count;
            else if (HasComp<StackPriceComponent>(uid))
                continue; // цену стопки считает другой компонент — не рискуем
            else
                total = price.Price;

            if (total > _maxPrice)
                continue;

            // Контейнеры (ящики, сумки) и всё живое не трогаем.
            if (HasComp<ContainerManagerComponent>(uid)
                || HasComp<MobStateComponent>(uid)
                || HasComp<ActorComponent>(uid)
                || HasComp<BrainComponent>(uid))
                continue;

            _toDelete.Add(uid);
        }

        foreach (var uid in _toDelete)
        {
            QueueDel(uid);
        }

        Log.Info($"SpaceLitterCleanup: удалено {_toDelete.Count} сущностей в открытом космосе.");
        _toDelete.Clear();
    }

    /// <summary>
    /// Сущность лежит прямо на карте: не на гриде и не в контейнере (руки, рюкзак, шкаф).
    /// </summary>
    private bool IsDriftingInSpace(EntityUid uid, TransformComponent xform)
    {
        return xform.GridUid == null
               && xform.MapUid != null
               && xform.ParentUid == xform.MapUid
               && !_container.IsEntityInContainer(uid);
    }
}
// Horizon-End
