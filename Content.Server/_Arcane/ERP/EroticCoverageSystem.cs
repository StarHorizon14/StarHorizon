using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.ERP.Organs;
using Content.Shared._Arcane.ERP.OrgansAppearance;
using Content.Shared._Arcane.ERP.Preferences;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Server._Arcane.ERP;

public sealed class EroticCoverageSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, EroticOrgansSpawnedEvent>(OnOrgansSpawned);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ClothingDidEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ClothingDidUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HumanoidAppearanceComponent, WearerMaskToggledEvent>(OnMaskToggled);
        // Re-run coverage when the visual component arrives (may lag behind the organs spawn event).
        SubscribeLocalEvent<ErpOrganVisualsComponent, ComponentStartup>(OnVisualsStartup);
    }

    private void OnOrgansSpawned(Entity<HumanoidAppearanceComponent> ent, ref EroticOrgansSpawnedEvent args)
        => RefreshCoverage(ent);

    private void OnEquipped(Entity<HumanoidAppearanceComponent> ent, ref ClothingDidEquippedEvent args)
        => RefreshCoverage(ent);

    private void OnUnequipped(Entity<HumanoidAppearanceComponent> ent, ref ClothingDidUnequippedEvent args)
        => RefreshCoverage(ent);

    private void OnMaskToggled(Entity<HumanoidAppearanceComponent> ent, ref WearerMaskToggledEvent args)
        => RefreshCoverage(ent);

    private void OnVisualsStartup(Entity<ErpOrganVisualsComponent> ent, ref ComponentStartup args)
        => RefreshCoverage(ent);

    public void RefreshCoverage(EntityUid uid)
    {
        var coveredLayers = GetCoveredVisualLayers(uid);
        var newCovered = new HashSet<string>();

        if (!TryComp<BodyComponent>(uid, out var body)) // Arcane: standalone entities have no body
            return;

        foreach (var organ in _body.GetBodyOrganEntityComps<EroticOrganComponent>((uid, body)))
        {
            if (!_containers.TryGetContainingContainer(organ.Owner, out var container))
                continue;

            var containerId = container.ID;
            if (!containerId.StartsWith(SharedBodySystem.OrganSlotContainerIdPrefix))
                continue;

            var slotId = containerId[SharedBodySystem.OrganSlotContainerIdPrefix.Length..];

            // This codebase's anatomy doesn't split the torso into separate groin/chest parts,
            // so coverage is discriminated by organ slot id instead of body part type.
            var covered = slotId switch
            {
                ErpOrganSlots.Breasts => coveredLayers.Contains(HumanoidVisualLayers.ErpChest),
                _ => coveredLayers.Contains(HumanoidVisualLayers.ErpGroin),
            };

            organ.Comp1.Visible = !covered;

            if (covered)
                newCovered.Add(slotId);
        }

        if (!TryComp<ErpOrganVisualsComponent>(uid, out var visuals))
            return;

        if (visuals.CoveredSlots.SetEquals(newCovered))
            return;

        visuals.CoveredSlots = newCovered;
        Dirty(uid, visuals);
    }

    private HashSet<HumanoidVisualLayers> GetCoveredVisualLayers(EntityUid uid)
    {
        var coveredLayers = new HashSet<HumanoidVisualLayers>();
        var enumerator = _inventory.GetSlotEnumerator(uid);
        while (enumerator.NextItem(out var item))
        {
            if (!TryComp<HideLayerClothingComponent>(item, out var hideLayer) ||
                !TryComp<ClothingComponent>(item, out var clothing))
            {
                continue;
            }

            var inSlot = clothing.InSlotFlag ?? SlotFlags.NONE;
            if (inSlot == SlotFlags.NONE || !IsHideLayerEnabled(item, hideLayer))
                continue;

            foreach (var (layer, validSlots) in hideLayer.Layers)
            {
                if (validSlots.HasFlag(inSlot))
                    coveredLayers.Add(layer);
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if (hideLayer.Slots is { } slots && clothing.Slots.HasFlag(inSlot))
#pragma warning restore CS0618 // Type or member is obsolete
            {
                foreach (var layer in slots)
                    coveredLayers.Add(layer);
            }
        }

        return coveredLayers;
    }

    private bool IsHideLayerEnabled(EntityUid uid, HideLayerClothingComponent hideLayer)
    {
        if (!hideLayer.HideOnToggle)
            return true;

        if (!TryComp<MaskComponent>(uid, out var mask))
            return true;

        return !mask.IsToggled;
    }
}
