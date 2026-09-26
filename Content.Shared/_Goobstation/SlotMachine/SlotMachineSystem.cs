using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Goobstation.SlotMachine;

public sealed class SlotMachineSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedStackSystem _stackSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly SharedPrizeSystem _prize = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlotMachineComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<SlotMachineComponent, SlotMachineDoAfterEvent>(OnSlotMachineDoAfter);
        SubscribeLocalEvent<SlotMachineComponent, SlotMachineEmagDoAfterEvent>(OnSlotMachineEmagDoAfter);
        SubscribeLocalEvent<SlotMachineComponent, GotEmaggedEvent>(OnEmagged);
    }

    /// <summary>
    /// Spins once and spawns a random entity when emagged
    /// </summary>
    private void OnEmagged(Entity<SlotMachineComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(ent, EmagType.Interaction) || ent.Comp.IsSpinning)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var entities = _proto.EnumeratePrototypes<EntityPrototype>()
            .Where(proto => !proto.Abstract && !proto.HideSpawnMenu)
            .ToList();

        if (entities.Count == 0)
            return;

        ent.Comp.EmagSpawnEntity = _random.Pick(entities).ID;
        ent.Comp.IsSpinning = true;
        Dirty(ent);

        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.DoAfterTime, new SlotMachineEmagDoAfterEvent(), ent.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
        };

        _audio.PlayPvs(ent.Comp.SpinSound, ent.Owner);
        _doAfter.TryStartDoAfter(doAfter);
        _appearance.SetData(ent.Owner, SlotMachineVisuals.Spinning, true);
    }

    private void OnSlotMachineEmagDoAfter(Entity<SlotMachineComponent> ent, ref SlotMachineEmagDoAfterEvent args)
    {
        _appearance.SetData(ent.Owner, SlotMachineVisuals.Spinning, false);

        if (!args.Cancelled && ent.Comp.EmagSpawnEntity is { } spawn)
            EntityManager.PredictedSpawnAtPosition(spawn, ent.Owner.ToCoordinates());

        ent.Comp.EmagSpawnEntity = null;
        ent.Comp.IsSpinning = false;
        Dirty(ent);
    }

    /// <summary>
    /// Handle the logic for starting the slot machine
    /// </summary>
    private void OnActivate(Entity<SlotMachineComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (ent.Comp.IsSpinning || !_power.IsPowered(ent.Owner))
            return;

        args.Handled = true;

        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.MoneySlot, out var slot)
            || slot.Item is not { } item
            || !TryComp<StackComponent>(item, out var stack)
            || _stackSystem.GetCount(item, stack) < ent.Comp.SpinCost)
        {
            _popupSystem.PopupClient(Loc.GetString("slotmachine-no-money"), ent.Owner, args.User);
            return;
        }

        // The DoAfter causes a weird jitter if its predicted, so the whole spin is handled by the server
        if (_net.IsClient)
            return;

        _stackSystem.SetCount(item, _stackSystem.GetCount(item, stack) - ent.Comp.SpinCost, stack);

        ent.Comp.IsSpinning = true;
        Dirty(ent);

        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.DoAfterTime, new SlotMachineDoAfterEvent(), ent.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
        };

        _audio.PlayPvs(ent.Comp.SpinSound, ent.Owner);
        _doAfter.TryStartDoAfter(doAfter);
        _appearance.SetData(ent.Owner, SlotMachineVisuals.Spinning, true);
    }

    private void OnSlotMachineDoAfter(Entity<SlotMachineComponent> ent, ref SlotMachineDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        ent.Comp.IsSpinning = false;
        Dirty(ent);

        _appearance.SetData(ent.Owner, SlotMachineVisuals.Spinning, false);

        if (args.Cancelled) // Almost no way for it to be canceled but just in case
            return;

        _prize.HandlePrize(ent.Comp.Prizes, ent.Owner);
    }
}
