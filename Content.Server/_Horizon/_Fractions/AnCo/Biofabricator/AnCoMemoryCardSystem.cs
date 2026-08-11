using Content.Server.Popups;
using Content.Shared._Horizon._Fractions.AnCo.Biofabricator;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;

namespace Content.Server._Horizon._Fractions.AnCo.Biofabricator;

public sealed class AnCoMemoryCardSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private readonly Dictionary<NetUserId, EntityUid> _cardByOwner = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnCoMemoryCardComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AnCoMemoryCardComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AnCoMemoryCardComponent, ComponentRemove>(OnCardRemoved);
    }

    public bool TryGetCardForUser(NetUserId user, out EntityUid card)
    {
        return _cardByOwner.TryGetValue(user, out card) && Exists(card);
    }

    private void OnCardRemoved(EntityUid uid, AnCoMemoryCardComponent component, ComponentRemove args)
    {
        if (component.OwnerUserId is { } owner &&
            _cardByOwner.TryGetValue(owner, out var registered) &&
            registered == uid)
        {
            _cardByOwner.Remove(owner);
        }
    }

    private void OnExamined(EntityUid uid, AnCoMemoryCardComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(component.OwnerCharacterName is { } name
            ? Loc.GetString("anco-memory-card-examine-bound", ("name", name))
            : Loc.GetString("anco-memory-card-examine-empty"));
    }

    private void OnAfterInteract(EntityUid uid, AnCoMemoryCardComponent component, AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach || args.Handled)
            return;

        var user = args.User;

        if (!HasComp<HumanoidAppearanceComponent>(target) ||
            !_mind.TryGetMind(target, out _, out var mind) ||
            mind.UserId == null)
        {
            _popup.PopupEntity(Loc.GetString("anco-memory-card-bind-fail-not-humanoid"), user, user);
            return;
        }

        if (_mobState.IsCritical(target) || _mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("anco-memory-card-bind-fail-not-alive"), user, user);
            return;
        }

        var attempt = new AnCoMemoryCardBindAttemptEvent();
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("anco-memory-card-bind-fail-uncloneable"), user, user);
            return;
        }

        var userId = mind.UserId.Value;
        var characterName = MetaData(target).EntityName;

        // Only one active card per player - unbind the previous one, if any.
        if (_cardByOwner.TryGetValue(userId, out var previousCard) && previousCard != uid)
            UnbindCard(previousCard);

        component.OwnerUserId = userId;
        component.OwnerCharacterName = characterName;
        component.StoredImplants.Clear();
        component.ConsentGranted = false;
        _cardByOwner[userId] = uid;

        _popup.PopupEntity(Loc.GetString("anco-memory-card-bind-success", ("name", characterName)), user, user);
        args.Handled = true;
    }

    private void UnbindCard(EntityUid cardUid)
    {
        if (!TryComp<AnCoMemoryCardComponent>(cardUid, out var card))
            return;

        if (card.OwnerUserId is { } owner && _cardByOwner.TryGetValue(owner, out var registered) && registered == cardUid)
            _cardByOwner.Remove(owner);

        card.OwnerUserId = null;
        card.OwnerCharacterName = null;
        card.StoredImplants.Clear();
        card.ConsentGranted = false;
    }
}
