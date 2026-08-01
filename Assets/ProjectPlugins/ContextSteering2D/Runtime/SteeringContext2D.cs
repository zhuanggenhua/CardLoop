using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSteering2D
{
    public sealed class SteeringContribution2D
    {
        private readonly float[] m_interest;
        private readonly float[] m_constraint;

        public SteeringContribution2D(int sampleCount)
        {
            m_interest = new float[sampleCount];
            m_constraint = new float[sampleCount];
        }

        public string StableId { get; private set; }
        public string DisplayName { get; private set; }
        public ReadOnlySpan<float> Interest => m_interest;
        public ReadOnlySpan<float> Constraint => m_constraint;
        public bool HasSpeedScale { get; private set; }
        public float SpeedScale { get; private set; } = 1.0f;

        internal void Reset(string stableId, string displayName)
        {
            StableId = stableId;
            DisplayName = displayName;
            Array.Clear(m_interest, 0, m_interest.Length);
            Array.Clear(m_constraint, 0, m_constraint.Length);
            HasSpeedScale = false;
            SpeedScale = 1.0f;
        }

        public void AddInterest(SteeringDirectionSet2D directions, Vector2 direction, float weight)
        {
            AddDirectionalValues(directions, direction, weight, m_interest);
        }

        public void AddConstraint(SteeringDirectionSet2D directions, Vector2 direction, float weight)
        {
            AddDirectionalValues(directions, direction, weight, m_constraint);
        }

        public void LimitSpeed(float scale)
        {
            HasSpeedScale = true;
            SpeedScale = Mathf.Min(SpeedScale, Mathf.Clamp01(scale));
        }

        private static void AddDirectionalValues(SteeringDirectionSet2D directions, Vector2 direction, float weight, float[] values)
        {
            if (direction.sqrMagnitude <= 0.0001f || weight <= 0.0f)
            {
                return;
            }

            Vector2 normalized = direction.normalized;
            for (int i = 0; i < directions.Count; i++)
            {
                float dot = Vector2.Dot(normalized, directions[i]);
                if (dot > 0.0f)
                {
                    values[i] = Mathf.Max(values[i], dot * weight);
                }
            }
        }
    }

    public readonly struct SteeringSelection2D
    {
        public SteeringSelection2D(Vector2 direction, float strength, float speedScale)
        {
            Direction = direction;
            Strength = Mathf.Clamp01(strength);
            SpeedScale = Mathf.Clamp01(speedScale);
        }

        public Vector2 Direction { get; }
        public float Strength { get; }
        public float SpeedScale { get; }
    }

    public sealed class SteeringContext2D
    {
        private readonly List<SteeringContribution2D> m_contributions = new();
        private float[] m_interest;
        private float[] m_constraint;
        private float[] m_combined;

        public SteeringContext2D(SteeringDirectionSet2D directionSet)
        {
            ResetDirectionSet(directionSet);
        }

        public SteeringDirectionSet2D DirectionSet { get; private set; }
        public IReadOnlyList<SteeringContribution2D> Contributions => m_contributions;
        public ReadOnlySpan<float> Interest => m_interest;
        public ReadOnlySpan<float> Constraint => m_constraint;
        public ReadOnlySpan<float> Combined => m_combined;

        public void ResetDirectionSet(SteeringDirectionSet2D directionSet)
        {
            DirectionSet = directionSet ?? throw new ArgumentNullException(nameof(directionSet));
            m_interest = new float[directionSet.Count];
            m_constraint = new float[directionSet.Count];
            m_combined = new float[directionSet.Count];
            m_contributions.Clear();
        }

        public SteeringContribution2D GetContribution(int index)
        {
            while (m_contributions.Count <= index)
            {
                m_contributions.Add(new SteeringContribution2D(DirectionSet.Count));
            }

            return m_contributions[index];
        }

        public SteeringSelection2D Resolve(int contributionCount, SteeringContextCombineMode2D combineMode, SteeringDirectionSelectionMode2D selectionMode)
        {
            Array.Clear(m_interest, 0, m_interest.Length);
            Array.Clear(m_constraint, 0, m_constraint.Length);
            Array.Clear(m_combined, 0, m_combined.Length);

            float speedScale = 1.0f;
            for (int contributionIndex = 0; contributionIndex < contributionCount; contributionIndex++)
            {
                SteeringContribution2D contribution = m_contributions[contributionIndex];
                if (contribution.HasSpeedScale)
                {
                    speedScale = Mathf.Min(speedScale, contribution.SpeedScale);
                }

                for (int directionIndex = 0; directionIndex < DirectionSet.Count; directionIndex++)
                {
                    float interest = contribution.Interest[directionIndex];
                    m_interest[directionIndex] = combineMode == SteeringContextCombineMode2D.MaximumInterestMinusConstraints
                        ? Mathf.Max(m_interest[directionIndex], interest)
                        : m_interest[directionIndex] + interest;
                    m_constraint[directionIndex] = Mathf.Max(m_constraint[directionIndex], contribution.Constraint[directionIndex]);
                }
            }

            int highestIndex = -1;
            float highestScore = 0.0f;
            Vector2 weightedDirection = Vector2.zero;
            for (int i = 0; i < DirectionSet.Count; i++)
            {
                float score = Mathf.Clamp01(m_interest[i]) * (1.0f - Mathf.Clamp01(m_constraint[i]));
                m_combined[i] = score;
                weightedDirection += DirectionSet[i] * score;
                if (score > highestScore)
                {
                    highestScore = score;
                    highestIndex = i;
                }
            }

            Vector2 direction = selectionMode == SteeringDirectionSelectionMode2D.HighestScore
                ? (highestIndex >= 0 ? DirectionSet[highestIndex] : Vector2.zero)
                : (weightedDirection.sqrMagnitude > 0.0001f ? weightedDirection.normalized : Vector2.zero);
            return new SteeringSelection2D(direction, highestScore, speedScale);
        }
    }
}
