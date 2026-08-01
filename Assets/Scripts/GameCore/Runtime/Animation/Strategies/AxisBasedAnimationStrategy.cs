using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class AxisBasedAnimationStrategy : AAnimationStrategy
    {
        [Header("Axis-Based Animation Parameters")]
        [SerializeField] private string m_moveXAnimationParameter = "moveX";
        [SerializeField] private string m_moveYAnimationParameter = "moveY";

        private bool m_hasMoveXAnimation;
        private bool m_hasMoveYAnimation;

        protected override void CheckForAnimations()
        {
            base.CheckForAnimations();

            if (m_animator)
            {
                m_hasMoveXAnimation = AnimationUtils.HasParameter(m_animator, m_moveXAnimationParameter);
                m_hasMoveYAnimation = AnimationUtils.HasParameter(m_animator, m_moveYAnimationParameter);
            }
        }

        public override void SetMovement(Vector2 speed)
        {
            if (m_hasMoveXAnimation)
            {
                m_animator.SetFloat(m_moveXAnimationParameter, speed.x);
            }

            if (m_hasMoveYAnimation)
            {
                m_animator.SetFloat(m_moveYAnimationParameter, speed.y);
            }
        }
    }
}

