using System;
using System.Threading.Tasks;

namespace GameCore
{
    /// <summary>
    /// 请求关闭当前所有菜单的命令。
    /// </summary>
    [Serializable]
    public class CloseMenus : IContextualCommand
    {
        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            GameManager.UISystem.CloseAllMenus();
            return Task.CompletedTask;
        }
    }
}

