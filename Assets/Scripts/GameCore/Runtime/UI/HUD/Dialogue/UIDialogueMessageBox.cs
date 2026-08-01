using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UIDialogueMessageBox : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private string m_animationParameter = "visible";

        [Header("Audio")]
        [SerializeField] private AudioClipResolver m_dialogueBlipAudio;

        [Header("References")]
        [SerializeField] private TextMeshProUGUI m_text = null;
        [SerializeField] private UIDialogueSpeakerBox m_speakerBox = null;
        [SerializeField] private GameObject m_arrow = null;


        // Public Members
        private Queue<char> m_charQueue = null;

        // Private Members
        private bool m_hasAnimationParameter = false;
        private bool m_visible = false;
        private bool m_textAnimationInProgress = false;
        private bool m_showArrow = false;

        // Component References
        private Animator m_animator = null;
        private IDialogueHudEventReceiver m_receiver = null;
        private Coroutine m_textAnimationCoroutine = null;

        public void Show() => SetVisible(true);
        public void Hide()
        {
            AbortTextAnimation();
            SetVisible(false);
        }

        private void Awake()
        {
            m_animator = GetComponent<Animator>();
            m_receiver = GetComponentInParent<IDialogueHudEventReceiver>();
            Debug.Assert(m_receiver != null, $"{nameof(UIDialogueMessageBox)} 需要父级实现 {nameof(IDialogueHudEventReceiver)}。");

            if (m_animator)
            {
                m_hasAnimationParameter = AnimationUtils.HasParameter(m_animator, m_animationParameter);
            }
        }

        private void OnDisable()
        {
            AbortTextAnimation();
        }

        private void OnDestroy()
        {
            AbortTextAnimation();
        }

        public void SetText(string speaker, string text, bool showArrow)
        {
            AbortTextAnimation();
            m_speakerBox.SetText(speaker);
            m_showArrow = showArrow;
            m_text.text = string.Empty;
            m_charQueue = new Queue<char>(text ?? string.Empty);
            m_textAnimationCoroutine = StartCoroutine(UpdateText());
        }

        public void SetMargin(Vector4 margins)
        {
            m_text.margin = margins;
        }

        IEnumerator UpdateText()
        {
            OnTextAnimationStart();

            while (m_charQueue != null && m_charQueue.Count > 0)
            {
                char c = m_charQueue.Dequeue();

                m_text.text += c;

                if (!char.IsWhiteSpace(c))
                {
                    GameRuntimeEvents.RequestAudioPlayback(m_dialogueBlipAudio);
                }

                yield return new WaitForSecondsRealtime(0.05f);
            }

            m_textAnimationCoroutine = null;
            CompleteTextAnimation();
        }

        private void OnTextAnimationStart()
        {
            m_textAnimationInProgress = true;
            m_arrow.SetActive(false);
        }

        private void CompleteTextAnimation()
        {
            if (m_showArrow)
            {
                m_arrow.SetActive(true);
            }
            else
            {
                m_arrow.SetActive(false);
            }

            m_textAnimationInProgress = false;
            m_charQueue = null;
            m_receiver?.HandleMessageBoxTextAnimationFinished();
        }

        public void SkipTextAnimation()
        {
            if (!m_textAnimationInProgress)
            {
                return;
            }

            StopTextAnimationCoroutine();

            if (m_charQueue != null && m_charQueue.Count > 0)
            {
                m_text.text += new string(m_charQueue.ToArray());
            }

            CompleteTextAnimation();
        }

        public bool IsTextAnimationFinished()
        {
            return !m_textAnimationInProgress;
        }

        private void SetVisible(bool visible)
        {
            if (visible != m_visible)
            {
                m_visible = visible;

                if (m_animator && m_hasAnimationParameter)
                {
                    m_animator.SetBool(m_animationParameter, visible);
                }
            }
        }

        private void StopTextAnimationCoroutine()
        {
            if (m_textAnimationCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_textAnimationCoroutine);
            m_textAnimationCoroutine = null;
        }

        // 对话关闭或切换到下一句时直接终止当前跳字，避免旧协程在后台继续驱动 UI 状态。
        private void AbortTextAnimation()
        {
            StopTextAnimationCoroutine();
            m_textAnimationInProgress = false;
            m_charQueue = null;
            m_arrow.SetActive(false);
        }
    }
}

