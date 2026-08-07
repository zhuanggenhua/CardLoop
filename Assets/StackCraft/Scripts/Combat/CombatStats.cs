namespace CryingSnow.StackCraft
{
    public class CombatStats
    {
        public Stat MaxHealth { get; private set; }
        public Stat Attack { get; private set; }
        public Stat Defense { get; private set; }
        public Stat AttackSpeed { get; private set; }
        public Stat Accuracy { get; private set; }
        public Stat Dodge { get; private set; }
        public Stat CriticalChance { get; private set; }
        public Stat CriticalMultiplier { get; private set; }

        public CombatStats(float maxHealth, float attack, float defense, float attackSpeed,
                           float accuracy, float dodge, float criticalChance, float criticalMultiplier)
        {
            MaxHealth = new Stat(maxHealth);
            Attack = new Stat(attack);
            Defense = new Stat(defense);
            AttackSpeed = new Stat(attackSpeed);
            Accuracy = new Stat(accuracy);
            Dodge = new Stat(dodge);
            CriticalChance = new Stat(criticalChance);
            CriticalMultiplier = new Stat(criticalMultiplier);
        }

        /// <summary>
        /// Generates a multiline string containing the current base value of every combat statistic,
        /// formatted for display in a UI tooltip or debug log.
        /// </summary>
        /// <returns>A string with each combat stat and its value on a new line.</returns>
        public string GetFormattedStats()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.Append($"Max Health ({MaxHealth.Value.ToString()})\n");
            sb.Append($"Attack ({Attack.Value.ToString()})\n");
            sb.Append($"Defense ({Defense.Value.ToString()})\n");
            sb.Append($"Attack Speed ({AttackSpeed.Value.ToString()})\n");
            sb.Append($"Accuracy ({Accuracy.Value.ToString()})\n");
            sb.Append($"Dodge ({Dodge.Value.ToString()})\n");
            sb.Append($"Crit. Chance ({CriticalChance.Value.ToString()})\n");
            sb.Append($"Crit. Multiplier ({CriticalMultiplier.Value.ToString()})");

            return sb.ToString();
        }
    }
}
