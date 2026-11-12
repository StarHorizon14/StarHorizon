using System.Linq;
using Content.Shared._Horizon.Medical.Damage;
using Content.Shared.CCVar;
using Content.Shared.Chemistry;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radiation.Events;
using Content.Shared.Rejuvenate;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Damage
{
    public sealed class DamageableSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
        [Dependency] private readonly IConfigurationManager _config = default!;
        [Dependency] private readonly SharedChemistryGuideDataSystem _chemistryGuideData = default!;

        private EntityQuery<AppearanceComponent> _appearanceQuery;
        private EntityQuery<DamageableComponent> _damageableQuery;
        private EntityQuery<MindContainerComponent> _mindContainerQuery;

        public float UniversalAllDamageModifier { get; private set; } = 1f;
        public float UniversalAllHealModifier { get; private set; } = 1f;
        public float UniversalMeleeDamageModifier { get; private set; } = 1f;
        public float UniversalProjectileDamageModifier { get; private set; } = 1f;
        public float UniversalHitscanDamageModifier { get; private set; } = 1f;
        public float UniversalReagentDamageModifier { get; private set; } = 1f;
        public float UniversalReagentHealModifier { get; private set; } = 1f;
        public float UniversalExplosionDamageModifier { get; private set; } = 1f;
        public float UniversalThrownDamageModifier { get; private set; } = 1f;
        public float UniversalTopicalsHealModifier { get; private set; } = 1f;
        public float UniversalMobDamageModifier { get; private set; } = 1f;

        public override void Initialize()
        {
            SubscribeLocalEvent<DamageableComponent, ComponentInit>(DamageableInit);
            SubscribeLocalEvent<DamageableComponent, ComponentHandleState>(DamageableHandleState);
            SubscribeLocalEvent<DamageableComponent, ComponentGetState>(DamageableGetState);
            SubscribeLocalEvent<DamageableComponent, OnIrradiatedEvent>(OnIrradiated);
            SubscribeLocalEvent<DamageableComponent, RejuvenateEvent>(OnRejuvenate);

            _appearanceQuery = GetEntityQuery<AppearanceComponent>();
            _damageableQuery = GetEntityQuery<DamageableComponent>();
            _mindContainerQuery = GetEntityQuery<MindContainerComponent>();

            Subs.CVar(_config, CCVars.PlaytestAllDamageModifier, value =>
            {
                UniversalAllDamageModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            }, true);
            Subs.CVar(_config, CCVars.PlaytestAllHealModifier, value =>
            {
                UniversalAllHealModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            }, true);
            Subs.CVar(_config, CCVars.PlaytestProjectileDamageModifier, value => UniversalProjectileDamageModifier = value, true);
            Subs.CVar(_config, CCVars.PlaytestMeleeDamageModifier, value => UniversalMeleeDamageModifier = value, true);
            Subs.CVar(_config, CCVars.PlaytestProjectileDamageModifier, value => UniversalProjectileDamageModifier = value, true);
            Subs.CVar(_config, CCVars.PlaytestHitscanDamageModifier, value => UniversalHitscanDamageModifier = value, true);
            Subs.CVar(_config, CCVars.PlaytestReagentDamageModifier, value =>
            {
                UniversalReagentDamageModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            }, true);
            Subs.CVar(_config, CCVars.PlaytestReagentHealModifier, value =>
            {
                UniversalReagentHealModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            }, true);
            Subs.CVar(_config, CCVars.PlaytestExplosionDamageModifier, value => UniversalExplosionDamageModifier = value, true);
            Subs.CVar(_config, CCVars.PlaytestThrownDamageModifier, value => UniversalThrownDamageModifier = value, true);
            Subs.CVar(_config, CCVars.PlaytestTopicalsHealModifier, value => UniversalTopicalsHealModifier = value, true);
            Subs.CVar(_config, CCVars.PlaytestMobDamageModifier, value => UniversalMobDamageModifier = value, true);
        }

        private void DamageableInit(EntityUid uid, DamageableComponent component, ComponentInit _)
        {
            if (component.DamageContainerID != null &&
                _prototypeManager.TryIndex<DamageContainerPrototype>(component.DamageContainerID,
                out var damageContainerPrototype))
            {
                foreach (var type in damageContainerPrototype.SupportedTypes)
                {
                    component.Damage.DamageDict.TryAdd(type, FixedPoint2.Zero);
                }

                foreach (var groupId in damageContainerPrototype.SupportedGroups)
                {
                    var group = _prototypeManager.Index<DamageGroupPrototype>(groupId);
                    foreach (var type in group.DamageTypes)
                    {
                        component.Damage.DamageDict.TryAdd(type, FixedPoint2.Zero);
                    }
                }
            }
            else
            {
                foreach (var type in _prototypeManager.EnumeratePrototypes<DamageTypePrototype>())
                {
                    component.Damage.DamageDict.TryAdd(type.ID, FixedPoint2.Zero);
                }
            }

            component.Damage.GetDamagePerGroup(_prototypeManager, component.DamagePerGroup);
            component.TotalDamage = component.Damage.GetTotal();
        }

        public void SetDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)
        {
            damageable.Damage = damage;
            DamageChanged(uid, damageable);
        }

        public void DamageChanged(EntityUid uid, DamageableComponent component, DamageSpecifier? damageDelta = null,
            bool interruptsDoAfters = true, EntityUid? origin = null)
        {
            component.Damage.GetDamagePerGroup(_prototypeManager, component.DamagePerGroup);
            component.TotalDamage = component.Damage.GetTotal();
            Dirty(uid, component);

            if (_appearanceQuery.TryGetComponent(uid, out var appearance) && damageDelta != null)
            {
                var data = new DamageVisualizerGroupData(component.DamagePerGroup.Keys.ToList());
                _appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, data, appearance);
            }
            RaiseLocalEvent(uid, new DamageChangedEvent(component, damageDelta, interruptsDoAfters, origin));
        }

        private static DamageSpecifier BlendDamage(DamageSpecifier from, DamageSpecifier to, float t)
        {
            var result = new DamageSpecifier();
            var types = new HashSet<string>(from.DamageDict.Keys);
            types.UnionWith(to.DamageDict.Keys);

            foreach (var type in types)
            {
                var f = from.DamageDict.TryGetValue(type, out var fv) ? (float)fv : 0f;
                var d = to.DamageDict.TryGetValue(type, out var tv) ? (float)tv : 0f;
                var blended = f + (d - f) * t;
                if (blended != 0f)
                    result.DamageDict[type] = blended;
            }
            return result;
        }

        public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, float ignoreResistances = 0,
            bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, EntityUid? tool = null) // Horizon Attachments
        {
            if (!uid.HasValue || !_damageableQuery.Resolve(uid.Value, ref damageable, false))
            {
                return null;
            }

            if (damage.Empty)
            {
                return damage;
            }

            var before = new BeforeDamageChangedEvent(damage, origin);
            RaiseLocalEvent(uid.Value, ref before);

            if (before.Cancelled)
                return null;

            if (ignoreResistances < 100f)
            {
                var resistScale = 1f - (ignoreResistances / 100f);

                if (damageable.DamageModifierSetId != null &&
                    _prototypeManager.TryIndex<DamageModifierSetPrototype>(damageable.DamageModifierSetId, out var modifierSet))
                {
                    var damageBeforeModifier = damage;
                    var damageAfterModifier = DamageSpecifier.ApplyModifierSet(damageBeforeModifier, modifierSet);
                    damage = BlendDamage(damageBeforeModifier, damageAfterModifier, resistScale);
                }

                var preEventDamage = damage;
                var ev = new DamageModifyEvent(preEventDamage, origin, tool);
                RaiseLocalEvent(uid.Value, ev);

                damage = BlendDamage(preEventDamage, ev.Damage, resistScale);

                if (damage.Empty)
                {
                    return damage;
                }
            }

            damage = ApplyUniversalAllModifiers(damage);
            // Horizon start
            var finalEv = new DamageBeforeApplyEvent
            {
                Damage = damage,
                Origin = origin
            };
            RaiseLocalEvent(uid.Value, finalEv);
            if (finalEv.Cancelled)
                return damage;
            // Horizon end

            var delta = new DamageSpecifier();
            delta.DamageDict.EnsureCapacity(damage.DamageDict.Count);

            var dict = damageable.Damage.DamageDict;
            foreach (var (type, value) in damage.DamageDict)
            {
                if (!dict.TryGetValue(type, out var oldValue))
                    continue;

                var newValue = FixedPoint2.Max(FixedPoint2.Zero, oldValue + value);
                if (newValue == oldValue)
                    continue;

                dict[type] = newValue;
                delta.DamageDict[type] = newValue - oldValue;
            }

            if (delta.DamageDict.Count > 0)
                DamageChanged(uid.Value, damageable, delta, interruptsDoAfters, origin);

            return delta;
        }

        // ИЗМЕНЕНО: добавлена bool-перегрузка для обратной совместимости со старыми вызовами.
        // true  → 100f (игнорировать сопротивления полностью)
        // false → 0f   (применять сопротивления полностью)
        [Obsolete("Используйте float-версию: 0 = применять сопротивления, 100 = игнорировать.")]
        public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances,
            bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, EntityUid? tool = null)
            => TryChangeDamage(uid, damage, ignoreResistances ? 100f : 0f,
                               interruptsDoAfters, damageable, origin, tool);

        public DamageSpecifier ApplyUniversalAllModifiers(DamageSpecifier damage)
        {
            if (UniversalAllDamageModifier == 1f && UniversalAllHealModifier == 1f)
                return damage;

            foreach (var (key, value) in damage.DamageDict)
            {
                if (value == 0)
                    continue;

                if (value > 0)
                {
                    damage.DamageDict[key] *= UniversalAllDamageModifier;
                    continue;
                }

                if (value < 0)
                {
                    damage.DamageDict[key] *= UniversalAllHealModifier;
                }
            }

            return damage;
        }

        public void SetAllDamage(EntityUid uid, DamageableComponent component, FixedPoint2 newValue)
        {
            if (newValue < 0)
                return;

            foreach (var type in component.Damage.DamageDict.Keys)
            {
                component.Damage.DamageDict[type] = newValue;
            }

            DamageChanged(uid, component, new DamageSpecifier());
        }

        public void SetDamageModifierSetId(EntityUid uid, string? damageModifierSetId, DamageableComponent? comp = null)
        {
            if (!_damageableQuery.Resolve(uid, ref comp))
                return;

            comp.DamageModifierSetId = damageModifierSetId;
            Dirty(uid, comp);
        }

        private void DamageableGetState(EntityUid uid, DamageableComponent component, ref ComponentGetState args)
        {
            if (_netMan.IsServer)
            {
                args.State = new DamageableComponentState(component.Damage.DamageDict, component.DamageContainerID, component.DamageModifierSetId, component.HealthBarThreshold);
            }
            else
            {
                args.State = new DamageableComponentState(component.Damage.DamageDict.ShallowClone(), component.DamageContainerID, component.DamageModifierSetId, component.HealthBarThreshold);
            }
        }

        private void OnIrradiated(EntityUid uid, DamageableComponent component, OnIrradiatedEvent args)
        {
            var damageValue = FixedPoint2.New(args.TotalRads);

            DamageSpecifier damage = new();
            foreach (var typeId in component.RadiationDamageTypeIDs)
            {
                damage.DamageDict.Add(typeId, damageValue);
            }

            TryChangeDamage(uid, damage, interruptsDoAfters: false, origin: args.Origin);
        }

        private void OnRejuvenate(EntityUid uid, DamageableComponent component, RejuvenateEvent args)
        {
            TryComp<MobThresholdsComponent>(uid, out var thresholds);
            _mobThreshold.SetAllowRevives(uid, true, thresholds);
            SetAllDamage(uid, component, 0);
            _mobThreshold.SetAllowRevives(uid, false, thresholds);
        }

        private void DamageableHandleState(EntityUid uid, DamageableComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not DamageableComponentState state)
                return;

            component.DamageContainerID = state.DamageContainerId;
            component.DamageModifierSetId = state.ModifierSetId;
            component.HealthBarThreshold = state.HealthBarThreshold;

            DamageSpecifier newDamage = new() { DamageDict = new(state.DamageDict) };
            var delta = newDamage - component.Damage;
            delta.TrimZeros();

            if (!delta.Empty)
            {
                component.Damage = newDamage;
                DamageChanged(uid, component, delta);
            }
        }
    }

    [ByRefEvent]
    public record struct BeforeDamageChangedEvent(DamageSpecifier Damage, EntityUid? Origin = null, bool Cancelled = false);

    public sealed class DamageModifyEvent : EntityEventArgs, IInventoryRelayEvent
    {
        public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

        public readonly DamageSpecifier OriginalDamage;
        public DamageSpecifier Damage;
        public EntityUid? Origin;
        public EntityUid? Tool;

        public DamageModifyEvent(DamageSpecifier damage, EntityUid? origin = null, EntityUid? tool = null)
        {
            OriginalDamage = damage;
            Damage = damage;
            Origin = origin;
            Tool = tool;
        }
    }

    public sealed class DamageChangedEvent : EntityEventArgs
    {
        public readonly DamageableComponent Damageable;
        public readonly DamageSpecifier? DamageDelta;
        public readonly bool DamageIncreased;
        public readonly bool InterruptsDoAfters;
        public readonly EntityUid? Origin;

        public DamageChangedEvent(DamageableComponent damageable, DamageSpecifier? damageDelta, bool interruptsDoAfters, EntityUid? origin)
        {
            Damageable = damageable;
            DamageDelta = damageDelta;
            Origin = origin;

            if (DamageDelta == null)
                return;

            foreach (var damageChange in DamageDelta.DamageDict.Values)
            {
                if (damageChange > 0)
                {
                    DamageIncreased = true;
                    break;
                }
            }
            InterruptsDoAfters = interruptsDoAfters && DamageIncreased;
        }
    }
}
