using GameCore.Editor;
using GAS.Runtime;
using UnityEditor;
using UnityEngine;

namespace GameCore
{
    [CustomEditor(typeof(CharacterSheet))]
    public class CharacterSheetEditor : DatabaseEntryEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            CharacterSheet sheet = target as CharacterSheet;
            CharacterSheetFeedbackEditorUtility.DrawFeedbackWarnings(sheet);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("EX-GAS 属性预览", EditorStyles.boldLabel);

            AttrSetConfig config = sheet.CreateAttributeSetConfig();
            GUI.enabled = false;
            foreach (AttributeBaseSetting attribute in config.Settings)
            {
                EditorGUILayout.FloatField(
                    CharacterAttributeCodeDrawer.GetDisplayName(attribute.Code),
                    attribute.InitValue);
            }
            GUI.enabled = true;
        }
    }
}
