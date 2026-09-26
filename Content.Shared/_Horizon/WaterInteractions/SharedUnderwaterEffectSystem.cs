using Content.Shared.Body.Components;
using Content.Shared.Inventory;
using Content.Shared.Overlays;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Horizon.WaterInteractions;

public sealed class SharedUnderwaterEffectSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityUid? _soundEntity;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<InGasEvent>(OnGasState);
    }

    private void OnGasState(InGasEvent msg, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(msg.Entity, out var ent))
            return;

        var entUid = ent.Value;

        if (!TryComp<BodyComponent>(entUid, out _))
            return;

        if (msg.InWater && _soundEntity == null)
        {
            SoundSpecifier rumblingSound = new SoundPathSpecifier("/Audio/_Horizon/Ambience/Ocean/rumbling.ogg");
            var audio = _audio.PlayGlobal(rumblingSound, entUid, AudioParams.Default.WithVolume(-3f).WithLoop(true));
            _soundEntity = audio?.Entity;

            if (_inventory.TryGetSlotEntity(entUid, "head", out var headEnt)
                && HasComp<AirtightHelmetComponent>(headEnt))
                return;

            var viewer = EnsureComp<WaterViewerComponent>(entUid);
            viewer.StartTime = _timing.CurTime;
        }
        else if (!msg.InWater && _soundEntity != null)
        {
            _audio.Stop(_soundEntity);
            _soundEntity = null;
            RemComp<WaterViewerComponent>(entUid);
        }
    }
}
