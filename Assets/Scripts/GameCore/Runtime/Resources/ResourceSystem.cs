using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YokiFrame;
using YooAsset;
using UObject = UnityEngine.Object;

namespace GameCore
{
    /// <summary>
    /// 项目动态资源入口。默认包由 YokiFrame 的 YooInit 初始化，外部 Mod 各自使用独立 YooAsset 包。
    /// DatabaseRegistry 与稳定 ID 仍是玩法数据真相，本类型只负责资源定位和生命周期。
    /// </summary>
    public static class ResourceSystem
    {
        private const byte AssetLoadOperationType = 0;
        private const byte InstantiateOperationType = 1;

        private static readonly HashSet<ResourceOperationState> ActiveOperations = new();
        private static readonly List<ModPackageEntry> ModPackages = new();
        private static ResourceSystemSceneLoaderPool s_sceneLoaderPool;
        private static bool s_ownsResourceRuntime;
        private static CancellationTokenSource s_initializationCancellation;

        public static bool Initialized =>
            s_ownsResourceRuntime &&
            DefaultPackage != null &&
            YooInit.Initialized &&
            YooAssets.IsInitialized;
        public static ResourcePackage DefaultPackage { get; private set; }

        /// <summary>
        /// 多地址加载的合并策略。YooAsset 没有 Addressables 标签合并语义，项目只保留直接地址集合的兼容入口。
        /// </summary>
        public enum MergeMode
        {
            None = 0,
            UseFirst = 0,
            Union,
            Intersection
        }

        private sealed class ModPackageEntry
        {
            public ResourcePackage Package;
            public string PackageHash;
        }

