using System;
using UnityEngine;
using UnityEngine.Events;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 装备操作结果。
    /// 背包、角色动作锁和装备槽规则都通过该结果向 UI 反馈失败原因。
    /// </summary>
    public enum EEquipmentOperationResult
    {
        Valid,
        NotEnoughHealth,
        NotEnoughMana,
        InvalidTarget,
        MissingItem,
        ActionLocked,
    }

    /// <summary>
    /// 角色 Actor 的持久化数据块。
    /// 在 CharacterBase 基础上追加经验、自由属性点、装备槽和快捷技能槽。
    /// </summary>
    [Serializable]
    public class CharacterActorDataBlock : CharacterBaseDataBlock
    {
        public int usedPoints;
        public int experience;
        public Stats customStats;
        public CharacterEquipmentSlotData[] equipmentSlots;
        public CharacterAbilitySlotData[] quickAbilitySlots;
    }

    /// <summary>
    /// 角色局部运行时快照。
    /// 玩家队伍、中立角色和敌对角色都使用同一份角色状态结构。
    /// </summary>
    [Serializable]
    public class CharacterActorRuntimeStateData : CharacterRuntimeStateData
    {
        public int usedPoints;
        public int experience;
        public Stats customStats;
        public CharacterEquipmentSlotData[] equipmentSlots;
        public CharacterAbilitySlotData[] quickAbilitySlots;
    }

    /// <summary>
    /// 装备槽存档条目。
    /// slotType 是槽位真相，equipment 是数据库引用，避免保存运行时装备实例。
    /// </summary>
    [Serializable]
    public class CharacterEquipmentSlotData
    {
        public EEquipmentType slotType;
        public DatabaseEntryReference<Equipment> equipment;
    }

    /// <summary>
    /// 快捷技能槽存档条目。
    /// 只保存正式 EX-GAS 能力编号，运行时能力实例由 CharacterAbilitySet 重建。
    /// </summary>
    [Serializable]
    public class CharacterAbilitySlotData
    {
        public int slotIndex;
        public int formalGasAbilityCode;
    }

    /// <summary>
    /// 可成长、可装备、可被队伍/AI 控制的正式角色实体。
    /// 它在 CharacterBase 基础上增加经验等级、自定义属性点、装备与快捷能力槽恢复。
    /// </summary>
    public partial class CharacterActor : CharacterBase
    {
        [Header("音频")]
        [InspectorName("升级音效")]
        [Tooltip("非静默升级时播放的音频配置。")]
        [SerializeField] private AudioClipResolver m_levelUpSound;

        [Header("表现")]
        [InspectorName("动画驱动组件")]
        [Tooltip("正式统一角色 Prefab 上的动画驱动。为空时回退到旧动画策略。")]
        [SerializeField] private MonoBehaviour m_animationDriverBehaviour;

        public int experience => m_experience;
        public int nextLevelExperience => GetTotalExpRequirement(m_level + 1);
        public int availablePoints => GetAvailablePoints(m_level, m_sheet.pointsPerLevel);
        public Stats customStats => CreateCustomStatsSnapshot();
        public int usedPoints => m_usedPoints;

        private Stats m_customStats = new();
        private int m_usedPoints = 0;
        private int m_experience = 0;
        private bool m_usesFormalDeathAnimation;

        public override void Revive()
        {
            base.Revive();
            if (m_usesFormalDeathAnimation)
            {
                m_usesFormalDeathAnimation = false;
                if (m_animationDriverBehaviour is ICharacterAnimationDriver animationDriver)
                {
                    animationDriver.ClearAnimationLock();
                    if (animationDriver.TryPlayDefaultAnimation())
                    {
                        return;
                    }
                }

                Debug.LogError(
                    $"角色“{name}”复活时无法通过正式动画驱动播放默认动作。"
                    + "请检查统一角色 Prefab 的动画驱动引用和默认动作配置。",
                    this);
                return;
            }

            m_animationStrategy?.Resume();
        }

        public int GetTotalExpRequirement(int level)
        {
            int total = 0;

            for (int i = 1; i < level; i++)
            {
                total += m_sheet.GetExperienceRequiredAtLevel(i);
            }

            return total;
        }

        public void AddExperience(int experience, bool silentMode = false)
        {
            Debug.Assert(experience > 0, "Cannot add a negative amount of experience.");
            GameRuntimeEvents.NotifyCharacterExperienceGained(this, experience);
            m_experience += experience;

            while (m_experience >= GetTotalExpRequirement(m_level + 1))
            {
                LevelUp(silentMode);
            }
        }

        public void AddCustomStats(Stats customStats)
        {
            m_customStats += customStats;
            RefreshResolvedStats();
        }

        public void LogUsedPoints(int points)
        {
            m_usedPoints += points;
        }

        protected override void InitializeStats()
        {
            RefreshResolvedStats();
        }

        internal void RefreshResolvedStats()
        {
            SetResolvedBaseStats(BuildResolvedStats());
        }

        internal override void RefreshResolvedStatsForEquipmentRuntime()
        {
            RefreshResolvedStats();
        }

        private Stats BuildResolvedStats()
        {
            return m_sheet.GetStatsAtLevel(m_level)
                + CreateCustomStatsSnapshot()
                + CreateEquipmentStatContributionSnapshot();
        }

        public override void LevelUp(bool silentMode = false)
        {
            base.LevelUp(silentMode);
            RefreshResolvedStats();

            if (!silentMode)
            {
                GameRuntimeEvents.NotifyCharacterLevelUp(this, m_level);
                GameRuntimeEvents.RequestAudioPlayback(m_levelUpSound);
            }
        }

        internal void SetLevel(int level)
        {
            int targetLevel = Mathf.Clamp(level, Constants.MinLevel, Constants.MaxLevel);
            if (targetLevel < m_level)
            {
                m_level = targetLevel;
                RefreshResolvedStats();
                return;
            }

            while (m_level < targetLevel)
            {
                LevelUp(silentMode: true);
            }

            RefreshResolvedStats();
        }

        protected override void OnDeath()
        {
            m_destroyOnDeath = false;
            base.OnDeath();
            if (!m_usesFormalDeathAnimation)
            {
                m_animationStrategy?.Pause();
            }

            GameManager.PlayerSystem.NotifyCharacterKilled(this);
        }

        protected override void UpdateMovementAnimation(Vector2 movement)
        {
            if (m_animationDriverBehaviour is ICharacterAnimationDriver animationDriver)
            {
                animationDriver.SetMovement(movement);
                return;
            }

            base.UpdateMovementAnimation(movement);
        }

        protected override bool TryPlayHitAnimation()
        {
            if (m_animationDriverBehaviour == null)
            {
                return base.TryPlayHitAnimation();
            }

            if (m_animationDriverBehaviour is ICharacterAnimationDriver animationDriver &&
                animationDriver.TryPlayDamageAnimation())
            {
                return true;
            }

            Debug.LogError(
                $"角色“{name}”无法通过正式动画驱动播放受击动作。"
                + "请检查统一角色 Prefab 的动画驱动引用、受击动作配置、动画数据库和 Animator 状态。",
                this);
            return false;
        }

        protected override bool TryPlayDeathAnimation()
        {
            if (m_animationDriverBehaviour == null)
            {
                return base.TryPlayDeathAnimation();
            }

            m_usesFormalDeathAnimation = true;
            if (m_animationDriverBehaviour is ICharacterAnimationDriver animationDriver &&
                animationDriver.TryLockDeathAnimation())
            {
                // 死亡是终止态：立即收口玩法逻辑，同时锁住非循环死亡动作，
                // 防止尚未结束的攻击 Cue 或普通待机同步覆盖尸体表现。
                return false;
            }

            Debug.LogError(
                $"角色“{name}”无法通过正式动画驱动播放死亡动作。"
                + "请检查统一角色 Prefab 的动画驱动引用、死亡动作配置、动画数据库和 Animator 状态。",
                this);
            return false;
        }

        protected override Type GetDataBlockType() => typeof(CharacterActorDataBlock);

        protected override void OnSave(PersistableDataBlock block)
        {
            base.OnSave(block);
            var actorBlock = block.As<CharacterActorDataBlock>();
            actorBlock.usedPoints = m_usedPoints;
            actorBlock.experience = m_experience;
            actorBlock.customStats = CreateCustomStatsSnapshot();
            actorBlock.equipmentSlots = CreateEquipmentSlotDataSnapshot(GameManager.Database);
            actorBlock.quickAbilitySlots = CreateEquippedAbilitySlotDataSnapshot(GameManager.Database);
        }

        protected override void OnLoad(PersistableDataBlock block)
        {
            var actorBlock = block.As<CharacterActorDataBlock>();
            m_usedPoints = actorBlock.usedPoints;
            m_customStats = actorBlock.customStats != null ? actorBlock.customStats.Clone() : new Stats();

            if (actorBlock.experience > 0)
            {
                AddExperience(actorBlock.experience, true);
            }

            RestoreEquipmentFromSlotData(
                actorBlock.equipmentSlots,
                reference => GameManager.Database.LoadFromReference(reference));

            RefreshResolvedStats();
            base.OnLoad(block); // CharacterBase 先恢复正式能力实例、等级与当前属性，再由 CharacterAbilitySet 恢复技能槽布局。
            RestoreEquippedAbilitiesFromSlotData(actorBlock.quickAbilitySlots);
        }

        internal CharacterActorRuntimeStateData CreateActorRuntimeState()
        {
            CharacterRuntimeStateData baseRuntimeState = CreateRuntimeState();
            return new CharacterActorRuntimeStateData
            {
                identifier = baseRuntimeState.identifier,
                state = baseRuntimeState.state,
                position = baseRuntimeState.position,
                rotation = baseRuntimeState.rotation,
                scale = baseRuntimeState.scale,
                lookAtDirection = baseRuntimeState.lookAtDirection,
                controllerData = baseRuntimeState.controllerData,
                level = baseRuntimeState.level,
                currentStats = baseRuntimeState.currentStats,
                activeAlterationRules = baseRuntimeState.activeAlterationRules,
                abilityRuntimeStates = baseRuntimeState.abilityRuntimeStates,
                abilitySources = baseRuntimeState.abilitySources,
                abilitySuppressions = baseRuntimeState.abilitySuppressions,
                usedPoints = m_usedPoints,
                experience = m_experience,
                customStats = CreateCustomStatsSnapshot(),
                equipmentSlots = CreateEquipmentSlotDataSnapshot(GameManager.Database),
                quickAbilitySlots = CreateEquippedAbilitySlotDataSnapshot(GameManager.Database)
            };
        }

        internal void LoadActorRuntimeState(CharacterActorRuntimeStateData runtimeState)
        {
            if (runtimeState == null)
            {
                return;
            }

            m_usedPoints = runtimeState.usedPoints;
            m_customStats = runtimeState.customStats != null ? runtimeState.customStats.Clone() : new Stats();

            if (runtimeState.experience > 0)
            {
                AddExperience(runtimeState.experience, true);
            }

            RestoreEquipmentFromSlotData(
                runtimeState.equipmentSlots,
                reference => GameManager.Database.LoadFromReference(reference));

            RefreshResolvedStats();
            LoadRuntimeState(runtimeState);
            RestoreEquippedAbilitiesFromSlotData(runtimeState.quickAbilitySlots);
        }

        private Stats CreateCustomStatsSnapshot()
        {
            return m_customStats.Clone();
        }

        private int GetAvailablePoints(int currentLevel, int pointsPerLevel)
        {
            return pointsPerLevel * (currentLevel - Constants.MinLevel) - m_usedPoints;
        }
    }
}
