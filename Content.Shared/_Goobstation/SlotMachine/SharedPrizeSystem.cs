using Content.Shared.Coordinates;
using Content.Shared.EntityTable;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Goobstation.SlotMachine;

/// <summary>
/// Used for getting a weighted random prize from a list of prizes
/// </summary>
public abstract class SharedPrizeSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    /// <summary>
    /// Picks a weighted random prize from the list.
    /// </summary>
    public PrizePrototype GetRandomPrize(List<ProtoId<PrizePrototype>> prizes)
    {
        var picks = new List<PrizePrototype>(prizes.Count);
        var sum = 0f;

        foreach (var prize in prizes)
        {
            var proto = _proto.Index(prize);
            picks.Add(proto);
            sum += proto.Weight;
        }

        var rand = _random.NextFloat() * sum;
        var accumulated = 0f;

        foreach (var prize in picks)
        {
            accumulated += prize.Weight;

            if (accumulated >= rand)
                return prize;
        }

        return picks[0]; // Shouldn't be possible but just in case
    }

    /// <summary>
    /// Selects a weighted random prize from the list, spawns it, plays its audio and depending on the AnnounceType speaks or popups.
    /// </summary>
    /// <param name="prizes">List of prize prototypes to pick from</param>
    /// <param name="uid">Whatever entity is spawning the prize</param>
    public void HandlePrize(List<ProtoId<PrizePrototype>> prizes, EntityUid uid)
    {
        if (prizes.Count == 0)
            return;

        var prize = GetRandomPrize(prizes);

        if (prize.PrizeTable != null)
        {
            foreach (var item in _entityTable.GetSpawns(prize.PrizeTable))
            {
                EntityManager.PredictedSpawnAtPosition(item, uid.ToCoordinates());
            }
        }

        HandleAnnouncement(prize, uid);
        _audio.PlayPredicted(prize.WinSound, uid, uid);
    }

    private void HandleAnnouncement(PrizePrototype prize, EntityUid uid)
    {
        if (prize.WinMessage is not { } message)
            return;

        switch (prize.AnnounceType)
        {
            case AnnounceType.Speak:
                Speak(uid, Loc.GetString(message));
                break;

            case AnnounceType.Popup:
                _popupSystem.PopupPredicted(Loc.GetString(message), uid, uid);
                break;
        }
    }

    /// <summary>
    /// Makes the machine say the message in IC chat. Only does something on the server.
    /// </summary>
    public virtual void Speak(EntityUid uid, string message)
    {
    }
}
