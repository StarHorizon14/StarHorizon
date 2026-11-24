using System.Numerics;
using Content.Server._Horizon.Shuttles.Components;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Horizon.CCVar;
using Content.Shared._Horizon.Shuttles;
using Content.Shared._Horizon.Shuttles.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Shuttles.Systems;

/// <summary>
/// Lets a shuttle land on the landing pad tiles of another grid (tiles with
/// <see cref="ContentTileDefinition.GridUnderlay"/>) and take off again.
/// A landed shuttle is welded to the grid under it, so it moves and jumps to FTL together with it.
/// </summary>
public sealed class ShuttleLandingSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ThrusterSystem _thruster = default!;

    private const string JointPrefix = "landing";

    /// <summary>
    /// How often landed shuttles are checked for a missing host.
    /// </summary>
    private const float CheckInterval = 1f;

    private float _minCoverage;
    private float _maxSpeed;
    private float _checkAccumulator;

    /// <summary>
    /// Same sounds as a jump to FTL: the engines spinning up on take off, and the arrival on landing.
    /// </summary>
    private static readonly SoundSpecifier TakeOffSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    private static readonly SoundSpecifier LandingSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    /// <summary>
    /// How far past the shuttle's edge a pushed entity is put, in tiles.
    /// </summary>
    private const float PushMargin = 0.45f;

    /// <summary>
    /// Step used when looking for the shuttle's edge, in tiles.
    /// </summary>
    private const float PushStep = 0.25f;

    /// <summary>
    /// Below this relative speed a shuttle is treated as standing still and pushes things to its nearest edge.
    /// </summary>
    private const float PushMinSpeed = 0.1f;

    /// <summary>
    /// How close to a landing pad a moving shuttle starts pushing, in world units.
    /// </summary>
    private const float PushRange = 1.5f;

    /// <summary>
    /// How close the shuttle's edge has to be behind an entity's edge to push it, in world units.
    /// </summary>
    private const float PushContactGap = 0.1f;

    /// <summary>
    /// A pushed entity moves this much faster than the shuttle (factor and flat extra), so it stays clear of it.
    /// </summary>
    private const float PushSpeedFactor = 1.05f;
    private const float PushExtraSpeed = 0.2f;

    private const float DefaultPushRadius = 0.35f;

    private readonly HashSet<EntityUid> _lookupEnts = new();
    private readonly HashSet<EntityUid> _changedHosts = new();
    private List<Entity<MapGridComponent>> _pushGrids = new();

    /// <summary>
    /// Grids known to have (true) or not to have (false) landing pad tiles. Missing means unknown.
    /// </summary>
    private readonly Dictionary<EntityUid, bool> _padGrids = new();

    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<(EntityUid Uid, Matrix3x2 InvMatrix, MapGridComponent Grid)> _candidates = new();
    private readonly Dictionary<EntityUid, int> _coverage = new();
    private readonly List<EntityUid> _toProcess = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleConsoleComponent, ShuttleConsoleLandingRequest>(OnLandingRequest);

        SubscribeLocalEvent<LandedShuttleComponent, ComponentShutdown>(OnLandedShutdown);
        SubscribeLocalEvent<LandedShuttleComponent, ConsoleFTLAttemptEvent>(OnFTLAttempt);
        SubscribeLocalEvent<LandedShuttleComponent, TileFrictionEvent>(OnTileFriction);

        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoval);

        Subs.CVar(_cfg, HorizonCCVars.ShuttleLandingMinCoverage, value => _minCoverage = value, true);
        Subs.CVar(_cfg, HorizonCCVars.ShuttleLandingMaxSpeed, value => _maxSpeed = value, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateTakeOffs();
        UpdatePushing(frameTime);
        UpdateChangedHosts();
        UpdateDrift();

        _checkAccumulator += frameTime;
        if (_checkAccumulator < CheckInterval)
            return;

        _checkAccumulator = 0f;
        _toProcess.Clear();

        var query = EntityQueryEnumerator<LandedShuttleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var landed, out var xform))
        {
            // The host is gone, or it went to another map without us.
            if (TerminatingOrDeleted(landed.Host) || Transform(landed.Host).MapUid != xform.MapUid)
                _toProcess.Add(uid);
        }

        foreach (var uid in _toProcess)
        {
            if (!TryComp<LandedShuttleComponent>(uid, out var landed))
                continue;

            if (TerminatingOrDeleted(landed.Host))
                Release(uid);
            else
                PlaceOnHost((uid, landed));
        }
    }

    #region Public API

    /// <summary>
    /// Is this shuttle sitting on a landing pad?
    /// </summary>
    public bool IsLanded(EntityUid shuttle)
    {
        return GetState(CompOrNull<LandedShuttleComponent>(shuttle)) != ShuttleLandingState.None;
    }

    /// <summary>
    /// The landing state for a shuttle with this <see cref="LandedShuttleComponent"/> (or none).
    /// A component that is being removed counts as gone, so consoles refreshed during its shutdown show the shuttle as free.
    /// </summary>
    public static ShuttleLandingState GetState(LandedShuttleComponent? landed)
    {
        if (landed == null || landed.LifeStage > ComponentLifeStage.Running)
            return ShuttleLandingState.None;

        return landed.TakeOffAt != null ? ShuttleLandingState.TakingOff : ShuttleLandingState.Landed;
    }

    /// <summary>
    /// Lands the shuttle on the landing pad under it.
    /// </summary>
    /// <param name="reason">Why the shuttle cannot land, already localized.</param>
    public bool TryLand(EntityUid shuttle, out string? reason)
    {
        reason = null;

        if (HasComp<LandedShuttleComponent>(shuttle))
        {
            reason = Loc.GetString("shuttle-landing-already-landed");
            return false;
        }

        if (HasComp<FTLComponent>(shuttle))
        {
            reason = Loc.GetString("shuttle-landing-in-ftl");
            return false;
        }

        if (!TryComp<MapGridComponent>(shuttle, out var grid) ||
            !TryComp<PhysicsComponent>(shuttle, out var body))
        {
            reason = Loc.GetString("shuttle-landing-no-pad");
            return false;
        }

        if (!TryFindHost((shuttle, grid), out var host))
        {
            reason = Loc.GetString("shuttle-landing-no-pad");
            return false;
        }

        var shuttleXform = Transform(shuttle);
        var hostVelocity = GetHostVelocityAt(host, _transform.GetWorldPosition(shuttleXform));
        var relativeSpeed = (_physics.GetMapLinearVelocity(shuttle, body, shuttleXform) - hostVelocity).Length();

        if (relativeSpeed > _maxSpeed)
        {
            reason = Loc.GetString("shuttle-landing-too-fast");
            return false;
        }

        Land(shuttle, host);
        return true;
    }

    /// <summary>
    /// Starts taking off, like spinning up for an FTL jump: the engines fire and the sound plays.
    /// The shuttle is released once <see cref="ShuttleSystem.DefaultStartupTime"/> has passed.
    /// </summary>
    public bool TakeOff(EntityUid shuttle)
    {
        return TryTakeOff(shuttle, out _);
    }

    /// <inheritdoc cref="TakeOff"/>
    /// <param name="reason">Why the shuttle cannot take off, already localized; null if it simply is not landed.</param>
    public bool TryTakeOff(EntityUid shuttle, out string? reason)
    {
        reason = null;

        if (!TryComp<LandedShuttleComponent>(shuttle, out var landed) || GetState(landed) != ShuttleLandingState.Landed)
            return false;

        // A shuttle carried to an expedition stays on its carrier: it must not be used to fly around the expedition.
        if (HasComp<SalvageExpeditionComponent>(Transform(shuttle).MapUid))
            return false;

        landed.TakeOffAt = _timing.CurTime + TimeSpan.FromSeconds(_shuttle.DefaultStartupTime);

        if (TryComp<ShuttleComponent>(shuttle, out var shuttleComp))
        {
            // Same as when spinning up for FTL.
            _thruster.DisableLinearThrusters(shuttleComp);
            _thruster.EnableLinearThrustDirection(shuttleComp, DirectionFlag.North);
            _thruster.SetAngularThrust(shuttleComp, false);
        }

        _audio.SetGridAudio(_audio.PlayPvs(TakeOffSound, shuttle));
        _console.RefreshShuttleConsoles(shuttle);
        return true;
    }

    /// <summary>
    /// Releases the shuttle right away, with no take off.
    /// </summary>
    public bool Release(EntityUid shuttle)
    {
        // The weld is removed and the engines switched off when the component shuts down.
        return RemComp<LandedShuttleComponent>(shuttle);
    }

    /// <summary>
    /// How far a landed shuttle may drift from its place on the host before it is put back, in world units and radians.
    /// </summary>
    private const float DriftDistance = 0.05f;
    private static readonly double DriftAngle = Angle.FromDegrees(0.5).Theta;

    /// <summary>
    /// The weld holds the shuttle rigidly, but the physics solver can still let it creep, e.g. when a heavy host turns fast.
    /// A landed shuttle that has moved away from its place on the host is put back.
    /// </summary>
    private void UpdateDrift()
    {
        _lookupEnts.Clear();
        var query = EntityQueryEnumerator<LandedShuttleComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var landed, out var xform))
        {
            if (TerminatingOrDeleted(landed.Host) || HasComp<FTLComponent>(uid) || HasComp<FTLComponent>(landed.Host))
                continue;

            var hostXform = Transform(landed.Host);

            if (hostXform.MapUid != xform.MapUid)
                continue;

            var (_, hostRot, hostMatrix, _) = _transform.GetWorldPositionRotationMatrixWithInv(hostXform);
            var (pos, rot) = _transform.GetWorldPositionRotation(xform);
            var expectedPos = Vector2.Transform(landed.LocalPosition, hostMatrix);
            var angleError = Math.Abs(Math.IEEERemainder((rot - hostRot - landed.LocalRotation).Theta, 2 * Math.PI));

            if ((pos - expectedPos).LengthSquared() > DriftDistance * DriftDistance || angleError > DriftAngle)
                _lookupEnts.Add(uid);
        }

        foreach (var uid in _lookupEnts)
        {
            if (TryComp<LandedShuttleComponent>(uid, out var landed))
                SnapToHost((uid, landed));
        }

        _lookupEnts.Clear();
    }

    /// <summary>
    /// Puts a landed shuttle exactly where it landed on its host, moving with it.
    /// </summary>
    private void SnapToHost(Entity<LandedShuttleComponent> shuttle)
    {
        var (hostRot, hostMatrix) = (_transform.GetWorldRotation(shuttle.Comp.Host), _transform.GetWorldMatrix(shuttle.Comp.Host));
        var position = Vector2.Transform(shuttle.Comp.LocalPosition, hostMatrix);

        _transform.SetWorldPositionRotation(shuttle, position, hostRot + shuttle.Comp.LocalRotation);
        MatchHostVelocity(shuttle, shuttle.Comp.Host);
    }

    private void UpdateChangedHosts()
    {
        if (_changedHosts.Count == 0)
            return;

        _lookupEnts.Clear();
        var query = EntityQueryEnumerator<LandedShuttleComponent>();

        while (query.MoveNext(out var uid, out var landed))
        {
            if (_changedHosts.Contains(landed.Host))
                _lookupEnts.Add(uid);
        }

        _changedHosts.Clear();

        foreach (var uid in _lookupEnts)
        {
            if (TryComp<LandedShuttleComponent>(uid, out var landed))
                CheckCoverage((uid, landed));
        }

        _lookupEnts.Clear();
    }

    private void UpdateTakeOffs()
    {
        var now = _timing.CurTime;
        _lookupEnts.Clear();

        var query = EntityQueryEnumerator<LandedShuttleComponent>();
        while (query.MoveNext(out var uid, out var landed))
        {
            if (landed.TakeOffAt is { } at && at <= now)
                _lookupEnts.Add(uid);
        }

        foreach (var uid in _lookupEnts)
        {
            Release(uid);
        }

        _lookupEnts.Clear();
    }

    #endregion

    #region Landing

    private void OnLandingRequest(EntityUid uid, ShuttleConsoleComponent component, ShuttleConsoleLandingRequest args)
    {
        if (Transform(uid).GridUid is not { } shuttle || !HasComp<ShuttleComponent>(shuttle))
            return;

        if (!args.Land)
        {
            TryTakeOff(shuttle, out _);
            return;
        }

        if (!TryLand(shuttle, out var reason) && reason != null)
            _popup.PopupEntity(reason, uid, args.Actor, PopupType.MediumCaution);
    }

    /// <summary>
    /// Finds the grid whose landing pad tiles are under enough of the shuttle's tiles.
    /// </summary>
    private bool TryFindHost(Entity<MapGridComponent> shuttle, out EntityUid host)
    {
        host = EntityUid.Invalid;
        var xform = Transform(shuttle);

        if (xform.MapUid == null)
            return false;

        var shuttleMatrix = _transform.GetWorldMatrix(xform);
        var worldAABB = shuttleMatrix.TransformBox(shuttle.Comp.LocalAABB);

        _grids.Clear();
        _candidates.Clear();
        _coverage.Clear();
        _mapManager.FindGridsIntersecting(xform.MapUid.Value, worldAABB, ref _grids, approx: true, includeMap: false);

        foreach (var grid in _grids)
        {
            // Not ourselves, and not something that is sitting on us.
            if (grid.Owner == shuttle.Owner || IsLandedOn(grid.Owner, shuttle.Owner))
                continue;

            _candidates.Add((grid.Owner, _transform.GetInvWorldMatrix(grid.Owner), grid.Comp));
        }

        if (_candidates.Count == 0)
            return false;

        var total = 0;
        var tileSize = shuttle.Comp.TileSize;

        foreach (var tile in _map.GetAllTiles(shuttle.Owner, shuttle.Comp))
        {
            total++;
            var local = (new Vector2(tile.GridIndices.X, tile.GridIndices.Y) + new Vector2(0.5f, 0.5f)) * tileSize;
            var world = Vector2.Transform(local, shuttleMatrix);

            foreach (var (candidate, invMatrix, candidateGrid) in _candidates)
            {
                var candidateLocal = Vector2.Transform(world, invMatrix) / candidateGrid.TileSize;
                var indices = new Vector2i((int) MathF.Floor(candidateLocal.X), (int) MathF.Floor(candidateLocal.Y));

                if (!_map.TryGetTileRef(candidate, candidateGrid, indices, out var candidateTile) ||
                    !IsLandingPad(candidateTile.Tile))
                {
                    continue;
                }

                _coverage[candidate] = _coverage.GetValueOrDefault(candidate) + 1;
                break;
            }
        }

        if (total == 0)
            return false;

        var best = 0;

        foreach (var (candidate, count) in _coverage)
        {
            if (count <= best)
                continue;

            best = count;
            host = candidate;
        }

        return best > 0 && (float) best / total >= _minCoverage;
    }

    /// <summary>
    /// The share of the shuttle's tiles that are over landing pad tiles of <paramref name="host"/>.
    /// </summary>
    private float GetCoverage(Entity<MapGridComponent> shuttle, EntityUid host)
    {
        if (!TryComp<MapGridComponent>(host, out var hostGrid))
            return 0f;

        var shuttleMatrix = _transform.GetWorldMatrix(shuttle);
        var hostInvMatrix = _transform.GetInvWorldMatrix(host);
        var tileSize = shuttle.Comp.TileSize;
        var total = 0;
        var covered = 0;

        foreach (var tile in _map.GetAllTiles(shuttle.Owner, shuttle.Comp))
        {
            total++;
            var local = (new Vector2(tile.GridIndices.X, tile.GridIndices.Y) + new Vector2(0.5f, 0.5f)) * tileSize;
            var hostLocal = Vector2.Transform(Vector2.Transform(local, shuttleMatrix), hostInvMatrix) / hostGrid.TileSize;
            var indices = new Vector2i((int) MathF.Floor(hostLocal.X), (int) MathF.Floor(hostLocal.Y));

            if (_map.TryGetTileRef(host, hostGrid, indices, out var hostTile) && IsLandingPad(hostTile.Tile))
                covered++;
        }

        return total == 0 ? 0f : (float) covered / total;
    }

    /// <summary>
    /// A landed shuttle whose pad has been taken from under it takes off by itself.
    /// </summary>
    private void CheckCoverage(Entity<LandedShuttleComponent> shuttle)
    {
        if (GetState(shuttle.Comp) != ShuttleLandingState.Landed ||
            !TryComp<MapGridComponent>(shuttle, out var grid) ||
            TerminatingOrDeleted(shuttle.Comp.Host))
        {
            return;
        }

        // Rounding: a fully covered shuttle must not take off because of float error.
        if (GetCoverage((shuttle, grid), shuttle.Comp.Host) + 0.0001f >= _minCoverage)
            return;

        TakeOff(shuttle);
    }

    private bool IsLandingPad(Tile tile)
    {
        return !tile.IsEmpty &&
               _tileDefs.TryGetDefinition(tile.TypeId, out var def) &&
               def is ContentTileDefinition { GridUnderlay: true };
    }

    /// <summary>
    /// Is <paramref name="uid"/> landed on <paramref name="host"/>, directly or through other landed shuttles?
    /// </summary>
    private bool IsLandedOn(EntityUid uid, EntityUid host)
    {
        // The depth limit only guards against a broken chain.
        for (var i = 0; i < 16 && TryComp<LandedShuttleComponent>(uid, out var landed); i++)
        {
            if (landed.Host == host)
                return true;

            uid = landed.Host;
        }

        return false;
    }

    private void Land(EntityUid shuttle, EntityUid host)
    {
        var (position, rotation) = _transform.GetWorldPositionRotation(shuttle);
        var landed = AddComp<LandedShuttleComponent>(shuttle);

        landed.Host = host;
        landed.JointId = JointPrefix + shuttle;
        landed.LocalPosition = Vector2.Transform(position, _transform.GetInvWorldMatrix(host));
        landed.LocalRotation = rotation - _transform.GetWorldRotation(host);

        MatchHostVelocity(shuttle, host);
        Weld((shuttle, landed));

        if (TryComp<ShuttleComponent>(shuttle, out var shuttleComp))
        {
            _thruster.DisableLinearThrusters(shuttleComp);
            _thruster.SetAngularThrust(shuttleComp, false);
        }

        // Nothing may stay under the landed shuttle.
        if (TryComp<MapGridComponent>(shuttle, out var grid))
            PushFromUnder((shuttle, grid), Transform(shuttle), Vector2.Zero, 0f);

        _audio.SetGridAudio(_audio.PlayPvs(LandingSound, shuttle));
        _console.RefreshShuttleConsoles(shuttle);
    }

    private void OnLandedShutdown(Entity<LandedShuttleComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        RemoveWeld(ent);

        if (TryComp<ShuttleComponent>(ent, out var shuttleComp))
        {
            _thruster.DisableLinearThrusters(shuttleComp);
            _thruster.SetAngularThrust(shuttleComp, false);
        }

        _console.RefreshShuttleConsoles(ent);
    }

    /// <summary>
    /// Welds the shuttle to its host at their current positions.
    /// </summary>
    private void Weld(Entity<LandedShuttleComponent> shuttle)
    {
        var host = shuttle.Comp.Host;

        if (!HasComp<PhysicsComponent>(host) ||
            !HasComp<PhysicsComponent>(shuttle) ||
            Transform(host).MapID != Transform(shuttle).MapID)
        {
            return;
        }

        var anchorWorld = _transform.GetWorldPosition(shuttle);
        var joint = _joints.GetOrCreateWeldJoint(host, shuttle, shuttle.Comp.JointId);

        joint.LocalAnchorA = Vector2.Transform(anchorWorld, _transform.GetInvWorldMatrix(host));
        joint.LocalAnchorB = Vector2.Zero;
        joint.ReferenceAngle = (float) (_transform.GetWorldRotation(shuttle) - _transform.GetWorldRotation(host));
        joint.CollideConnected = false;

        // Rigid, unlike docking: a spring lets the shuttle swing around its anchor like on a bearing,
        // because its stiffness is computed from masses while turning is resisted by the much larger inertia.
        joint.Stiffness = 0f;
        joint.Damping = 0f;
    }

    private void RemoveWeld(Entity<LandedShuttleComponent> shuttle)
    {
        // Does nothing if the joint is already gone.
        _joints.RemoveJoint(shuttle, shuttle.Comp.JointId);
    }

    private Vector2 GetHostVelocityAt(EntityUid host, Vector2 worldPosition)
    {
        if (!TryComp<PhysicsComponent>(host, out var hostBody))
            return Vector2.Zero;

        var hostXform = Transform(host);
        var localPoint = Vector2.Transform(worldPosition, _transform.GetInvWorldMatrix(hostXform));
        return _physics.GetLinearVelocity(host, localPoint, hostBody, hostXform);
    }

    private void MatchHostVelocity(EntityUid shuttle, EntityUid host)
    {
        if (!TryComp<PhysicsComponent>(shuttle, out var shuttleBody))
            return;

        var angular = TryComp<PhysicsComponent>(host, out var hostBody) ? hostBody.AngularVelocity : 0f;

        _physics.SetLinearVelocity(shuttle, GetHostVelocityAt(host, _transform.GetWorldPosition(shuttle)), body: shuttleBody);
        _physics.SetAngularVelocity(shuttle, angular, body: shuttleBody);
    }

    #endregion

    #region Pushing

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        // The pad under a landed shuttle may be gone.
        _changedHosts.Add(ev.Entity);

        foreach (var change in ev.Changes)
        {
            if (IsLandingPad(change.NewTile))
            {
                _padGrids[ev.Entity] = true;
                return;
            }

            // A pad tile is gone: there may be none left, find out when it matters.
            if (IsLandingPad(change.OldTile))
                _padGrids.Remove(ev.Entity);
        }
    }

    private void OnGridRemoval(GridRemovalEvent ev)
    {
        _padGrids.Remove(ev.EntityUid);
    }

    private bool HasLandingPads(Entity<MapGridComponent> grid)
    {
        if (_padGrids.TryGetValue(grid, out var known))
            return known;

        var found = false;
        var tiles = _map.GetAllTilesEnumerator(grid, grid.Comp);

        while (tiles.MoveNext(out var tile))
        {
            if (!IsLandingPad(tile.Value.Tile))
                continue;

            found = true;
            break;
        }

        _padGrids[grid] = found;
        return found;
    }

    /// <summary>
    /// A shuttle moving over a landing pad pushes whatever stands on the pad in front of it, like a bulldozer:
    /// anything its edge is about to hit is given at least the shuttle's speed in the direction it moves.
    /// The physics engine cannot do this by itself: the shuttle's floor only collides with other grids,
    /// and its walls barely move bodies that stand on another grid.
    /// </summary>
    private void UpdatePushing(float frameTime)
    {
        var query = EntityQueryEnumerator<ShuttleComponent, MapGridComponent, PhysicsComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var grid, out var body, out var xform))
        {
            // Landed shuttles do not move relative to their pad.
            if (!body.Awake || body.BodyType != BodyType.Dynamic || HasComp<LandedShuttleComponent>(uid))
                continue;

            if (body.LinearVelocity.LengthSquared() < PushMinSpeed * PushMinSpeed && MathF.Abs(body.AngularVelocity) < 0.01f)
                continue;

            if (!FindPadGridsNear((uid, grid), xform))
                continue;

            PushEntitiesOnPads((uid, grid, body), xform, frameTime);
        }
    }

    /// <summary>
    /// Fills <see cref="_pushGrids"/> with the grids that have landing pads near the shuttle. Returns whether there are any.
    /// </summary>
    private bool FindPadGridsNear(Entity<MapGridComponent> shuttle, TransformComponent xform)
    {
        _pushGrids.Clear();

        if (xform.MapUid is not { } mapUid)
            return false;

        var worldAABB = _transform.GetWorldMatrix(xform).TransformBox(shuttle.Comp.LocalAABB).Enlarged(PushRange);
        _mapManager.FindGridsIntersecting(mapUid, worldAABB, ref _pushGrids, approx: true, includeMap: false);

        for (var i = _pushGrids.Count - 1; i >= 0; i--)
        {
            var other = _pushGrids[i];

            if (other.Owner == shuttle.Owner || !HasLandingPads(other))
                _pushGrids.RemoveAt(i);
        }

        return _pushGrids.Count > 0;
    }

    private void PushEntitiesOnPads(Entity<MapGridComponent, PhysicsComponent> shuttle, TransformComponent xform, float frameTime)
    {
        var (_, shuttleRotation, shuttleMatrix, shuttleInvMatrix) = _transform.GetWorldPositionRotationMatrixWithInv(xform);
        var worldAABB = shuttleMatrix.TransformBox(shuttle.Comp1.LocalAABB).Enlarged(PushRange);
        var tileSize = (float) shuttle.Comp1.TileSize;
        var gridEnt = new Entity<MapGridComponent>(shuttle, shuttle.Comp1);

        foreach (var pad in _pushGrids)
        {
            _lookupEnts.Clear();
            _lookup.GetEntitiesIntersecting(pad.Owner, worldAABB, _lookupEnts, LookupFlags.Dynamic);

            if (_lookupEnts.Count == 0)
                continue;

            var padInvMatrix = _transform.GetInvWorldMatrix(pad.Owner);

            foreach (var ent in _lookupEnts)
            {
                var entXform = Transform(ent);

                if (entXform.GridUid != pad.Owner ||
                    entXform.ParentUid != pad.Owner ||
                    entXform.Anchored ||
                    !TryComp<PhysicsComponent>(ent, out var entBody) ||
                    !entBody.CanCollide ||
                    entBody.BodyType == BodyType.Static ||
                    HasComp<FTLSmashImmuneComponent>(ent))
                {
                    continue;
                }

                var worldPos = _transform.GetWorldPosition(entXform);

                // Only what stands on a landing pad: anywhere else the shuttle cannot be.
                var padLocal = Vector2.Transform(worldPos, padInvMatrix) / pad.Comp.TileSize;
                var padIndices = new Vector2i((int) MathF.Floor(padLocal.X), (int) MathF.Floor(padLocal.Y));

                if (!_map.TryGetTileRef(pad.Owner, pad.Comp, padIndices, out var padTile) || !IsLandingPad(padTile.Tile))
                    continue;

                // Velocity of the shuttle's hull at this point, relative to the pad, in map space.
                var shuttleLocal = Vector2.Transform(worldPos, shuttleInvMatrix);
                var relative = _physics.GetLinearVelocity(shuttle, shuttleLocal, shuttle.Comp2, xform)
                               - GetHostVelocityAt(pad.Owner, worldPos);
                var speed = relative.Length();

                if (speed < PushMinSpeed)
                    continue;

                var pushDir = relative / speed;
                var localDir = (-shuttleRotation).RotateVec(pushDir);
                var local = shuttleLocal / tileSize;

                if (IsShuttleTile(gridEnt, local))
                {
                    // Already under the shuttle (it was too fast, or turned onto it): put it back in front.
                    if (!TryFindExit(gridEnt, local, localDir, out var exit))
                        continue;

                    var exitWorld = Vector2.Transform(exit * tileSize, shuttleMatrix);
                    _transform.SetCoordinates(ent, entXform, new EntityCoordinates(pad.Owner, Vector2.Transform(exitWorld, padInvMatrix)));
                }
                else
                {
                    // Is the shuttle's edge right behind it? Look behind its centre and both of its sides.
                    var radius = GetPushRadius(ent, entXform);
                    var behind = local - localDir * ((radius + PushContactGap) / tileSize);
                    var side = new Vector2(-localDir.Y, localDir.X) * (radius * 0.8f / tileSize);

                    if (!IsShuttleTile(gridEnt, behind) &&
                        !IsShuttleTile(gridEnt, behind + side) &&
                        !IsShuttleTile(gridEnt, behind - side))
                    {
                        continue;
                    }
                }

                // Keep its sideways motion, but make it move away at least as fast as the shuttle comes.
                var along = Vector2.Dot(entBody.LinearVelocity, pushDir);
                var target = speed * PushSpeedFactor + PushExtraSpeed;

                if (along < target)
                    _physics.SetLinearVelocity(ent, entBody.LinearVelocity + pushDir * (target - along), body: entBody);

                _physics.WakeBody(ent, body: entBody);
            }
        }

        _lookupEnts.Clear();
    }

    /// <summary>
    /// How far the entity reaches from its centre, from its physics bounds.
    /// </summary>
    private float GetPushRadius(EntityUid ent, TransformComponent xform)
    {
        var aabb = _physics.GetWorldAABB(ent, xform: xform);
        var radius = MathF.Max(aabb.Width, aabb.Height) / 2f;
        return radius > 0.05f ? MathF.Min(radius, 1f) : DefaultPushRadius;
    }

    /// <summary>
    /// Moves loose entities that stand on another grid's landing pad, over a tile of this shuttle, out from under it.
    /// </summary>
    /// <param name="shuttleVelocity">World velocity of the shuttle, they are pushed along it. Zero pushes to the nearest edge.</param>
    private void PushFromUnder(Entity<MapGridComponent> shuttle, TransformComponent xform, Vector2 shuttleVelocity, float frameTime)
    {
        if (xform.MapUid is not { } mapUid)
            return;

        var (_, shuttleRotation, shuttleMatrix, shuttleInvMatrix) = _transform.GetWorldPositionRotationMatrixWithInv(xform);
        var worldAABB = shuttleMatrix.TransformBox(shuttle.Comp.LocalAABB);
        var tileSize = (float) shuttle.Comp.TileSize;

        _pushGrids.Clear();
        _mapManager.FindGridsIntersecting(mapUid, worldAABB, ref _pushGrids, approx: true, includeMap: false);

        foreach (var other in _pushGrids)
        {
            if (other.Owner == shuttle.Owner || !HasLandingPads(other))
                continue;

            _lookupEnts.Clear();
            _lookup.GetEntitiesIntersecting(other.Owner, worldAABB, _lookupEnts, LookupFlags.Dynamic);

            if (_lookupEnts.Count == 0)
                continue;

            var otherInvMatrix = _transform.GetInvWorldMatrix(other.Owner);

            foreach (var ent in _lookupEnts)
            {
                var entXform = Transform(ent);

                if (entXform.GridUid != other.Owner ||
                    entXform.Anchored ||
                    !TryComp<PhysicsComponent>(ent, out var entBody) ||
                    !entBody.CanCollide ||
                    HasComp<FTLSmashImmuneComponent>(ent))
                {
                    continue;
                }

                var worldPos = _transform.GetWorldPosition(entXform);

                // Only what stands on a landing pad: anywhere else the shuttle cannot be.
                var otherLocal = Vector2.Transform(worldPos, otherInvMatrix) / other.Comp.TileSize;
                var otherIndices = new Vector2i((int) MathF.Floor(otherLocal.X), (int) MathF.Floor(otherLocal.Y));

                if (!_map.TryGetTileRef(other.Owner, other.Comp, otherIndices, out var otherTile) ||
                    !IsLandingPad(otherTile.Tile))
                {
                    continue;
                }

                var local = Vector2.Transform(worldPos, shuttleInvMatrix) / tileSize;

                if (!IsShuttleTile(shuttle, local))
                    continue;

                // Direction in the shuttle's frame: where the shuttle is going relative to the pad.
                var relative = shuttleVelocity - GetHostVelocityAt(other.Owner, worldPos);
                var direction = Vector2.Zero;

                if (relative.LengthSquared() >= PushMinSpeed * PushMinSpeed)
                    direction = (-shuttleRotation).RotateVec(relative).Normalized();

                if (!TryFindExit(shuttle, local, direction, out var exit))
                    continue;

                // Give it a head start, so the shuttle does not catch it again next tick.
                if (direction != Vector2.Zero)
                    exit += direction * (relative.Length() * frameTime / tileSize);

                var exitWorld = Vector2.Transform(exit * tileSize, shuttleMatrix);
                _transform.SetCoordinates(ent, entXform, new EntityCoordinates(other.Owner, Vector2.Transform(exitWorld, otherInvMatrix)));
            }
        }

        _lookupEnts.Clear();
    }

    private bool IsShuttleTile(Entity<MapGridComponent> shuttle, Vector2 localTiles)
    {
        var indices = new Vector2i((int) MathF.Floor(localTiles.X), (int) MathF.Floor(localTiles.Y));
        return _map.TryGetTileRef(shuttle.Owner, shuttle.Comp, indices, out var tile) && !tile.Tile.IsEmpty;
    }

    /// <summary>
    /// Finds the point just past the shuttle's edge, going from <paramref name="from"/> along <paramref name="direction"/>,
    /// or towards the nearest edge if there is no direction. Positions are in the shuttle's local tiles.
    /// </summary>
    private bool TryFindExit(Entity<MapGridComponent> shuttle, Vector2 from, Vector2 direction, out Vector2 exit)
    {
        var aabb = shuttle.Comp.LocalAABB;
        var maxSteps = (int) MathF.Ceiling((aabb.Width + aabb.Height) / (shuttle.Comp.TileSize * PushStep)) + 2;

        if (direction != Vector2.Zero)
            return TryMarch(shuttle, from, direction, maxSteps, out exit, out _);

        exit = default;
        var best = int.MaxValue;

        foreach (var dir in new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY })
        {
            if (!TryMarch(shuttle, from, dir, Math.Min(maxSteps, best), out var candidate, out var steps) || steps >= best)
                continue;

            best = steps;
            exit = candidate;
        }

        return best != int.MaxValue;
    }

    private bool TryMarch(Entity<MapGridComponent> shuttle, Vector2 from, Vector2 direction, int maxSteps, out Vector2 exit, out int steps)
    {
        for (steps = 1; steps <= maxSteps; steps++)
        {
            var point = from + direction * (steps * PushStep);

            if (IsShuttleTile(shuttle, point))
                continue;

            // The first point off the shuttle; step back to the exact edge and go a margin past it.
            var lo = point - direction * PushStep;
            var hi = point;

            for (var i = 0; i < 6; i++)
            {
                var mid = (lo + hi) / 2f;

                if (IsShuttleTile(shuttle, mid))
                    lo = mid;
                else
                    hi = mid;
            }

            exit = hi + direction * PushMargin;
            return true;
        }

        exit = default;
        return false;
    }

    #endregion

    #region While landed

    private void OnFTLAttempt(Entity<LandedShuttleComponent> ent, ref ConsoleFTLAttemptEvent args)
    {
        args.Cancelled = true;
        args.Reason = Loc.GetString("shuttle-console-landed");
    }

    private void OnTileFriction(Entity<LandedShuttleComponent> ent, ref TileFrictionEvent args)
    {
        // The shuttle is cargo now, it should not brake the grid that carries it.
        args.Modifier = 0f;
    }

    private void OnFTLStarted(ref FTLStartedEvent args)
    {
        // Something sent a landed shuttle to FTL without asking the console. Let it go.
        Release(args.Entity);
        CarryLanded(args.Entity);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        CarryLanded(args.Entity);
    }

    /// <summary>
    /// Brings every shuttle landed on <paramref name="host"/> along after the host has been moved to another place,
    /// and whatever is landed on those shuttles too.
    /// </summary>
    private void CarryLanded(EntityUid host, int depth = 0)
    {
        if (depth > 16)
            return;

        var landedShuttles = new List<Entity<LandedShuttleComponent>>();
        var query = EntityQueryEnumerator<LandedShuttleComponent>();

        while (query.MoveNext(out var uid, out var landed))
        {
            if (landed.Host == host)
                landedShuttles.Add((uid, landed));
        }

        foreach (var shuttle in landedShuttles)
        {
            PlaceOnHost(shuttle);
            CarryLanded(shuttle, depth + 1);
        }
    }

    /// <summary>
    /// Puts the shuttle back where it landed, relative to its host, and welds it again.
    /// </summary>
    private void PlaceOnHost(Entity<LandedShuttleComponent> shuttle)
    {
        var hostXform = Transform(shuttle.Comp.Host);

        if (hostXform.MapUid is not { } mapUid)
        {
            Release(shuttle);
            return;
        }

        RemoveWeld(shuttle);

        var position = Vector2.Transform(shuttle.Comp.LocalPosition, _transform.GetWorldMatrix(hostXform));
        var rotation = _transform.GetWorldRotation(hostXform) + shuttle.Comp.LocalRotation;

        _transform.SetCoordinates(shuttle, Transform(shuttle), new EntityCoordinates(mapUid, position), rotation: rotation);
        MatchHostVelocity(shuttle, shuttle.Comp.Host);
        Weld(shuttle);
    }

    #endregion
}
