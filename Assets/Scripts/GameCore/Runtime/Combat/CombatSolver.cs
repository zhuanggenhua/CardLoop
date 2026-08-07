namespace GameCore
{
    public static class CombatSolver
    {
        public static bool IsJudiciousTarget(CharacterBase attacker, CharacterBase defender) => CanTarget(attacker, defender) && AreEnemies(attacker, defender);

        public static bool CanTarget(DamageOutputDescriptor damageOutput, CharacterBase defender)
        {
            if (damageOutput.TryGetSourceCharacter(out CharacterBase attacker))
            {
                return CanTarget(attacker, defender);
            }

            return true;
        }

        public static bool CanTarget(CharacterBase a, CharacterBase b)
        {
            return
                (!b.invincible || a == b) && // Can't target invincible characters unless targeting self
                (a == null || !a.dead) &&
                !b.dead;
        }

        public static bool AreAllies(CharacterBase a, CharacterBase b)
        {
            return a.currentAlignment == b.currentAlignment;
        }

        public static bool AreEnemies(CharacterBase a, CharacterBase b)
        {
            return a.currentAlignment != b.currentAlignment;
        }

        public static bool IsHostileTowards(CharacterBase a, CharacterBase b)
        {
            return
                AreEnemies(a, b) &&
                a.currentAlignment != EAlignment.Neutral &&
                b.currentAlignment != EAlignment.Neutral;
        }
    }
}

