using UnityEngine;

namespace FantasyWord.GameCore
{
    public static class DisplayNameUtils
    {
        public static string GetNameOrDefault(Object caller, string name)
        {
            return !string.IsNullOrWhiteSpace(name) ? name : caller.name;
        }
    }
}

