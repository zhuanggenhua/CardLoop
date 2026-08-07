using System;
using System.Threading.Tasks;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 可通过命令系统打开的游戏菜单。
    /// </summary>
    public enum EMenu
    {
        Pause,
        Save,
        Settings,
        Death
    }

    /// <summary>
    /// 打开指定菜单的命令，并等待正式 UI 系统返回面板关闭结果。
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
            await GameManager.UISystem.OpenMenuAsync(m_menuToOpen);
        }
    }
}

