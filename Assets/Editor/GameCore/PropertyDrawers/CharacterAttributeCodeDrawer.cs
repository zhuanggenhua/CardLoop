using System;
using System.Collections.Generic;
using GAS.General;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace GameCore.Editor
{
    /// <summary>
    /// 通过 EX-GAS 已生成的 FightUnit 属性缓存绘制角色属性码下拉框。
    /// 这里不读取表格、不生成编号，也不提供第二个手填身份入口。
    /// </summary>
    [CustomPropertyDrawer(typeof(CharacterAttributeCodeAttribute))]
    public sealed class CharacterAttributeCodeDrawer : PropertyDrawer
    {
        private static bool s_cacheLoaded;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                throw new InvalidOperationException(
                    $"{nameof(CharacterAttributeCodeAttribute)} 只能标记整数属性码字段：{property.propertyPath}。");
            }

            List<ValueDropdownItem> choices = GetChoices();
            if (choices == null || choices.Count == 0)
            {
                EditorGUI.HelpBox(position, "EX-GAS FightUnit 属性缓存为空，无法选择属性。", MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            try
            {
                int selectedIndex = FindChoiceIndex(choices, property.intValue);
                int invalidLabelOffset = selectedIndex >= 0 ? 0 : 1;
                string[] labels = CreateChoiceLabels(choices, selectedIndex, property.intValue);
                Rect fieldRect = EditorGUI.PrefixLabel(position, label);
                int nextIndex = EditorGUI.Popup(fieldRect, selectedIndex + invalidLabelOffset, labels);
                int nextChoiceIndex = nextIndex - invalidLabelOffset;
                if (nextChoiceIndex >= 0 && nextChoiceIndex < choices.Count)
                {
                    property.intValue = (int)choices[nextChoiceIndex].Value;
                }
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }

        private static List<ValueDropdownItem> GetChoices()
        {
            if (!s_cacheLoaded)
            {
                GeneralGasChoiceHelper.LoadCache();
                s_cacheLoaded = true;
            }

            return GeneralGasChoiceHelper.Attrs(CharacterAttributes.SetCode);
        }

        internal static string GetDisplayName(int attributeCode)
        {
            List<ValueDropdownItem> choices = GetChoices();
            if (choices == null || choices.Count == 0)
            {
                throw new InvalidOperationException(
                    "EX-GAS FightUnit 属性缓存为空，无法生成属性预览。");
            }

            int choiceIndex = FindChoiceIndex(choices, attributeCode);
            return choiceIndex >= 0
                ? choices[choiceIndex].Text
                : $"无效属性码 [{attributeCode}]";
        }

        private static int FindChoiceIndex(List<ValueDropdownItem> choices, int attributeCode)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                if ((int)choices[i].Value == attributeCode)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string[] CreateChoiceLabels(
            List<ValueDropdownItem> choices,
            int selectedIndex,
            int currentAttributeCode)
        {
            int extraLabelCount = selectedIndex >= 0 ? 0 : 1;
            string[] labels = new string[choices.Count + extraLabelCount];
            if (extraLabelCount == 1)
            {
                labels[0] = $"无效属性码 [{currentAttributeCode}]";
            }

            int offset = extraLabelCount;
            for (int i = 0; i < choices.Count; i++)
            {
                labels[i + offset] = choices[i].Text;
            }

            return labels;
        }
    }
}
