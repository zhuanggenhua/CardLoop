using Sirenix.OdinInspector;
using System;
using System.Threading.Tasks;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    /// <summary>
    /// 顺序交互的中断策略，决定子交互成功或失败时是否提前结束。
    /// </summary>
    public enum ESequenceInterruptionPolicy
    {
        OnSuccess,
        OnFailure,
        Never
    }

    /// <summary>
    /// 按配置顺序执行多个交互，并根据中断策略返回累计结果。
    /// </summary>
    [Serializable]
    public class SequentialInteraction : IInteraction
    {
        [LabelText("交互列表")]
        [Tooltip("按顺序尝试执行的交互。每个交互都会接收同一来源和目标。")]
        [SerializeReference, SubclassSelector]
        private IInteraction[] m_interactions;

        [LabelText("中断策略")]
        [Tooltip("控制子交互成功或失败后是否提前停止后续交互。")]
        [SerializeField] private ESequenceInterruptionPolicy m_interruptionPolicy = ESequenceInterruptionPolicy.OnSuccess;

        public async Task<bool> TryExecute(CharacterBase source, IInteractionTarget target)
        {
            bool sequenceOutcome = true;

            foreach (IInteraction interaction in m_interactions)
            {
                bool interactionOutcome = await interaction.TryExecute(source, target);

                sequenceOutcome &= interactionOutcome;

                if (interactionOutcome && m_interruptionPolicy == ESequenceInterruptionPolicy.OnSuccess)
                {
                    return sequenceOutcome;
                }
                else if (!interactionOutcome && m_interruptionPolicy == ESequenceInterruptionPolicy.OnFailure)
                {
                    return sequenceOutcome;
                }
            }

            return sequenceOutcome;
        }
    }
}

