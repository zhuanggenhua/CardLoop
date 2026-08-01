using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public sealed class OrbitSteeringBehaviour2D : SteeringBehaviour2D
    {
        [SerializeField, Min(0.0f)] private float m_desiredRadius = 1.5f;
        [SerializeField] private bool m_clockwise = true;

        public OrbitSteeringBehaviour2D() : base("orbit") { }

        public override string DisplayName => "Orbit";

        protected override void EvaluateEnabled(SteeringDetectionFrame2D frame, SteeringContribution2D contribution, float weight)
        {
            if (!frame.TargetPosition.HasValue)
            {
                return;
            }

            Vector2 toTarget = frame.TargetPosition.Value - frame.Position;
            float distance = toTarget.magnitude;
            if (distance <= 0.0001f)
            {
                return;
            }

            Vector2 radial = toTarget / distance;
            Vector2 tangent = m_clockwise ? new Vector2(radial.y, -radial.x) : new Vector2(-radial.y, radial.x);
            float desiredRadius = frame.ArrivalStopRadius >= 0.0f
                ? frame.ArrivalStopRadius
                : m_desiredRadius;
            float radialError = distance - Mathf.Max(desiredRadius, 0.0f);
            Vector2 desired = tangent + radial * Mathf.Clamp(radialError, -1.0f, 1.0f);
            contribution.AddInterest(frame.DirectionSet, desired, weight);
        }
    }
}
