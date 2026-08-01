using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
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

            EditorGUILayout.Space();
            EditorGUILayout.IntField(
                "Experience Required",
                sheet.GetExperienceRequiredAtLevel(m_previewLevel));
            EditorGUILayout.IntField(
                "Experience Required Total",
                GetTotalExperienceRequired(sheet, m_previewLevel));
            EditorGUILayout.IntField(
                "Kill Experience Reward",
                sheet.GetExperienceRewardAtLevel(m_previewLevel));
            EditorGUILayout.IntField(
                "Kill Money Reward",
                sheet.GetMoneyRewardAtLevel(m_previewLevel));
            GUI.enabled = true;
        }

        private static int GetTotalExperienceRequired(CharacterSheet sheet, int level)
        {
            int total = 0;
            for (int currentLevel = Constants.MinLevel; currentLevel <= level; ++currentLevel)
            {
                total += sheet.GetExperienceRequiredAtLevel(currentLevel);
            }

            return total;
        }
    }
}
