using System.Collections.Generic;
using System.Linq;
using GAS.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityObject = UnityEngine.Object;

namespace GameCore
{
    public abstract partial class CharacterBase
    {
        protected virtual void OnFormalGasAbilityAdded(int formalGasAbilityCode)
        {
            if (formalGasAbilityCode <= 0 || !TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return;
            }

            abilitySet.RegisterFormalGasAbilityRule(formalGasAbilityCode);
            ApplyFormalGasAbilitySuppressionState(formalGasAbilityCode);
            abilitySet.TryAutoEquipOwnedFormalGasAbilityCode(formalGasAbilityCode);
            YokiFrame.EventKit.Type.Send(new CharacterAbilityAddedEvent(this, formalGasAbilityCode));
        }

        protected virtual void OnFormalGasAbilityRemoved(int formalGasAbilityCode)
        {
            if (formalGasAbilityCode <= 0 || !TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return;
            }

            abilitySet.UnregisterFormalGasAbilityRule(formalGasAbilityCode);
            abilitySet.RemoveEquippedFormalGasAbilityCodeFromAllSlots(formalGasAbilityCode);
            YokiFrame.EventKit.Type.Send(new CharacterAbilityRemovedEvent(this, formalGasAbilityCode));
        }

        private void InitializeAbilities()
        {
            IEnumerable<int> formalGasAbilityCodes =
                TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet)
                    ? abilitySet.CreateInitialFormalGasAbilityCodeSet(characterSheet.GetAvailableFormalGasAbilitiesAtLevel(m_level))
                    : characterSheet.GetAvailableFormalGasAbilitiesAtLevel(m_level);
            UnlockFormalGasAbilitiesForLevel(formalGasAbilityCodes);
        }

        public bool AddBonusFormalGasAbility(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            if (formalGasAbilityCode <= 0 || count <= 0)
            {
                return false;
            }

            return AbilityRuntime.TryRegisterBonusFormalGasAbility(
                formalGasAbilityCode,
                source,
                InstantiateFormalGasAbilityPrefab,
                count,
                OnFormalGasAbilityAdded);
        }

        public bool RemoveBonusFormalGasAbility(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            if (formalGasAbilityCode <= 0 || count <= 0)
            {
                return false;
            }

            return AbilityRuntime.TryUnregisterBonusFormalGasAbility(
                formalGasAbilityCode,
                source,
                count,
                ReleaseAbilityPrefab,
                OnFormalGasAbilityRemoved);
        }

        /// <summary>
        /// 状态效果、变形和感染等临时规则只通过来源键授予能力。
        /// 这样效果结束或形态退出时可以精确撤回对应来源，不会误删装备、永久成长或其它状态授予的同名能力。
        /// </summary>
        public bool AddSourcedBonusFormalGasAbility(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            if (formalGasAbilityCode <= 0 || count <= 0 || !IsTemporaryAbilitySourceKind(source.Kind))
            {
                return false;
            }

            return AbilityRuntime.TryRegisterBonusFormalGasAbility(
                formalGasAbilityCode,
                source,
                InstantiateFormalGasAbilityPrefab,
                count,
                OnFormalGasAbilityAdded);
        }

        public bool RemoveSourcedBonusFormalGasAbility(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            if (formalGasAbilityCode <= 0 || count <= 0 || !IsTemporaryAbilitySourceKind(source.Kind))
            {
                return false;
            }

            return AbilityRuntime.TryUnregisterBonusFormalGasAbility(
                formalGasAbilityCode,
                source,
                count,
                ReleaseAbilityPrefab,
                OnFormalGasAbilityRemoved);
        }

