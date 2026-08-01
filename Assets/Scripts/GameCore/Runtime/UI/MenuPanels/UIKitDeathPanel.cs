using UnityEngine;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 正式死亡菜单的 UIKit 面板实现。
    /// 当前只承接旧死亡菜单的最小面板合同：不可被返回键弹掉、默认聚焦退出按钮、点击后回主菜单。
    /// </summary>
    public sealed class UIKitDeathPanel : UIKitMenuPanelBase
    {
        [Header("References")]
        [SerializeField] private Button m_quitButton = null;

        protected override bool CanCloseFromMenuStack()
        {
            return false;
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            return m_quitButton ? m_quitButton.gameObject : null;
        }

        public void GoToMainMenu()
        {
            GameRuntimeEvents.RequestReturnToMainMenu();
        }
    }
}

