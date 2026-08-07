using ContextSteering2D;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 保存单个敌人的游走方向与跟随距离，按参考实现每 1.5 秒重新随机一次。
    /// </summary>
    internal sealed class CombatWanderRuntime2D
    {
        private const float DirectionChangeInterval = 1.5f;

        private float m_directionChangeTimer;
        private SteeringWanderIntent2D? m_currentIntent;

        public static bool ShouldUse(
            bool enabled,
            bool hasTarget,
            float distanceToTarget,
            float wanderRange)
        {
            return enabled &&
                hasTarget &&
                distanceToTarget <= wanderRange;
        }

        public SteeringWanderIntent2D Tick(float deltaTime, float attackRange, float wanderRange)
        {
            m_directionChangeTimer -= Mathf.Max(deltaTime, 0.0f);
            if (!m_currentIntent.HasValue || m_directionChangeTimer <= 0.0f)
            {
                m_directionChangeTimer = DirectionChangeInterval;
                float minimumDistance = Mathf.Max(attackRange, 0.0f);
                float maximumDistance = Mathf.Max(wanderRange, minimumDistance);
                m_currentIntent = new SteeringWanderIntent2D(
                    Random.value > 0.5f ? 1.0f : -1.0f,
                    Random.Range(minimumDistance, maximumDistance));
            }

            return m_currentIntent.Value;
        }

        public void Reset()
        {
            m_directionChangeTimer = 0.0f;
            m_currentIntent = null;
        }
    }
}
