using System;
using UnityEditor;
using UnityEngine;

namespace GameCore
{
    [CustomPropertyDrawer(typeof(PersistableReference<>), true)]
    public class PersistableReferencePropertyDrawer : PropertyDrawer
    {
        private enum ReferenceMode
        {
            Identifier,
            Object
        }

        private static ReferenceMode m_mode = ReferenceMode.Identifier;
        private UnityEngine.Object m_objectValue = null;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            float dropdownWidth = 100f;
            Rect dropdownRect = new(position.x, position.y, dropdownWidth, position.height);
            dropdownRect.x -= EditorGUI.indentLevel * 15f;
            dropdownRect.width += EditorGUI.indentLevel * 15f;

            Rect fieldRect = new(position.x + dropdownWidth, position.y, position.width - dropdownWidth, position.height);
            fieldRect.x -= EditorGUI.indentLevel * 15f;
            fieldRect.width += EditorGUI.indentLevel * 15f;

            Type targetType = fieldInfo.FieldType.GetGenericArguments()[0];

            SerializedProperty identifierProperty = property.FindPropertyRelative("m_identifier");

            string[] options = { "Identifier (Any Map)", "Object (Current Map Only)" };
            m_mode = (ReferenceMode)EditorGUI.Popup(dropdownRect, (int)m_mode, options);

            if (m_mode == ReferenceMode.Identifier)
            {
                EditorGUI.PropertyField(fieldRect, identifierProperty, GUIContent.none, true);
            }
            else
            {
                if (m_objectValue == null && !string.IsNullOrEmpty(identifierProperty.stringValue))
                {
                    var persistables = UnityEngine.Object.FindObjectsByType(targetType, FindObjectsSortMode.None);
                    foreach (var persistable in persistables)
                    {
                        Persistable p = persistable as Persistable;
                        if (p != null)
                        {
                            if (p.GetPersistentIdentifier() == identifierProperty.stringValue)
                            {
                                m_objectValue = persistable;
                                break;
                            }
                        }
                    }
                }

                EditorGUI.BeginChangeCheck();
                m_objectValue = EditorGUI.ObjectField(fieldRect, m_objectValue, targetType, true);
                if (EditorGUI.EndChangeCheck())
                {
                    if (m_objectValue != null)
                    {
                        Persistable persistable = m_objectValue as Persistable;
                        if (persistable != null)
                        {
                            identifierProperty.stringValue = persistable.GetPersistentIdentifier();
                        }
                        else
                        {
                            identifierProperty.stringValue = null;
                        }
                    }
                    else
                    {
                        identifierProperty.stringValue = null;
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
