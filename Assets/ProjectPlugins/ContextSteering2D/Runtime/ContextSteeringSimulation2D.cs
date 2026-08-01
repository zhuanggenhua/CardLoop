using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSteering2D
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class ContextSteeringSimulation2D : MonoBehaviour
    {
        [SerializeField, Min(8)] private int m_detectionBufferSize = 128;
        [SerializeField, Range(1, 16)] private int m_contactIterations = 4;

        private readonly List<ContextSteeringAgentHandle2D> m_agents = new();
        private readonly Dictionary<Collider2D, ContextSteeringAgentHandle2D> m_registeredColliders = new();
        private readonly List<LocalAvoidanceInput2D> m_avoidanceInputs = new();
        private readonly AgentSpatialIndex2D m_agentSpatialIndex = new();
        private SteeringDetectionScheduler2D m_detectionScheduler;
        private ILocalAvoidanceBackend2D m_localAvoidanceBackend;
        private PositionBasedContactResolver2D m_contactResolver;
        private Vector2[] m_safeVelocities = Array.Empty<Vector2>();
        private Vector2[] m_contactCorrections = Array.Empty<Vector2>();
        private SteeringResult2D[] m_preferredResults = Array.Empty<SteeringResult2D>();
        private Vector2[] m_agentPositions = Array.Empty<Vector2>();
        private int m_nextAgentId;

        public static ContextSteeringSimulation2D Current { get; private set; }
        public int AgentCount => m_agents.Count;
        public ContextSteeringSimulationMetrics2D LastMetrics { get; private set; }

        public static ContextSteeringSimulation2D RequireCurrent()
        {
            if (Current == null)
            {
                throw new InvalidOperationException("No ContextSteeringSimulation2D is active. Add one explicit simulation component to the active world scene.");
            }

            return Current;
        }

        public ContextSteeringAgentHandle2D Register(
            Rigidbody2D body,
            ContextSteeringProfile2D profile,
            ContactFilter2D obstacleFilter,
            ContactFilter2D neighbourFilter,
            ContactFilter2D semanticFilter)
        {
            EnsureRuntimeServices();
            if (m_nextAgentId == int.MaxValue)
            {
                throw new InvalidOperationException("ContextSteering2D exhausted its stable runtime agent IDs.");
            }

            ContextSteeringAgentHandle2D handle = new(
                this,
                ++m_nextAgentId,
                body,
                profile,
                obstacleFilter,
                neighbourFilter,
                semanticFilter);

            Collider2D[] childColliders = body.GetComponentsInChildren<Collider2D>(true);
            List<Collider2D> ownedColliders = new(childColliders.Length);
            for (int i = 0; i < childColliders.Length; i++)
            {
                Collider2D collider = childColliders[i];
                if (collider == null || collider.attachedRigidbody != body)
                {
                    continue;
                }

                if (m_registeredColliders.ContainsKey(collider))
                {
                    throw new InvalidOperationException($"Collider '{collider.name}' is already registered to ContextSteeringSimulation2D.");
                }

                ownedColliders.Add(collider);
            }

            if (ownedColliders.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Rigidbody2D '{body.name}' has no Collider2D owned by that body. ContextSteering2D cannot detect this agent as a neighbour.");
            }

            for (int i = 0; i < ownedColliders.Count; i++)
            {
                Collider2D collider = ownedColliders[i];
                m_registeredColliders.Add(collider, handle);
            }

            m_agents.Add(handle);
            return handle;
        }

        internal void Unregister(ContextSteeringAgentHandle2D handle)
        {
            m_agents.Remove(handle);
            List<Collider2D> removals = null;
            foreach (KeyValuePair<Collider2D, ContextSteeringAgentHandle2D> pair in m_registeredColliders)
            {
                if (pair.Value == handle)
                {
                    removals ??= new List<Collider2D>();
                    removals.Add(pair.Key);
                }
            }

            if (removals == null) return;
            for (int i = 0; i < removals.Count; i++) m_registeredColliders.Remove(removals[i]);
        }

        public void Simulate(float deltaTime)
        {
            long totalStart = System.Diagnostics.Stopwatch.GetTimestamp();
            long detectionTicks = 0;
            long steeringTicks = 0;
            EnsureRuntimeServices();
            int count = m_agents.Count;
            EnsureCapacity(count);
            m_avoidanceInputs.Clear();
            float spatialCellSize = 0.1f;
            for (int i = 0; i < count; i++)
            {
                if (m_agents[i].Body == null)
                {
                    throw new MissingReferenceException("A registered ContextSteering agent lost its Rigidbody2D.");
                }
                m_agentPositions[i] = m_agents[i].Body.position;
                spatialCellSize = Mathf.Max(spatialCellSize, m_agents[i].Profile.NeighbourRadius);
            }
            m_agentSpatialIndex.Build(m_agentPositions, count, spatialCellSize);

            for (int i = 0; i < count; i++)
            {
                ContextSteeringAgentHandle2D agent = m_agents[i];
                if (agent.Body == null)
                {
                    throw new MissingReferenceException("A registered ContextSteering agent lost its Rigidbody2D.");
                }

                if (agent.Active)
                {
                    long steeringStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    agent.Solver.Prepare(agent.Profile);
                    steeringTicks += System.Diagnostics.Stopwatch.GetTimestamp() - steeringStart;
                    long detectionStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    m_detectionScheduler.BuildFrame(agent, m_registeredColliders, m_agents, m_agentSpatialIndex);
                    detectionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - detectionStart;
                    steeringStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    m_preferredResults[i] = agent.Solver.Solve(agent.Frame, agent.Profile, agent.GroupId, agent.MaxSpeed);
                    steeringTicks += System.Diagnostics.Stopwatch.GetTimestamp() - steeringStart;
                }
                else
                {
                    m_preferredResults[i] = default;
                }

                m_avoidanceInputs.Add(new LocalAvoidanceInput2D(
                    agent.AgentId,
                    agent.Body.position,
                    agent.Body.linearVelocity,
                    m_preferredResults[i].PreferredVelocity,
                    agent.MaxSpeed,
                    agent.Profile.AgentRadius,
                    agent.Profile.Mass,
                    agent.Profile.AvoidancePriority,
                    agent.Profile.ContactStiffness,
                    agent.Profile.MaxContactCorrection,
                    agent.Profile.NeighbourRadius,
                    agent.Profile.TimeHorizon,
                    agent.Profile.MaxNeighbours,
                    agent.Active));
            }

            long localAvoidanceStart = System.Diagnostics.Stopwatch.GetTimestamp();
            m_localAvoidanceBackend.Resolve(m_avoidanceInputs, m_safeVelocities, deltaTime);
            long localAvoidanceTicks = System.Diagnostics.Stopwatch.GetTimestamp() - localAvoidanceStart;
            long contactStart = System.Diagnostics.Stopwatch.GetTimestamp();
            m_contactResolver.Resolve(
                m_avoidanceInputs,
                m_safeVelocities,
                m_contactCorrections,
                m_contactIterations,
                deltaTime);
            long contactTicks = System.Diagnostics.Stopwatch.GetTimestamp() - contactStart;
            for (int i = 0; i < count; i++)
            {
                ContextSteeringAgentHandle2D agent = m_agents[i];
                if (!agent.Active)
                {
                    agent.Result = default;
                    agent.PublishDebugSnapshot(null);
                    continue;
                }

                SteeringResult2D preferred = m_preferredResults[i];
                Vector2 push = m_contactCorrections[i];
                Vector2 finalVelocity = m_safeVelocities[i] + (deltaTime > 0.0f ? push / deltaTime : Vector2.zero);
                agent.Result = new SteeringResult2D(
                    preferred.DesiredDirection,
                    preferred.SpeedScale,
                    preferred.PreferredVelocity,
                    m_safeVelocities[i],
                    push,
                    finalVelocity);

                int contributionCount = agent.Profile.GetBehaviourGroup(agent.GroupId).Behaviours.Count;
                SteeringDebugSnapshot2D snapshot = agent.CaptureDebug
                    ? new SteeringDebugSnapshot2D(
                        agent.Frame,
                        agent.Solver.Context,
                        contributionCount,
                        agent.Result,
                        agent.Profile.name,
                        agent.GroupId)
                    : null;
                agent.PublishDebugSnapshot(snapshot);
            }

            LastMetrics = new ContextSteeringSimulationMetrics2D(
                count,
                ToMilliseconds(detectionTicks),
                ToMilliseconds(steeringTicks),
                ToMilliseconds(localAvoidanceTicks),
                ToMilliseconds(contactTicks),
                ToMilliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - totalStart));
        }

        private void OnEnable()
        {
            if (Current != null && Current != this)
            {
                throw new InvalidOperationException("Only one ContextSteeringSimulation2D may be active in a world.");
            }

            Current = this;
            EnsureRuntimeServices();
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            ReleaseRuntimeServices();
        }

        private void OnDestroy() => ReleaseRuntimeServices();

        internal void ReleaseRuntimeServices()
        {
            m_localAvoidanceBackend?.Dispose();
            m_localAvoidanceBackend = null;
        }

        private void FixedUpdate() => Simulate(Time.fixedDeltaTime);

        private void EnsureRuntimeServices()
        {
            m_detectionScheduler ??= new SteeringDetectionScheduler2D(m_detectionBufferSize);
            m_localAvoidanceBackend ??= new Rvo2LocalAvoidanceBackend2D();
            m_contactResolver ??= new PositionBasedContactResolver2D();
        }

        private void EnsureCapacity(int count)
        {
            if (m_safeVelocities.Length >= count) return;
            int capacity = Mathf.NextPowerOfTwo(Mathf.Max(count, 4));
            Array.Resize(ref m_safeVelocities, capacity);
            Array.Resize(ref m_contactCorrections, capacity);
            Array.Resize(ref m_preferredResults, capacity);
            Array.Resize(ref m_agentPositions, capacity);
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
    }

    public readonly struct ContextSteeringSimulationMetrics2D
    {
        public ContextSteeringSimulationMetrics2D(
            int agentCount,
            double detectionMilliseconds,
            double steeringMilliseconds,
            double localAvoidanceMilliseconds,
            double contactMilliseconds,
            double totalMilliseconds)
        {
            AgentCount = agentCount;
            DetectionMilliseconds = detectionMilliseconds;
            SteeringMilliseconds = steeringMilliseconds;
            LocalAvoidanceMilliseconds = localAvoidanceMilliseconds;
            ContactMilliseconds = contactMilliseconds;
            TotalMilliseconds = totalMilliseconds;
        }

        public int AgentCount { get; }
        public double DetectionMilliseconds { get; }
        public double SteeringMilliseconds { get; }
        public double LocalAvoidanceMilliseconds { get; }
        public double ContactMilliseconds { get; }
        public double TotalMilliseconds { get; }
    }
}
