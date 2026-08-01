using System;
using GAS.Runtime;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 当前阶段对接 EX-GAS 2.0 的正式属性编码表。
    /// 它只提供项目属性到 EX-GAS attrSetCode/attrCode 的稳定映射，
    /// 不再伪装成旧版本已不存在的 OOP AttributeSet/AttributeBase 类型。
    /// </summary>
    public static class FormalGameplayAttributeSet
    {
        public const int SetCode = 1001;

        public const int Health = 1;
        public const int Mana = 2;
        public const int PhysicalAttack = 3;
        public const int MagicalAttack = 4;
        public const int PhysicalDefense = 5;
        public const int MagicalDefense = 6;
        public const int Agility = 7;
        public const int Luck = 8;
        public const int AttackSpeed = 9;

        public static AttrSetConfig CreateConfig(Stats baseStats)
        {
            Stats safeBaseStats = baseStats ?? new Stats();
            return new AttrSetConfig(
                SetCode,
                new[]
                {
                    CreateResourceSetting(Health, safeBaseStats[EStat.Health]),
                    CreateResourceSetting(Mana, safeBaseStats[EStat.Mana]),
                    CreateRegularSetting(PhysicalAttack, safeBaseStats[EStat.PhysicalAttack]),
                    CreateRegularSetting(MagicalAttack, safeBaseStats[EStat.MagicalAttack]),
                    CreateRegularSetting(PhysicalDefense, safeBaseStats[EStat.PhysicalDefense]),
                    CreateRegularSetting(MagicalDefense, safeBaseStats[EStat.MagicalDefense]),
                    CreateRegularSetting(Agility, safeBaseStats[EStat.Agility]),
                    CreateRegularSetting(Luck, safeBaseStats[EStat.Luck]),
                    CreateRegularSetting(AttackSpeed, safeBaseStats[EStat.AttackSpeed]),
                });
        }

        public static int GetAttributeCode(EStat stat)
        {
            return stat switch
            {
                EStat.Health => Health,
                EStat.Mana => Mana,
                EStat.PhysicalAttack => PhysicalAttack,
                EStat.MagicalAttack => MagicalAttack,
                EStat.PhysicalDefense => PhysicalDefense,
                EStat.MagicalDefense => MagicalDefense,
                EStat.Agility => Agility,
                EStat.Luck => Luck,
                EStat.AttackSpeed => AttackSpeed,
                _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "未知正式属性。")
            };
        }

        private static AttributeBaseSetting CreateRegularSetting(int attributeCode, int initialValue)
        {
            return new AttributeBaseSetting(
                attributeCode,
                initialValue,
                isClampMin: false,
                isClampMax: false,
                min: 0.0f,
                max: 0.0f);
        }

        private static AttributeBaseSetting CreateResourceSetting(int attributeCode, int initialValue)
        {
            return new AttributeBaseSetting(
                attributeCode,
                initialValue,
                isClampMin: true,
                isClampMax: false,
                min: 0.0f,
                max: 0.0f);
        }
    }
}
