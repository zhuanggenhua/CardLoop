using UnityEngine;

using YokiFrame;

namespace FantasyWord.GameCore
{
    public class TransitionSystem : AGameSystem, ITransitionAnimationStateReceiver
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

        private MapLoadingDelegationParams m_mapLoadingDelegationParams = null;

        public override void OnSystemInit()
        {
            Debug.Assert(m_animator, ErrorMessages.InspectorMissingComponentReference<Animator>());

            m_hasFadeInAnimation = AnimationUtils.HasParameter(m_animator, m_fadeInAnimationParameter);
            m_hasFadeOutAnimation = AnimationUtils.HasParameter(m_animator, m_fadeOutAnimationParameter);
            m_hasSkipFadeOutAnimation = AnimationUtils.HasParameter(m_animator, m_skipFadeOutAnimationParameter);

            if (m_startWithBlackScreen)
            {
                TryShowBlackScreen();
            }
        }

        public override void OnSystemStart()
        {
            EventKit.Type.Register<MapTransitionDelegationRequestedEvent>(OnMapTransitionDelegationRequested);
        }

        public override void OnSystemStop()
        {
            EventKit.Type.UnRegister<MapTransitionDelegationRequestedEvent>(OnMapTransitionDelegationRequested);
        }

        private void OnMapTransitionDelegationRequested(MapTransitionDelegationRequestedEvent transitionDelegationRequestedEvent)
        {
            m_mapLoadingDelegationParams = transitionDelegationRequestedEvent.DelegationParams;

            Debug.Assert(m_mapLoadingDelegationParams.unloadDelegate != null, "Unload delegate is null");
            Debug.Assert(m_mapLoadingDelegationParams.loadDelegate != null, "Load delegate is null");
            Debug.Assert(m_mapLoadingDelegationParams.completionDelegate != null, "Completion delegate is null");

            if (!m_isBlackScreen)
            {
                if (!TryPlayFadeOutTransition())
                {
                    OnFadeOutCompleted();
                }
            }
            else
            {
                OnFadeOutCompleted();
            }
        }

        /// <summary>
        /// 过场淡出完成后的正式入口。
        /// 当前由 StateMessageDispatcher 通过 <see cref="ITransitionAnimationStateReceiver"/> 正式调用；
        /// 若接不到这里，就应视为动画接线错误。
        /// </summary>
        public void OnFadeOutCompleted()
        {
            m_isBlackScreen = true;

            m_mapLoadingDelegationParams.unloadDelegate(() =>
            {
                m_mapLoadingDelegationParams.loadDelegate(() =>
                {
                    if (!TryPlayFadeInTransition())
                    {
                        OnFadeInCompleted();
                    }
                });
            });
        }

        /// <summary>
        /// 过场淡入完成后的正式入口。
        /// </summary>
        public void OnFadeInCompleted()
        {
            m_isBlackScreen = false;
            m_mapLoadingDelegationParams.completionDelegate();
        }

        public bool TryPlayFadeInTransition()
        {
            Debug.Assert(m_isBlackScreen, "Can't play fade in transition if the screen is not black");

            if (m_hasFadeInAnimation)
            {
                m_animator.SetTrigger(m_fadeInAnimationParameter);
                return true;
            }

            return false;
        }

        public bool TryPlayFadeOutTransition()
        {
            if (m_hasFadeOutAnimation)
            {
                m_animator.SetTrigger(m_fadeOutAnimationParameter);
                return true;
            }

            return false;
        }

        public bool TryShowBlackScreen()
        {
            if (m_hasSkipFadeOutAnimation)
            {
                m_isBlackScreen = true;
                m_animator.SetTrigger(m_skipFadeOutAnimationParameter);
                return true;
            }

            return false;
        }
    }
}

