using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameCore
{
    /// <summary>
    /// 正式属性定义。
    /// 这份定义表只回答“项目里到底有哪些正式属性，以及它们的稳定标识是什么”，
    /// 不承担具体结算、存档或 UI 逻辑。
    /// </summary>
    public readonly struct FormalAttributeDefinition
    {
        public FormalAttributeDefinition(EStat stat, string stableId, string displayName, bool isResource, bool supportsCurrentValue)
        {
            Stat = stat;
            StableId = stableId ?? throw new ArgumentNullException(nameof(stableId));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            IsResource = isResource;
            SupportsCurrentValue = supportsCurrentValue;
        }

        public EStat Stat { get; }

        public string StableId { get; }

        public string DisplayName { get; }

        public bool IsResource { get; }

        public bool SupportsCurrentValue { get; }

        public int Index => (int)Stat;
    }

    /// <summary>
    /// 正式属性目录。
    /// 后续若由 GAS 接管，优先替换的是这份目录所映射的真相来源，而不是让调用方继续自己猜 enum 顺序。
    /// </summary>
    public static class FormalAttributeCatalog
    {
        private static readonly FormalAttributeDefinition[] s_definitionArray =
        {
            new(EStat.Health, "core.health", "生命", isResource: true, supportsCurrentValue: true),
            new(EStat.Mana, "core.mana", "法力", isResource: true, supportsCurrentValue: true),
            new(EStat.PhysicalAttack, "core.physical_attack", "物攻", isResource: false, supportsCurrentValue: true),
            new(EStat.MagicalAttack, "core.magical_attack", "法攻", isResource: false, supportsCurrentValue: true),
            new(EStat.PhysicalDefense, "core.physical_defense", "物防", isResource: false, supportsCurrentValue: true),
            new(EStat.MagicalDefense, "core.magical_defense", "法防", isResource: false, supportsCurrentValue: true),
            new(EStat.Agility, "core.agility", "敏捷", isResource: false, supportsCurrentValue: true),
            new(EStat.Luck, "core.luck", "幸运", isResource: false, supportsCurrentValue: true),
            new(EStat.AttackSpeed, "core.attack_speed", "攻速", isResource: false, supportsCurrentValue: true)
        };
        private static readonly ReadOnlyCollection<FormalAttributeDefinition> s_definitions = Array.AsReadOnly(s_definitionArray);

        static FormalAttributeCatalog()
        {
            Array enumValues = Enum.GetValues(typeof(EStat));
            if (enumValues.Length != s_definitionArray.Length)
            {
                throw new InvalidOperationException(
                    $"正式属性目录数量 {s_definitionArray.Length} 与 {nameof(EStat)} 枚举数量 {enumValues.Length} 不一致。");
            }

            HashSet<string> stableIds = new(StringComparer.Ordinal);
            for (int i = 0; i < s_definitionArray.Length; i++)
            {
                FormalAttributeDefinition definition = s_definitionArray[i];
                if (definition.Index != i)
                {
                    throw new InvalidOperationException(
                        $"正式属性目录顺序错误：索引 {i} 对应的是 {definition.Stat}。目录顺序必须与 {nameof(EStat)} 一致。");
                }

                if (string.IsNullOrWhiteSpace(definition.StableId))
                {
                    throw new InvalidOperationException($"正式属性 {definition.Stat} 缺少稳定 ID。");
                }

                if (!stableIds.Add(definition.StableId))
                {
                    throw new InvalidOperationException($"正式属性稳定 ID 重复：{definition.StableId}");
                }
            }
        }

        public static IReadOnlyList<FormalAttributeDefinition> Definitions => s_definitions;

        public static int Count => s_definitions.Count;

        public static FormalAttributeDefinition Get(EStat stat) => s_definitionArray[(int)stat];

        public static FormalAttributeDefinition Get(int index) => s_definitionArray[index];

        public static bool TryGetByStableId(string stableId, out FormalAttributeDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(stableId))
            {
                for (int i = 0; i < s_definitionArray.Length; i++)
                {
                    if (string.Equals(s_definitionArray[i].StableId, stableId, StringComparison.Ordinal))
                    {
                        definition = s_definitionArray[i];
                        return true;
                    }
                }
            }

            definition = default;
            return false;
        }
    }
}
