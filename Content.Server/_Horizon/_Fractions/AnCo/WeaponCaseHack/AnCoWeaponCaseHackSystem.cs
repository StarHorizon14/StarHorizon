using Content.Server.Explosion.EntitySystems;
using Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Horizon._Fractions.AnCo.WeaponCaseHack;

/// <summary>
/// Взлом кейсов АнКо: дорожки A/B/C. Граф генерируется при первом взломе и не меняется до успешного вскрытия.
/// Неверный выбор (тупик) отнимает попытку и возвращает игрока в начало. Без попыток кейс взрывается.
/// </summary>
public sealed class AnCoWeaponCaseHackSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnCoWeaponCaseHackComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AnCoWeaponCaseHackComponent, AnCoHackChooseMessage>(OnChoose);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AnCoWeaponCaseHackComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Deadline is not { } deadline)
                continue;

            // Кейс уже вскрыт другим способом - таймер не нужен.
            if (!TryComp<LockComponent>(uid, out var lockComp) || !lockComp.Locked)
            {
                comp.Deadline = null;
                continue;
            }

            if (_timing.CurTime >= deadline)
                Fail(uid, comp);
        }
    }

    private void OnGetVerbs(EntityUid uid, AnCoWeaponCaseHackComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<LockComponent>(uid, out var lockComp) || !lockComp.Locked)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("anco-weapon-case-hack-verb"),
            Act = () => StartHack(uid, comp, user)
        });
    }

    private void StartHack(EntityUid uid, AnCoWeaponCaseHackComponent comp, EntityUid user)
    {
        if (comp.Nodes.Count == 0)
            GenerateGraph(comp);

        _ui.OpenUi(uid, AnCoHackUiKey.Key, user);
        UpdateUi(uid, comp);
    }

    private void GenerateGraph(AnCoWeaponCaseHackComponent comp)
    {
        comp.Nodes = AnCoHackGraph.Generate(_random, comp.NodeCount);
        comp.Attempts = comp.MaxAttempts;
        comp.NodeIndex = 0;
    }

    private void OnChoose(EntityUid uid, AnCoWeaponCaseHackComponent comp, AnCoHackChooseMessage args)
    {
        var user = args.Actor;

        if (!TryComp<LockComponent>(uid, out var lockComp) || !lockComp.Locked || comp.Nodes.Count == 0)
        {
            _ui.CloseUi(uid, AnCoHackUiKey.Key);
            return;
        }

        // Сообщение устарело (например, после сброса в начало) - просто обновим окно.
        if (args.NodeIndex != comp.NodeIndex || comp.NodeIndex >= comp.Nodes.Count)
        {
            UpdateUi(uid, comp);
            return;
        }

        var node = comp.Nodes[comp.NodeIndex];

        // Выбор направления: влево/вверх/вправо. Направление без выхода игнорируем (кнопка там неактивна).
        bool chooseB;
        if (args.Direction == node.LeftExit)
            chooseB = true;
        else if (args.Direction == node.RightExit)
            chooseB = false;
        else
        {
            UpdateUi(uid, comp);
            return;
        }

        // Таймер стартует с первого ответа и идёт до вскрытия, даже если окно закрыть.
        comp.Deadline ??= _timing.CurTime + TimeSpan.FromSeconds(comp.TimeLimit);

        if (chooseB == node.CorrectIsB)
        {
            comp.NodeIndex++;
            if (comp.NodeIndex >= comp.Nodes.Count)
            {
                Succeed(uid, comp, user);
                return;
            }

            UpdateUi(uid, comp);
            return;
        }

        // Тупик: -1 попытка, полный сброс карты и возврат в самое начало (граф остаётся тем же).
        comp.Attempts--;
        comp.NodeIndex = 0;

        if (comp.Attempts <= 0)
        {
            Fail(uid, comp);
            return;
        }

        _popup.PopupEntity(Loc.GetString("anco-weapon-case-hack-fail-attempt", ("attempts", comp.Attempts)), uid, user);
        UpdateUi(uid, comp);
    }

    private void Succeed(EntityUid uid, AnCoWeaponCaseHackComponent comp, EntityUid user)
    {
        _lock.Unlock(uid, user);
        _popup.PopupEntity(Loc.GetString("anco-weapon-case-hack-success"), uid, user);

        // Следующий взлом (если кейс закроют снова) получит новый граф.
        comp.Nodes.Clear();
        comp.NodeIndex = 0;
        comp.Deadline = null;

        _ui.CloseUi(uid, AnCoHackUiKey.Key);
    }

    private void Fail(EntityUid uid, AnCoWeaponCaseHackComponent comp)
    {
        comp.Deadline = null;

        _explosion.QueueExplosion(
            uid,
            comp.ExplosionPrototype,
            comp.ExplosionIntensity,
            comp.ExplosionSlope,
            comp.ExplosionMaxTileIntensity,
            canCreateVacuum: false);
        QueueDel(uid);
    }

    private void UpdateUi(EntityUid uid, AnCoWeaponCaseHackComponent comp)
    {
        if (comp.Nodes.Count == 0 || comp.NodeIndex >= comp.Nodes.Count)
            return;

        // Клиенту уходит только геометрия пройденных узлов и выходы текущей развилки;
        // какой выход верный и что дальше - не раскрывается.
        var passed = new List<AnCoHackPassedNode>(comp.NodeIndex);
        for (var i = 0; i < comp.NodeIndex; i++)
            passed.Add(comp.Nodes[i].ToPassed());

        var node = comp.Nodes[comp.NodeIndex];
        var state = new AnCoHackBoundUserInterfaceState(
            comp.Attempts,
            comp.MaxAttempts,
            node.RuleId,
            node.SerialB,
            node.SerialC,
            passed,
            node.LeftExit,
            node.RightExit,
            comp.Deadline);

        _ui.SetUiState(uid, AnCoHackUiKey.Key, state);
    }
}
