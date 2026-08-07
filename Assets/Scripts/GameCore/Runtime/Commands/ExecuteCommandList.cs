using System;
using System.Threading.Tasks;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    /// <summary>
    /// 命令列表的执行策略，决定子命令按顺序等待还是同时启动。
    /// </summary>
    public enum ECommandListExecutionMode
    {
        Sequential,
        Parallel
    }

    /// <summary>
    /// 可序列化的命令组合，执行期间可临时锁定角色动作并按配置调度多个子命令。
    /// </summary>
    [Serializable]
    public class ExecuteCommandList : IContextualCommand
    {
        [InspectorName("执行模式")]
        [Tooltip("顺序模式会等待每个命令完成后再执行下一个；并行模式会同时启动所有命令并等待全部完成。")]
        [SerializeField] private ECommandListExecutionMode m_executionMode = ECommandListExecutionMode.Sequential;

        [InspectorName("执行期间禁用动作")]
        [Tooltip("命令列表执行期间临时禁用的角色动作，完成或异常后都会恢复。")]
        [SerializeField] private EActionFlags m_disabledActions = EActionFlags.None;

        [InspectorName("子命令列表")]
        [Tooltip("要执行的命令序列；支持实现 IContextualCommand 的命令接收同一上下文。")]
        [SerializeReference, SubclassSelector] private ICommand[] m_commands = null;

        private async Task ExecuteSequential(GameCommandContext context)
        {
            for (int i = 0; i < m_commands.Length; i++)
            {
                ICommand command = m_commands[i] ?? throw CreateMissingCommandException(i);
                await command.Execute(context);
            }
        }

        private async Task ExecuteParallel(GameCommandContext context)
        {
            Task[] tasks = new Task[m_commands.Length];

            for (int i = 0; i < m_commands.Length; i++)
            {
                ICommand command = m_commands[i] ?? throw CreateMissingCommandException(i);
                tasks[i] = command.Execute(context);
            }

            await Task.WhenAll(tasks);
        }

        public async Task Execute()
        {
            await Execute(GameCommandContext.Script());
        }

        public async Task Execute(GameCommandContext context)
        {
            EnsureCommandsConfigured();

            CharacterBase actionLockTarget = m_disabledActions == EActionFlags.None
                ? null
                : context.ResolveRequiredActorOrCurrentControlledCharacter(nameof(ExecuteCommandList));
            actionLockTarget?.DisableActions(m_disabledActions);

            try
            {
                if (m_executionMode == ECommandListExecutionMode.Sequential)
                {
                    await ExecuteSequential(context);
                }
                else
                {
                    await ExecuteParallel(context);
                }
            }
            finally
            {
                actionLockTarget?.EnableActions(m_disabledActions);
            }
        }

        private void EnsureCommandsConfigured()
        {
            if (m_commands == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(ExecuteCommandList)} 需要有效子命令数组；如果确实要无操作，请配置为空数组而不是缺失数组。");
            }
        }

        private static InvalidOperationException CreateMissingCommandException(int index)
        {
            return new InvalidOperationException(
                $"{nameof(ExecuteCommandList)} 的第 {index} 个子命令缺失，不能把命令组合当成成功 no-op。");
        }
    }
}

