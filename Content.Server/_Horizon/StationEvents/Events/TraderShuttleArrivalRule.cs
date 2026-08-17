using Content.Server._Horizon.StationEvents.Components;
using Content.Server._NF.Tools.Components;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;

namespace Content.Server._Horizon.StationEvents.Events;

public sealed class TraderShuttleArrivalRule : StationEventSystem<TraderShuttleArrivalRuleComponent>
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    protected override void Started(EntityUid uid, TraderShuttleArrivalRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (FindTargetStation(component.TargetStationName) is not { } stationUid)
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

        if (TryComp<ShuttleComponent>(shuttleUid, out var shuttleComp))
            _shuttle.TryFTLDock(shuttleUid, shuttleComp, stationUid);

        ProtectShuttleContents(shuttleUid);

        QueueDel(map.Value.Owner);
    }

    protected override void Ended(EntityUid uid, TraderShuttleArrivalRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        if (component.ShuttleGrid is { } shuttleGrid && !Deleted(shuttleGrid))
            QueueDel(shuttleGrid);
    }

    private EntityUid? FindTargetStation(string targetName)
    {
        var query = EntityQueryEnumerator<StationDataComponent, MetaDataComponent>();
        while (query.MoveNext(out var stationUid, out _, out var metaData))
        {
            if (metaData.EntityName == targetName)
                return stationUid;
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
