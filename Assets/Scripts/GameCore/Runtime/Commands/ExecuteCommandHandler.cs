using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 转发到 CommandHandler 的命令包装器，便于在可序列化命令槽位中复用组件命令。
    /// </summary>
    [Serializable]
    public class ExecuteCommandHandler : IContextualCommand
    {
        [InspectorName("命令处理器")]
        [Tooltip("实际执行命令的 CommandHandler。缺失时会暴露配置错误。")]
        [SerializeField] private CommandHandler m_commandHandler = null;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            if (m_commandHandler == null)
            {
                throw new InvalidOperationException($"{nameof(ExecuteCommandHandler)} 缺少要执行的命令资产。");
            }

            return m_commandHandler.Execute(context);
        }
    }
}

