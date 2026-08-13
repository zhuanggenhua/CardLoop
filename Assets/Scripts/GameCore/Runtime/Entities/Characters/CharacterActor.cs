using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 角色 Actor 的持久化数据块。
    /// 在 CharacterBase 基础上追加快捷技能槽。
    /// </summary>
    [Serializable]
    public class CharacterActorDataBlock : CharacterBaseDataBlock
    {
        public CharacterAbilitySlotData[] quickAbilitySlots;
    }

    /// <summary>
    /// 角色局部运行时快照。
    /// 玩家队伍、中立角色和敌对角色都使用同一份角色状态结构。
    /// </summary>
    [Serializable]
    public class CharacterActorRuntimeStateData : CharacterRuntimeStateData
    {
        public CharacterAbilitySlotData[] quickAbilitySlots;
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
    /// 可被队伍/AI 控制的正式角色实体。
    /// 它在 CharacterBase 基础上增加动画驱动和快捷能力槽恢复。
    /// </summary>
    public partial class CharacterActor : CharacterBase
    {
        [Header("表现")]
        [LabelText("动画驱动组件")]
        [Tooltip("正式统一角色 Prefab 上的动画驱动。为空时回退到旧动画策略。")]
        [SerializeField] private MonoBehaviour m_animationDriverBehaviour;

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

        internal void SetLevel(int level)
        {
            int targetLevel = Mathf.Clamp(level, Constants.MinLevel, Constants.MaxLevel);
            if (targetLevel < m_level)
            {
                m_level = targetLevel;
                return;
            }

            while (m_level < targetLevel)
            {
                LevelUp(silentMode: true);
            }

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
            actorBlock.quickAbilitySlots = CreateEquippedAbilitySlotDataSnapshot(GameManager.Database);
        }

        protected override void OnLoad(PersistableDataBlock block)
        {
            var actorBlock = block.As<CharacterActorDataBlock>();
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
                attributes = baseRuntimeState.attributes,
                activeAlterationRules = baseRuntimeState.activeAlterationRules,
                abilityRuntimeStates = baseRuntimeState.abilityRuntimeStates,
                abilitySources = baseRuntimeState.abilitySources,
                abilitySuppressions = baseRuntimeState.abilitySuppressions,
                quickAbilitySlots = CreateEquippedAbilitySlotDataSnapshot(GameManager.Database)
            };
        }

        internal void LoadActorRuntimeState(CharacterActorRuntimeStateData runtimeState)
        {
            if (runtimeState == null)
            {
                return;
            }

            LoadRuntimeState(runtimeState);
            RestoreEquippedAbilitiesFromSlotData(runtimeState.quickAbilitySlots);
        }
    }
}
