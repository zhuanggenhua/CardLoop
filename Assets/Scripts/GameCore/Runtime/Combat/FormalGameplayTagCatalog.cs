using System;
using GAS.General;

namespace GameCore
{
    /// <summary>
    /// 当前项目对 EX-GAS 2.0 正式标签码的 GameCore 投影。
    /// GameCore 不能直接引用生成程序集，因为生成程序集需要反向引用 GameCore 的正式扩展类型。
    /// </summary>
    public static class FormalGameplayTagCatalog
    {
        private const string GeneratedTagTypeName = "GAS.Runtime.XTag";

        public static readonly int EventAttackingTagCode =
            ResolveRequiredGeneratedTagCode("Event_Attacking", "Event.Attacking");

        public static readonly FormalGameplayTagDefinition AttackingEvent =
            new("event.attacking", "正在攻击", EventAttackingTagCode);

        public static readonly FormalGameplayTagDefinition ControlEffect =
            new("effect.debuff.control", "控制效果", 0);

        public static readonly FormalGameplayTagDefinition StunControlEffect =
            new("effect.debuff.control.stun", "眩晕", 0);

        public static readonly FormalGameplayTagDefinition SilenceControlEffect =
            new("effect.debuff.control.silence", "沉默", 0);

        public static readonly FormalGameplayTagDefinition RootControlEffect =
            new("effect.debuff.control.root", "定身", 0);

        public static bool HasRegisteredControlTags =>
            StunControlEffect.TagCode > 0 ||
            SilenceControlEffect.TagCode > 0 ||
            RootControlEffect.TagCode > 0;

        private static int ResolveRequiredGeneratedTagCode(string generatedMemberName, string stableName)
        {
            if (!ReflectionHelper.MemberExists(GeneratedTagTypeName, generatedMemberName))
            {
                throw new InvalidOperationException(
                    $"缺少 EX-GAS 生成标签：{stableName} ({GeneratedTagTypeName}.{generatedMemberName})。请先从 EX-GAS 标签表重新生成运行时代码。");
            }

            object value = ReflectionHelper.GetStaticFieldOrProperty(GeneratedTagTypeName, generatedMemberName);
            if (value is int tagCode && tagCode > 0)
            {
                return tagCode;
            }

            throw new InvalidOperationException(
                $"EX-GAS 生成标签无效：{stableName} ({GeneratedTagTypeName}.{generatedMemberName}) 不是有效正整数。");
        }
    }

    public readonly struct FormalGameplayTagDefinition
    {
        public FormalGameplayTagDefinition(string stableId, string displayName, int tagCode)
        {
            StableId = stableId ?? throw new ArgumentNullException(nameof(stableId));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            TagCode = tagCode;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public int TagCode { get; }
    }
}
