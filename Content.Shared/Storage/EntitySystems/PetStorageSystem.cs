using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Storage.EntitySystems;

public sealed class PetStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemComponent, GetVerbsEvent<AlternativeVerb>>(OnItemGetAlternativeVerbs);
        SubscribeLocalEvent<PetStorageComponent, PetInsertItemDoAfterEvent>(OnInsertItemDoAfter);
        SubscribeLocalEvent<PetStorageComponent, PetRemoveItemDoAfterEvent>(OnRemoveItemDoAfter);
    }

    private void OnItemGetAlternativeVerbs(EntityUid itemUid, ItemComponent itemComp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<PetStorageComponent>(args.User, out var petStorage))
            return;

        // Получаем ВСЕ надетые рюкзаки питомца
        var storageEntities = GetAllStorageEntities(args.User, petStorage);

        // Проверяем, находится ли предмет в каком-либо рюкзаке (надетом или нет)
        // Если предмет находится в рюкзаке, который не надет на питомца, добавляем верб для извлечения
        if (TryComp<StorageComponent>(Transform(itemUid).ParentUid, out var parentStorage))
        {
            var parentStorageUid = Transform(itemUid).ParentUid;

            // Проверяем, что это не надетый рюкзак питомца
            bool isEquippedStorage = storageEntities.Any(x => x.entity == parentStorageUid);

            if (!isEquippedStorage)
            {
                // Это рюкзак на земле или в руках - добавляем верб для извлечения
                AlternativeVerb removeFromUnequippedVerb = new()
                {
                    Act = () => TryRemoveItem(args.User, itemUid, petStorage, parentStorageUid),
                    Text = Loc.GetString("pet-storage-remove-from-verb", ("storage", Name(parentStorageUid))),
                    Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                    Priority = 2
                };
                args.Verbs.Add(removeFromUnequippedVerb);
            }
        }

        // Если нет надетых рюкзаков, выходим
        if (storageEntities.Count == 0)
            return;

        int verbIndex = 1;
        foreach (var (storageEntity, storage, storageName) in storageEntities)
        {
            // Проверяем, что предмет - это не сам рюкзак
            if (itemUid == storageEntity)
                continue;

            bool isInPetStorage = storage.Container.Contains(itemUid);

            if (isInPetStorage)
            {
                // Верб для извлечения из конкретного надетого рюкзака
                AlternativeVerb removeVerb = new()
                {
                    Act = () => TryRemoveItem(args.User, itemUid, petStorage, storageEntity),
                    Text = Loc.GetString("pet-storage-remove-from-verb", ("storage", storageName)),
                    Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                    Priority = 2
                };
                args.Verbs.Add(removeVerb);
            }
            else
            {
                // Верб для вставки в конкретный надетый рюкзак
                AlternativeVerb insertVerb = new()
                {
                    Act = () => TryInsertItem(args.User, itemUid, petStorage, storageEntity),
                    Text = Loc.GetString("pet-storage-insert-to-verb", ("storage", storageName)),
                    Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
                    Priority = 2
                };
                args.Verbs.Add(insertVerb);
            }
            verbIndex++;
        }
    }

    /// <summary>
    /// Получает все рюкзаки питомца из инвентаря
    /// </summary>
    private List<(EntityUid entity, StorageComponent storage, string name)> GetAllStorageEntities(EntityUid petUid, PetStorageComponent component)
    {
        var result = new List<(EntityUid, StorageComponent, string)>();

        // Если указан конкретный StorageEntity, возвращаем только его
        if (component.StorageEntity != null && TryComp<StorageComponent>(component.StorageEntity.Value, out var specificStorage))
        {
            result.Add((component.StorageEntity.Value, specificStorage, Name(component.StorageEntity.Value)));
            return result;
        }

        // Ищем ВСЕ рюкзаки во ВСЕХ слотах инвентаря
        if (!TryComp<InventoryComponent>(petUid, out var inventory))
            return result;

        // Получаем все слоты инвентаря
        var enumerator = _inventory.GetSlotEnumerator((petUid, inventory));
        while (enumerator.NextItem(out var slotEntity))
        {
            if (TryComp<StorageComponent>(slotEntity, out var storage))
            {
                result.Add((slotEntity, storage, Name(slotEntity)));
            }
        }

        return result;
    }

    private bool TryGetStorageEntity(EntityUid petUid, PetStorageComponent component, out EntityUid storageEntity, [NotNullWhen(true)] out StorageComponent? storage)
    {
        if (component.StorageEntity != null)
        {
            storageEntity = component.StorageEntity.Value;
            return TryComp(storageEntity, out storage);
        }

        if (!TryComp<InventoryComponent>(petUid, out var inventory))
        {
            storageEntity = EntityUid.Invalid;
            storage = null;
            return false;
        }

        // Ищем первый найденный рюкзак во всех слотах
        var enumerator = _inventory.GetSlotEnumerator((petUid, inventory));
        while (enumerator.NextItem(out var slotEntity))
        {
            if (TryComp<StorageComponent>(slotEntity, out storage))
            {
                storageEntity = slotEntity;
                return true;
            }
        }

        storageEntity = EntityUid.Invalid;
        storage = null;
        return false;
    }

    private void TryInsertItem(EntityUid uid, EntityUid item, PetStorageComponent component, EntityUid storageEntity)
    {
        // Проверяем размер предмета перед вставкой в хранилище
        if (component.MaxItemSize != null && TryComp<ItemComponent>(item, out var itemComp))
        {
            if (_prototypeManager.TryIndex(component.MaxItemSize.Value, out var maxSizeProto) &&
                _prototypeManager.TryIndex(itemComp.Size, out var itemSizeProto))
            {
                if (itemSizeProto > maxSizeProto)
                {
                    _popup.PopupClient(Loc.GetString("pet-storage-item-too-big"), uid, uid);
                    return;
                }
            }
        }

        var ev = new PetInsertItemDoAfterEvent { StorageEntity = GetNetEntity(storageEntity) };
        var args = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(component.InsertDelay), ev, uid, target: item, used: item)
        {
            BreakOnMove = component.BreakOnMove,
            BreakOnDamage = component.BreakOnDamage,
            BlockDuplicate = true
        };

        if (_doAfter.TryStartDoAfter(args))
        {
            _popup.PopupClient(Loc.GetString("pet-storage-insert-start", ("item", Name(item))), uid, uid);
        }
    }

    private void TryRemoveItem(EntityUid uid, EntityUid item, PetStorageComponent component, EntityUid storageEntity)
    {
        var ev = new PetRemoveItemDoAfterEvent { StorageEntity = GetNetEntity(storageEntity) };
        var args = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(component.RemoveDelay), ev, uid, target: item)
        {
            BreakOnMove = component.BreakOnMove,
            BreakOnDamage = component.BreakOnDamage,
            BlockDuplicate = true
        };

        if (_doAfter.TryStartDoAfter(args))
        {
            _popup.PopupClient(Loc.GetString("pet-storage-remove-start", ("item", Name(item))), uid, uid);
        }
    }

    private void OnInsertItemDoAfter(EntityUid uid, PetStorageComponent component, PetInsertItemDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Used == null)
            return;

        var storageEntity = GetEntity(args.StorageEntity);
        if (!TryComp<StorageComponent>(storageEntity, out var storage))
            return;

        if (_storage.Insert(storageEntity, args.Used.Value, out _, null, storage, playSound: true))
        {
            _popup.PopupClient(Loc.GetString("pet-storage-insert-success", ("item", Name(args.Used.Value))), uid, uid);
        }
        else
        {
            _popup.PopupClient(Loc.GetString("pet-storage-insert-failure", ("item", Name(args.Used.Value))), uid, uid);
        }

        args.Handled = true;
    }

    private void OnRemoveItemDoAfter(EntityUid uid, PetStorageComponent component, PetRemoveItemDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Target == null)
            return;

        var storageEntity = GetEntity(args.StorageEntity);
        if (!TryComp<StorageComponent>(storageEntity, out var storage))
            return;

        var item = args.Target.Value;

        if (!storage.Container.Contains(item))
        {
            _popup.PopupClient(Loc.GetString("pet-storage-remove-failure"), uid, uid);
            args.Handled = true;
            return;
        }

        if (_container.Remove(item, storage.Container))
        {
            var transform = Transform(uid);
            var itemTransform = Transform(item);
            itemTransform.Coordinates = transform.Coordinates;

            _popup.PopupClient(Loc.GetString("pet-storage-remove-success", ("item", Name(item))), uid, uid);
        }
        else
        {
            _popup.PopupClient(Loc.GetString("pet-storage-remove-failure"), uid, uid);
        }

        args.Handled = true;
    }
}
