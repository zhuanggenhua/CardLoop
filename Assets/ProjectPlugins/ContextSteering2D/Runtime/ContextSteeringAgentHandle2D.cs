using System;
using UnityEngine;

namespace ContextSteering2D
{
    public sealed class ContextSteeringAgentHandle2D : IDisposable
    {
        private readonly ContextSteeringSimulation2D m_owner;
        private bool m_disposed;

        internal ContextSteeringAgentHandle2D(
            ContextSteeringSimulation2D owner,
            int agentId,
            Rigidbody2D body,
            ContextSteeringProfile2D profile,
            ContactFilter2D obstacleFilter,
            ContactFilter2D neighbourFilter,
            ContactFilter2D semanticFilter)
        {
            m_owner = owner;
            AgentId = agentId > 0 ? agentId : throw new ArgumentOutOfRangeException(nameof(agentId));
            Body = body ? body : throw new ArgumentNullException(nameof(body));
            Profile = profile ? profile : throw new ArgumentNullException(nameof(profile));
            Profile.ValidateOrThrow();
            ObstacleFilter = obstacleFilter;
            NeighbourFilter = neighbourFilter;
            SemanticFilter = semanticFilter;
            UnionFilter = CreateUnionFilter(obstacleFilter, neighbourFilter, semanticFilter);
            Solver = new ContextSteeringSolver2D(profile.SampleCount);
            Frame = new SteeringDetectionFrame2D();
            GroupId = profile.DefaultGroupIdValue;
            Forward = body.transform.right;
            MaxSpeed = profile.MaxSpeed;
        }

        public Rigidbody2D Body { get; }
        public int AgentId { get; }
        public ContextSteeringProfile2D Profile { get; }
        public bool Active { get; private set; }
        public string GroupId { get; private set; }
        public Vector2? TargetPosition { get; private set; }
        public Vector2 TargetVelocity { get; private set; }
        public Vector2 Forward { get; private set; }
        public float MaxSpeed { get; private set; }
        public SteeringResult2D Result { get; internal set; }
        public SteeringDebugSnapshot2D DebugSnapshot { get; internal set; }
        public event Action<SteeringDebugSnapshot2D> DebugSnapshotPublished;
        public System.Collections.Generic.IReadOnlyList<Collider2D> DetectedColliders => Frame.DetectedColliders;

        internal ContextSteeringSolver2D Solver { get; }
        internal SteeringDetectionFrame2D Frame { get; }
        internal ContactFilter2D ObstacleFilter { get; }
        internal ContactFilter2D NeighbourFilter { get; }
        internal ContactFilter2D SemanticFilter { get; }
        internal ContactFilter2D UnionFilter { get; }
        internal bool CaptureDebug { get; private set; }
        internal float SemanticQueryRadius { get; private set; }
        internal float ArrivalStopRadius { get; private set; } = -1.0f;
        internal SteeringWanderIntent2D? WanderIntent { get; private set; }

        public void SubmitIntent(
            bool active,
            Vector2? targetPosition,
            Vector2 targetVelocity,
            Vector2 forward,
            string groupId = null,
            bool captureDebug = false,
            float semanticQueryRadius = 0.0f,
            float maxSpeed = -1.0f,
            float arrivalStopRadius = -1.0f,
            SteeringWanderIntent2D? wanderIntent = null)
        {
            ThrowIfDisposed();
            Active = active;
            TargetPosition = targetPosition;
            TargetVelocity = targetVelocity;
            Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Body.transform.right;
            GroupId = string.IsNullOrWhiteSpace(groupId) ? Profile.DefaultGroupIdValue : groupId;
            CaptureDebug = captureDebug;
            SemanticQueryRadius = Mathf.Max(semanticQueryRadius, 0.0f);
            MaxSpeed = maxSpeed >= 0.0f ? maxSpeed : Profile.MaxSpeed;
            ArrivalStopRadius = arrivalStopRadius >= 0.0f ? arrivalStopRadius : -1.0f;
            WanderIntent = wanderIntent;
            Profile.GetBehaviourGroup(GroupId);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_owner.Unregister(this);
            DebugSnapshotPublished = null;
        }

        internal void PublishDebugSnapshot(SteeringDebugSnapshot2D snapshot)
        {
            DebugSnapshot = snapshot;
            DebugSnapshotPublished?.Invoke(snapshot);
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(ContextSteeringAgentHandle2D));
            }
        }

        private static ContactFilter2D CreateUnionFilter(
            ContactFilter2D obstacleFilter,
            ContactFilter2D neighbourFilter,
            ContactFilter2D semanticFilter)
        {
            ContactFilter2D filter = new();
            int obstacleMask = obstacleFilter.useLayerMask ? obstacleFilter.layerMask.value : ~0;
            int neighbourMask = neighbourFilter.useLayerMask ? neighbourFilter.layerMask.value : ~0;
            int semanticMask = semanticFilter.useLayerMask ? semanticFilter.layerMask.value : ~0;
            filter.SetLayerMask(obstacleMask | neighbourMask | semanticMask);
            filter.useTriggers = obstacleFilter.useTriggers || neighbourFilter.useTriggers || semanticFilter.useTriggers;
            return filter;
        }
    }
}
