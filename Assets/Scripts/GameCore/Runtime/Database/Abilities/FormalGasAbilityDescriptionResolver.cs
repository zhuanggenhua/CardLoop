using System;
using System.Collections.Generic;

namespace GameCore
{
    public static class FormalGasAbilityDescriptionResolver
    {
        public delegate bool AppendFormalDamageLinesHandler(int abilityCode, List<AbilityDescriptionLine> lines);

        private static AppendFormalDamageLinesHandler s_appendFormalDamageLines;

        public static void RegisterAppendFormalDamageLinesHandler(AppendFormalDamageLinesHandler handler)
        {
            s_appendFormalDamageLines = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public static bool TryAppendFormalDamageLines(int abilityCode, List<AbilityDescriptionLine> lines)
        {
            if (abilityCode <= 0 || lines == null)
            {
                return false;
            }

            return s_appendFormalDamageLines?.Invoke(abilityCode, lines) == true;
        }
    }
}
