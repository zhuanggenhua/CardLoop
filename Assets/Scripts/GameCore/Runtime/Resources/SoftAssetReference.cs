using System;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace GameCore
{
    /// <summary>
    /// 软资源引用的非泛型基类，保存 YooAsset 地址和编辑器锁定信息。
    /// </summary>
    [Serializable]
    public class SoftAssetReferenceBase
    {
        [InspectorName("资源地址")]
        [Tooltip("YooAsset 资源地址。为空时该引用无效。")]
        public string Address;

#if UNITY_EDITOR
        [SerializeField] internal string Guid;
        [SerializeField] internal bool Locked = true;
#endif

        public override string ToString()
        {
            return Address;
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Address);
        }
    }

    /// <summary>
    /// 轻量资源地址引用。
    /// 它只适合引用 YooAsset 资源地址，不替代存档和玩法数据使用的 DatabaseEntryReference。
    /// </summary>
    [Serializable]
    public class SoftAssetReference<T> : SoftAssetReferenceBase, IDisposable where T : UObject
    {
        private ResourceHandle<T> m_resourceHandle;

        public SoftAssetReference(string address)
        {
            Address = address;
#if UNITY_EDITOR
            Guid = string.Empty;
            Locked = false;
#endif
        }

        public SoftAssetReference()
        {
        }

        public ResourceHandle<T> LoadAsync()
        {
            if (m_resourceHandle.IsValid())
            {
                return m_resourceHandle;
            }

            return m_resourceHandle = ResourceSystem.LoadAssetAsync<T>(Address);
        }

        public void Release()
        {
            if (!m_resourceHandle.IsValid())
            {
                return;
            }

            ResourceSystem.ReleaseAsset(m_resourceHandle);
            m_resourceHandle = default;
        }

        public void Dispose()
        {
            Release();
        }

        public static implicit operator SoftAssetReference<T>(string address)
        {
            return new SoftAssetReference<T>(address);
        }

        public static implicit operator SoftAssetReference<T>(SoftAssetReference assetReference)
        {
            return new SoftAssetReference<T>
            {
                Address = assetReference.Address,
#if UNITY_EDITOR
                Guid = assetReference.Guid,
                Locked = assetReference.Locked
#endif
            };
        }

        public static implicit operator SoftAssetReference(SoftAssetReference<T> assetReference)
        {
            return new SoftAssetReference
            {
                Address = assetReference.Address,
#if UNITY_EDITOR
                Guid = assetReference.Guid,
                Locked = assetReference.Locked
#endif
            };
        }
    }

    /// <summary>
    /// 非泛型软资源引用，按 UnityEngine.Object 加载 YooAsset 资源。
    /// </summary>
    [Serializable]
    public class SoftAssetReference : SoftAssetReferenceBase, IDisposable
    {
        private ResourceHandle m_resourceHandle;

        public SoftAssetReference(string address)
        {
            Address = address;
#if UNITY_EDITOR
            Guid = string.Empty;
            Locked = false;
#endif
        }

        public SoftAssetReference()
        {
        }

        public ResourceHandle LoadAsync()
        {
            if (m_resourceHandle.IsValid())
            {
                return m_resourceHandle;
            }

            return m_resourceHandle = ResourceSystem.LoadAssetAsync<UObject>(Address);
        }

        public void Release()
        {
            if (!m_resourceHandle.IsValid())
            {
                return;
            }

            ResourceSystem.ReleaseAsset(m_resourceHandle);
            m_resourceHandle = default;
        }

        public void Dispose()
        {
            Release();
        }

        public static implicit operator SoftAssetReference(string address)
        {
            return new SoftAssetReference(address);
        }
    }
}
