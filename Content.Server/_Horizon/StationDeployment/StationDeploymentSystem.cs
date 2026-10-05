using System.Numerics;
using Content.Shared._Horizon.StationDeployment.Components;
using Content.Shared._Horizon.StationDeployment.Events;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Server.Maps;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Horizon.StationDeployment;

/// <summary>
/// Handles deploying a new station grid from a <see cref="StationDeploymentKitComponent"/> - position/proximity
/// validation (mirrors the salvage expedition console's proximity check), the deploy DoAfter, loading the grid
/// and registering it as a real station, and writing the resulting deed onto the linked ID card.
/// </summary>
public sealed class StationDeploymentSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    /// <summary>
    /// IFF color shipyard-purchased vessels get (see BaseVessel in _NF/Shipyard/Base/base.yml) -
    /// deployed stations should look the same on radar.
    /// </summary>
    private static readonly Color StationIFFColor = Color.White;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationDeploymentKitComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<StationDeploymentKitComponent, DoAfterAttemptEvent<StationDeploymentDoAfterEvent>>(OnDeployAttempt);
        SubscribeLocalEvent<StationDeploymentKitComponent, StationDeploymentDoAfterEvent>(OnDeployDoAfter);
    }

    private void OnUseInHand(Entity<StationDeploymentKitComponent> kit, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (kit.Comp.LinkedIdCard == null)
        {
            _popup.PopupEntity(Loc.GetString("station-deployment-kit-not-linked"), kit.Owner, args.User, PopupType.Medium);
            _audio.PlayPvs(kit.Comp.ErrorSound, kit.Owner);
            return;
        }

        if (!ValidateDeployPosition(args.User, kit.Comp, out var denyLocKey))
        {
            _popup.PopupEntity(Loc.GetString(denyLocKey!), args.User, args.User, PopupType.MediumCaution);
            _audio.PlayPvs(kit.Comp.ErrorSound, kit.Owner);
            args.Handled = true;
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, kit.Comp.DeployDelay, new StationDeploymentDoAfterEvent(), kit.Owner, used: kit.Owner)
        {
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnDeployAttempt(Entity<StationDeploymentKitComponent> kit, ref DoAfterAttemptEvent<StationDeploymentDoAfterEvent> args)
    {
        if (!ValidateDeployPosition(args.DoAfter.Args.User, kit.Comp, out _))
            args.Cancel();
    }

    private void OnDeployDoAfter(Entity<StationDeploymentKitComponent> kit, ref StationDeploymentDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var user = args.User;

        if (!ValidateDeployPosition(user, kit.Comp, out var denyLocKey))
        {
            _popup.PopupEntity(Loc.GetString(denyLocKey!), user, user, PopupType.MediumCaution);
            _audio.PlayPvs(kit.Comp.ErrorSound, kit.Owner);
            return;
        }

        DoDeploy(user, kit);
    }

    /// <summary>
    /// Not on a grid, not on a planet, not too close to other grids - adapted from
    /// SalvageSystem.ExpeditionConsole's proximity check, but centered on the user's bare position
    /// rather than an existing ship grid.
    /// </summary>
    private bool ValidateDeployPosition(EntityUid user, StationDeploymentKitComponent kit, out string? denyLocKey)
    {
        var xform = Transform(user);

        if (xform.GridUid != null)
        {
            denyLocKey = "station-deployment-on-grid";
            return false;
        }

        if (xform.MapUid != null && HasComp<MapGridComponent>(xform.MapUid))
        {
            denyLocKey = "station-deployment-on-planet";
            return false;
        }

        var userWorldPos = _transform.GetWorldPosition(user);
        var bounds = new Box2(userWorldPos - new Vector2(kit.DeployRange), userWorldPos + new Vector2(kit.DeployRange));
        var otherGrids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(xform.MapID, bounds, ref otherGrids);

        var bodyQuery = GetEntityQuery<PhysicsComponent>();
        foreach (var otherGrid in otherGrids)
        {
            if (!bodyQuery.TryGetComponent(otherGrid.Owner, out var body) ||
                (body.Mass < kit.MinShipMass && body.BodyType == BodyType.Dynamic))
            {
                continue;
            }

            denyLocKey = "station-deployment-proximity";
            return false;
        }

        denyLocKey = null;
        return true;
    }

    private void DoDeploy(EntityUid user, Entity<StationDeploymentKitComponent> kit)
    {
        var xform = Transform(user);
        var worldPos = _transform.GetWorldPosition(user);

        if (!_mapLoader.TryLoadGrid(xform.MapID, kit.Comp.StationGridPath, out var gridEnt, offset: worldPos))
        {
            _popup.PopupEntity(Loc.GetString("station-deployment-failed"), user, user, PopupType.MediumCaution);
            return;
        }

        if (!_prototypeManager.TryIndex<GameMapPrototype>(kit.Comp.StationGameMapId, out var mapProto) ||
            !mapProto.Stations.TryGetValue(kit.Comp.StationConfigKey, out var stationConfig))
        {
            QueueDel(gridEnt.Value.Owner);
            _popup.PopupEntity(Loc.GetString("station-deployment-failed"), user, user, PopupType.MediumCaution);
            return;
        }

        // Don't pass a name here - leave it to the station's own StationNameSetup (on its prototype)
        // to generate the random name/code, same as how shipyard-purchased shuttles get their suffix.
        var station = _station.InitializeNewStation(stationConfig, new[] { gridEnt.Value.Owner });

        var fullName = MetaData(station).EntityName;
        ParseStationName(fullName, out var baseName, out var number);

        var idCard = kit.Comp.LinkedIdCard!.Value;
        var idDeed = EnsureComp<StationDeedComponent>(idCard);
        idDeed.StationUid = station;
        idDeed.StationGridUid = gridEnt.Value.Owner;
        idDeed.StationName = baseName;
        idDeed.StationNumber = number;
        Dirty(idCard, idDeed);

        var gridDeed = EnsureComp<StationDeedComponent>(gridEnt.Value.Owner);
        gridDeed.StationUid = station;
        gridDeed.StationGridUid = gridEnt.Value.Owner;
        gridDeed.StationName = baseName;
        gridDeed.StationNumber = number;
        Dirty(gridEnt.Value.Owner, gridDeed);

        var iff = EnsureComp<IFFComponent>(gridEnt.Value.Owner);
        _shuttle.SetIFFColor(gridEnt.Value.Owner, StationIFFColor, iff);
        _shuttle.AddIFFFlag(gridEnt.Value.Owner, IFFFlags.IsPlayerShuttle, iff);

        _popup.PopupEntity(Loc.GetString("station-deployment-success", ("name", fullName)), user, user, PopupType.Medium);
        _audio.PlayPvs(kit.Comp.DeploySound, user);

        QueueDel(kit.Owner);
    }

    /// <summary>
    /// Splits a generated station name like "Deployed Station LV-042" into a renameable base name
    /// ("Deployed Station") and a persistent number/code suffix ("LV-042") - mirrors
    /// ShipyardSystem.Consoles.cs's TryParseShuttleName so station codes behave like shuttle ones.
    /// </summary>
    private static void ParseStationName(string name, out string baseName, out string? number)
    {
        var nameParts = name.Split(' ');
        var hasSuffix = nameParts.Length > 1
            && nameParts[^1].Length < ShuttleDeedComponent.MaxSuffixLength
            && nameParts[^1].Contains('-');

        number = hasSuffix ? nameParts[^1] : null;
        baseName = string.Join(" ", nameParts[..^(hasSuffix ? 1 : 0)]);
    }
}
