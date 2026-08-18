using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace GameCore
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
        private static CancellationTokenSource s_initializationCancellation;
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

        public static async UniTask Initialize(
            ModConfig modConfig = null,
            IModLoader modLoader = null,
            CancellationToken cancellationToken = default)
        {
            if (Initialized)
            {
                throw new InvalidOperationException(
                    "ModAPI 已经初始化，重复初始化违反进程级唯一启动入口。");
            }

            if (s_initializationCancellation != null)
            {
                throw new InvalidOperationException(
                    "ModAPI 正在初始化，不能并发进入第二个进程级 Mod 启动入口。");
            }

            CancellationTokenSource initializationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            s_initializationCancellation = initializationCancellation;
            var loadedModInfos = new List<ModInfo>();

            try
            {
                initializationCancellation.Token.ThrowIfCancellationRequested();

                ModConfig config = modConfig ?? ModConfig.LoadOrCreate();
                config.Validate();
                modLoader ??= new ModLoader(config, new APIValidator(config.ApiVersion));
                if (!await modLoader.LoadAllModsAsync(
                    loadedModInfos,
                    initializationCancellation.Token))
                {
                    throw new InvalidOperationException(
                        "Mod 内容扫描或加载失败，ModAPI 未进入已初始化状态。");
                }

                initializationCancellation.Token.ThrowIfCancellationRequested();

                config.Save();
                initializationCancellation.Token.ThrowIfCancellationRequested();

                ModInfos.Clear();
                ModInfos.AddRange(loadedModInfos);
                s_config = config;
                Initialized = true;
                NotifyRefreshed();
            }
            catch
            {
                if (ResourceSystem.Initialized)
                {
                    for (int i = loadedModInfos.Count - 1; i >= 0; i--)
                    {
                        ModInfo mod = loadedModInfos[i];
                        if (!string.IsNullOrWhiteSpace(mod?.packageName))
                        {
                            await ResourceSystem.UnloadModPackageAsync(mod.packageName);
                        }
                    }
                }
                ResetRuntimeState();
                throw;
            }
            finally
            {
                if (ReferenceEquals(s_initializationCancellation, initializationCancellation))
                {
                    s_initializationCancellation = null;
                }

                initializationCancellation.Dispose();
            }
        }

        /// <summary>
        /// 清理本次运行的 Mod 清单状态。资源包生命周期由 ResourceSystem 统一回收。
        /// </summary>
        public static void Shutdown()
        {
            s_initializationCancellation?.Cancel();
            ResetRuntimeState();
            s_refreshed = null;
        }

        public static void DeleteMod(ModInfo modInfo)
        {
            EnsureInitialized();
            if (s_config.GetModState(modInfo) == ModStatus.Delete)
            {
                return;
            }

            ModDependencyResolver.RequireCanDelete(modInfo, ModInfos, s_config);
            s_config.DeleteMod(modInfo);
            s_config.Save();
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

        /// <summary>按当前启用状态和已加载 YooAsset 清单，生成可随单局存档的严格 Mod 集合事实。</summary>
        public static ModPackageSetSnapshot CreateActivePackageSetSnapshot()
        {
            EnsureInitialized();
            var packages = new List<ModPackageSnapshot>();
            for (int i = 0; i < ModInfos.Count; i++)
            {
                ModInfo mod = ModInfos[i];
                if (!ResourceSystem.IsModPackageLoaded(mod.packageName))
                {
                    continue;
                }
                packages.Add(new ModPackageSnapshot(
                    mod.modId,
                    mod.version,
                    ResourceSystem.GetModPackageHash(mod.packageName),
                    ResourceSystem.GetModPackageVersion(mod.packageName)));
            }
            return new ModPackageSetSnapshot(packages);
        }

        internal static void DeleteModFromDisk(ModInfo modInfo, string loadingRoot)
        {
            Directory.Delete(RequireModDirectoryForDeletion(modInfo, loadingRoot), true);
        }

        /// <summary>验证待删除目录确实存在于 Mod 根目录内，并返回规范化路径。</summary>
        internal static string RequireModDirectoryForDeletion(ModInfo modInfo, string loadingRoot)
        {
            if (modInfo == null)
            {
                throw new ArgumentNullException(nameof(modInfo));
            }
            if (string.IsNullOrWhiteSpace(modInfo.FilePath))
            {
                throw new InvalidDataException($"Mod {modInfo.DisplayName ?? "<未命名>"} 没有可删除的安装目录。");
            }

            string fullPath = Path.GetFullPath(modInfo.FilePath);
            if (!IsPathInsideDirectory(loadingRoot, fullPath))
            {
                throw new InvalidDataException($"拒绝删除 Mod 根目录外的目录：{fullPath}。");
            }
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"待删除的 Mod 目录不存在：{fullPath}。");
            }

            return fullPath;
        }

        public static ModInfo LoadModInfo(string modInfoPath)
        {
            if (string.IsNullOrWhiteSpace(modInfoPath))
            {
                throw new ArgumentException("Mod 清单路径不能为空。", nameof(modInfoPath));
            }

            string fullPath = Path.GetFullPath(modInfoPath);
            ModInfo modInfo;
            try
            {
                modInfo = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(fullPath));
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Mod 清单不是有效 JSON：{fullPath}。", exception);
            }
            if (modInfo == null)
            {
                throw new InvalidDataException($"文件 {fullPath} 没有包含有效 Mod 清单。");
            }

            string directory = Path.GetDirectoryName(fullPath);
            modInfo.FilePath = string.IsNullOrWhiteSpace(directory) ? null : Path.GetFullPath(directory);
            return modInfo;
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

        private static void ResetRuntimeState()
        {
            ModInfos.Clear();
            s_config = null;
            Initialized = false;
        }

        private static void NotifyRefreshed()
        {
            s_refreshed?.Invoke();
        }
    }
}
