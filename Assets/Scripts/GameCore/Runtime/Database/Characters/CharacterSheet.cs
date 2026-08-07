using System;
using System.Collections.Generic;
using System.Linq;
using MackySoft.SerializeReferenceExtensions;
using UnityEngine;
using UnityEngine.Serialization;
using azixMcAze.SerializableDictionary;

namespace GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.Characters + nameof(CharacterSheet))]
    public sealed class CharacterSheet : DatabaseEntry, INameable
    {
        [Header("Identity")]
        [SerializeField] private EAlignment m_alignment = EAlignment.Default;
        [SerializeField] private string m_displayName = string.Empty;
        [FormerlySerializedAs("m_abilitiesPerLevel")]
        [SerializeField] private SerializableDictionary<int, int> m_formalGasAbilitiesPerLevel;

        [Header("Audio")]
        [SerializeField] private AudioClipResolver m_hitAudio;
        [SerializeField] private AudioClipResolver m_deathAudio;

        [Header("Feedbacks")]
        [SerializeField] private GameplayFeedbackSet m_feedbacks = new();

        [Header("Stats")]
        [SerializeField, FormerlySerializedAs("baseStats")]
        private Stats m_baseStats = new();

        [SerializeField] private bool m_useLevelScaledStats = false;
        [SerializeField] private LevelScaledStats m_levelScaledStats = new();

        [SerializeReference, SubclassSelector] private ICommand m_executeOnDeath;

        public EAlignment alignment => m_alignment;
        public string displayName => DisplayNameUtils.GetNameOrDefault(this, m_displayName);
        public AudioClipResolver hitAudio => m_hitAudio;
        public AudioClipResolver deathAudio => m_deathAudio;
        public GameplayFeedbackSet feedbacks => m_feedbacks ??= new GameplayFeedbackSet();
        public Stats baseStats => m_baseStats?.Clone() ?? new Stats();

        public Stats GetStatsAtLevel(int level)
        {
            if (!m_useLevelScaledStats)
            {
                return baseStats;
            }

            return ((m_levelScaledStats ??= new LevelScaledStats())[level])?.Clone() ?? new Stats();
        }

        public void ExecuteOnDeath(GameCommandContext context)
        {
            m_executeOnDeath.ExecuteFireAndReport(context, nameof(CharacterSheet), this);
        }

        public int[] GetAvailableFormalGasAbilitiesAtLevel(int level)
        {
            return CreateDistinctFormalGasAbilityCodes(
                m_formalGasAbilitiesPerLevel != null
                    ? m_formalGasAbilitiesPerLevel
                        .Where(keyValuePair => keyValuePair.Key > 0 && keyValuePair.Value <= level)
                        .Select(keyValuePair => keyValuePair.Key)
                    : Array.Empty<int>());
        }

        public int[] GetFormalGasAbilitiesUnlockedAtLevel(int level)
        {
            return CreateDistinctFormalGasAbilityCodes(
                m_formalGasAbilitiesPerLevel != null
                    ? m_formalGasAbilitiesPerLevel
                        .Where(keyValuePair => keyValuePair.Key > 0 && keyValuePair.Value == level)
                        .Select(keyValuePair => keyValuePair.Key)
                    : Array.Empty<int>());
        }

        private static int[] CreateDistinctFormalGasAbilityCodes(params IEnumerable<int>[] sources)
        {
            List<int> result = new();
            foreach (IEnumerable<int> source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                foreach (int formalGasAbilityCode in source)
                {
                    if (formalGasAbilityCode > 0 && !result.Contains(formalGasAbilityCode))
                    {
                        result.Add(formalGasAbilityCode);
                    }
                }
            }

            return result.ToArray();
        }
    }
}
