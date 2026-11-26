using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Marketplace;

/// <summary>
/// Tray in a PDA for bluespace crystals that pay for marketplace teleport deliveries.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MarketplaceCrystalSlotComponent : Component
{
    public const string SlotId = "PDA-bluespace";

    [DataField]
    public ItemSlot CrystalSlot = new();
}

/// <summary>
/// A sector teleporter a parcel can be delivered to.
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceDeliveryPoint
{
    public NetEntity Teleporter;
    public string Name = string.Empty;
    public bool Powered;
}

/// <summary>
/// Accept an offer from the PDA and have the item teleported to a sector teleporter.
/// </summary>
[Serializable, NetSerializable]
public sealed class MarketplaceCartridgeAcceptOfferEvent(int offerId, NetEntity teleporter) : CartridgeMessageEvent
{
    public readonly int OfferId = offerId;
    public readonly NetEntity Teleporter = teleporter;
}

public sealed class MarketplaceCrystalSlotSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarketplaceCrystalSlotComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MarketplaceCrystalSlotComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(Entity<MarketplaceCrystalSlotComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent, MarketplaceCrystalSlotComponent.SlotId, ent.Comp.CrystalSlot);
    }

    private void OnRemove(Entity<MarketplaceCrystalSlotComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent, ent.Comp.CrystalSlot);
    }
}
