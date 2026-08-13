using GameCore;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌视图资源、渲染层级和拖拽手感的 ScriptableObject 作者设置。
	/// </summary>
	[CreateAssetMenu(menuName = "Gameplay/牌桌/视图设置", fileName = "牌桌视图设置")]
	public sealed class TabletopViewSettings : ScriptableObject
	{
		[Header("视图资源")]
		[SerializeField]
		[LabelText("卡牌视图预制体")]
		[Tooltip("由 ResourceSystem 按该地址实例化的 Gameplay 视图预制体。预制体根对象必须包含 TabletopCardView。")]
		private SoftAssetReference<GameObject> m_cardViewPrefab = new SoftAssetReference<GameObject>();

		[SerializeField]
		[LabelText("行动进度视图预制体")]
		[Tooltip("由 ResourceSystem 为每个活动行动实例化的牌桌进度视图。预制体根对象必须包含 TabletopActionProgressView。")]
		private SoftAssetReference<GameObject> m_actionProgressViewPrefab = new SoftAssetReference<GameObject>();

		[SerializeField]
		[LabelText("每层渲染深度")]
		[Tooltip("同一牌堆每增加一张卡牌时的 Z 轴表现偏移；XY 偏移由当前牌桌的唯一放置规则提供。")]
		private float m_stackDepthStep = -0.01f;

		[SerializeField]
		[LabelText("基础排序值")]
		[Tooltip("卡牌视图的基础渲染排序值，堆栈成员索引会在此基础上递增。")]
		private int m_baseSortingOrder;

		[Header("战斗表现")]
		[SerializeField]
		[LabelText("战斗基础排序值")]
		[Tooltip("活动战斗参与者的基础渲染排序值。阵型只提供相对顺序，所有战斗卡牌的层级由这一处统一配置。")]
		private int m_battleBaseSortingOrder = 100;

		[SerializeField]
		[LabelText("拖拽跟随锐度")]
		[Tooltip("拖拽时尾随卡牌追赶前一张卡牌的速度。只影响表现，不改变指针位置或权威卡牌状态。")]
		[Min(0.01f)]
		private float m_dragFollowSharpness = 100f;

		public SoftAssetReference<GameObject> CardViewPrefab => m_cardViewPrefab;

		public SoftAssetReference<GameObject> ActionProgressViewPrefab => m_actionProgressViewPrefab;

		public int BattleBaseSortingOrder => m_battleBaseSortingOrder;

		public float DragFollowSharpness => m_dragFollowSharpness;

		public TabletopCardLayoutParameters CreateLayoutParameters(TabletopCardStackGeometry geometry)
		{
			return new TabletopCardLayoutParameters(
				new Vector3(geometry.StackStep.x, geometry.StackStep.y, m_stackDepthStep),
				m_baseSortingOrder);
		}
	}
}
