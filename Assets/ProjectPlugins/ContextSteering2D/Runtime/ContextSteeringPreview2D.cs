using UnityEngine;

namespace ContextSteering2D
{
    public static class ContextSteeringPreview2D
    {
        public static SteeringDebugSnapshot2D Evaluate(ContextSteeringProfile2D profile, string groupId = null)
        {
            if (profile == null) throw new System.ArgumentNullException(nameof(profile));
            profile.ValidateOrThrow();

            ContextSteeringSolver2D solver = new(profile.SampleCount);
            SteeringDetectionFrame2D frame = new();
            frame.Reset(
                solver.DirectionSet,
                1,
                Vector2.zero,
                Vector2.right,
                Vector2.zero,
                profile,
                new Vector2(2.0f, 0.0f),
                Vector2.zero);
            frame.AddObstacle(new SteeringObstacle2D(new Vector2(0.8f, 0.0f), 0.15f));
            frame.AddNeighbour(new SteeringBody2D(2, new Vector2(0.55f, -0.55f), profile.AgentRadius, new Vector2(0.5f, 0.0f)));

            string resolvedGroup = string.IsNullOrWhiteSpace(groupId) ? profile.DefaultGroupIdValue : groupId;
            SteeringResult2D result = solver.Solve(frame, profile, resolvedGroup);
            int contributionCount = profile.GetBehaviourGroup(resolvedGroup).Behaviours.Count;
            return new SteeringDebugSnapshot2D(
                frame,
                solver.Context,
                contributionCount,
                result,
                profile.name,
                resolvedGroup);
        }
    }
}
