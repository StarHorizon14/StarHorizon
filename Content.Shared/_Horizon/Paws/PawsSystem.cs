using Content.Shared.Item;

namespace Content.Shared._Horizon.Paws;

/// <summary>
/// Система, которая блокирует поднятие предметов для существ с лапами.
/// По умолчанию блокирует всё, кроме предметов из WhitelistEntities.
/// Проверку размера выполняет PetStorageSystem.
/// </summary>
public sealed class PawsSystem : EntitySystem
{
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

        // Получаем прототип предмета
        var protoId = MetaData(args.Item).EntityPrototype?.ID;

        // Проверяем белый список энтити - если предмет в списке, разрешаем взять
        if (protoId != null && ent.Comp.WhitelistEntities.Contains(protoId))
        {
            // Предмет в белом списке - разрешаем взять
            // Проверку размера сделает PetStorageSystem
            return;
        }

        // Если включен режим "блокировать всё по умолчанию"
        if (ent.Comp.BlockAllByDefault)
        {
            // Предмет НЕ в белом списке и включён BlockAllByDefault - блокируем
            args.Cancel();
            return;
        }
    }
}
