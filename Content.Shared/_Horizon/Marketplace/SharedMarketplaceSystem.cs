using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Horizon.Marketplace;

public abstract class SharedMarketplaceSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarketplaceConsoleComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MarketplaceConsoleComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(Entity<MarketplaceConsoleComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent, MarketplaceConsoleComponent.ItemSlotId, ent.Comp.ItemSlot);
    }

    private void OnComponentRemove(Entity<MarketplaceConsoleComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent, ent.Comp.ItemSlot);
    }

    public static int GetTax(int total, float taxRate)
    {
        return (int) Math.Floor(total * (double) Math.Clamp(taxRate, 0f, 1f));
    }
}
