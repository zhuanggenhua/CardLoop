using System.Collections;
using TMPro;
using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UITipsItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_text = null;
        [SerializeField] private float m_fadeInDuration = 0.2f;
        [SerializeField] private float m_visibleDuration = 1.5f;
        [SerializeField] private float m_fadeOutDuration = 0.25f;

        private CanvasGroup m_canvasGroup;
        private Coroutine m_showCoroutine;

        private void Awake()
        {
            m_canvasGroup = GetComponent<CanvasGroup>();
            if (m_text == null)
            {
                m_text = GetComponentInChildren<TMP_Text>();
            }
        }

        private void OnDisable()
        {
            StopShowRoutine();
            ResetVisualState();
        }

        private void OnDestroy()
        {
            StopShowRoutine();
        }

        public void Show(string text)
        {
            if (m_text != null)
            {
                m_text.text = text ?? string.Empty;
            }

            StopShowRoutine();

            gameObject.SetActive(true);
            m_showCoroutine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            yield return Fade(0f, 1f, m_fadeInDuration);
            if (m_visibleDuration > 0f)
            {
                yield return CoroutineKit.WaitForSecondsRealtime(m_visibleDuration);
            }
            yield return Fade(1f, 0f, m_fadeOutDuration);
            m_showCoroutine = null;

            if (!GameObjectPoolService.Return(gameObject))
            {
                gameObject.SetActive(false);
            }
        }

        private void StopShowRoutine()
        {
            if (m_showCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_showCoroutine);
            m_showCoroutine = null;
        }

        private void ResetVisualState()
        {
            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 0f;
            }

            if (m_text != null)
            {
                m_text.text = string.Empty;
            }
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (m_canvasGroup == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                m_canvasGroup.alpha = to;
                yield break;
            }

            var elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                m_canvasGroup.alpha = Mathf.Lerp(from, to, elapsedTime / duration);
                yield return null;
            }

            m_canvasGroup.alpha = to;
        }
    }
}
