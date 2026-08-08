using GameCore;
using UnityEngine;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 牌桌表现层的项目配置。
    /// 它是视图预制体和布局参数的作者源，不登记玩法内容，也不保存任何牌桌运行时状态。
    /// </summary>
    [CreateAssetMenu(menuName = "Gameplay/牌桌/表现设置", fileName = "牌桌表现设置")]
    public sealed class TabletopCardPresentationSettings : ScriptableObject
    {
        [Header("视图资源")]
        [SerializeField, InspectorName("卡牌视图预制体")]
        [Tooltip("由 ResourceSystem 按该地址实例化的 Gameplay 视图预制体。预制体根对象必须包含 TabletopCardView。")]
        private SoftAssetReference<GameObject> m_cardViewPrefab = new();

        [Header("堆栈布局")]
        [SerializeField, InspectorName("每层视觉步进")]
        [Tooltip("同一堆栈中每向顶部一张卡牌增加的局部三维偏移；可按相机方向配置正负深度，不会修改权威卡牌位置。")]
        private Vector3 m_stackVisualStep = new(0f, 0.08f, -0.01f);

        [SerializeField, InspectorName("基础排序值")]
        [Tooltip("卡牌视图的基础渲染排序值，堆栈成员索引会在此基础上递增。")]
        private int m_baseSortingOrder;

        [SerializeField, InspectorName("拖拽跟随锐度")]
        [Tooltip("拖拽时尾随卡牌追赶前一张卡牌的速度。只影响表现，不改变指针位置或权威卡牌状态。")]
        [Min(0.01f)]
        private float m_dragFollowSharpness = 100f;

        /// <summary>
        /// 返回视图预制体的现有 YooAsset 地址引用。
        /// 该引用只负责资源定位，不是玩法内容身份。
        /// </summary>
        public SoftAssetReference<GameObject> CardViewPrefab =>
            m_cardViewPrefab ??= new SoftAssetReference<GameObject>();

        /// <summary>
        /// 返回布局计算使用的不可变参数快照，防止运行时计算直接修改 Inspector 配置。
        /// </summary>
        public TabletopCardLayoutParameters LayoutParameters =>
            new(m_stackVisualStep, m_baseSortingOrder);

        /// <summary>
        /// 拖拽预览中尾随卡牌的指数跟随锐度。
        /// </summary>
        public float DragFollowSharpness => Mathf.Max(0.01f, m_dragFollowSharpness);
    }
}
