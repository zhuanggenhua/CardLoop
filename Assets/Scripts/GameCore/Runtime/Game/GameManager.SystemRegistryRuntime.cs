using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public partial class GameManager
    {
        /// <summary>
        /// 收集并初始化项目级正式系统。
        /// 这里只处理 AGameSystem 根节点注册，不承担世界、模式或实体层状态所有权。
        /// </summary>
        private void InitializeSystems()
        {
            foreach (AGameSystem system in m_systems.Values)
            {
                system.OnSystemInit();
            }
        }

        private void StartSystems()
        {
            foreach (AGameSystem system in m_systems.Values)
            {
                system.OnSystemStart();
            }
        }

        private void StopSystems()
        {
            foreach (AGameSystem system in m_systems.Values)
            {
                system.OnSystemStop();
            }
        }

        private void FindSystems()
        {
            AGameSystem[] systems = FindObjectsByType<AGameSystem>(FindObjectsSortMode.InstanceID);

            m_systems = new Dictionary<Type, AGameSystem>();

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
