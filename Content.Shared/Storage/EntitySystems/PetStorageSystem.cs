using System.Diagnostics.CodeAnalysis;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared.Storage.EntitySystems;

public sealed class PetStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

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

        if (!TryGetStorageEntity(args.User, petStorage, out var storageEntity, out var storage))
            return;

        bool isInPetStorage = storage.Container.Contains(itemUid);

        if (isInPetStorage)
        {
            AlternativeVerb removeVerb = new()
            {
                Act = () => TryRemoveItem(args.User, itemUid, petStorage, storageEntity),
                Text = Loc.GetString("pet-storage-remove-verb"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                Priority = 2
            };
            args.Verbs.Add(removeVerb);
        }
        else
        {
            AlternativeVerb insertVerb = new()
            {
                Act = () => TryInsertItem(args.User, itemUid, petStorage, storageEntity),
                Text = Loc.GetString("pet-storage-insert-verb"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
                Priority = 2
            };
            args.Verbs.Add(insertVerb);
        }
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

        foreach (var slotName in component.SlotPriority)
        {
            if (_inventory.TryGetSlotEntity(petUid, slotName, out var slotEntity, inventory))
            {
                if (TryComp<StorageComponent>(slotEntity, out storage))
                {
                    storageEntity = slotEntity.Value;
                    return true;
                }
            }
        }

        storageEntity = EntityUid.Invalid;
        storage = null;
        return false;
    }

    private void TryInsertItem(EntityUid uid, EntityUid item, PetStorageComponent component, EntityUid storageEntity)
    {
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
