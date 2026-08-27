using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using GAS.Runtime;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 角色某个 EX-GAS 属性值发生变化后的事件载荷。
    /// </summary>
    [Serializable]
    public readonly struct CharacterAttributeValueChange
    {
        public CharacterAttributeValueChange(int attributeCode, float previousValue, float currentValue)
        {
            AttributeCode = attributeCode;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }

        public int AttributeCode { get; }
        public float PreviousValue { get; }
        public float CurrentValue { get; }
    }

    /// <summary>
    /// 角色对 EX-GAS 属性集默认值的差异覆盖。
    /// 属性身份和钳制规则仍由 EX-GAS 表负责，这里只保存角色自己的基础值。
    /// </summary>
    [Serializable]
    public struct CharacterAttributeOverride
    {
        [LabelText("属性")]
        [Tooltip("引用 EX-GAS FightUnit 属性集中的唯一属性码。")]
        [CharacterAttributeCode]
        [SerializeField] private int m_attributeCode;

        [LabelText("基础值")]
        [Tooltip("该角色覆盖 EX-GAS 表默认值后的基础值；钳制范围仍取自 EX-GAS 属性集表。")]
        [SerializeField] private float m_baseValue;

        public CharacterAttributeOverride(int attributeCode, float baseValue)
        {
            m_attributeCode = attributeCode;
            m_baseValue = baseValue;
        }

        public int AttributeCode => m_attributeCode;
        public float BaseValue => m_baseValue;
    }

    /// <summary>
    /// GameCore 运行时需要识别的标准角色属性码。
    /// 数值只能由项目组合程序集从 EX-GAS 生成常量传入，不能在 GameCore 内手写。
    /// </summary>
    public readonly struct CharacterAttributeCodes
    {
        public CharacterAttributeCodes(
            int setCode,
            int health,
            int mana,
            int moveSpeed,
            int attack,
            int defense,
            int stamina,
            int maxHealth,
            int maxMana,
            int maxStamina,
            int attackSpeed,
            int accuracy,
            int dodge,
            int criticalChance,
            int criticalMultiplier)
        {
            SetCode = RequirePositive(setCode, nameof(setCode));
            Health = RequirePositive(health, nameof(health));
            Mana = RequirePositive(mana, nameof(mana));
            MoveSpeed = RequirePositive(moveSpeed, nameof(moveSpeed));
            Attack = RequirePositive(attack, nameof(attack));
            Defense = RequirePositive(defense, nameof(defense));
            Stamina = RequirePositive(stamina, nameof(stamina));
            MaxHealth = RequirePositive(maxHealth, nameof(maxHealth));
            MaxMana = RequirePositive(maxMana, nameof(maxMana));
            MaxStamina = RequirePositive(maxStamina, nameof(maxStamina));
            AttackSpeed = RequirePositive(attackSpeed, nameof(attackSpeed));
            Accuracy = RequirePositive(accuracy, nameof(accuracy));
            Dodge = RequirePositive(dodge, nameof(dodge));
            CriticalChance = RequirePositive(criticalChance, nameof(criticalChance));
            CriticalMultiplier = RequirePositive(criticalMultiplier, nameof(criticalMultiplier));
        }

        public int SetCode { get; }
        public int Health { get; }
        public int Mana { get; }
        public int MoveSpeed { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int Stamina { get; }
        public int MaxHealth { get; }
        public int MaxMana { get; }
        public int MaxStamina { get; }
        public int AttackSpeed { get; }
        public int Accuracy { get; }
        public int Dodge { get; }
        public int CriticalChance { get; }
        public int CriticalMultiplier { get; }

        private static int RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "EX-GAS 属性码必须是正整数。");
            }

            return value;
        }
    }

    /// <summary>
    /// GameCore 所需的 EX-GAS 角色属性入口。
    /// 属性身份由项目组合程序集从 EX-GAS 生成代码注入；这里不维护并行属性编号。
    /// </summary>
    public static class CharacterAttributes
    {
        private static CharacterAttributeCodes s_codes;
        private static Func<AttrSetConfig> s_createCanonicalConfig;
        private static HashSet<int> s_knownAttributeCodes;
        private static bool s_isConfigured;

        public static int SetCode => Codes.SetCode;
        public static int Health => Codes.Health;
        public static int Mana => Codes.Mana;
        public static int MoveSpeed => Codes.MoveSpeed;
        public static int Attack => Codes.Attack;
        public static int Defense => Codes.Defense;
        public static int Stamina => Codes.Stamina;
        public static int MaxHealth => Codes.MaxHealth;
        public static int MaxMana => Codes.MaxMana;
        public static int MaxStamina => Codes.MaxStamina;
        public static int AttackSpeed => Codes.AttackSpeed;
        public static int Accuracy => Codes.Accuracy;
        public static int Dodge => Codes.Dodge;
        public static int CriticalChance => Codes.CriticalChance;
        public static int CriticalMultiplier => Codes.CriticalMultiplier;

        /// <summary>
        /// 由引用 EX-GAS 生成程序集的项目组合入口配置标准属性码。
        /// 未配置就读取属于启动接线错误，应立即抛出而不是回退到手写编号。
        /// </summary>
        public static void Configure(
            CharacterAttributeCodes codes,
            Func<AttrSetConfig> createCanonicalConfig)
        {
            if (createCanonicalConfig == null)
            {
                throw new ArgumentNullException(nameof(createCanonicalConfig));
            }

            s_codes = codes;
            s_createCanonicalConfig = createCanonicalConfig;
            s_knownAttributeCodes = null;
            s_isConfigured = true;
        }

        /// <summary>
        /// 确认属性码确实属于 EX-GAS FightUnit 属性集。
        /// 这是由生成配置派生的只读索引，不是第二份作者码表。
        /// </summary>
        public static void RequireKnownAttributeCode(int attributeCode)
        {
            if (attributeCode <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attributeCode),
                    attributeCode,
                    "EX-GAS 属性码必须是正整数。");
            }

            EnsureKnownAttributeCodes();
            if (!s_knownAttributeCodes.Contains(attributeCode))
            {
                throw new InvalidOperationException(
                    $"EX-GAS FightUnit 属性集 {SetCode} 不包含属性码 {attributeCode}。");
            }
        }

        /// <summary>
        /// 返回 EX-GAS FightUnit 属性集当前生成出的全部属性码。
        /// 返回数组是派生快照，调用方不能把它当作者源修改。
        /// </summary>
        public static int[] GetKnownAttributeCodes()
        {
            EnsureKnownAttributeCodes();
            int[] result = new int[s_knownAttributeCodes.Count];
            s_knownAttributeCodes.CopyTo(result);
            Array.Sort(result);
            return result;
        }

        /// <summary>
        /// 通过 EX-GAS 正式 Cell 修改角色基础属性，并立即重算当前值。
        /// 用于运行时规则需要在同一流程内读取最新当前值的资源变化。
        /// </summary>
        public static float SetBaseValueAndRecalculate(
            AbilitySystemCell abilitySystem,
            int attributeCode,
            float value)
        {
            if (abilitySystem == null)
            {
                throw new ArgumentNullException(nameof(abilitySystem));
            }
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "EX-GAS 属性基础值必须是有限数值。");
            }

            RequireKnownAttributeCode(attributeCode);
            abilitySystem.SetAttrBaseValue(SetCode, attributeCode, value);
            return AttributeHelper.RecalculateCurrentValue(
                abilitySystem.Entity,
                SetCode,
                attributeCode);
        }

        /// <summary>
        /// 从 EX-GAS 正式属性集克隆角色配置，并只替换角色声明的基础值。
        /// 未覆盖属性和所有钳制规则始终继承 EX-GAS 表。
        /// </summary>
        public static AttrSetConfig CreateConfig(IReadOnlyList<CharacterAttributeOverride> overrides)
        {
            AttrSetConfig canonical = CreateCanonicalConfig();
            AttributeBaseSetting[] sourceSettings = canonical.Settings;
            AttributeBaseSetting[] settings = new AttributeBaseSetting[sourceSettings.Length];
            Array.Copy(sourceSettings, settings, sourceSettings.Length);

            HashSet<int> overriddenCodes = new();
            if (overrides != null)
            {
                for (int i = 0; i < overrides.Count; i++)
                {
                    CharacterAttributeOverride attributeOverride = overrides[i];
                    ValidateOverride(attributeOverride, overriddenCodes);

                    int settingIndex = FindSettingIndex(settings, attributeOverride.AttributeCode);
                    if (settingIndex < 0)
                    {
                        throw new InvalidOperationException(
                            $"角色属性覆盖引用了 FightUnit 属性集不存在的属性码 {attributeOverride.AttributeCode}。");
                    }

                    ValidateValueAgainstGasClamp(
                        settings[settingIndex],
                        attributeOverride.BaseValue);
                    settings[settingIndex] = WithInitialValue(
                        settings[settingIndex],
                        attributeOverride.BaseValue);
                }
            }

            SynchronizeInitialResource(settings, overriddenCodes, MaxHealth, Health);
            SynchronizeInitialResource(settings, overriddenCodes, MaxMana, Mana);
            SynchronizeInitialResource(settings, overriddenCodes, MaxStamina, Stamina);

            return new AttrSetConfig(SetCode, settings);
        }

        private static CharacterAttributeCodes Codes => s_isConfigured
            ? s_codes
            : throw new InvalidOperationException(
                "标准角色属性尚未由 EX-GAS 生成配置完成接线。"
                + "请确认 GameCore.GasIntegration 已在角色初始化前运行。");

        private static AttrSetConfig CreateCanonicalConfig()
        {
            _ = Codes;
            AttrSetConfig config = s_createCanonicalConfig();
            if (config.Code != SetCode)
            {
                throw new InvalidOperationException(
                    $"EX-GAS 角色属性配置返回了属性集 {config.Code}，预期为 {SetCode}。");
            }

            if (config.Settings == null || config.Settings.Length == 0)
            {
                throw new InvalidOperationException(
                    $"EX-GAS FightUnit 属性集 {SetCode} 没有可用属性配置。");
            }

            return config;
        }

        private static void EnsureKnownAttributeCodes()
        {
            if (s_knownAttributeCodes != null)
            {
                return;
            }

            AttrSetConfig config = CreateCanonicalConfig();
            HashSet<int> knownAttributeCodes = new();
            for (int i = 0; i < config.Settings.Length; i++)
            {
                knownAttributeCodes.Add(config.Settings[i].Code);
            }

            s_knownAttributeCodes = knownAttributeCodes;
        }

        private static void ValidateOverride(
            CharacterAttributeOverride attributeOverride,
            HashSet<int> overriddenCodes)
        {
            if (attributeOverride.AttributeCode <= 0)
            {
                throw new InvalidOperationException("角色属性覆盖必须选择有效的 EX-GAS 属性。");
            }

            if (float.IsNaN(attributeOverride.BaseValue) || float.IsInfinity(attributeOverride.BaseValue))
            {
                throw new InvalidOperationException(
                    $"属性 {attributeOverride.AttributeCode} 的角色基础值必须是有限数值。");
            }

            if (!overriddenCodes.Add(attributeOverride.AttributeCode))
            {
                throw new InvalidOperationException(
                    $"角色属性 {attributeOverride.AttributeCode} 被重复覆盖。每个属性只能声明一次。");
            }
        }

        private static void SynchronizeInitialResource(
            AttributeBaseSetting[] settings,
            HashSet<int> overriddenCodes,
            int maximumCode,
            int currentCode)
        {
            if (!overriddenCodes.Contains(maximumCode) || overriddenCodes.Contains(currentCode))
            {
                return;
            }

            int maximumIndex = FindSettingIndex(settings, maximumCode);
            int currentIndex = FindSettingIndex(settings, currentCode);
            if (maximumIndex < 0 || currentIndex < 0)
            {
                throw new InvalidOperationException(
                    $"EX-GAS FightUnit 属性集缺少资源配对 {currentCode}/{maximumCode}。");
            }

            settings[currentIndex] = WithInitialValue(
                settings[currentIndex],
                settings[maximumIndex].InitValue);
        }

        private static void ValidateValueAgainstGasClamp(
            AttributeBaseSetting setting,
            float value)
        {
            if (setting.IsClampMin && value < setting.Min)
            {
                throw new InvalidOperationException(
                    $"属性 {setting.Code} 的角色基础值 {value} 小于 EX-GAS 表下限 {setting.Min}。");
            }

            if (setting.IsClampMax && value > setting.Max)
            {
                throw new InvalidOperationException(
                    $"属性 {setting.Code} 的角色基础值 {value} 大于 EX-GAS 表上限 {setting.Max}。");
            }
        }

        private static int FindSettingIndex(AttributeBaseSetting[] settings, int attributeCode)
        {
            for (int i = 0; i < settings.Length; i++)
            {
                if (settings[i].Code == attributeCode)
                {
                    return i;
                }
            }

            return -1;
        }

        private static AttributeBaseSetting WithInitialValue(
            AttributeBaseSetting setting,
            float initialValue)
        {
            return new AttributeBaseSetting(
                setting.Code,
                initialValue,
                setting.IsClampMin,
                setting.IsClampMax,
                setting.Min,
                setting.Max);
        }
    }
}
