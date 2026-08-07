using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    /// <summary>
    /// 条件状态机当前缓存的条件结果。
    /// </summary>
    public enum EConditionalState
    {
        None,
        Met,
        NotMet
    }

    /// <summary>
    /// 基于条件结果驱动派生行为的状态机基类，负责启动监听和在条件变化时分发回调。
    /// </summary>
    public abstract class AConditionalStateMachine : MonoBehaviour
    {
        [InspectorName("驱动条件")]
        [Tooltip("用于决定状态机进入满足或不满足分支的条件。为空时按满足处理。")]
        [SerializeReference, SubclassSelector] private ICondition m_condition = null;

        /// <summary>
        /// 最近一次计算出的条件状态。
        /// </summary>
        public EConditionalState state => m_state;

        private EConditionalState m_state = EConditionalState.None;
        private bool m_isListening;

        protected virtual void OnConditionMet() { }
        protected virtual void OnConditionNotMet() { }

        private void OnEnable()
        {
            UpdateState();
            StartConditionListening();
        }

        private void OnDisable()
        {
            StopConditionListening();
        }

        private void OnDestroy()
        {
            StopConditionListening();
        }

        private void StartConditionListening()
        {
            if (m_condition == null || m_isListening)
            {
                return;
            }

            m_condition.StartListening(UpdateState);
            m_isListening = true;
        }

        private void StopConditionListening()
        {
            if (m_condition == null || !m_isListening)
            {
                return;
            }

            m_condition.StopListening();
            m_isListening = false;
        }

        private void UpdateState()
        {
            EConditionalState newState = (m_condition?.Evaluate() ?? true) ? EConditionalState.Met : EConditionalState.NotMet;

            if (newState != m_state)
            {
                m_state = newState;

                switch (m_state)
                {
                    case EConditionalState.Met:
                        OnConditionMet();
                        break;

                    case EConditionalState.NotMet:
                        OnConditionNotMet();
                        break;
                }
            }
        }
    }
}
