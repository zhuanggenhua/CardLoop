using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public sealed class CohesionSteeringBehaviour2D : SteeringBehaviour2D
    {
        public CohesionSteeringBehaviour2D() : base("cohesion") { }

        public override string DisplayName => "Cohesion";

        protected override void EvaluateEnabled(SteeringDetectionFrame2D frame, SteeringContribution2D contribution, float weight)
        {
            Vector2 center = Vector2.zero;
            int count = 0;
            for (int i = 0; i < frame.Neighbours.Count; i++)
            {
                if (!frame.Neighbours[i].IsStatic)
                {
                    center += frame.Neighbours[i].Position;
                    count++;
                }
            }

            if (count > 0)
            {
                contribution.AddInterest(frame.DirectionSet, center / count - frame.Position, weight);
            }
        }
    }
}
