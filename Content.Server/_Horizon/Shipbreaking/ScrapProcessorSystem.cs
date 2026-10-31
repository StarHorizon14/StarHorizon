using System.Linq;
using Content.Server._Horizon.Shipbreaking.Derelict;
using Content.Server.Administration.Logs;
using Content.Server._NF.Bank;
using Content.Server.Cargo.Systems;
using Content.Server.Power.EntitySystems;
using Content.Shared._Horizon.Shipbreaking;
using Content.Shared._NF.Bank.BUI;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Shipbreaking;

public sealed class ScrapProcessorSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly DerelictYardSystem _derelict = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScrapProcessorComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<ScrapProcessorComponent, EndCollideEvent>(OnEndCollide);
    }

    private void OnStartCollide(Entity<ScrapProcessorComponent> ent, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!this.IsPowered(ent, EntityManager))
            return;

        if (HasComp<MobStateComponent>(other))
        {
            if (ent.Comp.GibLiving && HasComp<BodyComponent>(other))
            {
                var impact = HasComp<HumanoidAppearanceComponent>(other) ? LogImpact.Extreme : LogImpact.Medium;
                _adminLogger.Add(LogType.Gib, impact, $"{ToPrettyString(other):victim} was gibbed by {ToPrettyString(ent):entity}");
                _body.GibBody(other, true);
                _audio.PlayPvs(ent.Comp.Sound, ent);
                QueueDel(other);
            }
            else if (ent.Comp.BurnDamage != null)
            {
                ent.Comp.Burning.Add(other);
            }

            return;
        }

        if (Transform(other).Anchored || HasComp<MapGridComponent>(other))
            return;

        if (_whitelist.IsWhitelistPass(ent.Comp.Blacklist, other))
            return;

        if (!_whitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, other))
        {
            if (!ent.Comp.DestroyRejected)
                return;

            QueueDel(other);
            return;
        }

        var payout = (int) (_pricing.GetPrice(other) * ent.Comp.PayoutMultiplier);
        if (payout > 0 && _bank.TrySectorDeposit(ent.Comp.Account, payout, LedgerEntryType.StationDepositAssetsSold))
            _derelict.CreditScrap(other, payout);

        _audio.PlayPvs(ent.Comp.Sound, ent);
        QueueDel(other);
    }

    private void OnEndCollide(Entity<ScrapProcessorComponent> ent, ref EndCollideEvent args)
    {
        ent.Comp.Burning.Remove(args.OtherEntity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ScrapProcessorComponent>();
        while (query.MoveNext(out var uid, out var processor))
        {
            if (processor.BurnDamage == null || processor.Burning.Count == 0 || processor.NextBurn > _timing.CurTime)
                continue;

            processor.NextBurn = _timing.CurTime + processor.BurnInterval;

            if (!this.IsPowered(uid, EntityManager))
                continue;

            foreach (var victim in processor.Burning.ToArray())
            {
                // The end of a contact does not always come, so whoever walked off is let go here.
                if (TerminatingOrDeleted(victim) || !Transform(uid).Coordinates.TryDistance(EntityManager, Transform(victim).Coordinates, out var distance) || distance > 2f)
                {
                    processor.Burning.Remove(victim);
                    continue;
                }

                _damageable.TryChangeDamage(victim, processor.BurnDamage);
                _audio.PlayPvs(processor.BurnSound, victim);
            }
        }
    }
}
