using Content.Server._Horizon.Shipbreaking.Derelict;
using Content.Shared._Horizon.MusicRadio;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.MusicRadio;

public sealed class MusicRadioSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly DerelictYardSystem _yard = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MusicRadioComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<MusicRadioComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnToggled(Entity<MusicRadioComponent> ent, ref ItemToggledEvent args)
    {
        if (args.Activated)
            PlayNext(ent);
        else
            Stop(ent);
    }

    private void OnShutdown(Entity<MusicRadioComponent> ent, ref ComponentShutdown args)
    {
        Stop(ent);
    }

    private void Stop(Entity<MusicRadioComponent> ent)
    {
        _audio.Stop(ent.Comp.Stream);
        ent.Comp.Stream = null;
    }

    /// <summary>
    /// Starts a random track of the collection, never the one that has just played.
    /// </summary>
    private void PlayNext(Entity<MusicRadioComponent> ent)
    {
        ent.Comp.Tension = IsEndNear(ent.Comp);

        var id = ent.Comp.Tension ? ent.Comp.TensionCollection : ent.Comp.Collection;
        if (!_proto.TryIndex(id, out var collection) || collection.PickFiles.Count == 0)
            return;

        var files = collection.PickFiles;
        var pick = files[_random.Next(files.Count)];
        if (files.Count > 1 && pick.ToString() == ent.Comp.Last)
            pick = files[(files.IndexOf(pick) + 1) % files.Count];

        ent.Comp.Last = pick.ToString();

        var spec = new SoundPathSpecifier(pick, AudioParams.Default.WithVolume(ent.Comp.Volume).WithMaxDistance(ent.Comp.MaxDistance));
        ent.Comp.Stream = _audio.PlayPvs(spec, ent)?.Entity;
    }

    /// <summary>
    /// Whether the time of a leased wreck is running out: the tension tracks play then.
    /// </summary>
    private bool IsEndNear(MusicRadioComponent radio)
    {
        return _yard.IsLeaseEnding(radio.TensionTime);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MusicRadioComponent>();
        while (query.MoveNext(out var uid, out var radio))
        {
            if (radio.NextCheck > _timing.CurTime)
                continue;

            radio.NextCheck = _timing.CurTime + CheckInterval;

            if (!_toggle.IsActivated(uid))
                continue;

            // A lease is running out: the regular track is cut off and the tension ones begin.
            if (!radio.Tension && IsEndNear(radio))
            {
                Stop((uid, radio));
                PlayNext((uid, radio));
                continue;
            }

            // A track that has ended is replaced by the next random one.
            if (!Exists(radio.Stream))
                PlayNext((uid, radio));
        }
    }
}
