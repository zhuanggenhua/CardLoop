using azixMcAze.SerializableDictionary;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FantasyWord.GameCore
{
    public partial class GameConfig
    {
        public const string DefaultAssetPath = "Assets/GameData/GameCore/GameConfig.asset";

        [Header("Game Terms")]
        [SerializeField] private SerializableDictionary<string, TermDefinition> m_gameTerms = new();

        [Header("Game Terms Bindings (Advanced Settings)")]
        [SerializeField] private SerializableDictionary<EStat, string> m_statTermsBinding = new();
        [SerializeField] private SerializableDictionary<EStat, string> m_statIncreaseTermsBinding = new();
        [SerializeField] private SerializableDictionary<EStat, string> m_statDecreaseTermsBinding = new();
        [SerializeField] private SerializableDictionary<EItemCategory, string> m_itemCategoryTermsBinding = new();
        [SerializeField] private SerializableDictionary<EDamageType, string> m_damageTypesBinding = new();
        [SerializeField] private SerializableDictionary<EAbilityType, string> m_abilityTypesBinding = new();

        private readonly TermDefinition m_defaultTermDefinition = new()
        {
            fullName = "[INVALID_FULLNAME]",
            shortName = "[INVALID_SHORTNAME]",
            description = "[INVALID_DESCRIPTION]",
            icon = null
        };

        public static bool TryGetActiveOrEditorDefault(out GameConfig config)
        {
            if (GameManager.Exists())
            {
                config = GameManager.Config;
                return config != null;
            }

#if UNITY_EDITOR
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(DefaultAssetPath);
            return config != null;
#else
            config = null;
            return false;
#endif
        }

        public static TermDefinition GetSafeTermDefinition(string termID)
        {
            if (TryGetActiveOrEditorDefault(out GameConfig config))
            {
                TermDefinition definition = config.GetTermDefinition(termID);
                if (IsResolvedTermDefinition(definition))
                {
                    return definition;
                }
            }

            return CreateFallbackTermDefinition(termID);
        }

        public static TermDefinition GetSafeTermDefinition(EDamageType type)
        {
            if (TryGetActiveOrEditorDefault(out GameConfig config))
            {
                TermDefinition definition = config.GetTermDefinition(type);
                if (IsResolvedTermDefinition(definition))
                {
                    return definition;
                }
            }

            return CreateFallbackTermDefinition(type);
        }

        private static bool IsResolvedTermDefinition(TermDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.shortName) == false &&
                   definition.shortName != "[INVALID_SHORTNAME]";
        }

        private static TermDefinition CreateFallbackTermDefinition(string termID)
        {
            string readableName = ResolveFallbackTermName(termID);
            return new TermDefinition
            {
                fullName = readableName,
                shortName = readableName,
                description = string.Empty,
                icon = null
            };
        }

        private static TermDefinition CreateFallbackTermDefinition(EDamageType type)
        {
            return CreateFallbackTermDefinition(type switch
            {
                EDamageType.Physical => "物理",
                EDamageType.Magical => "魔法",
                EDamageType.None => "无类型",
                _ => type.ToString()
            });
        }

        private static string ResolveFallbackTermName(string termID)
        {
            return termID switch
            {
                "flat_damage" => "固定伤害",
                "scaled_damage" => "属性缩放伤害",
                "remove_health" => "造成伤害",
                "mana_cost" => "法力消耗",
                "cooldown" => "冷却",
                _ when string.IsNullOrWhiteSpace(termID) => "未命名术语",
                _ => termID
            };
        }

        public TermDefinition GetTermDefinition(string termID)
        {
            if (m_gameTerms.ContainsKey(termID))
            {
                return m_gameTerms[termID];
            }

            return m_defaultTermDefinition;
        }

        public TermDefinition GetTermDefinition(EStat stat)
        {
            if (m_statTermsBinding.ContainsKey(stat))
            {
                return GetTermDefinition(m_statTermsBinding[stat]);
            }

            return m_defaultTermDefinition;
        }

        public bool TryGetTermId(EStat stat, out string termId)
        {
            if (m_statTermsBinding.TryGetValue(stat, out termId) &&
                !string.IsNullOrWhiteSpace(termId))
            {
                return true;
            }

            termId = string.Empty;
            return false;
        }

        public TermDefinition GetStatIncreaseTermDefinition(EStat stat)
        {
            if (m_statIncreaseTermsBinding.ContainsKey(stat))
            {
                return GetTermDefinition(m_statIncreaseTermsBinding[stat]);
            }

            return m_defaultTermDefinition;
        }

        public bool TryGetStatIncreaseTermId(EStat stat, out string termId)
        {
            if (m_statIncreaseTermsBinding.TryGetValue(stat, out termId) &&
                !string.IsNullOrWhiteSpace(termId))
            {
                return true;
            }

            termId = string.Empty;
            return false;
        }

        public TermDefinition GetStatDecreaseTermDefinition(EStat stat)
        {
            if (m_statDecreaseTermsBinding.ContainsKey(stat))
            {
                return GetTermDefinition(m_statDecreaseTermsBinding[stat]);
            }

            return m_defaultTermDefinition;
        }

        public bool TryGetStatDecreaseTermId(EStat stat, out string termId)
        {
            if (m_statDecreaseTermsBinding.TryGetValue(stat, out termId) &&
                !string.IsNullOrWhiteSpace(termId))
            {
                return true;
            }

            termId = string.Empty;
            return false;
        }

        public TermDefinition GetTermDefinition(EItemCategory category)
        {
            if (m_itemCategoryTermsBinding.ContainsKey(category))
            {
                return GetTermDefinition(m_itemCategoryTermsBinding[category]);
            }

            return m_defaultTermDefinition;
        }

        public TermDefinition GetTermDefinition(EDamageType type)
        {
            if (m_damageTypesBinding.ContainsKey(type))
            {
                return GetTermDefinition(m_damageTypesBinding[type]);
            }

            return m_defaultTermDefinition;
        }

        public bool TryGetTermId(EDamageType type, out string termId)
        {
            if (m_damageTypesBinding.TryGetValue(type, out termId) &&
                !string.IsNullOrWhiteSpace(termId))
            {
                return true;
            }

            termId = string.Empty;
            return false;
        }

        public TermDefinition GetTermDefinition(EAbilityType abilityType)
        {
            if (m_abilityTypesBinding.ContainsKey(abilityType))
            {
                return GetTermDefinition(m_abilityTypesBinding[abilityType]);
            }

            return m_defaultTermDefinition;
        }
    }
}
