using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 菜单语义运行时入口。
    /// 这里只负责把项目菜单请求接到 UIKit 原生 panel 机制，不复制第二套路由或菜单栈真相。
    /// </summary>
    public sealed partial class UIManager
    {
        private const string DefaultStackName = "fw_menu";

        private void StartMenuRuntime()
        {
            RebuildRegistrations();

            EventKit.Type.Register<MenuRequestedEvent>(OnMenuRequested);
            EventKit.Type.Register<InventoryMenuRequestedEvent>(OnInventoryMenuRequested);
            EventKit.Type.Register<CharacterMenuRequestedEvent>(OnCharacterMenuRequested);
            EventKit.Type.Register<AbilitiesMenuRequestedEvent>(OnAbilitiesMenuRequested);
            EventKit.Type.Register<ShopRequestedEvent>(OnShopRequested);
            EventKit.Type.Register<CraftRequestedEvent>(OnCraftRequested);
            EventKit.Type.Register<CloseAllMenusRequestedEvent>(OnCloseAllMenusRequested);

            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Performed, OnCancel);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Canceled, OnCancelReleased);
        }

        private void StopMenuRuntime()
        {
            EventKit.Type.UnRegister<MenuRequestedEvent>(OnMenuRequested);
            EventKit.Type.UnRegister<InventoryMenuRequestedEvent>(OnInventoryMenuRequested);
            EventKit.Type.UnRegister<CharacterMenuRequestedEvent>(OnCharacterMenuRequested);
            EventKit.Type.UnRegister<AbilitiesMenuRequestedEvent>(OnAbilitiesMenuRequested);
            EventKit.Type.UnRegister<ShopRequestedEvent>(OnShopRequested);
            EventKit.Type.UnRegister<CraftRequestedEvent>(OnCraftRequested);
            EventKit.Type.UnRegister<CloseAllMenusRequestedEvent>(OnCloseAllMenusRequested);

            if (GameManager.Exists() && GameManager.HasSystem<InputSystem>())
            {
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Performed, OnCancel);
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Canceled, OnCancelReleased);
            }

            ResolveAllCloseTasks();
        }
    }
}
