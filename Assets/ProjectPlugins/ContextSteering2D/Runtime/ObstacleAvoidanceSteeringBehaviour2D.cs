using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public sealed class ObstacleAvoidanceSteeringBehaviour2D : SteeringBehaviour2D
    {
        [SerializeField, Range(0.0f, 1.0f)] private float m_sidePreference = 0.35f;

        public ObstacleAvoidanceSteeringBehaviour2D() : base("obstacle-avoidance") { }

        public override string DisplayName => "Obstacle Avoidance";

        protected override void EvaluateEnabled(SteeringDetectionFrame2D frame, SteeringContribution2D contribution, float weight)
        {
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
                if (proximity <= 0.0f)
                {
                    continue;
                }

                Vector2 obstacleDirection = toObstacle / distance;
                contribution.AddConstraint(frame.DirectionSet, obstacleDirection, proximity * weight);

                if (Vector2.Dot(obstacleDirection, frame.Forward) > 0.0f && m_sidePreference > 0.0f)
                {
                    float sideSign = Vector3.Cross(frame.Forward, obstacleDirection).z >= 0.0f ? -1.0f : 1.0f;
                    Vector2 side = new(-frame.Forward.y * sideSign, frame.Forward.x * sideSign);
                    contribution.AddInterest(frame.DirectionSet, side, proximity * weight * m_sidePreference);
                }
            }
        }
    }
}
