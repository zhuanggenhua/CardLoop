using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 打开指定制作台菜单并等待菜单关闭的命令。
    /// </summary>
    [Serializable]
    public class OpenCraftMenu : IContextualCommand
    {
        [InspectorName("制作台")]
        [Tooltip("要打开的制作台配置。")]
        [SerializeField] private CraftingStation m_craftingStation = null;

        public async Task Execute()
        {
            await Execute(GameCommandContext.Script());
        }

        public async Task Execute(GameCommandContext context)
        {
            Debug.Assert(m_craftingStation != null, "Missing CraftingStation reference!");
            var taskCompletionSource = new TaskCompletionSource<bool>();
            GameRuntimeEvents.RequestCraft(m_craftingStation, context, taskCompletionSource);
            await taskCompletionSource.Task;
        }
    }
}

