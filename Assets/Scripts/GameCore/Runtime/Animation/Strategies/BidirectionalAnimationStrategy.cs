using Sirenix.OdinInspector;
using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace GameCore
{
    /// <summary>
    /// 双向动画资源默认面朝方向，用于根据移动方向决定是否水平翻转。
    /// </summary>
    public enum EBidrectionalAnimationDirection
    {
        Right,
        Left
    }

    /// <summary>
    /// 只使用左右两个朝向的动画策略，通过 SpriteRenderer.flipX 派生相反方向。
    /// </summary>
    [MovedFrom(true, "GameCore.Strategies", "GameCore", null)]
    [Serializable]
    public class BidirectionalAnimationStrategy : AAnimationStrategy
    {
        [Header("双向动画设置")]
        [LabelText("默认朝向")]
        [Tooltip("原始 Sprite 资源默认面朝方向；运行时会根据目标方向决定是否水平翻转。")]
        [SerializeField] private EBidrectionalAnimationDirection m_defaultDirection = EBidrectionalAnimationDirection.Right;

        [LabelText("启用水平镜像")]
        [Tooltip("仅用于缺少真实左右方向素材的角色。使用四向 SpriteLibrary 的角色必须关闭。")]
        [SerializeField] private bool m_flipHorizontalDirections = true;

        public override void SetLookAtDirection(Vector2 direction)
        {
            base.SetLookAtDirection(direction);

            if (m_spriteRenderer && m_flipHorizontalDirections && direction.x != 0.0f)
            {
                m_spriteRenderer.flipX =
                    m_defaultDirection == EBidrectionalAnimationDirection.Right ?
                    direction.x < 0.0f :
                    direction.x > 0.0f;
            }
            else if (m_spriteRenderer && !m_flipHorizontalDirections)
            {
                m_spriteRenderer.flipX = false;
            }
        }
    }
}
