using System.Collections;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using YokiFrame;
using azixMcAze.SerializableDictionary;

namespace GameCore
{
    public class UIHUDAbilityMessage : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] protected TextMeshProUGUI m_message = null;

        [Header("Animation Settings")]
        [Min(0.0f)]
        [SerializeField] protected float m_delayBeforeFadeOut = 1.5f;

        [Min(0.0f)]
        [SerializeField] protected float m_fadeOutDuration = 0.5f;

        [Header("Message Settings")]
        [SerializeField] private SerializableDictionary<EAbilityFireCheckResult, string> m_messages = null;

        private Coroutine m_hideCoroutine = null;

        private void Awake()
        {
            ResetVisualState();
        }

        /// <summary>
        /// 这是纯 HUD 提示层，不应在 UI 被隐藏或销毁后继续保留全局失败提示监听。
        /// </summary>
        private void OnEnable()
        {
            EventKit.Type.Register<PlayerAbilityFireFailedEvent>(OnPlayerAbilityFireFailed);
            EventKit.Type.Register<LocalPlayerCommandFailedEvent>(OnLocalPlayerCommandFailed);
        }

        private void OnDisable()
        {
            EventKit.Type.UnRegister<PlayerAbilityFireFailedEvent>(OnPlayerAbilityFireFailed);
            EventKit.Type.UnRegister<LocalPlayerCommandFailedEvent>(OnLocalPlayerCommandFailed);
            Hide();
        }

        private void OnDestroy()
        {
            Hide();
        }

        private void OnPlayerAbilityFireFailed(PlayerAbilityFireFailedEvent evt)
        {
            OnPlayerFireFailed(evt.Reason);
        }

        private void OnLocalPlayerCommandFailed(LocalPlayerCommandFailedEvent evt)
        {
            if (TryGetCommandFailureMessage(evt.Result, out string message))
            {
                Show(message);
            }
        }

        private void OnPlayerFireFailed(EAbilityFireCheckResult reason)
        {
            if (m_messages.ContainsKey(reason))
            {
                Show(m_messages[reason]);
                return;
            }
        }

        private static bool TryGetCommandFailureMessage(PlayerCommandResult result, out string message)
        {
            message = result.FailureReason switch
            {
                EPlayerCommandFailureReason.MissingInputTarget => ResolveNoControlledCharacterMessage(result.Request.Kind),
                EPlayerCommandFailureReason.InvalidControlledCharacter => ResolveNoControlledCharacterMessage(result.Request.Kind),
                EPlayerCommandFailureReason.InvalidTarget => ResolveInvalidTargetMessage(result.Request.Kind),
                EPlayerCommandFailureReason.ActorMismatch => "That character is not in the current control group.",
                EPlayerCommandFailureReason.ControlLocked => "I can't control that character right now.",
                EPlayerCommandFailureReason.InteractionLocked => ResolveInteractionLockedMessage(result.Request.Kind),
                EPlayerCommandFailureReason.MissingAbility => ResolveMissingAbilityMessage(result.Request.Kind),
                EPlayerCommandFailureReason.BlockedByState => ResolveBlockedByStateMessage(result.Request.Kind),
                EPlayerCommandFailureReason.NotRunning => ResolveNotRunningMessage(result.Request.Kind),
                _ => null
            };

            return !string.IsNullOrWhiteSpace(message);
        }

        private static string ResolveNoControlledCharacterMessage(EPlayerCommandKind kind)
        {
            return kind switch
            {
                EPlayerCommandKind.StopMove => null,
                EPlayerCommandKind.Move => null,
                _ => "No controllable character selected."
            };
        }

        private static string ResolveMissingAbilityMessage(EPlayerCommandKind kind)
        {
            return kind == EPlayerCommandKind.FireAbility
                ? "No ability equipped there."
                : null;
        }

        private static string ResolveInteractionLockedMessage(EPlayerCommandKind kind)
        {
            return kind == EPlayerCommandKind.Interact
                ? "I can't interact right now."
                : null;
        }

        private static string ResolveBlockedByStateMessage(EPlayerCommandKind kind)
        {
            return kind switch
            {
                EPlayerCommandKind.Interact => "Nothing to interact with.",
                EPlayerCommandKind.ClickMove => "I can't move there right now.",
                EPlayerCommandKind.OpenGameMenu => "I can't open that right now.",
                EPlayerCommandKind.FireAbility => "I can't cast right now.",
                _ => null
            };
        }

        private static string ResolveInvalidTargetMessage(EPlayerCommandKind kind)
        {
            return kind switch
            {
                EPlayerCommandKind.ClickMove => "No valid destination selected.",
                EPlayerCommandKind.Interact => "Nothing to interact with.",
                _ => null
            };
        }

        private static string ResolveNotRunningMessage(EPlayerCommandKind kind)
        {
            return kind switch
            {
                EPlayerCommandKind.OpenGameMenu => "I can't open that right now.",
                EPlayerCommandKind.FireAbility => "I can't cast right now.",
                _ => null
            };
        }

        public void Show(string message)
        {
            InterruptPreviousMessage();

            m_message.text = message;
            m_message.enabled = true;
            m_message.alpha = 1.0f;

            m_hideCoroutine = StartCoroutine(FadeOutAfterDelay(Mathf.Max(0.0f, m_delayBeforeFadeOut)));
        }

        private void InterruptPreviousMessage()
        {
            StopHideCoroutine();
        }

        private void StopHideCoroutine()
        {
            if (m_hideCoroutine != null)
            {
                StopCoroutine(m_hideCoroutine);
                m_hideCoroutine = null;
            }
        }

        private IEnumerator FadeOutAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            yield return FadeOut(Mathf.Max(0.0f, m_fadeOutDuration));

            m_hideCoroutine = null;
            ResetVisualState();
        }

        private IEnumerator FadeOut(float duration)
        {
            float elapsedTime = 0.0f;

            while (elapsedTime < duration)
            {
                m_message.alpha = math.lerp(1.0f, 0.0f, elapsedTime / duration);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            m_message.alpha = 0.0f;
        }

        private void Hide()
        {
            StopHideCoroutine();
            ResetVisualState();
        }

        private void ResetVisualState()
        {
            if (m_message == null)
            {
                return;
            }

            m_message.text = string.Empty;
            m_message.alpha = 0.0f;
            m_message.enabled = false;
        }
    }
}
