using UnityEngine;

namespace ContextSteering2D
{
    public readonly struct SteeringObstacle2D
    {
        public SteeringObstacle2D(Vector2 closestPoint, float radius = 0.0f, bool isStatic = true)
        {
            ClosestPoint = closestPoint;
            Radius = Mathf.Max(radius, 0.0f);
            IsStatic = isStatic;
        }

        public Vector2 ClosestPoint { get; }
        public float Radius { get; }
        public bool IsStatic { get; }
    }
}
