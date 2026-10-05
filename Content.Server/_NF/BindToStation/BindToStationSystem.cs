using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Nodes;
using Content.Server.Station.Systems;
using Content.Server.Construction.Components;
using Content.Shared._NF.BindToStation;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.NodeContainer;
using Robust.Server.Containers;

namespace Content.Server._NF.BindToStation;

public sealed class BindToStationSystem : EntitySystem
{
    [Dependency] private readonly ExtensionCableSystem _extensionCable = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationBoundObjectComponent, ExaminedEvent>(OnBoundItemExamined);
        SubscribeLocalEvent<StationBoundObjectComponent, MapInitEvent>(OnBoundMapInit);
        SubscribeLocalEvent<StationBoundObjectComponent, GotEmaggedEvent>(OnBoundEmagged);
        SubscribeLocalEvent<StationBoundObjectComponent, GotUnEmaggedEvent>(OnBoundUnemagged);

        // Horizon: machines wired directly into the HV power network (SMES, power transmission
        // points, etc.) don't have ExtensionCableReceiver, so nothing re-checks their binding when
        // they're re-anchored somewhere else. Cut their power the same way on every anchor change.
        SubscribeLocalEvent<StationBoundObjectComponent, AnchorStateChangedEvent>(OnBoundAnchorStateChanged);
        SubscribeLocalEvent<StationBoundObjectComponent, ReAnchorEvent>(OnBoundReAnchor);
    }

    // Horizon: re-run the node-disabling check below whenever a bound entity's anchor state changes.
    private void OnBoundAnchorStateChanged(Entity<StationBoundObjectComponent> ent, ref AnchorStateChangedEvent args)
    {
        DisableOffGridCableDevices(ent, ent.Comp.BoundStation, ent.Comp.Enabled);
    }

    // Horizon
    private void OnBoundReAnchor(Entity<StationBoundObjectComponent> ent, ref ReAnchorEvent args)
    {
        DisableOffGridCableDevices(ent, ent.Comp.BoundStation, ent.Comp.Enabled);
    }

    private void OnBoundItemExamined(EntityUid uid, StationBoundObjectComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || component.BoundStation == null || !component.Enabled)
            return;

        var stationName = TryComp(component.BoundStation, out MetaDataComponent? meta) ? meta.EntityName : Loc.GetString("bound-to-grid-unknown-station");
        args.PushMarkup(Loc.GetString("bound-to-grid-examine-text", ("shipname", stationName)));
    }

    // Ensure consistency for station-bound machines
    public void OnBoundMapInit(Entity<StationBoundObjectComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Enabled
            && TryComp<ExtensionCableReceiverComponent>(ent.Owner, out var receiver)
            && _station.GetOwningStation(ent.Owner) != ent.Comp.BoundStation)
        {
            _extensionCable.Disconnect((ent.Owner, receiver));
        }

        // Horizon
        DisableOffGridCableDevices(ent.Owner, ent.Comp.BoundStation, ent.Comp.Enabled);
    }

    public void OnBoundEmagged(Entity<StationBoundObjectComponent> ent, ref GotEmaggedEvent args)
    {
        // Don't check handled - machines may be emagged separately by other types.
        if (!args.Type.HasFlag(EmagType.StationBound))
            return;

        if (TryComp<EmaggedComponent>(ent, out var emagged) && emagged.EmagType.HasFlag(EmagType.StationBound))
            return;

        // Already disabled or not bound.
        if (!ent.Comp.Enabled || ent.Comp.BoundStation == null)
            return;

        // Disable the machine binding, leave the repeatable field as-is in case other machines set it.
        BindToStation(ent, ent.Comp.BoundStation, false);
        args.Handled = true;
    }

    public void OnBoundUnemagged(Entity<StationBoundObjectComponent> ent, ref GotUnEmaggedEvent args)
    {
        // Don't check handled - machines may be emagged separately by other types.
        if (!args.Type.HasFlag(EmagType.StationBound))
            return;

        if (!TryComp<EmaggedComponent>(ent, out var emagged) || !emagged.EmagType.HasFlag(EmagType.StationBound))
            return;

        // Already enabled or not bound (enabling does nothing).
        if (ent.Comp.Enabled || ent.Comp.BoundStation == null)
            return;

        // Re-enable the machine binding, leave the repeatable field as-is in case other machines set it.
        BindToStation(ent, ent.Comp.BoundStation, true);
        args.Handled = true;
    }

    /// <summary>
    /// Binds a given machine to a particular station - the machine will only work when on a grid belonging to that station.
    /// </summary>
    /// <param name="target">The item to be associated with the station.</param>
    /// <param name="station">The station to bind the grid to. If null, unbinds the machine.</param>
    public void BindToStation(EntityUid target, EntityUid? station, bool enabled = true)
    {
        var binding = EnsureComp<StationBoundObjectComponent>(target);
        binding.BoundStation = station;
        binding.Enabled = enabled;

        // If this receives power, adjust powered status depending on bound station
        if (TryComp<ExtensionCableReceiverComponent>(target, out var receiver))
        {
            if ((!enabled
                || _station.GetOwningStation(target) == station
                || station == null)
                && TryComp(target, out TransformComponent? xform)
                && xform.Anchored)
            {
                _extensionCable.Connect((target, receiver));
            }
            else
            {
                _extensionCable.Disconnect((target, receiver));
            }
        }

        // If this is a machine with a board, also make sure the binding is applied to the contained board too
        if (HasComp<MachineComponent>(target) && _container.TryGetContainer(target, MachineFrameComponent.BoardContainerName, out var mboardContainer))
        {
            foreach (var board in mboardContainer.ContainedEntities)
            {
                BindToStation(board, binding.BoundStation, binding.Enabled);
            }
        }
        // Repeat for computers and their boards
        if (HasComp<ComputerComponent>(target) && _container.TryGetContainer(target, "board", out var cboardContainer))
        {
            foreach (var board in cboardContainer.ContainedEntities)
            {
                BindToStation(board, binding.BoundStation, binding.Enabled);
            }
        }

        // Horizon
        DisableOffGridCableDevices(target, station, enabled);
    }

    /// <summary>
    /// Horizon: cuts power to any CableDeviceNode on this entity if it's bound to a station but
    /// currently sitting on a different one - the HV-network equivalent of what ExtensionCableSystem
    /// already does for ExtensionCableReceiver. Only ever forces nodes off, never back on, so it
    /// doesn't fight whatever normally turns a given device's power draw on and off.
    /// </summary>
    private void DisableOffGridCableDevices(EntityUid target, EntityUid? station, bool enabled)
    {
        var offBoundGrid = enabled && station != null && _station.GetOwningStation(target) != station;
        if (!offBoundGrid || !TryComp<NodeContainerComponent>(target, out var nodeContainer))
            return;

        foreach (var node in nodeContainer.Nodes.Values)
        {
            if (node is not CableDeviceNode { Enabled: true } deviceNode)
                continue;

            deviceNode.Enabled = false;
            _nodeGroup.QueueNodeRemove(deviceNode);
        }
    }
}
