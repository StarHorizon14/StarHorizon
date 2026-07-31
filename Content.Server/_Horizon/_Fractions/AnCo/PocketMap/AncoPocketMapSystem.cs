using System.Numerics;
using Content.Shared._Horizon._Fractions.AnCo.PocketMap;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Timing;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;

namespace Content.Server._Horizon._Fractions.AnCo.PocketMap;

/// <summary>
/// Lets an item carrying <see cref="AncoPocketMapComponent"/> open a portal to a
/// private pocket map and then pull the user into it after a short delay.
/// </summary>
public sealed class AncoPocketMapSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    private const string EnterDelayId = "AncoPocketMapEnter";
    private const string ExitDelayId = "AncoPocketMapExit";
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AncoPocketMapComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AncoPocketMapComponent, ComponentRemove>(OnRemoved);
        SubscribeLocalEvent<AncoPocketMapComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AncoPocketMapComponent, AncoPocketMapEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<AncoPocketMapExitComponent, GetVerbsEvent<AlternativeVerb>>(OnGetExitVerbs);
        SubscribeLocalEvent<AncoPocketMapExitComponent, AncoPocketMapExitDoAfterEvent>(OnExitDoAfter);

        _sawmill = Logger.GetSawmill("anco_pocket_map");
    }

    private void OnStartup(EntityUid uid, AncoPocketMapComponent component, ComponentStartup args)
    {
        if (Deleted(component.PocketMap))
            TryCreatePocketMap(uid, component);
    }

    private void OnRemoved(EntityUid uid, AncoPocketMapComponent component, ComponentRemove args)
    {
        if (!Deleted(component.PocketMap))
            QueueDel(component.PocketMap!.Value);
    }

    private void OnGetVerbs(EntityUid uid, AncoPocketMapComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<HandsComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("anco-pocket-map-verb-text"),
            Disabled = !component.IsOpen,
            Act = () => StartEnter(uid, component, user)
        });
    }

    private void OnGetExitVerbs(EntityUid uid, AncoPocketMapExitComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<HandsComponent>(args.User))
            return;

        if (!TryComp(component.Source, out AncoPocketMapComponent? source) || !source.Occupants.Contains(args.User))
            return;

        if (Transform(args.User).MapID != Transform(uid).MapID)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("anco-pocket-map-exit-verb-text"),
            Act = () => StartExit(uid, component, args.User)
        });
    }

    private void StartEnter(EntityUid uid, AncoPocketMapComponent component, EntityUid user)
    {
        if (Deleted(user) || Deleted(uid))
            return;

        if (!component.IsOpen)
            return;

        if (component.PocketMap != null && component.LoadedMapPath != component.MapPath)
        {
            if (!Deleted(component.PocketMap))
                QueueDel(component.PocketMap.Value);

            component.PocketMap = null;
            component.InnerPortal = null;
        }

        if (Deleted(component.PocketMap) && !TryCreatePocketMap(uid, component))
            return;

        _audio.PlayPvs(component.EnterSound, uid);

        var delay = EnsureComp<UseDelayComponent>(user);
        _useDelay.SetLength((user, delay), component.EnterDelay, EnterDelayId);
        _useDelay.TryResetDelay((user, delay), id: EnterDelayId);

        var doAfter = new DoAfterArgs(EntityManager, user, component.EnterDelay, new AncoPocketMapEnterDoAfterEvent(), uid, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnEnterDoAfter(EntityUid uid, AncoPocketMapComponent component, AncoPocketMapEnterDoAfterEvent args)
    {
        if (TryComp(args.User, out UseDelayComponent? delayComp))
            _useDelay.CancelDelay((args.User, delayComp), EnterDelayId);

        if (args.Cancelled || args.Handled)
            return;

        if (Deleted(component.PocketMap) || Deleted(component.InnerPortal))
            return;

        component.Occupants.Add(args.User);

        var portalCoords = Transform(component.InnerPortal.Value).Coordinates;
        var entryCoords = portalCoords.Offset(new Vector2(component.TeleportOffsetX, component.TeleportOffsetY));

        _xform.SetCoordinates(args.User, entryCoords);
        args.Handled = true;
    }

    private void StartExit(EntityUid uid, AncoPocketMapExitComponent exit, EntityUid user)
    {
        if (Deleted(user) || Deleted(uid))
            return;

        if (!TryComp(exit.Source, out AncoPocketMapComponent? source))
            return;

        if (!source.Occupants.Contains(user))
            return;

        _audio.PlayPvs(source.EnterSound, uid);

        var delay = EnsureComp<UseDelayComponent>(user);
        _useDelay.SetLength((user, delay), source.EnterDelay, ExitDelayId);
        _useDelay.TryResetDelay((user, delay), id: ExitDelayId);

        var doAfter = new DoAfterArgs(EntityManager, user, source.EnterDelay, new AncoPocketMapExitDoAfterEvent(), uid, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnExitDoAfter(EntityUid uid, AncoPocketMapExitComponent exit, AncoPocketMapExitDoAfterEvent args)
    {
        if (TryComp(args.User, out UseDelayComponent? delayComp))
            _useDelay.CancelDelay((args.User, delayComp), ExitDelayId);

        if (args.Cancelled || args.Handled)
            return;

        if (!TryComp(exit.Source, out AncoPocketMapComponent? source))
            return;

        if (!source.Occupants.Remove(args.User))
            return;

        _xform.SetCoordinates(args.User, Transform(exit.Source).Coordinates);
        args.Handled = true;
    }

    private bool TryCreatePocketMap(EntityUid uid, AncoPocketMapComponent component)
    {
        var options = DeserializationOptions.Default with
        {
            InitializeMaps = true,
            PauseMaps = false,
        };

        if (!_mapLoader.TryLoadMap(component.MapPath, out var map, out var grids, options))
        {
            _sawmill.Error($"Failed to load pocket map {component.MapPath}");
            return false;
        }

        component.PocketMap = map.Value.Owner;
        component.LoadedMapPath = component.MapPath;

        foreach (var grid in grids)
        {
            var coords = new EntityCoordinates(grid.Owner, 0, 0);
            component.InnerPortal = Spawn(component.PortalPrototype, coords);
            EnsureComp<AncoPocketMapExitComponent>(component.InnerPortal.Value, out var exit);
            exit.Source = uid;

            // required so the first TryUnlink cleanup doesn't fail
            EnsureComp<LinkedEntityComponent>(uid);

            _sawmill.Info($"Created pocket map on grid {grid.Owner} of map {map.Value.Owner}");
            return true;
        }

        _sawmill.Error($"Pocket map {component.MapPath} had no grids!");
        QueueDel(component.PocketMap);
        component.PocketMap = null;
        component.InnerPortal = null;
        return false;
    }
}





