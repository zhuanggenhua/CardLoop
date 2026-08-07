using System;
using System.Threading.Tasks;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    [Serializable]
    public class ConditionalInteraction : IInteraction
    {
        [SerializeReference, SubclassSelector] private ICondition m_condition = null;
        [SerializeReference, SubclassSelector] private IInteraction m_interaction = null;

        public Task<bool> TryExecute(CharacterBase source, IInteractionTarget target)
        {
            if (m_condition?.Evaluate() ?? true)
            {
                if (m_interaction == null)
                {
                    Debug.LogError($"[{nameof(ConditionalInteraction)}] 条件满足，但没有配置要执行的交互。");
                    return Task.FromResult(false);
                }

                return m_interaction.TryExecute(source, target);
            }

            return Task.FromResult(false);
        }
    }
}

