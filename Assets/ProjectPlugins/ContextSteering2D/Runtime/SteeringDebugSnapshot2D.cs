using System;
using UnityEngine;

namespace ContextSteering2D
{
    public sealed class SteeringContributionSnapshot2D
    {
        internal SteeringContributionSnapshot2D(SteeringContribution2D source)
        {
            StableId = source.StableId;
            DisplayName = source.DisplayName;
            Interest = source.Interest.ToArray();
            Constraint = source.Constraint.ToArray();
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public float[] Interest { get; }
        public float[] Constraint { get; }
    }

    public sealed class SteeringDebugSnapshot2D
    {
        internal SteeringDebugSnapshot2D(
            SteeringDetectionFrame2D frame,
            SteeringContext2D context,
            int contributionCount,
            SteeringResult2D result,
            string profileName,
            string behaviourGroupId)
        {
            ProfileName = profileName ?? string.Empty;
            BehaviourGroupId = behaviourGroupId ?? string.Empty;
            Position = frame.Position;
            Forward = frame.Forward;
            TargetPosition = frame.TargetPosition;
            AgentRadius = frame.AgentRadius;
            Directions = frame.DirectionSet.Directions.ToArray();
            Interest = context.Interest.ToArray();
            Constraint = context.Constraint.ToArray();
            Combined = context.Combined.ToArray();
            Obstacles = CopyObstacles(frame);
            Neighbours = CopyNeighbours(frame);
            DetectedColliderCount = frame.DetectedColliders.Count;
            Contributions = new SteeringContributionSnapshot2D[contributionCount];
            for (int i = 0; i < contributionCount; i++)
            {
                Contributions[i] = new SteeringContributionSnapshot2D(context.Contributions[i]);
            }

            Result = result;
        }

        public Vector2 Position { get; }
        public string ProfileName { get; }
        public string BehaviourGroupId { get; }
        public Vector2 Forward { get; }
        public Vector2? TargetPosition { get; }
        public float AgentRadius { get; }
        public Vector2[] Directions { get; }
        public float[] Interest { get; }
        public float[] Constraint { get; }
        public float[] Combined { get; }
        public SteeringObstacle2D[] Obstacles { get; }
        public SteeringBody2D[] Neighbours { get; }
        public int DetectedColliderCount { get; }
        public SteeringContributionSnapshot2D[] Contributions { get; }
        public SteeringResult2D Result { get; }

        private static SteeringObstacle2D[] CopyObstacles(SteeringDetectionFrame2D frame)
        {
            SteeringObstacle2D[] values = new SteeringObstacle2D[frame.Obstacles.Count];
            for (int i = 0; i < values.Length; i++) values[i] = frame.Obstacles[i];
            return values;
        }

        private static SteeringBody2D[] CopyNeighbours(SteeringDetectionFrame2D frame)
        {
            SteeringBody2D[] values = new SteeringBody2D[frame.Neighbours.Count];
            for (int i = 0; i < values.Length; i++) values[i] = frame.Neighbours[i];
            return values;
        }
    }
}
