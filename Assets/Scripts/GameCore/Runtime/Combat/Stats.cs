using System;
using Unity.Mathematics;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 角色正式属性枚举。
    /// 顺序需要与 FormalAttributeCatalog 保持兼容，存档数组按该顺序保存。
    /// </summary>
    public enum EStat
    {
        Health = 0,
        Mana = 1,
        PhysicalAttack = 2,
        MagicalAttack = 3,
        PhysicalDefense = 4,
        MagicalDefense = 5,
        Agility = 6,
        Luck = 7,
        AttackSpeed = 8
    }

    /// <summary>
    /// 一组角色属性值。
    /// 内部数组会随 FormalAttributeCatalog 尺寸调整，避免新增属性后旧存档越界。
    /// </summary>
    [Serializable]
    public class Stats : ISerializationCallbackReceiver
    {
        public static int StatCount => FormalAttributeCatalog.Count;

        [SerializeField] private int[] m_values = new int[StatCount];

        public Stats() : this(new int[StatCount])
        {
        }

        public Stats(Stats copy)
        {
            Array.Copy(copy.m_values, m_values, math.min(copy.m_values.Length, StatCount));
        }

        public Stats(int[] values)
        {
            if (values == null)
            {
                return;
            }

            Array.Copy(values, m_values, math.min(values.Length, StatCount));
        }

        public Stats Clone() => new(this);

        public void OnBeforeSerialize()
        {
            ResizeBackingArrayIfNeeded();
        }

        public void OnAfterDeserialize()
        {
            ResizeBackingArrayIfNeeded();
        }

        public void Reset()
        {
            for (int i = 0; i < m_values.Length; ++i)
            {
                m_values[i] = 0;
            }
        }

        public int GetTotal()
        {
            int total = 0;

            for (int i = 0; i < StatCount; ++i)
            {
                total += m_values[i];
            }

            return total;
        }

        private int this[int i]
        {
            get => m_values[i];
            set => m_values[i] = value;
        }

        public int this[EStat stat]
        {
            get => this[(int)stat];
            set => this[(int)stat] = value;
        }

        public static Stats operator +(Stats a, Stats b)
        {
            Stats output = new();

            for (int i = 0; i < StatCount; ++i)
            {
                output[i] = a[i] + b[i];
            }

            return output;
        }

        public static Stats operator -(Stats a, Stats b)
        {
            Stats output = new();

            for (int i = 0; i < StatCount; ++i)
            {
                output[i] = a[i] - b[i];
            }

            return output;
        }

        public static Stats operator *(Stats a, float scale)
        {
            Stats output = new();

            for (int i = 0; i < StatCount; ++i)
            {
                output[i] = (int)math.floor(a[i] * scale);
            }

            return output;
        }

        public static Stats Lerp(Stats a, Stats b, float t)
        {
            Stats output = new();

            for (int i = 0; i < StatCount; ++i)
            {
                output[i] = a[i] + b[i];
                output[i] = (int)math.floor(math.lerp(a[i], b[i], t));
            }

            return output;
        }

        private void ResizeBackingArrayIfNeeded()
        {
            if (m_values != null && m_values.Length == StatCount)
            {
                return;
            }

            int[] resizedValues = new int[StatCount];
            if (m_values != null)
            {
                Array.Copy(m_values, resizedValues, math.min(m_values.Length, StatCount));
            }

            m_values = resizedValues;
        }
    }

    /// <summary>
    /// 属性随等级成长的插值配置。
    /// min/max 分别代表最低和最高等级的属性端点。
    /// </summary>
    [Serializable]
    public struct StatsEvolutionProfile
    {
        public Stats min;
        public Stats max;

        public static int Mix(int min, int max, float a) => (int)math.floor(math.lerp(min, max, a));

        public Stats GetStatsAtLevel(int level)
        {
            float t = (level - 1) / (float)(Constants.MaxLevel - 1);
            return Stats.Lerp(min, max, t);
        }
    }
}

