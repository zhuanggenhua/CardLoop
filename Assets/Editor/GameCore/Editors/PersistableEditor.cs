using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [CustomEditor(typeof(Persistable), true), CanEditMultipleObjects]
    public class PersistableEditor : FancyEditor<Persistable>
    {
        private bool m_editMode = false;
        private string m_identifier = null;

        private void DrawViewMode(Persistable target)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Edit") && EditorUtility.DisplayDialog(
                $"Editing identifier for {target.name}",
                "WARNING: When manually editing the identifier, you need to ensure that each identifier is unique. This mode is not recommended unless ABSOLUTELY NECESSARY.\n\n" +
                "Are you sure you want to proceed?",
                "Yes", "No"
            ))
            {
                m_editMode = true;
                m_identifier = PersistanceUtil.GetIdentifierFrom(target);
            }

            if (GUILayout.Button("Copy"))
            {
                EditorGUIUtility.systemCopyBuffer = PersistanceUtil.GetIdentifierFrom(target);
                Debug.Log($"Identifier {PersistanceUtil.GetIdentifierFrom(target)} copied to clipboard.");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEditMode(Persistable target, string identifier)
        {
            m_identifier = identifier;

            if (GUILayout.Button("Regen"))
            {
                m_identifier = PersistanceUtil.GenerateIdentifier();
            }

            GUI.enabled = !string.IsNullOrEmpty(m_identifier);

            bool identifierChanged = PersistanceUtil.GetIdentifierFrom(target) != m_identifier;

            GUI.enabled = identifierChanged;

            if (GUILayout.Button("Apply"))
            {
                GUI.FocusControl(null); // <-- required otherwise the text field won't update until the user press ENTER
                if (m_identifier != PersistanceUtil.GetIdentifierFrom(target) && EditorUtility.DisplayDialog(
                    $"Changing identifier for {target.name}",
                    "WARNING: Any save files that rely on this previous identifier may fail to load the data correctly.\n\n" +
                    "Previous identifier: " + PersistanceUtil.GetIdentifierFrom(target) + "\n" +
                    "New identifier: " + m_identifier + "\n\n" +
                    "Are you sure you want to proceed?",
                    "Yes", "No"
                ))
                {
                    PersistanceUtil.SetIdentifierFor(target, m_identifier);
                }

                m_editMode = false;
            }

            GUI.enabled = true;

            if (GUILayout.Button("Cancel"))
            {
                GUI.FocusControl(null);
                m_editMode = false;
            }

            GUI.color = Color.white;
            GUI.enabled = true;
        }

        private string DrawIdentifierField(Persistable target)
        {
            EditorGUILayout.PrefixLabel("Persistable Identifier");
            GUI.enabled = m_editMode;
            string identifier = EditorGUILayout.TextField(m_editMode ? m_identifier : PersistanceUtil.GetIdentifierFrom(target));
            GUI.enabled = true;
            return identifier;
        }

        protected override void DrawCustomInspector(Persistable target)
        {
            EditorGUILayout.LabelField("Persistable Settings", EditorStyles.boldLabel);

            if (target.IsPreInstanced())
            {
                EditorGUILayout.BeginHorizontal();
                string identifier = DrawIdentifierField(target);
                if (m_editMode)
                {
                    DrawEditMode(target, identifier);
                }
                else
                {
                    DrawViewMode(target);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}