        /// <summary>
        /// 初始化默认资源包，并配置 UIKit 与 SceneKit 使用项目的 YooAsset 后端。
        /// </summary>
        public static async UniTask InitializeAsync(
            YooInitConfig config = null,
            CancellationToken cancellationToken = default)
        {
            if (Initialized)
            {
                throw new InvalidOperationException(
                    "资源系统已经初始化，重复初始化违反进程级唯一启动入口。");
            }

            EnsureReadyForInitialization();

            YooInitConfig initializationConfig = config ?? new YooInitConfig();
            ValidateInitializationConfig(initializationConfig);

            s_ownsResourceRuntime = true;
            CancellationTokenSource initializationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            s_initializationCancellation = initializationCancellation;
            try
            {
                initializationCancellation.Token.ThrowIfCancellationRequested();

                // YokiFrame 取消等待时不会回滚已登记的资源包；先让插件完成，再在提交前检查本轮取消。
                await YooInit.InitAsync(initializationConfig);
                initializationCancellation.Token.ThrowIfCancellationRequested();

                DefaultPackage = YooInit.DefaultPackage ??
                    throw new InvalidOperationException("YokiFrame.YooInit 未提供默认资源包，请检查 YooInitConfig.PackageNames。");

                YooInitUIKitExt.ConfigureUIKit();
                s_sceneLoaderPool = new ResourceSystemSceneLoaderPool();
                SceneKit.SetLoaderPool(s_sceneLoaderPool);
            }
            catch (Exception initializationException)
            {
                try
                {
                    RollbackFailedInitialization();
                }
                catch (Exception shutdownException)
                {
                    throw new AggregateException(
                        "资源系统初始化失败，且初始化后的回滚也失败。",
                        initializationException,
                        shutdownException);
                }

                if (HasYokiFramePartialInitializationState())
                {
                    throw new InvalidOperationException(
                        "资源系统初始化失败，YokiFrame 保留了未完成的资源包状态。"
                        + "当前插件没有公开的完整回滚入口；请重启 Unity Editor，或在获得插件源码修改授权后修复该插件生命周期。",
                        initializationException);
                }

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
        /// 释放项目持有的全部句柄和资源包。每个包必须先销毁，再从 YooAsset 注册表移除。
        /// </summary>
        public static void Shutdown()
        {
            if (!Initialized && s_initializationCancellation != null)
            {
                // 初始化尚未交付完整资源状态时，只取消本轮初始化；回滚由初始化入口统一完成。
                s_initializationCancellation.Cancel();
                return;
            }

            if (!s_ownsResourceRuntime)
            {
                if (YooInit.Initialized || YooAssets.IsInitialized ||
                    HasYokiFramePartialInitializationState() || HasLocalResourceState())
                {
                    throw new InvalidOperationException(
                        "资源系统尚未取得资源运行时所有权，不能关闭其它入口或未完成初始化保留的状态。");
                }

                ResetState();
                return;
            }

            ShutdownOwnedRuntime();
        }

        private static void ShutdownOwnedRuntime()
        {
            if (!YooInit.Initialized || !YooAssets.IsInitialized)
            {
                throw new InvalidOperationException(
                    "资源系统已标记为初始化，但 YokiFrame.YooInit 或 YooAsset 的状态不完整。");
            }

            foreach (ResourceOperationState operation in ActiveOperations.ToArray())
            {
                operation.Release();
            }

            YooInit.Dispose();
            YooAssets.Destroy();
            ResetState();
        }

        private static void RollbackFailedInitialization()
        {
            foreach (ResourceOperationState operation in ActiveOperations.ToArray())
            {
                operation.Release();
            }

            if (YooInit.Initialized)
            {
                YooInit.Dispose();
            }

            if (YooAssets.IsInitialized)
            {
                YooAssets.Destroy();
            }

            ResetState();
        }

        /// <summary>
        /// 从 Mod 目录初始化一个独立资源包。目录必须是 YooAsset 对应包名的完整构建输出。
        /// </summary>
        public static async UniTask<ResourcePackage> LoadModPackageAsync(
            string packageName,
            string packageDirectory,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Mod 资源包名称不能为空。", nameof(packageName));
            }

            if (!Directory.Exists(packageDirectory))
            {
                throw new DirectoryNotFoundException($"Mod 资源包目录不存在：{packageDirectory}");
            }

            ModPackageEntry existing = ModPackages.FirstOrDefault(entry =>
                string.Equals(entry.Package.PackageName, packageName, StringComparison.Ordinal));
            if (existing != null)
            {
                return existing.Package;
            }

            if (YooAssets.TryGetPackage(packageName, out _))
            {
                throw new InvalidOperationException($"资源包名称重复：{packageName}");
            }

            ResourcePackage package = YooAssets.CreatePackage(packageName);
            try
            {
                var fileSystemParameters =
                    FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(Path.GetFullPath(packageDirectory));
                var options = new CustomPlayModeOptions
                {
                    AutoUnloadBundleWhenUnused = true
                };
                options.FileSystemParameterList.Add(fileSystemParameters);

                InitializePackageOperation initialize = package.InitializePackageAsync(options);
                await initialize;
                EnsureSucceeded(initialize, $"初始化 Mod 资源包 {packageName}");
                cancellationToken.ThrowIfCancellationRequested();

                RequestPackageVersionOperation version = package.RequestPackageVersionAsync();
                await version;
                EnsureSucceeded(version, $"读取 Mod 资源包版本 {packageName}");
                cancellationToken.ThrowIfCancellationRequested();

                string packageHashFile = Path.Combine(
                    Path.GetFullPath(packageDirectory),
                    YooAssetConfiguration.GetPackageHashFileName(packageName, version.PackageVersion));
                if (!File.Exists(packageHashFile))
                {
                    throw new InvalidDataException(
                        $"Mod 资源包 {packageName} 缺少 YooAsset 官方包哈希文件：{packageHashFile}");
                }
                string packageHash = File.ReadAllText(packageHashFile).Trim();
                if (string.IsNullOrWhiteSpace(packageHash))
                {
                    throw new InvalidDataException($"Mod 资源包 {packageName} 的 YooAsset 包哈希文件为空。");
                }

                LoadPackageManifestOperation manifest = package.LoadPackageManifestAsync(
                    new LoadPackageManifestOptions(version.PackageVersion, 60));
                await manifest;
                EnsureSucceeded(manifest, $"加载 Mod 资源清单 {packageName}");
                cancellationToken.ThrowIfCancellationRequested();

                EnsurePackageAddressesDoNotConflict(package);

                ModPackages.Add(new ModPackageEntry
                {
                    Package = package,
                    PackageHash = packageHash
                });
                return package;
            }
            catch
            {
                await DestroyAndRemovePackageAsync(package);
                throw;
            }
        }

        public static async UniTask UnloadModPackageAsync(string packageName)
        {
            ModPackageEntry entry = ModPackages.FirstOrDefault(candidate =>
                string.Equals(candidate.Package.PackageName, packageName, StringComparison.Ordinal));
            if (entry == null)
            {
                return;
            }

            EnsurePackageHasNoLoadedScene(packageName);
            ReleaseOperationsForPackage(packageName);
            ModPackages.Remove(entry);
            await DestroyAndRemovePackageAsync(entry.Package);
        }

        /// <summary>查询指定 Mod 资源包是否已进入当前资源运行时。</summary>
        public static bool IsModPackageLoaded(string packageName)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Mod 资源包名称不能为空。", nameof(packageName));
            }

            return ModPackages.Any(candidate =>
                string.Equals(candidate.Package.PackageName, packageName, StringComparison.Ordinal));
        }

