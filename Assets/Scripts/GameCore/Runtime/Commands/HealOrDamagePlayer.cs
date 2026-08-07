using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 对上下文角色治疗或造成无来源伤害。
    /// </summary>
    [Serializable]
    public class HealOrDamagePlayer : IContextualCommand
    {
        [InspectorName("动作")]
        [Tooltip("Add 表示治疗，Remove 表示造成伤害。")]
        [SerializeField] private EAction m_action = EAction.Add;

        [InspectorName("数值")]
        [Tooltip("治疗或伤害的数值。")]
        [SerializeField][Min(0)] private int m_amount = 0;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            CharacterBase target =
                context.ResolveRequiredActorOrCurrentControlledCharacter(nameof(HealOrDamagePlayer));

            switch (m_action)
            {
                case EAction.Add:
                    target.Heal(m_amount);
                    break;

                case EAction.Remove:
                    target.Damage(new DamageOutputDescriptor
                    {
                        source = new UnknownDamageSource(),
                        damage = math.abs(m_amount),
                        flags = EDamageFlag.None,
                        type = EDamageType.None,
                    });
                    break;
            }

            return Task.CompletedTask;
        }
    }
}

