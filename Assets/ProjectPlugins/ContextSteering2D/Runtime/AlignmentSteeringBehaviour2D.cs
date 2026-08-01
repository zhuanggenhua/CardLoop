using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public sealed class AlignmentSteeringBehaviour2D : SteeringBehaviour2D
    {
        public AlignmentSteeringBehaviour2D() : base("alignment") { }

        public override string DisplayName => "Alignment";

        protected override void EvaluateEnabled(SteeringDetectionFrame2D frame, SteeringContribution2D contribution, float weight)
        {
            Vector2 averageVelocity = Vector2.zero;
            int count = 0;
            for (int i = 0; i < frame.Neighbours.Count; i++)
            {
                SteeringBody2D neighbour = frame.Neighbours[i];
                if (!neighbour.IsStatic && HasDirection(neighbour.Velocity))
                {
                    averageVelocity += neighbour.Velocity;
                    count++;
                }
            }

            if (count > 0)
            {
                contribution.AddInterest(frame.DirectionSet, averageVelocity / count, weight);
            }
        }
    }
}
