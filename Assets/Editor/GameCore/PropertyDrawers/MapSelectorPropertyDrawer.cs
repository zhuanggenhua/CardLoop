using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameCore
{
    [CustomPropertyDrawer(typeof(MapSelectorAttribute))]
    public class MapSelectorPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "MapSelector 只能用于 YooAsset 场景地址字符串");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            position.x -= EditorGUI.indentLevel * 15f;
            position.width += EditorGUI.indentLevel * 15f;

            string[] sceneAddresses = SceneUtil.CreateSceneAddressSnapshot();
            string[] options = new string[] { "Current" }.Concat(sceneAddresses).ToArray();

            int currentIndex = Array.IndexOf(options, property.stringValue);
            if (currentIndex < 0) currentIndex = 0;

            int selectedIndex = EditorGUI.Popup(position, currentIndex, options);

            if (selectedIndex == 0)
            {
                property.stringValue = string.Empty;
            }
            else if (selectedIndex > 0 && selectedIndex < options.Length)
            {
                property.stringValue = options[selectedIndex];
            }

            EditorGUI.EndProperty();
        }
    }
}
