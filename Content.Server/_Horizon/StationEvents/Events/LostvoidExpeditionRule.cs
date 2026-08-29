using System.Threading;
using Content.Server._Horizon.Salvage;
using Content.Server._Horizon.StationEvents.Components;
using Content.Server.GameTicking;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Procedural;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Horizon.StationEvents.Events;

/// <summary>
/// Public Lostvoid dungeon event: generates a planet+dungeon on its own map using the same
/// pipeline as the salvage expedition console, then opens it up as a public FTL destination
/// any shuttle can travel to for the duration of the event.
/// </summary>
public sealed class LostvoidExpeditionRule : StationEventSystem<LostvoidExpeditionRuleComponent>
{
    [Dependency] private readonly AnchorableSystem _anchorable = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly DungeonSystem _dungeon = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsoles = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private const string AnnouncementSender = "неизвестный сигнал";
    private const double JobTime = 0.002;
    private static readonly ProtoId<SalvageDifficultyPrototype> Difficulty = "NFExtreme";
    private static readonly TimeSpan ArrivalsBlockedWarning = TimeSpan.FromMinutes(5);
    private static readonly SoundSpecifier ClosingMusic = new SoundPathSpecifier("/Audio/_Horizon/Quasimorph/Fokermas-Cult.ogg");

    private readonly JobQueue _queue = new();
    private readonly Dictionary<EntityUid, (SpawnLostvoidExpeditionJob Job, CancellationTokenSource Cancel)> _jobs = new();

    protected override void Started(EntityUid uid, LostvoidExpeditionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var seed = RobustRandom.Next();

        var cancelToken = new CancellationTokenSource();
        var job = new SpawnLostvoidExpeditionJob(
            JobTime,
            EntityManager,
            _logManager,
            PrototypeManager,
            _anchorable,
            _biome,
            _dungeon,
            _metaDataSystem,
            _map,
            seed,
            Difficulty,
            cancelToken.Token);

        _jobs[uid] = (job, cancelToken);
        _queue.EnqueueJob(job);
    }

    protected override void Ended(EntityUid uid, LostvoidExpeditionRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        if (_jobs.Remove(uid, out var pending))
            pending.Cancel.Cancel();

        if (component.MapUid is not { } mapUid || Deleted(mapUid))
            return;

        // Safety net for anyone who arrived too late for the graceful recall in Update() to catch.
        RecallShuttles(mapUid, TimeSpan.Zero);

        ChatSystem.DispatchFilteredAnnouncement(
            Filter.Broadcast(),
            Loc.GetString("station-event-lostvoid-expedition-end-announcement"),
            sender: AnnouncementSender);

        if (TryComp<MapComponent>(mapUid, out var mapComp))
            Audio.PlayGlobal(ClosingMusic, Filter.BroadcastMap(mapComp.MapId), true);

        QueueDel(mapUid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _queue.Process();

        var query = EntityQueryEnumerator<LostvoidExpeditionRuleComponent, StationEventComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var component, out var stationEvent, out var gameRule))
        {
            if (_jobs.TryGetValue(uid, out var pending) && pending.Job.Status == JobStatus.Finished)
            {
                _jobs.Remove(uid);

                if (pending.Job.Exception != null || pending.Job.Result != true)
                {
                    Sawmill.Error($"LostvoidExpeditionRule: expedition generation failed for {ToPrettyString(uid)}.");
                    GameTicker.EndGameRule(uid, gameRule);
                    continue;
                }

                component.MapUid = pending.Job.MapUid;
                Dirty(uid, component);

                ChatSystem.DispatchFilteredAnnouncement(
                    Filter.Broadcast(),
                    Loc.GetString("station-event-lostvoid-expedition-start-announcement"),
                    sender: AnnouncementSender,
                    announcementSound: new SoundPathSpecifier("/Audio/_NF/Announcements/PocketSizedAndy/andy2_bluespace_ship_arrival.ogg"));
            }

            if (component.MapUid is not { } mapUid || Deleted(mapUid) || stationEvent.EndTime is not { } endTime)
                continue;

            var remaining = endTime - Timing.CurTime;

            // 5 minutes before the portal closes: stop new shuttles from FTLing in and warn everyone.
            // Shuttles already on the planet are left alone until the actual close at Ended().
            if (!component.ArrivalsBlocked && remaining <= ArrivalsBlockedWarning)
            {
                component.ArrivalsBlocked = true;
                Dirty(uid, component);

                if (TryComp<FTLDestinationComponent>(mapUid, out var dest))
                {
                    dest.Enabled = false;
                    Dirty(mapUid, dest);
                    _shuttleConsoles.RefreshShuttleConsoles();
                }

                ChatSystem.DispatchFilteredAnnouncement(
                    Filter.Broadcast(),
                    Loc.GetString("station-event-lostvoid-expedition-warning-announcement"),
                    sender: AnnouncementSender);

                if (TryComp<MapComponent>(mapUid, out var mapComp))
                    Audio.PlayGlobal(ClosingMusic, Filter.BroadcastMap(mapComp.MapId), true);
            }

            // Final countdown: same auto-recall mechanic SalvageExpeditionComponent uses - shuttles
            // still on the planet get FTLed out with a startup time trimmed to the time remaining,
            // so the departure animation finishes right as the map closes.
            if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime) + TimeSpan.FromSeconds(0.5))
                RecallShuttles(mapUid, remaining);
        }
    }

    // Sends any shuttles still on the expedition map back to the default map, mirroring
    // SalvageExpeditionComponent's own final-countdown auto-recall.
    private void RecallShuttles(EntityUid mapUid, TimeSpan remaining)
    {
        var mapId = GameTicker.DefaultMap;
        if (!MapSystem.TryGetMap(mapId, out var defaultMapUid))
            return;

        var ftlTime = (float)remaining.TotalSeconds;

        if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime))
            ftlTime = MathF.Max(0, (float)remaining.TotalSeconds - 0.5f);

        ftlTime = MathF.Min(ftlTime, _shuttle.DefaultStartupTime);

        var shuttleQuery = AllEntityQuery<ShuttleComponent, TransformComponent>();
        while (shuttleQuery.MoveNext(out var shuttleUid, out var shuttle, out var shuttleXform))
        {
            if (shuttleXform.MapUid != mapUid || HasComp<FTLComponent>(shuttleUid))
                continue;

            var dropLocation = RobustRandom.NextVector2(750f, 3500f);
            _shuttle.FTLToCoordinates(shuttleUid, shuttle, new EntityCoordinates(defaultMapUid.Value, dropLocation), 0f, ftlTime, _shuttle.DefaultTravelTime);
        }
    }
}
