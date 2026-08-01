using UnityEngine;

namespace ContextSteering2D
{
    public readonly struct SteeringResult2D
    {
        public SteeringResult2D(
            Vector2 desiredDirection,
            float speedScale,
            Vector2 preferredVelocity,
            Vector2 safeVelocity,
            Vector2 pushCorrection,
            Vector2 finalVelocity)
        {
            DesiredDirection = desiredDirection;
            SpeedScale = Mathf.Clamp01(speedScale);
            PreferredVelocity = preferredVelocity;
            SafeVelocity = safeVelocity;
            SafeDirection = safeVelocity.sqrMagnitude > 0.0001f ? safeVelocity.normalized : Vector2.zero;
            PushCorrection = pushCorrection;
            FinalVelocity = finalVelocity;
            FinalDirection = finalVelocity.sqrMagnitude > 0.0001f ? finalVelocity.normalized : Vector2.zero;
            HasOutput = FinalDirection.sqrMagnitude > 0.0001f || pushCorrection.sqrMagnitude > 0.0001f;
        }

        public Vector2 DesiredDirection { get; }
        public float SpeedScale { get; }
        public Vector2 PreferredVelocity { get; }
        public Vector2 SafeVelocity { get; }
        public Vector2 SafeDirection { get; }
        public Vector2 PushCorrection { get; }
        public Vector2 FinalVelocity { get; }
        public Vector2 FinalDirection { get; }
        public bool HasOutput { get; }
    }
}
