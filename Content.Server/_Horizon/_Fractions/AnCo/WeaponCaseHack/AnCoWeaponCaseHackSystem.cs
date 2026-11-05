using Content.Server.Explosion.EntitySystems;
using Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnCoWeaponCaseHackComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AnCoWeaponCaseHackComponent, AnCoHackChooseMessage>(OnChoose);
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
        // Условия не повторяются в пределах одного взлома.
        var rules = new List<AnCoSerialRule>(AnCoSerialRules.All);
        _random.Shuffle(rules);

        comp.Nodes.Clear();
        comp.DeadEnds.Clear();
        comp.Attempts = comp.MaxAttempts;
        comp.NodeIndex = 0;

        var count = Math.Min(comp.NodeCount, rules.Count);
        for (var i = 0; i < count; i++)
        {
            var rule = rules[i];
            var correctIsB = _random.Prob(0.5f);
            var matching = AnCoSerialRules.GenerateSerial(_random, rule, true);
            var other = AnCoSerialRules.GenerateSerial(_random, rule, false);

            comp.Nodes.Add(new AnCoHackNode
            {
                RuleId = rule.LocId,
                SerialB = correctIsB ? matching : other,
                SerialC = correctIsB ? other : matching,
                CorrectIsB = correctIsB,
            });
        }
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
        if (args.ChooseB == node.CorrectIsB)
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

        // Тупик: -1 попытка, возвращаемся в самое начало (граф остаётся тем же).
        var deadEnd = new AnCoHackDeadEnd(comp.NodeIndex, args.ChooseB);
        if (!comp.DeadEnds.Contains(deadEnd))
            comp.DeadEnds.Add(deadEnd);

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
        comp.DeadEnds.Clear();
        comp.NodeIndex = 0;

        _ui.CloseUi(uid, AnCoHackUiKey.Key);
    }

    private void Fail(EntityUid uid, AnCoWeaponCaseHackComponent comp)
    {
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

        var node = comp.Nodes[comp.NodeIndex];
        // Правильная сторона клиенту не передаётся, только уже найденные тупики.
        var state = new AnCoHackBoundUserInterfaceState(
            comp.Attempts,
            comp.MaxAttempts,
            comp.NodeIndex,
            comp.Nodes.Count,
            node.RuleId,
            node.SerialB,
            node.SerialC,
            new List<AnCoHackDeadEnd>(comp.DeadEnds));

        _ui.SetUiState(uid, AnCoHackUiKey.Key, state);
    }
}
