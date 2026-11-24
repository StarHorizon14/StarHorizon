using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Shared._Horizon.Administration.RtsControl;
using Content.Shared.Administration;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.Administration.RtsControl;

/// <summary>
///     Admin RTS-style control: drag to select creatures, then order them to move to a point.
///     Enabled with the <c>rtscontrol</c> command.
/// </summary>
public sealed class RtsControlSystem : EntitySystem
{
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float ClickPickRadius = 0.5f;
    private const float MinDragSize = 0.2f;
    private static readonly TimeSpan MarkerDuration = TimeSpan.FromSeconds(1.5);

    public bool Enabled { get; private set; }

    public readonly HashSet<EntityUid> Selected = new();

    /// <summary>World position where the current drag-selection started, if dragging.</summary>
    public MapCoordinates? DragStart { get; private set; }

    public MapCoordinates? OrderMarker { get; private set; }
    public TimeSpan OrderMarkerExpires { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        _admin.AdminStatusUpdated += OnAdminStatusUpdated;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _admin.AdminStatusUpdated -= OnAdminStatusUpdated;
        Disable();
    }

    private void OnAdminStatusUpdated()
    {
        if (Enabled && !_admin.HasFlag(AdminFlags.Fun))
            Disable();
    }

    /// <returns>New state, or null if the mode could not be enabled.</returns>
    public bool? Toggle()
    {
        if (Enabled)
        {
            Disable();
            return false;
        }

        if (!_admin.HasFlag(AdminFlags.Fun))
            return null;

        Enable();
        return true;
    }

    private void Enable()
    {
        if (Enabled)
            return;

        Enabled = true;

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.AdminRtsSelect,
                new PointerStateInputCmdHandler(OnSelectDown, OnSelectUp, outsidePrediction: true))
            .Bind(ContentKeyFunctions.AdminRtsOrder,
                new PointerInputCmdHandler(OnOrder, outsidePrediction: true))
            // Ctrl+click is also bound to pulling, so swallow those while RTS mode is on.
            .BindBefore(ContentKeyFunctions.TryPullObject,
                new PointerInputCmdHandler((_, _, _) => true, outsidePrediction: true),
                typeof(SharedInteractionSystem))
            .Bind(ContentKeyFunctions.MovePulledObject,
                new PointerInputCmdHandler((_, _, _) => true, outsidePrediction: true))
            .Register<RtsControlSystem>();

        _overlay.AddOverlay(new RtsSelectionOverlay(this));
    }

    private void Disable()
    {
        if (!Enabled)
            return;

        Enabled = false;
        CommandBinds.Unregister<RtsControlSystem>();
        _overlay.RemoveOverlay<RtsSelectionOverlay>();

        Selected.Clear();
        DragStart = null;
        OrderMarker = null;
    }

    private bool OnSelectDown(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        DragStart = _xform.ToMapCoordinates(coords);
        return true;
    }

    private bool OnSelectUp(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (DragStart is not { } start)
            return false;

        DragStart = null;

        var end = _xform.ToMapCoordinates(coords);
        if (start.MapId != end.MapId)
            return true;

        var min = Vector2.Min(start.Position, end.Position);
        var max = Vector2.Max(start.Position, end.Position);

        // A plain click picks the creature under the cursor.
        if (max.X - min.X < MinDragSize && max.Y - min.Y < MinDragSize)
        {
            var center = (min + max) / 2f;
            min = center - new Vector2(ClickPickRadius);
            max = center + new Vector2(ClickPickRadius);
        }

        var found = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesIntersecting(end.MapId, new Box2(min, max), found);

        Selected.Clear();
        var self = _player.LocalEntity;
        foreach (var ent in found)
        {
            if (ent.Owner != self)
                Selected.Add(ent.Owner);
        }

        return true;
    }

    private bool OnOrder(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        Selected.RemoveWhere(e => !Exists(e) || Terminating(e));
        if (Selected.Count == 0)
            return false;

        var units = new List<NetEntity>(Selected.Count);
        foreach (var unit in Selected)
        {
            units.Add(GetNetEntity(unit));
        }

        RaiseNetworkEvent(new RtsMoveOrderEvent(units, GetNetCoordinates(coords)));

        OrderMarker = _xform.ToMapCoordinates(coords);
        OrderMarkerExpires = _timing.CurTime + MarkerDuration;
        return true;
    }
}
