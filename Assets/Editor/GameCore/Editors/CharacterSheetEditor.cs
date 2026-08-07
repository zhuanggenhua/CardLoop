using UnityEditor;
using UnityEngine;

namespace GameCore
{
    [CustomEditor(typeof(CharacterSheet))]
    public class CharacterSheetEditor : DatabaseEntryEditor
    {
        private int m_previewLevel = Constants.MinLevel;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            CharacterSheet sheet = target as CharacterSheet;
            CharacterSheetFeedbackEditorUtility.DrawFeedbackWarnings(sheet);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Evolution Preview", EditorStyles.boldLabel);
            m_previewLevel = EditorGUILayout.IntSlider(
                "Level",
                m_previewLevel,
                Constants.MinLevel,
                Constants.MaxLevel);

            Stats previewStats = sheet.GetStatsAtLevel(m_previewLevel);
            GUI.enabled = false;
            foreach (FormalAttributeDefinition attribute in FormalAttributeCatalog.Definitions)
            {
                EditorGUILayout.IntField(attribute.DisplayName, previewStats[attribute.Stat]);
            }
            GUI.enabled = true;
        }
    }
}
