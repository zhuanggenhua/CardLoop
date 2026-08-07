using System;
using System.Threading.Tasks;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 对上下文角色恢复或消耗法力值。
    /// </summary>
    [Serializable]
    public class AddOrRemoveMana : IContextualCommand
    {
        [InspectorName("动作")]
        [Tooltip("Add 表示恢复法力，Remove 表示消耗法力。")]
        [SerializeField] private EAction m_action = EAction.Add;

        [InspectorName("数值")]
        [Tooltip("恢复或消耗的法力值。")]
        [SerializeField][Min(0)] private int m_amount = 0;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            CharacterBase target =
                context.ResolveRequiredActorOrCurrentControlledCharacter(nameof(AddOrRemoveMana));

            switch (m_action)
            {
                case EAction.Add:
                    target.RecoverMana(m_amount);
                    break;

                case EAction.Remove:
                    target.ConsumeMana(m_amount);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}

