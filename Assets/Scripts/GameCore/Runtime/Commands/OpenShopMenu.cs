using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 打开指定商店菜单并等待菜单关闭的命令。
    /// </summary>
    [Serializable]
    public class OpenShopMenu : IContextualCommand
    {
        [InspectorName("商店")]
        [Tooltip("要打开的商店资产。")]
        [SerializeField] private Shop m_shop = null;

        public async Task Execute()
        {
            await Execute(GameCommandContext.Script());
        }

        public async Task Execute(GameCommandContext context)
        {
            Debug.Assert(m_shop != null, "Missing Shop reference!");
            var taskCompletionSource = new TaskCompletionSource<bool>();
            GameRuntimeEvents.RequestShop(m_shop, context, taskCompletionSource);
            await taskCompletionSource.Task;
        }
    }
}

