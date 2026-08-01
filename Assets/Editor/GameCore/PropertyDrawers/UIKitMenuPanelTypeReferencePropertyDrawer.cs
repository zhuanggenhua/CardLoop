using System;
using System.Collections.Generic;
using System.Linq;
using FantasyWord.GameCore;
using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 让 UIKit 菜单入口直接在 Inspector 里选择正式面板类型，
    /// 避免下一阶段再造运行时注册脚本或硬编码映射。
    /// </summary>
    [CustomPropertyDrawer(typeof(UIKitMenuPanelTypeReference))]
    public sealed class UIKitMenuPanelTypeReferencePropertyDrawer : PropertyDrawer
    {
        private static readonly GUIContent NoneOption = new("<None>");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty assemblyQualifiedNameProperty = property.FindPropertyRelative("m_assemblyQualifiedName");
            TypeReferenceOption[] options = GetTypeOptions();
            int selectedIndex = FindSelectedIndex(assemblyQualifiedNameProperty.stringValue, options);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            int newIndex = EditorGUI.Popup(
                position,
                label.text,
                selectedIndex,
                options.Select(option => option.DisplayName).ToArray());

            if (EditorGUI.EndChangeCheck())
            {
                assemblyQualifiedNameProperty.stringValue = options[newIndex].AssemblyQualifiedName;
            }

            EditorGUI.EndProperty();
        }

        private static int FindSelectedIndex(string assemblyQualifiedName, IReadOnlyList<TypeReferenceOption> options)
        {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
            {
                return 0;
            }

            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].AssemblyQualifiedName, assemblyQualifiedName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        private static TypeReferenceOption[] GetTypeOptions()
        {
            List<TypeReferenceOption> options = new()
            {
                new TypeReferenceOption(NoneOption.text, string.Empty)
            };

            IEnumerable<Type> panelTypes = TypeCache.GetTypesDerivedFrom<UIKitMenuPanelBase>()
                .Where(type => type != null && !type.IsAbstract && !type.IsGenericType)
                .OrderBy(type => type.FullName, StringComparer.Ordinal);

            foreach (Type panelType in panelTypes)
            {
                options.Add(new TypeReferenceOption(panelType.FullName, panelType.AssemblyQualifiedName));
            }

            return options.ToArray();
        }

        private readonly struct TypeReferenceOption
        {
            public TypeReferenceOption(string displayName, string assemblyQualifiedName)
            {
                DisplayName = displayName;
                AssemblyQualifiedName = assemblyQualifiedName;
            }

            public string DisplayName { get; }

            public string AssemblyQualifiedName { get; }
        }
    }
}
