using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 使用命令上下文销毁指定实体。
    /// </summary>
    [Serializable]
    public class DestroyEntity : IContextualCommand
    {
        [InspectorName("目标实体")]
        [Tooltip("命令执行时要销毁的实体。缺失时会暴露配置错误。")]
        [SerializeField] private Entity m_toDestroy = null;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            if (m_toDestroy == null)
            {
                throw new InvalidOperationException($"{nameof(DestroyEntity)} 缺少要销毁的目标实体。");
            }

            m_toDestroy.Destroy(context);
            return Task.CompletedTask;
        }
    }
}

