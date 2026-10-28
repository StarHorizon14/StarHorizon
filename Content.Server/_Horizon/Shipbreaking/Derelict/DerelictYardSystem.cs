using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using System.Linq;
using Content.Server.Station.Systems;
using Content.Server._NF.Bank;
using Content.Server._NF.Shipyard.Systems;
using Content.Shared._Horizon.Shipbreaking.Derelict;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Shipbreaking.Derelict;

/// <summary>
/// The derelict yard: sells wrecks for the price of the station account and deletes them, with every piece cut off
/// them, when the lease is over. What the wreck is scrapped for pays into the account through the scrap machines and
/// counts towards the job of that wreck; when the lease is over the share that came back pays a bonus or costs a fee,
/// and the revenue raises the rank of the yard, which opens the bigger wrecks.
/// </summary>
public sealed class DerelictYardSystem : EntitySystem
{
    private const int MaxExtensions = 2;
    private const float ExtendPriceShare = 0.15f;
    private static readonly TimeSpan ExtendTime = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan FirstWarning = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LastWarning = TimeSpan.FromMinutes(1);

    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
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

    /// <summary>
    /// What the rental of a wreck is about: the time, what it was appraised at and what came back through the scrap
    /// machines. Kept under the grid that was bought, for the round.
    /// </summary>
    private sealed class Job
    {
        public EntityUid Station;
        public string Name = string.Empty;
        public int Price;
        public int Appraisal;
        public int Revenue;
        public int Extensions;
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
        SubscribeLocalEvent<DerelictYardConsoleComponent, DerelictExtendMessage>(OnExtend);
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
                CanExtend = job.Extensions < MaxExtensions,
                ExtendPrice = ExtendPrice(job),
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

    private static int ExtendPrice(Job job) => (int) (job.Price * ExtendPriceShare);

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

        if (!_shipyard.TryPurchaseShuttle(station, vessel.ShuttlePath, out var shuttle))
        {
            _bank.TrySectorDeposit(SectorBankAccount.Frontier, vessel.Price, LedgerEntryType.StationDepositOther);
            _popup.PopupEntity(Loc.GetString("derelict-yard-failed"), ent, args.Actor);
            return;
        }

        EnsureComp<DerelictLeaseComponent>(shuttle.Value).Group = shuttle.Value;
        MarkOrigin(shuttle.Value);

        _jobs[shuttle.Value] = new Job
        {
            Station = station,
            Name = Loc.GetString(vessel.Name),
            Price = vessel.Price,
            Appraisal = (int) _pricing.AppraiseGrid(shuttle.Value),
            Expires = _timing.CurTime + vessel.Lifetime,
        };

        _chat.DispatchStationAnnouncement(station,
            Loc.GetString("derelict-yard-bought", ("name", Loc.GetString(vessel.Name)), ("minutes", (int) vessel.Lifetime.TotalMinutes)),
            Loc.GetString("derelict-yard-sender"),
            playDefaultSound: false);

        UpdateAllUis();
    }

    /// <summary>
    /// Marks everything on the wreck, so what the scrap machines pay for it counts towards the job wherever it ends up.
    /// </summary>
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

    /// <summary>
    /// Counts what a scrap machine paid for something towards the job of the wreck it came from.
    /// </summary>
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

    private void OnExtend(Entity<DerelictYardConsoleComponent> ent, ref DerelictExtendMessage args)
    {
        if (!_jobs.TryGetValue(GetEntity(args.Group), out var job) || job.Extensions >= MaxExtensions ||
            job.Station != _station.GetOwningStation(ent))
            return;

        var price = ExtendPrice(job);
        if (!_bank.TryGetBalance(SectorBankAccount.Frontier, out var balance) || balance < price ||
            !_bank.TrySectorWithdraw(SectorBankAccount.Frontier, price, LedgerEntryType.StationWithdrawalSupplies))
        {
            _popup.PopupEntity(Loc.GetString("derelict-yard-no-funds"), ent, args.Actor);
            return;
        }

        job.Extensions++;
        job.Expires += ExtendTime;
        job.Warned5 = false;
        job.Warned1 = false;

        UpdateAllUis();
    }

    /// <summary>
    /// A piece cut off the wreck is part of the lease too.
    /// </summary>
    private void OnSplit(Entity<DerelictLeaseComponent> ent, ref PostGridSplitEvent args)
    {
        EnsureComp<DerelictLeaseComponent>(args.Grid).Group = ent.Comp.Group;
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
        _chat.DispatchStationAnnouncement(job.Station,
            Loc.GetString("derelict-yard-warning", ("minutes", minutes)),
            Loc.GetString("derelict-yard-sender"),
            playDefaultSound: false);
    }

    /// <summary>
    /// Ends a job: pays the bonus or takes the fee, raises the rank, then deletes the wreck with every piece of it.
    /// Whoever is still aboard is put out into space first, only the wreck goes.
    /// </summary>
    private void Finish(EntityUid group, Job job)
    {
        Settle(job);

        var grids = new HashSet<EntityUid>();
        var leases = EntityQueryEnumerator<DerelictLeaseComponent>();
        while (leases.MoveNext(out var uid, out var lease))
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

            // Straight onto the map, keeping the place: the grid under it is the one that goes.
            var xform = Transform(mob);
            if (xform.MapUid is { } map)
                _transform.SetParent(mob, xform, map);
        }

        foreach (var grid in grids)
        {
            QueueDel(grid);
        }
    }

    /// <summary>
    /// The result of a job: the share of the appraisal that came back through the scrap machines pays a bonus
    /// or costs a fee, and the revenue counts towards the rank.
    /// </summary>
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

        _chat.DispatchStationAnnouncement(job.Station,
            Loc.GetString("derelict-yard-result",
                ("name", job.Name), ("percent", percent), ("revenue", job.Revenue), ("bonus", bonus), ("fee", fee)),
            Loc.GetString("derelict-yard-sender"),
            playDefaultSound: false);

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
        _chat.DispatchStationAnnouncement(job.Station,
            Loc.GetString("derelict-yard-rank-up", ("rank", newRank)),
            Loc.GetString("derelict-yard-sender"),
            playDefaultSound: false);
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

    /// <summary>
    /// Loads the wreck on a map of its own, colours each tile by the minimap rules, and throws the map away.
    /// </summary>
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

                // The rules are laid over each other, the last one that fits gives the colour.
                var tileId = _tileDefs[tile.Tile.TypeId].ID;
                foreach (var rule in minimap.Tiles)
                {
                    if (rule.Fits(tileId))
                        color = rule.Color;
                }

                // The same for what stands on the tile, the rule that comes last among all of it wins.
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

    /// <summary>
    /// The index of the last rule that fits the prototype or one of the prototypes it comes from, so a pattern for
    /// a base prototype takes in everything made from it. -1 if none fits.
    /// </summary>
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

    /// <summary>
    /// The id of the prototype and of what it comes from. Abstract bases can not be looked up any more, so for them
    /// only the id is known, and what they come from is not.
    /// </summary>
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
