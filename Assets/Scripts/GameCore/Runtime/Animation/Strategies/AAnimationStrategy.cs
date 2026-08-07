using System;
using UnityEngine;
using UnityEngine.Events;

namespace GameCore
{
    [Serializable]
    public abstract class AAnimationStrategy : IAnimationStrategy
    {
        [Header("Movable Animation Parameters")]
        [SerializeField] private string m_isMovingAnimationParameter = "isMoving";

        [Header("Character Animation Parameters")]
        [SerializeField] private string m_hitAnimationParameter = "hit";
        [SerializeField] private string m_deathAnimationParameter = "death";
        [SerializeField] private string m_invincibleAnimationParameter = "invincible";

        [Header("General Settings")]
        [SerializeField] private bool m_dynamicSortingOrder = true;
        [SerializeField] private int m_orderInLayerOverrideWhenMovingUp = 2;

        [Header("References")]
        [SerializeField] protected Animator m_animator = null;
        [SerializeField] protected SpriteRenderer m_spriteRenderer = null;

        private bool m_hasDeathAnimation = false;
        private bool m_hasHitAnimation = false;
        private bool m_hasInvincibleAnimation = false;
        private bool m_hasMovingAnimation;
        private UnityEvent m_deathAnimationStarted = new();
        private UnityEvent m_deathAnimationEnded = new();
        private bool m_invincibleAnimationPlaying = false;

        private int m_defaultOrderInLayer = 0;

        public virtual void Initialize()
        {
            Debug.Assert(m_animator, ErrorMessages.InspectorMissingComponentReference<Animator>());
            Debug.Assert(m_spriteRenderer, ErrorMessages.InspectorMissingComponentReference<SpriteRenderer>());

            m_defaultOrderInLayer = m_spriteRenderer.sortingOrder;

            CheckForAnimations();
        }

        public void AddDeathAnimationStartedListener(UnityAction listener)
        {
            m_deathAnimationStarted.AddListener(listener);
        }

        public void RemoveDeathAnimationStartedListener(UnityAction listener)
        {
            m_deathAnimationStarted.RemoveListener(listener);
        }

        public void AddDeathAnimationEndedListener(UnityAction listener)
        {
            m_deathAnimationEnded.AddListener(listener);
        }

        public void RemoveDeathAnimationEndedListener(UnityAction listener)
        {
            m_deathAnimationEnded.RemoveListener(listener);
        }

        public void Resume()
        {
            m_animator.enabled = true;

            // Animator 启停后需要重新绑定并推进一次 0 delta，确保复活/重生后持续动画按当前参数恢复。
            // 相关 Unity 行为：https://issuetracker.unity3d.com/issues/animator-does-not-continue-animation-indefinitely-when-toggling-animator-dot-enabled-through-code
            m_animator.Rebind();
            m_animator.Update(0);
        }

        public void Pause() => m_animator.enabled = false;

        public virtual void OnInvincibleAnimationStart()
        {
            m_invincibleAnimationPlaying = true;
        }

        public virtual void OnInvincibleAnimationStop()
        {
            m_invincibleAnimationPlaying = false;
        }

        public virtual void OnDeathAnimationStart()
        {
            m_deathAnimationStarted.Invoke();
        }

        public virtual void OnDeathAnimationStop()
        {
            m_deathAnimationEnded.Invoke();
        }

        protected virtual void CheckForAnimations()
        {
            if (m_animator)
            {
                m_hasHitAnimation = AnimationUtils.HasParameter(m_animator, m_hitAnimationParameter);
                m_hasDeathAnimation = AnimationUtils.HasParameter(m_animator, m_deathAnimationParameter);
                m_hasInvincibleAnimation = AnimationUtils.HasParameter(m_animator, m_invincibleAnimationParameter);
                m_hasMovingAnimation = AnimationUtils.HasParameter(m_animator, m_isMovingAnimationParameter);
            }
        }

        public virtual void SetLookAtDirection(Vector2 direction) { }

        public virtual void SetTargetDirection(Vector2 direction)
        {
            if (m_dynamicSortingOrder)
            {
                // Set the sorting order based on the direction:
                // When moving up, the sprite should be rendered on top of everything (hands, weapon, etc.)
                m_spriteRenderer.sortingOrder = direction.y > 0.0f ? m_orderInLayerOverrideWhenMovingUp : m_defaultOrderInLayer;
            }
        }

        public virtual void SetMovement(Vector2 speed)
        {
            if (m_hasMovingAnimation)
            {
                m_animator.SetBool(m_isMovingAnimationParameter, speed.magnitude > 0.0f);
            }
        }

        public virtual bool PlayHitAnimation()
        {
            if (m_animator && m_hasHitAnimation)
            {
                m_animator.SetTrigger(m_hitAnimationParameter);
                return true;
            }

            return false;
        }

        public virtual bool PlayDeathAnimation()
        {
            if (m_animator && m_hasDeathAnimation)
            {
                m_animator.SetTrigger(m_deathAnimationParameter);
                return true;
            }

            return false;
        }

        public virtual bool PlayInvincibleAnimation()
        {
            if (m_animator && m_hasInvincibleAnimation)
            {
                // 无敌语义从请求这一刻开始成立，动画状态消息只负责在结束时把它收回。
                m_invincibleAnimationPlaying = true;
                m_animator.SetTrigger(m_invincibleAnimationParameter);
                return true;
            }

            return false;
        }

        public virtual bool IsInvincibleAnimationPlaying()
        {
            return m_invincibleAnimationPlaying;
        }
    }
}
