using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UObject = UnityEngine.Object;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 按地址缓存一组 YooAsset 资源。
    /// 它服务资源加载生命周期，不承担 GameCore 数据库条目的稳定 ID 真相。
    /// </summary>
    public class ResourceCache<TAsset> : IDisposable, IReadOnlyDictionary<string, TAsset>
        where TAsset : UObject
    {
        private readonly Dictionary<string, ResourceHandle<TAsset>> m_internalHandles = new();
        private readonly Dictionary<string, TAsset> m_cacheMap = new();
        private readonly Dictionary<string, int> m_versionMap = new();
        private int m_loadingRef;

        public bool AddressSafeCheck { get; set; }
        public int Version { get; private set; }
        public bool IsLoading => m_loadingRef > 0;
        public IEnumerable<string> Keys => m_cacheMap.Keys.ToArray();
        public IEnumerable<TAsset> Values => m_cacheMap.Values.ToArray();
        public int Count => m_cacheMap.Count;
        public TAsset this[string key] => m_cacheMap[key];

        public async UniTask<TAsset> LoadAssetAsync(string address)
        {
            m_versionMap[address] = Version;
            if (!m_cacheMap.TryGetValue(address, out TAsset asset))
            {
                m_loadingRef++;
                try
                {
                    if (AddressSafeCheck)
                    {
                        await ResourceSystem.EnsureAssetExistsAsync<TAsset>(address);
                    }

                    asset = await LoadNewAssetAsync(address).ToUniTask();
                }
                finally
                {
                    m_loadingRef--;
                }
            }

            return asset;
        }

        public TAsset LoadAsset(string address)
        {
            m_versionMap[address] = Version;
            if (!m_cacheMap.TryGetValue(address, out TAsset asset))
            {
                if (AddressSafeCheck)
                {
                    ResourceSystem.EnsureAssetExists<TAsset>(address);
                }

                asset = LoadNewAssetAsync(address).WaitForCompletion();
            }

            return asset;
        }

        private ResourceHandle<TAsset> LoadNewAssetAsync(string address, Action<TAsset> callback = null)
        {
            if (m_internalHandles.TryGetValue(address, out ResourceHandle<TAsset> internalHandle))
            {
                if (internalHandle.IsDone())
                {
                    callback?.Invoke(internalHandle.Result);
                }
                else if (callback != null)
                {
                    internalHandle.RegisterCallback(callback);
                }

                return internalHandle;
            }

            internalHandle = ResourceSystem.LoadAssetAsync<TAsset>(address, asset =>
            {
                m_cacheMap[address] = asset;
                callback?.Invoke(asset);
            });
            m_internalHandles.Add(address, internalHandle);
            return internalHandle;
        }

        public virtual void Dispose()
        {
            foreach (ResourceHandle<TAsset> handle in m_internalHandles.Values)
            {
                ResourceSystem.ReleaseAsset(handle);
            }

            m_internalHandles.Clear();
            m_cacheMap.Clear();
            m_versionMap.Clear();
        }

        public string[] GetCacheKeys()
        {
            return m_cacheMap.Keys.ToArray();
        }

        public int UpdateVersion()
        {
            return ++Version;
        }

        public void ReleaseAssetsWithVersion(int version)
        {
            foreach (string address in m_versionMap
                         .Where(pair => pair.Value == version)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                if (m_internalHandles.TryGetValue(address, out ResourceHandle<TAsset> handle))
                {
                    ResourceSystem.ReleaseAsset(handle);
                }

                m_cacheMap.Remove(address);
                m_internalHandles.Remove(address);
                m_versionMap.Remove(address);
            }
        }

        public void ReleaseAssetsAndUpdateVersion()
        {
            ReleaseAssetsWithVersion(Version);
            UpdateVersion();
        }

        public bool ContainsKey(string key)
        {
            return m_cacheMap.ContainsKey(key);
        }

        public bool TryGetValue(string key, out TAsset value)
        {
            return m_cacheMap.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<string, TAsset>> GetEnumerator()
        {
            return m_cacheMap.ToArray().AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
