using Content.Server.PowerCell;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Medical.Automender;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Medical.Automender;

/// <summary>
/// Обрабатывает взаимодействие <see cref="AutomenderComponent"/>: запускает цепочку
/// самоповторяющихся DoAfter-тиков лечения, списывая на каждом тике заряд батареи
/// из слота инструмента.
/// </summary>
public sealed class AutomenderSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutomenderComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DamageableComponent, AutomenderDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<AutomenderComponent> automender, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (TryStartHealing(automender, args.Target.Value, args.User))
            args.Handled = true;
    }

    private bool TryStartHealing(Entity<AutomenderComponent> automender, EntityUid target, EntityUid user)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable))
            return false;

        if (automender.Comp.DamageContainers is not null &&
            damageable.DamageContainerID is not null &&
            !automender.Comp.DamageContainers.Contains(damageable.DamageContainerID.Value))
        {
            return false;
        }

        if (user != target && !_interaction.InRangeUnobstructed(user, target, popup: true))
            return false;

        if (!HasDamage(automender.Comp, damageable))
        {
            _popup.PopupClient(Loc.GetString("medical-item-cant-use", ("item", automender.Owner)), automender.Owner, user);
            return false;
        }

        if (!_powerCell.HasCharge(automender.Owner, automender.Comp.ChargeUsePerTick, user: user))
            return false;

        _audio.PlayPvs(automender.Comp.HealingBeginSound, automender.Owner);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, user, automender.Comp.Delay, new AutomenderDoAfterEvent(), target, target: target, used: automender.Owner)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
        };

        return _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnDoAfter(Entity<DamageableComponent> target, ref AutomenderDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp(args.Used, out AutomenderComponent? automender))
            return;

        if (automender.DamageContainers is not null &&
            target.Comp.DamageContainerID is not null &&
            !automender.DamageContainers.Contains(target.Comp.DamageContainerID.Value))
        {
            return;
        }

        if (!_powerCell.TryUseCharge(args.Used.Value, automender.ChargeUsePerTick, user: args.User))
        {
            args.Repeat = false;
            args.Handled = true;
            return;
        }

        _damageable.TryChangeDamage(target.Owner, automender.Damage, true, origin: args.Args.User);

        _audio.PlayPvs(automender.HealingTickSound, target.Owner);

        args.Repeat = HasDamage(automender, target.Comp);
        args.Handled = true;

        if (!args.Repeat)
            _popup.PopupClient(Loc.GetString("medical-item-finished-using", ("item", args.Used)), target.Owner, args.User);
    }

    private bool HasDamage(AutomenderComponent automender, DamageableComponent damageable)
    {
        var damageableDict = damageable.Damage.DamageDict;
        var healingDict = automender.Damage.DamageDict;
        foreach (var type in healingDict)
        {
            if (damageableDict[type.Key].Value > 0)
                return true;
        }

        return false;
    }
}
