using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace FantasyWord.GameCore
{
    [System.Flags]
    public enum EDamageScreenFlashSources
    {
        None = 0,
        PlayerReceiveDamage = 1 << 0,
        AnyCharacterReceiveDamageFromPlayer = 1 << 1
    }

    /// <summary>
    /// 受击屏幕闪屏表现入口。
    /// 它只消费 GameRuntimeEvents 派发的正式受击表现事件，不直接读取伤害通知或 TopDown Manager。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(Image))]
    public sealed class DamageScreenFlash : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("承载闪屏透明度的 CanvasGroup。通常就是当前对象自己。")]
        [SerializeField] private CanvasGroup m_canvasGroup = null;

        [Tooltip("承载闪屏颜色的全屏 Image。通常就是当前对象自己。")]
        [SerializeField] private Image m_overlayImage = null;

        [Header("Trigger Sources")]
        [Tooltip("哪些正式受击来源可以触发闪屏。")]
        [SerializeField] private EDamageScreenFlashSources m_sources = EDamageScreenFlashSources.PlayerReceiveDamage;

        [Header("Flash Settings")]
        [Tooltip("普通受击时的屏幕闪色。")]
        [SerializeField] private Color m_defaultColor = new(0.8f, 0.15f, 0.15f, 1.0f);

        [Tooltip("暴击受击时的屏幕闪色。")]
        [SerializeField] private Color m_criticalColor = new(1.0f, 1.0f, 1.0f, 1.0f);

        [Range(0.0f, 1.0f)]
        [Tooltip("普通受击时的最大透明度。")]
        [SerializeField] private float m_defaultAlpha = 0.18f;

        [Range(0.0f, 1.0f)]
        [Tooltip("暴击受击时的最大透明度。")]
        [SerializeField] private float m_criticalAlpha = 0.3f;

        [Min(0.01f)]
        [Tooltip("闪屏从峰值淡回透明的持续秒数。")]
        [SerializeField] private float m_duration = 0.15f;

        private Coroutine m_flashCoroutine = null;

        private void Awake()
        {
            AutoAssignReferences();
            ResetOverlay();
        }

        private void OnValidate()
        {
            AutoAssignReferences();

            if (!Application.isPlaying)
            {
                ResetOverlay();
            }
        }

        private void OnEnable()
        {
            EventKit.Type.Register<DamageTakenPresentationEvent>(OnDamageTakenPresentation);
        }

        private void OnDisable()
        {
            EventKit.Type.UnRegister<DamageTakenPresentationEvent>(OnDamageTakenPresentation);

            if (m_flashCoroutine != null)
            {
                StopCoroutine(m_flashCoroutine);
                m_flashCoroutine = null;
            }

            ResetOverlay();
        }

        private void AutoAssignReferences()
        {
            m_canvasGroup ??= GetComponent<CanvasGroup>();
            m_overlayImage ??= GetComponent<Image>();
        }

        private void ResetOverlay()
        {
            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 0.0f;
            }
        }

        private static bool TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter)
        {
            currentControlledCharacter = null;
            if (!GameManager.Exists() || !GameManager.TryGetSystem(out PlayerSystem playerSystem))
            {
                return false;
            }

            currentControlledCharacter = playerSystem.GetCurrentControlledCharacterOrPlayerInstance();
            return currentControlledCharacter != null;
        }

        private bool IsValidFlashSource(DamageTakenFeedbackContext context)
        {
            if (m_sources == EDamageScreenFlashSources.None)
            {
                return false;
            }

            if (!TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter))
            {
                return false;
            }

            return
                (m_sources.HasFlag(EDamageScreenFlashSources.PlayerReceiveDamage) && context.target == currentControlledCharacter) ||
                (m_sources.HasFlag(EDamageScreenFlashSources.AnyCharacterReceiveDamageFromPlayer) && context.sourceCharacter == currentControlledCharacter);
        }

        private void OnDamageTakenPresentation(DamageTakenPresentationEvent presentationEvent)
        {
            DamageTakenFeedbackContext context = presentationEvent.Context;

            if (m_overlayImage == null || m_canvasGroup == null)
            {
                return;
            }

            if (context.visualFlags.HasFlag(EEffectVisualFlags.NoScreenFlash) || context.damageInput.IsMissed || context.damageInput.silent || !IsValidFlashSource(context))
            {
                return;
            }

            bool isCriticalHit = context.damageInput.IsCriticalHit;
            Color color = isCriticalHit ? m_criticalColor : m_defaultColor;
            float alpha = isCriticalHit ? m_criticalAlpha : m_defaultAlpha;

            m_overlayImage.color = new Color(color.r, color.g, color.b, 1.0f);

            if (m_flashCoroutine != null)
            {
                StopCoroutine(m_flashCoroutine);
            }

            m_flashCoroutine = StartCoroutine(FlashCoroutine(alpha));
        }

        private IEnumerator FlashCoroutine(float startAlpha)
        {
            m_canvasGroup.alpha = startAlpha;

            float elapsedTime = 0.0f;

            while (elapsedTime < m_duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float ratio = Mathf.Clamp01(elapsedTime / m_duration);
                m_canvasGroup.alpha = Mathf.Lerp(startAlpha, 0.0f, ratio);
                yield return null;
            }

            m_flashCoroutine = null;
            ResetOverlay();
        }
    }
}
