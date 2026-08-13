using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace GameCore
{
#if UNITY_EDITOR
    /// <summary>
    /// 数据库引用的编辑器序列化扩展，负责在非 PlayMode 下把资产引用同步成 GUID。
    /// </summary>
    public partial class DatabaseEntryReference<T> : ISerializationCallbackReceiver where T : DatabaseEntry
    {
        public void OnBeforeSerialize()
        {
            // 编辑器非 PlayMode 下由对象引用刷新 GUID；运行时 GUID 应由 DatabaseRegistry 构造引用时写入。
            if (!Application.isPlaying)
            {
                m_guid = m_instance ? m_instance.GetAssetGUID() : string.Empty;
            }
        }

        public void OnAfterDeserialize() { }
    }
#endif

    /// <summary>
    /// 数据库条目的轻量引用，运行时只依赖 GUID，编辑器下保留对象引用方便资产配置。
    /// </summary>
    [Serializable]
    public partial class DatabaseEntryReference<T> where T : DatabaseEntry
    {
        /// <summary>
        /// 目标数据库条目的 GUID。
        /// </summary>
        public string guid => m_guid;

        [LabelText("数据库资产")]
        [Tooltip("仅编辑器配置使用；序列化时会同步为 GUID，运行时不依赖该对象引用。")]
        [SerializeField] private T m_instance;

        [LabelText("GUID")]
        [Tooltip("数据库资产的稳定 GUID，运行时用它从 DatabaseRegistry 找回目标资产。")]
        [SerializeField] private string m_guid = string.Empty;

        public DatabaseEntryReference(string guid)
        {
            m_guid = guid;
        }
    }
}

