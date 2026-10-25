using System.Linq;
using Content.Server.AlertLevel;
using Content.Server.CrewManifest;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Horizon.ShiftStartEffect;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Horizon.ShiftStartEffect;

/// <summary>
/// Отправляет игроку данные для интро появления (станция, экипаж по отделам).
/// Не применяется к наблюдателям/призракам.
/// </summary>
public sealed class ShiftStartEffectSystem : EntitySystem
{
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly CrewManifestSystem _crewManifest = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private const int MaxColumns = 3;
    private const int MaxEntriesPerColumn = 6;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Silent || HasComp<GhostComponent>(ev.Mob))
            return;

        var title = Loc.GetString("shift-start-intro-unknown-station");
        var (threat, threatColor) = GetThreat(ev.Mob);
        var groups = new Dictionary<string, List<ShiftStartIntroEntry>>();
        var total = 0;

        if (_station.GetOwningStation(ev.Mob) is { } station)
        {
            var (name, entries) = _crewManifest.GetCrewManifest(station);
            if (!string.IsNullOrEmpty(name))
                title = name;

            foreach (var entry in entries?.Entries ?? Array.Empty<Content.Shared.CrewManifest.CrewManifestEntry>())
            {
                var header = GetDepartmentName(entry.JobPrototype);
                if (!groups.TryGetValue(header, out var list))
                    groups[header] = list = new();

                list.Add(new ShiftStartIntroEntry(entry.Name, entry.JobTitle));
                total++;
            }
        }

        var columns = groups
            .OrderByDescending(g => g.Value.Count)
            .Take(MaxColumns)
            .Select(g => new ShiftStartIntroColumn(
                g.Key,
                g.Value.Count,
                g.Value.Take(MaxEntriesPerColumn).ToArray()))
            .ToArray();

        var lines = new[]
        {
            Loc.GetString("shift-start-intro-line-corp", ("round", _ticker.RoundId)),
            Loc.GetString("shift-start-intro-line-pop", ("sessions", _players.PlayerCount)),
            Loc.GetString("shift-start-intro-line-threat", ("level", threat)),
            Loc.GetString("shift-start-intro-line-manifest"),
        };

        var terminalLine = Loc.GetString("shift-start-intro-terminal", ("id", $"NT-{_random.Next(100, 1000)}"));
        var recordLine = Loc.GetString("shift-start-intro-record",
            ("id", $"{_ticker.RoundId}-{RandomLetters(4)}"));
        var subjectLine = Loc.GetString("shift-start-intro-subject", ("name", MetaData(ev.Mob).EntityName));

        RaiseNetworkEvent(
            new ShiftStartIntroEvent(title, Loc.GetString("shift-start-intro-company"), lines, threatColor, _ticker.RoundDuration(), terminalLine, recordLine, subjectLine, columns),
            ev.Player);
    }

    private string RandomLetters(int count)
    {
        var chars = new char[count];
        for (var i = 0; i < count; i++)
            chars[i] = (char) ('A' + _random.Next(26));

        return new string(chars);
    }

    private (string, Color) GetThreat(EntityUid mob)
    {
        var id = _alertLevel.GetLevel(mob);
        if (string.IsNullOrEmpty(id))
            return (Loc.GetString("alert-level-unknown").TrimEnd('.').ToUpperInvariant(), Color.White);

        var color = Color.White;
        var query = EntityQueryEnumerator<AlertLevelComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.AlertLevels != null && comp.AlertLevels.Levels.TryGetValue(id, out var detail))
            {
                color = detail.Color;
                break;
            }
        }

        return (Loc.GetString($"alert-level-{id}").ToUpperInvariant(), color);
    }

    private string GetDepartmentName(string jobId)
    {
        foreach (var dept in _prototype.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (dept.Roles.Contains(jobId))
                return Loc.GetString(dept.Name);
        }

        return Loc.GetString("shift-start-intro-department-other");
    }
}
