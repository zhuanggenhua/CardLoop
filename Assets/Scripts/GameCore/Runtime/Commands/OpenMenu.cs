using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 可通过命令系统打开的游戏菜单。
    /// </summary>
    public enum EMenu
    {
        Pause,
        Character,
        Abilities,
        Inventory,
        Journal,
        Save,
        Settings,
        Death
    }

    /// <summary>
    /// 打开指定菜单的命令，会等待菜单系统通过 TaskCompletionSource 回传完成信号。
    /// </summary>
    [Serializable]
    public class OpenMenu : IContextualCommand
    {
        [InspectorName("目标菜单")]
        [Tooltip("命令执行时请求打开的菜单类型。")]
        [SerializeField] private EMenu m_menuToOpen;

        public async Task Execute()
        {
            await Execute(GameCommandContext.Script());
        }

        public async Task Execute(GameCommandContext context)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            GameRuntimeEvents.RequestMenu(m_menuToOpen, taskCompletionSource);
            await taskCompletionSource.Task;
        }
    }
}

