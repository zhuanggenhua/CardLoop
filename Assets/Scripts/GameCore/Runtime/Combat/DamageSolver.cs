using Unity.Mathematics;

namespace FantasyWord.GameCore
{
    public static class DamageSolver
    {
        public static int CalculateDamageOut(int flatDamages, float scale, int stat)
        {
            return flatDamages + (int)math.round(stat * scale);
        }

        public static int CalculateDamageIn(int damage, int stat)
        {
            return (int)math.floor(damage * (100.0f / (100.0f + stat)));
        }

        public static int CalculateCriticalDamage(int damage)
        {
            return damage * 2;
        }

        public static int CalculateMissDamage(int damage)
        {
            return 0;
        }

        public static bool EvaluateCritical(int luck)
        {
            return UnityEngine.Random.Range(1, 100) < luck;
        }

        public static bool EvaluateMiss(int attackerAgility, int defenderAgility)
        {
            float missChance = math.clamp((int)math.floor((90.0f + attackerAgility) * (100.0f / (100.0f + defenderAgility))), 0, 100);
            return UnityEngine.Random.Range(1, 100) > missChance;
        }

        public static DamageOutputDescriptor SolveDamageOutput(CharacterBase attacker, DamageDescriptor input)
        {
            if (attacker)
            {
                CombatStatSnapshot attackerCombatStats = attacker.CreateCombatStatSnapshot();
                int damage = CalculateDamageOut(
                    input.flatDamages,
                    input.scalingFactor,
                    attackerCombatStats.GetOffensiveStat(input.damageType));

                bool canCriticalHit =
                    GameManager.Config.canCriticalHit &&
                    input.criticalBehavior != EResolutionBehavior.Never;

                bool criticalHit =
                    canCriticalHit &&
                    (input.criticalBehavior == EResolutionBehavior.Always || EvaluateCritical(attackerCombatStats.Luck));

                return new DamageOutputDescriptor
                {
                    source = CharacterDamageSource.Create(attacker),
                    damage = criticalHit ? CalculateCriticalDamage(damage) : damage,
                    type = input.damageType,
                    flags = criticalHit ? EDamageFlag.Critical : EDamageFlag.None,
                    missBehavior = input.missBehavior,
                    ignoreDefense = input.ignoreDefense,
                    silent = input.silent
                };
            }
            else
            {
                return new DamageOutputDescriptor
                {
                    source = new UnknownDamageSource(),
                    damage = input.flatDamages,
                    type = input.damageType,
                    flags = EDamageFlag.None,
                    silent = input.silent
                };
            }
        }

        public static DamageInputDescriptor SolveDamageInput(CharacterBase defender, DamageOutputDescriptor output)
        {
            if (output.TryGetSourceCombatStatSnapshot(out CombatStatSnapshot attackerCombatStats))
            {
                CombatStatSnapshot defenderCombatStats = defender.CreateCombatStatSnapshot();
                int damage = CalculateDamageIn(
                    output.damage,
                    output.ignoreDefense ? 0 : defenderCombatStats.GetDefensiveStat(output.type)
                );

                bool canMiss =
                    GameManager.Config.canMissHit &&
                    output.missBehavior != EResolutionBehavior.Never;

                bool missed =
                    canMiss &&
                    (output.missBehavior == EResolutionBehavior.Always || EvaluateMiss(attackerCombatStats.Agility, defenderCombatStats.Agility));

                return new DamageInputDescriptor
                {
                    source = output.source,
                    damage = missed ? CalculateMissDamage(damage) : damage,
                    flags = missed ? output.flags | EDamageFlag.Miss : output.flags,
                    silent = output.silent
                };
            }
            else
            {
                return new DamageInputDescriptor
                {
                    source = output.source,
                    damage = output.damage,
                    flags = output.flags,
                    silent = output.silent
                };
            }
        }
    }
}

