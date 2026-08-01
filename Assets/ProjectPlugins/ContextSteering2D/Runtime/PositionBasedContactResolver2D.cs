using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSteering2D
{
    internal sealed class PositionBasedContactResolver2D
    {
        private readonly AgentSpatialIndex2D m_spatialIndex = new();
        private readonly List<int> m_candidates = new();
        private Vector2[] m_predictedPositions = Array.Empty<Vector2>();
        private Vector2[] m_iterationCorrections = Array.Empty<Vector2>();
        private int[] m_contactCounts = Array.Empty<int>();

        public void Resolve(
            IReadOnlyList<LocalAvoidanceInput2D> inputs,
            Vector2[] safeVelocities,
            Vector2[] contactCorrections,
            int iterations,
            float deltaTime)
        {
            int count = inputs.Count;
            if (safeVelocities.Length < count || contactCorrections.Length < count)
            {
                throw new ArgumentException("PBD contact buffers are smaller than the input batch.");
            }

            EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                m_predictedPositions[i] = inputs[i].Position + safeVelocities[i] * deltaTime;
                contactCorrections[i] = Vector2.zero;
            }

            int resolvedIterations = Mathf.Max(iterations, 1);
            for (int iteration = 0; iteration < resolvedIterations; iteration++)
            {
                Array.Clear(m_iterationCorrections, 0, count);
                Array.Clear(m_contactCounts, 0, count);
                m_spatialIndex.Build(m_predictedPositions, count, ResolveCellSize(inputs));

                for (int i = 0; i < count; i++)
                {
                    float queryRadius = inputs[i].Radius + m_spatialIndex.MaxRadius;
                    m_spatialIndex.Collect(m_predictedPositions[i], queryRadius, m_candidates);
                    for (int candidateIndex = 0; candidateIndex < m_candidates.Count; candidateIndex++)
                    {
                        int j = m_candidates[candidateIndex];
                        if (j <= i) continue;
                        AccumulatePair(inputs, i, j);
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    if (m_contactCounts[i] == 0) continue;
                    Vector2 correction = m_iterationCorrections[i] / m_contactCounts[i];
                    m_predictedPositions[i] += correction;
                    contactCorrections[i] += correction;
                }
            }

            for (int i = 0; i < count; i++)
            {
                float limit = inputs[i].MaxContactCorrection;
                if (limit > 0.0f)
                {
                    contactCorrections[i] = Vector2.ClampMagnitude(contactCorrections[i], limit);
                }
            }
        }

        private void AccumulatePair(IReadOnlyList<LocalAvoidanceInput2D> inputs, int first, int second)
        {
            LocalAvoidanceInput2D a = inputs[first];
            LocalAvoidanceInput2D b = inputs[second];
            Vector2 delta = m_predictedPositions[first] - m_predictedPositions[second];
            float distance = delta.magnitude;
            float penetration = a.Radius + b.Radius - distance;
            if (penetration <= 0.0f) return;

            Vector2 normal;
            if (distance > 0.0001f)
            {
                normal = delta / distance;
            }
            else
            {
                if (a.AgentId == b.AgentId)
                {
                    throw new InvalidOperationException($"PBD contact resolver received duplicate agent ID {a.AgentId}.");
                }
                normal = a.AgentId < b.AgentId ? Vector2.left : Vector2.right;
            }

            float inverseA = a.InverseContactMass;
            float inverseB = b.InverseContactMass;
            float inverseSum = inverseA + inverseB;
            if (inverseSum <= 0.0001f) return;

            float stiffness = Mathf.Min(a.ContactStiffness, b.ContactStiffness);
            Vector2 correction = normal * penetration * stiffness;
            m_iterationCorrections[first] += correction * (inverseA / inverseSum);
            m_iterationCorrections[second] -= correction * (inverseB / inverseSum);
            m_contactCounts[first]++;
            m_contactCounts[second]++;
        }

        private static float ResolveCellSize(IReadOnlyList<LocalAvoidanceInput2D> inputs)
        {
            float maxDiameter = 0.1f;
            for (int i = 0; i < inputs.Count; i++)
            {
                maxDiameter = Mathf.Max(maxDiameter, inputs[i].Radius * 2.0f);
            }
            return maxDiameter;
        }

        private void EnsureCapacity(int count)
        {
            if (m_predictedPositions.Length >= count) return;
            int capacity = Mathf.NextPowerOfTwo(Mathf.Max(count, 4));
            Array.Resize(ref m_predictedPositions, capacity);
            Array.Resize(ref m_iterationCorrections, capacity);
            Array.Resize(ref m_contactCounts, capacity);
        }
    }
}
