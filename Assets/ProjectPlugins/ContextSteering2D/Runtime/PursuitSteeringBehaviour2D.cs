using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public sealed class PursuitSteeringBehaviour2D : SteeringBehaviour2D
    {
        [SerializeField, Min(0.0f)] private float m_maxPredictionTime = 0.75f;

        public PursuitSteeringBehaviour2D() : base("pursuit") { }

        public override string DisplayName => "Pursuit";

        protected override void EvaluateEnabled(SteeringDetectionFrame2D frame, SteeringContribution2D contribution, float weight)
        {
            if (!frame.TargetPosition.HasValue)
            {
                return;
            }

            Vector2 predictedTarget = frame.TargetPosition.Value + frame.TargetVelocity * Mathf.Max(m_maxPredictionTime, 0.0f);
            contribution.AddInterest(frame.DirectionSet, predictedTarget - frame.Position, weight);
        }
    }
}
