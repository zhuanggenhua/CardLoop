using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 投射物发射参数，包含基础伤害、飞行速度和可选爆炸范围规则。
    /// </summary>
    public readonly struct ProjectileLaunchParameters
    {
        public ProjectileLaunchParameters(
            FormalDamageEffectPayload baseDamage,
            float speed,
            float explosionRadius = 0.0f,
            bool explosionApplyBaseDamage = true,
            bool explosionBaseDamageIgnorePrimaryTarget = true)
        {
            BaseDamage = baseDamage;
            Speed = speed;
            ExplosionRadius = explosionRadius;
            ExplosionApplyBaseDamage = explosionApplyBaseDamage;
            ExplosionBaseDamageIgnorePrimaryTarget = explosionBaseDamageIgnorePrimaryTarget;
        }

        public FormalDamageEffectPayload BaseDamage { get; }
        public float Speed { get; }
        public float ExplosionRadius { get; }
        public bool ExplosionApplyBaseDamage { get; }
        public bool ExplosionBaseDamageIgnorePrimaryTarget { get; }
    }

    /// <summary>
    /// 可持久化投射物实体，负责飞行、寿命结束、碰撞终止和可选爆炸伤害。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public partial class Projectile : Entity
    {
        [Header("引用")]
        [InspectorName("刚体")]
        [Tooltip("投射物飞行速度写入的 Rigidbody2D。")]
        [SerializeField] private Rigidbody2D m_rigidbody = null;

        [InspectorName("动画器")]
        [Tooltip("投射物销毁前可选播放销毁动画。")]
        [SerializeField] private Animator m_animator = null;

        [Header("设置")]
        [InspectorName("反向旋转")]
        [Tooltip("开启后，投射物朝向会使用飞行方向的反方向，适配默认朝向相反的美术资源。")]
        [SerializeField] private bool m_reverseRotation = false;

        [InspectorName("最长存在时间")]
        [Tooltip("投射物未命中任何目标时的最大飞行时长，超时后会终止。")]
        [SerializeField] private float m_maxDuration = 2.0f;

        [Header("动画参数")]
        [InspectorName("销毁动画参数")]
        [Tooltip("Animator 中触发销毁动画的 Trigger 参数名。缺少该参数时会直接销毁。")]
        [SerializeField] private string m_destroyAnimationParameter = "destroy";

        [Header("音频")]
        [InspectorName("碰撞音效")]
        [Tooltip("投射物命中或终止时播放的音效解析器。")]
        [SerializeField] private AudioClipResolver m_collisionSound;

        private CharacterBase m_source;
        private FormalDamageEffectPayload m_baseDamage;
        private float m_explosionRadius;
        private bool m_explosionApplyBaseDamage;
        private bool m_explosionBaseDamageIgnorePrimaryTarget;
        private GameCommandContext m_fireCommandContext = GameCommandContext.Script();
        private Vector2 m_direction;
        private float m_speed;
        private float m_remainingLifetime;
        private bool m_hasDestroyAnimation;
        private bool m_operating = false;

        internal bool shouldPersistRuntimeState => m_operating && m_remainingLifetime > 0.0f;

        protected override void Awake()
        {
            base.Awake();

            m_hasDestroyAnimation = m_animator && AnimationUtils.HasParameter(m_animator, m_destroyAnimationParameter);
        }

        /// <summary>
        /// 初始化并发射投射物；命令上下文会绑定到来源角色，便于后续伤害归因。
        /// </summary>
        public void Throw(CharacterBase source, Vector2 direction, ProjectileLaunchParameters parameters, GameCommandContext commandContext)
        {
            Debug.Assert(parameters.Speed > 0.0f, $"{name} 缺少有效投射物速度，无法初始化投射物执行参数。");
            m_source = source;
            m_fireCommandContext = commandContext.HasActor
                ? commandContext
                : GameCommandContext.Recreate(commandContext.IssuerKind, source, commandContext.IssuerId);

            m_baseDamage = parameters.BaseDamage;
            m_direction = direction;
            m_speed = parameters.Speed;
            m_remainingLifetime = m_maxDuration;
            m_explosionRadius = parameters.ExplosionRadius;
            m_explosionApplyBaseDamage = parameters.ExplosionApplyBaseDamage;
            m_explosionBaseDamageIgnorePrimaryTarget = parameters.ExplosionBaseDamageIgnorePrimaryTarget;
            m_operating = true;
        }

        /// <summary>
        /// 销毁动画结束事件入口，动画帧调用后正式销毁投射物。
        /// </summary>
        public void OnDestroyAnimationEnd()
        {
            Destroy(ResolveDestroyCommandContext());
        }

        private GameCommandContext ResolveDestroyCommandContext()
        {
            if (!m_source)
            {
                return m_fireCommandContext;
            }

            return GameCommandContext.Recreate(m_fireCommandContext.IssuerKind, m_source, m_fireCommandContext.IssuerId);
        }

        private void Terminate(CharacterBase primaryTarget = null)
        {
            if (m_operating)
            {
                m_operating = false;
                m_rigidbody.linearVelocity = Vector3.zero;
                HandleExplosion(primaryTarget);

                if (m_hasDestroyAnimation)
                {
                    m_animator?.SetTrigger(m_destroyAnimationParameter);
                }
                else
                {
                    Destroy(ResolveDestroyCommandContext());
                }
            }
        }

        private void Update()
        {
            if (m_operating)
            {
                m_remainingLifetime -= Time.deltaTime;

                if (m_remainingLifetime <= 0.0f)
                {
                    Terminate();
                }
            }
        }

        private void FixedUpdate()
        {
            transform.rotation = Quaternion.LookRotation(Vector3.forward, m_direction * (m_reverseRotation ? -1.0f : 1.0f));

            m_rigidbody.linearVelocity =
                m_operating ?
                m_direction * m_speed :
                Vector2.zero;
        }
    }
}
