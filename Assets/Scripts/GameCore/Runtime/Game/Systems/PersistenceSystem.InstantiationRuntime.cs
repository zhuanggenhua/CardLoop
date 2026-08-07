using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 持久化系统的运行时实例化扩展，集中处理 prefab 实例和持久化标识登记。
    /// </summary>
    public partial class PersistenceSystem
    {
        /// <summary>
        /// 实例化后的 Persistable 和实际使用的标识符。
        /// </summary>
        internal struct InstanstiationResult
        {
            public Persistable persistable;
            public string identifier;
        }

        /// <summary>
        /// 从 PrefabReference 实例化对象，并登记为运行时实例，读档时由持久化系统自动重建。
        /// </summary>
        internal TPersistable InstantiateRuntime<TPersistable>(PrefabReference instance, Vector3 position, Quaternion rotation, Transform parent = null, string identifier = null) where TPersistable : Persistable
        {
            if (instance == null || instance.prefab == null)
            {
                throw new InvalidOperationException($"[{nameof(PersistenceSystem)}] 运行时持久化实例需要有效的 PrefabReference 和 Prefab。");
            }

            InstanstiationResult result = InstantiateInternal(instance.prefab, position, rotation, parent, identifier);
            TPersistable persistable = RequireInstantiatedPersistable<TPersistable>(result, instance.prefab);
            persistable.MakeRuntimeInstanced(instance, result.identifier);
            RegisterPersistable(persistable);
            return persistable;
        }

        /// <summary>
        /// 从 prefab 实例化对象并登记为自定义实例，重建责任由调用方业务系统承担。
        /// </summary>
        internal TPersistable InstantiateCustom<TPersistable>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null, string identifier = null) where TPersistable : Persistable
        {
            InstanstiationResult result = InstantiateInternal(prefab, position, rotation, parent, identifier);
            TPersistable persistable = RequireInstantiatedPersistable<TPersistable>(result, prefab);
            persistable.MakeCustomInstanced(result.identifier);
            RegisterPersistable(persistable);
            return persistable;
        }

        /// <summary>
        /// 执行实际 Instantiate，并返回新对象和本次解析出的标识符。
        /// </summary>
        internal InstanstiationResult InstantiateInternal(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null, string identifier = null)
        {
            if (prefab == null)
            {
                throw new InvalidOperationException($"[{nameof(PersistenceSystem)}] 持久化实例化需要有效 Prefab。");
            }

            identifier = ResolvePersistenceIdentifier(identifier);

            GameObject go = Instantiate(prefab, position, rotation, parent);
            if (!go.TryGetComponent(out Persistable persistable))
            {
                Destroy(go);
                throw new InvalidOperationException(
                    $"[{nameof(PersistenceSystem)}] Prefab {prefab.name} 缺少 {nameof(Persistable)}，不能作为持久化实例。");
            }

            return new InstanstiationResult
            {
                persistable = persistable,
                identifier = identifier
            };
        }

        /// <summary>
        /// 把外部已创建的 Persistable 登记为自定义实例。
        /// </summary>
        internal void RegisterCustomInstancedPersistable(Persistable persistable, string identifier = null)
        {
            if (persistable == null)
            {
                throw new InvalidOperationException($"[{nameof(PersistenceSystem)}] 注册自定义持久化实例需要有效 {nameof(Persistable)}。");
            }

            identifier = ResolvePersistenceIdentifier(identifier);

            persistable.MakeCustomInstanced(identifier);
            RegisterPersistable(persistable);
        }

        private TPersistable RequireInstantiatedPersistable<TPersistable>(InstanstiationResult result, GameObject prefab)
            where TPersistable : Persistable
        {
            if (result.persistable is TPersistable persistable)
            {
                return persistable;
            }

            m_persistables.Remove(result.identifier);
            RemovePersistableIdentifier(result.identifier);
            if (result.persistable != null)
            {
                Destroy(result.persistable.gameObject);
            }

            throw new InvalidOperationException(
                $"[{nameof(PersistenceSystem)}] Prefab {(prefab ? prefab.name : "<null>")} 必须包含 {typeof(TPersistable).Name}，不能登记为该类型的持久化实例。");
        }

        private static string ResolvePersistenceIdentifier(string identifier)
        {
            if (identifier == null)
            {
                return Guid.NewGuid().ToString();
            }

            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new InvalidOperationException($"[{nameof(PersistenceSystem)}] 持久化实例标识符不能是空字符串。");
            }

            return identifier;
        }
    }
}

