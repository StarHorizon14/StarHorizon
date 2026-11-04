using Content.Shared._Horizon.Shuttles.Components;
using Content.Shared._Horizon.SpaceHazards;
using Content.Shared.Paper;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Shuttles.Systems;

/// <summary>
/// Ties every secret tunnel map (syndicate outpost, pirate cove) to a random expedition planet and writes the planet's
/// name into the tunnel notes handed to syndicate and pirate players. It checks every few seconds because the planets
/// and the hidden bases are created independently of each other.
/// </summary>
public sealed class SecretTunnelSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    private readonly List<EntityUid> _planets = new();
    private readonly List<Entity<SecretTunnelNoteComponent>> _pendingNotes = new();
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecretTunnelNoteComponent, MapInitEvent>(OnNoteInit);
    }

    private void OnNoteInit(Entity<SecretTunnelNoteComponent> ent, ref MapInitEvent args)
    {
        if (!TryWriteNote(ent))
            _pendingNotes.Add(ent);
    }

    /// <summary>Writes the planet's name into the note. False when the tunnel or its planet does not exist yet.</summary>
    private bool TryWriteNote(Entity<SecretTunnelNoteComponent> note)
    {
        var tunnels = EntityQueryEnumerator<SecretTunnelMapComponent>();
        while (tunnels.MoveNext(out var uid, out var tunnel))
        {
            if (tunnel.Id != note.Comp.TunnelId)
                continue;

            if (!EnsurePlanet(uid, tunnel) || tunnel.Planet is not { } planet)
                return false;

            _paper.SetContent(note.Owner, Loc.GetString(note.Comp.Text, ("planet", Name(planet))));
            return true;
        }

        return false;
    }

    /// <summary>Picks the tunnel's planet if it has none (or lost it). False when there is no planet to pick.</summary>
    private bool EnsurePlanet(EntityUid uid, SecretTunnelMapComponent tunnel)
    {
        if (tunnel.Planet is { } current && !TerminatingOrDeleted(current))
            return true;

        _planets.Clear();
        var planets = EntityQueryEnumerator<ExpeditionPlanetComponent>();
        while (planets.MoveNext(out var planetUid, out _))
        {
            _planets.Add(planetUid);
        }

        if (_planets.Count == 0)
            return false;

        tunnel.Planet = _random.Pick(_planets);
        Dirty(uid, tunnel);
        Log.Info($"Secret tunnel {ToPrettyString(uid)} is tied to expedition planet {ToPrettyString(tunnel.Planet.Value)}");
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        var tunnels = EntityQueryEnumerator<SecretTunnelMapComponent>();
        while (tunnels.MoveNext(out var uid, out var tunnel))
        {
            EnsurePlanet(uid, tunnel);
        }

        for (var i = _pendingNotes.Count - 1; i >= 0; i--)
        {
            var note = _pendingNotes[i];
            if (TerminatingOrDeleted(note.Owner) || TryWriteNote(note))
                _pendingNotes.RemoveAt(i);
        }
    }
}
