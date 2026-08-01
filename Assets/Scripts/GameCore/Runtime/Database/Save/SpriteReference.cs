using UnityEngine;
using UnityEngine.Serialization;

namespace FantasyWord.GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Save + nameof(SpriteReference))]
    public class SpriteReference : DatabaseEntry
    {
        [Header("References")]
        [SerializeField, FormerlySerializedAs("sprite")]
        private Sprite m_sprite;

        public Sprite sprite => m_sprite;
    }
}
