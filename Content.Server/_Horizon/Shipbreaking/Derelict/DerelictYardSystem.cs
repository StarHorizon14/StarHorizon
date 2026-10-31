using Content.Server.Cargo.Systems;
using Content.Server.Radio.EntitySystems;
using System.Linq;
using Content.Server.Station.Systems;
using Content.Server._NF.Bank;
using Content.Server._NF.Shipyard.Systems;
using Content.Shared._Horizon.Shipbreaking.Derelict;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Shipbreaking.Derelict;

public sealed class DerelictYardSystem : EntitySystem
{
    private const string DerelictDockTag = "DerelictDock";

    private static readonly TimeSpan FirstWarning = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LastWarning = TimeSpan.FromMinutes(1);

    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private sealed class Job
    {
        public EntityUid Station;
        public string Name = string.Empty;
        public int Price;
        public int Appraisal;
        public int Revenue;
        public TimeSpan Expires;
        public bool Warned5;
        public bool Warned1;
    }

    private readonly Dictionary<EntityUid, Job> _jobs = new();

    private readonly Dictionary<string, (Vector2i[] Tiles, Color[] Colors)> _previews = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DerelictYardConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<DerelictYardConsoleComponent, DerelictPurchaseMessage>(OnPurchase);
        SubscribeLocalEvent<DerelictYardConsoleComponent, DerelictPreviewRequestMessage>(OnPreviewRequest);
        SubscribeLocalEvent<DerelictYardConsoleComponent, DerelictFinishMessage>(OnFinishEarly);
        SubscribeLocalEvent<DerelictLeaseComponent, PostGridSplitEvent>(OnSplit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _jobs.Clear());
    }

    private void OnOpened(Entity<DerelictYardConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DerelictYardConsoleComponent> ent)
    {
        var station = _station.GetOwningStation(ent);
        var rank = station != null ? EnsureComp<DerelictYardRankComponent>(station.Value) : null;
        var state = new DerelictYardState
        {
            Rank = rank?.Rank ?? 0,
            Experience = rank?.Experience ?? 0,
        };

        if (state.Rank + 1 < DerelictYardRankComponent.Thresholds.Length)
            state.NextRankAt = DerelictYardRankComponent.Thresholds[state.Rank + 1];

        foreach (var id in ent.Comp.Vessels)
        {
            if (!_proto.TryIndex(id, out var vessel))
                continue;

            state.Vessels.Add(new DerelictVesselEntry
            {
                Id = vessel.ID,
                Name = Loc.GetString(vessel.Name),
                Description = vessel.Description is { } description ? Loc.GetString(description) : string.Empty,
                Price = vessel.Price,
                Lifetime = vessel.Lifetime,
                MinRank = vessel.MinRank,
                Locked = vessel.MinRank > state.Rank,
            });
        }

        foreach (var (group, job) in _jobs)
        {
            if (job.Station != station)
                continue;

            state.Leases.Add(new DerelictLeaseEntry
            {
                Group = GetNetEntity(group),
                Name = job.Name,
                Left = job.Expires - _timing.CurTime,
                Share = job.Appraisal > 0 ? (float) job.Revenue / job.Appraisal : 0f,
            });
        }

        if (station != null && _bank.TryGetBalance(SectorBankAccount.Frontier, out var balance))
            state.Balance = balance;

        _ui.SetUiState(ent.Owner, DerelictYardUiKey.Key, state);
    }

    private void UpdateAllUis()
    {
        var consoles = EntityQueryEnumerator<DerelictYardConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            UpdateUi((uid, console));
        }
    }

    private void OnPurchase(Entity<DerelictYardConsoleComponent> ent, ref DerelictPurchaseMessage args)
    {
        if (!ent.Comp.Vessels.Contains(args.Id) || !_proto.TryIndex<DerelictVesselPrototype>(args.Id, out var vessel))
            return;

        if (_station.GetOwningStation(ent) is not { } station)
        {
            _popup.PopupEntity(Loc.GetString("derelict-yard-no-station"), ent, args.Actor);
            return;
        }

        var rank = EnsureComp<DerelictYardRankComponent>(station);
        if (vessel.MinRank > rank.Rank)
        {
            _popup.PopupEntity(Loc.GetString("derelict-yard-rank-too-low"), ent, args.Actor);
            return;
        }

        if (!_bank.TryGetBalance(SectorBankAccount.Frontier, out var balance) || balance < vessel.Price ||
            !_bank.TrySectorWithdraw(SectorBankAccount.Frontier, vessel.Price, LedgerEntryType.StationWithdrawalSupplies))
        {
            _popup.PopupEntity(Loc.GetString("derelict-yard-no-funds"), ent, args.Actor);
            return;
        }

        if (!_shipyard.TryPurchaseShuttle(station, vessel.ShuttlePath, out var shuttle, DerelictDockTag))
        {
            _bank.TrySectorDeposit(SectorBankAccount.Frontier, vessel.Price, LedgerEntryType.StationDepositOther);
            _popup.PopupEntity(Loc.GetString("derelict-yard-failed"), ent, args.Actor);
            return;
        }

        EnsureComp<DerelictLeaseComponent>(shuttle.Value).Group = shuttle.Value;
        EnsureComp<ShuttleUtilizationComponent>(shuttle.Value);
        MarkOrigin(shuttle.Value);

        _jobs[shuttle.Value] = new Job
        {
            Station = station,
            Name = Loc.GetString(vessel.Name),
            Price = vessel.Price,
            Appraisal = (int) _pricing.AppraiseGrid(shuttle.Value),
            Expires = _timing.CurTime + vessel.Lifetime,
        };

        Announce(station,
            Loc.GetString("derelict-yard-bought", ("name", Loc.GetString(vessel.Name)), ("minutes", (int) vessel.Lifetime.TotalMinutes)));

        UpdateAllUis();
    }

    /// <summary>
    /// The news of the yard goes to the supply radio channel, spoken by a yard console of the station.
    /// </summary>
    private void Announce(EntityUid station, string message)
    {
        var consoles = EntityQueryEnumerator<DerelictYardConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            if (_station.GetOwningStation(uid) != station)
                continue;

            _radio.SendRadioMessage(uid, message, CargoOrderConsoleComponent.BaseAnnouncementChannel, uid, escapeMarkup: false);
            return;
        }
    }

    private void MarkOrigin(EntityUid shuttle)
    {
        if (!TryComp<MapGridComponent>(shuttle, out var grid))
            return;

        var found = new HashSet<EntityUid>();
        _lookup.GetLocalEntitiesIntersecting(shuttle, grid.LocalAABB.Enlarged(1f), found,
            LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Uncontained);

        foreach (var uid in found)
        {
            if (uid != shuttle)
                EnsureComp<DerelictScrapOriginComponent>(uid).Group = shuttle;
        }
    }

    public void CreditScrap(EntityUid source, int amount)
    {
        EntityUid? group = null;
        if (TryComp<DerelictScrapOriginComponent>(source, out var origin))
            group = origin.Group;
        else if (TryComp<DerelictLeaseComponent>(source, out var lease))
            group = lease.Group;

        if (group != null && _jobs.TryGetValue(group.Value, out var job))
            job.Revenue += amount;
    }

    /// <summary>
    /// Whether any lease runs out within the given time.
    /// </summary>
    public bool IsLeaseEnding(TimeSpan within)
    {
        foreach (var job in _jobs.Values)
        {
            if (job.Expires - _timing.CurTime <= within)
                return true;
        }

        return false;
    }

    private void OnFinishEarly(Entity<DerelictYardConsoleComponent> ent, ref DerelictFinishMessage args)
    {
        var group = GetEntity(args.Group);
        if (!_jobs.TryGetValue(group, out var job) || job.Station != _station.GetOwningStation(ent))
            return;

        Finish(group, job);
        _jobs.Remove(group);

        UpdateAllUis();
    }

    private void OnSplit(Entity<DerelictLeaseComponent> ent, ref PostGridSplitEvent args)
    {
        EnsureComp<DerelictLeaseComponent>(args.Grid).Group = ent.Comp.Group;

        EnsureComp<ShuttleUtilizationComponent>(ent);
        EnsureComp<ShuttleUtilizationComponent>(args.Grid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_jobs.Count == 0)
            return;

        List<EntityUid>? finished = null;
        foreach (var (group, job) in _jobs)
        {
            var left = job.Expires - _timing.CurTime;
            if (left <= TimeSpan.Zero)
            {
                (finished ??= new()).Add(group);
            }
            else if (left <= LastWarning && !job.Warned1)
            {
                job.Warned1 = true;
                Warn(job, 1);
            }
            else if (left <= FirstWarning && !job.Warned5)
            {
                job.Warned5 = true;
                Warn(job, 5);
            }
        }

        if (finished == null)
            return;

        foreach (var group in finished)
        {
            Finish(group, _jobs[group]);
            _jobs.Remove(group);
        }

        UpdateAllUis();
    }

    private void Warn(Job job, int minutes)
    {
        Announce(job.Station,
            Loc.GetString("derelict-yard-warning", ("minutes", minutes)));
    }

    private void Finish(EntityUid group, Job job)
    {
        Settle(job);

        var grids = new HashSet<EntityUid>();
        var leases = EntityQueryEnumerator<ShuttleUtilizationComponent, DerelictLeaseComponent>();
        while (leases.MoveNext(out var uid, out _, out var lease))
        {
            if (lease.Group == group)
                grids.Add(uid);
        }

        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        var rescued = new List<EntityUid>();
        while (mobs.MoveNext(out var mob, out _, out var xform))
        {
            if (xform.GridUid is { } grid && grids.Contains(grid))
                rescued.Add(mob);
        }

        foreach (var mob in rescued)
        {
            if (_container.TryGetContainingContainer((mob, null, null), out var container))
                _container.Remove(mob, container, force: true);

            var xform = Transform(mob);
            if (xform.MapUid is { } map)
                _transform.SetParent(mob, xform, map);
        }

        foreach (var grid in grids)
        {
            QueueDel(grid);
        }
    }

    private void Settle(Job job)
    {
        var share = job.Appraisal > 0 ? (float) job.Revenue / job.Appraisal : 0f;
        var percent = (int) MathF.Round(share * 100f);
        var bonus = 0;
        var fee = 0;

        if (share >= 0.9f)
            bonus = (int) (job.Revenue * 0.25f);
        else if (share >= 0.7f)
            bonus = (int) (job.Revenue * 0.1f);
        else if (share < 0.3f)
            fee = (int) (job.Price * 0.1f);

        if (bonus > 0)
            _bank.TrySectorDeposit(SectorBankAccount.Frontier, bonus, LedgerEntryType.StationDepositOther);

        if (fee > 0 && !_bank.TrySectorWithdraw(SectorBankAccount.Frontier, fee, LedgerEntryType.StationWithdrawalSupplies))
            fee = 0;

        Announce(job.Station,
            Loc.GetString("derelict-yard-result",
                ("name", job.Name), ("percent", percent), ("revenue", job.Revenue), ("bonus", bonus), ("fee", fee)));

        var rank = EnsureComp<DerelictYardRankComponent>(job.Station);
        rank.Experience += job.Revenue;

        var newRank = rank.Rank;
        while (newRank + 1 < DerelictYardRankComponent.Thresholds.Length &&
               rank.Experience >= DerelictYardRankComponent.Thresholds[newRank + 1])
        {
            newRank++;
        }

        if (newRank == rank.Rank)
            return;

        rank.Rank = newRank;
        Announce(job.Station,
            Loc.GetString("derelict-yard-rank-up", ("rank", newRank)));
    }

    private void OnPreviewRequest(Entity<DerelictYardConsoleComponent> ent, ref DerelictPreviewRequestMessage args)
    {
        if (!ent.Comp.Vessels.Contains(args.Id) || !_proto.TryIndex<DerelictVesselPrototype>(args.Id, out var vessel))
            return;

        if (!_previews.TryGetValue(vessel.ID, out var preview))
        {
            if (!TryBuildPreview(vessel, out preview))
                return;

            _previews[vessel.ID] = preview;
        }

        _ui.ServerSendUiMessage(ent.Owner, DerelictYardUiKey.Key,
            new DerelictPreviewMessage(vessel.ID, preview.Tiles, preview.Colors), args.Actor);
    }

    private bool TryBuildPreview(DerelictVesselPrototype vessel, out (Vector2i[] Tiles, Color[] Colors) preview)
    {
        preview = default;

        if (!_proto.TryIndex(vessel.Minimap, out var minimap))
            return false;

        _map.CreateMap(out var mapId, runMapInit: false);
        try
        {
            if (!_mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out var grid))
                return false;

            var tiles = new List<Vector2i>();
            var colors = new List<Color>();

            foreach (var tile in _map.GetAllTiles(grid.Value.Owner, grid.Value.Comp))
            {
                var color = Color.Black;

                var tileId = _tileDefs[tile.Tile.TypeId].ID;
                foreach (var rule in minimap.Tiles)
                {
                    if (rule.Fits(tileId))
                        color = rule.Color;
                }

                var best = -1;
                foreach (var anchored in _map.GetAnchoredEntities(grid.Value.Owner, grid.Value.Comp, tile.GridIndices))
                {
                    var rule = LastRule(minimap.Entities, Prototype(anchored));
                    if (rule > best)
                        best = rule;
                }

                if (best >= 0)
                    color = minimap.Entities[best].Color;

                tiles.Add(tile.GridIndices);
                colors.Add(color);
            }

            preview = (tiles.ToArray(), colors.ToArray());
            return true;
        }
        finally
        {
            _map.DeleteMap(mapId);
        }
    }

    private EntityPrototype? Prototype(EntityUid uid) => MetaData(uid).EntityPrototype;

    private int LastRule(List<ShuttleMinimapRule> rules, EntityPrototype? proto)
    {
        if (proto == null)
            return -1;

        var ids = new HashSet<string>();
        CollectIds(proto, ids);

        for (var i = rules.Count - 1; i >= 0; i--)
        {
            if (ids.Any(rules[i].Fits))
                return i;
        }

        return -1;
    }

    private void CollectIds(EntityPrototype proto, HashSet<string> ids)
    {
        if (!ids.Add(proto.ID) || proto.Parents == null)
            return;

        foreach (var parent in proto.Parents)
        {
            if (_proto.TryIndex<EntityPrototype>(parent, out var parentProto))
                CollectIds(parentProto, ids);
            else
                ids.Add(parent);
        }
    }
}
