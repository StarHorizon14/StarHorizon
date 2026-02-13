using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Horizon.EventItems;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._Horizon.EventItems;

/// <summary>
/// Handles event items: granting, revoking, toggling, syncing to clients,
/// and entity serialization for storing modified component data.
/// </summary>
public sealed class EventItemsSystem : EntitySystem
{
    /// <summary>
    /// Components that should never be serialized/applied as overrides.
    /// These contain runtime-only state (physics broadphase, transforms, etc.)
    /// that would corrupt the entity if overwritten on an already-initialized entity.
    /// </summary>
    private static readonly HashSet<string> SkippedComponents = new()
    {
        "Transform",
        "MetaData",
        "Physics",
        "Fixtures",
        "Broadphase",
        "PhysicsMap",
        "ContainerManager",
        "ActorComponent",
        "Eye",
        "Appearance",
    };

    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("event-items");

        SubscribeNetworkEvent<EventItemRequestMsg>(OnItemRequest);
        SubscribeNetworkEvent<EventItemToggleMsg>(OnItemToggle);
    }

    /// <summary>
    /// Serializes an entity's component overrides relative to its prototype into YAML.
    /// Only stores the diff (fields that differ from the prototype defaults).
    /// </summary>
    public string? SerializeEntityOverrides(EntityUid uid)
    {
        var meta = MetaData(uid);
        var proto = meta.EntityPrototype;

        if (proto == null)
        {
            _sawmill.Warning($"Entity {ToPrettyString(uid)} has no prototype, cannot serialize overrides.");
            return null;
        }

        // Build prototype component cache (serialize prototype defaults)
        // Some components contain non-serializable types (e.g. EntityUid), so we skip those.
        var protoCache = new Dictionary<string, MappingDataNode>();
        foreach (var (compName, comp) in proto.Components)
        {
            try
            {
                protoCache[compName] = _serialization.WriteValueAs<MappingDataNode>(
                    comp.Component.GetType(),
                    comp.Component,
                    alwaysWrite: true);
            }
            catch (Exception)
            {
                // Component contains non-serializable types (EntityUid, etc.) — skip it
            }
        }

        var components = new SequenceDataNode();

        foreach (var component in EntityManager.GetComponents(uid))
        {
            var compType = component.GetType();
            var reg = _compFactory.GetRegistration(compType);

            if (reg.Unsaved)
                continue;

            // Skip components with runtime-only state that can't/shouldn't be serialized
            if (SkippedComponents.Contains(reg.Name))
                continue;

            // Skip components that couldn't be serialized at prototype level
            // (they contain non-serializable types like EntityUid)
            if (proto.Components.ContainsKey(reg.Name) && !protoCache.ContainsKey(reg.Name))
                continue;

            MappingDataNode compMapping;

            try
            {
                if (protoCache.TryGetValue(reg.Name, out var protoMapping))
                {
                    // Serialize with alwaysWrite to capture all fields, then diff against prototype
                    compMapping = _serialization.WriteValueAs<MappingDataNode>(
                        compType,
                        component,
                        alwaysWrite: true);

                    var diffMapping = compMapping.Except(protoMapping);
                    if (diffMapping == null)
                        continue; // No changes from prototype

                    compMapping = diffMapping;
                }
                else
                {
                    // Component not in prototype — it was added, serialize everything
                    compMapping = _serialization.WriteValueAs<MappingDataNode>(
                        compType,
                        component,
                        alwaysWrite: false);
                }
            }
            catch (Exception ex)
            {
                // Component contains non-serializable fields (EntityUid, etc.) — skip it
                _sawmill.Debug($"Skipping component {reg.Name} during serialization: {ex.Message}");
                continue;
            }

            if (compMapping.Children.Count != 0 || !protoCache.ContainsKey(reg.Name))
            {
                compMapping.InsertAt(0, "type", new ValueDataNode(reg.Name));
                components.Add(compMapping);
            }
        }

        if (components.Count == 0)
            return null;

        // Convert to YAML string
        var sw = new StringWriter();
        components.Write(sw);
        return sw.ToString();
    }

    /// <summary>
    /// Grants an item to a player and stores it in the database.
    /// </summary>
    public async void GrantItemToPlayer(
        EntityUid itemEntity,
        Guid targetPlayerUserId,
        int creditCost,
        string grantedBy)
    {
        var meta = MetaData(itemEntity);
        var protoId = meta.EntityPrototype?.ID;

        if (string.IsNullOrEmpty(protoId))
        {
            _sawmill.Error($"Cannot grant entity {ToPrettyString(itemEntity)} — no prototype.");
            return;
        }

        var overridesYaml = SerializeEntityOverrides(itemEntity);

        // Capture custom name/description if they differ from prototype
        string? customName = null;
        string? customDesc = null;

        if (meta.EntityPrototype != null)
        {
            if (meta.EntityName != meta.EntityPrototype.Name)
                customName = meta.EntityName;
            if (meta.EntityDescription != meta.EntityPrototype.Description)
                customDesc = meta.EntityDescription;
        }

        var dbItem = new HorizonAdminLoadout
        {
            PlayerUserId = targetPlayerUserId,
            PrototypeId = protoId,
            ComponentOverridesYaml = overridesYaml,
            CustomName = customName,
            CustomDescription = customDesc,
            CreditCost = creditCost,
            IsEnabled = true,
            GrantedBy = grantedBy,
            GrantedAt = DateTime.UtcNow,
        };

        try
        {
            await _db.AddAdminLoadoutItemAsync(dbItem);
            _sawmill.Info($"Admin {grantedBy} granted event item {protoId} to player {targetPlayerUserId} (cost: {creditCost})");

            // Immediately notify the target player if they're online
            SendItemsToPlayer(targetPlayerUserId);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to grant event item: {ex}");
        }
    }

    /// <summary>
    /// Removes an event item from the database.
    /// </summary>
    public async void RemoveItem(int itemId, Guid playerUserId)
    {
        try
        {
            await _db.RemoveAdminLoadoutItemAsync(itemId);
            _sawmill.Info($"Removed event item {itemId}");
            SendItemsToPlayer(playerUserId);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to remove event item {itemId}: {ex}");
        }
    }

    /// <summary>
    /// Sends the full list of event items to a player if they're online.
    /// </summary>
    public async void SendItemsToPlayer(Guid userId)
    {
        if (!_playerManager.TryGetSessionByUsername(
                GetPlayerNameByUserId(userId), out var session))
            return;

        await SendItemsToSession(session, userId);
    }

    /// <summary>
    /// Sends the full list of event items to a specific session.
    /// </summary>
    public async Task SendItemsToSession(ICommonSession session, Guid userId)
    {
        try
        {
            var dbItems = await _db.GetAdminLoadoutItemsAsync(userId);
            var items = dbItems.Select(ConvertToData).ToList();

            var msg = new EventItemListMsg { Items = items };
            RaiseNetworkEvent(msg, session);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to send event items to player: {ex}");
        }
    }

    private void OnItemRequest(EventItemRequestMsg msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId.UserId;
        _ = SendItemsToSession(args.SenderSession, userId);
    }

    private async void OnItemToggle(EventItemToggleMsg msg, EntitySessionEventArgs args)
    {
        try
        {
            await _db.SetAdminLoadoutItemEnabledAsync(msg.ItemId, msg.Enabled);
            _sawmill.Debug($"Player {args.SenderSession.Name} toggled event item {msg.ItemId} to {msg.Enabled}");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to toggle event item {msg.ItemId}: {ex}");
        }
    }

    private string GetPlayerNameByUserId(Guid userId)
    {
        // Try to find the player by iterating sessions
        foreach (var session in _playerManager.Sessions)
        {
            if (session.UserId.UserId == userId)
                return session.Name;
        }

        return string.Empty;
    }

    private static EventItemData ConvertToData(HorizonAdminLoadout dbItem)
    {
        return new EventItemData
        {
            Id = dbItem.Id,
            PrototypeId = dbItem.PrototypeId,
            CustomName = dbItem.CustomName,
            CustomDescription = dbItem.CustomDescription,
            CreditCost = dbItem.CreditCost,
            IsEnabled = dbItem.IsEnabled,
            GrantedBy = dbItem.GrantedBy,
            GrantedAt = dbItem.GrantedAt,
        };
    }
}
