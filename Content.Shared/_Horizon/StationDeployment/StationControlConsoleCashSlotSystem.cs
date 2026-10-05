using Content.Shared._Horizon.StationDeployment.Components;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Horizon.StationDeployment;

/// <summary>
/// Wires up the station control console's cash slot - mirrors SharedBankSystem's handling of
/// StationBankATMComponent's cash slot. Shared (not server-only) because the item slot's backing
/// container needs to exist and sync on the client too.
/// </summary>
public sealed class StationControlConsoleCashSlotSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationControlConsoleComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<StationControlConsoleComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(EntityUid uid, StationControlConsoleComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, StationControlConsoleComponent.CashSlotId, component.CashSlot);
    }

    private void OnComponentRemove(EntityUid uid, StationControlConsoleComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.CashSlot);
    }
}
