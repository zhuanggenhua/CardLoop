using System;
using System.Collections.Generic;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    /// <summary>
    /// 角色阵营语义。
    /// 当前主要服务敌我、交互和条件判断，不等同于存档身份或队伍所有权。
    /// </summary>
    public enum EAlignment
    {
        Good,
        Evil,
        Neutral,
        Default = Neutral
    }

    /// <summary>
    /// 角色资源扣减或校验的结果。
    /// 调用方用它区分是生命、法力还是资源条件本身失败。
    /// </summary>
    public enum EResourceValidationResult
    {
        Valid,
        HealthBelowMinimum,
        ManaBelowMinimum
    }

    /// <summary>
    /// 当前角色可执行动作的位标记。
    /// 控制层和状态效果通过它临时禁用移动、交互、技能和装备变更等入口。
    /// </summary>
    [Flags]
    public enum EActionFlags
    {
        [HideInInspector] None = 0,
        Move = 1 << 0,
        Interact = 1 << 1,
        UseAbility = 1 << 2,
        UpdateTargetDirection = 1 << 3,
        [HideInInspector] All = ~None
    }

    /// <summary>
    /// 角色获得正式 EX-GAS 能力的来源类型。
    /// 该来源用于叠加、移除和存档恢复，不直接代表 UI 显示分组。
    /// </summary>
    public enum ECharacterAbilitySourceKind
    {
        Script,
        Summon,
        StatusEffect,
        Transformation,
        Infection
    }

    /// <summary>
    /// 一个能力来源的稳定键。
    /// 同类来源通过 SourceId 区分具体装备、状态、召唤物或脚本入口，避免只靠能力编号判断叠加关系。
    /// </summary>
    [Serializable]
    public readonly struct CharacterAbilitySourceKey : IEquatable<CharacterAbilitySourceKey>
    {
        public static readonly CharacterAbilitySourceKey Script = new(ECharacterAbilitySourceKind.Script, "script");

        public CharacterAbilitySourceKey(ECharacterAbilitySourceKind kind, string sourceId)
        {
            Kind = kind;
            SourceId = NormalizeSourceId(sourceId);
        }

        public ECharacterAbilitySourceKind Kind { get; }
        public string SourceId { get; }

        public bool Equals(CharacterAbilitySourceKey other) =>
            Kind == other.Kind &&
            string.Equals(SourceId, other.SourceId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CharacterAbilitySourceKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, SourceId);
        public override string ToString() => $"{Kind}:{SourceId}";

        private static string NormalizeSourceId(string sourceId)
        {
            return string.IsNullOrWhiteSpace(sourceId) ? "default" : sourceId;
        }
    }

    /// <summary>
    /// 运行时能力来源条目。
    /// 它记录某个正式能力编号来自哪条来源以及当前叠加层数。
    /// </summary>
    public readonly struct CharacterAbilitySourceRuntimeEntry
    {
        public CharacterAbilitySourceRuntimeEntry(
            int formalGasAbilityCode,
            CharacterAbilitySourceKey source,
            int stackCount)
        {
            FormalGasAbilityCode = Math.Max(0, formalGasAbilityCode);
            Source = source;
            StackCount = stackCount;
        }

        public int FormalGasAbilityCode { get; }
        public CharacterAbilitySourceKey Source { get; }
        public int StackCount { get; }
        public bool HasFormalGasAbility => FormalGasAbilityCode > 0;

    }

    /// <summary>
    /// 存档中的能力来源记录。
    /// 字段保持简单可序列化，读取后再恢复为运行时来源键和叠加状态。
    /// </summary>
    [Serializable]
    public class CharacterAbilitySourceData
    {
        public int formalGasAbilityCode;
        public ECharacterAbilitySourceKind sourceKind;
        public string sourceId;
        public int stackCount;
    }

    /// <summary>
    /// 角色尝试释放能力后的只读结果。
    /// UI、输入和自动化验证用它判断是技能检查失败，还是某个正式能力被成功接收。
    /// </summary>
    public readonly struct CharacterAbilityFireResult
    {
        public CharacterAbilityFireResult(EAbilityFireCheckResult result, int formalGasAbilityCode)
        {
            Result = result;
            FormalGasAbilityCode = Math.Max(0, formalGasAbilityCode);
        }

        public EAbilityFireCheckResult Result { get; }
        public int FormalGasAbilityCode { get; }
        public bool HasFormalGasAbility => FormalGasAbilityCode > 0;
        public bool HasAbilitySource => HasFormalGasAbility;
    }

    /// <summary>
    /// 主动技能槽的展示快照。
    /// 它延迟解析图标和文案，避免 UI 直接持有角色内部能力实例。
    /// </summary>
    public readonly struct CharacterEquippedAbilitySlotView
    {
        public CharacterEquippedAbilitySlotView(int slotIndex, int formalGasAbilityCode)
        {
            SlotIndex = slotIndex;
            FormalGasAbilityCode = formalGasAbilityCode;
        }

        public int SlotIndex { get; }
        public int FormalGasAbilityCode { get; }
        public bool HasFormalGasAbility => FormalGasAbilityCode > 0;
        public bool HasDisplaySource => HasFormalGasAbility;

        public Sprite Icon
        {
            get
            {
                if (HasFormalGasAbility &&
                    TryResolveFormalRuntimeConfig(out FormalGasAbilityRuntimeConfig config) &&
                    config.TryLoadIcon(out Sprite icon))
                {
                    return icon;
                }

                return null;
            }
        }

        public string DisplayName
        {
            get
            {
                if (HasFormalGasAbility &&
                    FormalGasAbilityIdentityResolver.TryResolveAbilityIdentity(
                    FormalGasAbilityCode,
                    out FormalGasAbilityIdentity identity))
                {
                    return identity.DisplayName;
                }

                return HasFormalGasAbility ? $"EX-GAS Ability {FormalGasAbilityCode}" : string.Empty;
            }
        }

        public string Description
        {
            get
            {
                if (HasFormalGasAbility &&
                    FormalGasAbilityIdentityResolver.TryResolveAbilityIdentity(
                    FormalGasAbilityCode,
                    out FormalGasAbilityIdentity identity))
                {
                    return identity.Description;
                }

                return string.Empty;
            }
        }

        public bool TryResolveFormalRuntimeConfig(out FormalGasAbilityRuntimeConfig config)
        {
            config = default;
            return FormalGasAbilityCode > 0 &&
                FormalGasAbilityRuntimeConfigResolver.TryResolveRuntimeConfig(FormalGasAbilityCode, out config);
        }
    }

    /// <summary>
    /// 能力菜单中的可装备条目。
    /// 它只暴露 UI 需要的正式能力编号、图标、名称和描述，不暴露能力来源容器。
    /// </summary>
    public readonly struct CharacterAbilityMenuEntry
    {
        public CharacterAbilityMenuEntry(int formalGasAbilityCode)
        {
            FormalGasAbilityCode = formalGasAbilityCode;
        }

        public int FormalGasAbilityCode { get; }
        public bool HasFormalGasAbility => FormalGasAbilityCode > 0;
        public bool HasDisplaySource => HasFormalGasAbility;
        public bool CanEquipToActiveSlot => HasFormalGasAbility;

        public Sprite Icon
        {
            get
            {
                if (HasFormalGasAbility &&
                    FormalGasAbilityRuntimeConfigResolver.TryResolveRuntimeConfig(FormalGasAbilityCode, out FormalGasAbilityRuntimeConfig config) &&
                    config.TryLoadIcon(out Sprite icon))
                {
                    return icon;
                }

                return null;
            }
        }

        public string DisplayName
        {
            get
            {
                if (HasFormalGasAbility &&
                    FormalGasAbilityIdentityResolver.TryResolveAbilityIdentity(
                        FormalGasAbilityCode,
                        out FormalGasAbilityIdentity identity) &&
                    !string.IsNullOrWhiteSpace(identity.DisplayName))
                {
                    return identity.DisplayName;
                }

                return HasFormalGasAbility ? $"EX-GAS Ability {FormalGasAbilityCode}" : string.Empty;
            }
        }

        public string Description
        {
            get
            {
                if (HasFormalGasAbility &&
                    FormalGasAbilityIdentityResolver.TryResolveAbilityIdentity(
                        FormalGasAbilityCode,
                        out FormalGasAbilityIdentity identity))
                {
                    return identity.Description;
                }

                return string.Empty;
            }
        }

        public void GenerateAdditionalDescriptionLines(List<AbilityDescriptionLine> lines)
        {
            if (lines == null)
            {
                return;
            }

            if (HasFormalGasAbility)
            {
                FormalGasAbilityDescriptionResolver.TryAppendFormalDamageLines(FormalGasAbilityCode, lines);
            }
        }
    }

    /// <summary>
    /// 角色持久化数据块。
    /// 它承接 Movable 的基础保存内容，并追加等级、属性、能力来源和持续效果恢复数据。
    /// </summary>
    [Serializable]
    public class CharacterBaseDataBlock : MovableDataBlock
    {
        public int level;
        /// <summary>
        /// ASC 基础值快照只用于存档恢复，不参与运行时属性查询或效果重算。
        /// </summary>
        public CharacterAttributeSnapshot attributes;
        public DatabaseEntryReference<CharacterAlterationRule>[] activeAlterationRules;
        public CharacterAbilityRuntimeStateData[] abilityRuntimeStates;
        public CharacterAbilitySourceData[] abilitySources;
        public CharacterAbilitySourceData[] abilitySuppressions;
    }

    /// <summary>
    /// 角色局部运行时快照。
    /// 它只服务能力 extra state 等局部恢复，不再夹带 PersistableDataBlock.info 这类持久化身份壳。
    /// </summary>
    [Serializable]
    public class CharacterRuntimeStateData
    {
        public string identifier;
        public EPersistableObjectState state;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector2 lookAtDirection;
        [SerializeReference, SubclassSelector] public IControllerDataBlock controllerData;
        public int level;
        public CharacterAttributeSnapshot attributes;
        public DatabaseEntryReference<CharacterAlterationRule>[] activeAlterationRules;
        public CharacterAbilityRuntimeStateData[] abilityRuntimeStates;
        public CharacterAbilitySourceData[] abilitySources;
        public CharacterAbilitySourceData[] abilitySuppressions;
    }

    /// <summary>
    /// 正式能力恢复快照。
    /// 顶层只保存稳定能力表引用、通用冷却和正式 extra state；
    /// 能力运行时恢复统一走这一份正式状态。
    /// </summary>
    [Serializable]
    public class CharacterAbilityRuntimeStateData
    {
        public int formalGasAbilityCode;
        public EPersistableObjectState state;
        public float remainingCooldownTimer;
        [SerializeReference, SubclassSelector] public AbilityRuntimeExtraState extraRuntimeState;
    }

    /// <summary>
    /// 主动能力展示快照。
    /// UI 只读这份正式查询结果，不直接依赖角色内部能力实例集合。
    /// </summary>
    public readonly struct CharacterAbilityCooldownSnapshot
    {
        public CharacterAbilityCooldownSnapshot(int formalGasAbilityCode, float remainingCooldown, float cooldown)
        {
            FormalGasAbilityCode = Math.Max(0, formalGasAbilityCode);
            RemainingCooldown = remainingCooldown;
            Cooldown = cooldown;
        }

        public int FormalGasAbilityCode { get; }
        public bool HasFormalGasAbility => FormalGasAbilityCode > 0;
        public float RemainingCooldown { get; }
        public float Cooldown { get; }
    }

}

