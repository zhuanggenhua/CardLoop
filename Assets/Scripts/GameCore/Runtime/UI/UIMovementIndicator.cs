using UnityEngine;

namespace GameCore
{
    public class UIMovementIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Movable m_target = null;
        [SerializeField] private SpriteRenderer m_sprite = null;

        [Header("Settings")]
        [SerializeField] private bool m_autoHide = true;
        [SerializeField] private float m_transitionSpeed = 20.0f;

        private Color m_initialColor;
        private Color m_hiddenColor;

        private void Start()
        {
            m_initialColor = m_sprite.color;
            m_hiddenColor = new(m_initialColor.r, m_initialColor.g, m_initialColor.b, 0);
        }

        private void Update()
        {
            if (m_autoHide)
            {
                Color targetColor = m_target.CanUpdateTargetDirection() ? m_initialColor : m_hiddenColor;
                m_sprite.color = Color.Lerp(m_sprite.color, targetColor, Time.unscaledDeltaTime * m_transitionSpeed);
            }
        }
    }
}

