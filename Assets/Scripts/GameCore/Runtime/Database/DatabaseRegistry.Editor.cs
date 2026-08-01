using System;
using System.Collections.Generic;
using System.Linq;
using azixMcAze.SerializableDictionary;

namespace FantasyWord.GameCore
{
#if UNITY_EDITOR
    public partial class DatabaseRegistry
    {
        public void SetEntries(IEnumerable<DatabaseEntry> entries)
        {
            m_entries = new SerializableDictionary<string, DatabaseEntry>(entries.ToDictionary(entry => entry.GetAssetGUID(), entry => entry));
        }

        public void Register(DatabaseEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            EnsureCollectionsInitialized();
            m_entries.TryAdd(entry.GetAssetGUID(), entry);
        }

        public void Unregister(DatabaseEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            RemoveAt(entry.GetAssetGUID());
        }

        public void RemoveAt(string guid)
        {
            EnsureCollectionsInitialized();
            m_entries.Remove(guid);
        }

        public int RemoveMissingReferences()
        {
            EnsureCollectionsInitialized();

            List<string> keysToRemove = new();
            foreach (var entry in m_entries)
            {
                if (entry.Value == null)
                {
                    keysToRemove.Add(entry.Key);
                }
            }

            foreach (string key in keysToRemove)
            {
                m_entries.Remove(key);
            }

            return keysToRemove.Count;
        }

        public void ClearEntries()
        {
            EnsureCollectionsInitialized();
            m_entries.Clear();
        }

        public void RemoveConversion(string from)
        {
            EnsureCollectionsInitialized();
            m_GUIDConversionMap.Remove(from);
        }

        public void SetConversion(string from, string to)
        {
            EnsureCollectionsInitialized();
            m_GUIDConversionMap[from] = to;
        }
    }
#endif
}
