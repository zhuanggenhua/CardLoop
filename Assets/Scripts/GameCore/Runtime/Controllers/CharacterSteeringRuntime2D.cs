using System;
using System.Collections.Generic;
using ContextSteering2D;
using UnityEngine;

namespace GameCore
{
    internal sealed class CharacterSteeringRuntime2D : IDisposable
    {
        private readonly CharacterBase m_character;
        private readonly Rigidbody2D m_body;
        private readonly ContextSteeringProfile2D m_profile;
        private readonly ContextSteeringDebugProbe2D m_debugProbe;
        private ContextSteeringAgentHandle2D m_handle;

        public CharacterSteeringRuntime2D(CharacterBase character, ContextSteeringProfile2D profile)
        {
            m_character = character ? character : throw new ArgumentNullException(nameof(character));
            if (!character.TryGetComponent(out m_body))
            {
                throw new InvalidOperationException($"Character '{character.name}' requires a Rigidbody2D before it can register with ContextSteering2D.");
            }

            m_profile = profile;
            if (m_profile == null)
            {
                throw new InvalidOperationException($"Character '{character.name}' has no ContextSteeringProfile2D assigned in AIController.");
            }

            character.TryGetComponent(out ContextSteeringDebugProbe2D debugProbe);
            m_debugProbe = debugProbe;
        }

        public SteeringResult2D LatestResult => m_handle != null ? m_handle.Result : default;
        public IReadOnlyList<Collider2D> DetectedColliders => m_handle != null ? m_handle.DetectedColliders : Array.Empty<Collider2D>();

        public void Submit(
            bool active,
            Vector2? targetPosition,
            Vector2 targetVelocity,
            Vector2 forward,
            string behaviourGroupId = null,
            float semanticQueryRadius = 0.0f,
            float arrivalStopRadius = -1.0f,
            float speedMultiplier = 1.0f,
            SteeringWanderIntent2D? wanderIntent = null)
        {
            EnsureRegistered();
            m_handle.SubmitIntent(
                active,
                targetPosition,
                targetVelocity,
                forward,
                behaviourGroupId,
                m_debugProbe != null && m_handle.Profile.DrawDebug,
                semanticQueryRadius,
                m_character.GetResolvedMoveSpeed() * Mathf.Max(speedMultiplier, 0.0f),
                arrivalStopRadius,
                wanderIntent);
        }

        public void ApplyLatestResult()
        {
            SteeringResult2D result = m_handle.Result;
            float maxSpeed = Mathf.Max(m_handle.MaxSpeed, 0.0001f);
            float safeSpeedScale = Mathf.Clamp01(result.SafeVelocity.magnitude / maxSpeed);
            m_character.SetSteeringMotion(safeSpeedScale, result.PushCorrection);
            m_character.SetSteeringMovementDirection(result.SafeDirection);
        }

        public void Stop()
        {
            if (m_handle != null)
            {
                Submit(false, null, Vector2.zero, ResolveCharacterForward());
            }
            m_character.SetSteeringMotion(1.0f, Vector2.zero);
            m_character.SetSteeringMovementDirection(Vector2.zero);
        }

        public void Dispose()
        {
            Stop();
            if (m_handle != null)
            {
                m_handle.DebugSnapshotPublished -= OnDebugSnapshotPublished;
                m_handle.Dispose();
                m_handle = null;
            }
        }

        private void EnsureRegistered()
        {
            if (m_handle != null)
            {
                return;
            }

            m_handle = ContextSteeringSimulation2D.RequireCurrent().Register(
                m_body,
                m_profile,
                GameManager.Config.collisionContactFilter,
                GameManager.Config.steeringNeighbourContactFilter,
                GameManager.Config.steeringNeighbourContactFilter);
            m_handle.DebugSnapshotPublished += OnDebugSnapshotPublished;
        }

        private Vector2 ResolveCharacterForward()
        {
            Vector2 lookAtDirection = m_character.GetLookAtDirection();
            if (lookAtDirection != Vector2.zero)
            {
                return lookAtDirection.normalized;
            }

            return m_character.transform.right;
        }

        private void OnDebugSnapshotPublished(SteeringDebugSnapshot2D snapshot)
        {
            if (m_debugProbe == null)
            {
                return;
            }

            if (snapshot != null)
            {
                m_debugProbe.Capture(snapshot);
            }
            else
            {
                m_debugProbe.Clear();
            }
        }
    }
}
