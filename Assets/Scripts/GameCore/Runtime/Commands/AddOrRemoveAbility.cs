using Sirenix.OdinInspector;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 给上下文角色添加或移除脚本来源的 Formal GAS 奖励技能。
    /// </summary>
    [Serializable]
    public class AddOrRemoveAbility : IContextualCommand
    {
        [LabelText("动作")]
        [Tooltip("决定添加还是移除该技能。")]
        [SerializeField] private EAction m_action = EAction.Add;

        [LabelText("Formal GAS 技能编码")]
        [Tooltip("要添加或移除的 Formal GAS 技能编码；必须大于 0。")]
        [SerializeField] private int m_formalGasAbilityCode = 0;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            int formalGasAbilityCode = EnsureValidFormalGasAbilityCode();
            CharacterBase target =
                context.ResolveRequiredActorOrCurrentControlledCharacter(nameof(AddOrRemoveAbility));

            switch (m_action)
            {
                case EAction.Add:
                    target.AddBonusFormalGasAbility(formalGasAbilityCode, CreateCommandAbilitySource(formalGasAbilityCode));
                    break;

                case EAction.Remove:
                    target.RemoveBonusFormalGasAbility(formalGasAbilityCode, CreateCommandAbilitySource(formalGasAbilityCode));
                    break;
            }

            return Task.CompletedTask;
        }

        private int EnsureValidFormalGasAbilityCode()
        {
            if (m_formalGasAbilityCode <= 0)
            {
                throw new InvalidOperationException(
                    $"[{nameof(AddOrRemoveAbility)}] 奖励技能命令需要大于 0 的 Formal GAS 技能编码，不能把空编码当成成功命令。");
            }

            return m_formalGasAbilityCode;
        }

        private CharacterAbilitySourceKey CreateCommandAbilitySource(int formalGasAbilityCode)
        {
            string sourceId = $"{GetType().FullName}:{formalGasAbilityCode}";
            return new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Script, sourceId);
        }
    }
}

