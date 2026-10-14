using System.Diagnostics.CodeAnalysis;
using Content.Server._NF.Bank;
using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Shared._Horizon.PaymentTerminal;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Preferences;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Horizon.PaymentTerminal;

public sealed class PaymentTerminalSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaymentTerminalComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PaymentTerminalComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PaymentTerminalComponent, GetVerbsEvent<Verb>>(OnGetVerbs);

        Subs.BuiEvents<PaymentTerminalComponent>(PaymentTerminalUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<PaymentTerminalKeypadMessage>(OnKeypad);
            subs.Event<PaymentTerminalClearMessage>(OnClear);
            subs.Event<PaymentTerminalEnterMessage>(OnEnter);
        });
    }

    private void OnExamined(Entity<PaymentTerminalComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.OwnerIdName is { } name)
            args.PushMarkup(Loc.GetString("payment-terminal-examine-bound", ("name", name)));
        else
            args.PushMarkup(Loc.GetString("payment-terminal-examine-unbound"));
    }

    private void OnUiOpened(Entity<PaymentTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnInteractUsing(Entity<PaymentTerminalComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_idCard.TryGetIdCard(args.Used, out var idCard))
            return;

        args.Handled = true;

        if (ent.Comp.OwnerUserId == null)
            TryBind(ent, args.User, idCard.Comp.FullName);
        else
            TryPay(ent, args.User);
    }

    private void TryBind(Entity<PaymentTerminalComponent> ent, EntityUid user, string? idName)
    {
        if (!HasComp<BankAccountComponent>(user)
            || !_player.TryGetSessionByEntity(user, out var session)
            || !_prefs.TryGetCachedPreferences(session.UserId, out var prefs)
            || prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
        {
            Deny(ent, user, "payment-terminal-no-account");
            return;
        }

        ent.Comp.OwnerUserId = session.UserId;
        ent.Comp.OwnerCharacterName = profile.Name;
        ent.Comp.OwnerIdName = string.IsNullOrWhiteSpace(idName) ? profile.Name : idName;

        _audio.PlayPvs(ent.Comp.SuccessSound, ent);
        _popup.PopupEntity(Loc.GetString("payment-terminal-bound", ("name", ent.Comp.OwnerIdName)), ent, user);
        _adminLog.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(user):actor} bound payment terminal {ToPrettyString(ent):entity} to their account");
        UpdateUi(ent);
    }

    private void TryPay(Entity<PaymentTerminalComponent> ent, EntityUid user)
    {
        var comp = ent.Comp;

        if (comp.Stage != PaymentTerminalStage.Ready || comp.Price <= 0)
        {
            Deny(ent, user, "payment-terminal-not-ready");
            return;
        }

        if (IsOwner(comp, user))
        {
            Deny(ent, user, "payment-terminal-self-pay");
            return;
        }

        if (!TryGetOwnerAccount(comp, out _, out _, out _))
        {
            Deny(ent, user, "payment-terminal-owner-unavailable");
            return;
        }

        var total = comp.Price + comp.Tip;
        if (!_bank.TryBankWithdraw(user, total))
        {
            Deny(ent, user, "payment-terminal-insufficient-funds", ("amount", total));
            return;
        }

        var tax = GetTax(comp, total);
        var payout = total - tax;

        // Look the owner up again: the withdrawal may have replaced their cached preferences if they share a player account.
        if (payout > 0 && !TryDepositToOwner(comp, payout))
        {
            _bank.TryBankDeposit(user, total);
            Deny(ent, user, "payment-terminal-transaction-failed");
            return;
        }

        if (tax > 0)
            _bank.TrySectorDeposit(comp.TaxAccount, tax, LedgerEntryType.VendorTax);

        _audio.PlayPvs(comp.SuccessSound, ent);
        _popup.PopupEntity(Loc.GetString("payment-terminal-paid", ("amount", total), ("name", comp.OwnerIdName ?? string.Empty)), ent, user);

        if (comp.OwnerUserId is { } ownerId
            && _player.TryGetSessionById(ownerId, out var ownerSession)
            && ownerSession.AttachedEntity is { } ownerMob)
        {
            _popup.PopupEntity(Loc.GetString("payment-terminal-received", ("amount", payout), ("tax", tax)), ownerMob, ownerMob);
        }

        _adminLog.Add(LogType.ATMUsage, LogImpact.Low,
            $"{ToPrettyString(user):actor} paid {total} (price {comp.Price}, tip {comp.Tip}, tax {tax}) to {comp.OwnerCharacterName} via payment terminal {ToPrettyString(ent):entity}");

        ResetForNextBuyer(comp);
        UpdateUi(ent);
    }

    private bool TryDepositToOwner(PaymentTerminalComponent comp, int amount)
    {
        if (!TryGetOwnerAccount(comp, out var session, out var prefs, out var profile))
            return false;

        // Prefer the entity overload when the owner is playing that character, so their balance display stays in sync.
        if (session.AttachedEntity is { } mob
            && HasComp<BankAccountComponent>(mob)
            && ReferenceEquals(prefs.SelectedCharacter, profile))
        {
            return _bank.TryBankDeposit(mob, amount);
        }

        return _bank.TryBankDeposit(session, prefs, profile, amount, out _);
    }

    /// <summary>
    /// Bank accounts live in the character profiles, which are only cached while the owner is connected.
    /// </summary>
    private bool TryGetOwnerAccount(PaymentTerminalComponent comp,
        [NotNullWhen(true)] out ICommonSession? session,
        [NotNullWhen(true)] out PlayerPreferences? prefs,
        [NotNullWhen(true)] out HumanoidCharacterProfile? profile)
    {
        session = null;
        prefs = null;
        profile = null;

        if (comp.OwnerUserId is not { } ownerId
            || !_player.TryGetSessionById(ownerId, out session)
            || !_prefs.TryGetCachedPreferences(ownerId, out prefs))
        {
            return false;
        }

        foreach (var character in prefs.Characters.Values)
        {
            if (character is HumanoidCharacterProfile humanoid && humanoid.Name == comp.OwnerCharacterName)
            {
                profile = humanoid;
                return true;
            }
        }

        return false;
    }

    private bool IsOwner(PaymentTerminalComponent comp, EntityUid user)
    {
        return comp.OwnerUserId is { } ownerId
               && _player.TryGetSessionByEntity(user, out var session)
               && session.UserId == ownerId
               && _prefs.TryGetCachedPreferences(ownerId, out var prefs)
               && prefs.SelectedCharacter is HumanoidCharacterProfile profile
               && profile.Name == comp.OwnerCharacterName;
    }

    /// <summary>
    /// Owner-only settings are open to anyone until the terminal is bound.
    /// </summary>
    private bool CanConfigure(PaymentTerminalComponent comp, EntityUid user)
    {
        return comp.OwnerUserId == null || IsOwner(comp, user);
    }

    private static int GetTax(PaymentTerminalComponent comp, int total)
    {
        return (int) Math.Floor(total * Math.Clamp(comp.TaxRate, 0f, 1f));
    }

    /// <summary>
    /// After a payment: a fixed price stays for the next buyer, a free one starts over.
    /// </summary>
    private static void ResetForNextBuyer(PaymentTerminalComponent comp)
    {
        comp.Tip = 0;

        if (comp.Mode == PaymentTerminalMode.Fixed && comp.PriceLocked)
        {
            comp.Stage = PaymentTerminalStage.Tip;
            return;
        }

        comp.Price = 0;
        comp.Stage = PaymentTerminalStage.Price;
    }

    private static void ResetAll(PaymentTerminalComponent comp)
    {
        comp.PriceLocked = false;
        comp.Price = 0;
        comp.Tip = 0;
        comp.Stage = PaymentTerminalStage.Price;
    }

    #region Keypad

    private void OnKeypad(Entity<PaymentTerminalComponent> ent, ref PaymentTerminalKeypadMessage args)
    {
        if (args.Digit is < 0 or > 9)
            return;

        var comp = ent.Comp;
        switch (comp.Stage)
        {
            case PaymentTerminalStage.Price:
                if (comp.Mode == PaymentTerminalMode.Fixed && !CanConfigure(comp, args.Actor))
                {
                    Deny(ent, args.Actor, "payment-terminal-owner-only");
                    return;
                }

                if (!TryAppendDigit(comp, comp.Price, args.Digit, out var price))
                    return;

                comp.Price = price;
                break;
            case PaymentTerminalStage.Tip:
                if (!TryAppendDigit(comp, comp.Tip, args.Digit, out var tip))
                    return;

                comp.Tip = tip;
                break;
            default:
                return;
        }

        PlayKeypad(ent);
        UpdateUi(ent);
    }

    private static bool TryAppendDigit(PaymentTerminalComponent comp, int current, int digit, out int result)
    {
        var next = (long) current * 10 + digit;
        result = (int) Math.Min(next, int.MaxValue);
        return next <= comp.MaxAmount;
    }

    /// <summary>
    /// Confirms the current step: price, then tip, then the terminal waits for an ID swipe.
    /// </summary>
    private void OnEnter(Entity<PaymentTerminalComponent> ent, ref PaymentTerminalEnterMessage args)
    {
        var comp = ent.Comp;
        switch (comp.Stage)
        {
            case PaymentTerminalStage.Price:
                if (comp.Mode == PaymentTerminalMode.Fixed && !CanConfigure(comp, args.Actor))
                {
                    Deny(ent, args.Actor, "payment-terminal-owner-only");
                    return;
                }

                if (comp.Price <= 0)
                {
                    Deny(ent, args.Actor, "payment-terminal-no-price");
                    return;
                }

                if (comp.Mode == PaymentTerminalMode.Fixed)
                {
                    comp.PriceLocked = true;
                    _popup.PopupEntity(Loc.GetString("payment-terminal-price-locked", ("amount", comp.Price)), ent, args.Actor);
                }

                comp.Stage = PaymentTerminalStage.Tip;
                break;
            case PaymentTerminalStage.Tip:
                comp.Stage = PaymentTerminalStage.Ready;
                break;
            default:
                return;
        }

        PlayKeypad(ent);
        UpdateUi(ent);
    }

    /// <summary>
    /// A free price starts over from the price; a fixed one only drops the tip, the price stays until the owner clears it.
    /// </summary>
    private void OnClear(Entity<PaymentTerminalComponent> ent, ref PaymentTerminalClearMessage args)
    {
        var comp = ent.Comp;

        if (comp.Mode == PaymentTerminalMode.Fixed && comp.PriceLocked)
        {
            comp.Tip = 0;
            comp.Stage = PaymentTerminalStage.Tip;
        }
        else
        {
            if (comp.Mode == PaymentTerminalMode.Fixed && !CanConfigure(comp, args.Actor))
            {
                Deny(ent, args.Actor, "payment-terminal-owner-only");
                return;
            }

            ResetAll(comp);
        }

        PlayKeypad(ent);
        UpdateUi(ent);
    }

    private void PlayKeypad(EntityUid uid)
    {
        _audio.PlayPvs(Comp<PaymentTerminalComponent>(uid).KeypadSound, uid, AudioParams.Default.WithVolume(-5f));
    }

    #endregion

    #region Verbs

    private void OnGetVerbs(Entity<PaymentTerminalComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanConfigure(ent.Comp, args.User))
            return;

        var user = args.User;
        var comp = ent.Comp;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(comp.Mode == PaymentTerminalMode.Fixed
                ? "payment-terminal-verb-mode-to-free"
                : "payment-terminal-verb-mode-to-fixed"),
            Act = () => ToggleMode(ent, user),
            Priority = 2,
        });

        if (comp.Mode == PaymentTerminalMode.Fixed)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("payment-terminal-verb-clear"),
                Act = () => ClearPrice(ent, user),
                Priority = 1,
            });
        }

        if (comp.OwnerUserId != null)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("payment-terminal-verb-unbind"),
                Act = () => Unbind(ent, user),
                Priority = 0,
            });
        }
    }

    private void ToggleMode(Entity<PaymentTerminalComponent> ent, EntityUid user)
    {
        var comp = ent.Comp;
        if (!CanConfigure(comp, user))
            return;

        comp.Mode = comp.Mode == PaymentTerminalMode.Fixed ? PaymentTerminalMode.Free : PaymentTerminalMode.Fixed;
        ResetAll(comp);

        _popup.PopupEntity(Loc.GetString(comp.Mode == PaymentTerminalMode.Fixed
            ? "payment-terminal-mode-fixed"
            : "payment-terminal-mode-free"), ent, user);
        UpdateUi(ent);
    }

    private void ClearPrice(Entity<PaymentTerminalComponent> ent, EntityUid user)
    {
        if (!CanConfigure(ent.Comp, user))
            return;

        ResetAll(ent.Comp);
        _popup.PopupEntity(Loc.GetString("payment-terminal-cleared"), ent, user);
        UpdateUi(ent);
    }

    private void Unbind(Entity<PaymentTerminalComponent> ent, EntityUid user)
    {
        var comp = ent.Comp;
        if (comp.OwnerUserId == null || !IsOwner(comp, user))
            return;

        _adminLog.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(user):actor} unbound payment terminal {ToPrettyString(ent):entity}");

        comp.OwnerUserId = null;
        comp.OwnerCharacterName = null;
        comp.OwnerIdName = null;
        ResetAll(comp);

        _popup.PopupEntity(Loc.GetString("payment-terminal-unbound"), ent, user);
        UpdateUi(ent);
    }

    #endregion

    private void Deny(EntityUid terminal, EntityUid user, string message, params (string, object)[] args)
    {
        _audio.PlayPvs(Comp<PaymentTerminalComponent>(terminal).ErrorSound, terminal);
        _popup.PopupEntity(Loc.GetString(message, args), terminal, user);
    }

    private void UpdateUi(Entity<PaymentTerminalComponent> ent)
    {
        var comp = ent.Comp;
        var state = new PaymentTerminalBoundUserInterfaceState(
            comp.OwnerIdName,
            comp.Mode,
            comp.Stage,
            comp.Price,
            comp.Tip,
            GetTax(comp, comp.Price + comp.Tip),
            comp.TaxRate);

        _ui.SetUiState(ent.Owner, PaymentTerminalUiKey.Key, state);
    }
}
