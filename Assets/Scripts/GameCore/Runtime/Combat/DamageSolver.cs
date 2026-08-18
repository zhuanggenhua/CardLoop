using System;
using GAS.Runtime;
using Unity.Mathematics;

namespace GameCore
{
    /// <summary>
    /// 纯伤害解算器。属性来自命中快照，随机掷值由权威调用方显式传入。
    /// </summary>
    public static class DamageSolver
    {
        internal static int CalculateDamageOut(int flatDamages, float scale, float attack)
        {
            return flatDamages + (int)math.round(attack * scale);
        }

        internal static int CalculateDamageIn(int damage, float defense)
        {
            int defensePower = (int)math.round(defense);
            return math.max(1, damage - defensePower);
        }

        internal static int CalculateCriticalDamage(int damage, float criticalMultiplierPercent)
        {
            return (int)math.round(damage * math.max(0.0f, criticalMultiplierPercent) / 100.0f);
        }

        internal static int CalculateMatchupDamage(int damage, float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(multiplier),
                    multiplier,
                    "伤害克制倍率必须是非负有限数值。");
            }

            return (int)math.round(damage * multiplier);
        }

        internal static bool EvaluateCritical(float criticalChancePercent, float rollPercent)
        {
            return rollPercent <= math.clamp(criticalChancePercent, 0.0f, 100.0f);
        }

        internal static bool EvaluateMiss(float accuracy, float dodge, float hitRollPercent)
        {
            float hitChancePercent = math.clamp(accuracy - dodge, 5.0f, 95.0f);
            return hitRollPercent > hitChancePercent;
        }

        internal static DamageOutputDescriptor SolveDamageOutput(
            CharacterBase attacker,
            DamageDescriptor input,
            DamageResolutionRolls rolls)
        {
            if (!attacker)
            {
                return new DamageOutputDescriptor
                {
                    source = new UnknownDamageSource(),
                    damage = input.FlatDamages,
                    type = input.DamageType,
                    flags = EDamageFlag.None,
                    rolls = rolls,
                    criticalBehavior = input.CriticalBehavior,
                    missBehavior = input.MissBehavior,
                    matchupRules = input.MatchupRules,
                    ignoreDefense = input.IgnoreDefense,
                    silent = input.Silent
                };
            }

            CombatStatSnapshot attackerCombatStats = attacker.CreateCombatStatSnapshot();
            return SolveDamageOutput(
                CharacterDamageSource.Create(attacker),
                attackerCombatStats,
                input,
                rolls);
        }

        internal static DamageOutputDescriptor SolveDamageOutput(
            AbilitySystemCell attacker,
            DamageDescriptor input,
            DamageResolutionRolls rolls)
        {
            if (attacker == null)
            {
                return SolveDamageOutput((CharacterBase)null, input, rolls);
            }

            CombatStatSnapshot attackerCombatStats = CreateCombatStatSnapshot(attacker);
            return SolveDamageOutput(
                new AbilitySystemDamageSource(attacker),
                attackerCombatStats,
                input,
                rolls);
        }

        internal static DamageInputDescriptor SolveDamageInput(
            CharacterBase defender,
            DamageOutputDescriptor output)
        {
            if (!defender)
            {
                throw new ArgumentNullException(nameof(defender));
            }

            AbilitySystemCell defenderAbilitySystem = null;
            if (defender.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent))
            {
                defenderAbilitySystem = abilitySystemComponent.Cell;
            }

            return SolveDamageInput(defender.CreateCombatStatSnapshot(), defenderAbilitySystem, output);
        }

        internal static DamageInputDescriptor SolveDamageInput(
            AbilitySystemCell defender,
            DamageOutputDescriptor output)
        {
            if (defender == null)
            {
                throw new ArgumentNullException(nameof(defender));
            }

            return SolveDamageInput(CreateCombatStatSnapshot(defender), defender, output);
        }

        internal static CombatStatSnapshot CreateCombatStatSnapshot(AbilitySystemCell abilitySystem)
        {
            if (abilitySystem == null)
            {
                throw new ArgumentNullException(nameof(abilitySystem));
            }

            return new CombatStatSnapshot(
                abilitySystem.GetAttrCurrentValue(CharacterAttributes.SetCode, CharacterAttributes.Attack),
                abilitySystem.GetAttrCurrentValue(CharacterAttributes.SetCode, CharacterAttributes.Defense),
                abilitySystem.GetAttrCurrentValue(CharacterAttributes.SetCode, CharacterAttributes.Accuracy),
                abilitySystem.GetAttrCurrentValue(CharacterAttributes.SetCode, CharacterAttributes.Dodge),
                abilitySystem.GetAttrCurrentValue(CharacterAttributes.SetCode, CharacterAttributes.CriticalChance),
                abilitySystem.GetAttrCurrentValue(CharacterAttributes.SetCode, CharacterAttributes.CriticalMultiplier));
        }

        private static DamageOutputDescriptor SolveDamageOutput(
            IDamageSource source,
            CombatStatSnapshot attackerCombatStats,
            DamageDescriptor input,
            DamageResolutionRolls rolls)
        {
            int damage = CalculateDamageOut(
                input.FlatDamages,
                input.ScalingFactor,
                attackerCombatStats.GetOffensiveStat(input.DamageType));
            return new DamageOutputDescriptor
            {
                source = source,
                damage = damage,
                type = input.DamageType,
                flags = EDamageFlag.None,
                rolls = rolls,
                criticalBehavior = input.CriticalBehavior,
                missBehavior = input.MissBehavior,
                matchupRules = input.MatchupRules,
                ignoreDefense = input.IgnoreDefense,
                silent = input.Silent
            };
        }

        private static DamageInputDescriptor SolveDamageInput(
            CombatStatSnapshot defenderCombatStats,
            AbilitySystemCell defenderAbilitySystem,
            DamageOutputDescriptor output)
        {
            if (!output.TryGetSourceCombatStatSnapshot(out CombatStatSnapshot attackerCombatStats))
            {
                return new DamageInputDescriptor
                {
                    source = output.source,
                    damage = output.damage,
                    flags = output.flags,
                    matchupResult = DamageMatchupResult.None,
                    silent = output.silent
                };
            }

            int damage = CalculateDamageIn(
                output.damage,
                output.ignoreDefense ? 0.0f : defenderCombatStats.GetDefensiveStat(output.type));
            bool canMiss =
                GameManager.Config.canMissHit &&
                output.missBehavior != EResolutionBehavior.Never;
            bool missed =
                canMiss &&
                (output.missBehavior == EResolutionBehavior.Always ||
                     EvaluateMiss(
                         attackerCombatStats.Accuracy,
                         defenderCombatStats.Dodge,
                         output.rolls.HitRollPercent));
            DamageMatchupEvaluation matchup = missed
                ? default
                : EvaluateMatchup(output, defenderAbilitySystem);
            if (matchup.HasMatch)
            {
                damage = CalculateMatchupDamage(damage, matchup.Multiplier);
            }
            bool canCriticalHit =
                GameManager.Config.canCriticalHit &&
                output.criticalBehavior != EResolutionBehavior.Never;
            bool criticalHit =
                !missed &&
                canCriticalHit &&
                (output.criticalBehavior == EResolutionBehavior.Always ||
                 EvaluateCritical(attackerCombatStats.CriticalChance, output.rolls.CriticalRollPercent));

            return new DamageInputDescriptor
            {
                source = output.source,
                damage = missed
                    ? 0
                    : criticalHit
                        ? CalculateCriticalDamage(damage, attackerCombatStats.CriticalMultiplier)
                        : damage,
                flags = missed
                    ? EDamageFlag.Miss
                    : criticalHit
                        ? EDamageFlag.Critical
                        : EDamageFlag.None,
                matchupResult = missed ? DamageMatchupResult.None : matchup.Result,
                silent = output.silent
            };
        }

        private static DamageMatchupEvaluation EvaluateMatchup(
            DamageOutputDescriptor output,
            AbilitySystemCell defenderAbilitySystem)
        {
            if (output.matchupRules == null || output.matchupRules.Count == 0)
            {
                return default;
            }
            if (defenderAbilitySystem == null)
            {
                throw new InvalidOperationException(
                    "正式伤害配置了克制规则，但目标缺少 EX-GAS ASC，无法读取目标战斗标签。");
            }
            if (output.source == null ||
                !output.source.TryResolveAbilitySystem(out AbilitySystemCell sourceAbilitySystem) ||
                sourceAbilitySystem == null)
            {
                throw new InvalidOperationException(
                    "正式伤害配置了克制规则，但来源缺少 EX-GAS ASC，无法读取来源战斗标签。");
            }

            for (int ruleIndex = 0; ruleIndex < output.matchupRules.Count; ruleIndex++)
            {
                DamageMatchupRule rule = output.matchupRules[ruleIndex];
                if (sourceAbilitySystem.HasTag(rule.SourceRequiredTag) &&
                    defenderAbilitySystem.HasTag(rule.TargetRequiredTag))
                {
                    return new DamageMatchupEvaluation(rule.Multiplier, rule.Result);
                }
            }

            return default;
        }
    }
}
