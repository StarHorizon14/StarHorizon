using System.Numerics;
using Content.Shared._Horizon.Shipbreaking;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Horizon.Shipbreaking;

/// <summary>
/// Handles <see cref="BreachChargeComponent"/>: hanging a charge on the edge of a tile, and cutting a line of seams
/// along that edge when the fuse is out.
/// </summary>
public sealed class BreachChargeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedShipCutterSystem _seams = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShipCutterSystem _cutter = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreachChargeComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BreachChargeComponent, BreachChargePlaceDoAfterEvent>(OnPlaced);
        SubscribeLocalEvent<ArmedBreachChargeComponent, TriggerEvent>(OnTrigger);
    }

    private void OnAfterInteract(Entity<BreachChargeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // The edge nearest to the click. A wall on the tile does not matter, only the floor does.
        if (!_seams.TryGetSeam(_transform.ToMapCoordinates(args.ClickLocation), ent.Comp.EdgeTolerance,
                out var gridUid, out var grid, out var tile, out var edge))
            return;

        args.Handled = true;

        if (!_seams.CanCut(gridUid, grid, tile, edge))
        {
            _popup.PopupEntity(Loc.GetString("breach-charge-cannot-place"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.PlaceDelay,
            new BreachChargePlaceDoAfterEvent(GetNetEntity(gridUid), tile, edge), ent, used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPlaced(Entity<BreachChargeComponent> ent, ref BreachChargePlaceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var gridUid = GetEntity(args.Grid);
        if (!TryComp<MapGridComponent>(gridUid, out var grid) || !_seams.CanCut(gridUid, grid, args.Tile, args.Edge))
        {
            _popup.PopupEntity(Loc.GetString("breach-charge-cannot-place"), ent, args.User);
            return;
        }

        // Hang it in the middle of the edge, lying along it.
        var size = grid.TileSize;
        var centre = new Vector2(args.Tile.X + 0.5f, args.Tile.Y + 0.5f) + new Vector2(args.Edge.Offset().X, args.Edge.Offset().Y) * 0.5f;
        var armed = Spawn(ent.Comp.ArmedPrototype, new EntityCoordinates(gridUid, centre * size));
        _transform.SetLocalRotation(armed, new Vector2(args.Edge.Along().X, args.Edge.Along().Y).ToAngle());

        var comp = EnsureComp<ArmedBreachChargeComponent>(armed);
        comp.Grid = gridUid;
        comp.Tile = args.Tile;
        comp.Edge = args.Edge;
        comp.Segments = ent.Comp.Segments;

        // The prototype has the timer trigger that counts the fuse down.
        _trigger.ActivateTimerTrigger(armed, args.User);

        _audio.PlayPvs(ent.Comp.PlaceSound, armed);
        QueueDel(ent);
    }

    /// <summary>
    /// Cuts the seams of a line along the edge, centred on the charge. Walls and furniture are not touched, only the floor.
    /// </summary>
    private void OnTrigger(Entity<ArmedBreachChargeComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        args.Handled = true;
        Detonate(ent);
    }

    private void Detonate(Entity<ArmedBreachChargeComponent> ent)
    {
        var armed = ent.Comp;
        var gridUid = armed.Grid;

        // If the grid was split since, the charge is on another one and the line is gone with its place.
        if (TryComp<MapGridComponent>(gridUid, out var grid) && Transform(ent).GridUid == gridUid)
        {
            var along = armed.Edge.Along();
            var first = -(armed.Segments - 1) / 2;
            var seams = new List<(Vector2i, CutEdges)>(armed.Segments);

            for (var k = 0; k < armed.Segments; k++)
            {
                var tile = armed.Tile + along * (first + k);
                if (_seams.CanCut(gridUid, grid, tile, armed.Edge))
                    seams.Add((tile, armed.Edge));
            }

            _cutter.CutSeams(gridUid, seams);
        }

        QueueDel(ent);
    }
}
