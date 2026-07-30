using Content.Shared._Horizon.RCD;
using Content.Shared.AdvancedRCD.Components;
using Content.Shared.Audio;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.AdvancedRCD.Systems;

public abstract class SharedAdvancedRCDSystem : EntitySystem
{
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly IPrototypeManager ProtoManager = default!;
    [Dependency] protected readonly SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected readonly SharedHandsSystem Hands = default!;
    [Dependency] protected readonly SharedInteractionSystem Interaction = default!;
    [Dependency] protected readonly SharedMaterialStorageSystem MaterialStorage = default!;
    [Dependency] protected readonly SharedMapSystem MapSystem = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] protected readonly TurfSystem Turf = default!;
    [Dependency] private readonly MachinePartSystem _machinePart = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly EntProtoId ConstructionEffectProto = "EffectRCDConstruct3";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvancedRCDComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<AdvancedRCDComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AdvancedRCDComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AdvancedRCDComponent, AdvancedRCDDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<AdvancedRCDComponent, DoAfterAttemptEvent<AdvancedRCDDoAfterEvent>>(OnDoAfterAttempt);

        Subs.BuiEvents<AdvancedRCDComponent>(AdvancedRCDUiKey.Key, subs =>
        {
            subs.Event<AdvancedRCDSelectMessage>(OnSelectMessage);
            subs.Event<AdvancedRCDSelectEntityMessage>(OnSelectEntityMessage);
            subs.Event<AdvancedRCDToggleEntityMessage>(OnToggleEntityMessage);
        });
    }

    #region UI Messages

    private void OnSelectMessage(EntityUid uid, AdvancedRCDComponent component, AdvancedRCDSelectMessage args)
    {
        if (args.ProtoId == null)
        {
            component.ProtoId = null;
            component.SelectedEntityProto = null;
            Dirty(uid, component);
            return;
        }

        // Verify the prototype is available
        if (!component.AvailablePrototypes.Contains(args.ProtoId))
            return;

        component.ProtoId = args.ProtoId;
        component.SelectedEntityProto = null; // Clear entity selection
        Dirty(uid, component);
    }

    private void OnSelectEntityMessage(EntityUid uid, AdvancedRCDComponent component, AdvancedRCDSelectEntityMessage args)
    {
        if (args.EntityProtoId == null)
        {
            component.SelectedEntityProto = null;
            Dirty(uid, component);
            return;
        }

        // Verify the entity is available and enabled
        if (!component.InsertedEntities.TryGetValue(args.EntityProtoId, out var data) || !data.Enabled)
            return;

        component.SelectedEntityProto = args.EntityProtoId;
        component.ProtoId = null; // Clear RCD prototype selection
        Dirty(uid, component);
    }

    private void OnToggleEntityMessage(EntityUid uid, AdvancedRCDComponent component, AdvancedRCDToggleEntityMessage args)
    {
        if (!component.InsertedEntities.TryGetValue(args.EntityProtoId, out var data))
            return;

        data.Enabled = args.Enabled;

        // If we disabled the currently selected entity, clear selection
        if (!args.Enabled && component.SelectedEntityProto == args.EntityProtoId)
            component.SelectedEntityProto = null;

        Dirty(uid, component);
    }

    #endregion

    #region Event Handlers

    private void OnExamine(EntityUid uid, AdvancedRCDComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // Show selected structure from RCD prototype
        if (component.ProtoId != null && ProtoManager.TryIndex(component.ProtoId.Value, out var rcdProto))
        {
            args.PushMarkup(Loc.GetString("advanced-rcd-examine-selected", ("name", Loc.GetString(rcdProto.Name))));

            if (rcdProto.MaterialCost.Count > 0)
            {
                var costStrings = new List<string>();
                foreach (var (mat, amount) in rcdProto.MaterialCost)
                {
                    if (ProtoManager.TryIndex<MaterialPrototype>(mat, out var matProto))
                        costStrings.Add($"{Loc.GetString(matProto.Name)}: {amount}");
                }
                args.PushMarkup(Loc.GetString("advanced-rcd-examine-cost", ("cost", string.Join(", ", costStrings))));
            }
        }
        // Show selected structure from inserted board
        else if (component.SelectedEntityProto != null &&
                 ProtoManager.TryIndex<EntityPrototype>(component.SelectedEntityProto.Value, out var entProto) &&
                 component.InsertedEntities.TryGetValue(component.SelectedEntityProto.Value, out var boardData))
        {
            args.PushMarkup(Loc.GetString("advanced-rcd-examine-selected", ("name", entProto.Name)));

            // Show cost from stored board data
            if (boardData.MaterialCost.Count > 0)
            {
                var costStrings = new List<string>();
                foreach (var (mat, amount) in boardData.MaterialCost)
                {
                    if (ProtoManager.TryIndex<MaterialPrototype>(mat, out var matProto))
                        costStrings.Add($"{Loc.GetString(matProto.Name)}: {amount}");
                }
                args.PushMarkup(Loc.GetString("advanced-rcd-examine-cost", ("cost", string.Join(", ", costStrings))));
            }
        }

        // Show material storage
        var materials = MaterialStorage.GetStoredMaterials(uid);
        if (materials.Count > 0)
        {
            var matStrings = new List<string>();
            foreach (var (mat, amount) in materials)
            {
                if (ProtoManager.TryIndex<MaterialPrototype>(mat, out var matProto))
                    matStrings.Add($"{Loc.GetString(matProto.Name)}: {amount}");
            }
            args.PushMarkup(Loc.GetString("advanced-rcd-examine-materials", ("materials", string.Join(", ", matStrings))));
        }

        // Show inserted boards count
        if (component.InsertedEntities.Count > 0)
        {
            args.PushMarkup(Loc.GetString("advanced-rcd-examine-boards", ("count", component.InsertedEntities.Count)));
        }
    }

    private void OnAfterInteract(EntityUid uid, AdvancedRCDComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (TryInteract(uid, args.User, args.Target, args.ClickLocation, component))
            args.Handled = true;
    }

    private void OnInteractUsing(EntityUid uid, AdvancedRCDComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Check if it's a machine board
        if (!TryComp<MachineBoardComponent>(args.Used, out var board))
            return;

        var entityProto = board.Prototype;

        // Check if already inserted
        if (component.InsertedEntities.ContainsKey(entityProto))
        {
            Popup.PopupClient(Loc.GetString("advanced-rcd-board-already-installed"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Calculate material cost from board requirements using MachinePartSystem
        var materialCost = _machinePart.GetMachineBoardMaterialCost((args.Used, board));

        // Add base board cost (similar to FlatpackCreator's baseMachineCost)
        foreach (var (mat, amount) in component.BaseBoardCost)
        {
            var matId = mat.Id;
            materialCost.TryAdd(matId, 0);
            materialCost[matId] += amount;
        }

        // Insert the entity prototype from the board with calculated cost
        component.InsertedEntities[entityProto] = new InsertedBoardData(true, materialCost);

        // Consume the board
        if (Net.IsServer)
            QueueDel(args.Used);

        Popup.PopupClient(Loc.GetString("advanced-rcd-board-installed"), uid, args.User);
        Dirty(uid, component);
        args.Handled = true;
    }

    private void OnDoAfterAttempt(EntityUid uid, AdvancedRCDComponent component, DoAfterAttemptEvent<AdvancedRCDDoAfterEvent> args)
    {
        if (args.Event?.DoAfter?.Args == null)
            return;

        // Check if selection changed
        if (component.ProtoId != args.Event.ProtoId || component.SelectedEntityProto != args.Event.EntityProtoId)
        {
            args.Cancel();
            return;
        }

        var location = GetCoordinates(args.Event.Location);
        var gridUid = TransformSystem.GetGrid(location);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
        {
            args.Cancel();
            return;
        }

        var tile = MapSystem.GetTileRef(gridUid.Value, mapGrid, location);
        var position = MapSystem.TileIndicesFor(gridUid.Value, mapGrid, location);

        if (!IsOperationValid(uid, component, gridUid.Value, mapGrid, tile, position, args.Event.Target, args.Event.User, false))
            args.Cancel();
    }

    protected virtual void OnDoAfter(EntityUid uid, AdvancedRCDComponent component, AdvancedRCDDoAfterEvent args)
    {
        // Server-side implementation
    }

    #endregion

    #region Core Logic

    public bool TryInteract(EntityUid uid, EntityUid user, EntityUid? target, EntityCoordinates location, AdvancedRCDComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (!location.IsValid(EntityManager))
            return false;

        // Check what's selected
        AdvancedRCDPrototype? rcdProto = null;
        EntProtoId? entityProto = null;
        float delay;

        if (component.ProtoId != null)
        {
            if (!ProtoManager.TryIndex(component.ProtoId.Value, out rcdProto))
                return false;
            delay = rcdProto.Delay;
        }
        else if (component.SelectedEntityProto != null)
        {
            entityProto = component.SelectedEntityProto;
            delay = component.DefaultBoardDelay;
        }
        else
        {
            Popup.PopupClient(Loc.GetString("advanced-rcd-no-selection"), uid, user);
            return false;
        }

        var gridUid = TransformSystem.GetGrid(location);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
        {
            Popup.PopupClient(Loc.GetString("advanced-rcd-no-grid"), uid, user);
            return false;
        }

        var tile = MapSystem.GetTileRef(gridUid.Value, mapGrid, location);
        var position = MapSystem.TileIndicesFor(gridUid.Value, mapGrid, location);

        if (!IsOperationValid(uid, component, gridUid.Value, mapGrid, tile, position, target, user))
            return false;

        if (!Net.IsServer)
            return true;

        // Spawn construction effect
        var effect = Spawn(ConstructionEffectProto, location);

        // Start DoAfter
        var ev = new AdvancedRCDDoAfterEvent(
            GetNetCoordinates(location),
            component.ConstructionDirection,
            component.ProtoId,
            entityProto,
            GetNetEntity(effect));

        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay, ev, uid, target: target, used: uid)
        {
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = false,
            BlockDuplicate = false
        };

        if (!DoAfter.TryStartDoAfter(doAfterArgs))
            QueueDel(effect);

        return true;
    }

    public bool IsOperationValid(EntityUid uid, AdvancedRCDComponent component, EntityUid gridUid, MapGridComponent mapGrid, TileRef tile, Vector2i position, EntityUid? target, EntityUid user, bool popMsgs = true)
    {
        // Check range
        var unobstructed = target == null
            ? Interaction.InRangeUnobstructed(user, MapSystem.GridTileToWorld(gridUid, mapGrid, position), component.Range, popup: popMsgs)
            : Interaction.InRangeUnobstructed(user, target.Value, component.Range, popup: popMsgs);

        if (!unobstructed)
            return false;

        // RCD prototype mode
        if (component.ProtoId != null)
        {
            if (!ProtoManager.TryIndex(component.ProtoId.Value, out var rcdProto))
                return false;

            if (rcdProto.IsDeconstruct)
                return IsDeconstructValid(uid, component, tile, target, user, popMsgs);
            else
                return IsBuildValidRcd(uid, component, rcdProto, gridUid, mapGrid, tile, position, user, popMsgs);
        }

        // Entity prototype mode (from inserted board)
        if (component.SelectedEntityProto != null)
        {
            return IsBuildValidEntity(uid, component, component.SelectedEntityProto.Value, gridUid, mapGrid, tile, position, user, popMsgs);
        }

        return false;
    }

    private bool IsBuildValidRcd(EntityUid uid, AdvancedRCDComponent component, AdvancedRCDPrototype rcdProto, EntityUid gridUid, MapGridComponent mapGrid, TileRef tile, Vector2i position, EntityUid user, bool popMsgs = true)
    {
        // Check if we can afford it
        if (!CanAfford(uid, rcdProto))
        {
            if (popMsgs)
                Popup.PopupClient(Loc.GetString("advanced-rcd-insufficient-materials"), uid, user);
            return false;
        }

        return IsBuildLocationValid(uid, component, gridUid, tile, position, user, popMsgs);
    }

    private bool IsBuildValidEntity(EntityUid uid, AdvancedRCDComponent component, EntProtoId entityProto, EntityUid gridUid, MapGridComponent mapGrid, TileRef tile, Vector2i position, EntityUid user, bool popMsgs = true)
    {
        // Check if we can afford it using stored board cost
        if (!CanAffordEntity(uid, component, entityProto))
        {
            if (popMsgs)
                Popup.PopupClient(Loc.GetString("advanced-rcd-insufficient-materials"), uid, user);
            return false;
        }

        // Entities from boards can be built on empty tiles (CanBuildOnEmptyTile rule)
        return IsBuildLocationValidForBoards(uid, component, gridUid, tile, position, user, popMsgs);
    }

    private bool IsBuildLocationValid(EntityUid uid, AdvancedRCDComponent component, EntityUid gridUid, TileRef tile, Vector2i position, EntityUid user, bool popMsgs = true)
    {
        // Must build on valid tile (not empty space)
        if (tile.Tile.IsEmpty)
        {
            if (popMsgs)
                Popup.PopupClient(Loc.GetString("advanced-rcd-no-empty-tile"), uid, user);
            return false;
        }

        return CheckForObstructions(uid, gridUid, position, user, popMsgs);
    }

    private bool IsBuildLocationValidForBoards(EntityUid uid, AdvancedRCDComponent component, EntityUid gridUid, TileRef tile, Vector2i position, EntityUid user, bool popMsgs = true)
    {
        // Only check for obstructions (anchored entities)
        return CheckForObstructions(uid, gridUid, position, user, popMsgs);
    }

    private bool CheckForObstructions(EntityUid uid, EntityUid gridUid, Vector2i position, EntityUid user, bool popMsgs = true)
    {
        // Use TurfSystem.IsTileBlocked with Impassable collision mask
        if (Turf.IsTileBlocked(gridUid, position, CollisionGroup.Impassable))
        {
            if (popMsgs)
                Popup.PopupClient(Loc.GetString("advanced-rcd-tile-occupied"), uid, user);
            return false;
        }

        return true;
    }

    private bool IsDeconstructValid(EntityUid uid, AdvancedRCDComponent component, TileRef tile, EntityUid? target, EntityUid user, bool popMsgs = true)
    {
        if (target == null)
        {
            if (popMsgs)
                Popup.PopupClient(Loc.GetString("advanced-rcd-no-target"), uid, user);
            return false;
        }

        // Check if target has RCDDeconstructableComponent and is deconstructable
        if (!TryComp<RCDDeconstructableComponent>(target, out var deconstructible) || !deconstructible.Deconstructable)
        {
            if (popMsgs)
                Popup.PopupClient(Loc.GetString("advanced-rcd-cannot-deconstruct"), uid, user);
            return false;
        }

        return true;
    }

    #endregion

    #region Material Cost Calculation

    /// <summary>
    /// Checks if the RCD has enough materials to build using RCD prototype.
    /// </summary>
    public bool CanAfford(EntityUid uid, AdvancedRCDPrototype rcdProto)
    {
        if (rcdProto.MaterialCost.Count == 0)
            return true;

        var stored = MaterialStorage.GetStoredMaterials(uid);

        foreach (var (mat, required) in rcdProto.MaterialCost)
        {
            if (!stored.TryGetValue(mat, out var available) || available < required)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the RCD has enough materials to build an entity using stored board cost.
    /// </summary>
    public bool CanAffordEntity(EntityUid uid, AdvancedRCDComponent component, EntProtoId entityProto)
    {
        if (!component.InsertedEntities.TryGetValue(entityProto, out var boardData))
            return false;

        if (boardData.MaterialCost.Count == 0)
            return true;

        var stored = MaterialStorage.GetStoredMaterials(uid);

        foreach (var (mat, required) in boardData.MaterialCost)
        {
            // Convert string key to ProtoId for lookup
            ProtoId<MaterialPrototype> matProtoId = mat;
            if (!stored.TryGetValue(matProtoId, out var available) || available < required)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Gets material cost for an entity from stored board data.
    /// </summary>
    public Dictionary<string, int> GetEntityMaterialCost(AdvancedRCDComponent component, EntProtoId entityProto)
    {
        if (component.InsertedEntities.TryGetValue(entityProto, out var boardData))
            return boardData.MaterialCost;

        return new Dictionary<string, int>();
    }

    /// <summary>
    /// Consumes materials from storage for building using RCD prototype.
    /// </summary>
    public bool TryConsumeMaterials(EntityUid uid, AdvancedRCDPrototype rcdProto)
    {
        if (rcdProto.MaterialCost.Count == 0)
            return true;

        var consumption = new Dictionary<string, int>();
        foreach (var (mat, amount) in rcdProto.MaterialCost)
        {
            consumption[mat] = -amount;
        }

        return MaterialStorage.TryChangeMaterialAmount(uid, consumption);
    }

    /// <summary>
    /// Consumes materials from storage for building an entity using stored board cost.
    /// </summary>
    public bool TryConsumeMaterialsForEntity(EntityUid uid, AdvancedRCDComponent component, EntProtoId entityProto)
    {
        if (!component.InsertedEntities.TryGetValue(entityProto, out var boardData))
            return false;

        if (boardData.MaterialCost.Count == 0)
            return true;

        var consumption = new Dictionary<string, int>();
        foreach (var (mat, amount) in boardData.MaterialCost)
        {
            consumption[mat] = -amount;
        }

        return MaterialStorage.TryChangeMaterialAmount(uid, consumption);
    }

    #endregion
}

[Serializable, NetSerializable]
public sealed partial class AdvancedRCDDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Location { get; private set; }

    [DataField]
    public Direction Direction { get; private set; }

    [DataField]
    public ProtoId<AdvancedRCDPrototype>? ProtoId { get; private set; }

    [DataField]
    public EntProtoId? EntityProtoId { get; private set; }

    [DataField]
    public NetEntity Effect { get; private set; }

    private AdvancedRCDDoAfterEvent() { }

    public AdvancedRCDDoAfterEvent(NetCoordinates location, Direction direction, ProtoId<AdvancedRCDPrototype>? protoId, EntProtoId? entityProtoId, NetEntity effect)
    {
        Location = location;
        Direction = direction;
        ProtoId = protoId;
        EntityProtoId = entityProtoId;
        Effect = effect;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}
