using UnityEngine;
using UnityEngine.Serialization;

namespace GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.UI + nameof(NavigationCursorStyle))]
    public class NavigationCursorStyle : DatabaseEntry
    {
        [SerializeField, FormerlySerializedAs("sprite")]
        private Sprite m_sprite = null;

        [SerializeField, FormerlySerializedAs("color")]
        private Color m_color = Color.white;

        [SerializeField, FormerlySerializedAs("positionOffset")]
        private Vector2 m_positionOffset = Vector2.zero;

        [SerializeField, FormerlySerializedAs("sizeOffset")]
        private Vector2 m_sizeOffset = Vector2.zero;

        public Sprite sprite => m_sprite;
        public Color color => m_color;
        public Vector2 positionOffset => m_positionOffset;
        public Vector2 sizeOffset => m_sizeOffset;
    }
}
