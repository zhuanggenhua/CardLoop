using System.Collections.Generic;
using UnityEngine;

namespace ContextSteering2D
{
    internal sealed class SteeringDetectionScheduler2D
    {
        private readonly Collider2D[] m_overlapBuffer;
        private readonly List<int> m_neighbourCandidates = new();

        public SteeringDetectionScheduler2D(int overlapBufferSize)
        {
            m_overlapBuffer = new Collider2D[Mathf.Max(overlapBufferSize, 8)];
        }

        public void BuildFrame(
            ContextSteeringAgentHandle2D agent,
            IReadOnlyDictionary<Collider2D, ContextSteeringAgentHandle2D> registeredColliders,
            IReadOnlyList<ContextSteeringAgentHandle2D> agents,
            AgentSpatialIndex2D agentIndex)
        {
            ContextSteeringProfile2D profile = agent.Profile;
            SteeringDetectionFrame2D frame = agent.Frame;
            frame.Reset(
                agent.Solver.DirectionSet,
                agent.AgentId,
                agent.Body.position,
                agent.Forward,
                agent.Body.linearVelocity,
                profile,
                agent.TargetPosition,
                agent.TargetVelocity,
                agent.ArrivalStopRadius,
                agent.WanderIntent);

            CollectRegisteredNeighbours(agent, agents, agentIndex, frame);

            float radius = Mathf.Max(profile.ObstacleProbeRadius, agent.SemanticQueryRadius);
            if (radius <= 0.0f)
            {
                return;
            }

            ContactFilter2D queryFilter = agent.UnionFilter;
            int count = Physics2D.OverlapCircle(agent.Body.position, radius, queryFilter, m_overlapBuffer);
            if (count >= m_overlapBuffer.Length)
            {
                throw new System.InvalidOperationException(
                    $"ContextSteering2D detection buffer ({m_overlapBuffer.Length}) is full near '{agent.Body.name}'. Increase the world simulation buffer size; results were not truncated silently.");
            }

            for (int i = 0; i < count; i++)
            {
                Collider2D collider = m_overlapBuffer[i];
                if (collider == null || collider.attachedRigidbody == agent.Body)
                {
                    continue;
                }

                if (agent.SemanticQueryRadius > 0.0f && Allows(agent.SemanticFilter, collider))
                {
                    agent.Frame.AddDetectedCollider(collider);
                }

                if (registeredColliders.ContainsKey(collider))
                {
                    continue;
                }

                if (profile.ObstacleProbeRadius <= 0.0f || !Allows(agent.ObstacleFilter, collider))
                {
                    continue;
                }

                Vector2 closestPoint = collider.ClosestPoint(agent.Body.position);
                bool isStatic = collider.attachedRigidbody == null || collider.attachedRigidbody.bodyType == RigidbodyType2D.Static;
                frame.AddObstacle(new SteeringObstacle2D(closestPoint, 0.0f, isStatic));
            }
        }

        private void CollectRegisteredNeighbours(
            ContextSteeringAgentHandle2D agent,
            IReadOnlyList<ContextSteeringAgentHandle2D> agents,
            AgentSpatialIndex2D agentIndex,
            SteeringDetectionFrame2D frame)
        {
            float radius = agent.Profile.NeighbourRadius;
            if (radius <= 0.0f) return;

            agentIndex.Collect(agent.Body.position, radius, m_neighbourCandidates);
            for (int i = 0; i < m_neighbourCandidates.Count; i++)
            {
                ContextSteeringAgentHandle2D neighbour = agents[m_neighbourCandidates[i]];
                if (neighbour == agent || !neighbour.Active || !AllowsLayer(agent.NeighbourFilter, neighbour.Body.gameObject.layer))
                {
                    continue;
                }

                frame.AddNeighbour(new SteeringBody2D(
                    neighbour.AgentId,
                    neighbour.Body.position,
                    neighbour.Profile.AgentRadius,
                    neighbour.Body.linearVelocity,
                    neighbour.Profile.Mass,
                    neighbour.Profile.AvoidancePriority));
            }
        }

        private static bool Allows(ContactFilter2D filter, Collider2D collider)
        {
            if (!filter.useTriggers && collider.isTrigger)
            {
                return false;
            }

            return !filter.useLayerMask || (filter.layerMask.value & (1 << collider.gameObject.layer)) != 0;
        }

        private static bool AllowsLayer(ContactFilter2D filter, int layer)
        {
            return !filter.useLayerMask || (filter.layerMask.value & (1 << layer)) != 0;
        }
    }
}
