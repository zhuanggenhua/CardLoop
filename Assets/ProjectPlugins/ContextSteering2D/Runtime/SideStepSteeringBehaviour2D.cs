using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public sealed class SideStepSteeringBehaviour2D : SteeringBehaviour2D
    {
        [SerializeField, Range(-1.0f, 1.0f)] private float m_forwardDotThreshold = 0.15f;

        public SideStepSteeringBehaviour2D() : base("side-step") { }

        public override string DisplayName => "Side Step";

        protected override void EvaluateEnabled(
            SteeringDetectionFrame2D frame,
            SteeringContribution2D contribution,
            float weight)
        {
            for (int i = 0; i < frame.Neighbours.Count; i++)
            {
                SteeringBody2D neighbour = frame.Neighbours[i];
                Vector2 toNeighbour = neighbour.Position - frame.Position;
                float distance = toNeighbour.magnitude;
                if (distance <= 0.0001f)
                {
                    continue;
                }

                Vector2 direction = toNeighbour / distance;
                if (Vector2.Dot(frame.Forward, direction) < m_forwardDotThreshold)
                {
                    continue;
                }

                float range = Mathf.Max(frame.NeighbourRadius, frame.AgentRadius + neighbour.Radius + 0.01f);
                float proximity = 1.0f - Mathf.Clamp01(distance / range);
                if (proximity <= 0.0f)
                {
                    continue;
                }

                float cross = Vector3.Cross(frame.Forward, direction).z;
                float sideSign = Mathf.Abs(cross) > 0.001f
                    ? -Mathf.Sign(cross)
                    : -1.0f;
                Vector2 side = new(-frame.Forward.y * sideSign, frame.Forward.x * sideSign);
                contribution.AddInterest(frame.DirectionSet, side, proximity * weight);
            }
        }
    }
}
