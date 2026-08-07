using UnityEditor;
using UnityEngine;

namespace GameCore
{
    [CustomPropertyDrawer(typeof(DatabaseEntryReference<>), true)]
    public class DatabaseEntryReferencePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty instanceProperty = property.FindPropertyRelative("m_instance");
            EditorGUI.ObjectField(position, instanceProperty, label);
            EditorGUI.EndProperty();
        }
    }
}

