using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    public class TransitionSystem : AGameSystem, ITransitionAnimationStateReceiver, ISceneTransitionUniTask
    {
        [Header("Settings")]
        [SerializeField] private bool m_startWithBlackScreen = false;
        [SerializeField] private string m_fadeInAnimationParameter;
        [SerializeField] private string m_fadeOutAnimationParameter;
        [SerializeField] private string m_skipFadeOutAnimationParameter;

        [Header("References")]
        [SerializeField] private Animator m_animator;

        private bool m_hasFadeInAnimation = false;
        private bool m_hasFadeOutAnimation = false;
        private bool m_hasSkipFadeOutAnimation = false;

        private bool m_isBlackScreen = false;

        private bool m_transitionInProgress;
        private float m_progress;
        private UniTaskCompletionSource m_fadeOutCompletion;
        private UniTaskCompletionSource m_fadeInCompletion;

        public float Progress => m_progress;
        public bool IsTransitioning => m_transitionInProgress;

        public override void OnSystemInit()
        {
            bool hasConfiguredAnimation =
                !string.IsNullOrWhiteSpace(m_fadeInAnimationParameter) ||
                !string.IsNullOrWhiteSpace(m_fadeOutAnimationParameter) ||
                !string.IsNullOrWhiteSpace(m_skipFadeOutAnimationParameter);
            if (m_animator == null)
            {
                if (hasConfiguredAnimation)
                {
                    throw new InvalidOperationException(
                        $"{nameof(TransitionSystem)} 配置了过场动画参数，但没有绑定 {nameof(Animator)}。");
                }

                m_isBlackScreen = m_startWithBlackScreen;
                return;
            }

            m_hasFadeInAnimation = AnimationUtils.HasParameter(m_animator, m_fadeInAnimationParameter);
            m_hasFadeOutAnimation = AnimationUtils.HasParameter(m_animator, m_fadeOutAnimationParameter);
            m_hasSkipFadeOutAnimation = AnimationUtils.HasParameter(m_animator, m_skipFadeOutAnimationParameter);

            if (m_startWithBlackScreen)
            {
                TryShowBlackScreen();
            }
        }

        public void FadeOutAsync(Action onComplete)
        {
            FadeOutUniTaskAsync()
                .ContinueWith(() => onComplete?.Invoke())
                .Forget(exception => Debug.LogException(exception, this));
        }

        public void FadeInAsync(Action onComplete)
        {
            FadeInUniTaskAsync()
                .ContinueWith(() => onComplete?.Invoke())
                .Forget(exception => Debug.LogException(exception, this));
        }

        public async UniTask FadeOutUniTaskAsync(CancellationToken cancellationToken = default)
        {
            if (m_transitionInProgress)
            {
                throw new InvalidOperationException("已有场景过渡正在执行，不能并发开始第二次淡出。");
            }

            m_transitionInProgress = true;
            m_progress = 0f;
            try
            {
                await PlayFadeOutAsync(cancellationToken);
                m_progress = 0.5f;
            }
            catch
            {
                ClearTransitionState();
                throw;
            }
        }

        public async UniTask FadeInUniTaskAsync(CancellationToken cancellationToken = default)
        {
            if (!m_transitionInProgress)
            {
                throw new InvalidOperationException("没有正在执行的场景过渡，不能单独开始淡入。");
            }

            try
            {
                await PlayFadeInAsync(cancellationToken);
                m_progress = 1f;
            }
            finally
            {
                m_transitionInProgress = false;
            }
        }

        public override void OnSystemStop()
        {
            m_fadeOutCompletion?.TrySetCanceled();
            m_fadeInCompletion?.TrySetCanceled();
            ClearTransitionState();
        }

        /// <summary>
        /// 过场淡出完成后的正式入口。
        /// 当前由 StateMessageDispatcher 通过 <see cref="ITransitionAnimationStateReceiver"/> 正式调用；
        /// 若接不到这里，就应视为动画接线错误。
        /// </summary>
        public void OnFadeOutCompleted()
        {
            EnsureAnimationCallbackExpected(m_fadeOutCompletion, nameof(OnFadeOutCompleted));
            m_isBlackScreen = true;
            m_fadeOutCompletion.TrySetResult();
        }

        /// <summary>
        /// 过场淡入完成后的正式入口。
        /// </summary>
        public void OnFadeInCompleted()
        {
            EnsureAnimationCallbackExpected(m_fadeInCompletion, nameof(OnFadeInCompleted));
            m_isBlackScreen = false;
            m_fadeInCompletion.TrySetResult();
        }

        public bool TryShowBlackScreen()
        {
            if (m_hasSkipFadeOutAnimation && m_animator != null)
            {
                m_isBlackScreen = true;
                m_animator.SetTrigger(m_skipFadeOutAnimationParameter);
                return true;
            }

            return false;
        }

        private async UniTask PlayFadeOutAsync(CancellationToken cancellationToken)
        {
            if (m_isBlackScreen)
            {
                return;
            }

            if (!m_hasFadeOutAnimation)
            {
                m_isBlackScreen = true;
                return;
            }

            m_fadeOutCompletion = new UniTaskCompletionSource();
            m_animator.SetTrigger(m_fadeOutAnimationParameter);
            await m_fadeOutCompletion.Task.AttachExternalCancellation(cancellationToken);
            m_fadeOutCompletion = null;
        }

        private async UniTask PlayFadeInAsync(CancellationToken cancellationToken)
        {
            if (!m_isBlackScreen)
            {
                return;
            }

            if (!m_hasFadeInAnimation)
            {
                m_isBlackScreen = false;
                return;
            }

            m_fadeInCompletion = new UniTaskCompletionSource();
            m_animator.SetTrigger(m_fadeInAnimationParameter);
            await m_fadeInCompletion.Task.AttachExternalCancellation(cancellationToken);
            m_fadeInCompletion = null;
        }

        private void ClearTransitionState()
        {
            m_fadeOutCompletion = null;
            m_fadeInCompletion = null;
            m_transitionInProgress = false;
            m_progress = 0f;
        }

        private static void EnsureAnimationCallbackExpected(
            UniTaskCompletionSource completionSource,
            string callbackName)
        {
            if (completionSource == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(TransitionSystem)}.{callbackName} 收到了没有对应过场等待者的动画回调。");
            }
        }
    }
}

