using System;
using UnityEngine;

namespace ContextSteering2D
{
    [DisallowMultipleComponent]
    public sealed class ContextSteeringDebugProbe2D : MonoBehaviour
    {
        [Header("Scene View")]
        [SerializeField] private bool m_drawSceneView = true;
        [SerializeField] private bool m_drawWhenNotSelected = true;
        [SerializeField] private bool m_drawTarget = true;
        [SerializeField] private bool m_drawDetection = true;

        [Header("Context Map")]
        [SerializeField] private bool m_drawContributions = true;
        [SerializeField, HideInInspector] private string m_contributionFilter = string.Empty;
        [SerializeField] private bool m_drawConstraints = true;
        [SerializeField] private bool m_drawCombined = true;

        [Header("Result")]
        [SerializeField] private bool m_drawPreferredVelocity = true;
        [SerializeField] private bool m_drawSafeVelocity = true;
        [SerializeField] private bool m_drawPushCorrection = true;
        [SerializeField] private bool m_drawOverlay = true;

        public bool DrawSceneView
        {
            get => m_drawSceneView;
            set => m_drawSceneView = value;
        }

        public bool DrawWhenNotSelected
        {
            get => m_drawWhenNotSelected;
            set => m_drawWhenNotSelected = value;
        }
        public bool DrawTarget => m_drawTarget;
        public bool DrawDetection => m_drawDetection;
        public bool DrawContributions => m_drawContributions;
        public string ContributionFilter => m_contributionFilter;
        public bool DrawConstraints => m_drawConstraints;
        public bool DrawCombined => m_drawCombined;
        public bool DrawPreferredVelocity => m_drawPreferredVelocity;
        public bool DrawSafeVelocity => m_drawSafeVelocity;
        public bool DrawPushCorrection => m_drawPushCorrection;
        public bool DrawOverlay => m_drawOverlay;

        public SteeringDebugSnapshot2D Snapshot { get; private set; }
        public bool HasSnapshot => Snapshot != null;
        public float MaximumObservedPushCorrectionSqrMagnitude { get; private set; }

        public void Capture(SteeringDebugSnapshot2D snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            MaximumObservedPushCorrectionSqrMagnitude = Mathf.Max(
                MaximumObservedPushCorrectionSqrMagnitude,
                snapshot.Result.PushCorrection.sqrMagnitude);
        }

        public void Clear()
        {
            Snapshot = null;
        }

        public void ResetHistory()
        {
            MaximumObservedPushCorrectionSqrMagnitude = 0.0f;
        }

    }
}
