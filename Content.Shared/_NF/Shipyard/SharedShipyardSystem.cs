using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Content.Shared._NF.Shipyard;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared._NF.Shipyard.Components;

namespace Content.Shared._NF.Shipyard;

// Note: when adding a new ui key, don't forget to modify the dictionary in SharedShipyardSystem
[NetSerializable, Serializable]
public enum ShipyardConsoleUiKey : byte
{
    Shipyard,
    Security,
    Syndicate,
    BlackMarket,
    Expedition,
    Scrap,
    Sr,
    Medical,
    // Add ships to this key if they are only available from mothership consoles. Shipyards using it are inherently empty and are populated using the ShipyardListingComponent.
    Custom,
    Parking
}

public abstract class SharedShipyardSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShipyardConsoleComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShipyardConsoleComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<ShipyardConsoleComponent, EntInsertedIntoContainerMessage>(OnCashSlotEntityInserted); // Horizon: cash slot
        SubscribeLocalEvent<ShipyardConsoleComponent, EntRemovedFromContainerMessage>(OnCashSlotEntityRemoved); // Horizon: cash slot

        // Horizon commented
        //SubscribeLocalEvent<ShipyardConsoleComponent, ComponentGetState>(OnGetState);
        // SubscribeLocalEvent<ShipyardConsoleComponent, ComponentHandleState>(OnHandleState);
    }

    // Horizon commented
    // private void OnHandleState(EntityUid uid, ShipyardConsoleComponent component, ref ComponentHandleState args)
    // {
    //     if (args.Current is not ShipyardConsoleComponentState state) return;

    // }

    // private void OnGetState(EntityUid uid, ShipyardConsoleComponent component, ref ComponentGetState args)
    // {

    // }

    private void OnComponentInit(EntityUid uid, ShipyardConsoleComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, ShipyardConsoleComponent.TargetIdCardSlotId, component.TargetIdSlot);

        // Horizon: create the cash slot if this console has one
        if (component.CashSlot != null && component.CashSlotName != null)
            _itemSlotsSystem.AddItemSlot(uid, component.CashSlotName, component.CashSlot);
    }

    private void OnComponentRemove(EntityUid uid, ShipyardConsoleComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.TargetIdSlot);

        // Horizon: cash slot
        if (component.CashSlot != null)
            _itemSlotsSystem.RemoveItemSlot(uid, component.CashSlot);
    }

    // Horizon: cash slot handlers
    private void OnCashSlotEntityInserted(Entity<ShipyardConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (ent.Comp.CashSlotName == null || args.Container.ID != ent.Comp.CashSlotName)
            return;

        if (ent.Comp.CurrencyStackType != null
            && _itemSlotsSystem.TryGetSlot(ent.Owner, ent.Comp.CashSlotName, out var slot)
            && TryComp<StackComponent>(slot.ContainerSlot?.ContainedEntity, out var stack)
            && stack.StackTypeId == ent.Comp.CurrencyStackType)
        {
            ent.Comp.CashSlotBalance = stack.Count;
        }
        else
        {
            ent.Comp.CashSlotBalance = 0;
        }
        Dirty(ent);
    }

    private void OnCashSlotEntityRemoved(Entity<ShipyardConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (ent.Comp.CashSlotName == null || args.Container.ID != ent.Comp.CashSlotName)
            return;

        ent.Comp.CashSlotBalance = 0;
        Dirty(ent);
    }
    // End Horizon: cash slot handlers

    [Serializable, NetSerializable]
    private sealed class ShipyardConsoleComponentState : ComponentState
    {
        public List<string> AccessLevels;

        public ShipyardConsoleComponentState(List<string> accessLevels)
        {
            AccessLevels = accessLevels;
        }
    }

}
