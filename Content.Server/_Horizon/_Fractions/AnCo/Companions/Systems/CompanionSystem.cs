using Content.Server._Horizon._Fractions.AnCo.Companions.Components;
using Content.Server.CartridgeLoader;
using Content.Server.Database;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Power.Components;
using Content.Server.PowerCell;
using Content.Shared._Horizon._Fractions.AnCo.Companions;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.Charges.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.PDA;
using Content.Shared.Pointing;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Content.Server.Worldgen.Tools.PoissonDiskSampler;

namespace Content.Server._Horizon._Fractions.AnCo.Companions.Systems;

/// <summary>
/// Система обрабатывающая компаньонов.
/// <para>
/// Отслеживает нажатие КПК/ID-карты по сущности и если сущность
/// имеет компонент <see cref="CompanionComponent"/>, то ему
/// присваивается EntityUid ID-карты.
/// </para>
/// </summary>
[Experimental("HORIZON_SYSTEM_01")]
public sealed class CompanionSystem : SharedCompanionSystem
{
    // Зависимости
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedChargesSystem _sharedCharges = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    // Временный флаг, помечающий, что система не активна.
    private bool _isActive = true;

    private readonly ISawmill _sawmill = Logger.GetSawmill("companions");

    public override void Initialize()
    {
        base.Initialize();

        if (_isActive)
        {
            SubscribeLocalEvent<CompanionComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<HandsComponent, AfterPointedAtEvent>(OnAfterPointedAt);
            SubscribeLocalEvent<CompanionCartridgeComponent, CartridgeUiReadyEvent>(OnUiOpen);
            _sawmill.Info("Система компаньонов (CompanionSystem) активна!");
        }
        else
        {
            _sawmill.Info("Система компаньонов (CompanionSystem) неактивна...");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CompanionComponent, BorgChassisComponent>();
        while (query.MoveNext(out var companionUid, out var companion, out var borg))
        {
            UpdateBorgLogic(companionUid, companion);
        }
    }

    #region Настройка ИИ компаньонов при привязках
    private void UpdateBorgLogic(EntityUid borg, CompanionComponent comp)
    {
        if (HasComp<HTNComponent>(borg))
            return;

        if (comp.IdCard.IsValid())
        {
            if (comp.DefaultRootTask == null)
                return;

            if (!_prototype.HasIndex<HTNCompoundPrototype>(comp.DefaultRootTask))
            {
                _sawmill.Error($"Не найден прототип {comp.DefaultRootTask} для установики ИИ сущности {Identity.Entity(borg, EntityManager)} по умолчанию, возможно его не существует.");
                comp.DefaultRootTask = null;
                return;
            }

            var htn = EnsureComp<HTNComponent>(borg);
            htn.RootTask = new HTNCompoundTask { Task = comp.DefaultRootTask };
            _htn.Replan(htn);
        }
    }
    #endregion

    #region Основные обработчики событий
    private void OnInteractUsing(EntityUid uid, CompanionComponent comp, InteractUsingEvent args)
    {
        // Отмена привязки через импульсы
        if (TryComp<EmagComponent>(args.Used, out var emag) && TryComp<WiresPanelComponent>(uid, out var panelState))
        {
            if (!comp.IdCard.IsValid() || !panelState.Open)
                return;

            if (_sharedCharges.IsEmpty(args.Used))
                return;

            _popup.PopupPredicted($"Карточка замыкает что-то в {Identity.Entity(uid, EntityManager)}.", uid, uid, PopupType.Medium);
            _audio.PlayPredicted(emag.EmagSound, args.Used, args.Used);

            _sharedCharges.TryUseCharge(args.Used);

            comp.IdCard = default;
            args.Handled = true;
            return;
        }

        // Привязка сущности компаньона к ID-карте или через КПК.
        if (TryComp<PdaComponent>(args.Used, out var pdaComp))
        {
            if (comp.IdCard == pdaComp.ContainedId)
            {
                comp.IdCard = default;
                if (TryComp<HTNComponent>(args.User, out var htnComp))
                    _htn.Replan(htnComp);
                _popup.PopupEntity("Киборг удалил данные КПК из памяти.", uid, args.User, PopupType.Medium);
                return;
            }

            if (comp.IdCard.Id != 0 && comp.IdCard != pdaComp.ContainedId)
            {
                _popup.PopupEntity("Киборг отказался сканировать КПК.", uid, args.User);
                return;
            }

            if (comp.IdCard.Id == 0 && pdaComp.ContainedId != null)
            {
                _popup.PopupEntity("Киборг сканирует КПК и записывает данные в память.", uid, args.User);
                comp.IdCard = (EntityUid)pdaComp.ContainedId;
                return;
            }

            if (comp.IdCard.Id == 0 && pdaComp.ContainedId == null)
            {
                _popup.PopupEntity("Киборг не нашёл ID-карту в КПК.", uid, args.User);
                return;
            }
        }

        // Проверка на компонент ID карты
        if (TryComp<IdCardComponent>(args.Used, out var idCardComp))
        {
            if (comp.IdCard == args.Used)
            {
                comp.IdCard = default;
                if (TryComp<HTNComponent>(args.User, out var htnComp))
                    _htn.Replan(htnComp);
                _popup.PopupEntity("Данные ID-карты удалены из памяти.", uid, args.User, PopupType.Medium);
                return;
            }

            if (comp.IdCard.Id != 0 && comp.IdCard != args.Used)
            {
                _popup.PopupEntity("Невозможно перезаписать данные ID-карты.", uid, args.User);
                return;
            }

            if (comp.IdCard.Id == 0)
            {
                _popup.PopupEntity("ID-карта записана в память.", uid, args.User);
                comp.IdCard = args.Used;
                return;
            }
        }

        args.Handled = true;
    }

    /// <summary>
    /// Слушатель отвечающий за получение данных на указываемый предмет.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="comp"></param>
    /// <param name="args"></param>
    private void OnAfterPointedAt(EntityUid uid, HandsComponent comp, AfterPointedAtEvent args)
    {
        var player = uid;
        var target = args.Pointed;

        var query = EntityQueryEnumerator<CompanionComponent, HTNComponent>();
        while (query.MoveNext(out var companionUid, out var compComp, out var htn))
        {
            if (!compComp.IdCard.IsValid() || !IsIdOwned(uid, compComp.IdCard, compComp))
                continue;

            ExecuteCompanionCommand(player, companionUid, compComp, htn, args.Pointed);
        }
    }

    /// <summary>
    /// Проверка на владельца Id карты.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="idCard"></param>
    /// <returns></returns>
    private bool IsIdOwned(EntityUid player, EntityUid idCard, CompanionComponent comp)
    {
        List<string> slotsToCheck = new();

        if (comp.MainSlots != null) slotsToCheck.AddRange(comp.MainSlots);

        if (comp.IsCheckAllSlots && comp.OtherSlots != null)
            slotsToCheck.AddRange(comp.OtherSlots);

        foreach (var slot in slotsToCheck)
        {
            if (string.IsNullOrWhiteSpace(slot)) continue;

            if (_inventory.TryGetSlotEntity(player, slot, out var item))
                if (IsIdCardOwned(item.Value, idCard))
                    return true;
        }

        foreach (EntityUid item in _hands.EnumerateHeld(player))
        {
            if (IsIdCardOwned(item, idCard))
                return true;
        }

        return false;
    }

    private bool IsIdCardOwned(EntityUid item, EntityUid idCard)
    {
        if (item == idCard)
            return true;

        if (TryComp<PdaComponent>(item, out var pda) && pda.ContainedId == idCard)
            return true;

        return false;
    }

    /// <summary>
    /// Метод для обработки поведения компаньонов в зависимости от отданной команды.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="companion"></param>
    /// <param name="htnCompanion"></param>
    /// <param name="target"></param>
    [Obsolete("Для более продвинутой системы комманд будет создана отдельно система отдачи команд.")]
    private void ExecuteCompanionCommand(EntityUid player, EntityUid companion, CompanionComponent comp, HTNComponent htnCompanion, EntityUid target)
    {
        if (comp.FollowTask == null)
            return;

        if (comp.FollowTargetKey == null)
            return;

        if (comp.FollowRangeKey == null)
            return;

        if (!_prototype.HasIndex<HTNCompoundPrototype>(comp.FollowTask))
        {
            _sawmill.Error($"Не найден прототип {comp.FollowTask} для установики ИИ сущности {Identity.Entity(companion, EntityManager)} по умолчанию, возможно его не существует.");
            comp.FollowTask = null;
            return;
        }

        if (target == player)
        {
            _popup.PopupEntity("Киборг начал следовать за вами.", companion);
        }
        else
        {
            _popup.PopupEntity("Киборг зафиксировал цель.", companion);
        }

        htnCompanion.Blackboard.SetValue(comp.FollowTargetKey, Transform(target).Coordinates);
        htnCompanion.Blackboard.SetValue(comp.FollowRangeKey, 1.8f);
        htnCompanion.Blackboard.SetValue("FollowCloseRange", 0.85f);

        htnCompanion.RootTask = new HTNCompoundTask { Task = comp.FollowTask.Value };

        _htn.Replan(htnCompanion);
    }
    #endregion

    #region Companion UI функции
    /*
     * Логика обработки интерфейсов для КПК связанные с компаньонами.
     */

    private void OnUiOpen(EntityUid uid, CompanionCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        if (!TryComp<PdaComponent>(args.Loader, out var pda))
            return;

        _sawmill.Debug($"Открыт интерфейс в КПК: {ToPrettyString(args.Loader)}.");
        _sawmill.Debug($"Картридж открыт: {ToPrettyString(uid)}.");

        UpdatePDAInterface(args.Loader, pda);
    }

    private void UpdatePDAInterface(EntityUid loaderUid, PdaComponent pda)
    {
        _sawmill.Debug($"Открыт интерфейс КПК, с ID-картой: {ToPrettyString(pda.ContainedId)}.");
        List<CompanionEntry> companions = new();
        var query = EntityQueryEnumerator<CompanionComponent, MetaDataComponent>();

        while (query.MoveNext(out var companion, out var component, out var meta))
        {
            if (component.IdCard != pda.ContainedId)
                continue;

            CompanionEntry entry = new();
            entry.Entity = GetNetEntity(companion);
            entry.Name = meta.EntityName;

            if (TryComp<BorgChassisComponent>(companion, out var borg))
            {
                entry.modulesCount = borg.ModuleCount;
                entry.maxModules = borg.MaxModules;
            }

            if (TryComp<MindContainerComponent>(companion, out var mind))
            {
                entry.mind = mind.Mind != null ? GetNetEntity(mind.Mind) : null;
            }

            if (_powerCell.TryGetBatteryFromSlot(companion, out var batteryUid, out var battery))
            {
                entry.currentCharge = battery.CurrentCharge;
                entry.maxCharge = battery.MaxCharge;
            }

            companions.Add(entry);
        }
        _sawmill.Debug($"Количество боргов у игрока: {companions.Count}.");

        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, new CompanionPDABoundUserInterfaceState(companions));
    }

    // Метод для получения ID-карты игрока в слотах id и рук.
    // Даже если id карта находится в КПК.
    private EntityUid? GetPlayerIdCard(EntityUid player)
    {
        string[] slots = new[] { "id", "hand_right", "hand_right" };

        foreach (string slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(player, slot, out var item))
                continue;

            if (HasComp<IdCardComponent>(item))
                return item;

            if (TryComp<PdaComponent>(item, out var pda) && pda.ContainedId != null)
                return pda.ContainedId;
        }

        return null;
    }

    #endregion
}
