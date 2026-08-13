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
            float nonNegativeDefense = math.max(0.0f, defense);
            return (int)math.floor(damage * (100.0f / (100.0f + nonNegativeDefense)));
        }

        internal static int CalculateCriticalDamage(int damage, float criticalMultiplierPercent)
        {
            return (int)math.round(damage * math.max(0.0f, criticalMultiplierPercent) / 100.0f);
        }

        internal static bool EvaluateCritical(float criticalChancePercent, float rollPercent)
        {
            return rollPercent < math.clamp(criticalChancePercent, 0.0f, 100.0f);
        }

        internal static bool EvaluateMiss(float accuracy, float dodge, float hitRollPercent)
        {
            float nonNegativeAccuracy = math.max(0.0f, accuracy);
            float nonNegativeDodge = math.max(0.0f, dodge);
            float denominator = nonNegativeAccuracy + nonNegativeDodge;
            float hitChance = denominator <= 0.0f
                ? 0.0f
                : nonNegativeAccuracy * 100.0f / denominator;
            return hitRollPercent >= hitChance;
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
                    missBehavior = input.MissBehavior,
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
            return SolveDamageInput(defender.CreateCombatStatSnapshot(), output);
        }

        internal static DamageInputDescriptor SolveDamageInput(
            AbilitySystemCell defender,
            DamageOutputDescriptor output)
        {
            if (defender == null)
            {
                throw new ArgumentNullException(nameof(defender));
            }

            return SolveDamageInput(CreateCombatStatSnapshot(defender), output);
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
            bool canCriticalHit =
                GameManager.Config.canCriticalHit &&
                input.CriticalBehavior != EResolutionBehavior.Never;
            bool criticalHit =
                canCriticalHit &&
                (input.CriticalBehavior == EResolutionBehavior.Always ||
                 EvaluateCritical(attackerCombatStats.CriticalChance, rolls.CriticalRollPercent));

            return new DamageOutputDescriptor
            {
                source = source,
                damage = criticalHit
                    ? CalculateCriticalDamage(damage, attackerCombatStats.CriticalMultiplier)
                    : damage,
                type = input.DamageType,
                flags = criticalHit ? EDamageFlag.Critical : EDamageFlag.None,
                rolls = rolls,
                missBehavior = input.MissBehavior,
                ignoreDefense = input.IgnoreDefense,
                silent = input.Silent
            };
        }

        private static DamageInputDescriptor SolveDamageInput(
            CombatStatSnapshot defenderCombatStats,
            DamageOutputDescriptor output)
        {
            if (!output.TryGetSourceCombatStatSnapshot(out CombatStatSnapshot attackerCombatStats))
            {
                return new DamageInputDescriptor
                {
                    source = output.source,
                    damage = output.damage,
                    flags = output.flags,
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

            return new DamageInputDescriptor
            {
                source = output.source,
                damage = missed ? 0 : damage,
                flags = missed ? output.flags | EDamageFlag.Miss : output.flags,
                silent = output.silent
            };
        }
    }
}
