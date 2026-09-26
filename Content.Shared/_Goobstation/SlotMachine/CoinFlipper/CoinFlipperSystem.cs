using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Goobstation.SlotMachine.CoinFlipper;

/// <summary>
/// This handles the coinflipper machine logic
/// </summary>
public sealed class CoinFlipperSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedStackSystem _stackSystem = default!;
    [Dependency] private readonly SharedPrizeSystem _prize = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoinFlipperComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CoinFlipperComponent, CoinFlipperDoAfterEvent>(OnCoinFlipperDoAfter);
    }

    private void OnActivate(Entity<CoinFlipperComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (ent.Comp.IsSpinning || !_power.IsPowered(ent.Owner))
            return;

        args.Handled = true;

        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.MoneySlot, out var slot)
            || slot.Item is not { } item
            || !TryComp<StackComponent>(item, out var stack))
        {
            _popupSystem.PopupClient(Loc.GetString("slotmachine-no-money"), ent.Owner, args.User);
            return;
        }

        if (_net.IsClient)
            return;

        ent.Comp.PrizeAmount = _stackSystem.GetCount(item, stack);
        QueueDel(item);

        ent.Comp.IsSpinning = true;
        Dirty(ent);

        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.DoAfterTime, new CoinFlipperDoAfterEvent(), ent.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
        };

        _audio.PlayPvs(ent.Comp.SpinSound, ent.Owner);
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCoinFlipperDoAfter(Entity<CoinFlipperComponent> ent, ref CoinFlipperDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var amount = ent.Comp.PrizeAmount;
        ent.Comp.PrizeAmount = 0;
        ent.Comp.IsSpinning = false;
        Dirty(ent);

        if (args.Cancelled) // Almost no way for it to be canceled but just in case, give the money back
        {
            SpawnCash(ent, amount);
            return;
        }

        if (amount <= 0 || !_random.Prob(ent.Comp.WinChance))
        {
            _audio.PlayPvs(ent.Comp.LoseSound, ent.Owner); // If nothing then lose
            return;
        }

        var winAmount = amount * 2;
        SpawnCash(ent, winAmount);

        _audio.PlayPvs(ent.Comp.WinSound, ent.Owner);
        _prize.Speak(ent.Owner, Loc.GetString("coinflipper-win", ("amount", winAmount)));
    }

    private void SpawnCash(Entity<CoinFlipperComponent> ent, int amount)
    {
        if (amount <= 0)
            return;

        var cash = EntityManager.PredictedSpawnAtPosition(ent.Comp.CashPrototype, ent.Owner.ToCoordinates());
        if (TryComp<StackComponent>(cash, out var stack))
            _stackSystem.SetCount(cash, amount, stack);
    }
}
