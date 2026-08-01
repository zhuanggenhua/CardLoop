using UnityEngine;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UINavigationCursor : MonoBehaviour
    {
        [SerializeField] private Image m_image = null;

        private UINavigationCursorTarget m_target = null;

        private Vector3 GetTargetPosition()
        {
            return m_target.transform.position + m_target.totalPositionOffset;
        }

        private Vector2 GetTargetSize()
        {
            return ((RectTransform)m_target.transform).sizeDelta + m_target.totalSizeOffset;
        }

        private void OnTargetDestroyed()
        {
            SetTarget(null);
        }

        private void SetTarget(UINavigationCursorTarget currentTarget)
        {
            UINavigationCursorTarget previousTarget = m_target;

            // If target changed
            if (currentTarget != previousTarget)
            {
                if (previousTarget != null)
                {
                    previousTarget.RemoveDestroyedListener(OnTargetDestroyed);
                }

                m_target = currentTarget;


                // New valid target found
                if (m_target != null)
                {
                    m_target.AddDestroyedListener(OnTargetDestroyed);

                    ((RectTransform)transform).sizeDelta = GetTargetSize();
                    transform.position = GetTargetPosition();

                    m_image.enabled = true;
                    m_image.sprite = m_target.navigationCursorStyle.sprite;
                    m_image.color = m_target.navigationCursorStyle.color;
                }
                else
                // No target
                {
                    m_image.enabled = false;
                }
            }
        }

        private void Update()
        {
            UINavigationCursorTarget currentTarget = null;

            if (GameManager.EventSystem.currentSelectedGameObject != null)
            {
                currentTarget = GameManager.EventSystem.currentSelectedGameObject?.GetComponent<UINavigationCursorTarget>();
            }

            // Ignore disabled targets
            if (currentTarget == null || !currentTarget.isActiveAndEnabled)
            {
                currentTarget = null;
            }

            SetTarget(currentTarget);
        }
    }
}

