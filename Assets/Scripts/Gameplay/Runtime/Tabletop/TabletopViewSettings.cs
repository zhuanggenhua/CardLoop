using System;
using GAS.Runtime;
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
		[LabelText("战斗区域视图预制体")]
		[Tooltip("由 ResourceSystem 为每场活动战斗实例化；预制体根对象必须包含 TabletopBattleAreaView。")]
		private SoftAssetReference<GameObject> m_battleAreaViewPrefab = new SoftAssetReference<GameObject>();

		[SerializeField]
		[LabelText("投射物视图预制体")]
		[Tooltip("远程或魔法自动攻击前摇时使用的纯表现预制体；规则结算仍由 Battle 和 EX-GAS 负责。")]
		private SoftAssetReference<GameObject> m_projectileViewPrefab = new SoftAssetReference<GameObject>();

		[SerializeField]
		[LabelText("卡牌烟雾粒子预制体")]
		[Tooltip("卡牌生成、交易、箱子存取币、卡牌消失和遭遇生成时使用的牌桌烟雾反馈；由 ResourceSystem 实例化。")]
		private SoftAssetReference<GameObject> m_cardSmokeEffectPrefab = new SoftAssetReference<GameObject>();

		[SerializeField]
		[LabelText("命中结果视图预制体")]
		[Tooltip("自动战斗结算后生成的独立浮动命中结果 UI；对齐参考模板的独立命中结果 UI，不挂在目标卡本体下。")]
		private SoftAssetReference<GameObject> m_hitResultViewPrefab = new SoftAssetReference<GameObject>();

		[SerializeField]
		[LabelText("每层抬升高度")]
		[Tooltip("同一牌堆每增加一张卡牌时的 Unity Y 轴抬升；对齐 StackCraft stackStep.y = 0.002。")]
		private float m_stackHeightStep = 0.002f;

		[SerializeField]
		[LabelText("基础排序值")]
		[Tooltip("卡牌视图的基础渲染排序值，堆栈成员索引会在此基础上递增。")]
		private int m_baseSortingOrder;

		[Header("牌桌音效")]
		[SerializeField]
		[LabelText("拿起卡牌音效")]
		[Tooltip("玩家按下并拿起牌桌卡牌时播放；为空则不播放。")]
		private AudioClipResolver m_cardPickAudio;

		[SerializeField]
		[LabelText("放下卡牌音效")]
		[Tooltip("玩家释放牌桌卡牌时播放；为空则不播放。")]
		private AudioClipResolver m_cardDropAudio;

		[SerializeField]
		[LabelText("卡牌滑动音效")]
		[Tooltip("日终进食时，食物卡向角色移动的模板反馈；为空则不播放。")]
		private AudioClipResolver m_cardSwipeAudio;

		[SerializeField]
		[LabelText("进食音效")]
		[Tooltip("日终进食消耗食物卡后播放；为空则不播放。")]
		private AudioClipResolver m_eatAudio;

		[SerializeField]
		[LabelText("生成完成音效")]
		[Tooltip("制作、研究等行动真实生成卡牌时播放；卡包、购买、售卖和取币有各自音效，不使用本项。")]
		private AudioClipResolver m_popAudio;

		[SerializeField]
		[LabelText("卡牌烟雾反馈音效")]
		[Tooltip("卡牌烟雾粒子生成时同步播放；为空则只播放粒子。")]
		private AudioClipResolver m_cardSmokeAudio;

		[SerializeField]
		[LabelText("单枚货币音效")]
		[Tooltip("从箱子取出一枚货币时播放；为空则不播放。")]
		private AudioClipResolver m_coinAudio;

		[SerializeField]
		[LabelText("多枚货币音效")]
		[Tooltip("出售卡牌或把货币存入箱子时播放；为空则不播放。")]
		private AudioClipResolver m_coinsAudio;

		[SerializeField]
		[LabelText("购买成交音效")]
		[Tooltip("向卡包商贩付款时播放；满价生成卡包和未满价付款都使用同一模板反馈。")]
		private AudioClipResolver m_cashRegisterAudio;

		[Header("战斗表现")]
		[SerializeField]
		[LabelText("战斗基础排序值")]
		[Tooltip("活动战斗参与者的基础渲染排序值。阵型只提供相对顺序，所有战斗卡牌的层级由这一处统一配置。")]
		private int m_battleBaseSortingOrder = 100;

		[SerializeField]
		[LabelText("投射物排序值")]
		[Tooltip("远程或魔法攻击投射物的渲染排序值；只影响表现层级。")]
		private int m_projectileSortingOrder = 140;

		[SerializeField]
		[LabelText("卡牌烟雾粒子排序值")]
		[Tooltip("牌桌卡牌烟雾粒子的渲染排序值；只影响表现层级，不改变规则状态。")]
		private int m_cardSmokeSortingOrder = 150;

		[SerializeField]
		[LabelText("命中结果排序值")]
		[Tooltip("独立浮动命中结果 UI 的世界 Canvas 排序值；对齐 StackCraft 命中反馈覆盖在卡牌和投射物上方。")]
		private int m_hitResultSortingOrder = 160;

		[Header("战斗音效")]
		[SerializeField]
		[LabelText("近战起手音效")]
		[Tooltip("自动战斗近战攻击开始时播放；为空则该表现不发出音效。")]
		private AudioClipResolver m_meleeAttackAudio;

		[SerializeField]
		[LabelText("远程起手音效")]
		[Tooltip("自动战斗远程攻击开始时播放；为空则该表现不发出音效。")]
		private AudioClipResolver m_rangedAttackAudio;

		[SerializeField]
		[LabelText("魔法起手音效")]
		[Tooltip("自动战斗魔法攻击开始时播放；为空则该表现不发出音效。")]
		private AudioClipResolver m_magicAttackAudio;

		[SerializeField]
		[LabelText("近战命中音效")]
		[Tooltip("自动战斗近战攻击命中后播放；Miss 不会播放该音效。")]
		private AudioClipResolver m_meleeHitAudio;

		[SerializeField]
		[LabelText("远程命中音效")]
		[Tooltip("自动战斗远程攻击命中后播放；Miss 不会播放该音效。")]
		private AudioClipResolver m_rangedHitAudio;

		[SerializeField]
		[LabelText("魔法命中音效")]
		[Tooltip("自动战斗魔法攻击命中后播放；Miss 不会播放该音效。")]
		private AudioClipResolver m_magicHitAudio;

		[SerializeField]
		[LabelText("未命中音效")]
		[Tooltip("自动战斗攻击结算为 Miss 时播放。")]
		private AudioClipResolver m_missAudio;

		[SerializeField]
		[LabelText("暴击音效")]
		[Tooltip("自动战斗攻击结算为暴击时，在命中音效后额外播放。")]
		private AudioClipResolver m_criticalAudio;

		[SerializeField]
		[LabelText("拖拽跟随锐度")]
		[Tooltip("拖拽时尾随卡牌追赶前一张卡牌的速度。只影响表现，不改变指针位置或权威卡牌状态。")]
		[Min(0.01f)]
		private float m_dragFollowSharpness = 100f;

		[SerializeField]
		[LabelText("点击判定距离")]
		[Tooltip("指针按下到释放的牌桌世界距离小于该值时按点击处理；对齐 StackCraft clickThreshold = 0.02。")]
		[Min(0f)]
		private float m_clickThreshold = 0.02f;

		[SerializeField]
		[LabelText("目标吸附半径")]
		[Tooltip("拖拽释放时，若指针没有直接射中目标卡牌，则在拖拽牌段首张卡周围按该半径查找最近候选；对齐 StackCraft attachRadius = 0.25。")]
		[Min(0f)]
		private float m_attachRadius = 0.25f;

		[SerializeField]
		[LabelText("拖拽抬升高度")]
		[Tooltip("玩家按下卡牌后，拖拽牌段首张卡离开桌面的 Unity Y 轴高度；对齐 StackCraft dragHeight = 0.1。")]
		[Min(0f)]
		private float m_dragHeight = 0.1f;

		[SerializeField]
		[LabelText("普通移动秒数")]
		[Tooltip("牌堆权威位置变化后，卡牌移动到目标姿态的补间时长；对齐 StackCraft moveDuration = 0.1。")]
		[Min(0f)]
		private float m_moveDurationSeconds = 0.1f;

		public SoftAssetReference<GameObject> CardViewPrefab => m_cardViewPrefab;

		public SoftAssetReference<GameObject> ActionProgressViewPrefab => m_actionProgressViewPrefab;

		public SoftAssetReference<GameObject> BattleAreaViewPrefab => m_battleAreaViewPrefab;

		public SoftAssetReference<GameObject> ProjectileViewPrefab => m_projectileViewPrefab;

		public SoftAssetReference<GameObject> CardSmokeEffectPrefab => m_cardSmokeEffectPrefab;

		public SoftAssetReference<GameObject> HitResultViewPrefab => m_hitResultViewPrefab;

		public int BattleBaseSortingOrder => m_battleBaseSortingOrder;

		public int ProjectileSortingOrder => m_projectileSortingOrder;

		public int CardSmokeSortingOrder => m_cardSmokeSortingOrder;

		public int HitResultSortingOrder => m_hitResultSortingOrder;

		public float DragFollowSharpness => m_dragFollowSharpness;

		public float ClickThreshold => m_clickThreshold;

		public float AttachRadius => m_attachRadius;

		public float DragHeight => m_dragHeight;

		public float MoveDurationSeconds => m_moveDurationSeconds;

		public AudioClipResolver MissAudio => m_missAudio;

		public AudioClipResolver CriticalAudio => m_criticalAudio;

		internal AudioClipResolver GetPresentationAudio(TabletopPresentationCueKind cue)
		{
			return cue switch
			{
				TabletopPresentationCueKind.CardPick => m_cardPickAudio,
				TabletopPresentationCueKind.CardDrop => m_cardDropAudio,
				TabletopPresentationCueKind.CardSwipe => m_cardSwipeAudio,
				TabletopPresentationCueKind.Eat => m_eatAudio,
				TabletopPresentationCueKind.Pop => m_popAudio,
				TabletopPresentationCueKind.CardSmoke => m_cardSmokeAudio,
				TabletopPresentationCueKind.CardHighlight => null,
				TabletopPresentationCueKind.Coin => m_coinAudio,
				TabletopPresentationCueKind.Coins => m_coinsAudio,
				TabletopPresentationCueKind.CashRegister => m_cashRegisterAudio,
				_ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "未知牌桌表现反馈类型。")
			};
		}

		internal AudioClipResolver GetAttackAudio(int combatTypeTagCode)
		{
			if (combatTypeTagCode == GAS.Runtime.XTag.Combat_Ranged)
			{
				return m_rangedAttackAudio;
			}
			if (combatTypeTagCode == GAS.Runtime.XTag.Combat_Magic)
			{
				return m_magicAttackAudio;
			}
			return m_meleeAttackAudio;
		}

		internal AudioClipResolver GetHitAudio(int combatTypeTagCode)
		{
			if (combatTypeTagCode == GAS.Runtime.XTag.Combat_Ranged)
			{
				return m_rangedHitAudio;
			}
			if (combatTypeTagCode == GAS.Runtime.XTag.Combat_Magic)
			{
				return m_magicHitAudio;
			}
			return m_meleeHitAudio;
		}

		public TabletopCardLayoutParameters CreateLayoutParameters(TabletopCardStackGeometry geometry)
		{
			return new TabletopCardLayoutParameters(
				new Vector3(geometry.StackStep.x, m_stackHeightStep, geometry.StackStep.y),
				m_baseSortingOrder);
		}
	}
}
