using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using YokiFrame;
using YooAsset;

namespace GameCore
{
    /// <summary>
    /// ResKit 的项目场景加载后端。SceneKit 继续拥有场景生命周期，
    /// 本类型只在实际加载时选择默认包或 Mod 包，并复用 YokiFrame 的 YooAsset 加载器。
    /// </summary>
    internal sealed class ResourceSystemSceneLoaderPool : ISceneResLoaderPool
    {
        private readonly Stack<ResourceSystemSceneLoader> m_recycledLoaders = new(4);
        private readonly HashSet<ResourceSystemSceneLoader> m_activeLoaders = new();

        public ISceneResLoader Allocate()
        {
            return m_recycledLoaders.Count > 0
                ? m_recycledLoaders.Pop()
                : new ResourceSystemSceneLoader(this);
        }

        public void Recycle(ISceneResLoader loader)
        {
            if (loader is not ResourceSystemSceneLoader projectLoader)
            {
                throw new InvalidOperationException(
                    $"{nameof(ResourceSystemSceneLoaderPool)} 不能回收 {loader?.GetType().FullName ?? "<null>"}。");
            }

            m_activeLoaders.Remove(projectLoader);
            m_recycledLoaders.Push(projectLoader);
        }

        internal void MarkInactive(ResourceSystemSceneLoader loader)
        {
            m_activeLoaders.Remove(loader);
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

    internal sealed class ResourceSystemSceneLoader : ISceneResLoader
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
            string scenePath,
            bool isAdditive,
            bool suspendLoad,
            Action<Scene> onComplete,
            Action<float> onProgress = null,
            Action onSuspended = null)
        {
            if (m_packageLoader != null)
            {
                throw new InvalidOperationException("场景加载器尚未回收，不能重复加载场景。");
            }

            m_packageLoader = m_owner.AllocateForAddress(this, scenePath, out string packageName);
            PackageName = packageName;
            try
            {
                m_packageLoader.LoadAsync(
                    scenePath,
                    isAdditive,
                    suspendLoad,
                    onComplete,
                    onProgress,
                    onSuspended);
            }
            catch
            {
                ReleasePackageLoader();
                throw;
            }
        }

        public void UnloadAsync(Scene scene, Action onComplete)
        {
            if (m_packageLoader == null)
            {
                onComplete?.Invoke();
                return;
            }

            m_packageLoader.UnloadAsync(scene, onComplete);
        }

        public void SuspendLoad()
        {
            m_packageLoader?.SuspendLoad();
        }

        public void ResumeLoad()
        {
            m_packageLoader?.ResumeLoad();
        }

        public void UnloadAndRecycle()
        {
            ReleasePackageLoader();
            m_owner.Recycle(this);
        }

        private void ReleasePackageLoader()
        {
            m_packageLoader?.UnloadAndRecycle();
            m_packageLoader = null;
            PackageName = string.Empty;
            m_owner.MarkInactive(this);
        }
    }
}
