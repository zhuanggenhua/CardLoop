using System;
using UnityEngine;

namespace ContextSteering2D
{
    /// <summary>
    /// 按游走意图沿目标侧向移动，并在跟随距离外补充靠近目标的方向兴趣。
    /// </summary>
    [Serializable]
    public sealed class CombatWanderSteeringBehaviour2D : SteeringBehaviour2D
    {
        public CombatWanderSteeringBehaviour2D() : base("combat-wander") { }

        public override string DisplayName => "Combat Wander";

        protected override void EvaluateEnabled(
            SteeringDetectionFrame2D frame,
            SteeringContribution2D contribution,
            float weight)
        {
            if (!frame.TargetPosition.HasValue || !frame.WanderIntent.HasValue)
            {
                return;
            }

            Vector2 toTarget = frame.TargetPosition.Value - frame.Position;
            float distance = toTarget.magnitude;
            if (distance <= 0.0001f)
            {
                return;
            }

            SteeringWanderIntent2D intent = frame.WanderIntent.Value;
            Vector2 radial = toTarget / distance;
            Vector2 right = new(radial.y, -radial.x);
            float sideSign = ResolveObstacleAdjustedSide(frame, intent.SideSign, right);
            contribution.AddInterest(frame.DirectionSet, right * sideSign, weight);

            float followDistance = Mathf.Max(intent.FollowDistance, 0.0001f);
            float distanceWeight = (distance - followDistance) / followDistance;
            if (distanceWeight > 0.0f)
            {
                contribution.AddInterest(frame.DirectionSet, radial, distanceWeight * weight);
            }
        }

        private static float ResolveObstacleAdjustedSide(
            SteeringDetectionFrame2D frame,
            float requestedSideSign,
            Vector2 right)
        {
            Vector2 dangerDirection = Vector2.zero;
            for (int i = 0; i < frame.Obstacles.Count; i++)
            {
                SteeringObstacle2D obstacle = frame.Obstacles[i];
                Vector2 toObstacle = obstacle.ClosestPoint - frame.Position;
                float distance = toObstacle.magnitude;
                if (distance <= 0.0001f)
                {
                    continue;
                }

                float combinedRadius = frame.AgentRadius + obstacle.Radius;
                float range = Mathf.Max(frame.ObstacleProbeRadius, combinedRadius + 0.01f);
                float proximity = distance <= combinedRadius
                    ? 1.0f
                    : 1.0f - Mathf.Clamp01((distance - combinedRadius) / (range - combinedRadius));
                dangerDirection += toObstacle / distance * proximity;
            }

            if (dangerDirection.magnitude <= 1.0f)
            {
                return requestedSideSign;
            }

            Vector2 requestedSide = right * requestedSideSign;
            Vector2 oppositeSide = -requestedSide;
            return Vector2.Dot(requestedSide, dangerDirection) <= Vector2.Dot(oppositeSide, dangerDirection)
                ? requestedSideSign
                : -requestedSideSign;
        }
    }
}
