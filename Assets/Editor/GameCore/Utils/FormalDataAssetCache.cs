using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 编辑器正式数据缓存只认项目侧正式数据目录，避免每次资源刷新都去全项目暴力扫描。
    /// </summary>
    public static class FormalDataAssetCache
    {
        private const string FormalDataRoot = "Assets/GameData";

        static readonly string[] SearchRoots = { FormalDataRoot };

        static readonly HashSet<Type> RequestedTypes = new();
        static readonly HashSet<Type> CachedConcreteTypes = new();
        static readonly Dictionary<Type, ScriptableObject[]> AssignableAssetsCache = new();
        static readonly List<ScriptableObject> CachedAssets = new();

        static bool s_isDirty = true;

        public static T[] CreateAssignableAssetSnapshot<T>() where T : ScriptableObject
        {
            return CreateAssignableAssetSnapshot(typeof(T)).OfType<T>().ToArray();
        }

        public static ScriptableObject[] CreateAssignableAssetSnapshot(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!typeof(ScriptableObject).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.FullName} is not a ScriptableObject.", nameof(type));

            RegisterKnownType(type);
            EnsureCacheUpToDate();

            if (!AssignableAssetsCache.TryGetValue(type, out ScriptableObject[] assets))
            {
                assets = CachedAssets
                    .Where(asset => type.IsAssignableFrom(asset.GetType()))
                    .OrderBy(asset => asset.name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                AssignableAssetsCache[type] = assets;
            }

            return (ScriptableObject[])assets.Clone();
        }

        public static void RegisterKnownTypes(IEnumerable<Type> types)
        {
            if (types == null)
                return;

            foreach (Type type in types)
            {
                RegisterKnownType(type);
            }
        }

        internal static void MarkDirty()
        {
            s_isDirty = true;
            AssignableAssetsCache.Clear();
        }

        internal static bool IsFormalDataAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return assetPath.StartsWith($"{FormalDataRoot}/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(assetPath, FormalDataRoot, StringComparison.OrdinalIgnoreCase);
        }

        static void RegisterKnownType(Type type)
        {
            if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type))
                return;

            bool changed = RequestedTypes.Add(type);

            foreach (Type concreteType in ExpandConcreteTypes(type))
            {
                if (CachedConcreteTypes.Add(concreteType))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                s_isDirty = true;
            }
        }

        static IEnumerable<Type> ExpandConcreteTypes(Type type)
        {
            if (!type.IsAbstract && !type.IsGenericTypeDefinition)
            {
                yield return type;
            }

            foreach (Type derivedType in TypeCache.GetTypesDerivedFrom(type))
            {
                if (!typeof(ScriptableObject).IsAssignableFrom(derivedType))
                    continue;

                if (derivedType.IsAbstract || derivedType.IsGenericTypeDefinition)
                    continue;

                yield return derivedType;
            }
        }

        static void EnsureCacheUpToDate()
        {
            if (!s_isDirty)
                return;

            RebuildCache();
        }

        static void RebuildCache()
        {
            CachedAssets.Clear();
            AssignableAssetsCache.Clear();

            string[] existingSearchRoots = SearchRoots
                .Where(AssetDatabase.IsValidFolder)
                .ToArray();
            if (existingSearchRoots.Length == 0)
            {
                s_isDirty = false;
                return;
            }

            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (Type concreteType in CachedConcreteTypes.OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                foreach (string guid in AssetDatabase.FindAssets($"t:{concreteType.Name}", existingSearchRoots))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!seenPaths.Add(path))
                        continue;

                    ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset != null)
                    {
                        CachedAssets.Add(asset);
                    }
                }
            }

            s_isDirty = false;
        }
    }

    public sealed class FormalDataAssetCachePostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (TouchesFormalData(importedAssets)
                || TouchesFormalData(deletedAssets)
                || TouchesFormalData(movedAssets)
                || TouchesFormalData(movedFromAssetPaths))
            {
                FormalDataAssetCache.MarkDirty();
            }
        }

        static bool TouchesFormalData(IEnumerable<string> assetPaths)
        {
            if (assetPaths == null)
                return false;

            foreach (string assetPath in assetPaths)
            {
                if (!assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (FormalDataAssetCache.IsFormalDataAssetPath(assetPath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
