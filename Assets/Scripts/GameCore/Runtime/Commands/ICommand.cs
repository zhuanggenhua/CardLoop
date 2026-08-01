using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 无上下文命令的最小合同，适合菜单、触发器和数据驱动动作统一调度。
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 执行命令并返回完成任务；实现方自行决定是否异步等待动画、对话或加载。
        /// </summary>
        public Task Execute();
    }

    /// <summary>
    /// 需要调用者、来源或目标等上下文信息的命令合同。
    /// </summary>
    public interface IContextualCommand : ICommand
    {
        /// <summary>
        /// 在指定命令上下文中执行；上下文用于权限、参与者校验和事件来源追踪。
        /// </summary>
        public Task Execute(GameCommandContext context);
    }

    /// <summary>
    /// 命令执行辅助入口，负责在有上下文时优先调用上下文版本。
    /// </summary>
    public static class CommandExecutionExtensions
    {
        /// <summary>
        /// 安全执行命令；空命令视为已完成，支持旧式无上下文命令和新式上下文命令共存。
        /// </summary>
        public static Task Execute(this ICommand command, GameCommandContext context)
        {
            if (command == null)
            {
                return Task.CompletedTask;
            }

            return command is IContextualCommand contextualCommand
                ? contextualCommand.Execute(context)
                : command.Execute();
        }

        /// <summary>
        /// 明确以后台方式执行命令，并把异步异常写入 Unity Console。
        /// 只能用于生命周期、触发器、死亡收口等调用方本身无法等待的事件入口。
        /// </summary>
        public static void ExecuteFireAndReport(
            this ICommand command,
            GameCommandContext context,
            string ownerName,
            UnityEngine.Object logContext = null)
        {
            _ = ExecuteFireAndReportAsync(command, context, ownerName, logContext);
        }

        private static async Task ExecuteFireAndReportAsync(
            ICommand command,
            GameCommandContext context,
            string ownerName,
            UnityEngine.Object logContext)
        {
            try
            {
                await command.Execute(context);
            }
            catch (Exception exception)
            {
                string owner = string.IsNullOrWhiteSpace(ownerName) ? "Command" : ownerName;
                Debug.LogException(
                    new InvalidOperationException($"[{owner}] 后台命令执行失败。", exception),
                    logContext);
            }
        }
    }
}
