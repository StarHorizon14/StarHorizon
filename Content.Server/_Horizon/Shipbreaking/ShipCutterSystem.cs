using System.Linq;
using Content.Server.PowerCell;
using Content.Shared._Horizon.Shipbreaking;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Horizon.Shipbreaking;

public sealed partial class ShipCutterSystem : SharedShipCutterSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GridCutsSystem _cuts = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipCutterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShipCutterComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ShipCutterComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ShipCutterComponent, ShipCutterDoAfterEvent>(OnDoAfter);
    }

    private void OnMapInit(Entity<ShipCutterComponent> ent, ref MapInitEvent args)
    {
        UpdateName(ent);
    }

    private void OnUseInHand(Entity<ShipCutterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        ent.Comp.Mode = ent.Comp.Mode == ShipToolMode.Cut ? ShipToolMode.Weld : ShipToolMode.Cut;
        Dirty(ent);
        UpdateName(ent);

        var changed = new ShipToolModeChangedEvent(ent.Comp.Mode);
        RaiseLocalEvent(ent, ref changed);

        _audio.PlayPvs(ent.Comp.ChangeSound, ent);
    }

    private void UpdateName(Entity<ShipCutterComponent> ent)
    {
        var cut = ent.Comp.Mode == ShipToolMode.Cut;
        _meta.SetEntityName(ent, Loc.GetString(cut ? ent.Comp.CutName : ent.Comp.WeldName));
        _meta.SetEntityDescription(ent, Loc.GetString(cut ? ent.Comp.CutDescription : ent.Comp.WeldDescription));

        var prefix = cut ? ent.Comp.CutHeldPrefix : ent.Comp.WeldHeldPrefix;
        if (prefix != null)
            _item.SetHeldPrefix(ent, prefix);
    }

    private void OnAfterInteract(Entity<ShipCutterComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || ent.Comp.Mode != ShipToolMode.Cut)
            return;

        var pick = Pick(TransformSystem.ToMapCoordinates(args.ClickLocation), ent.Comp);
        if (pick.Kind == ShipCutterPickKind.None)
            return;

        args.Handled = true;

        if (!pick.CanCut)
        {
            _popup.PopupEntity(Loc.GetString("ship-cutter-cannot-cut"), ent, args.User);
            return;
        }

        var target = pick.Kind == ShipCutterPickKind.Wall
            ? ShipCutterTarget.Wall(GetNetEntity(pick.Wall!.Value), pick.Edge)
            : ShipCutterTarget.Seam(GetNetEntity(pick.Grid), pick.Tile, pick.Edge);

        if (!TryResolve(ent.Comp, target, out var entity, out var delay, out var charge))
        {
            _popup.PopupEntity(Loc.GetString("ship-cutter-cannot-cut"), ent, args.User);
            return;
        }

        if (!_cell.HasCharge(ent, charge, user: args.User))
        {
            _popup.PopupEntity(Loc.GetString("ship-cutter-no-charge"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new ShipCutterDoAfterEvent(target), ent, target: entity, used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(ent.Comp.Sound, ent);
    }

    private bool TryResolve(ShipCutterComponent cutter, ShipCutterTarget target, out EntityUid entity, out float delay, out float charge)
    {
        entity = default;
        delay = 0f;
        charge = 0f;

        if (!TryGetEntity(target.Entity, out var uid) || TerminatingOrDeleted(uid.Value))
            return false;

        entity = uid.Value;

        if (target.IsWall)
        {
            if (!Transform(entity).Anchored || !Whitelist.IsWhitelistPass(cutter.WallWhitelist, entity) ||
                !TryGetWallTile(entity, out var wallGrid, out var wallGridComp, out var wallTile))
                return false;

            var held = WallHeldEdges(cutter, wallGrid, wallGridComp, wallTile, entity);
            if (target.Edge == CutEdges.None ? held != CutEdges.None : !held.HasFlag(target.Edge))
                return false;

            delay = cutter.WallDelay;
            charge = cutter.WallCharge;
            return true;
        }

        if (!TryComp<MapGridComponent>(entity, out var grid) || !CanCut(entity, grid, target.Tile, target.Edge))
            return false;

        delay = cutter.TileDelay;
        charge = cutter.TileCharge;
        return true;
    }

    private void OnDoAfter(Entity<ShipCutterComponent> ent, ref ShipCutterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryResolve(ent.Comp, args.Cut, out var entity, out _, out var charge))
            return;

        if (!_cell.TryUseCharge(ent, charge, user: args.User))
        {
            _popup.PopupEntity(Loc.GetString("ship-cutter-no-charge"), ent, args.User);
            return;
        }

        if (args.Cut.IsWall)
            CutWall(ent.Comp, entity, args.Cut.Edge);
        else
            CutSeam(entity, args.Cut.Tile, args.Cut.Edge);

        _audio.PlayPvs(ent.Comp.DoneSound, ent);
    }

    private bool TryGetWallTile(EntityUid wall, out EntityUid gridUid, out MapGridComponent grid, out Vector2i tile)
    {
        grid = default!;
        tile = default;
        gridUid = default;

        var xform = Transform(wall);
        if (xform.GridUid is not { } uid || !TryComp(uid, out MapGridComponent? gridComp))
            return false;

        gridUid = uid;
        grid = gridComp;
        tile = MapSystem.TileIndicesFor(uid, gridComp, xform.Coordinates);
        return true;
    }

    public void CutWall(ShipCutterComponent cutter, EntityUid wall, CutEdges edge)
    {
        if (edge != CutEdges.None && TryGetWallTile(wall, out var gridUid, out var grid, out var tile))
        {
            EnsureComp<WallCutsComponent>(wall).Cuts |= edge;
            Dirty(wall, Comp<WallCutsComponent>(wall));

            foreach (var other in MapSystem.GetAnchoredEntities((gridUid, grid), tile + edge.Offset()).ToList())
            {
                if (other == wall || !Whitelist.IsWhitelistPass(cutter.WallWhitelist, other))
                    continue;

                var otherCuts = EnsureComp<WallCutsComponent>(other);
                otherCuts.Cuts |= edge.Opposite();
                Dirty(other, otherCuts);
            }

            return;
        }

        CutLoose(wall);
    }

    private void CutLoose(EntityUid wall)
    {
        TransformSystem.Unanchor(wall);

        if (!TryComp<PhysicsComponent>(wall, out var body))
            return;

        _physics.SetBodyType(wall, BodyType.Dynamic, body: body);
        _physics.SetCanCollide(wall, true, body: body);
        _physics.WakeBody(wall, body: body);
    }

    public void CutSeam(EntityUid gridUid, Vector2i tile, CutEdges edge)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        _cuts.AddCut(gridUid, tile, edge);

        var other = tile + edge.Offset();
        TrySplit(gridUid, grid, Comp<GridCutsComponent>(gridUid), tile, other);
    }

    public void CutSeams(EntityUid gridUid, IReadOnlyList<(Vector2i Tile, CutEdges Edge)> seams)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        foreach (var (tile, edge) in seams)
        {
            _cuts.AddCut(gridUid, tile, edge);
        }

        foreach (var (tile, edge) in seams)
        {
            if (!TryComp<GridCutsComponent>(gridUid, out var cuts))
                break;

            TrySplit(gridUid, grid, cuts, tile, tile + edge.Offset());
        }
    }
}
