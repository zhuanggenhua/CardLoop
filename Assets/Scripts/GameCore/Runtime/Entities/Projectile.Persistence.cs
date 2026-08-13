using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 投射物完整持久化存档块，保存飞行状态、来源、命中效果和爆炸规则。
    /// </summary>
    [Serializable]
    public class ProjectileDataBlock : EntityDataBlock
    {
        /// <summary>
        /// 当前飞行方向。
        /// </summary>
        public Vector2 direction;

        /// <summary>
        /// 当前飞行速度。
        /// </summary>
        public float speed;

        /// <summary>
        /// 剩余生命周期秒数。
        /// </summary>
        public float remainingLifetime;

        /// <summary>
        /// 投射物是否仍在运行。
        /// </summary>
        public bool operating;

        /// <summary>
        /// 发射来源角色，用于伤害归因和命令上下文恢复。
        /// </summary>
        public PersistableReference<CharacterBase> source;

        /// <summary>
        /// 发射命令的来源类型。
        /// </summary>
        public EGameCommandIssuerKind fireCommandIssuerKind;

        /// <summary>
        /// 发射命令的来源标识。
        /// </summary>
        public string fireCommandIssuerId;

        /// <summary>
        /// 投射物命中时施加的 EX-GAS GameplayEffect ID。
        /// </summary>
        public int impactGameplayEffectId;

        /// <summary>
        /// 命中终止时的爆炸半径；0 表示不做范围扩散。
        /// </summary>
        public float explosionRadius;

        /// <summary>
        /// 爆炸范围内是否应用基础伤害。
        /// </summary>
        public bool explosionApplyImpactEffect;

        /// <summary>
        /// 爆炸范围应用基础伤害时是否跳过主命中目标。
        /// </summary>
        public bool explosionImpactEffectIgnorePrimaryTarget;
    }

    /// <summary>
    /// 飞行物局部运行时快照。
    /// 它只服务能力 extra state 恢复，不再夹带持久化系统专用的 info 字段。
    /// </summary>
    [Serializable]
    public class ProjectileRuntimeStateData
    {
        public EPersistableObjectState state;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector2 direction;
        public float speed;
        public float remainingLifetime;
        public bool operating;
        public PersistableReference<CharacterBase> source;
        public EGameCommandIssuerKind fireCommandIssuerKind;
        public string fireCommandIssuerId;
        public int impactGameplayEffectId;
        public float explosionRadius;
        public bool explosionApplyImpactEffect;
        public bool explosionImpactEffectIgnorePrimaryTarget;
    }

    /// <summary>
    /// 投射物持久化相关的 partial，实现完整存档和能力运行态快照的互转。
    /// </summary>
    public partial class Projectile
    {
        protected override Type GetDataBlockType() => typeof(ProjectileDataBlock);

        protected override void OnSave(PersistableDataBlock block)
        {
            base.OnSave(block);

            ProjectileDataBlock projectileBlock = block.As<ProjectileDataBlock>();
            projectileBlock.direction = m_direction;
            projectileBlock.speed = m_speed;
            projectileBlock.remainingLifetime = m_remainingLifetime;
            projectileBlock.operating = m_operating;
            projectileBlock.source = m_source;
            projectileBlock.fireCommandIssuerKind = m_fireCommandContext.IssuerKind;
            projectileBlock.fireCommandIssuerId = m_fireCommandContext.IssuerId;
            projectileBlock.impactGameplayEffectId = m_impactGameplayEffectId;
            projectileBlock.explosionRadius = m_explosionRadius;
            projectileBlock.explosionApplyImpactEffect = m_explosionApplyImpactEffect;
            projectileBlock.explosionImpactEffectIgnorePrimaryTarget = m_explosionImpactEffectIgnorePrimaryTarget;
        }

        protected override void OnLoad(PersistableDataBlock block)
        {
            base.OnLoad(block);

            ProjectileDataBlock projectileBlock = block.As<ProjectileDataBlock>();
            m_direction = projectileBlock.direction;
            m_speed = projectileBlock.speed;
            m_remainingLifetime = projectileBlock.remainingLifetime;
            m_operating = projectileBlock.operating;
            m_source = projectileBlock.source.ResolveOrNull();
            m_fireCommandContext = GameCommandContext.Recreate(projectileBlock.fireCommandIssuerKind, m_source, projectileBlock.fireCommandIssuerId);
            m_impactGameplayEffectId = ProjectileLaunchParameters.RequireGameplayEffectId(
                projectileBlock.impactGameplayEffectId);
            m_explosionRadius = projectileBlock.explosionRadius;
            m_explosionApplyImpactEffect = projectileBlock.explosionApplyImpactEffect;
            m_explosionImpactEffectIgnorePrimaryTarget = projectileBlock.explosionImpactEffectIgnorePrimaryTarget;
        }

        internal ProjectileRuntimeStateData CreateRuntimeState()
        {
            return new ProjectileRuntimeStateData
            {
                state = CapturePersistableState(),
                position = transform.position,
                rotation = transform.rotation,
                scale = transform.localScale,
                direction = m_direction,
                speed = m_speed,
                remainingLifetime = m_remainingLifetime,
                operating = m_operating,
                source = m_source,
                fireCommandIssuerKind = m_fireCommandContext.IssuerKind,
                fireCommandIssuerId = m_fireCommandContext.IssuerId,
                impactGameplayEffectId = m_impactGameplayEffectId,
                explosionRadius = m_explosionRadius,
                explosionApplyImpactEffect = m_explosionApplyImpactEffect,
                explosionImpactEffectIgnorePrimaryTarget = m_explosionImpactEffectIgnorePrimaryTarget
            };
        }

        internal void LoadRuntimeState(ProjectileRuntimeStateData runtimeState)
        {
            if (runtimeState == null || !ApplyPersistableState(runtimeState.state))
            {
                return;
            }

            transform.position = runtimeState.position;
            transform.rotation = runtimeState.rotation;
            transform.localScale = runtimeState.scale;
            m_direction = runtimeState.direction;
            m_speed = runtimeState.speed;
            m_remainingLifetime = runtimeState.remainingLifetime;
            m_operating = runtimeState.operating;
            m_source = runtimeState.source.ResolveOrNull();
            m_fireCommandContext = GameCommandContext.Recreate(runtimeState.fireCommandIssuerKind, m_source, runtimeState.fireCommandIssuerId);
            m_impactGameplayEffectId = ProjectileLaunchParameters.RequireGameplayEffectId(
                runtimeState.impactGameplayEffectId);
            m_explosionRadius = runtimeState.explosionRadius;
            m_explosionApplyImpactEffect = runtimeState.explosionApplyImpactEffect;
            m_explosionImpactEffectIgnorePrimaryTarget = runtimeState.explosionImpactEffectIgnorePrimaryTarget;
        }
    }
}
