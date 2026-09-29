using Content.Shared._Horizon.WaterInteractions;
using Content.Shared.Inventory.Events;

namespace Content.Server._Horizon.WaterInteractions;

/// <summary>
/// Controls abyssal pressure suits, protecting wearers from crush depths when equipped.
/// </summary>
public sealed partial class AbyssalSuitSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<AbyssalSuitComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<AbyssalSuitComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<AbyssalSuitComponent> ent, ref GotEquippedEvent args)
    {
        EnsureComp<AbyssalProtectedComponent>(args.Equipee);
    }

    private void OnUnequipped(Entity<AbyssalSuitComponent> ent, ref GotUnequippedEvent args)
    {
        RemComp<AbyssalProtectedComponent>(args.Equipee);
    }
}
