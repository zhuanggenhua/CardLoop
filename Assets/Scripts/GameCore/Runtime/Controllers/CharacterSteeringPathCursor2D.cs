using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    internal sealed class CharacterSteeringPathCursor2D
    {
        private Vector2[] m_path = Array.Empty<Vector2>();
        private int m_index;

        public bool HasPath => m_path.Length > 0;
        public bool HasPlannedDestination { get; private set; }
        public Vector2 PlannedDestination { get; private set; }

        public void SetPath(Vector2[] path, Vector2 plannedDestination)
        {
            m_path = path ?? throw new ArgumentNullException(nameof(path));
            m_index = 0;
            PlannedDestination = plannedDestination;
            HasPlannedDestination = true;
        }

        public bool HasDestinationMoved(Vector2 destination, float threshold)
        {
            if (!HasPlannedDestination)
            {
                return true;
            }

            float safeThreshold = Mathf.Max(threshold, 0.0f);
            return (destination - PlannedDestination).sqrMagnitude >= safeThreshold * safeThreshold;
        }

        public bool TryGetTarget(
            Vector2 currentPosition,
            float waypointTolerance,
            out Vector2 target,
            out bool isFinalTarget)
        {
            if (m_path.Length == 0)
            {
                target = default;
                isFinalTarget = false;
                return false;
            }

            float safeTolerance = Mathf.Max(waypointTolerance, 0.0f);
            while (m_index < m_path.Length - 1 &&
                Vector2.Distance(currentPosition, m_path[m_index]) <= safeTolerance)
            {
                m_index++;
            }

            target = m_path[Mathf.Min(m_index, m_path.Length - 1)];
            isFinalTarget = m_index >= m_path.Length - 1;
            return true;
        }

        public void Clear()
        {
            m_path = Array.Empty<Vector2>();
            m_index = 0;
            PlannedDestination = default;
            HasPlannedDestination = false;
        }
    }
}
