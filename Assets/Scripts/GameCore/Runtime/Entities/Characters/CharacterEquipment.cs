using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterBase))]
    public sealed class CharacterEquipment : MonoBehaviour
    {
        [Header("Equipment Ownership")]
        [SerializeField] private CharacterBase m_character = null;

        [Header("Initial Equipment")]
        [SerializeField] private Equipment[] m_initialEquipment = Array.Empty<Equipment>();

        private readonly CharacterEquippedItemLoadout m_equipmentLoadout = new();
        private readonly Dictionary<CharacterAbilitySourceKey, int> m_alterationEquipmentEffectSuppressions = new();

        public CharacterBase Character => m_character;
        public event Action EquipmentLoadoutChanged;

        public Stats CreateStatContributionSnapshot()
        {
            return BuildEquipmentStatContribution();
        }

        public CharacterEquipmentSlotData[] CreateSlotDataSnapshot(DatabaseRegistry databaseRegistry)
        {
            return m_equipmentLoadout.CreateSlotDataSnapshot(databaseRegistry);
        }

        public bool RestoreFromSlotData(
            IEnumerable<CharacterEquipmentSlotData> equipmentSlots,
            Func<DatabaseEntryReference<Equipment>, Equipment> resolveEquipment)
        {
            bool restored = m_equipmentLoadout.RestoreFromSlotData(
                equipmentSlots,
                resolveEquipment,
                item => Equip(item, autoUpdateStats: false));
            if (restored)
            {
                RefreshCharacterStats();
                EquipmentLoadoutChanged?.Invoke();
            }

            return restored;
        }

        public EEquipmentOperationResult TryEquip(Equipment equipment, out Equipment previousEquipment)
        {
            previousEquipment = null;
            if (!equipment)
            {
                return EEquipmentOperationResult.InvalidTarget;
            }

            return TryApplyEquipmentSlotChange(equipment.type, equipment, out previousEquipment);
        }

        public EEquipmentOperationResult TryUnequip(EEquipmentType type, out Equipment previousEquipment)
        {
            return TryApplyEquipmentSlotChange(type, null, out previousEquipment);
        }

        public bool TryGetEquipment(EEquipmentType type, out Equipment equipment)
        {
            return m_equipmentLoadout.TryGet(type, out equipment);
        }

        public Equipment[] GetEquippedItems()
        {
            return m_equipmentLoadout.SnapshotItems();
        }

        internal Equipment[] ForceUnequipAllEquipmentForLifecycle()
        {
            Equipment[] equippedItems = GetEquippedItems();
            if (equippedItems.Length == 0)
            {
                return Array.Empty<Equipment>();
            }

            List<Equipment> removedEquipment = new(equippedItems.Length);
            foreach (Equipment equipment in equippedItems)
            {
                if (!equipment)
                {
                    continue;
                }

                Equipment previousEquipment = ApplyEquipmentSlotChange(
                    CreateEquipmentSlotChange(equipment.type, null),
                    autoUpdateStats: false);

                if (previousEquipment)
                {
                    removedEquipment.Add(previousEquipment);
                }
            }

            RefreshCharacterStats();
            return removedEquipment.ToArray();
        }

        public void ApplyAlterationEquipmentEffectSuppressionRule(CharacterAbilitySourceKey source)
        {
            m_alterationEquipmentEffectSuppressions.TryGetValue(source, out int currentStackCount);
            m_alterationEquipmentEffectSuppressions[source] = currentStackCount + 1;
            ApplyEquipmentBonusFormalGasAbilitySuppressions(GetEquippedEquipmentBonusFormalGasAbilityCodeSnapshot(), source, 1);
            RefreshCharacterStats();
        }

        public void RemoveAlterationEquipmentEffectSuppressionRuleStack(CharacterAbilitySourceKey source)
        {
            if (!m_alterationEquipmentEffectSuppressions.TryGetValue(source, out int currentStackCount))
            {
                return;
            }

            RemoveEquipmentBonusFormalGasAbilitySuppressions(GetEquippedEquipmentBonusFormalGasAbilityCodeSnapshot(), source, 1);
            int nextStackCount = currentStackCount - 1;
            if (nextStackCount <= 0)
            {
                m_alterationEquipmentEffectSuppressions.Remove(source);
            }
            else
            {
                m_alterationEquipmentEffectSuppressions[source] = nextStackCount;
            }

            RefreshCharacterStats();
        }

        public void RemoveAllAlterationEquipmentEffectSuppressionRules(CharacterAbilitySourceKey source)
        {
            if (!m_alterationEquipmentEffectSuppressions.TryGetValue(source, out int currentStackCount))
            {
                return;
            }

            RemoveEquipmentBonusFormalGasAbilitySuppressions(GetEquippedEquipmentBonusFormalGasAbilityCodeSnapshot(), source, currentStackCount);
            m_alterationEquipmentEffectSuppressions.Remove(source);
            RefreshCharacterStats();
        }

        internal void ClearAlterationEquipmentEffectSuppressionRules()
        {
            if (m_alterationEquipmentEffectSuppressions.Count == 0)
            {
                return;
            }

            int[] currentEquipmentFormalGasAbilityCodes = GetEquippedEquipmentBonusFormalGasAbilityCodeSnapshot();
            foreach ((CharacterAbilitySourceKey source, int stackCount) in m_alterationEquipmentEffectSuppressions)
            {
                RemoveEquipmentBonusFormalGasAbilitySuppressions(currentEquipmentFormalGasAbilityCodes, source, stackCount);
            }

            m_alterationEquipmentEffectSuppressions.Clear();
            RefreshCharacterStats();
        }

        private Equipment Equip(Equipment equipment, bool autoUpdateStats = true)
        {
            return ApplyEquipmentSlotChange(CreateEquipmentSlotChange(equipment.type, equipment), autoUpdateStats);
        }

        private Stats BuildEquipmentStatContribution()
        {
            if (HasAlterationEquipmentEffectSuppression())
            {
                return new Stats();
            }

            return m_equipmentLoadout.BuildStatContribution();
        }

        private EEquipmentOperationResult TryApplyEquipmentSlotChange(
            EEquipmentType type,
            Equipment nextEquipment,
            out Equipment previousEquipment)
        {
            previousEquipment = null;
            if (!m_character)
            {
                return EEquipmentOperationResult.InvalidTarget;
            }

            if (!m_character.Can(EActionFlags.ChangeEquipment))
            {
                return EEquipmentOperationResult.ActionLocked;
            }

            CharacterEquipmentSlotChange change = CreateEquipmentSlotChange(type, nextEquipment);
            Stats effectiveStatDelta = HasAlterationEquipmentEffectSuppression() ? new Stats() : change.StatDelta;
            EEquipmentOperationResult result = MapEquipmentValidationResult(
                m_character.ValidateCurrentResourceDelta(effectiveStatDelta, minimumHealth: 1));

            if (result == EEquipmentOperationResult.Valid)
            {
                previousEquipment = ApplyEquipmentSlotChange(change, autoUpdateStats: true);
            }

            return result;
        }

        private CharacterEquipmentSlotChange CreateEquipmentSlotChange(EEquipmentType type, Equipment nextEquipment)
        {
            m_equipmentLoadout.TryGet(type, out Equipment currentEquipment);
            Stats statDelta = new();

            if (currentEquipment)
            {
                statDelta -= currentEquipment.CreateBonusStatsSnapshot();
            }

            if (nextEquipment)
            {
                statDelta += nextEquipment.CreateBonusStatsSnapshot();
            }

            return new CharacterEquipmentSlotChange(
                type,
                currentEquipment,
                nextEquipment,
                statDelta,
                GetEquipmentBonusFormalGasAbilityCodesSnapshot(currentEquipment),
                GetEquipmentBonusFormalGasAbilityCodesSnapshot(nextEquipment));
        }

        private Equipment ApplyEquipmentSlotChange(CharacterEquipmentSlotChange change, bool autoUpdateStats)
        {
            bool hasPreviousSource = TryPrepareEquipmentAbilitySource(
                change.PreviousEquipment,
                change.RemovedFormalGasAbilityCodes,
                out CharacterAbilitySourceKey previousSource);
            bool hasNextSource = TryPrepareEquipmentAbilitySource(
                change.NextEquipment,
                change.AddedFormalGasAbilityCodes,
                out CharacterAbilitySourceKey nextSource);

            RemoveEquipmentBonusFormalGasAbilitySuppressions(change.RemovedFormalGasAbilityCodes);
            if (hasPreviousSource)
            {
                ApplyEquipmentBonusFormalGasAbilityCodes(
                    change.RemovedFormalGasAbilityCodes,
                    code => m_character.RemoveBonusFormalGasAbility(code, previousSource));
            }

            m_equipmentLoadout.Set(change.SlotType, change.NextEquipment);
            if (hasNextSource)
            {
                ApplyEquipmentBonusFormalGasAbilityCodes(
                    change.AddedFormalGasAbilityCodes,
                    code => m_character.AddBonusFormalGasAbility(code, nextSource));
            }

            ApplyEquipmentBonusFormalGasAbilitySuppressions(change.AddedFormalGasAbilityCodes);

            if (autoUpdateStats)
            {
                RefreshCharacterStats();
            }

            EquipmentLoadoutChanged?.Invoke();
            return change.PreviousEquipment;
        }

        private static bool TryPrepareEquipmentAbilitySource(
            Equipment equipment,
            int[] formalGasAbilityCodes,
            out CharacterAbilitySourceKey source)
        {
            source = default;
            if (formalGasAbilityCodes == null || formalGasAbilityCodes.Length == 0)
            {
                return false;
            }

            if (!equipment)
            {
                throw new InvalidOperationException(
                    $"[{nameof(CharacterEquipment)}] 装备附加能力需要有效装备来源，不能在缺少装备资产时变更能力来源。");
            }

            if (!GameManager.Database.TryCreateReference(equipment, out DatabaseEntryReference<Equipment> reference))
            {
                throw new InvalidOperationException(
                    $"[{nameof(CharacterEquipment)}] 装备 {equipment.name} 未登记到 DatabaseRegistry，不能在装备状态改变后再丢失附加能力来源。");
            }

            source = new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Equipment, reference.guid);
            return true;
        }

        private static int[] GetEquipmentBonusFormalGasAbilityCodesSnapshot(Equipment equipment)
        {
            return equipment ? equipment.GetBonusFormalGasAbilityCodes() : Array.Empty<int>();
        }

        private int[] GetEquippedEquipmentBonusFormalGasAbilityCodeSnapshot()
        {
            return GetEquippedItems()
                .SelectMany(GetEquipmentBonusFormalGasAbilityCodesSnapshot)
                .Where(code => code > 0)
                .Distinct()
                .ToArray();
        }

        private bool HasAlterationEquipmentEffectSuppression()
        {
            foreach (int stackCount in m_alterationEquipmentEffectSuppressions.Values)
            {
                if (stackCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyEquipmentBonusFormalGasAbilitySuppressions(IEnumerable<int> formalGasAbilityCodes)
        {
            foreach ((CharacterAbilitySourceKey source, int stackCount) in m_alterationEquipmentEffectSuppressions)
            {
                ApplyEquipmentBonusFormalGasAbilitySuppressions(formalGasAbilityCodes, source, stackCount);
            }
        }

        private void ApplyEquipmentBonusFormalGasAbilitySuppressions(
            IEnumerable<int> formalGasAbilityCodes,
            CharacterAbilitySourceKey source,
            int count)
        {
            ApplyEquipmentBonusFormalGasAbilityCodes(
                formalGasAbilityCodes,
                code => m_character.AddSourcedFormalGasAbilitySuppression(code, source, count));
        }

        private void RemoveEquipmentBonusFormalGasAbilitySuppressions(IEnumerable<int> formalGasAbilityCodes)
        {
            foreach ((CharacterAbilitySourceKey source, int stackCount) in m_alterationEquipmentEffectSuppressions)
            {
                RemoveEquipmentBonusFormalGasAbilitySuppressions(formalGasAbilityCodes, source, stackCount);
            }
        }

        private void RemoveEquipmentBonusFormalGasAbilitySuppressions(
            IEnumerable<int> formalGasAbilityCodes,
            CharacterAbilitySourceKey source,
            int count)
        {
            ApplyEquipmentBonusFormalGasAbilityCodes(
                formalGasAbilityCodes,
                code => m_character.RemoveSourcedFormalGasAbilitySuppression(code, source, count));
        }

        private static void ApplyEquipmentBonusFormalGasAbilityCodes(IEnumerable<int> formalGasAbilityCodes, Action<int> applyAbility)
        {
            foreach (int formalGasAbilityCode in formalGasAbilityCodes)
            {
                if (formalGasAbilityCode > 0)
                {
                    applyAbility(formalGasAbilityCode);
                }
            }
        }

        private static EEquipmentOperationResult MapEquipmentValidationResult(EResourceValidationResult validationResult)
        {
            return validationResult switch
            {
                EResourceValidationResult.HealthBelowMinimum => EEquipmentOperationResult.NotEnoughHealth,
                EResourceValidationResult.ManaBelowMinimum => EEquipmentOperationResult.NotEnoughMana,
                _ => EEquipmentOperationResult.Valid
            };
        }

        private void RefreshCharacterStats()
        {
            m_character?.RefreshResolvedStatsForEquipmentRuntime();
        }

        private void Awake()
        {
            EnsureCharacterReference();
            ApplyInitialEquipment();
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

        private void ApplyInitialEquipment()
        {
            if (m_initialEquipment == null || m_initialEquipment.Length == 0)
            {
                return;
            }

            foreach (Equipment equipment in m_initialEquipment)
            {
                if (!equipment)
                {
                    continue;
                }

                Equip(equipment, autoUpdateStats: false);
            }

            RefreshCharacterStats();
            EquipmentLoadoutChanged?.Invoke();
        }
    }
}
