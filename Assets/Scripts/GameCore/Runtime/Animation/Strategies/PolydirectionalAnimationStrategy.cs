using System;
using UnityEngine;
using UnityEngine.U2D.Animation;
using azixMcAze.SerializableDictionary;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 多方向动画可配置的方向枚举；当前仍保留八向，具体资源可通过 flipSprite 复用。
    /// </summary>
    enum EAnimationDirection
    {
        Up,
        UpRight,
        Right,
        DownRight,
        Down,
        DownLeft,
        Left,
        UpLeft
    }

    /// <summary>
    /// 某个方向对应的 SpriteLibrary 覆盖配置，以及是否需要水平翻转。
    /// </summary>
    [Serializable]
    public struct AnimationDirectionOverride
    {
        [InspectorName("方向动画库")]
        [Tooltip("角色朝该方向时切换到的 SpriteLibraryAsset。")]
        public SpriteLibraryAsset spriteLibrary;

        [InspectorName("水平翻转")]
        [Tooltip("开启后使用同一动画库但水平翻转 SpriteRenderer，适合复用左右对称资源。")]
        public bool flipSprite;

        [InspectorName("匹配优先级")]
        [Tooltip("当两个方向与输入方向距离几乎相同时，优先级较高的方向胜出。")]
        public float priority;
    }

    /// <summary>
    /// 根据面朝方向切换 SpriteLibrary 的多方向动画策略，支持用优先级解决方向边界抖动。
    /// </summary>
    [Serializable]
    public class PolydirectionalAnimationStrategy : AAnimationStrategy
    {
        [Header("多方向动画设置")]
        [InspectorName("Sprite Library")]
        [Tooltip("运行时要切换 SpriteLibraryAsset 的 SpriteLibrary 组件。")]
        [SerializeField] protected SpriteLibrary m_spriteLibrary = null;

        [InspectorName("方向覆盖")]
        [Tooltip("每个方向对应的动画库和翻转策略；为空时保持默认动画库。")]
        [SerializeField] private SerializableDictionary<EAnimationDirection, AnimationDirectionOverride> m_animationDirectionOverrides = new();

        public override void Initialize()
        {
            base.Initialize();
            Debug.Assert(m_spriteRenderer, ErrorMessages.InspectorMissingComponentReference<SpriteLibrary>());
        }

        private AnimationDirectionOverride? GetAnimationOverride(Vector2 direction)
        {
            // Default to Top if the dictionary is empty
            if (m_animationDirectionOverrides.Count == 0)
            {
                return null;
            }

            // Normalize the direction to ensure consistency
            direction.Normalize();

            // Ignore the sign of direction.x
            float x = direction.x;
            float y = direction.y;

            // Thresholds for each direction
            EAnimationDirection bestDirection = EAnimationDirection.Up;
            float bestScore = float.MaxValue;
            float secondBestScore = float.MaxValue;
            EAnimationDirection secondBestDirection = EAnimationDirection.Up;

            foreach (var entry in m_animationDirectionOverrides)
            {
                Vector2 directionVector = DirectionToVector(entry.Key);
                float distance = Vector2.Distance(directionVector, new Vector2(x, y));

                if (distance < bestScore)
                {
                    secondBestScore = bestScore;
                    secondBestDirection = bestDirection;

                    bestScore = distance;
                    bestDirection = entry.Key;
                }
                else if (distance < secondBestScore)
                {
                    secondBestScore = distance;
                    secondBestDirection = entry.Key;
                }
            }

            // Check if the two closest directions are within epsilon
            if (Mathf.Abs(bestScore - secondBestScore) < Constants.Epsilon)
            {
                float highestPriority = m_animationDirectionOverrides[bestDirection].priority;
                float secondHighestPriority = m_animationDirectionOverrides[secondBestDirection].priority;

                // Choose the direction with the highest priority
                bestDirection = highestPriority >= secondHighestPriority ? bestDirection : secondBestDirection;
            }

            // Return the best-matched value
            return m_animationDirectionOverrides[bestDirection];
        }

        private Vector2 DirectionToVector(EAnimationDirection direction)
        {
            // Map each enum value to its corresponding normalized direction vector
            switch (direction)
            {
                case EAnimationDirection.Up: return Vector2.up;
                case EAnimationDirection.UpRight: return (Vector2.up + Vector2.right).normalized;
                case EAnimationDirection.Right: return Vector2.right;
                case EAnimationDirection.DownRight: return (Vector2.down + Vector2.right).normalized;
                case EAnimationDirection.Down: return Vector2.down;
                case EAnimationDirection.DownLeft: return (Vector2.down + Vector2.left).normalized;
                case EAnimationDirection.Left: return Vector2.left;
                case EAnimationDirection.UpLeft: return (Vector2.up + Vector2.left).normalized;
            }

            Debug.LogError("Invalid direction!");
            return Vector2.zero;
        }

        public override void SetLookAtDirection(Vector2 direction)
        {
            base.SetLookAtDirection(direction);

            if (m_spriteLibrary && m_animationDirectionOverrides.Count > 0)
            {
                AnimationDirectionOverride? animationDirectionOverride = GetAnimationOverride(direction);
                if (animationDirectionOverride != null)
                {
                    m_spriteLibrary.spriteLibraryAsset = animationDirectionOverride.Value.spriteLibrary;
                    m_spriteRenderer.flipX = animationDirectionOverride.Value.flipSprite;
                }
            }
        }
    }
}

