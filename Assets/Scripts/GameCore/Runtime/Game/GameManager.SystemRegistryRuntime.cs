using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    public partial class GameManager
    {
        private readonly List<AGameSystem> m_systemExecutionOrder = new();
        private readonly List<AGameSystem> m_initializedSystems = new();
        private readonly List<AGameSystem> m_startedSystems = new();

        /// <summary>
        /// 收集并初始化项目级正式系统。
        /// 只登记 GameManager 层级中明确装配的系统，避免把其它场景对象误提升为进程级系统。
        /// </summary>
        private void InitializeSystems()
        {
            if (m_initializedSystems.Count > 0)
            {
                throw new InvalidOperationException(
                    "GameManager 已经完成系统初始化，不能重复初始化。请先关闭当前进程级运行时。");
            }

            foreach (AGameSystem system in m_systemExecutionOrder)
            {
                try
                {
                    system.OnSystemInit();
                    m_initializedSystems.Add(system);
                }
                catch
                {
                    ShutdownSystemSafely(system);
                    ShutdownSystems();
                    throw;
                }
            }
        }

        private void StartSystems()
        {
            if (m_startedSystems.Count > 0)
            {
                throw new InvalidOperationException(
                    "GameManager 已经启动系统，不能重复启动。请先停止当前进程级运行时。");
            }

            foreach (AGameSystem system in m_systemExecutionOrder)
            {
                try
                {
                    system.OnSystemStart();
                    m_startedSystems.Add(system);
                }
                catch
                {
                    StopSystemSafely(system);
                    StopSystems();
                    throw;
                }
            }
        }

        private void StopSystems()
        {
            for (int i = m_startedSystems.Count - 1; i >= 0; i--)
            {
                StopSystemSafely(m_startedSystems[i]);
            }

            m_startedSystems.Clear();
        }

        private void ShutdownSystems()
        {
            StopSystems();

            for (int i = m_initializedSystems.Count - 1; i >= 0; i--)
            {
                ShutdownSystemSafely(m_initializedSystems[i]);
            }

            m_initializedSystems.Clear();
        }

        private void FindSystems()
        {
            AGameSystem[] systems = GetComponentsInChildren<AGameSystem>(includeInactive: false);

            m_systems = new Dictionary<Type, AGameSystem>();
            m_systemExecutionOrder.Clear();

            foreach (AGameSystem system in systems)
            {
                Type type = system.GetType();
                if (m_systems.ContainsKey(type))
                {
                    throw new InvalidOperationException(
                        $"Game System {type.Name} already registered. Only one {type.Name} can be owned by the active GameManager.");
                }

                m_systems[type] = system;
            }

            ResolveSystemExecutionOrder(systems);
        }

        private void ResolveSystemExecutionOrder(AGameSystem[] discoveredSystems)
        {
            var visiting = new HashSet<Type>();
            var visited = new HashSet<Type>();
            var dependencyPath = new List<Type>();

            foreach (AGameSystem system in discoveredSystems)
            {
                AddSystemWithDependencies(system, visiting, visited, dependencyPath);
            }
        }

        private void AddSystemWithDependencies(
            AGameSystem system,
            HashSet<Type> visiting,
            HashSet<Type> visited,
            List<Type> dependencyPath)
        {
            Type systemType = system.GetType();
            if (visited.Contains(systemType))
            {
                return;
            }

            if (!visiting.Add(systemType))
            {
                dependencyPath.Add(systemType);
                string cycle = string.Join(" -> ", dependencyPath.ConvertAll(type => type.Name));
                dependencyPath.RemoveAt(dependencyPath.Count - 1);
                throw new InvalidOperationException($"Game System startup dependency cycle detected: {cycle}.");
            }

            dependencyPath.Add(systemType);
            IReadOnlyCollection<Type> dependencies = system.StartupDependencies ?? Array.Empty<Type>();
            foreach (Type dependencyType in dependencies)
            {
                ValidateDependency(systemType, dependencyType);

                if (!m_systems.TryGetValue(dependencyType, out AGameSystem dependency))
                {
                    throw new InvalidOperationException(
                        $"Game System {systemType.Name} requires {dependencyType.Name}, but no {dependencyType.Name} is configured under the active GameManager.");
                }

                AddSystemWithDependencies(dependency, visiting, visited, dependencyPath);
            }

            dependencyPath.RemoveAt(dependencyPath.Count - 1);
            visiting.Remove(systemType);
            visited.Add(systemType);
            m_systemExecutionOrder.Add(system);
        }

        private static void ValidateDependency(Type systemType, Type dependencyType)
        {
            if (dependencyType == null)
            {
                throw new InvalidOperationException($"Game System {systemType.Name} declares a null startup dependency.");
            }

            if (!typeof(AGameSystem).IsAssignableFrom(dependencyType))
            {
                throw new InvalidOperationException(
                    $"Game System {systemType.Name} declares {dependencyType.Name} as a startup dependency, but it is not an {nameof(AGameSystem)}.");
            }

            if (dependencyType == systemType)
            {
                throw new InvalidOperationException($"Game System {systemType.Name} cannot depend on itself.");
            }
        }

        private static void StopSystemSafely(AGameSystem system)
        {
            try
            {
                system.OnSystemStop();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    $"Game System {system.GetType().Name} failed while stopping.", exception),
                    system);
            }
        }

        private static void ShutdownSystemSafely(AGameSystem system)
        {
            try
            {
                system.OnSystemShutdown();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    $"Game System {system.GetType().Name} failed while shutting down.", exception),
                    system);
            }
        }

        public static bool HasSystem<T>() where T : AGameSystem
        {
            return _instance != null &&
                _instance.m_systems != null &&
                _instance.m_systems.ContainsKey(typeof(T));
        }

        public static bool TryGetSystem<T>(out T system) where T : AGameSystem
        {
            system = null;
            if (_instance == null || _instance.m_systems == null)
            {
                return false;
            }

            bool systemFound = _instance.m_systems.TryGetValue(typeof(T), out AGameSystem gameSystem);
            system = systemFound ? (T)gameSystem : null;
            return systemFound;
        }

        public static T GetSystem<T>() where T : AGameSystem
        {
            if (TryGetSystem(out T system))
            {
                return system;
            }

            throw new InvalidOperationException(
                $"Game System {typeof(T).Name} could not be found. Add exactly one {typeof(T).Name} under the active GameManager scene hierarchy before using GameManager.{typeof(T).Name}.");
        }
    }
}
