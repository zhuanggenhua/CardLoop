using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 正式 EX-GAS 能力根节点模式。
    /// 用于运行时实例化后选择静态、四方向或横向表现根。
    /// </summary>
    public enum EFormalGasAbilityRootMode
    {
        Static,
        Polydirectional,
        Horizontal
    }

    /// <summary>
    /// 正式 EX-GAS 能力运行时资源配置。
    /// GUID 优先指向 GameCore 数据库引用资产；需要动态内容时，资源地址统一交给 ResourceSystem/YooAsset。
    /// 以 Assets/ 开头的项目路径只允许作为编辑器证据，不是玩家构建可用的运行时地址。
    /// </summary>
    public readonly struct FormalGasAbilityRuntimeConfig
    {
        public FormalGasAbilityRuntimeConfig(
            string prefabGuid,
            string prefabPath,
            string iconGuid,
            string iconPath,
            EFormalGasAbilityRootMode abilityRootMode,
            FormalAbilityInputGateConfig inputExecution)
        {
            PrefabGuid = prefabGuid ?? string.Empty;
            PrefabPath = prefabPath ?? string.Empty;
            IconGuid = iconGuid ?? string.Empty;
            IconPath = iconPath ?? string.Empty;
            AbilityRootMode = abilityRootMode;
            InputGate = inputExecution ?? new FormalAbilityInputGateConfig();
        }

        public string PrefabGuid { get; }
        public string PrefabPath { get; }
        public string IconGuid { get; }
        public string IconPath { get; }
        public EFormalGasAbilityRootMode AbilityRootMode { get; }
        public FormalAbilityInputGateConfig InputGate { get; }

        public bool TryLoadPrefab(out GameObject prefab)
        {
            prefab = LoadDatabaseEntry<PrefabReference>(PrefabGuid)?.prefab;
            if (prefab != null)
            {
                return true;
            }

            prefab = FormalGasAbilityResourceLoader.LoadRuntimeAddressSync<GameObject>(PrefabPath);
            if (prefab != null)
            {
                return true;
            }

#if UNITY_EDITOR
            prefab = LoadEditorAsset<GameObject>(PrefabGuid);
            return prefab != null;
#else
            return false;
#endif
        }

        public bool TryLoadIcon(out Sprite icon)
        {
            icon = LoadDatabaseEntry<SpriteReference>(IconGuid)?.sprite;
            if (icon != null)
            {
                return true;
            }

            icon = FormalGasAbilityResourceLoader.LoadRuntimeAddressSync<Sprite>(IconPath);
            if (icon != null)
            {
                return true;
            }

#if UNITY_EDITOR
            icon = LoadEditorAsset<Sprite>(IconGuid);
            return icon != null;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        private static T LoadEditorAsset<T>(string guid)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrWhiteSpace(assetPath)
                ? null
                : UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }
#endif

        private static T LoadDatabaseEntry<T>(string guid)
            where T : DatabaseEntry
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            if (GameManager.Exists() && GameManager.Database != null)
            {
                return GameManager.Database.GUIDToDatabaseEntry<T>(guid);
            }

#if UNITY_EDITOR
            GameConfig config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfig.DefaultAssetPath);
            if (config != null)
            {
                UnityEditor.SerializedObject serializedConfig = new(config);
                DatabaseRegistry database = serializedConfig.FindProperty("m_databaseRegistry")?.objectReferenceValue as DatabaseRegistry;
                return database == null ? null : database.GUIDToDatabaseEntry<T>(guid);
            }
