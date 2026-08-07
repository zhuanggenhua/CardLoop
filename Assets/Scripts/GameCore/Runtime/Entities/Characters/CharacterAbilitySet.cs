using System;
using System.Collections.Generic;
using System.Linq;
using GAS.Runtime;
using UnityEngine;
using UnityEngine.Events;

namespace GameCore
{
    // CharacterBase 已经正向要求 CharacterAbilitySet；
    // 这里不能反向 Require 抽象 CharacterBase，否则 Unity 自动补组件时会失败。
    [DisallowMultipleComponent]
    public sealed partial class CharacterAbilitySet : MonoBehaviour
    {
        private const string FormalRuleLogicTypeName = "FormalAbilityRuleProxyLogic";
        private const int FormalAbilityCodeSeed = 1200000000;
        private const int FormalCooldownTagSeed = 1700000000;

        [Header("Ability Composition")]
        [SerializeField] private CharacterBase m_character = null;
        [SerializeField] private bool m_ownsAbilityComposition = true;
        [SerializeField] private Transform m_staticAbilityRoot = null;
        [SerializeField] private Transform m_polydirectionalAbilityRoot = null;
        [SerializeField] private Transform m_horizontalAbilityRoot = null;
        [SerializeField] private int[] m_additionalFormalGasAbilityCodes = null;

        private readonly CharacterAbilitySetRuntime m_runtime = new();
        private readonly CharacterEquippedAbilityLoadout m_equippedAbilityLoadout = new(Constants.MaxEquipedAbilityCount);
        private readonly UnityEvent<CharacterEquippedAbilitySlotView[]> m_equippedAbilitiesChanged = new();
        public CharacterBase Character => m_character;
        public bool OwnsAbilityComposition => m_ownsAbilityComposition;
        internal CharacterAbilitySetRuntime Runtime => m_runtime;

        internal CharacterAbilityMenuEntry[] CreateActiveAbilityMenuEntrySnapshot()
        {
            if (!m_ownsAbilityComposition)
            {
                return System.Array.Empty<CharacterAbilityMenuEntry>();
            }

            List<CharacterAbilityMenuEntry> entries = new();
            foreach (int formalGasAbilityCode in m_runtime.GetFormalGasAbilityCodeSnapshots())
            {
                entries.Add(new CharacterAbilityMenuEntry(formalGasAbilityCode));
            }

            return entries.ToArray();
        }

        internal CharacterEquippedAbilitySlotView[] CreateEquippedAbilitySlotViewSnapshot()
        {
            return m_ownsAbilityComposition
                ? m_equippedAbilityLoadout.CreateSlotViewSnapshot()
                : Array.Empty<CharacterEquippedAbilitySlotView>();
        }

        internal bool TryGetFirstTriggerableFormalGasAbilityCode(out int formalGasAbilityCode)
        {
            formalGasAbilityCode = 0;
            if (!m_ownsAbilityComposition)
            {
                return false;
            }

            foreach (int candidateCode in m_runtime.GetFormalGasAbilityCodeSnapshots())
            {
                if (TryGetResolvedFormalGasActiveAbility(candidateCode, out ActiveAbilityBase ability) &&
                    ability.CanFire() == EAbilityFireCheckResult.Valid)
                {
                    formalGasAbilityCode = candidateCode;
                    return true;
                }
            }

            return false;
        }

        internal EAbilityFireCheckResult FireFormalGasAbility(int formalGasAbilityCode, GameCommandContext commandContext)
        {
            if (!m_ownsAbilityComposition ||
                m_character == null ||
                formalGasAbilityCode <= 0 ||
                !TryGetResolvedFormalGasActiveAbility(formalGasAbilityCode, out ActiveAbilityBase ability))
            {
                return EAbilityFireCheckResult.Unknown;
            }

            return FireResolvedAbility(ability, commandContext);
        }

