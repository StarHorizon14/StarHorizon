using Content.Shared.Item;
using Content.Shared.Tag;

namespace Content.Shared._Horizon.Paws;

/// <summary>
/// Система, которая блокирует поднятие предметов по тегам.
/// </summary>
public sealed class PawsSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PawsComponent, PickupAttemptEvent>(OnPickupAttempt);
    }

    private void OnPickupAttempt(Entity<PawsComponent> ent, ref PickupAttemptEvent args)
    {
        // Если событие уже отменено, ничего не делаем
        if (args.Cancelled)
            return;

        // Если список заблокированных тегов пуст, ничего не блокируем
        if (ent.Comp.BlockedTags.Count == 0)
            return;

        // Проверяем, есть ли у предмета любой из заблокированных тегов
        if (_tagSystem.HasAnyTag(args.Item, ent.Comp.BlockedTags))
        {
            args.Cancel();
        }
    }
}
