using UnityEngine;

namespace ContextSteering2D
{
    /// <summary>
    /// 单个代理本次游走求解所需的运行时意图；随机状态由业务控制器持有，不进入共享 Profile。
    /// </summary>
    public readonly struct SteeringWanderIntent2D
    {
        public SteeringWanderIntent2D(float sideSign, float followDistance)
        {
            SideSign = sideSign >= 0.0f ? 1.0f : -1.0f;
            FollowDistance = Mathf.Max(followDistance, 0.0f);
        }

        public float SideSign { get; }
        public float FollowDistance { get; }
    }
}