        internal CharacterAbilityFireResult FireEquippedAbilityAtIndex(
            int index,
            GameCommandContext commandContext,
            AbilityActivationContext activationContext = null)
        {
            int formalGasAbilityCode = m_equippedAbilityLoadout.GetFormalGasAbilityCode(index);
            if (formalGasAbilityCode > 0 &&
                TryGetResolvedFormalGasActiveAbility(formalGasAbilityCode, out ActiveAbilityBase formalAbility))
            {
                return new CharacterAbilityFireResult(
                    FireResolvedAbility(formalAbility, commandContext, activationContext),
                    formalGasAbilityCode);
            }

            return new CharacterAbilityFireResult(EAbilityFireCheckResult.Unknown, 0);
        }

        internal bool StopFireFormalGasAbility(int formalGasAbilityCode)
        {
            if (!m_ownsAbilityComposition ||
                formalGasAbilityCode <= 0 ||
                !TryGetResolvedFormalGasActiveAbility(formalGasAbilityCode, out ActiveAbilityBase activeAbility))
            {
                return false;
            }

            activeAbility.StopFire();
            return true;
        }

        internal bool StopFireEquippedAbilityAtIndex(int index)
        {
            int formalGasAbilityCode = m_equippedAbilityLoadout.GetFormalGasAbilityCode(index);
            if (formalGasAbilityCode > 0 &&
                TryGetResolvedFormalGasActiveAbility(formalGasAbilityCode, out ActiveAbilityBase formalAbility))
            {
                formalAbility.StopFire();
                return true;
            }

            return false;
        }

        internal bool TryGetActiveAbilityCooldownSnapshot(
            CharacterEquippedAbilitySlotView slot,
            out CharacterAbilityCooldownSnapshot snapshot)
        {
            snapshot = default;
            if (!m_ownsAbilityComposition)
            {
                return false;
            }

            if (slot.FormalGasAbilityCode > 0 &&
                TryGetResolvedFormalGasActiveAbility(slot.FormalGasAbilityCode, out ActiveAbilityBase activeAbility))
            {
                activeAbility.GetCooldownState(out float remainingCooldown, out float cooldown);
                snapshot = new CharacterAbilityCooldownSnapshot(slot.FormalGasAbilityCode, remainingCooldown, cooldown);
                return true;
            }

            return false;
        }

