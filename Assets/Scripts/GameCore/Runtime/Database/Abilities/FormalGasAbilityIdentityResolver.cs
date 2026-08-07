namespace GameCore
{
    /// <summary>
    /// Formal GAS 技能的展示身份信息，供 UI 和日志从技能编码解析名称与描述。
    /// </summary>
    public readonly struct FormalGasAbilityIdentity
    {
        public FormalGasAbilityIdentity(string displayName, string description)
        {
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string DisplayName { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Formal GAS 技能身份解析门面，由数据层注册解析器，运行时只按技能编码查询。
    /// </summary>
    public static class FormalGasAbilityIdentityResolver
    {
        public delegate bool TryResolveAbilityIdentityHandler(
            int abilityCode,
            out FormalGasAbilityIdentity identity);

        private static TryResolveAbilityIdentityHandler s_handler;

        public static void RegisterTryResolveAbilityIdentityHandler(TryResolveAbilityIdentityHandler handler)
        {
            s_handler = handler;
        }

        public static bool TryResolveAbilityIdentity(
            int abilityCode,
            out FormalGasAbilityIdentity identity)
        {
            if (abilityCode <= 0 || s_handler == null)
            {
                identity = default;
                return false;
            }

            return s_handler(abilityCode, out identity);
        }
    }
}
