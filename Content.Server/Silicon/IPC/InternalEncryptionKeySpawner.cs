using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Containers;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Silicon.IPC;

/// <summary>
/// Horizon: gives entities with a built-in radio (e.g. IPCs) the encryption keys they would otherwise
/// get from a headset, since they have no ears slot to wear one.
/// </summary>
public sealed partial class InternalEncryptionKeySpawner : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private const string EarsSlot = "ears";

    /// <summary>
    /// Whether this entity stores encryption keys internally instead of in a worn headset.
    /// </summary>
    public bool UsesInternalKeys(EntityUid target, [NotNullWhen(true)] out EncryptionKeyHolderComponent? holder)
    {
        holder = null;
        return !_inventory.HasSlot(target, EarsSlot) && TryComp(target, out holder);
    }

    /// <summary>
    /// Copies the default keys of the headset in the gear's ears slot into the entity's internal key holder.
    /// </summary>
    public void TryInsertEncryptionKeys(EntityUid target, IEquipmentLoadout gear)
    {
        if (!UsesInternalKeys(target, out var holder))
            return;

        var ears = gear.GetGear(EarsSlot);
        if (string.IsNullOrEmpty(ears)
            || !_proto.TryIndex<EntityPrototype>(ears, out var earsProto)
            || !earsProto.TryGetComponent<ContainerFillComponent>(out var fill, EntityManager.ComponentFactory)
            || !fill.Containers.TryGetValue(EncryptionKeyHolderComponent.KeyContainerName, out var keys))
            return;

        TryInsertEncryptionKeys(target, keys.Select(k => new EntProtoId(k)), holder);
    }

    /// <summary>
    /// Spawns the given keys into the entity's internal key holder, skipping duplicates and keys that don't fit.
    /// </summary>
    public void TryInsertEncryptionKeys(EntityUid target, IEnumerable<EntProtoId> keys, EncryptionKeyHolderComponent? holder = null)
    {
        if (!Resolve(target, ref holder, false))
            return;

        var coords = Transform(target).Coordinates;
        foreach (var key in keys)
        {
            if (holder.KeyContainer.ContainedEntities.Count >= holder.KeySlots)
                break;

            if (holder.KeyContainer.ContainedEntities.Any(e => MetaData(e).EntityPrototype?.ID == key.Id))
                continue;

            var keyEnt = Spawn(key, coords);
            if (!_container.Insert(keyEnt, holder.KeyContainer))
                QueueDel(keyEnt);
        }
    }
}