        /// <summary>读取已加载 Mod 包当前生效的 YooAsset 清单版本。</summary>
        public static string GetModPackageVersion(string packageName)
        {
            EnsureInitialized();
            ModPackageEntry entry = ModPackages.FirstOrDefault(candidate =>
                string.Equals(candidate.Package.PackageName, packageName, StringComparison.Ordinal));
            if (entry == null)
            {
                throw new InvalidOperationException($"Mod 资源包尚未加载：{packageName}。");
            }
            string version = entry.Package.GetPackageVersion();
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new InvalidOperationException($"Mod 资源包 {packageName} 没有生效的 YooAsset 清单版本。");
            }
            return version;
        }

        /// <summary>读取已加载 Mod 包由 YooAsset 构建产物提供的官方哈希。</summary>
        public static string GetModPackageHash(string packageName)
        {
            EnsureInitialized();
            ModPackageEntry entry = ModPackages.FirstOrDefault(candidate =>
                string.Equals(candidate.Package.PackageName, packageName, StringComparison.Ordinal));
            if (entry == null)
            {
                throw new InvalidOperationException($"Mod 资源包尚未加载：{packageName}。");
            }
            return entry.PackageHash;
        }

        public static void EnsureAssetExists<TAsset>(object key)
        {
            EnsureLocationExists(typeof(TAsset), ConvertKeyToLocation(key));
        }

        public static void EnsureAssetExists<TAsset>(IEnumerable keys, MergeMode mergeMode)
        {
            foreach (string location in ConvertKeysToLocations(keys, mergeMode))
            {
                EnsureLocationExists(typeof(TAsset), location);
            }
        }

        public static UniTask EnsureAssetExistsAsync<TAsset>(object key)
        {
            EnsureAssetExists<TAsset>(key);
            return UniTask.CompletedTask;
        }

        public static UniTask EnsureAssetExistsAsync<TAsset>(IEnumerable keys, MergeMode mergeMode)
        {
            EnsureAssetExists<TAsset>(keys, mergeMode);
            return UniTask.CompletedTask;
        }

        public static ResourceHandle<T> LoadAssetAsync<T>(string address, Action<T> callback = null)
            where T : UObject
        {
            ResourcePackage package = ResolvePackage(address, typeof(T));
            var state = Register(new AssetResourceOperationState<T>(package, address));
            var resourceHandle = new ResourceHandle<T>(state);
            if (callback != null)
            {
                resourceHandle.RegisterCallback(callback);
            }

            return resourceHandle;
        }

        public static ResourceHandle<T> LoadAssetAsync<T>(
            string packageName,
            string address,
            Action<T> callback = null)
            where T : UObject
        {
            ResourcePackage package = GetPackage(packageName);
            EnsureLocationExists(package, typeof(T), address);
            var state = Register(new AssetResourceOperationState<T>(package, address));
            var resourceHandle = new ResourceHandle<T>(state);
            if (callback != null)
            {
                resourceHandle.RegisterCallback(callback);
            }

            return resourceHandle;
        }

        public static ResourceHandle<T> InstantiateAsync<T>(
            string address,
            Transform parent = null,
            Action<T> callback = null)
            where T : UObject
        {
            ResourcePackage package = ResolvePackage(address, typeof(GameObject));
            var state = Register(new InstantiateResourceOperationState(
                package,
                address,
                new InstantiateOptions(true, parent, false)));
            var resourceHandle = new ResourceHandle<T>(state);
            if (callback != null)
            {
                resourceHandle.RegisterCallback(callback);
            }

            return resourceHandle;
        }

        public static ResourceHandle<IList<T>> LoadAssetsAsync<T>(object key, Action<IList<T>> callback = null)
            where T : UObject
        {
            string location = ConvertKeyToLocation(key);
            ResourcePackage package = ResolvePackage(location, typeof(T));
            var state = Register(new AllAssetsResourceOperationState<T>(package, location));
            var resourceHandle = new ResourceHandle<IList<T>>(state);
            if (callback != null)
            {
                resourceHandle.RegisterCallback(callback);
            }

            return resourceHandle;
        }

        public static ResourceHandle<IList<T>> LoadAssetsAsync<T>(
            IEnumerable keys,
            MergeMode mode,
            Action<IList<T>> callback = null)
            where T : UObject
        {
            string[] locations = ConvertKeysToLocations(keys, mode);
            var children = locations
                .Select(location => new AllAssetsResourceOperationState<T>(
                    ResolvePackage(location, typeof(T)),
                    location))
                .Cast<ResourceOperationState>()
                .ToArray();
            var state = Register(new CompositeResourceOperationState<T>(children));
            var resourceHandle = new ResourceHandle<IList<T>>(state);
            if (callback != null)
            {
                resourceHandle.RegisterCallback(callback);
            }

            return resourceHandle;
        }

        /// <summary>
        /// 从默认包和所有已启用 Mod 包中加载带有指定 YooAsset 资源标签的资产。
        /// 这里的资源标签只用于构建清单发现，不是 EX-GAS GameplayTag，也不是玩法内容 ID。
        /// </summary>
        public static ResourceHandle<IList<T>> LoadAssetsByAssetTagAsync<T>(
            string assetTag,
            Action<IList<T>> callback = null)
            where T : UObject
        {
            if (string.IsNullOrWhiteSpace(assetTag))
            {
                throw new InvalidResourceRequestException(assetTag, "YooAsset 资源标签不能为空。");
            }

            ResourcePackage[] packages = GetLoadedPackagesInContentOrder();
            var state = Register(new TaggedAssetsResourceOperationState<T>(packages, assetTag));
            var resourceHandle = new ResourceHandle<IList<T>>(state);
            if (callback != null)
            {
                resourceHandle.RegisterCallback(callback);
            }

            return resourceHandle;
        }

        public static void Release(ResourceHandle handle)
        {
            handle.State?.Release();
        }

        public static void Release<T>(ResourceHandle<T> handle)
        {
            handle.State?.Release();
        }

        public static void ReleaseAsset(ResourceHandle handle)
        {
            handle.State?.Release();
        }

        public static void ReleaseAsset<T>(ResourceHandle<T> handle)
        {
            handle.State?.Release();
        }

        public static void ReleaseInstance(ResourceHandle handle)
        {
            handle.State?.Release();
        }

        public static bool IsValid(this ResourceHandle handle)
        {
            return handle.State?.IsValid == true;
        }

        public static bool IsValid<T>(this ResourceHandle<T> handle)
        {
            return handle.State?.IsValid == true;
        }

        public static bool IsDone(this ResourceHandle handle)
        {
            return handle.State?.IsDone == true;
        }

        public static bool IsDone<T>(this ResourceHandle<T> handle)
        {
            return handle.State?.IsDone == true;
        }

        public static async UniTask<T> ToUniTask<T>(this ResourceHandle<T> handle)
        {
            if (handle.State == null)
            {
                throw new InvalidOperationException("资源句柄为空。");
            }

            object result = await handle.State.AwaitResultAsync();
            return result == null ? default : (T)result;
        }

        public static async UniTask ToUniTask(this ResourceHandle handle)
        {
            if (handle.State == null)
            {
                throw new InvalidOperationException("资源句柄为空。");
            }

            await handle.State.AwaitResultAsync();
        }

        internal static void NotifyReleased(ResourceOperationState state)
        {
            ActiveOperations.Remove(state);
        }

        private static TState Register<TState>(TState state) where TState : ResourceOperationState
        {
            ActiveOperations.Add(state);
            return state;
        }

        private static ResourcePackage ResolvePackage(string address, Type assetType)
        {
            EnsureInitialized();
            ResourcePackage resolved = null;
            foreach (ModPackageEntry entry in ModPackages)
            {
                AssetInfo modAsset = entry.Package.GetAssetInfo(address, assetType);
                if (modAsset.IsValid)
                {
                    if (resolved != null)
                    {
                        throw new InvalidResourceRequestException(
                            address,
                            $"资源定位 {address} 同时命中 Mod 包 {resolved.PackageName} 和 {entry.Package.PackageName}。"
                            + "当前内容包协议不允许静默覆盖。");
                    }
                    resolved = entry.Package;
                }
            }

            AssetInfo defaultAsset = DefaultPackage.GetAssetInfo(address, assetType);
            if (defaultAsset.IsValid)
            {
                if (resolved != null)
                {
                    throw new InvalidResourceRequestException(
                        address,
                        $"资源定位 {address} 同时命中默认包 {DefaultPackage.PackageName} 和 Mod 包 {resolved.PackageName}。"
                        + "当前内容包协议不允许静默覆盖。");
                }
                return DefaultPackage;
            }
            if (resolved != null)
            {
                return resolved;
            }
            throw new InvalidResourceRequestException(address, $"所有已加载资源包都不存在资源定位：{address}。");
        }

        internal static ResourcePackage ResolveScenePackage(string address)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidResourceRequestException(address, "场景地址不能为空。");
            }

            ResourcePackage resolved = null;
            foreach (ModPackageEntry entry in ModPackages)
            {
                AssetInfo modAsset = entry.Package.GetAssetInfo(address);
                if (IsSceneAsset(modAsset))
                {
                    if (resolved != null)
                    {
                        throw new InvalidResourceRequestException(
                            address,
                            $"场景定位 {address} 同时命中 Mod 包 {resolved.PackageName} 和 {entry.Package.PackageName}。"
                            + "当前内容包协议不允许静默覆盖。");
                    }
                    resolved = entry.Package;
                }
            }

            AssetInfo defaultAsset = DefaultPackage.GetAssetInfo(address);
            if (IsSceneAsset(defaultAsset))
            {
                if (resolved != null)
                {
                    throw new InvalidResourceRequestException(
                        address,
                        $"场景定位 {address} 同时命中默认包 {DefaultPackage.PackageName} 和 Mod 包 {resolved.PackageName}。"
                        + "当前内容包协议不允许静默覆盖。");
                }
                return DefaultPackage;
            }
            if (resolved == null)
            {
                throw new InvalidResourceRequestException(
                    address,
                    $"默认资源包中不存在场景地址：{address}。{defaultAsset.Error}");
            }
            return resolved;
        }

        private static ResourcePackage GetPackage(string packageName)
        {
            EnsureInitialized();
            if (!YooAssets.TryGetPackage(packageName, out ResourcePackage package))
            {
                throw new InvalidOperationException($"资源包尚未初始化：{packageName}");
            }

            return package;
        }

        private static ResourcePackage[] GetLoadedPackagesInContentOrder()
        {
            EnsureInitialized();
            var packages = new ResourcePackage[ModPackages.Count + 1];
            packages[0] = DefaultPackage;
            for (int i = 0; i < ModPackages.Count; i++)
            {
                packages[i + 1] = ModPackages[i].Package;
            }

            return packages;
        }

        private static void EnsurePackageAddressesDoNotConflict(ResourcePackage candidate)
        {
            ResourcePackage[] loadedPackages = GetLoadedPackagesInContentOrder();
            AssetInfo[] candidateAssets = candidate.GetAllAssetInfos();
            for (int assetIndex = 0; assetIndex < candidateAssets.Length; assetIndex++)
            {
                AssetInfo candidateAsset = candidateAssets[assetIndex];
                EnsureLocationDoesNotConflict(candidate, loadedPackages, candidateAsset.Address);
                if (!string.Equals(candidateAsset.AssetPath, candidateAsset.Address, StringComparison.Ordinal))
                {
                    EnsureLocationDoesNotConflict(candidate, loadedPackages, candidateAsset.AssetPath);
                }
            }
        }

        private static void EnsureLocationDoesNotConflict(
            ResourcePackage candidate,
            IReadOnlyList<ResourcePackage> loadedPackages,
            string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return;
            }
            for (int packageIndex = 0; packageIndex < loadedPackages.Count; packageIndex++)
            {
                ResourcePackage loaded = loadedPackages[packageIndex];
                AssetInfo existing = loaded.GetAssetInfo(location);
                if (existing.IsValid)
                {
                    throw new InvalidOperationException(
                        $"资源定位 {location} 同时存在于资源包 {loaded.PackageName} 和 {candidate.PackageName}。"
                        + "当前内容包协议不允许静默覆盖。");
                }
            }
        }

        private static void EnsureLocationExists(Type assetType, string location)
        {
            ResolvePackage(location, assetType);
        }

        private static void EnsureLocationExists(ResourcePackage package, Type assetType, string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new InvalidResourceRequestException(location, "资源地址不能为空。");
            }

            AssetInfo assetInfo = package.GetAssetInfo(location, assetType);
            if (!assetInfo.IsValid)
            {
                throw new InvalidResourceRequestException(
                    location,
                    $"资源包 {package.PackageName} 中不存在类型为 {assetType.Name} 的地址：{location}。{assetInfo.Error}");
            }
        }

        private static string ConvertKeyToLocation(object key)
        {
            if (key is string location && !string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            throw new InvalidResourceRequestException(
                key?.ToString(),
                "YooAsset 资源入口只接受明确的字符串地址；Addressables 标签键已经退出正式链路。");
        }

        private static string[] ConvertKeysToLocations(IEnumerable keys, MergeMode mode)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            string[] locations = keys.Cast<object>().Select(ConvertKeyToLocation).Distinct().ToArray();
            if (locations.Length == 0)
            {
                throw new InvalidResourceRequestException(string.Empty, "资源地址集合不能为空。");
            }

            return mode switch
            {
                MergeMode.UseFirst => new[] { locations[0] },
                MergeMode.Union => locations,
                MergeMode.Intersection => throw new NotSupportedException(
                    "YooAsset 直接地址集合不支持 Addressables 的标签交集语义，请在内容清单中生成明确地址。"),
                _ => locations
            };
        }

        private static void EnsureInitialized()
        {
            if (!Initialized)
            {
                throw new InvalidOperationException("资源系统尚未初始化，请先等待 GameManager 完成 YooAsset 启动。");
            }
        }

        private static void EnsureReadyForInitialization()
        {
            if (s_ownsResourceRuntime)
            {
                throw new InvalidOperationException(
                    "资源系统仍持有未关闭的资源运行时，不能重复初始化。"
                    + "请先通过正式关闭入口释放当前资源系统。");
            }

            if (YooInit.Initialized)
            {
                throw new InvalidOperationException(
                    "YokiFrame.YooInit 已由其它入口初始化。资源系统不能接管外部资源状态；"
                    + "请只保留 GameManager 的正式资源启动入口。");
            }

            if (YooAssets.IsInitialized)
            {
                throw new InvalidOperationException(
                    "YooAsset 已被其它入口初始化，但 YokiFrame.YooInit 尚未初始化。"
                    + "请只保留 GameManager 的正式资源启动入口。");
            }

            if (HasYokiFramePartialInitializationState())
            {
                throw new InvalidOperationException(
                    "YokiFrame 保留了未完成的资源包状态，不能继续初始化资源系统。"
                    + "请重启 Unity Editor，或在获得插件源码修改授权后修复该插件生命周期。");
            }

            if (HasLocalResourceState())
            {
                throw new InvalidOperationException(
                    "资源系统保留了未关闭的项目状态，不能重新初始化。"
                    + "请先通过正式关闭入口释放当前资源系统。");
            }
        }

        private static void ValidateInitializationConfig(YooInitConfig config)
        {
            if (!config.Validate(out List<string> validationErrors))
            {
                throw new ArgumentException(
                    $"YooAsset 初始化配置无效：{string.Join("；", validationErrors)}",
                    nameof(config));
            }

            switch (config.PlayMode)
            {
                case EPlayMode.HostPlayMode when YooInit.HostModeHandler == null:
                    throw new InvalidOperationException(
                        "YooAsset HostPlayMode 缺少 YokiFrame.YooInit.HostModeHandler。"
                        + "必须在 GameManager 启动前由项目资源配置提供该处理器。");

                case EPlayMode.WebPlayMode when YooInit.WebModeHandler == null:
                    throw new InvalidOperationException(
                        "YooAsset WebPlayMode 缺少 YokiFrame.YooInit.WebModeHandler。"
                        + "必须在 GameManager 启动前由项目资源配置提供该处理器。");

                case EPlayMode.CustomPlayMode when YooInit.CustomHandler == null:
                    throw new InvalidOperationException(
                        "YooAsset CustomPlayMode 缺少 YokiFrame.YooInit.CustomHandler。"
                        + "必须在 GameManager 启动前由项目资源配置提供该处理器。");
            }
        }

        private static bool HasYokiFramePartialInitializationState()
        {
            return YooInit.DefaultPackage != null || YooInit.Packages.Count > 0;
        }

        private static bool HasLocalResourceState()
        {
            return DefaultPackage != null || s_sceneLoaderPool != null ||
                   ActiveOperations.Count > 0 || ModPackages.Count > 0;
        }

        private static void EnsureSucceeded(AsyncOperationBase operation, string action)
        {
            if (operation.Status != EOperationStatus.Succeeded)
            {
                throw new InvalidOperationException($"{action}失败：{operation.Error}");
            }
        }

        private static void ReleaseOperationsForPackage(string packageName)
        {
            foreach (ResourceOperationState operation in ActiveOperations
                         .Where(operation => operation.UsesPackage(packageName))
                         .ToArray())
            {
                operation.Release();
            }
        }

        private static async UniTask DestroyAndRemovePackageAsync(ResourcePackage package)
        {
            EnsurePackageHasNoLoadedScene(package.PackageName);
            ReleaseOperationsForPackage(package.PackageName);
            DestroyPackageOperation destroy = package.DestroyPackageAsync();
            await destroy;
            EnsureSucceeded(destroy, $"销毁资源包 {package.PackageName}");
            YooAssets.RemovePackage(package.PackageName);
        }

        private static void ResetState()
        {
            ActiveOperations.Clear();
            ModPackages.Clear();
            s_sceneLoaderPool = null;
            DefaultPackage = null;
            s_ownsResourceRuntime = false;
            s_initializationCancellation = null;
        }

        private static bool IsSceneAsset(AssetInfo assetInfo)
        {
            return assetInfo.IsValid &&
                   assetInfo.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsurePackageHasNoLoadedScene(string packageName)
        {
            if (s_sceneLoaderPool?.UsesPackage(packageName) == true)
            {
                throw new InvalidOperationException(
                    $"资源包 {packageName} 仍有 SceneKit 场景正在使用。请先通过 SceneSystem 切离该场景，再卸载 Mod 包。");
            }
        }

        private sealed class AssetResourceOperationState<T> : ResourceOperationState where T : UObject
        {
            private readonly ResourcePackage m_package;
            private readonly AssetHandle m_handle;

            public AssetResourceOperationState(ResourcePackage package, string address)
                : base(address, AssetLoadOperationType)
            {
                m_package = package;
                m_handle = package.LoadAssetAsync<T>(address);
            }

            public override string PackageName => m_package.PackageName;
            public override bool UsesPackage(string packageName) =>
                string.Equals(m_package.PackageName, packageName, StringComparison.Ordinal);
            protected override bool IsOperationValid => m_handle.IsValid;
            protected override bool IsOperationDone => m_handle.IsDone;
            protected override object GetResult() => m_handle.GetAssetObject<T>();

            protected override void WaitForCompletionCore()
            {
                m_handle.WaitForAsyncComplete();
                EnsureHandleSucceeded(m_handle, Address);
            }

            protected override async UniTask<object> AwaitResultCore()
            {
                await m_handle;
                EnsureHandleSucceeded(m_handle, Address);
                return m_handle.GetAssetObject<T>();
            }

            protected override void RegisterCallbackCore(Action<object> callback)
            {
                m_handle.Completed += handle =>
                {
                    if (IsReleased)
                    {
                        return;
                    }
                    EnsureHandleSucceeded(handle, Address);
                    callback(handle.GetAssetObject<T>());
                };
            }

            protected override void ReleaseCore()
            {
                m_handle.Release();
            }
        }

        private sealed class InstantiateResourceOperationState : ResourceOperationState
        {
            private readonly ResourcePackage m_package;
            private readonly AssetHandle m_assetHandle;
            private readonly InstantiateOperation m_operation;

            public InstantiateResourceOperationState(
                ResourcePackage package,
                string address,
                InstantiateOptions options)
                : base(address, InstantiateOperationType)
            {
                m_package = package;
                m_assetHandle = package.LoadAssetAsync<GameObject>(address);
                m_operation = m_assetHandle.InstantiateAsync(options);
            }

            public override string PackageName => m_package.PackageName;
            public override bool UsesPackage(string packageName) =>
                string.Equals(m_package.PackageName, packageName, StringComparison.Ordinal);
            protected override bool IsOperationValid => m_assetHandle.IsValid;
            protected override bool IsOperationDone => m_operation.IsDone;
            protected override object GetResult() => m_operation.Result;

            protected override void WaitForCompletionCore()
            {
                m_operation.WaitForCompletion();
                EnsureSucceeded(m_operation, $"实例化资源 {Address}");
            }

            protected override async UniTask<object> AwaitResultCore()
            {
                await m_operation;
                EnsureSucceeded(m_operation, $"实例化资源 {Address}");
                return m_operation.Result;
            }

            protected override void RegisterCallbackCore(Action<object> callback)
            {
                m_operation.Completed += operation =>
                {
                    if (IsReleased)
                    {
                        return;
                    }
                    EnsureSucceeded(operation, $"实例化资源 {Address}");
                    callback(m_operation.Result);
                };
            }

            protected override void ReleaseCore()
            {
                if (!m_operation.IsDone)
                {
                    m_operation.Cancel();
                }

                if (m_operation.Result != null)
                {
                    UObject.Destroy(m_operation.Result);
                }

                m_assetHandle.Release();
            }
        }

        private sealed class AllAssetsResourceOperationState<T> : ResourceOperationState where T : UObject
        {
            private readonly ResourcePackage m_package;
            private readonly AllAssetsHandle m_handle;
            private IList<T> m_results;

            public AllAssetsResourceOperationState(ResourcePackage package, string address)
                : base(address, AssetLoadOperationType)
            {
                m_package = package;
                m_handle = package.LoadAllAssetsAsync<T>(address);
            }

            public override string PackageName => m_package.PackageName;
            public override bool UsesPackage(string packageName) =>
                string.Equals(m_package.PackageName, packageName, StringComparison.Ordinal);
            protected override bool IsOperationValid => m_handle.IsValid;
            protected override bool IsOperationDone => m_handle.IsDone;
            protected override object GetResult() => BuildResults();

            protected override void WaitForCompletionCore()
            {
                m_handle.WaitForAsyncComplete();
                EnsureHandleSucceeded(m_handle, Address);
                BuildResults();
            }

            protected override async UniTask<object> AwaitResultCore()
            {
                await m_handle;
                EnsureHandleSucceeded(m_handle, Address);
                return BuildResults();
            }

            protected override void RegisterCallbackCore(Action<object> callback)
            {
                m_handle.Completed += handle =>
                {
                    if (IsReleased)
                    {
                        return;
                    }
                    EnsureHandleSucceeded(handle, Address);
                    callback(BuildResults());
                };
            }

            protected override void ReleaseCore()
            {
                m_results = null;
                m_handle.Release();
            }

            private IList<T> BuildResults()
            {
                return m_results ??= m_handle.AllAssetObjects.OfType<T>().ToList();
            }
        }

        private sealed class CompositeResourceOperationState<T> : ResourceOperationState where T : UObject
        {
            private readonly ResourceOperationState[] m_children;
            private IList<T> m_results;

            public CompositeResourceOperationState(ResourceOperationState[] children)
                : base(string.Join(",", children.Select(child => child.Address)), AssetLoadOperationType)
            {
                m_children = children;
            }

            public override string PackageName => string.Join(",", m_children.Select(child => child.PackageName).Distinct());
            public override bool UsesPackage(string packageName) =>
                m_children.Any(child => child.UsesPackage(packageName));
            protected override bool IsOperationValid => m_children.All(child => child.IsValid);
            protected override bool IsOperationDone => m_children.All(child => child.IsDone);
            protected override object GetResult() => BuildResults();

            protected override void WaitForCompletionCore()
            {
                foreach (ResourceOperationState child in m_children)
                {
                    child.WaitForCompletion();
                }

                BuildResults();
            }

            protected override async UniTask<object> AwaitResultCore()
            {
                foreach (ResourceOperationState child in m_children)
                {
                    await child.AwaitResultAsync();
                }

                return BuildResults();
            }

            protected override void RegisterCallbackCore(Action<object> callback)
            {
                AwaitAndInvoke(callback).Forget();
            }

            protected override void ReleaseCore()
            {
                foreach (ResourceOperationState child in m_children)
                {
                    child.Release();
                }

                m_results = null;
            }

            private async UniTaskVoid AwaitAndInvoke(Action<object> callback)
            {
                callback(await AwaitResultCore());
            }

            private IList<T> BuildResults()
            {
                return m_results ??= m_children
                    .SelectMany(child => child.Result is IEnumerable<T> assets ? assets : Array.Empty<T>())
                    .Distinct()
                    .ToList();
            }
        }

        private sealed class TaggedAssetsResourceOperationState<T> : ResourceOperationState where T : UObject
        {
            private readonly string m_assetTag;
            private readonly string[] m_packageNames;
            private readonly string[] m_assetPaths;
            private readonly AssetHandle[] m_handles;
            private IList<T> m_results;

            public TaggedAssetsResourceOperationState(ResourcePackage[] packages, string assetTag)
                : base($"YooAssetTag:{assetTag}", AssetLoadOperationType)
            {
                m_assetTag = assetTag;
                var packageNames = new List<string>(packages.Length);
                var assetPaths = new List<string>();
                var handles = new List<AssetHandle>();

                for (int packageIndex = 0; packageIndex < packages.Length; packageIndex++)
                {
                    ResourcePackage package = packages[packageIndex];
                    packageNames.Add(package.PackageName);

                    AssetInfo[] assetInfos = package.GetAssetInfos(assetTag);
                    Array.Sort(
                        assetInfos,
                        (left, right) => string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal));

                    for (int assetIndex = 0; assetIndex < assetInfos.Length; assetIndex++)
                    {
                        AssetInfo assetInfo = assetInfos[assetIndex];
                        assetPaths.Add($"{package.PackageName}:{assetInfo.AssetPath}");
                        handles.Add(package.LoadAssetAsync(assetInfo));
                    }
                }

                m_packageNames = packageNames.ToArray();
                m_assetPaths = assetPaths.ToArray();
                m_handles = handles.ToArray();
            }

            public override string PackageName => string.Join(",", m_packageNames);

            public override bool UsesPackage(string packageName)
            {
                for (int i = 0; i < m_packageNames.Length; i++)
                {
                    if (string.Equals(m_packageNames[i], packageName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            protected override bool IsOperationValid
            {
                get
                {
                    for (int i = 0; i < m_handles.Length; i++)
                    {
                        if (!m_handles[i].IsValid)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            protected override bool IsOperationDone
            {
                get
                {
                    for (int i = 0; i < m_handles.Length; i++)
                    {
                        if (!m_handles[i].IsDone)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            protected override object GetResult()
            {
                return IsOperationDone ? BuildResults() : null;
            }

            protected override void WaitForCompletionCore()
            {
                for (int i = 0; i < m_handles.Length; i++)
                {
                    m_handles[i].WaitForAsyncComplete();
                    EnsureHandleSucceeded(m_handles[i], m_assetPaths[i]);
                }

                BuildResults();
            }

            protected override async UniTask<object> AwaitResultCore()
            {
                for (int i = 0; i < m_handles.Length; i++)
                {
                    await m_handles[i];
                    EnsureHandleSucceeded(m_handles[i], m_assetPaths[i]);
                }

                return BuildResults();
            }

            protected override void RegisterCallbackCore(Action<object> callback)
            {
                AwaitAndInvoke(callback).Forget();
            }

            protected override void ReleaseCore()
            {
                m_results = null;
                for (int i = 0; i < m_handles.Length; i++)
                {
                    m_handles[i].Release();
                }
            }

            private async UniTaskVoid AwaitAndInvoke(Action<object> callback)
            {
                try
                {
                    callback(await AwaitResultCore());
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            private IList<T> BuildResults()
            {
                if (m_results != null)
                {
                    return m_results;
                }

                var results = new List<T>(m_handles.Length);
                for (int i = 0; i < m_handles.Length; i++)
                {
                    UObject assetObject = m_handles[i].AssetObject;
                    if (assetObject is not T typedAsset)
                    {
                        string actualType = assetObject == null ? "<null>" : assetObject.GetType().FullName;
                        throw new InvalidResourceRequestException(
                            m_assetPaths[i],
                            $"YooAsset 资源标签 {m_assetTag} 包含了不能转换为 {typeof(T).FullName} 的资产：{m_assetPaths[i]}，实际类型：{actualType}。");
                    }

                    results.Add(typedAsset);
                }

                m_results = results;
                return m_results;
            }
        }

        private static void EnsureHandleSucceeded(HandleBase handle, string address)
        {
            if (handle.Status != EOperationStatus.Succeeded)
            {
                throw new InvalidResourceRequestException(address, $"YooAsset 加载失败：{handle.Error}");
            }
        }
    }

    /// <summary>
    /// 资源地址无效或加载失败时抛出的异常，保留原始地址便于定位内容配置。
    /// </summary>
    public class InvalidResourceRequestException : Exception
    {
        public string InvalidAddress { get; }

        public InvalidResourceRequestException(string address, string message) : base(message)
        {
            InvalidAddress = address;
        }
    }
}
