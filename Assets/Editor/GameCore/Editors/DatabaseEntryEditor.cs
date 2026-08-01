using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [CustomEditor(typeof(DatabaseEntry), true)]
    public class DatabaseEntryEditor : FancyEditor<DatabaseEntry>
    {
        private static bool s_expanded = false;

        protected override bool m_drawScriptHeader => false;

        private void DrawDatabaseRegistrySettings(DatabaseEntry entry)
        {
            DatabaseRegistry[] registries = FormalDataAssetCache.CreateAssignableAssetSnapshot<DatabaseRegistry>();

            if (registries.Length == 0)
            {
                EditorGUILayout.HelpBox("No DatabaseRegistry found! This DatabaseEntry won't be usable", MessageType.Error);
            }
            else
            {
                uint pureReferenceCount = 0;
                uint convertedReferenceCount = 0;

                foreach (DatabaseRegistry registry in registries)
                {
                    string entryGUID = entry.GetAssetGUID();
                    DatabaseEntry mappedEntry = registry.GUIDToDatabaseEntry<DatabaseEntry>(entryGUID);
                    bool isReferenced = registry.HasGUID(entryGUID);
                    pureReferenceCount += isReferenced ? 1u : 0u;
                    convertedReferenceCount += registry.HasGUIDConversion(entryGUID) ? 1u : 0u;

                    EditorGUILayout.BeginHorizontal();

                    if (EditorGUILayout.Toggle($"{registry.name}", isReferenced) != isReferenced)
                    {
                        if (isReferenced)
                        {
                            registry.Unregister(entry);
                        }
                        else
                        {
                            registry.Register(entry);
                        }

                        registry.SaveAsset();
                    }

                    if (GUILayout.Button("Inspect", EditorStyles.miniButton))
                    {
                        Selection.activeObject = registry;
                    }

                    GUILayout.Label(mappedEntry != entry ? "Mapped as" : "Map to");

                    DatabaseEntry selectedEntry = (DatabaseEntry)EditorGUILayout.ObjectField(mappedEntry != entry ? mappedEntry : null, typeof(DatabaseEntry), false);

                    if (selectedEntry != null && selectedEntry != mappedEntry)
                    {
                        registry.SetConversion(entryGUID, selectedEntry.GetAssetGUID());
                        registry.SaveAsset();
                    }

                    if (mappedEntry != entry)
                    {
                        if (GUILayout.Button("Unmap", EditorStyles.miniButton))
                        {
                            registry.RemoveConversion(entryGUID);
                            registry.SaveAsset();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                if (pureReferenceCount + convertedReferenceCount == 0)
                {
                    EditorGUILayout.HelpBox("This DatabaseEntry isn't referenced (or converted) in any DatabaseRegistry!", MessageType.Warning);
                }
            }
        }

        protected override void DrawCustomInspector(DatabaseEntry entry)
        {
            s_expanded = EditorGUILayout.BeginFoldoutHeaderGroup(s_expanded, "Database Registry Settings", EditorStyles.foldoutHeader);

            if (s_expanded)
            {
                DrawDatabaseRegistrySettings(entry);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
