using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Goobstation.SlotMachine.ClawGame;

/// <summary>
/// This handles the claw machine logic
/// </summary>
public sealed class ClawMachineSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly SharedPrizeSystem _prize = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClawMachineComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ClawMachineComponent, ClawGameDoAfterEvent>(OnClawGameDoAfter);
        SubscribeLocalEvent<ClawMachineComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<ClawMachineComponent, GotUnEmaggedEvent>(OnUnEmagged);
    }

    private void OnEmagged(Entity<ClawMachineComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(ent, EmagType.Interaction) || ent.Comp.EvilPrizes.Count == 0)
            return;

        args.Handled = true; // My name is nhoj nhoj and I am EVIL
    }

    private void OnUnEmagged(Entity<ClawMachineComponent> ent, ref GotUnEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(ent, EmagType.Interaction))
            args.Handled = true;
    }

    private void OnActivate(Entity<ClawMachineComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (ent.Comp.IsSpinning || !_power.IsPowered(ent.Owner))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.DoAfterTime, new ClawGameDoAfterEvent(), ent.Owner, ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        ent.Comp.IsSpinning = true;
        Dirty(ent);

        _audio.PlayPvs(ent.Comp.PlaySound, ent.Owner);
        _appearance.SetData(ent.Owner, ClawMachineVisuals.Spinning, true);
        _appearance.SetData(ent.Owner, ClawMachineVisuals.NormalSprite, false);
    }

    private void OnClawGameDoAfter(Entity<ClawMachineComponent> ent, ref ClawGameDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        ent.Comp.IsSpinning = false;
        Dirty(ent);

        _appearance.SetData(ent.Owner, ClawMachineVisuals.Spinning, false);
        _appearance.SetData(ent.Owner, ClawMachineVisuals.NormalSprite, true);

        if (args.Cancelled)
        {
            var selfMsgFail = Loc.GetString("clawmachine-fail-self");
            var othersMsgFail = Loc.GetString("clawmachine-fail-other", ("user", args.User));
            _popupSystem.PopupPredicted(selfMsgFail, othersMsgFail, args.User, args.User);
            return;
        }

        if (_net.IsClient) // Misprediction of the prize otherwise
            return;

        var prizes = _emag.CheckFlag(ent, EmagType.Interaction) && ent.Comp.EvilPrizes.Count > 0
            ? ent.Comp.EvilPrizes
            : ent.Comp.Prizes;

        _prize.HandlePrize(prizes, ent.Owner);
    }
}
