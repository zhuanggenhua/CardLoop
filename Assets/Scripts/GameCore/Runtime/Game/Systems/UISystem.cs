using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    public class UISystem : AGameSystem
    {
        private static readonly Type[] SystemStartupDependencies =
        {
            typeof(InputSystem),
            typeof(GameStateSystem)
        };

        [Header("UI 宿主")]
        [LabelText("UI 预制体")]
        [Tooltip("进程级 UI 宿主预制体，必须包含一个 UIManager；菜单注册、菜单栈和 UIKit 面板请求都从这里进入。")]
        [SerializeField] private GameObject m_uiPrefab;

        private GameObject m_uiInstance = null;
        private UIManager m_uiManager = null;

        public override IReadOnlyCollection<Type> StartupDependencies => SystemStartupDependencies;

        public override void OnSystemStart()
        {
            ShowUI();
            EventKit.Type.Register<SaveFileLoadedEvent>(OnSaveFileLoaded);
        }

        public override void OnSystemStop()
        {
            EventKit.Type.UnRegister<SaveFileLoadedEvent>(OnSaveFileLoaded);
        }

        /// <summary>
        /// 存档或单局内容加载完成后，确保 UI 宿主在玩法数据可用之后再创建。
        /// </summary>
        private void OnSaveFileLoaded(SaveFileLoadedEvent _)
        {
            ShowUI();
        }

        public void ShowUI()
        {
            if (m_uiInstance == null)
            {
                if (m_uiPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(UISystem)} 缺少 UI 预制体；请在唯一运行根上配置包含 {nameof(UIManager)} 的宿主预制体。");
                }

                m_uiInstance = Instantiate(m_uiPrefab, transform);
                m_uiManager = m_uiInstance.GetComponentInChildren<UIManager>(includeInactive: true);
                if (m_uiManager == null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(UISystem)} 的 UI 预制体必须包含一个 {nameof(UIManager)}。");
                }
            }
            else
            {
                m_uiInstance.SetActive(true);
            }

        }

        public Task<bool> OpenMenuAsync(EMenu menu)
        {
            ShowUI();
            return GetRequiredUIManager().OpenMenuAsync(menu);
        }

        public void CloseAllMenus()
        {
            GetRequiredUIManager().CloseAllMenus();
        }

        public bool CloseCurrentMenu()
        {
            return GetRequiredUIManager().CloseCurrentMenu();
        }

        public void HideUI()
        {
            if (m_uiInstance != null)
            {
                m_uiInstance.SetActive(false);
            }
        }

        private UIManager GetRequiredUIManager()
        {
            if (m_uiManager != null)
            {
                return m_uiManager;
            }

            throw new InvalidOperationException(
                $"{nameof(UISystem)} 尚未创建 {nameof(UIManager)} 运行时实例；请先通过正式 UI 预制体创建 UI 宿主。");
        }
    }
}

