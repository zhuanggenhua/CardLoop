using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using GAS.Runtime;
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

        [Header("属性")]
        [LabelText("角色基础值覆盖")]
        [Tooltip("只填写该角色不同于 EX-GAS FightUnit 默认值的属性；属性身份和钳制规则仍由 EX-GAS 表维护。")]
        [SerializeField] private CharacterAttributeOverride[] m_attributeOverrides =
            Array.Empty<CharacterAttributeOverride>();

        [SerializeReference, SubclassSelector] private ICommand m_executeOnDeath;

        public EAlignment alignment => m_alignment;
        public string displayName => DisplayNameUtils.GetNameOrDefault(this, m_displayName);
        public AudioClipResolver hitAudio => m_hitAudio;
        public AudioClipResolver deathAudio => m_deathAudio;
        public GameplayFeedbackSet feedbacks => m_feedbacks ??= new GameplayFeedbackSet();

        /// <summary>
        /// 创建该角色正式使用的 EX-GAS 属性集配置。
        /// 返回值继承表格默认值和钳制规则，仅应用当前角色的差异覆盖。
        /// </summary>
        public AttrSetConfig CreateAttributeSetConfig()
        {
            return CharacterAttributes.CreateConfig(m_attributeOverrides);
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
