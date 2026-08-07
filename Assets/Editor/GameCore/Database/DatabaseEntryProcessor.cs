using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace GameCore
{
    public class DatabaseEntryProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            DatabaseRegistry[] registries = FormalDataAssetCache.CreateAssignableAssetSnapshot<DatabaseRegistry>();
            if (registries.Length == 0)
                return;

            DatabaseEntry[] importedEntries = GetImportedEntries(importedAssets);
            string[] deletedGuids = GetDeletedGuids(deletedAssets);

            if (importedEntries.Length > 0)
            {
                HandleImportedAssets(
                    registries.Where(r => r.autoAddNewDatabaseEntries),
                    importedEntries
                );
            }

            if (deletedGuids.Length > 0)
            {
                HandleDeletedAssets(
                    registries.Where(r => r.autoRemoveDatabaseEntries),
                    deletedGuids
                );
            }
        }

        private static DatabaseEntry[] GetImportedEntries(IEnumerable<string> paths)
        {
            return paths
                .Where(FormalDataAssetCache.IsFormalDataAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DatabaseEntry>)
                .Where(entry => entry != null)
                .ToArray();
        }

        private static string[] GetDeletedGuids(IEnumerable<string> paths)
        {
            return paths
                .Where(FormalDataAssetCache.IsFormalDataAssetPath)
                .Select(AssetDatabase.AssetPathToGUID)
                .Where(guid => !string.IsNullOrEmpty(guid))
                .ToArray();
        }

        private static void HandleImportedAssets(IEnumerable<DatabaseRegistry> registries, IEnumerable<DatabaseEntry> entries)
        {
            foreach (DatabaseRegistry registry in registries)
            {
                foreach (DatabaseEntry entry in entries)
                {
                    registry.Register(entry);
                }

                registry.SaveAsset();
            }
        }

        private static void HandleDeletedAssets(IEnumerable<DatabaseRegistry> registries, IEnumerable<string> guids)
        {
            foreach (DatabaseRegistry registry in registries)
            {
                foreach (string guid in guids)
                {
                    registry.RemoveAt(guid);
                }

                registry.SaveAsset();
            }
        }
    }
}