        public CharacterAbilitySourceRuntimeEntry[] RemoveAllSourcedBonusAbilities(CharacterAbilitySourceKey source)
        {
            if (!IsTemporaryAbilitySourceKind(source.Kind))
            {
                return System.Array.Empty<CharacterAbilitySourceRuntimeEntry>();
            }

            CharacterAbilitySourceRuntimeEntry[] entries = AbilityRuntime.CreateBonusAbilitySourceEntrySnapshot(source);
            foreach (CharacterAbilitySourceRuntimeEntry entry in entries)
            {
                if (entry.HasFormalGasAbility)
                {
                    AbilityRuntime.TryUnregisterBonusFormalGasAbility(
                        entry.FormalGasAbilityCode,
                        entry.Source,
                        entry.StackCount,
                        ReleaseAbilityPrefab,
                        OnFormalGasAbilityRemoved);
                }
            }

            return entries;
        }

        /// <summary>
        /// 状态效果、变形和感染禁用能力时只登记来源化压制，不删除能力实例。
        /// 这样变形/沉默结束时只撤回自己的禁用层，不会误恢复仍被其它来源压制的能力。
        /// </summary>
        public bool AddSourcedFormalGasAbilitySuppression(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            if (formalGasAbilityCode <= 0 || count <= 0 || !IsTemporaryAbilitySourceKind(source.Kind))
            {
                return false;
            }

            bool changed = AbilityRuntime.TrySuppressFormalGasAbility(formalGasAbilityCode, source, count);
            if (changed)
            {
                ApplyFormalGasAbilitySuppressionState(formalGasAbilityCode);
            }

            return true;
        }

        public bool RemoveSourcedFormalGasAbilitySuppression(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            if (formalGasAbilityCode <= 0 || count <= 0 || !IsTemporaryAbilitySourceKind(source.Kind))
            {
                return false;
            }

            bool changed = AbilityRuntime.TryUnsuppressFormalGasAbility(formalGasAbilityCode, source, count);
            if (changed)
            {
                ApplyFormalGasAbilitySuppressionState(formalGasAbilityCode);
            }

            return true;
        }

        public CharacterAbilitySourceRuntimeEntry[] RemoveAllSourcedAbilitySuppressions(CharacterAbilitySourceKey source)
        {
            if (!IsTemporaryAbilitySourceKind(source.Kind))
            {
                return System.Array.Empty<CharacterAbilitySourceRuntimeEntry>();
            }

            CharacterAbilitySourceRuntimeEntry[] entries = AbilityRuntime.CreateSuppressedAbilitySourceEntrySnapshot(source);
            foreach (CharacterAbilitySourceRuntimeEntry entry in entries)
            {
                bool changed = entry.HasFormalGasAbility &&
                    AbilityRuntime.TryUnsuppressFormalGasAbility(entry.FormalGasAbilityCode, entry.Source, entry.StackCount);
                if (changed)
                {
                    ApplyFormalGasAbilitySuppressionState(entry.FormalGasAbilityCode);
                }
            }

            return entries;
        }

        public bool AddStatusEffectFormalGasAbility(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            return AddSourcedBonusFormalGasAbility(formalGasAbilityCode, source, count);
        }

        public bool RemoveStatusEffectFormalGasAbility(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            return RemoveSourcedBonusFormalGasAbility(formalGasAbilityCode, source, count);
        }

        public CharacterAbilitySourceRuntimeEntry[] RemoveAllStatusEffectAbilities(CharacterAbilitySourceKey source)
        {
            return RemoveAllSourcedBonusAbilities(source);
        }

        public bool AddStatusEffectFormalGasAbilitySuppression(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            return AddSourcedFormalGasAbilitySuppression(formalGasAbilityCode, source, count);
        }

        public bool RemoveStatusEffectFormalGasAbilitySuppression(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            return RemoveSourcedFormalGasAbilitySuppression(formalGasAbilityCode, source, count);
        }

        public CharacterAbilitySourceRuntimeEntry[] RemoveAllStatusEffectAbilitySuppressions(CharacterAbilitySourceKey source)
        {
            return RemoveAllSourcedAbilitySuppressions(source);
        }

        public CharacterAbilitySourceRuntimeEntry[] RemoveAllTransformationAbilities(string transformationId)
        {
            return RemoveAllSourcedBonusAbilities(CreateTransformationAbilitySource(transformationId));
        }

        public CharacterAbilitySourceRuntimeEntry[] RemoveAllTransformationAbilitySuppressions(string transformationId)
        {
            return RemoveAllSourcedAbilitySuppressions(CreateTransformationAbilitySource(transformationId));
        }

