using System.Linq;
using Content.Server.DoAfter;
using Content.Server.Humanoid;
using Content.Server.Popups;
using Content.Shared._Horizon.SlimeAppearance;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.SlimeAppearance;

public sealed class SlimeAppearanceChangeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlimeAppearanceChangeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlimeAppearanceChangeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SlimeAppearanceChangeComponent, SlimeChangeAppearanceActionEvent>(OnAction);
        SubscribeLocalEvent<SlimeAppearanceChangeComponent, SlimeAppearanceDoAfterEvent>(OnDoAfter);

        Subs.BuiEvents<SlimeAppearanceChangeComponent>(SlimeAppearanceUiKey.Key, subs =>
        {
            subs.Event<SlimeAppearanceApplyMessage>(OnApply);
        });
    }

    private void OnMapInit(Entity<SlimeAppearanceChangeComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnShutdown(Entity<SlimeAppearanceChangeComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
    }

    private void OnAction(Entity<SlimeAppearanceChangeComponent> ent, ref SlimeChangeAppearanceActionEvent args)
    {
        if (args.Handled || !TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        args.Handled = true;

        if (ent.Comp.DoAfter != null)
        {
            _popup.PopupEntity(Loc.GetString("slime-appearance-already-changing"), ent, ent);
            return;
        }

        _ui.OpenUi(ent.Owner, SlimeAppearanceUiKey.Key, ent.Owner);
        _ui.SetUiState(ent.Owner, SlimeAppearanceUiKey.Key, new SlimeAppearanceBoundUserInterfaceState(
            new MarkingSet(humanoid.MarkingSet),
            humanoid.Species,
            humanoid.Sex,
            humanoid.SkinColor,
            humanoid.EyeColor));
    }

    private void OnApply(Entity<SlimeAppearanceChangeComponent> ent, ref SlimeAppearanceApplyMessage args)
    {
        // Only the slime can reshape itself
        if (args.Actor != ent.Owner || ent.Comp.DoAfter != null)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid)
            || !_proto.TryIndex<SpeciesPrototype>(humanoid.Species, out var species))
            return;

        if (ent.Comp.ActionEntity is { } action
            && TryComp<ActionComponent>(action, out var actionComp)
            && actionComp.Cooldown is { } cooldown
            && cooldown.End > _timing.CurTime)
        {
            var remaining = cooldown.End - _timing.CurTime;
            _popup.PopupEntity(Loc.GetString("slime-appearance-cooldown", ("minutes", (int) Math.Ceiling(remaining.TotalMinutes))), ent, ent);
            return;
        }

        // Validate the same way character profiles are validated, so nothing the editor wouldn't allow gets through
        var skinColor = args.SkinColor;
        if (!SkinColor.VerifySkinColor(species.SkinColoration, skinColor))
            skinColor = SkinColor.ValidSkinTone(species.SkinColoration, skinColor);

        // Pronouns are the player's own choice, so only the sex changes
        var sex = species.Sexes.Contains(args.Sex) ? args.Sex : humanoid.Sex;

        var markings = new MarkingSet(args.Markings.GetForwardEnumerator().ToList(), species.MarkingPoints, _markingManager, _proto);
        markings.EnsureValid(_markingManager);
        markings.EnsureSpecies(humanoid.Species, skinColor, _markingManager);
        markings.EnsureSexes(sex, _markingManager);

        ent.Comp.PendingMarkings = markings;
        ent.Comp.PendingSex = sex;
        ent.Comp.PendingSkinColor = skinColor;
        ent.Comp.PendingEyeColor = new Color(args.EyeColor.R, args.EyeColor.G, args.EyeColor.B);

        var doAfterArgs = new DoAfterArgs(EntityManager, ent, ent.Comp.DoAfterTime, new SlimeAppearanceDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var id))
        {
            ent.Comp.PendingMarkings = null;
            return;
        }

        ent.Comp.DoAfter = id;
        _ui.CloseUi(ent.Owner, SlimeAppearanceUiKey.Key);
        _popup.PopupEntity(Loc.GetString("slime-appearance-start"), ent, ent);
    }

    private void OnDoAfter(Entity<SlimeAppearanceChangeComponent> ent, ref SlimeAppearanceDoAfterEvent args)
    {
        ent.Comp.DoAfter = null;

        if (args.Cancelled || ent.Comp.PendingMarkings is not { } markings
            || !TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
        {
            ent.Comp.PendingMarkings = null;
            if (args.Cancelled)
                _popup.PopupEntity(Loc.GetString("slime-appearance-interrupted"), ent, ent);
            return;
        }

        // The sex goes first: changing it drops the markings that don't fit, and the new ones are already checked against it
        _humanoid.SetSex(ent, ent.Comp.PendingSex, sync: false, humanoid: humanoid);
        humanoid.MarkingSet = markings;
        humanoid.EyeColor = ent.Comp.PendingEyeColor;
        _humanoid.SetSkinColor(ent, ent.Comp.PendingSkinColor, sync: false, humanoid: humanoid);
        Dirty(ent, humanoid);

        ent.Comp.PendingMarkings = null;

        if (ent.Comp.ActionEntity is { } action)
            _actions.SetCooldown(action, ent.Comp.Cooldown);

        _popup.PopupEntity(Loc.GetString("slime-appearance-done"), ent, ent);
    }
}
