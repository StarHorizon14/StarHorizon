using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._NF.Bank;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server.Stack;
using Content.Shared._Horizon.Marketplace;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Contraband;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Preferences;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Horizon.Marketplace;

/// <summary>
/// Sector-wide player marketplace. Listed items are moved into a hidden storage entity and handed to the buyer
/// at whichever console the purchase is made from. The seller receives the price minus the sector tax.
/// Listings only last for the current round.
/// </summary>
public sealed partial class MarketplaceSystem : SharedMarketplaceSystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <summary>
    /// Storage entity of each marketplace network.
    /// </summary>
    private readonly Dictionary<string, EntityUid> _storages = new();

    /// <summary>
    /// Which marketplace consoles each player currently has open. Used for PVS overrides and remote examining.
    /// </summary>
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _viewers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<MarketplaceListedItemComponent, ExamineRangeBypassEvent>(OnExamineBypass);
        SubscribeLocalEvent<MarketplaceConsoleComponent, EntInsertedIntoContainerMessage>(OnSlotInserted);
        SubscribeLocalEvent<MarketplaceConsoleComponent, EntRemovedFromContainerMessage>(OnSlotRemoved);
        SubscribeLocalEvent<MarketplaceConsoleComponent, ComponentShutdown>(OnConsoleShutdown);

        Subs.BuiEvents<MarketplaceConsoleComponent>(MarketplaceConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<MarketplaceListItemMessage>(OnListItem);
            subs.Event<MarketplaceBuyMessage>(OnBuy);
            subs.Event<MarketplaceWithdrawMessage>(OnWithdraw);
            subs.Event<MarketplaceEjectMessage>(OnEject);
            subs.Event<MarketplaceExamineRequestMessage>(OnExamineRequest);
            subs.Event<MarketplaceCreateRequestMessage>(OnCreateRequest);
            subs.Event<MarketplaceCancelRequestMessage>(OnCancelRequest);
            subs.Event<MarketplaceMakeOfferMessage>(OnMakeOffer);
            subs.Event<MarketplaceAcceptOfferMessage>(OnAcceptOffer);
            subs.Event<MarketplaceRejectOfferMessage>(OnRejectOffer);
            subs.Event<MarketplaceWithdrawOfferMessage>(OnWithdrawOffer);
        });

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);

        InitializeCartridge();
        InitializeDelivery();
    }

    #region Storage

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        // Normally done when the round ends, this catches restarts that skip the post-round stage.
        RefundAll();

        foreach (var storage in _storages.Values)
        {
            if (!TerminatingOrDeleted(storage))
                QueueDel(storage);
        }

        _storages.Clear();
        _viewers.Clear();
    }

    private Entity<MarketplaceStorageComponent> GetStorage(string network)
    {
        if (_storages.TryGetValue(network, out var existing)
            && TryComp<MarketplaceStorageComponent>(existing, out var existingComp))
        {
            return (existing, existingComp);
        }

        var uid = Spawn(null, MapCoordinates.Nullspace);
        _metaData.SetEntityName(uid, $"marketplace storage ({network})");
        var comp = EnsureComp<MarketplaceStorageComponent>(uid);
        comp.Network = network;
        comp.Salt = _random.Next();
        _container.EnsureContainer<Container>(uid, MarketplaceStorageComponent.ContainerId);
        _storages[network] = uid;

        // Players already browsing this network need to see the new storage.
        foreach (var (actor, consoles) in _viewers)
        {
            if (!consoles.Any(c => TryComp<MarketplaceConsoleComponent>(c, out var console) && console.Network == network))
                continue;

            if (TryComp<ActorComponent>(actor, out var actorComp))
                _pvsOverride.AddSessionOverride(uid, actorComp.PlayerSession);
        }

        return (uid, comp);
    }

    private bool TryGetExistingStorage(string network, [NotNullWhen(true)] out MarketplaceStorageComponent? storage)
    {
        storage = null;
        return _storages.TryGetValue(network, out var uid) && TryComp(uid, out storage);
    }

    private BaseContainer GetStorageContainer(EntityUid storage)
    {
        return _container.EnsureContainer<Container>(storage, MarketplaceStorageComponent.ContainerId);
    }

    private int GetListingCount(MarketplaceListing listing)
    {
        if (listing.StackType == null)
            return listing.Items.Count;

        var count = 0;
        foreach (var item in listing.Items)
        {
            count += _stack.GetCount(item);
        }

        return count;
    }

    private void StoreItem(Entity<MarketplaceStorageComponent> storage, MarketplaceListing listing, EntityUid item)
    {
        var listed = EnsureComp<MarketplaceListedItemComponent>(item);
        listed.ListingId = listing.Id;
        Dirty(item, listed);

        _container.Insert(item, GetStorageContainer(storage));
        listing.Items.Add(item);
    }

    /// <summary>
    /// Takes up to <paramref name="amount"/> units out of a listing and places them at <paramref name="coordinates"/>.
    /// </summary>
    private List<EntityUid> TakeFromListing(Entity<MarketplaceStorageComponent> storage, MarketplaceListing listing, int amount, EntityCoordinates coordinates)
    {
        var taken = new List<EntityUid>();
        var container = GetStorageContainer(storage);

        for (var i = listing.Items.Count - 1; i >= 0 && amount > 0; i--)
        {
            var item = listing.Items[i];
            if (TerminatingOrDeleted(item))
            {
                listing.Items.RemoveAt(i);
                continue;
            }

            var count = listing.StackType == null ? 1 : _stack.GetCount(item);
            if (count <= amount)
            {
                _container.Remove(item, container, destination: coordinates);
                RemComp<MarketplaceListedItemComponent>(item);
                listing.Items.RemoveAt(i);
                taken.Add(item);
                amount -= count;
                continue;
            }

            if (_stack.Split(item, amount, coordinates) is { } split)
            {
                RemComp<MarketplaceListedItemComponent>(split);
                taken.Add(split);
            }

            amount = 0;
        }

        if (listing.Items.Count == 0)
            storage.Comp.Listings.Remove(listing.Id);

        return taken;
    }

    #endregion

    #region Examine / viewers

    private void OnExamineBypass(Entity<MarketplaceListedItemComponent> ent, ref ExamineRangeBypassEvent args)
    {
        if (_viewers.TryGetValue(args.Examiner, out var consoles) && consoles.Count > 0)
            args.Allowed = true;
    }

    private void OnUiOpened(Entity<MarketplaceConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        var actor = args.Actor;
        _viewers.GetOrNew(actor).Add(ent);

        if (TryComp<ActorComponent>(actor, out var actorComp))
            _pvsOverride.AddSessionOverride(GetStorage(ent.Comp.Network), actorComp.PlayerSession);

        TryPayPending(actor, ent.Comp.Network);
        UpdateUi(ent);
    }

    private void OnUiClosed(Entity<MarketplaceConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        RemoveViewer(args.Actor, ent);
    }

    private void OnConsoleShutdown(Entity<MarketplaceConsoleComponent> ent, ref ComponentShutdown args)
    {
        foreach (var actor in _viewers.Keys.ToList())
        {
            RemoveViewer(actor, ent);
        }
    }

    private void RemoveViewer(EntityUid actor, EntityUid console)
    {
        if (!_viewers.TryGetValue(actor, out var consoles))
            return;

        consoles.Remove(console);
        if (consoles.Count == 0)
            _viewers.Remove(actor);

        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        // Stop sending the storages of networks the player no longer has a console open for.
        var stillOpen = new HashSet<string>();
        foreach (var open in consoles)
        {
            if (TryComp<MarketplaceConsoleComponent>(open, out var openComp))
                stillOpen.Add(openComp.Network);
        }

        foreach (var (network, storage) in _storages)
        {
            if (!stillOpen.Contains(network) && !TerminatingOrDeleted(storage))
                _pvsOverride.RemoveSessionOverride(storage, actorComp.PlayerSession);
        }
    }

    private void OnSlotInserted(Entity<MarketplaceConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == MarketplaceConsoleComponent.ItemSlotId)
            UpdateUi(ent);
    }

    private void OnSlotRemoved(Entity<MarketplaceConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == MarketplaceConsoleComponent.ItemSlotId)
            UpdateUi(ent);
    }

    #endregion

    #region Listing

    private void OnListItem(Entity<MarketplaceConsoleComponent> ent, ref MarketplaceListItemMessage args)
    {
        var user = args.Actor;
        var comp = ent.Comp;

        if (comp.ItemSlot.Item is not { } item || TerminatingOrDeleted(item))
        {
            Deny(ent, user, "marketplace-error-no-item");
            return;
        }

        if (!TryGetSeller(user, out var session, out var profile))
        {
            Deny(ent, user, "marketplace-error-no-account");
            return;
        }

        if (args.UnitPrice <= 0 || args.UnitPrice > comp.MaxUnitPrice)
        {
            Deny(ent, user, "marketplace-error-bad-price", ("max", comp.MaxUnitPrice));
            return;
        }

        if (GetDenyReason(ent, item) is { } reason)
        {
            Deny(ent, user, reason);
            return;
        }

        var storage = GetStorage(ent.Comp.Network);
        var sellerKey = (session.UserId, profile.Name);

        TryComp<StackComponent>(item, out var stack);
        var stackType = stack?.StackTypeId;
        var amount = 1;
        if (stack != null)
        {
            var available = _stack.GetCount(item, stack);
            amount = Math.Clamp(args.Amount, 1, available);
        }

        // Look for one of the seller's listings of the same resource at the same price to merge into.
        MarketplaceListing? listing = null;
        var ownListings = 0;
        foreach (var existing in storage.Comp.Listings.Values)
        {
            if (existing.SellerUserId != sellerKey.UserId || existing.SellerCharacterName != sellerKey.Name)
                continue;

            ownListings++;

            if (stackType != null
                && existing.StackType == stackType
                && existing.UnitPrice == args.UnitPrice)
            {
                listing = existing;
            }
        }

        if (listing == null && ownListings >= comp.MaxListingsPerSeller)
        {
            Deny(ent, user, "marketplace-error-too-many-listings", ("max", comp.MaxListingsPerSeller));
            return;
        }

        // Take only the requested part of a stack, the rest stays in the console.
        var toList = item;
        if (stack != null && amount < _stack.GetCount(item, stack))
        {
            if (_stack.Split(item, amount, Transform(ent).Coordinates, stack) is not { } split)
            {
                Deny(ent, user, "marketplace-error-failed");
                return;
            }

            toList = split;
        }

        var merged = listing != null;
        if (listing == null)
        {
            listing = new MarketplaceListing
            {
                Id = storage.Comp.NextId++,
                Name = Name(toList),
                ProtoId = Prototype(toList)?.ID,
                StackType = stackType,
                UnitPrice = args.UnitPrice,
                SellerUserId = session.UserId,
                SellerCharacterName = profile.Name,
                SellerIdName = GetIdName(user, profile),
                GridName = GetGridName(ent),
                ListedAt = _timing.CurTime,
            };
            storage.Comp.Listings[listing.Id] = listing;
        }
        else
        {
            // Fill up the existing stacks first, then store whatever is left as a new stack.
            foreach (var existingItem in listing.Items)
            {
                if (_stack.GetCount(toList) <= 0)
                    break;

                _stack.TryAdd(toList, existingItem);
            }
        }

        if (!TerminatingOrDeleted(toList) && (stack == null || _stack.GetCount(toList) > 0))
            StoreItem(storage, listing, toList);

        _audio.PlayPvs(comp.ConfirmSound, ent);
        _popup.PopupEntity(Loc.GetString(merged ? "marketplace-popup-merged" : "marketplace-popup-listed",
                ("item", listing.Name), ("id", listing.Id)),
            ent,
            user);

        _adminLog.Add(LogType.ATMUsage,
            LogImpact.Low,
            $"{ToPrettyString(user):actor} listed {amount}x {listing.Name} for {args.UnitPrice} per unit on the marketplace (listing #{listing.Id}) via {ToPrettyString(ent):entity}");

        UpdateAllUis(ent.Comp.Network);
    }

    /// <summary>
    /// Returns a locale id explaining why the item can't be listed, or null if it can.
    /// </summary>
    private string? GetDenyReason(Entity<MarketplaceConsoleComponent> console, EntityUid item)
    {
        if (HasComp<MarketplaceListedItemComponent>(item))
            return "marketplace-error-forbidden";

        return CheckItem(console.Comp, item, 0);
    }

    /// <summary>
    /// Checks the item and everything inside it. Returns a locale id explaining the refusal, or null if allowed.
    /// </summary>
    private string? CheckItem(MarketplaceConsoleComponent comp, EntityUid uid, int depth)
    {
        // Something absurdly nested is not worth the risk.
        if (depth > 16)
            return "marketplace-error-forbidden";

        if (TryComp<ContrabandComponent>(uid, out var contraband)
            && comp.ForbiddenContraband.Contains(contraband.Severity))
        {
            return depth == 0 ? "marketplace-error-contraband" : "marketplace-error-contraband-inside";
        }

        if (_whitelist.IsBlacklistPass(comp.Blacklist, uid)
            || HasComp<MobStateComponent>(uid)
            || TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
        {
            return "marketplace-error-forbidden";
        }

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return null;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (CheckItem(comp, contained, depth + 1) is { } reason)
                    return reason;
            }
        }

        return null;
    }

    /// <summary>
    /// Full examine text for the hover tooltip. Only for items stored on this console's network or in its tray.
    /// </summary>
    private void OnExamineRequest(Entity<MarketplaceConsoleComponent> ent, ref MarketplaceExamineRequestMessage args)
    {
        if (!TryGetEntity(args.Item, out var item) || TerminatingOrDeleted(item))
            return;

        var allowed = item == ent.Comp.ItemSlot.Item;
        if (!allowed
            && HasComp<MarketplaceListedItemComponent>(item)
            && _storages.TryGetValue(ent.Comp.Network, out var storage)
            && _container.TryGetContainingContainer(item.Value, out var container)
            && container.Owner == storage)
        {
            allowed = true;
        }

        if (!allowed)
            return;

        var text = _examine.GetExamineText(item.Value, args.Actor);
        _ui.ServerSendUiMessage(ent.Owner, MarketplaceConsoleUiKey.Key, new MarketplaceExamineResponseMessage(args.Item, text), args.Actor);
    }

    private void OnEject(Entity<MarketplaceConsoleComponent> ent, ref MarketplaceEjectMessage args)
    {
        _itemSlots.TryEjectToHands(ent, ent.Comp.ItemSlot, args.Actor, true);
    }

    private void OnWithdraw(Entity<MarketplaceConsoleComponent> ent, ref MarketplaceWithdrawMessage args)
    {
        var user = args.Actor;
        var storage = GetStorage(ent.Comp.Network);

        if (!storage.Comp.Listings.TryGetValue(args.ListingId, out var listing))
        {
            Deny(ent, user, "marketplace-error-no-listing");
            return;
        }

        if (!IsSeller(user, listing))
        {
            Deny(ent, user, "marketplace-error-not-owner");
            return;
        }

        var count = GetListingCount(listing);
        var items = TakeFromListing(storage, listing, int.MaxValue, Transform(ent).Coordinates);
        storage.Comp.Listings.Remove(listing.Id);

        foreach (var item in items)
        {
            _hands.PickupOrDrop(user, item, dropNear: true);
        }

        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        _popup.PopupEntity(Loc.GetString("marketplace-popup-withdrawn", ("item", listing.Name), ("id", listing.Id)), ent, user);
        _adminLog.Add(LogType.ATMUsage,
            LogImpact.Low,
            $"{ToPrettyString(user):actor} withdrew {count}x {listing.Name} (listing #{listing.Id}) from the marketplace via {ToPrettyString(ent):entity}");

        UpdateAllUis(ent.Comp.Network);
    }

    #endregion

    #region Buying

    private void OnBuy(Entity<MarketplaceConsoleComponent> ent, ref MarketplaceBuyMessage args)
    {
        var user = args.Actor;
        var comp = ent.Comp;
        var storage = GetStorage(ent.Comp.Network);

        if (!storage.Comp.Listings.TryGetValue(args.ListingId, out var listing))
        {
            Deny(ent, user, "marketplace-error-no-listing");
            return;
        }

        if (listing.UnitPrice != args.ExpectedUnitPrice)
        {
            Deny(ent, user, "marketplace-error-price-changed");
            UpdateUi(ent);
            return;
        }

        if (IsSeller(user, listing))
        {
            Deny(ent, user, "marketplace-error-own-listing");
            return;
        }

        if (!HasComp<BankAccountComponent>(user))
        {
            Deny(ent, user, "marketplace-error-no-account");
            return;
        }

        var available = GetListingCount(listing);
        var amount = listing.StackType == null ? 1 : args.Amount;
        if (amount <= 0 || amount > available)
        {
            Deny(ent, user, "marketplace-error-bad-amount", ("max", available));
            UpdateUi(ent);
            return;
        }

        var longTotal = (long) listing.UnitPrice * amount;
        if (longTotal > int.MaxValue)
        {
            Deny(ent, user, "marketplace-error-too-expensive");
            return;
        }

        var total = (int) longTotal;
        if (!_bank.TryBankWithdraw(user, total))
        {
            Deny(ent, user, "marketplace-error-insufficient-funds", ("amount", total));
            return;
        }

        var tax = GetTax(total, comp.TaxRate);
        var payout = total - tax;

        var paidDirectly = payout <= 0 || TryDepositToSeller(listing.SellerUserId, listing.SellerCharacterName, payout);
        if (!paidDirectly)
        {
            var key = (listing.SellerUserId, listing.SellerCharacterName);
            storage.Comp.PendingPayouts[key] = storage.Comp.PendingPayouts.GetValueOrDefault(key) + payout;
        }

        if (tax > 0 && comp.TaxAccount != SectorBankAccount.Invalid)
            _bank.TrySectorDeposit(comp.TaxAccount, tax, LedgerEntryType.MarketplaceTax);

        var items = TakeFromListing(storage, listing, amount, Transform(ent).Coordinates);
        foreach (var item in items)
        {
            _hands.PickupOrDrop(user, item, dropNear: true);
        }

        _audio.PlayPvs(comp.ConfirmSound, ent);
        _popup.PopupEntity(Loc.GetString("marketplace-popup-bought", ("item", listing.Name), ("amount", amount), ("total", total)), ent, user);

        if (_player.TryGetSessionById(listing.SellerUserId, out var sellerSession)
            && sellerSession.AttachedEntity is { } sellerMob
            && IsSeller(sellerMob, listing))
        {
            _popup.PopupEntity(Loc.GetString(paidDirectly ? "marketplace-popup-sold" : "marketplace-popup-sold-pending",
                    ("item", listing.Name),
                    ("amount", amount),
                    ("payout", payout),
                    ("tax", tax)),
                sellerMob,
                sellerMob);
        }

        _adminLog.Add(LogType.ATMUsage,
            LogImpact.Low,
            $"{ToPrettyString(user):actor} bought {amount}x {listing.Name} for {total} (tax {tax}) from {listing.SellerCharacterName} on the marketplace (listing #{listing.Id}) via {ToPrettyString(ent):entity}{(paidDirectly ? "" : ", payout is pending")}");

        UpdateAllUis(ent.Comp.Network);
    }

    /// <summary>
    /// Pays out any earnings that were held back because the seller was offline.
    /// </summary>
    private void TryPayPending(EntityUid user, string network)
    {
        if (!TryGetExistingStorage(network, out var storage)
            || !TryGetSeller(user, out var session, out var profile))
        {
            return;
        }

        var key = (session.UserId, profile.Name);
        if (!storage.PendingPayouts.TryGetValue(key, out var pending) || pending <= 0)
            return;

        if (!TryDepositToSeller(session.UserId, profile.Name, pending))
            return;

        storage.PendingPayouts.Remove(key);
        _popup.PopupEntity(Loc.GetString("marketplace-popup-pending-paid", ("amount", pending)), user, user);
        _adminLog.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(user):actor} received {pending} in pending marketplace earnings");
    }

    #endregion

    #region Accounts

    /// <summary>
    /// Gets the session and character a player would sell as.
    /// </summary>
    private bool TryGetSeller(EntityUid user,
        [NotNullWhen(true)] out ICommonSession? session,
        [NotNullWhen(true)] out HumanoidCharacterProfile? profile)
    {
        profile = null;
        session = null;

        if (!HasComp<BankAccountComponent>(user)
            || !_player.TryGetSessionByEntity(user, out session)
            || !_prefs.TryGetCachedPreferences(session.UserId, out var prefs)
            || prefs.SelectedCharacter is not HumanoidCharacterProfile selected)
        {
            return false;
        }

        profile = selected;
        return true;
    }

    private bool IsSeller(EntityUid user, MarketplaceListing listing)
    {
        return TryGetSeller(user, out var session, out var profile)
               && session.UserId == listing.SellerUserId
               && profile.Name == listing.SellerCharacterName;
    }

    /// <summary>
    /// Bank accounts live in the character profiles, which are only cached while the owner is connected.
    /// </summary>
    private bool TryDepositToSeller(NetUserId userId, string characterName, int amount)
    {
        if (!_player.TryGetSessionById(userId, out var session)
            || !_prefs.TryGetCachedPreferences(userId, out var prefs))
        {
            return false;
        }

        HumanoidCharacterProfile? profile = null;
        foreach (var character in prefs.Characters.Values)
        {
            if (character is HumanoidCharacterProfile humanoid && humanoid.Name == characterName)
            {
                profile = humanoid;
                break;
            }
        }

        if (profile == null)
            return false;

        // Prefer the entity overload when the seller is playing that character, so their balance display stays in sync.
        if (session.AttachedEntity is { } mob
            && HasComp<BankAccountComponent>(mob)
            && ReferenceEquals(prefs.SelectedCharacter, profile))
        {
            return _bank.TryBankDeposit(mob, amount);
        }

        return _bank.TryBankDeposit(session, prefs, profile, amount, out _);
    }

    private string GetIdName(EntityUid user, HumanoidCharacterProfile profile)
    {
        if (!_idCard.TryFindIdCard(user, out var idCard))
            return Loc.GetString("marketplace-no-id");

        var name = string.IsNullOrWhiteSpace(idCard.Comp.FullName) ? profile.Name : idCard.Comp.FullName;
        var job = idCard.Comp.LocalizedJobTitle;

        return string.IsNullOrWhiteSpace(job) ? name : $"{name} ({job})";
    }

    private string GetGridName(EntityUid console)
    {
        return Transform(console).GridUid is { } grid
            ? Name(grid)
            : Loc.GetString("marketplace-unknown-grid");
    }

    #endregion

    #region UI

    private void Deny(EntityUid console, EntityUid user, string message, params (string, object)[] args)
    {
        if (TryComp<MarketplaceConsoleComponent>(console, out var comp))
            _audio.PlayPvs(comp.ErrorSound, console);

        _popup.PopupEntity(Loc.GetString(message, args), console, user);
    }

    private void UpdateAllUis(string network)
    {
        var query = EntityQueryEnumerator<MarketplaceConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Network == network && _ui.IsUiOpen(uid, MarketplaceConsoleUiKey.Key))
                UpdateUi((uid, comp));
        }

        UpdateCartridges(network);
    }

    private MarketplaceListingData ToData(MarketplaceListing listing,
        MarketplaceStorageComponent storage,
        bool anonymous,
        bool isOwn,
        bool includeEntity)
    {
        var display = listing.Items.FirstOrDefault(e => !TerminatingOrDeleted(e));
        return new MarketplaceListingData
        {
            Id = listing.Id,
            Name = listing.Name,
            ProtoId = listing.ProtoId,
            DisplayEntity = includeEntity && display.Valid ? GetNetEntity(display) : null,
            Stackable = listing.StackType != null,
            StackType = listing.StackType,
            Count = GetListingCount(listing),
            UnitPrice = listing.UnitPrice,
            SellerName = DisplayName(anonymous, storage, listing.SellerUserId, listing.SellerCharacterName),
            SellerIdName = DisplayIdName(anonymous, listing.SellerIdName),
            GridName = DisplayGrid(anonymous, storage, listing.GridName),
            IsOwn = isOwn,
            ListedAt = listing.ListedAt,
        };
    }

    private void UpdateUi(Entity<MarketplaceConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, MarketplaceConsoleUiKey.Key))
            return;

        TryGetExistingStorage(ent.Comp.Network, out var storage);

        var gridName = GetGridName(ent);

        // Slot information is the same for everyone.
        NetEntity? slotEntity = null;
        string? slotName = null;
        string? slotStackType = null;
        string? slotDeny = null;
        var slotStackable = false;
        var slotCount = 0;
        if (ent.Comp.ItemSlot.Item is { } slotItem && !TerminatingOrDeleted(slotItem))
        {
            slotEntity = GetNetEntity(slotItem);
            slotName = Name(slotItem);
            if (TryComp<StackComponent>(slotItem, out var slotStack))
            {
                slotStackable = true;
                slotStackType = slotStack.StackTypeId;
                slotCount = _stack.GetCount(slotItem, slotStack);
            }
            else
            {
                slotCount = 1;
            }

            if (GetDenyReason(ent, slotItem) is { } deny)
                slotDeny = Loc.GetString(deny);
        }

        foreach (var actor in _ui.GetActors(ent.Owner, MarketplaceConsoleUiKey.Key).ToList())
        {
            NetUserId? viewerId = null;
            string? viewerName = null;
            if (TryGetSeller(actor, out var session, out var profile))
            {
                viewerId = session.UserId;
                viewerName = profile.Name;
            }

            var listings = new List<MarketplaceListingData>();
            if (storage != null)
            {
                foreach (var listing in storage.Listings.Values)
                {
                    listings.Add(ToData(listing,
                        storage,
                        ent.Comp.Anonymous,
                        viewerId == listing.SellerUserId && viewerName == listing.SellerCharacterName,
                        includeEntity: true));
                }
            }

            var hasAccount = _bank.TryGetBalance(actor, out var balance);
            var pending = 0;
            if (storage != null && viewerId is { } id && viewerName != null)
                pending = storage.PendingPayouts.GetValueOrDefault((id, viewerName));

            var state = new MarketplaceConsoleState(listings,
                balance,
                hasAccount,
                pending,
                ent.Comp.TaxRate,
                ent.Comp.MaxUnitPrice,
                gridName,
                slotEntity,
                slotName,
                slotStackable,
                slotStackType,
                slotCount,
                slotDeny);

            state.MaxRequestsPerPlayer = ent.Comp.MaxRequestsPerRequester;
            if (storage != null)
                FillRequests(ent.Comp, state, storage, viewerId, viewerName);

            _ui.ServerSendUiMessage(ent.Owner, MarketplaceConsoleUiKey.Key, state, actor);
        }
    }

    #endregion
}
