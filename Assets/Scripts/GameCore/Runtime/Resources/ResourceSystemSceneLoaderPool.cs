using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;
using YokiFrame;
using YooAsset;

namespace GameCore
{
    /// <summary>
    /// SceneKit 的项目场景加载后端。
    /// 它直接选择默认包或 Mod 包并持有 YooAsset 场景句柄，避免在 SceneKit 与 YooAsset 之间再叠加 ResKit 场景加载器。
    /// </summary>
    internal sealed class ResourceSystemSceneLoaderPool : ISceneLoaderPool
    {
        private readonly Stack<ResourceSystemSceneLoader> m_recycledLoaders = new(4);
        private readonly HashSet<ResourceSystemSceneLoader> m_activeLoaders = new();

        public ISceneLoader Allocate()
        {
            return m_recycledLoaders.Count > 0
                ? m_recycledLoaders.Pop()
                : new ResourceSystemSceneLoader(this);
        }

        public void Recycle(ISceneLoader loader)
        {
            if (loader is not ResourceSystemSceneLoader projectLoader)
            {
                throw new InvalidOperationException(
                    $"{nameof(ResourceSystemSceneLoaderPool)} 不能回收 {loader?.GetType().FullName ?? "<null>"}。");
            }

            projectLoader.ReleasePackageLoader();
            m_activeLoaders.Remove(projectLoader);
            m_recycledLoaders.Push(projectLoader);
        }

        internal ISceneResLoader AllocateForAddress(
            ResourceSystemSceneLoader owner,
            string sceneAddress,
            out string packageName)
        {
            ResourcePackage package = ResourceSystem.ResolveScenePackage(sceneAddress);
            packageName = package.PackageName;
            ISceneResLoader loader = new YooAssetSceneLoaderUniTaskPool(package).Allocate();
            m_activeLoaders.Add(owner);
            return loader;
        }

        internal void MarkInactive(ResourceSystemSceneLoader loader)
        {
            m_activeLoaders.Remove(loader);
        }

        internal bool UsesPackage(string packageName)
        {
            foreach (ResourceSystemSceneLoader loader in m_activeLoaders)
            {
                if (string.Equals(loader.PackageName, packageName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// SceneKit 分配的单个场景加载器。
    /// 加载、显式卸载和回收都由同一个实例释放 YooAsset 句柄，资源包占用不会滞留在独立的下层加载器中。
    /// </summary>
    internal sealed class ResourceSystemSceneLoader : ISceneLoader
    {
        private readonly ResourceSystemSceneLoaderPool m_owner;
        private ISceneResLoader m_packageLoader;

        internal ResourceSystemSceneLoader(ResourceSystemSceneLoaderPool owner)
        {
            m_owner = owner;
        }

        internal string PackageName { get; private set; } = string.Empty;

        public bool IsSuspended => m_packageLoader?.IsSuspended ?? false;
        public float Progress => m_packageLoader?.Progress ?? 0f;

        public void LoadAsync(
            string sceneAddress,
            SceneLoadMode mode,
            Action<Scene> onComplete,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f,
            Action onSuspended = null)
        {
            if (m_packageLoader != null)
            {
                throw new InvalidOperationException("场景加载器尚未回收，不能重复加载场景。");
            }

            m_packageLoader = m_owner.AllocateForAddress(this, sceneAddress, out string packageName);
            PackageName = packageName;
            try
            {
                m_packageLoader.LoadAsync(
                    sceneAddress,
                    mode == SceneLoadMode.Additive,
                    suspendAtProgress < 1f,
                    scene =>
                    {
                        if (!scene.IsValid() || !scene.isLoaded)
                        {
                            ReleasePackageLoader();
                        }

                        onComplete?.Invoke(scene);
                    },
                    onProgress,
                    onSuspended);
            }
            catch
            {
                ReleasePackageLoader();
                throw;
            }
        }

        public void LoadAsync(
            int buildIndex,
            SceneLoadMode mode,
            Action<Scene> onComplete,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f,
            Action onSuspended = null)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new InvalidOperationException($"找不到 Build Index {buildIndex} 对应的场景路径。");
            }

            LoadAsync(
                Path.GetFileNameWithoutExtension(scenePath),
                mode,
                onComplete,
                onProgress,
                suspendAtProgress,
                onSuspended);
        }

        public void UnloadAsync(Scene scene, Action onComplete)
        {
            if (m_packageLoader == null)
            {
                onComplete?.Invoke();
                return;
            }

            m_packageLoader.UnloadAsync(scene, () =>
            {
                ReleasePackageLoader();
                onComplete?.Invoke();
            });
        }

        public void SuspendLoad()
        {
            m_packageLoader?.SuspendLoad();
        }

        public void ResumeLoad()
        {
            m_packageLoader?.ResumeLoad();
        }

        public void Recycle()
        {
            m_owner.Recycle(this);
        }

        internal void ReleasePackageLoader()
        {
            m_packageLoader?.UnloadAndRecycle();
            m_packageLoader = null;
            PackageName = string.Empty;
            m_owner.MarkInactive(this);
        }
    }
}
