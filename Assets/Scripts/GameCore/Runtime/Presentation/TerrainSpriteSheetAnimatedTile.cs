using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyWord.GameCore
{
    [CreateAssetMenu(
        fileName = "地表动画瓦片-",
        menuName = "GameCore/表现/序列帧地表瓦片")]
    public sealed class TerrainSpriteSheetAnimatedTile : TileBase
    {
        [SerializeField] private Texture2D m_texture = null;
        [SerializeField] private Vector2Int m_framePixelSize = new(32, 32);
        [SerializeField, Min(1.0f)] private float m_pixelsPerUnit = 32.0f;
        [SerializeField, Min(0.01f)] private float m_minSpeed = 8.0f;
        [SerializeField, Min(0.01f)] private float m_maxSpeed = 8.0f;
        [SerializeField, Min(0)] private int m_animationStartFrame = 0;
        [SerializeField] private Tile.ColliderType m_tileColliderType =
            Tile.ColliderType.None;

        [NonSerialized] private Texture2D m_cachedTexture;
        [NonSerialized] private Vector2Int m_cachedFramePixelSize;
        [NonSerialized] private float m_cachedPixelsPerUnit;
        [NonSerialized] private Sprite[] m_cachedSprites;

        public override void GetTileData(
            Vector3Int position,
            ITilemap tilemap,
            ref TileData tileData)
        {
            Sprite[] sprites = GetAnimatedSprites();
            tileData.transform = Matrix4x4.identity;
            tileData.color = Color.white;
            tileData.sprite = sprites.Length > 0 ? sprites[0] : null;
            tileData.colliderType = m_tileColliderType;
        }

        public override bool GetTileAnimationData(
            Vector3Int position,
            ITilemap tilemap,
            ref TileAnimationData tileAnimationData)
        {
            Sprite[] sprites = GetAnimatedSprites();
            if (sprites.Length == 0)
            {
                return false;
            }

            float minSpeed = Mathf.Max(0.01f, m_minSpeed);
            float maxSpeed = Mathf.Max(minSpeed, m_maxSpeed);
            tileAnimationData.animatedSprites = sprites;
            tileAnimationData.animationSpeed = Mathf.Approximately(minSpeed, maxSpeed)
                ? minSpeed
                : UnityEngine.Random.Range(minSpeed, maxSpeed);
            tileAnimationData.animationStartTime = ResolveAnimationStartTime(tilemap);
            return true;
        }

        private float ResolveAnimationStartTime(ITilemap tilemap)
        {
            if (m_animationStartFrame <= 0)
            {
                return 0.0f;
            }

            Tilemap tilemapComponent = tilemap.GetComponent<Tilemap>();
            if (tilemapComponent == null ||
                tilemapComponent.animationFrameRate <= 0.0f)
            {
                return 0.0f;
            }

            int frameIndex = Mathf.Clamp(
                m_animationStartFrame - 1,
                0,
                Mathf.Max(0, GetAnimatedSprites().Length - 1));
            return frameIndex / tilemapComponent.animationFrameRate;
        }

        private Sprite[] GetAnimatedSprites()
        {
            Vector2Int framePixelSize = new(
                Mathf.Max(1, m_framePixelSize.x),
                Mathf.Max(1, m_framePixelSize.y));
            float pixelsPerUnit = Mathf.Max(1.0f, m_pixelsPerUnit);

            if (m_cachedSprites != null &&
                m_cachedTexture == m_texture &&
                m_cachedFramePixelSize == framePixelSize &&
                Mathf.Approximately(m_cachedPixelsPerUnit, pixelsPerUnit))
            {
                return m_cachedSprites;
            }

            m_cachedTexture = m_texture;
            m_cachedFramePixelSize = framePixelSize;
            m_cachedPixelsPerUnit = pixelsPerUnit;

            if (m_texture == null)
            {
                m_cachedSprites = Array.Empty<Sprite>();
                return m_cachedSprites;
            }

            int columns = m_texture.width / framePixelSize.x;
            int rows = m_texture.height / framePixelSize.y;
            int spriteCount = Mathf.Max(0, columns * rows);
            if (spriteCount == 0)
            {
                m_cachedSprites = Array.Empty<Sprite>();
                return m_cachedSprites;
            }

            Sprite[] sprites = new Sprite[spriteCount];
            int index = 0;
            for (int row = rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < columns; column++)
                {
                    Rect rect = new(
                        column * framePixelSize.x,
                        row * framePixelSize.y,
                        framePixelSize.x,
                        framePixelSize.y);
                    Sprite sprite = Sprite.Create(
                        m_texture,
                        rect,
                        new Vector2(0.5f, 0.5f),
                        pixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect);
                    sprite.name = $"{m_texture.name}_{index:00}";
                    sprite.hideFlags = HideFlags.HideAndDontSave;
                    sprites[index] = sprite;
                    index++;
                }
            }

            m_cachedSprites = sprites;
            return m_cachedSprites;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_cachedTexture = null;
            m_cachedSprites = null;
        }
#endif
    }
}
