using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [CustomPropertyDrawer(typeof(PersistableCheckpoint))]
    public class PersistableCheckpointPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Draw the foldout header
            position.height = EditorGUIUtility.singleLineHeight;
            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                SerializedProperty mapProperty = property.FindPropertyRelative("map");
                SerializedProperty instanceProperty = property.FindPropertyRelative("instance");

                position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                uint instancePropertyPrevious = instanceProperty.contentHash;
                EditorGUI.PropertyField(position, instanceProperty);
                position.y += EditorGUI.GetPropertyHeight(instanceProperty) + EditorGUIUtility.standardVerticalSpacing;

                if (instanceProperty.contentHash != instancePropertyPrevious)
                {
                    string currentIdentifier = GetPersistableReferenceIdentifier(instanceProperty);

                    if (!string.IsNullOrEmpty(currentIdentifier))
                    {
                        Checkpoint[] checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
                        Checkpoint referencedCheckpoint = checkpoints.FirstOrDefault(c =>
                        {
                            Persistable persistable = c.GetComponent<Persistable>();
                            return persistable != null &&
                                !string.IsNullOrEmpty(persistable.GetPersistentIdentifier()) &&
                                persistable.GetPersistentIdentifier() == currentIdentifier;
                        });

                        if (referencedCheckpoint != null)
                        {
                            mapProperty.stringValue = string.Empty;
                        }
                    }
                }

                EditorGUI.PropertyField(position, mapProperty);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            if (property.isExpanded)
            {
                SerializedProperty instanceProperty = property.FindPropertyRelative("instance");
                height += EditorGUI.GetPropertyHeight(instanceProperty) + EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        private string GetPersistableReferenceIdentifier(SerializedProperty persistableRefProperty)
        {
            SerializedProperty identifierProperty = persistableRefProperty.FindPropertyRelative("m_identifier");
            return identifierProperty != null ? identifierProperty.stringValue : string.Empty;
        }
    }
}

