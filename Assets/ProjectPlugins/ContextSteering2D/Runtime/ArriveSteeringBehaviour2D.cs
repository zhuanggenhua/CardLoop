using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public sealed class ArriveSteeringBehaviour2D : SteeringBehaviour2D
    {
        [SerializeField, Min(0.01f)] private float m_slowRadius = 1.5f;
        [SerializeField, Min(0.0f)] private float m_stopRadius = 0.1f;

        public ArriveSteeringBehaviour2D() : base("arrive") { }

        public override string DisplayName => "Arrive";

        protected override void EvaluateEnabled(SteeringDetectionFrame2D frame, SteeringContribution2D contribution, float weight)
        {
            if (!frame.TargetPosition.HasValue)
            {
                return;
            }

            Vector2 toTarget = frame.TargetPosition.Value - frame.Position;
            float distance = toTarget.magnitude;

            float stopRadius = frame.ArrivalStopRadius >= 0.0f
                ? frame.ArrivalStopRadius
                : Mathf.Max(m_stopRadius, 0.0f);
            float slowRadius = Mathf.Max(m_slowRadius, stopRadius + 0.01f);
            float speedScale = distance <= stopRadius
                ? 0.0f
                : Mathf.Clamp01((distance - stopRadius) / (slowRadius - stopRadius));
            contribution.LimitSpeed(speedScale);
        }
    }
}
