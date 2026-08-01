using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// Mod 内容包运行时 API。
    /// 它只负责发现、校验、启停状态和独立 YooAsset 包加载，不直接接管 GameCore 玩法系统。
    /// </summary>
    public static class ModAPI
    {
        public const string DefaultAPIVersion = "0.1.0";

#if !UNITY_EDITOR && UNITY_ANDROID
        public static readonly string LoadingPath = Path.Combine(Application.persistentDataPath, "Mods");
#else
        public static readonly string LoadingPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? Application.persistentDataPath, "Mods");
#endif

        private static readonly List<ModInfo> ModInfos = new();
        private static ModConfig s_config;
        private static event Action s_refreshed;

        public static bool Initialized { get; private set; }

        public static void AddRefreshedListener(Action listener)
        {
            if (listener != null)
            {
                s_refreshed += listener;
            }
        }

        public static void RemoveRefreshedListener(Action listener)
        {
            if (listener != null)
            {
                s_refreshed -= listener;
            }
        }

        public static async UniTask Initialize(ModConfig modConfig = null, IModLoader modLoader = null)
        {
            if (Initialized)
            {
                Debug.LogError("[ModAPI] Mod api is already initialized.");
                return;
            }

            s_config = modConfig ?? ModConfig.LoadOrCreate();
            modLoader ??= new ModLoader(s_config, new APIValidator(s_config.ApiVersion));

            ModInfos.Clear();
            if (await modLoader.LoadAllModsAsync(ModInfos))
            {
                for (int i = s_config.States.Count - 1; i >= 0; i--)
                {
                    ModState state = s_config.States[i];
                    if (ModInfos.All(info => info.FullName != state.fullName))
                    {
                        Debug.LogWarning($"[ModAPI] Missing mod {state.fullName}.");
                        s_config.States.RemoveAt(i);
                    }
                }

                s_config.Save();
                Initialized = true;
                NotifyRefreshed();
            }
        }

        /// <summary>
        /// 清理本次运行的 Mod 清单状态。资源包生命周期由 ResourceSystem 统一回收。
        /// </summary>
        public static void Shutdown()
        {
            ModInfos.Clear();
            s_config = null;
            s_refreshed = null;
            Initialized = false;
        }

        public static void DeleteMod(ModInfo modInfo)
        {
            EnsureInitialized();
            if (s_config.GetModState(modInfo) == ModStatus.Delete)
            {
                return;
            }

            s_config.DeleteMod(modInfo);
            s_config.Save();
            ModInfos.Remove(modInfo);
            NotifyRefreshed();
        }

        public static void SetModEnabled(ModInfo modInfo, bool isEnabled)
        {
            EnsureInitialized();
            if (s_config.GetModState(modInfo) == (isEnabled ? ModStatus.Enabled : ModStatus.Disabled))
            {
                return;
            }

            s_config.SetModEnabled(modInfo, isEnabled);
            s_config.Save();
            NotifyRefreshed();
        }

        public static ModStatus GetModState(ModInfo modInfo)
        {
            EnsureInitialized();
            return s_config.GetModState(modInfo);
        }

        public static ModInfo[] CreateInfoSnapshot()
        {
            EnsureInitialized();
            return ModInfos.ToArray();
        }

        public static void UnZipAll(string path, bool allDirectories)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (string zip in Directory.GetFiles(path, "*.zip", allDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                ExtractAndDeleteArchive(zip);
            }
        }

        public static async UniTask UnZipAllAsync(string path, bool allDirectories)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            string[] zips = Directory.GetFiles(path, "*.zip", allDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            UniTask[] tasks = zips.Select(zip => UniTask.RunOnThreadPool(() =>
            {
                ExtractAndDeleteArchive(zip);
            })).ToArray();

            await UniTask.WhenAll(tasks);
        }

        public static void DeleteModFromDisk(ModInfo modInfo)
        {
            DeleteModFromDisk(modInfo, LoadingPath);
        }

        internal static void DeleteModFromDisk(ModInfo modInfo, string loadingRoot)
        {
            if (modInfo == null || string.IsNullOrWhiteSpace(modInfo.FilePath) || !Directory.Exists(modInfo.FilePath))
            {
                return;
            }

            string fullPath = Path.GetFullPath(modInfo.FilePath);
            if (!IsPathInsideDirectory(loadingRoot, fullPath))
            {
                Debug.LogError($"[ModAPI] Refuse to delete mod outside loading root: {fullPath}");
                return;
            }

            Directory.Delete(fullPath, true);
        }

        public static async UniTask<ModInfo> LoadModInfo(string modInfoPath)
        {
            ModInfo modInfo = JsonConvert.DeserializeObject<ModInfo>(await File.ReadAllTextAsync(modInfoPath));
            if (modInfo == null)
            {
                return null;
            }

            string directory = Path.GetDirectoryName(modInfoPath);
            modInfo.FilePath = string.IsNullOrWhiteSpace(directory) ? null : Path.GetFullPath(directory);
            return modInfo;
        }

        private static void ExtractAndDeleteArchive(string zip)
        {
            string outputDirectory = Path.GetDirectoryName(zip);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return;
            }

            if (ZipArchiveExtractor.UnzipFile(zip, outputDirectory))
            {
                File.Delete(zip);
            }
        }

        private static bool IsPathInsideDirectory(string rootPath, string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(candidatePath))
            {
                return false;
            }

            string normalizedRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(rootPath));
            string normalizedCandidate = Path.GetFullPath(candidatePath);
            return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static void EnsureInitialized()
        {
            if (!Initialized)
            {
                throw new InvalidOperationException("ModAPI is not initialized.");
            }
        }

        private static void NotifyRefreshed()
        {
            s_refreshed?.Invoke();
        }
    }
}
