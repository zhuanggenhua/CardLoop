using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UIPlayerControlFeedback : MonoBehaviour
    {
        [SerializeField] private UIControllerButton m_interactionButtonFeedback = null;
        [SerializeField] private SpriteRenderer m_spriteRenderer = null;
        [SerializeField] private Vector3 m_offset = Vector3.up;
        [SerializeField] private float m_showAnimationSpeed = 20.0f;
        [SerializeField] private float m_hideAnimationSpeed = 20.0f;

        private Color m_initialSpriteColor = Color.white;
        private CharacterPlayerControl m_playerControl = null;
        private bool m_currentControlledCharacterListening = false;

        private void Awake()
        {
            if (m_spriteRenderer != null)
            {
                m_initialSpriteColor = m_spriteRenderer.color;
            }
        }

        private void OnEnable()
        {
            StartCurrentControlledCharacterListeningIfReady();
        }

        private void Start()
        {
            StartCurrentControlledCharacterListeningIfReady();
        }

        private void OnDisable()
        {
            StopCurrentControlledCharacterListening();
            m_playerControl = null;
        }

        private void OnDestroy()
        {
            StopCurrentControlledCharacterListening();
        }

        private void StartCurrentControlledCharacterListeningIfReady()
        {
            if (m_currentControlledCharacterListening)
            {
                return;
            }

            if (!GameManager.Exists() || !GameManager.HasSystem<PlayerSystem>())
            {
                return;
            }

            m_currentControlledCharacterListening = true;
            GameManager.PlayerSystem.AddCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            OnCurrentControlledCharacterChanged(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance());
        }

        private void StopCurrentControlledCharacterListening()
        {
            if (!m_currentControlledCharacterListening)
            {
                return;
            }

            m_currentControlledCharacterListening = false;
            if (GameManager.Exists() && GameManager.HasSystem<PlayerSystem>())
            {
                GameManager.PlayerSystem.RemoveCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        private void OnCurrentControlledCharacterChanged(CharacterBase character)
        {
            m_playerControl = character != null
                ? character.GetComponent<CharacterPlayerControl>()
                : null;
        }

        private void Update()
        {
            if (m_interactionButtonFeedback == null || m_spriteRenderer == null)
            {
                return;
            }

            if (m_playerControl == null)
            {
                m_interactionButtonFeedback.gameObject.SetActive(false);
                return;
            }

            if (m_playerControl.TryGetCurrentInteractionTargetPosition(out Vector3 targetPosition))
            {
                if (!m_interactionButtonFeedback.isActiveAndEnabled)
                {
                    m_interactionButtonFeedback.gameObject.SetActive(true);
                }

                m_interactionButtonFeedback.transform.position = targetPosition + m_offset;
                m_spriteRenderer.color = Color.Lerp(m_spriteRenderer.color, m_initialSpriteColor, m_showAnimationSpeed * Time.unscaledDeltaTime);
            }
            else
            {
                m_spriteRenderer.color = Color.Lerp(m_spriteRenderer.color, new Color(1.0f, 1.0f, 1.0f, 0.0f), m_hideAnimationSpeed * Time.unscaledDeltaTime);
            }
        }
    }
}
