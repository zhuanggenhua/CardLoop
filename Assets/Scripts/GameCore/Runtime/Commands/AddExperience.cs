using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 给命令上下文中的角色增加经验值；没有上下文角色时退回当前控制角色。
    /// </summary>
    [Serializable]
    public class AddExperience : IContextualCommand
    {
        [InspectorName("经验值")]
        [Tooltip("要添加到目标角色身上的经验值。")]
        [SerializeField] private int m_experience;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            CharacterBase resolvedTarget =
                context.ResolveRequiredActorOrCurrentControlledCharacter(nameof(AddExperience));
            CharacterActor target = resolvedTarget as CharacterActor;
            if (target == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(AddExperience)} 需要 {nameof(CharacterActor)}，但解析到的角色是 {resolvedTarget.GetType().Name}。");
            }

            target.AddExperience(m_experience);
            return Task.CompletedTask;
        }
    }
}

