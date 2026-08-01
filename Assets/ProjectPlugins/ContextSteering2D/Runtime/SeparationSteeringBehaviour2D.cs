using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public sealed class SeparationSteeringBehaviour2D : SteeringBehaviour2D
    {
        public SeparationSteeringBehaviour2D() : base("separation") { }

        public override string DisplayName => "Separation";

        protected override void EvaluateEnabled(SteeringDetectionFrame2D frame, SteeringContribution2D contribution, float weight)
        {
            for (int i = 0; i < frame.Neighbours.Count; i++)
            {
                SteeringBody2D neighbour = frame.Neighbours[i];
                Vector2 away = frame.Position - neighbour.Position;
                float distance = away.magnitude;
                float combinedRadius = frame.AgentRadius + neighbour.Radius;
                float range = Mathf.Max(frame.NeighbourRadius, combinedRadius + 0.01f);
                float proximity = distance <= combinedRadius
                    ? 1.0f
                    : 1.0f - Mathf.Clamp01((distance - combinedRadius) / (range - combinedRadius));
                Vector2 direction = distance > 0.0001f ? away / distance : -frame.Forward;
                contribution.AddInterest(frame.DirectionSet, direction, proximity * weight);
            }
        }
    }
}
