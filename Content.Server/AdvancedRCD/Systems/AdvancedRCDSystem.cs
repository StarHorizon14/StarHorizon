using Content.Server.Administration.Logs;
using Content.Shared._Horizon.RCD;
using Content.Shared.AdvancedRCD;
using Content.Shared.AdvancedRCD.Components;
using Content.Shared.AdvancedRCD.Systems;
using Content.Shared.Database;
using Content.Shared.Materials;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.AdvancedRCD.Systems;

public sealed class AdvancedRCDSystem : SharedAdvancedRCDSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvancedRCDComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AdvancedRCDComponent, MaterialEntityInsertedEvent>(OnMaterialInserted);
        SubscribeLocalEvent<AdvancedRCDComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    private void OnUiOpened(EntityUid uid, AdvancedRCDComponent component, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnMapInit(EntityUid uid, AdvancedRCDComponent component, MapInitEvent args)
    {
        // Select first available prototype if none selected
        if (component.ProtoId == null && component.AvailablePrototypes.Count > 0)
        {
            foreach (var protoId in component.AvailablePrototypes)
            {
                component.ProtoId = protoId;
                break;
            }
        }

        UpdateUi(uid, component);
    }

    private void OnMaterialInserted(EntityUid uid, AdvancedRCDComponent component, ref MaterialEntityInsertedEvent args)
    {
        // Enforce per-material limit
        if (component.MaterialLimit != null && TryComp<MaterialStorageComponent>(uid, out var storage))
        {
            foreach (var (mat, amount) in storage.Storage)
            {
                if (amount > component.MaterialLimit.Value)
                {
                    var excess = amount - component.MaterialLimit.Value;
                    MaterialStorage.TryChangeMaterialAmount(uid, mat, -excess, storage);
                }
            }
        }

        UpdateUi(uid, component);
    }

    protected override void OnDoAfter(EntityUid uid, AdvancedRCDComponent component, AdvancedRCDDoAfterEvent args)
    {
        // Delete the construction effect
        var effect = GetEntity(args.Effect);
        if (effect != EntityUid.Invalid)
            QueueDel(effect);

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var location = GetCoordinates(args.Location);
        var gridUid = TransformSystem.GetGrid(location);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        var tile = MapSystem.GetTileRef(gridUid.Value, mapGrid, location);
        var position = MapSystem.TileIndicesFor(gridUid.Value, mapGrid, location);

        var success = false;

        // Handle RCD prototype mode
        if (args.ProtoId != null)
        {
            if (!ProtoManager.TryIndex(args.ProtoId.Value, out var rcdProto))
                return;

            // Verify operation is still valid
            if (!IsOperationValid(uid, component, gridUid.Value, mapGrid, tile, position, args.Target, args.User))
                return;

            if (rcdProto.IsDeconstruct)
            {
                FinalizeDeconstruction(uid, component, args.Target, args.User);
            }
            else
            {
                FinalizeConstructionRcd(uid, component, rcdProto, gridUid.Value, mapGrid, position, args.Direction, args.User);
            }
            success = true;
        }
        // Handle entity prototype mode (from inserted board)
        else if (args.EntityProtoId != null)
        {
            // Verify operation is still valid
            if (!IsOperationValid(uid, component, gridUid.Value, mapGrid, tile, position, args.Target, args.User))
                return;

            FinalizeConstructionEntity(uid, component, args.EntityProtoId.Value, gridUid.Value, mapGrid, position, args.Direction, args.User);
            success = true;
        }

        if (success)
        {
            // Play success sound
            _audio.PlayPvs(component.SuccessSound, uid);

            // Raise placement finished event
            var ev = new RCDPlacementFinishedEvent();
            RaiseLocalEvent(uid, ref ev);
        }

        UpdateUi(uid, component);
    }

    private void FinalizeConstructionRcd(EntityUid uid, AdvancedRCDComponent component, AdvancedRCDPrototype rcdProto, EntityUid gridUid, MapGridComponent mapGrid, Vector2i position, Direction direction, EntityUid user)
    {
        if (rcdProto.Prototype == null)
            return;

        // Consume materials
        if (!TryConsumeMaterials(uid, rcdProto))
            return;

        // Spawn the entity
        var coords = MapSystem.GridTileToLocal(gridUid, mapGrid, position);
        var ent = Spawn(rcdProto.Prototype.Value, coords);

        // Set rotation
        var xform = Transform(ent);
        xform.LocalRotation = direction.ToAngle();

        _adminLogger.Add(LogType.RCD, LogImpact.High,
            $"{ToPrettyString(user):user} used Advanced RCD to spawn {ToPrettyString(ent)} at {position} on grid {gridUid}");
    }

    private void FinalizeConstructionEntity(EntityUid uid, AdvancedRCDComponent component, EntProtoId entityProto, EntityUid gridUid, MapGridComponent mapGrid, Vector2i position, Direction direction, EntityUid user)
    {
        // Consume materials using stored board cost
        if (!TryConsumeMaterialsForEntity(uid, component, entityProto))
            return;

        // Spawn the entity
        var coords = MapSystem.GridTileToLocal(gridUid, mapGrid, position);
        var ent = Spawn(entityProto, coords);

        // Set rotation
        var xform = Transform(ent);
        xform.LocalRotation = direction.ToAngle();

        _adminLogger.Add(LogType.RCD, LogImpact.High,
            $"{ToPrettyString(user):user} used Advanced RCD (board) to spawn {ToPrettyString(ent)} at {position} on grid {gridUid}");
    }

    private void FinalizeDeconstruction(EntityUid uid, AdvancedRCDComponent component, EntityUid? target, EntityUid user)
    {
        if (target == null)
            return;

        _adminLogger.Add(LogType.RCD, LogImpact.High,
            $"{ToPrettyString(user):user} used Advanced RCD to delete {ToPrettyString(target):target}");

        QueueDel(target);
    }

    private void UpdateUi(EntityUid uid, AdvancedRCDComponent component)
    {
        var categories = new Dictionary<string, List<(string ProtoId, bool CanAfford)>>();

        foreach (var protoId in component.AvailablePrototypes)
        {
            if (!ProtoManager.TryIndex(protoId, out var rcdProto))
                continue;

            var category = rcdProto.Category;
            if (!categories.ContainsKey(category))
                categories[category] = new List<(string ProtoId, bool CanAfford)>();

            var canAfford = CanAfford(uid, rcdProto);
            categories[category].Add((protoId.Id, canAfford));
        }

        var state = new AdvancedRCDBuiState(
            categories,
            component.ProtoId?.Id);

        _ui.SetUiState(uid, AdvancedRCDUiKey.Key, state);
    }
}
