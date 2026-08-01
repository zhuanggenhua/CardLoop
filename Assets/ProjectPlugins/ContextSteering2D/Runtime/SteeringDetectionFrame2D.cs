using System.Collections.Generic;
using UnityEngine;

namespace ContextSteering2D
{
    public sealed class SteeringDetectionFrame2D
    {
        private readonly List<SteeringObstacle2D> m_obstacles = new();
        private readonly List<SteeringBody2D> m_neighbours = new();
        private readonly List<Collider2D> m_detectedColliders = new();

        public SteeringDirectionSet2D DirectionSet { get; private set; }
        public int AgentId { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 Forward { get; private set; } = Vector2.right;
        public Vector2 Velocity { get; private set; }
        public float AgentRadius { get; private set; }
        public float AgentMass { get; private set; }
        public float AgentPriority { get; private set; }
        public float ObstacleProbeRadius { get; private set; }
        public float NeighbourRadius { get; private set; }
        public Vector2? TargetPosition { get; private set; }
        public Vector2 TargetVelocity { get; private set; }
        public float ArrivalStopRadius { get; private set; } = -1.0f;
        public SteeringWanderIntent2D? WanderIntent { get; private set; }
        public IReadOnlyList<SteeringObstacle2D> Obstacles => m_obstacles;
        public IReadOnlyList<SteeringBody2D> Neighbours => m_neighbours;
        public IReadOnlyList<Collider2D> DetectedColliders => m_detectedColliders;

        internal void Reset(
            SteeringDirectionSet2D directionSet,
            int agentId,
            Vector2 position,
            Vector2 forward,
            Vector2 velocity,
            ContextSteeringProfile2D profile,
            Vector2? targetPosition,
            Vector2 targetVelocity,
            float arrivalStopRadius = -1.0f,
            SteeringWanderIntent2D? wanderIntent = null)
        {
            DirectionSet = directionSet;
            AgentId = agentId;
            Position = position;
            Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
            Velocity = velocity;
            AgentRadius = profile.AgentRadius;
            AgentMass = profile.Mass;
            AgentPriority = profile.AvoidancePriority;
            ObstacleProbeRadius = profile.ObstacleProbeRadius;
            NeighbourRadius = profile.NeighbourRadius;
            TargetPosition = targetPosition;
            TargetVelocity = targetVelocity;
            ArrivalStopRadius = arrivalStopRadius >= 0.0f ? arrivalStopRadius : -1.0f;
            WanderIntent = wanderIntent;
            m_obstacles.Clear();
            m_neighbours.Clear();
            m_detectedColliders.Clear();
        }

        internal void AddObstacle(SteeringObstacle2D obstacle) => m_obstacles.Add(obstacle);
        internal void AddNeighbour(SteeringBody2D neighbour) => m_neighbours.Add(neighbour);
        internal void AddDetectedCollider(Collider2D collider) => m_detectedColliders.Add(collider);
    }
}
