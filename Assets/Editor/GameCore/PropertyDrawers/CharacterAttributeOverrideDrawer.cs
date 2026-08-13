using UnityEditor;
using UnityEngine;

namespace GameCore.Editor
{
    /// <summary>
    /// 复用 EX-GAS 已生成的 FightUnit 属性选择数据，避免角色配置编辑器复制一套属性表。
    /// </summary>
    [CustomPropertyDrawer(typeof(CharacterAttributeOverride))]
    public sealed class CharacterAttributeOverrideDrawer : PropertyDrawer
    {
        private const float AttributeCodeWidth = 210.0f;
        private const float ValueLabelWidth = 52.0f;
        private const float Spacing = 4.0f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty codeProperty = property.FindPropertyRelative("m_attributeCode");
            SerializedProperty valueProperty = property.FindPropertyRelative("m_baseValue");

            Rect contentRect = EditorGUI.PrefixLabel(position, label);
            Rect codeRect = new(contentRect.x, contentRect.y, AttributeCodeWidth, contentRect.height);
            Rect valueRect = new(
                codeRect.xMax + Spacing,
                contentRect.y,
                Mathf.Max(0.0f, contentRect.width - AttributeCodeWidth - Spacing),
                contentRect.height);

            EditorGUI.BeginProperty(position, label, property);
            try
            {
                EditorGUI.PropertyField(codeRect, codeProperty, GUIContent.none);
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = ValueLabelWidth;
                EditorGUI.PropertyField(valueRect, valueProperty, new GUIContent("基础值"));
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }
    }
}
