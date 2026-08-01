using System;
using System.Collections.Generic;
using UnityEngine;
using azixMcAze.SerializableDictionary;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 按目标记录冷却剩余时间的存档块，目标必须能通过持久化引用恢复。
    /// </summary>
    [Serializable]
    public class PerTargetCooldownDataBlock<TargetType> : DataBlock where TargetType : Persistable
    {
        /// <summary>
        /// 目标持久化引用到剩余冷却秒数的映射。
        /// </summary>
        public SerializableDictionary<PersistableReference<TargetType>, float> perTargetCooldowns = new();
    }

    /// <summary>
    /// 针对不同目标分别维护冷却时间，常用于同一技能对每个目标独立限频。
    /// </summary>
    public class PerTargetCooldown<TargetType> : IDataBlockHandler<PerTargetCooldownDataBlock<TargetType>> where TargetType : Persistable
    {
        private Dictionary<TargetType, float> m_perTargetCooldowns = new();

        /// <summary>
        /// 从候选目标中筛出仍存在且不在冷却中的目标快照。
        /// </summary>
        public TargetType[] CreateValidTargetSnapshot(IEnumerable<TargetType> potentialTargets)
        {
            if (potentialTargets == null)
            {
                return Array.Empty<TargetType>();
            }

            List<TargetType> validTargets = new();
            foreach (TargetType target in potentialTargets)
            {
                if (CanTrackTarget(target) && !IsTargetOnCooldown(target))
                {
                    validTargets.Add(target);
                }
            }

            return validTargets.ToArray();
        }

        /// <summary>
        /// 判断目标当前是否仍处于冷却中；无效或已销毁目标不算冷却。
        /// </summary>
        public bool IsTargetOnCooldown(TargetType target) => CanTrackTarget(target) && m_perTargetCooldowns.ContainsKey(target);

        /// <summary>
        /// 为目标启动一段冷却；目标无效或持续时间小于等于 0 时忽略。
        /// </summary>
        public void StartCooldown(TargetType target, float duration)
        {
            if (CanTrackTarget(target) && duration > 0.0f)
            {
                m_perTargetCooldowns[target] = duration;
            }
        }

        /// <summary>
        /// 清空所有目标冷却状态。
        /// </summary>
        public void Reset()
        {
            m_perTargetCooldowns.Clear();
        }

        /// <summary>
        /// 使用 Time.deltaTime 推进冷却。
        /// </summary>
        public void Update() => Update(Time.deltaTime);

        /// <summary>
        /// 推进指定秒数，并移除结束冷却或已经失效的目标。
        /// </summary>
        public void Update(float deltaTime)
        {
            foreach (var key in new HashSet<TargetType>(m_perTargetCooldowns.Keys))
            {
                if (!CanTrackTarget(key))
                {
                    m_perTargetCooldowns.Remove(key);
                    continue;
                }

                m_perTargetCooldowns[key] -= Mathf.Max(0.0f, deltaTime);
                if (m_perTargetCooldowns[key] <= 0.0f)
                {
                    m_perTargetCooldowns.Remove(key);
                }
            }
        }

        /// <summary>
        /// 导出仍有效且剩余时间大于 0 的目标冷却快照。
        /// </summary>
        public PerTargetCooldownDataBlock<TargetType> CreateDataBlock()
        {
            SerializableDictionary<PersistableReference<TargetType>, float> cooldowns = new();
            foreach (KeyValuePair<TargetType, float> pair in m_perTargetCooldowns)
            {
                if (!CanTrackTarget(pair.Key) || pair.Value <= 0.0f)
                {
                    continue;
                }

                cooldowns[new PersistableReference<TargetType>(pair.Key)] = pair.Value;
            }

            return new PerTargetCooldownDataBlock<TargetType>
            {
                perTargetCooldowns = cooldowns
            };
        }

        /// <summary>
        /// 从存档块恢复目标冷却；无法解析的目标会被丢弃。
        /// </summary>
        public void LoadDataBlock(PerTargetCooldownDataBlock<TargetType> block)
        {
            m_perTargetCooldowns.Clear();
            if (block?.perTargetCooldowns == null)
            {
                return;
            }

            foreach (KeyValuePair<PersistableReference<TargetType>, float> pair in block.perTargetCooldowns)
            {
                TargetType target = pair.Key.ResolveOrNull();
                if (!CanTrackTarget(target) || pair.Value <= 0.0f)
                {
                    continue;
                }

                // 冷却快照只保存仍存在的目标，避免读档后把已销毁对象重新当成命中事实。
                m_perTargetCooldowns[target] = pair.Value;
            }
        }

        private static bool CanTrackTarget(TargetType target)
        {
            return target != null && !target.isMarkedAsDestroyed;
        }
    }
}

