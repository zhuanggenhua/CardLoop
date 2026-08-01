using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [CustomEditor(typeof(DatabaseRegistry))]
    public class DatabaseRegistryEditor : FancyEditor<DatabaseRegistry>
    {
        private struct RegistryInfo
        {
            public int entryCount;
            public int missingReferenceCount;
            public int conversionCount;
        }

        protected override bool m_drawScriptHeader => false;

        private static DatabaseEntry[] GetFormalDatabaseEntries()
        {
            return FormalDataAssetCache.CreateAssignableAssetSnapshot<DatabaseEntry>();
        }

        private RegistryInfo CollectRegistryInfo(DatabaseRegistry registry)
        {
            RegistryInfo info = new()
            {
                entryCount = registry.entryCount,
                missingReferenceCount = 0,
                conversionCount = registry.conversionCount
            };

            foreach (var entry in registry.GetEntries())
            {
                if (entry.Value == null)
                {
                    ++info.missingReferenceCount;
                }
            }

            return info;
        }

        private void DrawSummary(RegistryInfo info)
        {
            EditorGUILayout.HelpBox($"Entries: {info.entryCount}\nConversions: {info.conversionCount}\nMissing References: {info.missingReferenceCount}", MessageType.Info);
        }

        protected override void DrawCustomInspector(DatabaseRegistry registry)
        {
            RegistryInfo info = CollectRegistryInfo(registry);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Regenerate"))
            {
                bool confirmed = info.entryCount == 0 || EditorUtility.DisplayDialog("Confirmation", "Are you sure you want to replace the database registry content with all database entries? The current content of the database will be overwritten.", "Regenerate", "Cancel");

                if (confirmed)
                {
                    registry.SetEntries(GetFormalDatabaseEntries());
                    registry.SaveAsset();
                }
            }

            if (GUILayout.Button("Add Missing Entries"))
            {
                var entries = GetFormalDatabaseEntries()
                    .Where(entry => !registry.HasGUID(entry.GetAssetGUID()))
                    .ToArray();

                foreach (var entry in entries)
                {
                    registry.Register(entry);
                }

                registry.SaveAsset();
            }

            bool wasGUIEnabled = GUI.enabled;
            GUI.enabled = info.entryCount > 0;
            if (GUILayout.Button("Clear All Entries"))
            {
                bool confirmed = EditorUtility.DisplayDialog("Confirmation", "Are you sure you want to clear the database registry? This action cannot be undone.", "Clear All Entries", "Cancel");

                if (confirmed)
                {
                    registry.ClearEntries();
                    registry.SaveAsset();
                }
            }

            GUI.enabled = wasGUIEnabled;

            wasGUIEnabled = GUI.enabled;
            GUI.enabled = info.missingReferenceCount > 0;
            if (GUILayout.Button("Remove Missing References"))
            {
                registry.RemoveMissingReferences();
                registry.SaveAsset();
            }

            GUI.enabled = wasGUIEnabled;

            GUILayout.EndHorizontal();

            DrawSummary(info);
            EditorGUILayout.HelpBox("正式数据库条目只从 Assets/GameData 读取；第三方 demo 资产和临时已加载对象不会再被当成正式数据库真相。", MessageType.None);
        }
    }
}
