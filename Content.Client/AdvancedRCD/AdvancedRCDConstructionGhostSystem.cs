using Content.Client.Hands.Systems;
using Content.Shared.AdvancedRCD;
using Content.Shared.AdvancedRCD.Components;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.AdvancedRCD;

/// <summary>
/// System for handling structure ghost placement for Advanced RCD.
/// </summary>
public sealed class AdvancedRCDConstructionGhostSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlacementManager _placementManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    private string _placementMode = nameof(AlignAdvancedRCDConstruction);
    private Direction _placementDirection = default;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Get current placer data
        var placerEntity = _placementManager.CurrentPermission?.MobUid;
        var placerProto = _placementManager.CurrentPermission?.EntityType;
        var placerIsAdvancedRCD = HasComp<AdvancedRCDComponent>(placerEntity);

        // Exit if erasing or the current placer is not an Advanced RCD
        if (_placementManager.Eraser || (placerEntity != null && !placerIsAdvancedRCD))
            return;

        // Determine if player is carrying an Advanced RCD in their active hand
        if (_playerManager.LocalSession?.AttachedEntity is not { } player)
            return;

        var heldEntity = _hands.GetActiveItem(player);

        if (!TryComp<AdvancedRCDComponent>(heldEntity, out var advRcd))
        {
            // If the player was holding an Advanced RCD, but is no longer, cancel placement
            if (placerIsAdvancedRCD)
                _placementManager.Clear();

            return;
        }

        // Determine the entity prototype to show as ghost
        string? entityProtoId = null;

        // Check if we have a selected RCD prototype
        if (advRcd.ProtoId != null)
        {
            if (!_prototypeManager.TryIndex<AdvancedRCDPrototype>(advRcd.ProtoId.Value, out var rcdProto))
            {
                if (placerIsAdvancedRCD)
                    _placementManager.Clear();
                return;
            }

            // Don't show ghost for deconstruct mode
            if (rcdProto.IsDeconstruct || rcdProto.Prototype == null)
            {
                if (placerIsAdvancedRCD)
                    _placementManager.Clear();
                return;
            }

            entityProtoId = rcdProto.Prototype.Value.Id;
        }
        // Check if we have a selected entity prototype from inserted boards
        else if (advRcd.SelectedEntityProto != null)
        {
            entityProtoId = advRcd.SelectedEntityProto.Value.Id;
        }
        else
        {
            // No selection, clear placement
            if (placerIsAdvancedRCD)
                _placementManager.Clear();
            return;
        }

        // Update the direction based on the placer direction
        if (_placementDirection != _placementManager.Direction)
        {
            _placementDirection = _placementManager.Direction;
            // Direction is handled via network events
        }

        // If the placer has not changed, exit
        if (heldEntity == placerEntity && entityProtoId == placerProto)
            return;

        // Create a new placer
        var newObjInfo = new PlacementInformation
        {
            MobUid = heldEntity.Value,
            PlacementOption = _placementMode,
            EntityType = entityProtoId,
            Range = (int)Math.Ceiling(advRcd.Range),
            IsTile = false,
            UseEditorContext = false,
        };

        _placementManager.Clear();
        _placementManager.BeginPlacing(newObjInfo);
    }
}
