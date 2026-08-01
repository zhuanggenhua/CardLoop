using System;
using UnityEngine;

namespace ContextSteering2D
{
    [Serializable]
    public abstract class SteeringBehaviour2D
    {
        [SerializeField] private string m_stableId;
        [SerializeField] private bool m_enabled = true;
        [SerializeField, Min(0.0f)] private float m_weight = 1.0f;

        protected SteeringBehaviour2D(string stableId)
        {
            m_stableId = stableId;
        }

        public string StableId => m_stableId;
        public bool Enabled => m_enabled;
        public float Weight => Mathf.Max(m_weight, 0.0f);
        public abstract string DisplayName { get; }

        internal void Evaluate(SteeringDetectionFrame2D frame, SteeringContribution2D contribution)
        {
            contribution.Reset(m_stableId, DisplayName);
            if (!m_enabled || Weight <= 0.0f)
            {
                return;
            }

            EvaluateEnabled(frame, contribution, Weight);
        }

        internal void Validate(string profileName, string groupId)
        {
            if (string.IsNullOrWhiteSpace(m_stableId))
            {
                throw new InvalidOperationException($"Steering profile '{profileName}' group '{groupId}' contains a {GetType().Name} without a stable ID.");
            }
        }

        protected abstract void EvaluateEnabled(SteeringDetectionFrame2D frame, SteeringContribution2D contribution, float weight);

        protected static bool HasDirection(Vector2 direction)
        {
            return direction.sqrMagnitude > 0.0001f;
        }
    }
}
