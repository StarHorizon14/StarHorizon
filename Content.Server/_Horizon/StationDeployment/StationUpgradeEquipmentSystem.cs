using Content.Server._Horizon.StationDeployment.Components;
using Content.Server._NF.Tools.Components;
using Content.Server.Power.Components;
using Content.Server.Power.Nodes;
using Content.Server.Station.Systems;
using Content.Shared._Horizon.StationDeployment.Components;
using Content.Shared._NF.BindToStation;
using Content.Shared.Interaction;
using Content.Shared.NodeContainer;
using Content.Shared.Popups;
using Content.Shared.StationRecords;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Horizon.StationDeployment;

/// <summary>
/// Handles activating purchased station upgrade equipment by swiping the station owner's ID card
/// on it - anchors it and turns it on. Server-side because it needs to verify station ownership,
/// which the client shouldn't be trusted to check.
/// </summary>
public sealed class StationUpgradeEquipmentSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationUpgradeEquipmentComponent, AfterInteractUsingEvent>(OnIdCardSwipe);
        SubscribeLocalEvent<StationUpgradeEquipmentComponent, MapInitEvent>(OnMapInit);
    }

    // Some of the underlying prototypes disable tool use (anti-theft on their vanilla usage) - that's
    // only safe to lift when something keeps re-validating StationBoundObjectComponent as the entity
    // gets moved around: ExtensionCableSystem does this for ExtensionCableReceiver, and
    // BindToStationSystem does the equivalent for HV-network machines (CableDeviceNode) now too.
    private void OnMapInit(Entity<StationUpgradeEquipmentComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<ExtensionCableReceiverComponent>(ent.Owner) || HasCableDeviceNode(ent.Owner))
            RemComp<DisableToolUseComponent>(ent.Owner);
    }

    private bool HasCableDeviceNode(EntityUid uid)
    {
        if (!TryComp<NodeContainerComponent>(uid, out var nodeContainer))
            return false;

        foreach (var node in nodeContainer.Nodes.Values)
        {
            if (node is CableDeviceNode)
                return true;
        }

        return false;
    }

    private void OnIdCardSwipe(Entity<StationUpgradeEquipmentComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || ent.Comp.Installed)
            return;

        var idCardUid = args.Used;
        if (!TryComp<StationRecordKeyStorageComponent>(idCardUid, out _))
            return;

        args.Handled = true;

        if (!TryComp<StationBoundObjectComponent>(ent, out var bound) ||
            bound.BoundStation is not { Valid: true } boundStation ||
            _station.GetOwningStation(ent.Owner) != boundStation)
        {
            _popup.PopupEntity(Loc.GetString("station-upgrade-equipment-wrong-grid"), ent, args.User, PopupType.MediumCaution);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent);
            return;
        }

        if (!TryComp<StationDeedComponent>(idCardUid, out var deed) || deed.StationUid != boundStation)
        {
            _popup.PopupEntity(Loc.GetString("station-upgrade-equipment-not-owner"), ent, args.User, PopupType.MediumCaution);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent);
            return;
        }

        ent.Comp.Installed = true;

        // Most of the underlying prototypes (structures/computers) already spawn anchored - only
        // anchor if it isn't, since re-anchoring an already-anchored entity asserts/crashes trying to
        // add it to the grid's snap cell a second time.
        var xform = Transform(ent.Owner);
        if (!xform.Anchored)
            _transform.AnchorEntity(ent.Owner, xform);

        _popup.PopupEntity(Loc.GetString("station-upgrade-equipment-installed"), ent, args.User, PopupType.Medium);
        _audio.PlayPvs(ent.Comp.ActivateSound, ent);
    }
}
