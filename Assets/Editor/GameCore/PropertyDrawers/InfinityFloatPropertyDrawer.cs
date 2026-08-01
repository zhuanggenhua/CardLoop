using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [CustomPropertyDrawer(typeof(InfinityFloatAttribute))]
    public class InfinityFloatPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Check if the property is a float
            if (property.propertyType == SerializedPropertyType.Float)
            {
                float value = property.floatValue;

                if (value == 0f)
                {
                    string valueStr = EditorGUI.TextField(position, label, "0 (Infinite)");

                    if (valueStr != "0 (Infinite)")
                    {
                        property.floatValue = string.IsNullOrEmpty(valueStr) ? 1f : math.max(float.TryParse(valueStr, out float valueFloat) ? valueFloat : 0.0f, 0.0f);
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
                else
                {
                    // Regular float field when value is not 0
                    EditorGUI.BeginChangeCheck();
                    float newValue = math.max(EditorGUI.FloatField(position, label, value), 0.0f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.floatValue = newValue;
                    }
                }
            }
            else
            {
                // Show error if not used on a float
                EditorGUI.LabelField(position, label, new GUIContent("Use InfinityFloat with float only"));
            }
        }
    }
}

