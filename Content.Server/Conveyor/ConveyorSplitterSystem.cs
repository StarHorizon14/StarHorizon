using System.Linq;
using Content.Shared.Conveyor;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server.Conveyor;

/// <summary>
/// Система разветвителя конвейеров.
/// Автоматически переключает ConveyorState между Forward и Reverse по таймеру.
/// </summary>
public sealed class ConveyorSplitterSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public static readonly VerbCategory IntervalCategory = new("verb-categories-timer", "/Textures/Interface/VerbIcons/clock.svg.192dpi.png");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConveyorSplitterComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ConveyorSplitterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<ConveyorSplitterComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInit(EntityUid uid, ConveyorSplitterComponent component, ComponentInit args)
    {
        SetConveyorState(uid, component.IsForward);
    }

    private void OnExamined(EntityUid uid, ConveyorSplitterComponent component, ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
        {
            args.PushMarkup(Loc.GetString("conveyor-splitter-examine-interval", ("time", component.SwitchInterval)));
        }
    }

    private void OnGetAltVerbs(EntityUid uid, ConveyorSplitterComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (component.IntervalOptions == null || component.IntervalOptions.Count <= 1)
            return;

        // Добавляем верб для циклического переключения
        args.Verbs.Add(new AlternativeVerb
        {
            Category = IntervalCategory,
            Text = Loc.GetString("verb-trigger-timer-cycle"),
            Act = () => CycleInterval(uid, component, args.User),
            Priority = 1
        });

        // Добавляем вербы для каждой опции
        foreach (var option in component.IntervalOptions)
        {
            var isCurrent = MathHelper.CloseTo(option, component.SwitchInterval);

            args.Verbs.Add(new AlternativeVerb
            {
                Category = IntervalCategory,
                Text = isCurrent
                    ? Loc.GetString("verb-trigger-timer-set-current", ("time", option))
                    : Loc.GetString("verb-trigger-timer-set", ("time", option)),
                Disabled = isCurrent,
                Priority = (int)(-100 * option),
                Act = () => SetInterval(uid, component, option, args.User)
            });
        }
    }

    private void CycleInterval(EntityUid uid, ConveyorSplitterComponent component, EntityUid user)
    {
        if (component.IntervalOptions == null || component.IntervalOptions.Count <= 1)
            return;

        var sorted = component.IntervalOptions.OrderBy(x => x).ToList();
        var currentIndex = sorted.FindIndex(x => MathHelper.CloseTo(x, component.SwitchInterval));

        var nextIndex = (currentIndex + 1) % sorted.Count;
        SetInterval(uid, component, sorted[nextIndex], user);
    }

    private void SetInterval(EntityUid uid, ConveyorSplitterComponent component, float interval, EntityUid user)
    {
        component.SwitchInterval = interval;
        component.Timer = 0f;
        Dirty(uid, component);

        _popup.PopupEntity(Loc.GetString("popup-trigger-timer-set", ("time", interval)), uid, user);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ConveyorSplitterComponent, ConveyorComponent>();

        while (query.MoveNext(out var uid, out var splitter, out var conveyor))
        {
            if (!conveyor.Powered)
                continue;

            splitter.Timer += frameTime;

            if (splitter.Timer >= splitter.SwitchInterval)
            {
                splitter.Timer = 0f;
                splitter.IsForward = !splitter.IsForward;
                SetConveyorState(uid, splitter.IsForward);
                Dirty(uid, splitter);
            }
        }
    }

    private void SetConveyorState(EntityUid uid, bool forward)
    {
        if (!TryComp<ConveyorComponent>(uid, out var conveyor))
            return;

        conveyor.State = forward ? ConveyorState.Forward : ConveyorState.Reverse;
        Dirty(uid, conveyor);
        _appearance.SetData(uid, ConveyorVisuals.State, conveyor.State);
    }
}
