using System;
using System.Threading.Tasks;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 角色移动命令基类，按相对位移调用目标角色的 MoveTo。
    /// </summary>
    [Serializable]
    public abstract class MoveCharacterBase : IContextualCommand
    {
        protected abstract CharacterBase targetCharacter { get; }

        protected virtual CharacterBase ResolveTargetCharacter(GameCommandContext context)
        {
            return targetCharacter;
        }

        [InspectorName("相对位移")]
        [Tooltip("命令执行时在目标角色当前位置基础上添加的位移。")]
        [SerializeField] private Vector2 m_movement;

        public async Task Execute()
        {
            await Execute(GameCommandContext.Script());
        }

        public async Task Execute(GameCommandContext context)
        {
            CharacterBase target = ResolveTargetCharacter(context);
            Debug.Assert(target != null, "Missing character reference!");
            if (!target)
            {
                throw new InvalidOperationException($"{GetType().Name} 缺少要移动的目标角色。");
            }

            Vector3 initialPosition = target.transform.position;
            Vector3 targetPosition = initialPosition + (Vector3)m_movement;
            await target.MoveTo(targetPosition).Task;
        }
    }
}

