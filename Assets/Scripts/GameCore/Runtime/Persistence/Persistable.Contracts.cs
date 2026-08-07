using System;
using UnityEngine.Serialization;

namespace GameCore
{
    /// <summary>
    /// 持久化 handler 的最小合同，只回答“当前对象如何被持久化系统识别”。
    /// </summary>
    public interface APersistenceInfo
    {
        public bool IsValid();
    }

    /// <summary>
    /// 可被稳定标识符引用的持久化 handler 合同。
    /// </summary>
    public interface IIdentifiablePersistentDataHandler
    {
        public string GetIdentifier();
    }

    /// <summary>
    /// 场景中预先摆放对象的持久化信息，依赖手工配置的稳定标识符。
    /// </summary>
    [Serializable]
    public class PreInstancedPersistentDataHandler : APersistenceInfo, IIdentifiablePersistentDataHandler
    {
        /// <summary>
        /// 场景内预置对象的稳定标识符，必须在同一存档范围内唯一。
        /// </summary>
        public string identifier;

        public string GetIdentifier() => identifier;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(identifier);
        }
    }

    /// <summary>
    /// 运行时实例化对象的持久化信息，额外记录 prefab 和地图来源以便读档重建。
    /// </summary>
    [Serializable]
    public class RuntimeInstancedPersistentDataHandler : APersistenceInfo, IIdentifiablePersistentDataHandler
    {
        /// <summary>
        /// 读档时用于重新实例化对象的 prefab 引用。
        /// </summary>
        public DatabaseEntryReference<PrefabReference> prefab;

        /// <summary>
        /// 对象所属地图标识，用于把运行时实例恢复到正确地图上下文。
        /// </summary>
        [FormerlySerializedAs("map")]
        public string sceneAddress;

        /// <summary>
        /// 运行时实例的稳定标识符。
        /// </summary>
        public string identifier;

        public string GetIdentifier() => identifier;

        public bool IsValid()
        {
            return
                prefab != null &&
                !string.IsNullOrWhiteSpace(prefab.guid) &&
                !string.IsNullOrEmpty(sceneAddress) &&
                !string.IsNullOrEmpty(identifier);
        }
    }

    /// <summary>
    /// 自定义实例化对象的持久化信息，适合由业务系统自行负责重建的对象。
    /// </summary>
    [Serializable]
    public class CustomInstancedPersistentDataHandler : APersistenceInfo, IIdentifiablePersistentDataHandler
    {
        /// <summary>
        /// 自定义持久化对象的稳定标识符。
        /// </summary>
        public string identifier;

        public string GetIdentifier() => identifier;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(identifier);
        }
    }

    /// <summary>
    /// 可持久化对象的归属类型，决定持久化系统按哪条路径识别和恢复对象。
    /// </summary>
    public enum EPersistableOwnershipKind
    {
        None,
        PreInstanced,
        RuntimeInstanced,
        CustomInstanced
    }
}

