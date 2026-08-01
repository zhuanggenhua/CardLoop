using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 使用 EX-GAS Timeline 执行主动技能的通用项目侧运行桥。
    /// 技能时序、消耗、冷却和效果仍由 EX-GAS 数据与任务拥有。
    /// </summary>
    public class TimelineActiveAbility : ActiveAbilityBase, IActionInterruptReceiver
    {
        protected override void ExecuteAbilityUse()
        {
        }

        protected override FormalAbilityInputGateSettings ResolveFormalAbilityInputGateSettings()
        {
            FormalAbilityInputGateConfig inputGate = ResolveFormalInputGateSettings();
            if (usesFormalGasAbility &&
                FormalGasAbilityTimelineExecutionResolver.TryResolveTimelineExecutionSettings(
                    formalGasAbilityCode,
                    out FormalGasTimelineExecutionSettings timelineSettings))
            {
                return FormalAbilityInputGateSettings.CreateTimelineGate(
                    inputGate,
                    timelineSettings.delayBeforeUse,
                    timelineSettings.timeBetweenUses);
            }

            Debug.LogError(
                $"{name} 没有可用的 EX-GAS Timeline 执行配置。",
                this);
            return FormalAbilityInputGateSettings.CreateTimelineGate(
                inputGate,
                0.0f,
                0.0f);
        }

        protected override GameplayFeedbackSet ResolveGameplayFeedbacks()
        {
            return new GameplayFeedbackSet();
        }

        public void OnActionInterrupted()
        {
            Interrupt();
        }

        private FormalAbilityInputGateConfig ResolveFormalInputGateSettings()
        {
            if (TryGetFormalGasRuntimeConfig(out FormalGasAbilityRuntimeConfig config))
            {
                return config.InputGate;
            }

            Debug.LogWarning(
                $"EX-GAS Ability {formalGasAbilityCode} 已绑定 EX-GAS Timeline，但缺少 exgas.abilityGameCore 输入配置；本次仅使用空输入兜底。",
                this);
            return new FormalAbilityInputGateConfig();
        }
    }
}