        internal bool HasOtherAbilityInInputGateState(ActiveAbilityBase ignoredAbility, EFormalAbilityInputGateState[] blockedStates)
        {
            if (!m_ownsAbilityComposition || blockedStates == null || blockedStates.Length == 0)
            {
                return false;
            }

            foreach (KeyValuePair<int, AbilityBase> entry in m_runtime.GetFormalGasAbilityInstanceEntriesSnapshot())
            {
                if (entry.Value is not ActiveAbilityBase ability || ability == ignoredAbility)
                {
                    continue;
                }

                for (int i = 0; i < blockedStates.Length; i++)
                {
                    if (ability.inputGateState == blockedStates[i])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool ShouldLockTargetDirectionForInputGate()
        {
            if (!m_ownsAbilityComposition)
            {
                return false;
            }

            foreach (KeyValuePair<int, AbilityBase> entry in m_runtime.GetFormalGasAbilityInstanceEntriesSnapshot())
            {
                if (entry.Value is ActiveAbilityBase ability &&
                    ability.ShouldLockTargetDirectionDuringInputGateForRuntime())
                {
                    return true;
                }
            }

            return false;
        }

        internal bool TryGetFormalGasAbilityRuleCooldownState(
            int formalGasAbilityCode,
            out float remainingCooldown,
            out float cooldown)
        {
            remainingCooldown = 0.0f;
            cooldown = 0.0f;
            return m_ownsAbilityComposition &&
                formalGasAbilityCode > 0 &&
                TryGetFormalAbilityCooldownState(formalGasAbilityCode, out remainingCooldown, out cooldown);
        }

        internal bool TryEvaluateFormalGasAbilityRuleActivation(
            int formalGasAbilityCode,
            out EAbilityFireCheckResult result,
            out bool usesFormalCost)
        {
            result = EAbilityFireCheckResult.Unknown;
            usesFormalCost = false;
            return m_ownsAbilityComposition &&
                formalGasAbilityCode > 0 &&
                TryEvaluateFormalAbilityActivation(formalGasAbilityCode, out result, out usesFormalCost);
        }

        internal bool TryApplyFormalGasAbilityRuleCooldown(int formalGasAbilityCode, float durationOverride = 0.0f)
        {
            return m_ownsAbilityComposition &&
                formalGasAbilityCode > 0 &&
                TryApplyFormalAbilityCooldown(formalGasAbilityCode, durationOverride);
        }

        internal bool TryCommitFormalGasAbilityRuleUse(int formalGasAbilityCode, out EAbilityFireCheckResult result)
        {
            result = EAbilityFireCheckResult.Unknown;
            return m_ownsAbilityComposition &&
                formalGasAbilityCode > 0 &&
                TryCommitFormalAbilityUse(formalGasAbilityCode, true, out result);
        }

        internal bool TryValidateFormalGasAbilityRuleUseAtFirePoint(int formalGasAbilityCode, out EAbilityFireCheckResult result)
        {
            result = EAbilityFireCheckResult.Unknown;
            return m_ownsAbilityComposition &&
                formalGasAbilityCode > 0 &&
                TryValidateFormalAbilityUseAtFirePoint(formalGasAbilityCode, out result);
        }

        internal bool BeginFormalGasAbilityRuleLifecycle(
            int formalGasAbilityCode,
            AbilityActivationContext activationContext)
        {
            return m_ownsAbilityComposition &&
                formalGasAbilityCode > 0 &&
                BeginFormalAbilityRuleLifecycle(formalGasAbilityCode, activationContext);
        }

        internal void EndFormalGasAbilityRuleLifecycle(int formalGasAbilityCode)
        {
            if (m_ownsAbilityComposition && formalGasAbilityCode > 0)
            {
                EndFormalAbilityRuleLifecycle(formalGasAbilityCode);
            }
        }

        internal void CancelFormalGasAbilityRuleLifecycle(int formalGasAbilityCode)
        {
            if (m_ownsAbilityComposition && formalGasAbilityCode > 0)
            {
                CancelFormalAbilityRuleLifecycle(formalGasAbilityCode);
            }
        }

        internal void ClearFormalGasAbilityRuleCooldown(int formalGasAbilityCode)
        {
            if (m_ownsAbilityComposition && formalGasAbilityCode > 0)
            {
                ClearFormalAbilityCooldown(formalGasAbilityCode);
            }
        }

        internal bool TryEquipFormalGasAbilityCodeToSlot(int formalGasAbilityCode, int index)
        {
            if (!m_ownsAbilityComposition || !CanAssignFormalGasAbilityCode(formalGasAbilityCode, index))
            {
                return false;
            }

            if (m_equippedAbilityLoadout.GetFormalGasAbilityCode(index) == formalGasAbilityCode)
            {
                return true;
            }

            bool changed = m_equippedAbilityLoadout.TryAssignFormalGasAbilityCodeToSlot(index, formalGasAbilityCode);
            if (changed)
            {
                NotifyEquippedAbilitiesChanged();
            }

            return changed;
        }

        internal bool ClearEquippedAbilitySlot(int index)
        {
            if (!m_ownsAbilityComposition)
            {
                return false;
            }

            bool changed = m_equippedAbilityLoadout.ClearSlot(index);
            if (changed)
            {
                NotifyEquippedAbilitiesChanged();
            }

            return changed;
        }

        internal bool TryAutoEquipOwnedFormalGasAbilityCode(int formalGasAbilityCode)
        {
            if (!m_ownsAbilityComposition || !CanAssignFormalGasAbilityCode(formalGasAbilityCode))
            {
                return false;
            }

            bool changed = m_equippedAbilityLoadout.TryAutoAssignFormalGasAbilityCode(formalGasAbilityCode);
            if (changed)
            {
                NotifyEquippedAbilitiesChanged();
            }

            return changed;
        }

        internal bool RemoveEquippedFormalGasAbilityCodeFromAllSlots(int formalGasAbilityCode)
        {
            if (!m_ownsAbilityComposition)
            {
                return false;
            }

            bool changed = m_equippedAbilityLoadout.RemoveFormalGasAbilityCodeFromAllSlots(formalGasAbilityCode);
            if (changed)
            {
                NotifyEquippedAbilitiesChanged();
            }

            return changed;
        }

        internal CharacterAbilitySlotData[] CreateEquippedAbilitySlotDataSnapshot(DatabaseRegistry databaseRegistry)
        {
            return m_ownsAbilityComposition
                ? m_equippedAbilityLoadout.CreateSlotDataSnapshot(databaseRegistry)
                : Array.Empty<CharacterAbilitySlotData>();
        }

        internal bool RestoreEquippedAbilitiesFromSlotData(
            IEnumerable<CharacterAbilitySlotData> quickAbilitySlots)
        {
            if (!m_ownsAbilityComposition)
            {
                return false;
            }

            bool restored = m_equippedAbilityLoadout.RestoreFromSlotData(quickAbilitySlots);
            NotifyEquippedAbilitiesChanged();
            return restored;
        }

        internal void AddEquippedAbilitiesChangedListener(UnityAction<CharacterEquippedAbilitySlotView[]> listener)
        {
            if (listener == null)
            {
                return;
            }

            m_equippedAbilitiesChanged.AddListener(listener);
        }

        internal void RemoveEquippedAbilitiesChangedListener(UnityAction<CharacterEquippedAbilitySlotView[]> listener)
        {
            if (listener == null)
            {
                return;
            }

            m_equippedAbilitiesChanged.RemoveListener(listener);
        }

        internal int[] CreateInitialFormalGasAbilityCodeSet(IEnumerable<int> sheetFormalGasAbilityCodes)
        {
            if (!m_ownsAbilityComposition)
            {
                return Array.Empty<int>();
            }

            List<int> codes = new();
            AddFormalGasAbilityCodes(codes, sheetFormalGasAbilityCodes);
            AddFormalGasAbilityCodes(codes, m_additionalFormalGasAbilityCodes);

            return codes.ToArray();
        }

        internal bool TryResolveFormalGasAbilityRoot(
            EFormalGasAbilityRootMode rootMode,
            out Transform abilityRoot)
        {
            abilityRoot = null;
            if (!m_ownsAbilityComposition)
            {
                return false;
            }

            abilityRoot = rootMode switch
            {
                EFormalGasAbilityRootMode.Static => m_staticAbilityRoot,
                EFormalGasAbilityRootMode.Polydirectional => m_polydirectionalAbilityRoot,
                EFormalGasAbilityRootMode.Horizontal => m_horizontalAbilityRoot,
                _ => null
            };

            return abilityRoot != null;
        }

        internal CharacterAbilityRuntimeStateData[] CreateAbilityRuntimeStates()
        {
            if (!m_ownsAbilityComposition)
            {
                return Array.Empty<CharacterAbilityRuntimeStateData>();
            }

            return m_runtime.GetFormalGasAbilityInstanceEntriesSnapshot()
                .Select(entry => entry.Value.CreateFormalRuntimeState())
                .Where(state => state != null)
                .ToArray();
        }

        internal void LoadAbilityRuntimeStates(CharacterAbilityRuntimeStateData[] abilityRuntimeStates)
        {
            if (!m_ownsAbilityComposition || abilityRuntimeStates == null)
            {
                return;
            }

            foreach (CharacterAbilityRuntimeStateData abilityRuntimeState in abilityRuntimeStates)
            {
                if (abilityRuntimeState == null)
                {
                    continue;
                }

                if (!TryResolveRuntimeStateAbility(abilityRuntimeState, out AbilityBase ability))
                {
                    continue;
                }

                ability.RestoreFormalRuntimeState(abilityRuntimeState);
            }
        }

        private bool TryResolveRuntimeStateAbility(
            CharacterAbilityRuntimeStateData abilityRuntimeState,
            out AbilityBase ability)
        {
            ability = null;

            if (abilityRuntimeState.formalGasAbilityCode > 0)
            {
                if (m_runtime.TryGetFormalGasAbilityInstance(
                        abilityRuntimeState.formalGasAbilityCode,
                        out ability))
                {
                    return true;
                }

                Debug.LogWarning($"Could not find ability instance matching EX-GAS Ability [{abilityRuntimeState.formalGasAbilityCode}].");
                return false;
            }

            return false;
        }

        private void Awake()
        {
            EnsureCharacterReference();
        }

        private void OnEnable()
        {
            EnsureCharacterReference();
        }

        private void Reset()
        {
            EnsureCharacterReference();
        }

        private void OnValidate()
        {
            EnsureCharacterReference();
        }

        private void EnsureCharacterReference()
        {
            if (m_character == null)
            {
                TryGetComponent(out m_character);
            }
        }

        private static void AddFormalGasAbilityCodes(List<int> codes, IEnumerable<int> formalGasAbilityCodes)
        {
            if (formalGasAbilityCodes == null)
            {
                return;
            }

            foreach (int formalGasAbilityCode in formalGasAbilityCodes)
            {
                AddFormalGasAbilityCode(codes, formalGasAbilityCode);
            }
        }

        private static void AddFormalGasAbilityCode(List<int> codes, int formalGasAbilityCode)
        {
            if (formalGasAbilityCode <= 0 || codes.Contains(formalGasAbilityCode))
            {
                return;
            }

            codes.Add(formalGasAbilityCode);
        }

        private bool TryGetResolvedEquippedActiveAbilityAtIndex(
            int index,
            out ActiveAbilityBase ability)
        {
            ability = null;

            int formalGasAbilityCode = m_equippedAbilityLoadout.GetFormalGasAbilityCode(index);
            if (formalGasAbilityCode > 0)
            {
                return TryGetResolvedFormalGasActiveAbility(formalGasAbilityCode, out ability);
            }

            return false;
        }

        private bool TryGetResolvedFormalGasActiveAbility(
            int formalGasAbilityCode,
            out ActiveAbilityBase ability)
        {
            ability = null;

            if (!m_runtime.TryGetFormalGasAbilityInstance(formalGasAbilityCode, out AbilityBase abilityBase) ||
                abilityBase is not ActiveAbilityBase activeAbility)
            {
                return false;
            }

            ability = activeAbility;
            return true;
        }

        private EAbilityFireCheckResult FireResolvedAbility(
            ActiveAbilityBase ability,
            GameCommandContext commandContext,
            AbilityActivationContext activationContext = null)
        {
            EAbilityFireCheckResult triggerAbilityCheckResult = ability.CanFire();

            if (triggerAbilityCheckResult == EAbilityFireCheckResult.Valid)
            {
                bool hasRequestedDirection = false;
                Vector2 requestedDirection = Vector2.zero;
                if (activationContext != null &&
                    activationContext.TryGetAimDirection(out Vector3 aimDirection))
                {
                    requestedDirection = new Vector2(aimDirection.x, aimDirection.y);
                    if (requestedDirection.sqrMagnitude > 0.0001f)
                    {
                        hasRequestedDirection = true;
                        requestedDirection.Normalize();
                        m_character.SetTargetDirection(requestedDirection);
                    }
                }

                if (ability.ShouldUpdateLookAtDirectionOnFireForRuntime())
                {
                    Vector2 targetDirection = m_character.GetTargetDirection();
                    if (hasRequestedDirection)
                    {
                        targetDirection = requestedDirection;
                    }

                    if (targetDirection.sqrMagnitude > 0.0001f)
                    {
                        m_character.SetLookAtDirection(targetDirection);
                    }
                }

                if (ability.UsesAutomaticRuntimeStateManagement())
                {
                    ability.gameObject.SetActive(true);
                    ability.Fire(
                        commandContext,
                        activationContext,
                        () => ability.gameObject.SetActive(false));
                }
                else
                {
                    ability.Fire(commandContext, activationContext, null);
                }
            }

            return triggerAbilityCheckResult;
        }

        private bool CanAssignFormalGasAbilityCode(int formalGasAbilityCode, int targetIndex = -1)
        {
            if (formalGasAbilityCode <= 0)
            {
                return false;
            }

            if (!TryGetResolvedFormalGasActiveAbility(formalGasAbilityCode, out _))
            {
                string targetSuffix = targetIndex >= 0 ? $" 槽位 {targetIndex}" : string.Empty;
                Debug.LogWarning($"[{nameof(CharacterAbilitySet)}] 试图将未拥有的 EX-GAS Ability {formalGasAbilityCode} 装入{targetSuffix}。", this);
                return false;
            }

            return true;
        }

        private void NotifyEquippedAbilitiesChanged()
        {
            m_equippedAbilitiesChanged.Invoke(CreateEquippedAbilitySlotViewSnapshot());
        }

    }
}

