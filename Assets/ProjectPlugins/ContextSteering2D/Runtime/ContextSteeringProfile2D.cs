using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSteering2D
{
    public enum SteeringContextCombineMode2D
    {
        WeightedSumMinusConstraints,
        MaximumInterestMinusConstraints,
    }

    public enum SteeringDirectionSelectionMode2D
    {
        WeightedBlend,
        HighestScore,
    }

    [Serializable]
    public sealed class SteeringBehaviourGroup2D
    {
        [SerializeField] private string m_stableId = "default";
        [SerializeField] private string m_displayName = "Default";
        [SerializeReference] private List<SteeringBehaviour2D> m_behaviours = new()
        {
            new SeekSteeringBehaviour2D(),
            new ArriveSteeringBehaviour2D(),
            new ObstacleAvoidanceSteeringBehaviour2D(),
            new SeparationSteeringBehaviour2D(),
            new SideStepSteeringBehaviour2D(),
        };

        public string StableId => m_stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(m_displayName) ? m_stableId : m_displayName;
        public IReadOnlyList<SteeringBehaviour2D> Behaviours => m_behaviours;

        internal static SteeringBehaviourGroup2D CreateTransitGroup()
        {
            return new SteeringBehaviourGroup2D
            {
                m_stableId = "transit",
                m_displayName = "Transit",
                m_behaviours = new List<SteeringBehaviour2D>
                {
                    new SeekSteeringBehaviour2D(),
                    new ObstacleAvoidanceSteeringBehaviour2D(),
                    new SeparationSteeringBehaviour2D(),
                    new SideStepSteeringBehaviour2D(),
                }
            };
        }

        internal static SteeringBehaviourGroup2D CreatePredictiveTargetGroup()
        {
            return new SteeringBehaviourGroup2D
            {
                m_stableId = "predictive-target",
                m_displayName = "Predictive Target",
                m_behaviours = new List<SteeringBehaviour2D>
                {
                    new PursuitSteeringBehaviour2D(),
                    new ArriveSteeringBehaviour2D(),
                    new ObstacleAvoidanceSteeringBehaviour2D(),
                    new SeparationSteeringBehaviour2D(),
                    new SideStepSteeringBehaviour2D(),
                }
            };
        }

        internal static SteeringBehaviourGroup2D CreateOrbitGroup()
        {
            return new SteeringBehaviourGroup2D
            {
                m_stableId = "orbit",
                m_displayName = "Orbit",
                m_behaviours = new List<SteeringBehaviour2D>
                {
                    new OrbitSteeringBehaviour2D(),
                    new ObstacleAvoidanceSteeringBehaviour2D(),
                    new SeparationSteeringBehaviour2D(),
                    new SideStepSteeringBehaviour2D(),
                }
            };
        }

        internal static SteeringBehaviourGroup2D CreateCombatWanderGroup()
        {
            return new SteeringBehaviourGroup2D
            {
                m_stableId = "combat-wander",
                m_displayName = "Combat Wander",
                m_behaviours = new List<SteeringBehaviour2D>
                {
                    new ObstacleAvoidanceSteeringBehaviour2D(),
                    new CombatWanderSteeringBehaviour2D(),
                    new SeparationSteeringBehaviour2D(),
                }
            };
        }

        internal void Validate(string profileName)
        {
            if (string.IsNullOrWhiteSpace(m_stableId))
            {
                throw new InvalidOperationException($"Steering profile '{profileName}' contains a behaviour group without a stable ID.");
            }

            if (m_behaviours == null || m_behaviours.Count == 0)
            {
                throw new InvalidOperationException($"Steering profile '{profileName}' group '{m_stableId}' has no behaviours.");
            }

            HashSet<string> behaviourIds = new(StringComparer.Ordinal);
            for (int i = 0; i < m_behaviours.Count; i++)
            {
                SteeringBehaviour2D behaviour = m_behaviours[i];
                if (behaviour == null)
                {
                    throw new InvalidOperationException($"Steering profile '{profileName}' group '{m_stableId}' contains a null behaviour at index {i}.");
                }

                behaviour.Validate(profileName, m_stableId);
                if (!behaviourIds.Add(behaviour.StableId))
                {
                    throw new InvalidOperationException($"Steering profile '{profileName}' group '{m_stableId}' contains duplicate behaviour ID '{behaviour.StableId}'.");
                }
            }
        }
    }

    [CreateAssetMenu(menuName = "Context Steering 2D/Profile", fileName = "ContextSteeringProfile2D")]
    public sealed class ContextSteeringProfile2D : ScriptableObject
    {
        public const string DefaultGroupId = "default";

        [Header("Sampling")]
        [SerializeField, Min(4)] private int m_sampleCount = 16;
        [SerializeField] private SteeringContextCombineMode2D m_combineMode = SteeringContextCombineMode2D.WeightedSumMinusConstraints;
        [SerializeField] private SteeringDirectionSelectionMode2D m_selectionMode = SteeringDirectionSelectionMode2D.WeightedBlend;

        [Header("Body")]
        [SerializeField, Min(0.01f)] private float m_agentRadius = 0.35f;
        [SerializeField, Min(0.01f)] private float m_maxSpeed = 1.0f;
        [SerializeField, Min(0.01f)] private float m_mass = 1.0f;
        [SerializeField, Min(0.01f)] private float m_avoidancePriority = 1.0f;

        [Header("Detection")]
        [SerializeField, Min(0.0f)] private float m_obstacleProbeRadius = 1.0f;
        [SerializeField, Min(0.0f)] private float m_neighbourRadius = 1.0f;

        [Header("Local Avoidance")]
        [SerializeField, Min(0.0f)] private float m_timeHorizon = 0.75f;
        [SerializeField, Min(0)] private int m_maxNeighbours = 12;
        [SerializeField, Range(0.0f, 1.0f)] private float m_contactStiffness = 1.0f;
        [SerializeField, Min(0.0f)] private float m_maxContactCorrection = 0.25f;

        [Header("Behaviour Groups")]
        [SerializeField] private string m_defaultGroupId = DefaultGroupId;
        [SerializeField] private List<SteeringBehaviourGroup2D> m_behaviourGroups = new()
        {
            new SteeringBehaviourGroup2D(),
            SteeringBehaviourGroup2D.CreateTransitGroup(),
            SteeringBehaviourGroup2D.CreatePredictiveTargetGroup(),
            SteeringBehaviourGroup2D.CreateCombatWanderGroup(),
            SteeringBehaviourGroup2D.CreateOrbitGroup(),
        };

        [Header("Debug")]
        [SerializeField] private bool m_drawDebug = true;

        public int SampleCount => Mathf.Max(m_sampleCount, 4);
        public SteeringContextCombineMode2D CombineMode => m_combineMode;
        public SteeringDirectionSelectionMode2D SelectionMode => m_selectionMode;
        public float AgentRadius => Mathf.Max(m_agentRadius, 0.01f);
        public float MaxSpeed => Mathf.Max(m_maxSpeed, 0.01f);
        public float Mass => Mathf.Max(m_mass, 0.01f);
        public float AvoidancePriority => Mathf.Max(m_avoidancePriority, 0.01f);
        public float ObstacleProbeRadius => Mathf.Max(m_obstacleProbeRadius, 0.0f);
        public float NeighbourRadius => Mathf.Max(m_neighbourRadius, 0.0f);
        public float TimeHorizon => Mathf.Max(m_timeHorizon, 0.0f);
        public int MaxNeighbours => Mathf.Max(m_maxNeighbours, 0);
        public float ContactStiffness => Mathf.Clamp01(m_contactStiffness);
        public float MaxContactCorrection => Mathf.Max(m_maxContactCorrection, 0.0f);
        public string DefaultGroupIdValue => m_defaultGroupId;
        public IReadOnlyList<SteeringBehaviourGroup2D> BehaviourGroups => m_behaviourGroups;
        public bool DrawDebug => m_drawDebug;

        public SteeringBehaviourGroup2D GetBehaviourGroup(string stableId)
        {
            string resolvedId = string.IsNullOrWhiteSpace(stableId) ? m_defaultGroupId : stableId;
            ValidateOrThrow();

            for (int i = 0; i < m_behaviourGroups.Count; i++)
            {
                SteeringBehaviourGroup2D group = m_behaviourGroups[i];
                if (string.Equals(group.StableId, resolvedId, StringComparison.Ordinal))
                {
                    return group;
                }
            }

            throw new InvalidOperationException($"Steering profile '{name}' does not contain behaviour group '{resolvedId}'.");
        }

        public void ValidateOrThrow()
        {
            if (m_behaviourGroups == null || m_behaviourGroups.Count == 0)
            {
                throw new InvalidOperationException($"Steering profile '{name}' has no behaviour groups.");
            }

            HashSet<string> groupIds = new(StringComparer.Ordinal);
            bool hasDefault = false;
            for (int i = 0; i < m_behaviourGroups.Count; i++)
            {
                SteeringBehaviourGroup2D group = m_behaviourGroups[i]
                    ?? throw new InvalidOperationException($"Steering profile '{name}' contains a null behaviour group at index {i}.");
                group.Validate(name);
                if (!groupIds.Add(group.StableId))
                {
                    throw new InvalidOperationException($"Steering profile '{name}' contains duplicate group ID '{group.StableId}'.");
                }

                hasDefault |= string.Equals(group.StableId, m_defaultGroupId, StringComparison.Ordinal);
            }

            if (!hasDefault)
            {
                throw new InvalidOperationException($"Steering profile '{name}' default group '{m_defaultGroupId}' does not exist.");
            }
        }
    }
}
