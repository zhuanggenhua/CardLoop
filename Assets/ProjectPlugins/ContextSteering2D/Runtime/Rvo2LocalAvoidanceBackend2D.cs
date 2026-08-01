using System;
using System.Collections.Generic;
using UnityEngine;
using RvoVector2 = RVO.Vector2;

namespace ContextSteering2D
{
    public interface ILocalAvoidanceBackend2D : IDisposable
    {
        void Resolve(IReadOnlyList<LocalAvoidanceInput2D> inputs, Vector2[] safeVelocities, float deltaTime);
    }

    public readonly struct LocalAvoidanceInput2D
    {
        public LocalAvoidanceInput2D(
            int agentId,
            Vector2 position,
            Vector2 velocity,
            Vector2 preferredVelocity,
            float maxSpeed,
            float radius,
            float mass,
            float priority,
            float contactStiffness,
            float maxContactCorrection,
            float neighbourDistance,
            float timeHorizon,
            int maxNeighbours,
            bool canMove)
        {
            AgentId = agentId;
            Position = position;
            Velocity = velocity;
            PreferredVelocity = preferredVelocity;
            MaxSpeed = Mathf.Max(maxSpeed, 0.0f);
            Radius = Mathf.Max(radius, 0.001f);
            Mass = Mathf.Max(mass, 0.001f);
            Priority = Mathf.Max(priority, 0.001f);
            ContactStiffness = Mathf.Clamp01(contactStiffness);
            MaxContactCorrection = Mathf.Max(maxContactCorrection, 0.0f);
            NeighbourDistance = Mathf.Max(neighbourDistance, Radius * 2.0f);
            TimeHorizon = Mathf.Max(timeHorizon, 0.01f);
            MaxNeighbours = Mathf.Max(maxNeighbours, 0);
            CanMove = canMove;
        }

        public int AgentId { get; }
        public Vector2 Position { get; }
        public Vector2 Velocity { get; }
        public Vector2 PreferredVelocity { get; }
        public float MaxSpeed { get; }
        public float Radius { get; }
        public float Mass { get; }
        public float Priority { get; }
        public float ContactStiffness { get; }
        public float MaxContactCorrection { get; }
        public float NeighbourDistance { get; }
        public float TimeHorizon { get; }
        public int MaxNeighbours { get; }
        public bool CanMove { get; }
        public float InverseContactMass => CanMove ? 1.0f / Mass : 0.0f;

        public float PredictiveTimeHorizon
        {
            get
            {
                float resistance = Mathf.Sqrt(Mass * Priority);
                return Mathf.Clamp(TimeHorizon / resistance, 0.05f, TimeHorizon * 4.0f);
            }
        }
    }

    public sealed class Rvo2LocalAvoidanceBackend2D : ILocalAvoidanceBackend2D
    {
        private static Rvo2LocalAvoidanceBackend2D s_owner;

        private readonly Dictionary<int, int> m_rvoAgentByRuntimeId = new();
        private readonly HashSet<int> m_liveRuntimeIds = new();
        private readonly List<int> m_removedRuntimeIds = new();
        private readonly RVO.Simulator m_simulator;
        private bool m_disposed;

        public Rvo2LocalAvoidanceBackend2D()
        {
            if (s_owner != null && !s_owner.m_disposed)
            {
                throw new InvalidOperationException(
                    "RVO2 uses one global Simulator instance. Dispose the active Rvo2LocalAvoidanceBackend2D before creating another world backend.");
            }

            s_owner = this;
            m_simulator = RVO.Simulator.Instance;
            try
            {
                m_simulator.Clear();
                m_simulator.SetNumWorkers(Mathf.Clamp(Environment.ProcessorCount - 1, 1, 8));
            }
            catch
            {
                s_owner = null;
                throw;
            }
        }

        public void Resolve(IReadOnlyList<LocalAvoidanceInput2D> inputs, Vector2[] safeVelocities, float deltaTime)
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(Rvo2LocalAvoidanceBackend2D));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (safeVelocities == null || safeVelocities.Length < inputs.Count)
            {
                throw new ArgumentException("Safe velocity buffer is smaller than the RVO input batch.", nameof(safeVelocities));
            }

            m_simulator.setTimeStep(Mathf.Max(deltaTime, 0.0001f));
            SynchronizeAgents(inputs);
            if (inputs.Count == 0)
            {
                return;
            }

            m_simulator.doStep();
            for (int i = 0; i < inputs.Count; i++)
            {
                int rvoId = m_rvoAgentByRuntimeId[inputs[i].AgentId];
                RvoVector2 velocity = m_simulator.getAgentVelocity(rvoId);
                safeVelocities[i] = new Vector2(velocity.x(), velocity.y());
            }
        }

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            m_rvoAgentByRuntimeId.Clear();
            m_liveRuntimeIds.Clear();
            m_removedRuntimeIds.Clear();
            m_simulator.Clear();
            if (s_owner == this) s_owner = null;
        }

        private void SynchronizeAgents(IReadOnlyList<LocalAvoidanceInput2D> inputs)
        {
            m_liveRuntimeIds.Clear();
            for (int i = 0; i < inputs.Count; i++)
            {
                LocalAvoidanceInput2D input = inputs[i];
                if (!m_liveRuntimeIds.Add(input.AgentId))
                {
                    throw new InvalidOperationException($"RVO2 received duplicate agent ID {input.AgentId}.");
                }

                if (!m_rvoAgentByRuntimeId.TryGetValue(input.AgentId, out int rvoId))
                {
                    rvoId = m_simulator.addAgent(
                        ToRvo(input.Position),
                        input.NeighbourDistance,
                        input.MaxNeighbours,
                        input.PredictiveTimeHorizon,
                        input.PredictiveTimeHorizon,
                        input.Radius,
                        input.MaxSpeed,
                        ToRvo(input.Velocity));
                    m_rvoAgentByRuntimeId.Add(input.AgentId, rvoId);
                }

                float maxSpeed = input.CanMove ? input.MaxSpeed : 0.0f;
                m_simulator.setAgentPosition(rvoId, ToRvo(input.Position));
                m_simulator.setAgentVelocity(rvoId, ToRvo(input.Velocity));
                m_simulator.setAgentPrefVelocity(rvoId, ToRvo(input.CanMove ? input.PreferredVelocity : Vector2.zero));
                m_simulator.setAgentMaxSpeed(rvoId, maxSpeed);
                m_simulator.setAgentMaxNeighbors(rvoId, input.MaxNeighbours);
                m_simulator.setAgentNeighborDist(rvoId, input.NeighbourDistance);
                m_simulator.setAgentRadius(rvoId, input.Radius);
                m_simulator.setAgentTimeHorizon(rvoId, input.PredictiveTimeHorizon);
                m_simulator.setAgentTimeHorizonObst(rvoId, input.PredictiveTimeHorizon);
            }

            m_removedRuntimeIds.Clear();
            foreach (KeyValuePair<int, int> pair in m_rvoAgentByRuntimeId)
            {
                if (!m_liveRuntimeIds.Contains(pair.Key))
                {
                    m_simulator.delAgent(pair.Value);
                    m_removedRuntimeIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < m_removedRuntimeIds.Count; i++)
            {
                m_rvoAgentByRuntimeId.Remove(m_removedRuntimeIds[i]);
            }
        }

        private static RvoVector2 ToRvo(Vector2 value) => new(value.x, value.y);
    }
}
