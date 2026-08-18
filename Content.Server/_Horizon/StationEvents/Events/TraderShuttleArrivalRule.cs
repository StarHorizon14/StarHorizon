using Content.Server._Horizon.StationEvents.Components;
using Content.Server._NF.Tools.Components;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Horizon.StationEvents.Events;

public sealed class TraderShuttleArrivalRule : StationEventSystem<TraderShuttleArrivalRuleComponent>
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private const string AnnouncementSender = "торговый шаттл";
    private const string TraderStationProto = "TraderShuttleStation";

    protected override void Started(EntityUid uid, TraderShuttleArrivalRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (FindTargetGrid(component.TargetStationName) is not { } targetGridUid)
        {
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        var loadOptions = new DeserializationOptions { InitializeMaps = true };
        if (!_mapLoader.TryLoadGrid(component.ShuttlePath, out var map, out var grid, loadOptions))
        {
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        var shuttleUid = grid.Value.Owner;
        component.ShuttleGrid = shuttleUid;
        var dummyMapUid = map.Value.Owner;

        if (TryComp<ShuttleComponent>(shuttleUid, out var shuttleComp))
            _shuttle.TryFTLDock(shuttleUid, shuttleComp, targetGridUid);

        // TryFTLDock returns false when it falls back to proximity placement (still a valid
        // arrival), so the real success signal is whether the shuttle actually left the dummy
        // staging map, not the return value itself.
        if (Transform(shuttleUid).MapUid == dummyMapUid)
        {
            Log.Warning($"TraderShuttleArrivalRule: failed to dock/place shuttle {ToPrettyString(shuttleUid)} near grid {ToPrettyString(targetGridUid)}.");
            component.ShuttleGrid = null;
            QueueDel(shuttleUid);
            QueueDel(dummyMapUid);
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        ProtectShuttleContents(shuttleUid);

        QueueDel(dummyMapUid);

        // Gives the shuttle its own minimal station (cargo order database + market data) so the
        // barter console aboard it can actually take orders - NFCargoSystem looks up orders via
        // the owning station, not the grid itself.
        var config = new StationConfig
        {
            StationPrototype = TraderStationProto,
            StationComponentOverrides = new ComponentRegistry(),
        };
        component.ShuttleStation = _station.InitializeNewStation(config, new[] { shuttleUid });

        ChatSystem.DispatchFilteredAnnouncement(
            Filter.Broadcast(),
            Loc.GetString("station-event-trader-shuttle-start-announcement"),
            sender: AnnouncementSender,
            announcementSound: new SoundPathSpecifier("/Audio/_NF/Announcements/PocketSizedAndy/andy2_bluespace_ship_arrival.ogg"));
    }

    protected override void Ended(EntityUid uid, TraderShuttleArrivalRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        if (component.ShuttleGrid is not { } shuttleGrid)
            return;

        if (!Deleted(shuttleGrid))
            QueueDel(shuttleGrid);

        if (component.ShuttleStation is { } shuttleStation && !Deleted(shuttleStation))
            QueueDel(shuttleStation);

        ChatSystem.DispatchFilteredAnnouncement(
            Filter.Broadcast(),
            Loc.GetString("station-event-trader-shuttle-end-announcement"),
            sender: AnnouncementSender,
            announcementSound: new SoundPathSpecifier("/Audio/_NF/Announcements/PocketSizedAndy/andy2_bluespace_ship_leave.ogg"));
    }

    // Docking needs an actual grid (docks are looked up as direct children of the grid entity),
    // not the station abstraction, so this resolves the station's name to one of its grids.
    private EntityUid? FindTargetGrid(string targetName)
    {
        var query = EntityQueryEnumerator<StationDataComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out var stationData, out var metaData))
        {
            if (metaData.EntityName != targetName)
                continue;

            foreach (var gridUid in stationData.Grids)
                return gridUid;
        }

        return null;
    }

    private void ProtectShuttleContents(EntityUid gridUid)
    {
        var xform = Transform(gridUid);
        var children = xform.ChildEnumerator;

        while (children.MoveNext(out var child))
        {
            var disable = EnsureComp<DisableToolUseComponent>(child);
            disable.Anchoring = true;
            disable.Prying = true;
            disable.Screwing = true;
        }
    }
}
