using Content.Shared.Item;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Horizon.Paws;

/// <summary>
/// Система, которая блокирует поднятие предметов для существ с лапами.
/// По умолчанию блокирует всё, кроме предметов из WhitelistEntities или WhitelistTags.
/// Также уменьшает скорость атаки оружием.
/// Проверку размера выполняет PetStorageSystem.
/// </summary>
public sealed class PawsSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PawsComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<GetMeleeAttackRateEvent>(OnGetMeleeAttackRate);
    }

    private void OnGetMeleeAttackRate(ref GetMeleeAttackRateEvent args)
    {
        if (!TryComp<PawsComponent>(args.User, out var paws))
            return;

        args.Multipliers *= paws.MeleeAttackRateMultiplier;
    }

    private void OnPickupAttempt(Entity<PawsComponent> ent, ref PickupAttemptEvent args)
    {
        // Если событие уже отменено, ничего не делаем
        if (args.Cancelled)
            return;

        // Если включен обход блеклиста - разрешаем брать всё
        if (ent.Comp.BypassBlacklist)
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

        // Проверяем белый список тегов - если предмет имеет любой из тегов, разрешаем взять
        foreach (var tag in ent.Comp.WhitelistTags)
        {
            if (_tag.HasTag(args.Item, tag))
            {
                // Предмет имеет разрешённый тег - разрешаем взять
                return;
            }
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
