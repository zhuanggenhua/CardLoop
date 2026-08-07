using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using azixMcAze.SerializableDictionary;

namespace GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.Root + nameof(DatabaseRegistry))]
    public partial class DatabaseRegistry : ScriptableObject
    {
        public bool autoAddNewDatabaseEntries => m_autoAddNewDatabaseEntries;
        public bool autoRemoveDatabaseEntries => m_autoRemoveDatabaseEntries;
        public int entryCount => m_entries?.Count ?? 0;
        public int conversionCount => m_GUIDConversionMap?.Count ?? 0;

        [Header("Automation Settings")]
        [SerializeField] private bool m_autoAddNewDatabaseEntries = true;
        [SerializeField] private bool m_autoRemoveDatabaseEntries = true;

        [Header("Database Content")]
        [SerializeField] private SerializableDictionary<string, DatabaseEntry> m_entries = null;
        [SerializeField] private SerializableDictionary<string, string> m_GUIDConversionMap = null;

        public KeyValuePair<string, DatabaseEntry>[] GetEntries()
        {
            EnsureCollectionsInitialized();
            return m_entries.ToArray();
        }

        public DatabaseEntryReference<T> CreateReference<T>(T entry) where T : DatabaseEntry
        {
            if (TryCreateReference(entry, out DatabaseEntryReference<T> reference))
            {
                return reference;
            }

            throw new InvalidOperationException(
                $"[{nameof(DatabaseRegistry)}] 数据库资产 {entry?.name ?? "<null>"} 未登记，不能创建稳定引用。");
        }

        public bool TryCreateReference<T>(T entry, out DatabaseEntryReference<T> reference) where T : DatabaseEntry
        {
            reference = null;
            string guid = DatabaseEntryToGUID(entry);
            if (string.IsNullOrWhiteSpace(guid))
            {
                return false;
            }

            reference = new DatabaseEntryReference<T>(guid);
            return true;
        }

        public T LoadFromReference<T>(DatabaseEntryReference<T> reference) where T : DatabaseEntry
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.guid))
            {
                return null;
            }

            return GUIDToDatabaseEntry<T>(reference.guid);
        }

        public T GUIDToDatabaseEntry<T>(string guid) where T : DatabaseEntry
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            EnsureCollectionsInitialized();
            HashSet<string> visited = new();

            // Convert the GUID if it exists in the conversion map.
            while (m_GUIDConversionMap.ContainsKey(guid))
            {
                guid = m_GUIDConversionMap[guid];
                if (visited.Contains(guid))
                {
                    Debug.LogError($"Circular reference detected in DatabaseRegistry: {guid}");
                    return null;
                }

                visited.Add(guid);
            }

            return m_entries.TryGetValue(guid, out DatabaseEntry entry) ? entry as T : null;
        }

        public string DatabaseEntryToGUID<T>(T instance) where T : DatabaseEntry
        {
            EnsureCollectionsInitialized();
            if (!instance)
            {
                Debug.LogError($"[{nameof(DatabaseRegistry)}] 不能为缺失的数据库资产创建引用。");
                return string.Empty;
            }

            string guid = m_entries.FirstOrDefault(entry => entry.Value == instance).Key;
            if (string.IsNullOrWhiteSpace(guid))
            {
                Debug.LogError($"[{nameof(DatabaseRegistry)}] 数据库资产 {instance.name} 未登记，不能创建稳定引用。", instance);
                return string.Empty;
            }

            return guid;
        }

        public bool HasGUID(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return false;
            }

            EnsureCollectionsInitialized();
            return m_entries.ContainsKey(guid);
        }

        public bool HasGUIDConversion(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return false;
            }

            EnsureCollectionsInitialized();
            return m_GUIDConversionMap.ContainsKey(guid);
        }

        private void EnsureCollectionsInitialized()
        {
            m_entries ??= new SerializableDictionary<string, DatabaseEntry>();
            m_GUIDConversionMap ??= new SerializableDictionary<string, string>();
        }
    }
}
