using UnityEngine;

namespace ContextSteering2D
{
    public readonly struct SteeringBody2D
    {
        public SteeringBody2D(
            int agentId,
            Vector2 position,
            float radius,
            Vector2 velocity = default,
            float mass = 1.0f,
            float priority = 1.0f,
            bool isStatic = false)
        {
            AgentId = agentId;
            Position = position;
            Radius = Mathf.Max(radius, 0.0f);
            Velocity = velocity;
            Mass = Mathf.Max(mass, 0.0001f);
            Priority = Mathf.Max(priority, 0.0001f);
            IsStatic = isStatic;
        }

        public int AgentId { get; }
        public Vector2 Position { get; }
        public float Radius { get; }
        public Vector2 Velocity { get; }
        public float Mass { get; }
        public float Priority { get; }
        public bool IsStatic { get; }
    }
}
