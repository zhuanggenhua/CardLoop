using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterBase))]
    public sealed class CharacterButtonActivation : MonoBehaviour
    {
        private static readonly List<Component> s_interactionReceiverComponents = new();

        [Header("Interaction")]
        [SerializeField] private CharacterBase m_character = null;
        [SerializeField] private Transform m_interactionPivot = null;
        [SerializeField] private float m_interactionDistance = 0.75f;
        [SerializeField] private AudioClipResolver m_interactionSound = null;

        private GameObject m_currentTarget = null;
        private bool m_interactedThisFrame = false;

        private Transform interactionPivot => m_interactionPivot != null ? m_interactionPivot : m_character.transform;

        public bool TryGetCurrentTargetPosition(out Vector3 position)
        {
            if (m_currentTarget != null)
            {
                position = m_currentTarget.transform.position;
                return true;
            }

            position = default;
            return false;
        }

        public bool HasInteractedThisFrame()
        {
            return m_interactedThisFrame;
        }

        public bool CanInteractNow()
        {
            return m_character && m_character.Can(EActionFlags.Interact);
        }

        public void ResetState()
        {
            m_interactedThisFrame = false;
            m_currentTarget = null;
        }

        public void RefreshCurrentTarget()
        {
            m_interactedThisFrame = false;
            m_currentTarget = ResolveInteractibleObject();
        }

        public bool TryInteract(GameObject explicitTarget = null)
        {
            GameObject currentTarget = ResolveInteractibleObject(explicitTarget);
            if (!TryDispatchInteraction(currentTarget))
            {
                return false;
            }

            m_interactedThisFrame = true;
            if (m_interactionSound)
            {
                YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(m_interactionSound));
            }

            return true;
        }

        private GameObject ResolveInteractibleObject(GameObject explicitTarget = null)
        {
            if (explicitTarget != null)
            {
                return explicitTarget;
            }

            if (!CanInteractNow())
            {
                return null;
            }

            Collider2D[] colliders = Physics2D.OverlapCircleAll(
                interactionPivot.position,
                m_interactionDistance,
                LayerMask.GetMask(GameManager.Config.interactionLayer));

            Array.Sort(colliders, (x, y) =>
                Vector3.Distance(interactionPivot.position, x.transform.position).CompareTo(
                    Vector3.Distance(interactionPivot.position, y.transform.position)));

            foreach (Collider2D collider in colliders)
            {
                Vector3 targetDirection = m_character.GetTargetDirection();
                Vector3 targetOffset = collider.transform.position + new Vector3(collider.offset.x, collider.offset.y, 0f) - interactionPivot.position;
                if (Vector3.Dot(targetDirection, targetOffset) > 0f)
                {
                    return collider.gameObject;
                }
            }

            return null;
        }

        private bool TryDispatchInteraction(GameObject currentInteractionTarget)
        {
            if (currentInteractionTarget == null)
            {
                return false;
            }

            s_interactionReceiverComponents.Clear();
            currentInteractionTarget.GetComponentsInParent(false, s_interactionReceiverComponents);

            bool dispatched = false;
            for (int i = 0; i < s_interactionReceiverComponents.Count; i++)
            {
                if (s_interactionReceiverComponents[i] is IInteractionReceiver interactionReceiver)
                {
                    interactionReceiver.OnInteract(m_character);
                    dispatched = true;
                }
            }

            return dispatched;
        }

        private void Awake()
        {
            EnsureCharacterReference();
        }

        private void Reset()
        {
            EnsureCharacterReference();
        }

        private void OnValidate()
        {
            EnsureCharacterReference();
        }

        private void EnsureCharacterReference()
        {
            if (m_character == null)
            {
                TryGetComponent(out m_character);
            }
        }
    }
}
