using System;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 受击反馈触发时，提供给镜头或屏幕表现层的只读上下文。
    /// 这让相机表现继续走正式反馈闭包，而不是直接从全局通知系统里自行推断业务语义。
    /// </summary>
    public readonly struct DamageTakenFeedbackContext
    {
        public readonly Vector3 position;
        public readonly CharacterBase target;
        public readonly CharacterBase sourceCharacter;
        public readonly DamageInputDescriptor damageInput;
        public readonly EEffectVisualFlags visualFlags;

        public DamageTakenFeedbackContext(
            Vector3 position,
            CharacterBase target,
            CharacterBase sourceCharacter,
            DamageInputDescriptor damageInput,
            EEffectVisualFlags visualFlags)
        {
            this.position = position;
            this.target = target;
            this.sourceCharacter = sourceCharacter;
            this.damageInput = damageInput;
            this.visualFlags = visualFlags;
        }
    }

    /// <summary>
    /// 数值型表现事件上下文。
    /// 用于治疗、法力消耗和法力恢复这类只影响前端表现的广播，不承担游戏规则推进。
    /// </summary>
    public readonly struct CharacterValuePresentationContext
    {
        public readonly Vector3 position;
        public readonly CharacterBase target;
        public readonly int value;
        public readonly EEffectVisualFlags visualFlags;

        public CharacterValuePresentationContext(Vector3 position, CharacterBase target, int value, EEffectVisualFlags visualFlags)
        {
            this.position = position;
            this.target = target;
            this.value = value;
            this.visualFlags = visualFlags;
        }
    }

    /// <summary>
    /// 死亡表现事件上下文。
    /// 只服务纯表现层的镜头、屏幕、音效和事件日志，不承载死亡结算本身。
    /// </summary>
    public readonly struct DeathPresentationContext
    {
        public readonly Vector3 position;
        public readonly CharacterBase target;
        public readonly CharacterBase sourceCharacter;

        public DeathPresentationContext(Vector3 position, CharacterBase target, CharacterBase sourceCharacter)
        {
            this.position = position;
            this.target = target;
            this.sourceCharacter = sourceCharacter;
        }
    }

    /// <summary>
    /// 交互执行表现事件上下文。
    /// 只服务纯表现层监听者，不承载交互命令和结果判断。
    /// </summary>
    public readonly struct InteractionPresentationContext
    {
        public readonly Vector3 position;
        public readonly CharacterBase sender;
        public readonly Entity entity;
        public readonly bool executed;

        public InteractionPresentationContext(Vector3 position, CharacterBase sender, Entity entity, bool executed)
        {
            this.position = position;
            this.sender = sender;
            this.entity = entity;
            this.executed = executed;
        }
    }

    /// <summary>
    /// 能力和本地输入门控层的表现反馈闭包。
    /// 这里是 GameCore 允许直接持有 MMFeedbacks 的边界；业务规则仍由 RPG Ability/Effect/Stats 结算，不能让 TopDown 的生命、输入或 Manager 接管真相。
    /// </summary>
    [Serializable]
    public sealed class GameplayFeedbackSet
    {
        [Header("能力反馈")]
        [Tooltip("能力正式开始执行时播放。用于动作/音效/镜头表现，不改变能力冷却、消耗或伤害结果。")]
        [SerializeField] private MMFeedbacks m_abilityStartFeedbacks = null;

        [Tooltip("能力被停止或释放结束时播放。只承担表现收口，不负责清理能力状态机。")]
        [SerializeField] private MMFeedbacks m_abilityStopFeedbacks = null;

        [Header("本地输入门控反馈")]
        [Tooltip("本地输入门控进入出手流程时播放。用于前摇或举武器表现，不决定是否命中。")]
        [SerializeField] private MMFeedbacks m_weaponStartFeedbacks = null;

        [Tooltip("武器完成一次真实使用时播放。命中、伤害和击退仍回到 GameCore 战斗链路。")]
        [SerializeField] private MMFeedbacks m_weaponUseFeedbacks = null;

        [Tooltip("武器停止开火或退出使用状态时播放。")]
        [SerializeField] private MMFeedbacks m_weaponStopFeedbacks = null;

        [Tooltip("武器需要换弹但尚未开始换弹时播放。")]
        [SerializeField] private MMFeedbacks m_reloadNeededFeedbacks = null;

        [Tooltip("换弹流程开始时播放。")]
        [SerializeField] private MMFeedbacks m_reloadStartFeedbacks = null;

        [Tooltip("换弹流程完成时播放。")]
        [SerializeField] private MMFeedbacks m_reloadCompleteFeedbacks = null;

        [Tooltip("能力或武器流程被打断时播放。")]
        [SerializeField] private MMFeedbacks m_interruptedFeedbacks = null;

        [Header("命中反馈")]
        [Tooltip("攻击命中可受伤角色并成功产生效果时播放。对齐 TopDown DamageOnTouch 的 HitDamageableFeedback。")]
        [SerializeField] private MMFeedbacks m_hitDamageableFeedbacks = null;

        [Tooltip("攻击命中碰撞体但没有可结算角色目标时播放。对齐 TopDown DamageOnTouch 的 HitNonDamageableFeedback。")]
        [SerializeField] private MMFeedbacks m_hitNonDamageableFeedbacks = null;

        [Tooltip("攻击命中任意碰撞体时播放。对齐 TopDown DamageOnTouch 的 HitAnythingFeedback。")]
        [SerializeField] private MMFeedbacks m_hitAnythingFeedbacks = null;

        [Header("生命反馈")]
        [Tooltip("角色实际受到伤害时播放。对齐 TopDown Health 的 DamageMMFeedbacks。")]
        [SerializeField] private MMFeedbacks m_damageTakenFeedbacks = null;

        [Tooltip("角色死亡时播放。对齐 TopDown Health 的 DeathMMFeedbacks。")]
        [SerializeField] private MMFeedbacks m_deathFeedbacks = null;

        [Header("交互反馈")]
        [Tooltip("交互成功执行时播放。对齐 TopDown ButtonActivated 的 ActivationFeedback。")]
        [SerializeField] private MMFeedbacks m_interactionActivationFeedbacks = null;

        [Tooltip("交互对象拒绝执行或没有可执行交互时播放。对齐 TopDown ButtonActivated 的 DeniedFeedback。")]
        [SerializeField] private MMFeedbacks m_interactionDeniedFeedbacks = null;

        public void PlayAbilityStart(Vector3 position) => Play(m_abilityStartFeedbacks, position);
        public void PlayAbilityStop(Vector3 position) => Play(m_abilityStopFeedbacks, position);
        public void PlayWeaponStart(Vector3 position) => Play(m_weaponStartFeedbacks, position);
        public void PlayWeaponUse(Vector3 position) => Play(m_weaponUseFeedbacks, position);
        public void PlayWeaponStop(Vector3 position) => Play(m_weaponStopFeedbacks, position);
        public void PlayReloadNeeded(Vector3 position) => Play(m_reloadNeededFeedbacks, position);
        public void PlayReloadStart(Vector3 position) => Play(m_reloadStartFeedbacks, position);
        public void PlayReloadComplete(Vector3 position) => Play(m_reloadCompleteFeedbacks, position);
        public void PlayInterrupted(Vector3 position) => Play(m_interruptedFeedbacks, position);
        public void PlayHitDamageable(Vector3 position) => Play(m_hitDamageableFeedbacks, position);
        public void PlayHitNonDamageable(Vector3 position) => Play(m_hitNonDamageableFeedbacks, position);
        public void PlayHitAnything(Vector3 position) => Play(m_hitAnythingFeedbacks, position);
        public void PlayDamageTaken(Vector3 position) => Play(m_damageTakenFeedbacks, position);

        /// <summary>
        /// 播放受击反馈，并把只读受击上下文广播给纯表现监听者。
        /// 这样相机震动之类的表现可以沿正式反馈闭包收口，而不是直接监听全局伤害通知。
        /// </summary>
        public void PlayDamageTaken(
            Vector3 position,
            CharacterBase target,
            DamageInputDescriptor damageInput,
            EEffectVisualFlags visualFlags)
        {
            PlayDamageTaken(position);

            damageInput.TryGetSourceCharacter(out CharacterBase sourceCharacter);
            YokiFrame.EventKit.Type.Send(new DamageTakenPresentationEvent(new DamageTakenFeedbackContext(position, target, sourceCharacter, damageInput, visualFlags)));
        }

        public void PlayDeath(Vector3 position) => Play(m_deathFeedbacks, position);
        public void PlayInteractionActivation(Vector3 position) => Play(m_interactionActivationFeedbacks, position);
        public void PlayInteractionDenied(Vector3 position) => Play(m_interactionDeniedFeedbacks, position);

        public bool HasFeedback(EGameCoreFeedbackCueKind kind)
        {
            return kind switch
            {
                EGameCoreFeedbackCueKind.AbilityStart => m_abilityStartFeedbacks != null,
                EGameCoreFeedbackCueKind.AbilityStop => m_abilityStopFeedbacks != null,
                EGameCoreFeedbackCueKind.WeaponStart => m_weaponStartFeedbacks != null,
                EGameCoreFeedbackCueKind.WeaponUse => m_weaponUseFeedbacks != null,
                EGameCoreFeedbackCueKind.WeaponStop => m_weaponStopFeedbacks != null,
                EGameCoreFeedbackCueKind.Interrupted => m_interruptedFeedbacks != null,
                EGameCoreFeedbackCueKind.HitDamageable => m_hitDamageableFeedbacks != null,
                EGameCoreFeedbackCueKind.HitNonDamageable => m_hitNonDamageableFeedbacks != null,
                EGameCoreFeedbackCueKind.HitAnything => m_hitAnythingFeedbacks != null,
                EGameCoreFeedbackCueKind.DamageTaken => m_damageTakenFeedbacks != null,
                EGameCoreFeedbackCueKind.Death => m_deathFeedbacks != null,
                EGameCoreFeedbackCueKind.ReloadNeeded => m_reloadNeededFeedbacks != null,
                EGameCoreFeedbackCueKind.ReloadStart => m_reloadStartFeedbacks != null,
                EGameCoreFeedbackCueKind.ReloadComplete => m_reloadCompleteFeedbacks != null,
                _ => false
            };
        }

        private static void Play(MMFeedbacks feedbacks, Vector3 position)
        {
            feedbacks?.PlayFeedbacks(position);
        }
    }
}
