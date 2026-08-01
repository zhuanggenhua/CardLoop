using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 运行时可序列化的 UIKit 菜单面板类型引用。
    /// 它只负责保存“正式菜单应该打开哪一种 UIKit 面板”，
    /// 不承担菜单语义、请求入口或任何业务真相。
    /// </summary>
    [Serializable]
    public sealed class UIKitMenuPanelTypeReference : ISerializationCallbackReceiver
    {
        [SerializeField] private string m_assemblyQualifiedName = string.Empty;

        [NonSerialized] private Type m_cachedType;

        public bool HasValue => !string.IsNullOrWhiteSpace(m_assemblyQualifiedName);

        public string AssemblyQualifiedName => m_assemblyQualifiedName;

        public bool TryResolve(out Type panelType)
        {
            if (string.IsNullOrWhiteSpace(m_assemblyQualifiedName))
            {
                panelType = null;
                return false;
            }

            m_cachedType ??= Type.GetType(m_assemblyQualifiedName, false);
            panelType = m_cachedType;
            return panelType != null;
        }

        public bool TryResolvePanelType(out Type panelType, out string error)
        {
            if (!TryResolve(out panelType))
            {
                error = HasValue
                    ? $"找不到已序列化的 UIKit 面板类型：{m_assemblyQualifiedName}"
                    : "未登记 UIKit 面板类型。";
                return false;
            }

            if (!typeof(UIKitMenuPanelBase).IsAssignableFrom(panelType))
            {
                error = $"类型 {panelType.FullName} 没有继承 {nameof(UIKitMenuPanelBase)}。";
                panelType = null;
                return false;
            }

            if (panelType.IsAbstract)
            {
                error = $"类型 {panelType.FullName} 是抽象类，不能作为正式 UIKit 菜单面板。";
                panelType = null;
                return false;
            }

            error = null;
            return true;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            m_cachedType = null;
        }
    }
}
