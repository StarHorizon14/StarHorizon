using Content.Server._Horizon.Shipbreaking.Derelict;
using Content.Server._NF.Bank;
using Content.Server.Cargo.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Content.Shared._NF.Bank.BUI;
using Content.Shared.Mobs.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Shipbreaking;

public sealed class ScrapPalletSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly DerelictYardSystem _derelict = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly HashSet<EntityUid> _found = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ScrapPalletComponent>();
        while (query.MoveNext(out var uid, out var pallet))
        {
            if (_timing.CurTime < pallet.NextScan)
                continue;

            pallet.NextScan = _timing.CurTime + pallet.Interval;
            Scan((uid, pallet));
        }
    }

    private void Scan(Entity<ScrapPalletComponent> ent)
    {
        _found.Clear();
        _lookup.GetEntitiesIntersecting(ent.Owner, _found, LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Uncontained);

        var sold = false;
        foreach (var other in _found)
        {
            if (other == ent.Owner || TerminatingOrDeleted(other) || Transform(other).Anchored || HasComp<MobStateComponent>(other))
                continue;

            if (_whitelist.IsWhitelistPass(ent.Comp.Blacklist, other))
                continue;

            var payout = (int) (_pricing.GetPrice(other) * ent.Comp.PayoutMultiplier);
            if (payout <= 0)
                continue;

            if (!_bank.TrySectorDeposit(ent.Comp.Account, payout, LedgerEntryType.StationDepositAssetsSold))
                continue;

            _derelict.CreditScrap(other, payout);
            QueueDel(other);
            sold = true;
        }

        if (sold)
            _audio.PlayPvs(ent.Comp.Sound, ent);
    }
}
