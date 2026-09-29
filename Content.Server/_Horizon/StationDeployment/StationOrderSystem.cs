using System.Linq;
using System.Numerics;
using Content.Server._Horizon.StationDeployment.Components;
using Content.Server._NF.Shipyard.Systems;
using Content.Server.Cargo.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Horizon.StationDeployment;
using Content.Shared._Horizon.StationDeployment.Components;
using Content.Shared._Horizon.StationDeployment.Prototypes;
using Content.Shared.Cargo.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Research.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.StationDeployment;

/// <summary>
/// Manages a deployed station's order pool and its task console's "summon"/"recall" cargo capsule flow.
/// </summary>
public sealed class StationOrderSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly ShuttleSystem _shuttles = default!;
    [Dependency] private readonly DockingSystem _docking = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string CargoCapsuleDockTag = "CargoCapsuleDock";

    /// <summary>
    /// Space between capsules queued up on the holding map, so simultaneous summons from
    /// different stations don't overlap.
    /// </summary>
    private const float CapsuleSpawnBuffer = 5f;

    /// <summary>
    /// A dedicated, hidden map capsules spawn onto before FTLing to their station - mirrors
    /// ShipyardSystem's ShipyardMap so capsules travel through proper FTL instead of just
    /// drifting over from a point on the station's own map.
    /// </summary>
    private MapId? _capsuleHoldingMap;

    private float _capsuleSpawnIndex;

    // Note: the base Industrial/Arsenal/Experimental/CivilianServices disciplines
    // (Resources/Prototypes/Research/disciplines.yml) are made abstract by this fork's
    // /Prototypes/Research entry in Resources/IgnoredPrototypes/ignoredPrototypes.yml ("Moved
    // science techs"), so they can't be Index()'d - use the fork's own active NF discipline set
    // instead (same sprite states, so visually equivalent to the vanilla icons).
    private static readonly ProtoId<TechDisciplinePrototype>[] DevelopmentCategories =
    {
        "NFEngineering", "NFArsenalMercenary", "NFScience", "NFService"
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationOrderDatabaseComponent, MapInitEvent>(OnOrderDbMapInit);
        SubscribeLocalEvent<StationTaskConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<StationTaskConsoleComponent, StationOrderSummonCapsuleMessage>(OnSummon);
        SubscribeLocalEvent<StationTaskConsoleComponent, StationOrderRecallCapsuleMessage>(OnRecall);
        SubscribeLocalEvent<StationTaskConsoleComponent, StationOrderCancelMessage>(OnCancelOrder);
        SubscribeLocalEvent<CargoCapsuleComponent, FTLCompletedEvent>(OnCapsuleDocked);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        CleanupCapsuleMap();
    }

    private void SetupCapsuleMapIfNeeded()
    {
        if (_capsuleHoldingMap != null && _map.MapExists(_capsuleHoldingMap.Value))
            return;

        _map.CreateMap(out var holdingMap);
        _capsuleHoldingMap = holdingMap;
        _capsuleSpawnIndex = 0f;

        _map.SetPaused(_capsuleHoldingMap.Value, false);
    }

    private void CleanupCapsuleMap()
    {
        if (_capsuleHoldingMap == null || !_map.MapExists(_capsuleHoldingMap.Value))
        {
            _capsuleHoldingMap = null;
            return;
        }

        _map.DeleteMap(_capsuleHoldingMap.Value);
        _capsuleHoldingMap = null;
    }

    private void OnOrderDbMapInit(Entity<StationOrderDatabaseComponent> ent, ref MapInitEvent args)
    {
        FillOrderDatabase(ent);
    }

    private void OnUiOpened(Entity<StationTaskConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSummon(Entity<StationTaskConsoleComponent> ent, ref StationOrderSummonCapsuleMessage args)
    {
        if (_station.GetOwningStation(ent.Owner) is not { Valid: true } station)
            return;

        if (FindActiveCapsule(station) != null)
        {
            _popup.PopupEntity(Loc.GetString("station-order-console-capsule-already-present"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (!TryComp<StationOrderDatabaseComponent>(station, out var orderDb))
            return;

        if (_timing.CurTime < orderDb.NextSummonTime)
        {
            var secondsLeft = (int) Math.Ceiling((orderDb.NextSummonTime - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("station-order-console-capsule-cooldown", ("seconds", secondsLeft)), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (!TryComp<StationBankAccountComponent>(station, out var bank) || GetBalance(bank) < ent.Comp.SummonCost)
        {
            _popup.PopupEntity(Loc.GetString("bank-insufficient-funds"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (Transform(ent.Owner).GridUid is not { Valid: true } consoleGrid)
            return;

        SetupCapsuleMapIfNeeded();

        var spawnPos = new Vector2(_capsuleSpawnIndex, 0f);
        if (!_mapLoader.TryLoadGrid(_capsuleHoldingMap!.Value, ent.Comp.CapsulePath, out var capsuleGrid, offset: spawnPos))
        {
            _popup.PopupEntity(Loc.GetString("station-order-console-capsule-spawn-failed"), ent, args.Actor, PopupType.MediumCaution);
            return;
        }

        _capsuleSpawnIndex += capsuleGrid.Value.Comp.LocalAABB.Width + CapsuleSpawnBuffer;

        // Only charge and start the cooldown once we know the capsule actually spawned.
        _cargo.UpdateBankAccount((station, bank), -ent.Comp.SummonCost, bank.PrimaryAccount);
        orderDb.NextSummonTime = _timing.CurTime + ent.Comp.SummonCooldown;

        var capsuleComp = EnsureComp<CargoCapsuleComponent>(capsuleGrid.Value.Owner);
        capsuleComp.OwningStation = station;

        var shuttle = EnsureComp<ShuttleComponent>(capsuleGrid.Value.Owner);
        _shuttles.FTLToDock(capsuleGrid.Value.Owner, shuttle, consoleGrid, hyperspaceTime: ent.Comp.CapsuleTravelTime, priorityTag: CargoCapsuleDockTag);

        UpdateUi(ent);
    }

    private void OnRecall(Entity<StationTaskConsoleComponent> ent, ref StationOrderRecallCapsuleMessage args)
    {
        if (_station.GetOwningStation(ent.Owner) is not { Valid: true } station)
            return;

        if (FindActiveCapsule(station) is not { } capsule || !capsule.Comp.Docked)
            return;

        var mobQuery = GetEntityQuery<MobStateComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        if (_shipyard.FoundOrganics(capsule.Owner, mobQuery, xformQuery) is { } organicName)
        {
            _popup.PopupEntity(Loc.GetString("station-order-console-organics-aboard", ("name", organicName)), ent, args.Actor, PopupType.MediumCaution);
            return;
        }

        var fulfilled = EvaluateAndConsume(station, capsule.Owner);

        // Sell everything in the capsule for station funds, same as a shuttle sold at the shipyard.
        var bill = (int) _pricing.AppraiseGrid(capsule.Owner);
        var sold = false;
        if (bill > 0 && TryComp<StationBankAccountComponent>(station, out var bank))
        {
            _cargo.UpdateBankAccount((station, bank), bill, bank.PrimaryAccount);
            sold = true;
        }

        // Raise the normal sold event (with the station we already resolved, since the capsule's own
        // grid was never added to StationDataComponent.Grids) so cargo bounties, the salvage job
        // board and market data all pick up on whatever was aboard, same as selling through a pallet
        // console.
        var soldEntities = new HashSet<EntityUid>();
        var children = Transform(capsule.Owner).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (TryComp<TransformComponent>(child, out var childXform) && childXform.Anchored)
                continue;

            soldEntities.Add(child);
        }

        if (soldEntities.Count > 0)
        {
            var soldEv = new EntitySoldEvent(soldEntities, station, args.Actor);
            RaiseLocalEvent(ref soldEv);
        }

        _docking.UndockDocks(capsule.Owner);
        QueueDel(capsule.Owner);

        if (sold)
        {
            _popup.PopupEntity(Loc.GetString("station-task-console-capsule-sold", ("amount", bill)), ent, args.Actor, PopupType.Medium);
        }

        // Selling for credits without matching an order is a normal outcome, not a failure - only
        // warn when the capsule genuinely did nothing (empty or worthless, and no order matched).
        if (fulfilled.Count == 0 && !sold)
        {
            _popup.PopupEntity(Loc.GetString("station-task-console-recall-no-match"), ent, args.Actor, PopupType.SmallCaution);
        }
        else if (fulfilled.Count > 0)
        {
            foreach (var (category, level) in fulfilled)
            {
                var categoryName = _protoMan.TryIndex(category, out var discipline) ? Loc.GetString(discipline.Name) : category.Id;
                _popup.PopupEntity(Loc.GetString("station-task-console-order-fulfilled",
                    ("category", categoryName), ("level", level)),
                    ent, args.Actor, PopupType.Medium);
            }
        }

        UpdateUi(ent);
    }

    private void OnCancelOrder(Entity<StationTaskConsoleComponent> ent, ref StationOrderCancelMessage args)
    {
        if (_station.GetOwningStation(ent.Owner) is not { Valid: true } station)
            return;

        if (!TryComp<StationOrderDatabaseComponent>(station, out var orderDb) ||
            !TryComp<StationDevelopmentComponent>(station, out var devel))
            return;

        var orderId = args.OrderId;
        var index = orderDb.Orders.FindIndex(o => o.Id == orderId);
        if (index == -1)
            return;

        var order = orderDb.Orders[index];
        orderDb.Orders.RemoveAt(index);

        if (_protoMan.TryIndex(order.Order, out var prototype))
        {
            devel.Progress.TryGetValue(prototype.Category, out var progress);
            devel.Progress[prototype.Category] = Math.Max(0, progress - 1);
        }

        // Refill just the category slot that was cancelled, keeping exactly one order per category.
        FillOrderDatabase((station, orderDb));

        _popup.PopupEntity(Loc.GetString("station-task-console-order-cancelled"), ent, args.Actor, PopupType.SmallCaution);

        UpdateUi(ent);
    }

    private void OnCapsuleDocked(Entity<CargoCapsuleComponent> ent, ref FTLCompletedEvent args)
    {
        ent.Comp.Docked = true;

        if (ent.Comp.OwningStation is not { Valid: true } station)
            return;

        var query = EntityQueryEnumerator<StationTaskConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (_station.GetOwningStation(uid) == station)
                UpdateUi((uid, console));
        }
    }

    /// <summary>
    /// Checks the capsule's contents against every active order, removing and crediting the
    /// ones that are satisfied. Returns the category/new-progress pairs for orders that were
    /// fulfilled, so the caller can report what actually happened.
    /// </summary>
    private List<(ProtoId<TechDisciplinePrototype> Category, int Progress)> EvaluateAndConsume(EntityUid station, EntityUid capsuleGrid)
    {
        var fulfilled = new List<(ProtoId<TechDisciplinePrototype>, int)>();

        if (!TryComp<StationOrderDatabaseComponent>(station, out var orderDb) ||
            !TryComp<StationDevelopmentComponent>(station, out var devel))
            return fulfilled;

        var entities = new HashSet<EntityUid>();
        var enumerator = Transform(capsuleGrid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            entities.UnionWith(_cargo.GetBountyEntities(child));
        }

        foreach (var order in orderDb.Orders.ToArray())
        {
            if (!_protoMan.TryIndex(order.Order, out var prototype))
                continue;

            if (!_cargo.IsBountyComplete(entities, prototype.Entries, out var usedEntities))
                continue;

            // Don't let the same physical items satisfy more than one order in this pass.
            entities.ExceptWith(usedEntities);

            orderDb.Orders.Remove(order);
            devel.Progress.TryGetValue(prototype.Category, out var progress);
            progress += 1;
            devel.Progress[prototype.Category] = progress;
            fulfilled.Add((prototype.Category, progress));
        }

        FillOrderDatabase((station, orderDb));
        return fulfilled;
    }

    /// <summary>
    /// Keeps exactly one active order per development category, so the console always shows
    /// one card per category rather than a randomly-skewed pool.
    /// </summary>
    private void FillOrderDatabase(Entity<StationOrderDatabaseComponent> ent)
    {
        foreach (var category in DevelopmentCategories)
        {
            var hasOrder = ent.Comp.Orders.Any(o =>
                _protoMan.TryIndex(o.Order, out var proto) && proto.Category == category);

            if (!hasOrder)
                TryAddOrderForCategory(ent, category);
        }
    }

    private bool TryAddOrderForCategory(Entity<StationOrderDatabaseComponent> ent, ProtoId<TechDisciplinePrototype> category)
    {
        var candidates = _protoMan.EnumeratePrototypes<StationOrderPrototype>()
            .Where(o => o.Category == category)
            .ToList();

        if (candidates.Count == 0)
            return false;

        var picked = _random.Pick(candidates);
        ent.Comp.Orders.Add(new StationOrderData(picked, ent.Comp.TotalOrders));
        ent.Comp.TotalOrders++;
        return true;
    }

    /// <summary>
    /// Internal (not private) so StationControlConsoleSystem can find the docked capsule to deliver
    /// purchased upgrade equipment to.
    /// </summary>
    internal Entity<CargoCapsuleComponent>? FindActiveCapsule(EntityUid station)
    {
        var query = EntityQueryEnumerator<CargoCapsuleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.OwningStation == station)
                return (uid, comp);
        }

        return null;
    }

    private static int GetBalance(StationBankAccountComponent bank)
    {
        return bank.Accounts.GetValueOrDefault(bank.PrimaryAccount, 0);
    }

    private void UpdateUi(Entity<StationTaskConsoleComponent> ent)
    {
        if (_station.GetOwningStation(ent.Owner) is not { Valid: true } station)
            return;

        if (!TryComp<StationOrderDatabaseComponent>(station, out var orderDb) ||
            !TryComp<StationDevelopmentComponent>(station, out var devel))
            return;

        var orders = orderDb.Orders.Select(o => new StationOrderUiEntry(o.Id, o.Order)).ToList();
        var levels = BuildLevels(devel);
        var capsule = FindActiveCapsule(station);

        _uiSystem.SetUiState(ent.Owner, StationTaskConsoleUiKey.Key,
            new StationTaskConsoleBuiState(orders, levels, capsule != null, capsule?.Comp.Docked ?? false, ent.Comp.SummonCost, orderDb.NextSummonTime));
    }

    private static Dictionary<ProtoId<TechDisciplinePrototype>, int> BuildLevels(StationDevelopmentComponent devel)
    {
        var levels = new Dictionary<ProtoId<TechDisciplinePrototype>, int>();
        foreach (var category in DevelopmentCategories)
        {
            devel.Progress.TryGetValue(category, out var progress);
            levels[category] = progress;
        }

        return levels;
    }
}