        public CharacterAbilitySourceRuntimeEntry[] RemoveAllInfectionAbilities(string infectionId)
        {
            return RemoveAllSourcedBonusAbilities(CreateInfectionAbilitySource(infectionId));
        }

        public CharacterAbilitySourceRuntimeEntry[] RemoveAllInfectionAbilitySuppressions(string infectionId)
        {
            return RemoveAllSourcedAbilitySuppressions(CreateInfectionAbilitySource(infectionId));
        }

        public bool HasFormalGasAbility(int formalGasAbilityCode)
        {
            return AbilityRuntime.HasFormalGasAbility(formalGasAbilityCode);
        }

        public bool IsFormalGasAbilitySuppressed(int formalGasAbilityCode)
        {
            return AbilityRuntime.IsFormalGasAbilitySuppressed(formalGasAbilityCode);
        }

        public int[] CreateOwnedFormalGasAbilityCodeSnapshot()
        {
            return AbilityRuntime.GetFormalGasAbilityCodeSnapshots();
        }

        /// <summary>
        /// 读档前必须先撤掉当前对象上残留的来源化能力与压制状态。
        /// 仅清空计数会把复用对象上的旧实例或禁用状态带进新档。
        /// </summary>
        private void ClearOwnedAbilitySourceRuntimeState()
        {
            foreach (CharacterAbilitySourceRuntimeEntry entry in AbilityRuntime.CreateBonusAbilitySourceEntrySnapshot())
            {
                if (entry.HasFormalGasAbility)
                {
                    AbilityRuntime.TryUnregisterBonusFormalGasAbility(
                        entry.FormalGasAbilityCode,
                        entry.Source,
                        entry.StackCount,
                        ReleaseAbilityPrefab,
                        OnFormalGasAbilityRemoved);
                }
            }

            foreach (CharacterAbilitySourceRuntimeEntry entry in AbilityRuntime.CreateSuppressedAbilitySourceEntrySnapshot())
            {
                bool changed = AbilityRuntime.TryUnsuppressFormalGasAbility(entry.FormalGasAbilityCode, entry.Source, entry.StackCount);
                if (changed)
                {
                    ApplyFormalGasAbilitySuppressionState(entry.FormalGasAbilityCode);
                }
            }
        }

        private void ApplyFormalGasAbilitySuppressionState(int formalGasAbilityCode)
        {
            if (!AbilityRuntime.TryGetFormalGasAbilityInstance(
                    formalGasAbilityCode,
                    out AbilityBase abilityInstance) ||
                abilityInstance == null)
            {
                return;
            }

            bool suppressed = AbilityRuntime.IsFormalGasAbilitySuppressed(formalGasAbilityCode);
            if (abilityInstance is ActiveAbilityBase activeAbility)
            {
                activeAbility.PermitAbility(!suppressed);
            }

            if (suppressed)
            {
                if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
                {
                    abilitySet.CancelFormalGasAbilityRuleLifecycle(formalGasAbilityCode);
                }

                abilityInstance.Interrupt();
                abilityInstance.gameObject.SetActive(false);
                return;
            }

            abilityInstance.gameObject.SetActive(GetDefaultAbilityState(abilityInstance));
        }

