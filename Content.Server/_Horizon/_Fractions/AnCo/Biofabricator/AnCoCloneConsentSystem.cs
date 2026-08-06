using Content.Server.EUI;
using Content.Server.Popups;
using Content.Shared._Horizon._Fractions.AnCo.Biofabricator;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Server.Player;

namespace Content.Server._Horizon._Fractions.AnCo.Biofabricator;

public sealed class AnCoCloneConsentSystem : EntitySystem
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AnCoMemoryCardSystem _memoryCard = default!;
    [Dependency] private readonly AnCoBiofabricatorSystem _biofabricator = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var uid = args.Target;

        if (!_mind.TryGetMind(uid, out var mindId, out var mind) || mind.UserId == null)
            return;

        // No point asking if nobody has a memory card bound to this player's ckey this round.
        if (!_memoryCard.TryGetCardForUser(mind.UserId.Value, out _))
            return;

        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        _euiManager.OpenEui(new AnCoBiofabricatorConsentEui(mindId, mind, uid, this), session);
    }

    public void HandleConsent(EntityUid mindId, MindComponent mind, EntityUid deadBody, bool accepted)
    {
        if (!accepted || mind.UserId == null)
            return;

        if (!_memoryCard.TryGetCardForUser(mind.UserId.Value, out var cardUid) ||
            !TryComp<AnCoMemoryCardComponent>(cardUid, out var card))
        {
            return;
        }

        card.StoredImplants.Clear();
        if (TryComp<ImplantedComponent>(deadBody, out var implanted))
        {
            foreach (var implant in implanted.ImplantContainer.ContainedEntities)
            {
                var implantId = MetaData(implant).EntityPrototype?.ID;
                if (implantId != null)
                    card.StoredImplants.Add(implantId);
            }
        }

        card.ConsentGranted = true;

        if (mind.UserId is { } userId && _playerManager.TryGetSessionById(userId, out var session))
            _popup.PopupCursor(Loc.GetString("anco-biofabricator-consent-granted"), session);

        // The card might already be sitting in a Biofabricator - try to start the restoration right away.
        if (_biofabricator.TryFindFabricatorForCard(cardUid, out var fab))
            _biofabricator.TryStartRestore(fab, null);
    }
}
