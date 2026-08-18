using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    public sealed partial class UIManager
    {
        [Serializable]
        private struct MenuPanelBinding
        {
            [LabelText("菜单类型")]
            [Tooltip("玩家请求打开的菜单枚举，例如暂停菜单或设置菜单。")]
            [SerializeField] private EMenu m_menu;

            [LabelText("面板类型")]
            [Tooltip("该菜单实际打开的 UIKit 面板类型；必须继承 UIKitMenuPanelBase。")]
            [SerializeField] private UIKitMenuPanelTypeReference m_panelType;

            [LabelText("UI 层级")]
            [Tooltip("该菜单打开时所在的 UI 层级，用于决定遮挡和输入顺序。")]
            [SerializeField] private UILevel m_level;

            public EMenu Menu => m_menu;

            public UIKitMenuPanelTypeReference PanelType => m_panelType;

            public UILevel Level => m_level;
        }

        private readonly Dictionary<EMenu, UIKitMenuRegistration> m_menuRegistrations = new();

        [Header("菜单注册")]
        [LabelText("菜单面板注册")]
        [Tooltip("把菜单枚举映射到正式 UIKit 面板类型；这里是菜单打开请求的唯一作者配置入口。")]
        [SerializeField] private MenuPanelBinding[] m_registeredMenuPanels = Array.Empty<MenuPanelBinding>();

        [Header("菜单运行设置")]
        [LabelText("菜单栈名称")]
        [Tooltip("UIManager 内用于菜单压栈和返回的栈名；同一 UI 宿主内保持唯一即可。")]
        [SerializeField] private string m_stackName = DefaultStackName;

        /// <summary>
        /// 只负责把序列化声明重建成正式菜单查找表。
        /// 不承担请求路由、面板栈、焦点或关闭会话管理。
        /// </summary>
        private void RebuildRegistrations()
        {
            m_menuRegistrations.Clear();

            foreach (MenuPanelBinding menuPanelBinding in m_registeredMenuPanels)
            {
                UIKitMenuPanelTypeReference typeReference = menuPanelBinding.PanelType;
                if (typeReference == null || !typeReference.HasValue)
                {
                    continue;
                }

                if (!TryCreateRegistration(typeReference, menuPanelBinding.Level, $"菜单 {menuPanelBinding.Menu}", out UIKitMenuRegistration registration))
                {
                    continue;
                }

                if (!m_menuRegistrations.TryAdd(menuPanelBinding.Menu, registration))
                {
                    Debug.LogError($"[{nameof(UIManager)}] 菜单 {menuPanelBinding.Menu} 被重复登记。", this);
                }
            }
        }

        private bool TryCreateRegistration(UIKitMenuPanelTypeReference typeReference, UILevel level, string slotName, out UIKitMenuRegistration registration)
        {
            registration = default;

            if (typeReference == null)
            {
                return false;
            }

            if (!typeReference.TryResolvePanelType(out Type panelType, out string error))
            {
                Debug.LogError($"[{nameof(UIManager)}] {slotName} 的类型登记无效：{error}", this);
                return false;
            }

            registration = CreateRegistration(panelType, level, slotName);
            return true;
        }

        private static UIKitMenuRegistration CreateRegistration(Type panelType, UILevel level, string slotName)
        {
            if (panelType == null)
            {
                throw new ArgumentNullException(nameof(panelType), $"{slotName} 缺少有效的面板类型。");
            }

            if (!typeof(UIKitMenuPanelBase).IsAssignableFrom(panelType))
            {
                throw new ArgumentException($"{slotName} 必须继承 {nameof(UIKitMenuPanelBase)}：{panelType.FullName}", nameof(panelType));
            }

            return new UIKitMenuRegistration(panelType, level);
        }

        private sealed class UIKitMenuRegistration
        {
            public UIKitMenuRegistration(Type panelType, UILevel level)
            {
                PanelType = panelType;
                Level = level;
            }

            public Type PanelType { get; }

            public UILevel Level { get; }
        }
    }
}
