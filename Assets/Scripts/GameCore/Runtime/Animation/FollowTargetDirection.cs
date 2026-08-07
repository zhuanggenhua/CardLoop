using Unity.Mathematics;
using UnityEngine;

namespace GameCore
{
    class FollowTargetDirection : MonoBehaviour
    {
        public enum EFollowStrategy
        {
            FlipSprites,
            NegativeScale
        }

        // Inspector Settings
        [SerializeField] private CharacterBase m_target = null;
        [SerializeField] private EFollowStrategy m_strategy = EFollowStrategy.FlipSprites;
        [SerializeField] private SpriteRenderer[] m_toFlip = null;
        [SerializeField] private bool m_polydirectional = false;
        [SerializeField] private float m_angleSnapOffset = 0.0f;
        [SerializeField] private float m_angleSnap = 0.0f;

        // Private Members
        private Vector3 m_initialPosition;
        private bool m_targetDirectionListening = false;

        public void Awake()
        {
            m_initialPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            StartTargetDirectionListeningIfReady();
        }

        private void Start()
        {
            StartTargetDirectionListeningIfReady();
        }

        private void OnDisable()
        {
            StopTargetDirectionListening();
        }

        private void OnDestroy()
        {
            StopTargetDirectionListening();
        }

        public void OnTargetDirectionChanged(Vector2 direction)
        {
            switch (m_strategy)
            {
                case EFollowStrategy.FlipSprites:
                    ApplyFlipSpritesStrategy(direction);
                    break;
                case EFollowStrategy.NegativeScale:
                    ApplyNegativeScaleStrategy(direction);
                    break;
            }

            if (m_polydirectional)
            {
                ApplyPolydirectionalTransformations(direction);
            }
        }

        private void ApplyFlipSpritesStrategy(Vector2 direction)
        {
            float modifier = direction.x > 0.0f ? 1.0f : -1.0f;

            transform.localPosition = new Vector3(m_initialPosition.x * modifier, m_initialPosition.y, m_initialPosition.z);

            if (m_toFlip != null)
            {
                foreach (SpriteRenderer spriteRenderer in m_toFlip)
                {
                    spriteRenderer.flipX = direction.x < 0.0f;
                }
            }
        }

        private void ApplyNegativeScaleStrategy(Vector2 direction)
        {
            float modifier = direction.x > 0.0f ? 1.0f : -1.0f;
            transform.localScale = new Vector3(math.abs(transform.localScale.x) * modifier, transform.localScale.y, transform.localScale.z);
        }

        private void ApplyPolydirectionalTransformations(Vector2 direction)
        {
            float angle = Vector2.SignedAngle(direction.x > 0.0f ? Vector2.right : Vector2.left, direction);

            if (m_angleSnap > 0.0f)
            {
                angle = Mathf.Round((angle - m_angleSnapOffset) / m_angleSnap) * m_angleSnap + m_angleSnapOffset;
            }

            transform.localRotation = Quaternion.Euler(0.0f, 0.0f, angle);
        }

        private void StartTargetDirectionListeningIfReady()
        {
            if (m_targetDirectionListening || m_target == null)
            {
                return;
            }

            m_targetDirectionListening = true;
            m_target.AddTargetDirectionChangedListener(OnTargetDirectionChanged);
            OnTargetDirectionChanged(m_target.GetTargetDirection());
        }

        private void StopTargetDirectionListening()
        {
            if (!m_targetDirectionListening)
            {
                return;
            }

            m_targetDirectionListening = false;
            if (m_target != null)
            {
                m_target.RemoveTargetDirectionChangedListener(OnTargetDirectionChanged);
            }
        }
    }
}