        private static CharacterAbilitySourceKey CreateTransformationAbilitySource(string transformationId)
        {
            return new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, transformationId);
        }

        private static CharacterAbilitySourceKey CreateInfectionAbilitySource(string infectionId)
        {
            return new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Infection, infectionId);
        }

        private static bool IsTemporaryAbilitySourceKind(ECharacterAbilitySourceKind sourceKind)
        {
            return sourceKind == ECharacterAbilitySourceKind.StatusEffect ||
                sourceKind == ECharacterAbilitySourceKind.Transformation ||
                sourceKind == ECharacterAbilitySourceKind.Infection;
        }

        /// <summary>
        /// 能力菜单和其它展示层只允许拿主动能力资产快照。
        /// 这里由角色正式拥有者从实例仓库投影出可读结果，而不是把查询职责继续塞在内部 runtime helper 上。
        /// </summary>
        public CharacterAbilityMenuEntry[] GetActiveAbilityMenuEntrySnapshots()
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.CreateActiveAbilityMenuEntrySnapshot();
            }

            return System.Array.Empty<CharacterAbilityMenuEntry>();
        }

        /// <summary>
        /// 能力菜单和其它展示层只允许拿被动能力资产快照。
        /// 这里和主动能力入口保持同一层角色公开接口，避免 UI 直接依赖内部能力集合。
        /// </summary>
        public bool TryGetFirstTriggerableFormalGasAbilityCode(out int formalGasAbilityCode)
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.TryGetFirstTriggerableFormalGasAbilityCode(out formalGasAbilityCode);
            }

            formalGasAbilityCode = 0;
            return false;
        }

        public bool TryGetActiveAbilityCooldownSnapshot(
            CharacterEquippedAbilitySlotView slot,
            out CharacterAbilityCooldownSnapshot snapshot)
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.TryGetActiveAbilityCooldownSnapshot(slot, out snapshot);
            }

            snapshot = default;
            return false;
        }

        public CharacterEquippedAbilitySlotView[] GetEquippedAbilitySlotViewSnapshots()
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.CreateEquippedAbilitySlotViewSnapshot();
            }

            return System.Array.Empty<CharacterEquippedAbilitySlotView>();
        }

        public CharacterAbilityFireResult FireEquippedAbilityAtIndex(int index, GameCommandContext commandContext)
        {
            return FireEquippedAbilityAtIndex(index, commandContext, null);
        }

        internal CharacterAbilityFireResult FireEquippedAbilityAtIndex(
            int index,
            GameCommandContext commandContext,
            AbilityActivationContext activationContext)
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.FireEquippedAbilityAtIndex(index, commandContext, activationContext);
            }

            return new CharacterAbilityFireResult(EAbilityFireCheckResult.Unknown, 0);
        }

        public bool StopFireEquippedAbilityAtIndex(int index)
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.StopFireEquippedAbilityAtIndex(index);
            }

            return false;
        }

        public EAbilityFireCheckResult FireFormalGasAbility(int formalGasAbilityCode, GameCommandContext commandContext)
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.FireFormalGasAbility(formalGasAbilityCode, commandContext);
            }

            Debug.LogError($"[{nameof(CharacterBase)}] Missing formal {nameof(CharacterAbilitySet)} for EX-GAS ability fire.", this);
            return EAbilityFireCheckResult.Unknown;
        }

        public bool StopFireFormalGasAbility(int formalGasAbilityCode)
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.StopFireFormalGasAbility(formalGasAbilityCode);
            }

            return false;
        }

        public bool TryEquipFormalGasAbilityCodeToSlot(int formalGasAbilityCode, int index)
        {
            return TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet) &&
                abilitySet.TryEquipFormalGasAbilityCodeToSlot(formalGasAbilityCode, index);
        }

        public bool ClearEquippedAbilitySlot(int index)
        {
            return TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet) &&
                abilitySet.ClearEquippedAbilitySlot(index);
        }

        public CharacterAbilitySlotData[] CreateEquippedAbilitySlotDataSnapshot(DatabaseRegistry databaseRegistry)
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.CreateEquippedAbilitySlotDataSnapshot(databaseRegistry);
            }

            return System.Array.Empty<CharacterAbilitySlotData>();
        }

        public bool RestoreEquippedAbilitiesFromSlotData(
            System.Collections.Generic.IEnumerable<CharacterAbilitySlotData> quickAbilitySlots)
        {
            return TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet) &&
                abilitySet.RestoreEquippedAbilitiesFromSlotData(quickAbilitySlots);
        }

        public void AddEquippedAbilitiesChangedListener(UnityAction<CharacterEquippedAbilitySlotView[]> listener)
        {
            if (listener == null)
            {
                return;
            }

            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                abilitySet.AddEquippedAbilitiesChangedListener(listener);
            }
        }

        public void RemoveEquippedAbilitiesChangedListener(UnityAction<CharacterEquippedAbilitySlotView[]> listener)
        {
            if (listener == null)
            {
                return;
            }

            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                abilitySet.RemoveEquippedAbilitiesChangedListener(listener);
            }
        }

        /// <summary>
        /// 查询除指定能力外，是否有其它可触发能力处于指定本地输入门控状态。
        /// 这是 GameCore 对“同一角色不能同时进入多个出手流程”的本地门控，不承载命中、伤害或表现真相。
        /// </summary>
        public bool HasOtherAbilityInInputGateState(ActiveAbilityBase ignoredAbility, EFormalAbilityInputGateState[] blockedStates)
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return abilitySet.HasOtherAbilityInInputGateState(ignoredAbility, blockedStates);
            }

            return false;
        }

        /// <summary>
        /// 动作打断只通知显式声明了中断合同的能力实例。
        /// 这里不再用 BroadcastMessage 依赖字符串和层级扫描，避免把打断语义偷偷扩散到无关组件。
        /// </summary>
        private void InterruptActions() => AbilityRuntime.NotifyActionInterrupted();

        private void UnlockFormalGasAbilitiesForLevel(IEnumerable<int> formalGasAbilityCodes)
        {
            if (formalGasAbilityCodes == null)
            {
                return;
            }

            foreach (int formalGasAbilityCode in formalGasAbilityCodes)
            {
                AbilityRuntime.TryAddUnlockedFormalGasAbility(
                    formalGasAbilityCode,
                    InstantiateFormalGasAbilityPrefab,
                    OnFormalGasAbilityAdded);
            }
        }

        private AbilityBase InstantiateFormalGasAbilityPrefab(int formalGasAbilityCode)
        {
            if (formalGasAbilityCode <= 0 ||
                !FormalGasAbilityRuntimeConfigResolver.TryResolveRuntimeConfig(
                    formalGasAbilityCode,
                    out FormalGasAbilityRuntimeConfig config) ||
                !config.TryLoadPrefab(out GameObject prefab) ||
                prefab == null)
            {
                Debug.LogError($"EX-GAS Ability {formalGasAbilityCode} 缺少可创建的正式 Prefab；请补齐 exgas.abilityGameCore 配置。", this);
                return null;
            }

            Transform abilityRoot = null;
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                abilitySet.TryResolveFormalGasAbilityRoot(config.AbilityRootMode, out abilityRoot);
            }

            Debug.Assert(abilityRoot != null, "No ability root found! Make sure to assign each ability root in the inspector.");

            GameObject instance = UnityObject.Instantiate(prefab, abilityRoot);
            instance.name = FormalGasAbilityIdentityResolver.TryResolveAbilityIdentity(
                    formalGasAbilityCode,
                    out FormalGasAbilityIdentity identity) &&
                !string.IsNullOrWhiteSpace(identity.DisplayName)
                    ? identity.DisplayName
                    : $"EX-GAS Ability {formalGasAbilityCode}";

            AbilityBase ability = instance.GetComponent<AbilityBase>();
            Debug.Assert(ability, $"The provided ability prefab doesn't have a behaviour of type {typeof(AbilityBase).Name} attached to its root");
            if (ability == null)
            {
                UnityObject.Destroy(instance);
                return null;
            }

            ability.InitFormalGasAbility(this, formalGasAbilityCode);
            ability.gameObject.SetActive(GetDefaultAbilityState(ability));
            return ability;
        }

        private void ReleaseAbilityPrefab(AbilityBase ability)
        {
            if (ability)
            {
                if (Application.isPlaying)
                {
                    UnityObject.Destroy(ability.gameObject);
                }
                else
                {
                    UnityObject.DestroyImmediate(ability.gameObject);
                }
            }
        }

        private static bool GetDefaultAbilityState(AbilityBase abilityInstance)
        {
            if (abilityInstance == null)
            {
                return false;
            }

            return abilityInstance.usesFormalGasAbilityForRuntime &&
                abilityInstance is not ITriggerableAbility;
        }

    }
}