#endif

            return null;
        }
    }

    /// <summary>
    /// 正式 EX-GAS 能力运行时配置解析门面。
    /// 具体数据来源由启动流程注册，调用方只按能力编号查询。
    /// </summary>
    public static class FormalGasAbilityRuntimeConfigResolver
    {
        /// <summary>
        /// 能力编号到运行时配置的解析回调。
        /// </summary>
        public delegate bool TryResolveRuntimeConfigHandler(
            int abilityCode,
            out FormalGasAbilityRuntimeConfig config);

        private static TryResolveRuntimeConfigHandler s_handler;

        public static void RegisterTryResolveRuntimeConfigHandler(TryResolveRuntimeConfigHandler handler)
        {
            s_handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public static bool TryResolveRuntimeConfig(
            int abilityCode,
            out FormalGasAbilityRuntimeConfig config)
        {
            config = default;
            if (abilityCode <= 0 || s_handler == null)
            {
                return false;
            }

            return s_handler.Invoke(abilityCode, out config);
        }
    }

    /// <summary>
    /// 正式 GAS 资源加载门面。
    /// 正式运行时只接受 ResourceSystem / YooAsset 地址；编辑器 Assets 路径只能用于诊断和兜底。
    /// </summary>
    public static class FormalGasAbilityResourceLoader
    {
        public static bool IsEditorAssetPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }

        public static T LoadRuntimeAddressSync<T>(string address)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(address) || IsEditorAssetPath(address))
            {
                return null;
            }

            return LoadResourceSystemSync<T>(address);
        }

        public static T LoadSync<T>(string path)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            T databaseAsset = LoadDatabaseAssetSync<T>(path);
            if (databaseAsset != null)
            {
                return databaseAsset;
            }

#if UNITY_EDITOR
            if (IsEditorAssetPath(path))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            }
#endif

            if (IsEditorAssetPath(path))
            {
                Debug.LogError(
                    $"GameCore 正式 GAS 资源地址不能使用编辑器项目路径：{path}。请改为 ResourceSystem / YooAsset 地址。");
                return null;
            }

            return LoadResourceSystemSync<T>(path);
        }

        private static T LoadResourceSystemSync<T>(string address)
            where T : UnityEngine.Object
        {
            try
            {
                ResourceHandle<T> handle = ResourceSystem.LoadAssetAsync<T>(address);
                return handle.WaitForCompletion();
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"GameCore 正式 GAS 资源加载失败：{address}。{exception.Message}");
#endif
                return null;
            }
        }

        private static T LoadDatabaseAssetSync<T>(string guid)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            DatabaseRegistry database = ResolveDatabase();
            if (database == null)
            {
                return null;
            }

            if (typeof(T) == typeof(GameObject))
            {
                return database.GUIDToDatabaseEntry<PrefabReference>(guid)?.prefab as T;
            }

            if (typeof(T) == typeof(Sprite))
            {
                return database.GUIDToDatabaseEntry<SpriteReference>(guid)?.sprite as T;
            }

            return null;
        }

        private static DatabaseRegistry ResolveDatabase()
        {
            if (GameManager.Exists() && GameManager.Database != null)
            {
                return GameManager.Database;
            }

#if UNITY_EDITOR
            GameConfig config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfig.DefaultAssetPath);
            if (config != null)
            {
                UnityEditor.SerializedObject serializedConfig = new(config);
                return serializedConfig.FindProperty("m_databaseRegistry")?.objectReferenceValue as DatabaseRegistry;
            }
#endif

            return null;
        }

        public static UnityEngine.Object LoadSync(string path, Type type)
        {
            if (type == typeof(GameObject))
            {
                return LoadSync<GameObject>(path);
            }

            if (type == typeof(Sprite))
            {
                return LoadSync<Sprite>(path);
            }

            if (type == typeof(AudioClip))
            {
                return LoadSync<AudioClip>(path);
            }

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(path) &&
                path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath(path, type);
            }
#endif

            return null;
        }

        public static void LoadAsync(string path, Type type, Action<UnityEngine.Object> onComplete)
        {
            onComplete?.Invoke(LoadSync(path, type));
        }

        public static void Release(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

#if !UNITY_EDITOR
            Resources.UnloadAsset(asset);
#endif
        }
    }
}

