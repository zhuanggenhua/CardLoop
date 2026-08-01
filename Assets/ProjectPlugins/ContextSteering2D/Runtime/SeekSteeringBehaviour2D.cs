using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public sealed class SeekSteeringBehaviour2D : SteeringBehaviour2D
    {
        public SeekSteeringBehaviour2D() : base("seek") { }

        public override string DisplayName => "Seek";

        protected override void EvaluateEnabled(SteeringDetectionFrame2D frame, SteeringContribution2D contribution, float weight)
        {
            if (frame.TargetPosition.HasValue)
            {
                contribution.AddInterest(frame.DirectionSet, frame.TargetPosition.Value - frame.Position, weight);
            }
        }
    }
}
