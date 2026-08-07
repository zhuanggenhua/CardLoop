using System;
using System.Collections.Generic;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    public sealed partial class UIManager
    {
        [Serializable]
        private struct MenuPanelBinding
        {
            [SerializeField] private EMenu m_menu;
            [SerializeField] private UIKitMenuPanelTypeReference m_panelType;
            [SerializeField] private UILevel m_level;

            public EMenu Menu => m_menu;

            public UIKitMenuPanelTypeReference PanelType => m_panelType;

            public UILevel Level => m_level;
        }

        private readonly Dictionary<EMenu, UIKitMenuRegistration> m_menuRegistrations = new();

        [Header("Registered Panels")]
        [SerializeField] private MenuPanelBinding[] m_registeredMenuPanels = Array.Empty<MenuPanelBinding>();

        [Header("Menu Runtime Settings")]
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
