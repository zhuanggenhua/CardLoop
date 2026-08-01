using UnityEngine;
using UnityEngine.Serialization;

namespace FantasyWord.GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Inns + nameof(Inn))]
    public class Inn : DatabaseEntry
    {
        [SerializeField, FormerlySerializedAs("price")]
        private int m_price;

        [SerializeField, FormerlySerializedAs("healAmount")]
        private int m_healAmount;

        [SerializeField, FormerlySerializedAs("manaRecoveredAmount")]
        private int m_manaRecoveredAmount;

        [SerializeField, FormerlySerializedAs("healingSound")]
        private AudioClipResolver m_healingSound;

        public int price => m_price;
        public int healAmount => m_healAmount;
        public int manaRecoveredAmount => m_manaRecoveredAmount;
        public AudioClipResolver healingSound => m_healingSound;
    }
}
