using UnityEngine;
using UnityEngine.Serialization;

namespace GameCore
{
    public partial class CharacterActor
    {
        [Header("Character Settings")]
        [FormerlySerializedAs("m_characterSheet")]
        [SerializeField] private CharacterSheet m_sheet = null;

        public override CharacterSheet characterSheet => m_sheet;
        public CharacterSheet sheet => m_sheet;
    }
}
