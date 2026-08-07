using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 一张可堆叠牌桌卡牌的纯表现投影。
    /// 它只持有当前卡牌身份和显示资源，不拥有堆栈成员关系、位置真相或任何玩法规则。
    /// </summary>
    public sealed class TabletopCardView : MonoBehaviour
    {
        [Header("表现组件")]
        [SerializeField, InspectorName("图片组件")]
        [Tooltip("可选的 SpriteRenderer。配置后会显示卡牌作者源选择的卡面图片。")]
        private SpriteRenderer m_artworkRenderer;

        [SerializeField, InspectorName("表面渲染器")]
        [Tooltip("可选的卡面 Renderer。配置纹理属性后，会将内容图片写入材质属性块，不复制材质实例。")]
        private Renderer m_surfaceRenderer;

        [SerializeField, InspectorName("表面纹理属性")]
        [Tooltip("表面材质用于接收卡面纹理的 Shader 属性名；留空表示不向表面材质写入纹理。")]
        private string m_surfaceTextureProperty = string.Empty;

        [SerializeField, InspectorName("高亮节点")]
        [Tooltip("可选的高亮子节点。拖拽命中空间候选时只切换其显隐，不创建材质或规则状态。")]
        private GameObject m_highlightRoot;

        private MaterialPropertyBlock m_propertyBlock;
        private Vector3 m_dragTargetLocalPosition;
        private float m_dragFollowSharpness;
        private bool m_isFollowingDragTarget;
        private SpriteRenderer[] m_spriteRenderers;

        /// <summary>
        /// 当前视图对应的局内卡牌身份。未绑定前无效。
        /// </summary>
        public TabletopCardId CardId { get; private set; }

        /// <summary>
        /// 当前视图对应的唯一内容身份。未绑定前无效。
        /// </summary>
        public GamePlayContentId ContentId { get; private set; }

        /// <summary>
        /// 当前是否正在显示空间候选高亮。该状态只代表表现反馈，不代表规则接受目标。
        /// </summary>
        public bool IsHighlighted => m_highlightRoot != null && m_highlightRoot.activeSelf;

        /// <summary>
        /// 将视图绑定到一张局内卡牌和对应卡牌作者源。
        /// 绑定不会修改卡牌状态，也不会执行任何行动或规则。
        /// </summary>
        public void Bind(TabletopCard tabletopCard, GamePlayCardDefinition contentAsset)
        {
            if (tabletopCard == null)
            {
                throw new System.ArgumentNullException(nameof(tabletopCard));
            }

            if (contentAsset == null)
            {
                throw new System.ArgumentNullException(nameof(contentAsset));
            }

            if (tabletopCard.ContentId != contentAsset.ContentId)
            {
                throw new System.ArgumentException(
                    "牌桌卡牌和卡牌作者源的内容 ID 不一致，拒绝创建错误投影。",
                    nameof(contentAsset));
            }

            CardId = tabletopCard.Id;
            ContentId = contentAsset.ContentId;
            gameObject.name = $"TabletopCard_{contentAsset.DisplayName}";
        }

        /// <summary>
        /// 将布局结果应用到 Unity Transform 和可选 SpriteRenderer。
        /// 位置只写入表现对象的局部坐标，不回写权威卡牌状态。
        /// </summary>
        public void ApplyPose(TabletopCardPose pose)
        {
            m_isFollowingDragTarget = false;
            transform.localPosition = pose.LocalPosition;
            ApplySortingOrder(pose.SortingOrder);
        }

        /// <summary>
        /// 应用拖拽预览姿态。被直接拖动的首张卡牌立即跟随，尾随卡牌使用指数阻尼追赶目标。
        /// 该方法只修改视图，不提交拆堆、移动或合堆。
        /// </summary>
        public void ApplyDragPose(TabletopCardPose pose, bool immediate, float followSharpness)
        {
            ApplySortingOrder(pose.SortingOrder);
            if (immediate)
            {
                m_isFollowingDragTarget = false;
                transform.localPosition = pose.LocalPosition;
                return;
            }

            m_dragTargetLocalPosition = pose.LocalPosition;
            m_dragFollowSharpness = Mathf.Max(0.01f, followSharpness);
            m_isFollowingDragTarget = true;
        }

        /// <summary>
        /// 写入已由 ResourceSystem 加载的图片。
        /// 视图没有图片组件时保持可用，允许 Mod 预制体完全使用自定义 Renderer 或其它表现组件。
        /// </summary>
        public void SetArtwork(Sprite artwork)
        {
            if (m_artworkRenderer != null)
            {
                m_artworkRenderer.sprite = artwork;
            }

            if (m_surfaceRenderer == null || string.IsNullOrWhiteSpace(m_surfaceTextureProperty))
            {
                return;
            }

            Material material = m_surfaceRenderer.sharedMaterial;
            if (material == null || !material.HasProperty(m_surfaceTextureProperty))
            {
                return;
            }

            m_propertyBlock ??= new MaterialPropertyBlock();
            m_surfaceRenderer.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetTexture(
                Shader.PropertyToID(m_surfaceTextureProperty),
                artwork == null ? null : artwork.texture);
            m_surfaceRenderer.SetPropertyBlock(m_propertyBlock);
        }

        /// <summary>
        /// 切换当前视图的空间候选高亮。
        /// 高亮只提供反馈，不代表后续行动、配方或规则一定接受该目标。
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (m_highlightRoot != null)
            {
                m_highlightRoot.SetActive(highlighted);
            }
        }

        private void Update()
        {
            if (!m_isFollowingDragTarget)
            {
                return;
            }

            float interpolation = 1f - Mathf.Exp(-m_dragFollowSharpness * Time.unscaledDeltaTime);
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                m_dragTargetLocalPosition,
                interpolation);

            if ((transform.localPosition - m_dragTargetLocalPosition).sqrMagnitude <= 0.000001f)
            {
                transform.localPosition = m_dragTargetLocalPosition;
                m_isFollowingDragTarget = false;
            }
        }

        private void ApplySortingOrder(int sortingOrder)
        {
            m_spriteRenderers ??= GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < m_spriteRenderers.Length; i++)
            {
                m_spriteRenderers[i].sortingOrder = sortingOrder;
            }
        }
    }
}
