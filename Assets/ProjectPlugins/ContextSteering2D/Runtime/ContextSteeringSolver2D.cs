using System;
using UnityEngine;

namespace ContextSteering2D
{
    internal sealed class ContextSteeringSolver2D
    {
        private SteeringDirectionSet2D m_directionSet;
        private SteeringContext2D m_context;

        public ContextSteeringSolver2D(int sampleCount)
        {
            SetSampleCount(sampleCount);
        }

        public SteeringContext2D Context => m_context;
        public SteeringDirectionSet2D DirectionSet => m_directionSet;

        public void Prepare(ContextSteeringProfile2D profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            SetSampleCount(profile.SampleCount);
        }

        public SteeringResult2D Solve(
            SteeringDetectionFrame2D frame,
            ContextSteeringProfile2D profile,
            string groupId,
            float maxSpeed = -1.0f)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            Prepare(profile);
            SteeringBehaviourGroup2D group = profile.GetBehaviourGroup(groupId);
            int contributionCount = group.Behaviours.Count;
            for (int i = 0; i < contributionCount; i++)
            {
                group.Behaviours[i].Evaluate(frame, m_context.GetContribution(i));
            }

            SteeringSelection2D selection = m_context.Resolve(contributionCount, profile.CombineMode, profile.SelectionMode);
            float resolvedMaxSpeed = maxSpeed >= 0.0f ? maxSpeed : profile.MaxSpeed;
            Vector2 preferredVelocity = selection.Direction * resolvedMaxSpeed * selection.SpeedScale * selection.Strength;
            return new SteeringResult2D(
                selection.Direction,
                selection.SpeedScale,
                preferredVelocity,
                preferredVelocity,
                Vector2.zero,
                preferredVelocity);
        }

        private void SetSampleCount(int sampleCount)
        {
            if (m_directionSet != null && m_directionSet.Count == sampleCount)
            {
                return;
            }

            m_directionSet = new SteeringDirectionSet2D(sampleCount);
            if (m_context == null)
            {
                m_context = new SteeringContext2D(m_directionSet);
            }
            else
            {
                m_context.ResetDirectionSet(m_directionSet);
            }
        }
    }
}
