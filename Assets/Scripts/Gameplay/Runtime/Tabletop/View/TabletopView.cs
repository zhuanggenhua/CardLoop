using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using GAS.Runtime;
using GameCore;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop.Actions;
using Sirenix.OdinInspector;
using UnityEngine;
using YokiFrame;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 当前牌桌的 Unity 表现对象，统一管理卡牌、行动进度、资源句柄和临时交互表现。
	/// </summary>
	public sealed class TabletopView : MonoBehaviour
	{
		private const float PresentationHighlightSeconds = 2f;

		private sealed class ViewEntry
		{
			internal TabletopCard TabletopCard { get; }

			internal CardDefinition Definition { get; }

			internal ResourceHandle<GameObject> InstanceHandle { get; }

			internal TabletopCardView View { get; set; }

			internal ViewEntry(TabletopCard tabletopCard, CardDefinition definition, ResourceHandle<GameObject> instanceHandle)
			{
				TabletopCard = tabletopCard;
				Definition = definition;
				InstanceHandle = instanceHandle;
			}
		}

		private sealed class ActionProgressEntry
		{
			internal ActionInstance Action { get; }

			internal TabletopCardId AnchorCardId { get; }

			internal ResourceHandle<GameObject> InstanceHandle { get; }

			internal TabletopActionProgressView View { get; set; }

			internal ActionProgressEntry(
				ActionInstance action,
				TabletopCardId anchorCardId,
				ResourceHandle<GameObject> instanceHandle)
			{
				Action = action;
				AnchorCardId = anchorCardId;
				InstanceHandle = instanceHandle;
			}
		}

		private sealed class BattleAreaEntry
		{
			internal Battle Battle { get; }

			internal ResourceHandle<GameObject> InstanceHandle { get; }

			internal TabletopBattleAreaView View { get; set; }

			internal BattleAreaEntry(Battle battle, ResourceHandle<GameObject> instanceHandle)
			{
				Battle = battle;
				InstanceHandle = instanceHandle;
			}
		}

		private sealed class ProjectileEntry
		{
			internal Battle Battle { get; }

			internal ulong Sequence { get; }

			internal ResourceHandle<GameObject> InstanceHandle { get; }

			internal TabletopProjectileView View { get; set; }

			internal ProjectileEntry(Battle battle, ulong sequence, ResourceHandle<GameObject> instanceHandle)
			{
				Battle = battle;
				Sequence = sequence;
				InstanceHandle = instanceHandle;
			}
		}

		private sealed class CardSmokeEffectEntry
		{
			internal ResourceHandle<GameObject> InstanceHandle { get; }

			internal TabletopCardSmokeEffectView View { get; set; }

			internal CardSmokeEffectEntry(ResourceHandle<GameObject> instanceHandle)
			{
				InstanceHandle = instanceHandle;
			}
		}

		private sealed class HitResultEntry
		{
			internal ResourceHandle<GameObject> InstanceHandle { get; }

			internal TabletopHitResultView View { get; set; }

			internal HitResultEntry(ResourceHandle<GameObject> instanceHandle)
			{
				InstanceHandle = instanceHandle;
			}
		}

		[Header("牌桌表现")]
		[SerializeField]
		[LabelText("视图设置")]
		[Tooltip("提供卡牌视图资源、渲染层级和拖拽手感；规则几何由绑定的牌桌提供。")]
		private TabletopViewSettings m_settings;

		private readonly Dictionary<TabletopCardId, ViewEntry> m_views = new Dictionary<TabletopCardId, ViewEntry>();

		private readonly Dictionary<ActionInstance, ActionProgressEntry> m_actionProgressViews =
			new Dictionary<ActionInstance, ActionProgressEntry>();

		private readonly Dictionary<Battle, BattleAreaEntry> m_battleAreaViews =
			new Dictionary<Battle, BattleAreaEntry>();

		private readonly Dictionary<Battle, ProjectileEntry> m_projectileViews =
			new Dictionary<Battle, ProjectileEntry>();

		private readonly List<CardSmokeEffectEntry> m_cardSmokeEffects = new List<CardSmokeEffectEntry>();

		private readonly List<HitResultEntry> m_hitResultViews = new List<HitResultEntry>();

		private readonly Dictionary<ContentId, ResourceHandle<Texture2D>> m_artHandles = new Dictionary<ContentId, ResourceHandle<Texture2D>>();

		private readonly Dictionary<ContentId, ResourceHandle<Material>> m_surfaceHandles =
			new Dictionary<ContentId, ResourceHandle<Material>>();

		private readonly Dictionary<ContentId, ResourceHandle<Texture2D>> m_cardBuyerCurrencyArtHandles =
			new Dictionary<ContentId, ResourceHandle<Texture2D>>();

		private readonly Dictionary<ContentId, Texture2D> m_cardBuyerCurrencyArtwork =
			new Dictionary<ContentId, Texture2D>();

		private readonly HashSet<TabletopCardId> m_highlightedDropTargetCardIds =
			new HashSet<TabletopCardId>();

		private readonly Dictionary<TabletopCardId, Vector3> m_cardSpawnPresentationOrigins =
			new Dictionary<TabletopCardId, Vector3>();

		private readonly List<TabletopCardId> m_highlightRemovalBuffer = new List<TabletopCardId>();

		private Tabletop m_tabletop;

		private TabletopCardId m_localDraggedStackAnchorCardId;

		private TabletopCardId m_hoveredCardId;

		private TabletopCardId m_selectedCardId;

		private bool m_hasLocalDraggedStack;

		private ulong m_projectedCardRevision;

		private ulong m_projectedBattleRevision;

		public bool IsBound => m_tabletop != null;

		/// <summary>指针当前悬浮的卡牌；悬浮只属于本地表现，不进入规则状态或存档。</summary>
		public TabletopCardId HoveredCardId => m_hoveredCardId;

		/// <summary>玩家当前选中的卡牌；选择只属于本地牌桌交互状态。</summary>
		public TabletopCardId SelectedCardId => m_selectedCardId;

		/// <summary>当前应向玩家展示详情的卡牌；悬浮临时覆盖持久选择。</summary>
		public TabletopCardId ReadableCardId => m_hoveredCardId.IsValid
			? m_hoveredCardId
			: m_selectedCardId;

		/// <summary>当前可读卡牌变化时通知直接绑定的表现组件。</summary>
		public event Action ReadableCardChanged;

		internal Tabletop BoundTabletop => m_tabletop;

		internal float CardClickThreshold => m_settings == null ? 0.02f : m_settings.ClickThreshold;

		internal float CardDragHeight => m_settings == null ? 0.1f : m_settings.DragHeight;

		internal float CardAttachRadius => m_settings == null ? 0.25f : m_settings.AttachRadius;

		/// <summary>
		/// 绑定当前单局的唯一牌桌；卡牌状态、内容索引和活动战斗都由它统一拥有。
		/// </summary>
		public void Bind(Tabletop tabletop)
		{
			if (tabletop == null)
			{
				throw new ArgumentNullException(nameof(tabletop));
			}
			if (m_settings == null)
			{
				throw new InvalidOperationException("卡牌视图投影缺少卡牌设置资产。");
			}
			if (m_settings.CardViewPrefab == null || !m_settings.CardViewPrefab.IsValid())
			{
				throw new InvalidOperationException("卡牌视图投影缺少有效的卡牌视图预制体地址。");
			}
			if (m_settings.ActionProgressViewPrefab == null || !m_settings.ActionProgressViewPrefab.IsValid())
			{
				throw new InvalidOperationException("卡牌视图投影缺少有效的行动进度视图预制体地址。");
			}
			if (m_settings.BattleAreaViewPrefab == null || !m_settings.BattleAreaViewPrefab.IsValid())
			{
				throw new InvalidOperationException("卡牌视图投影缺少有效的战斗区域视图预制体地址。");
			}
			if (m_settings.ProjectileViewPrefab == null || !m_settings.ProjectileViewPrefab.IsValid())
			{
				throw new InvalidOperationException("卡牌视图投影缺少有效的投射物视图预制体地址。");
			}
			if (m_settings.CardSmokeEffectPrefab == null || !m_settings.CardSmokeEffectPrefab.IsValid())
			{
				throw new InvalidOperationException("卡牌视图投影缺少有效的卡牌烟雾粒子预制体地址。");
			}
			if (m_settings.HitResultViewPrefab == null || !m_settings.HitResultViewPrefab.IsValid())
			{
				throw new InvalidOperationException("卡牌视图投影缺少有效的命中结果视图预制体地址。");
			}
			if (!float.IsFinite(m_settings.DragFollowSharpness) || m_settings.DragFollowSharpness <= 0f)
			{
				throw new InvalidOperationException("牌桌视图设置的拖拽跟随锐度必须是有限正数。");
			}
			if (!float.IsFinite(m_settings.ClickThreshold) || m_settings.ClickThreshold < 0f)
			{
				throw new InvalidOperationException("牌桌视图设置的点击判定距离必须是大于等于 0 的有限值。");
			}
			if (!float.IsFinite(m_settings.DragHeight) || m_settings.DragHeight < 0f)
			{
				throw new InvalidOperationException("牌桌视图设置的拖拽抬升高度必须是大于等于 0 的有限值。");
			}
			m_settings.CreateLayoutParameters(tabletop.PlacementRules.Geometry);
			Unbind();
			m_tabletop = tabletop;
			m_tabletop.ActionSettled += OnTabletopActionSettled;
			m_tabletop.PresentationCueRequested += OnTabletopPresentationCueRequested;
			Refresh();
		}

		private void Refresh()
		{
			if (!IsBound)
			{
				return;
			}
			TabletopCards cards = m_tabletop.Cards;
			RefreshBattleAreaViews();
			HashSet<TabletopCardId> liveCardIds = new HashSet<TabletopCardId>();
			for (int stackIndex = 0; stackIndex < cards.Stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = cards.Stacks[stackIndex];
				for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
				{
					TabletopCard tabletopCard = stack.Cards[cardIndex];
					liveCardIds.Add(tabletopCard.Id);
					if (!m_views.TryGetValue(tabletopCard.Id, out var entry))
					{
						CardDefinition contentAsset = GetRequiredCardDefinition(tabletopCard);
						RequestView(tabletopCard, contentAsset);
					}
					else
					{
						ApplyCardPose(entry, stack, cardIndex);
					}
				}
			}
			foreach (TabletopCardId cardId in new List<TabletopCardId>(m_views.Keys))
			{
				if (!liveCardIds.Contains(cardId))
				{
					ReleaseView(cardId);
				}
			}
			foreach (TabletopCardId cardId in new List<TabletopCardId>(m_cardSpawnPresentationOrigins.Keys))
			{
				if (!liveCardIds.Contains(cardId))
				{
					m_cardSpawnPresentationOrigins.Remove(cardId);
				}
			}
			SetReadableCards(
				liveCardIds.Contains(m_hoveredCardId) ? m_hoveredCardId : default,
				liveCardIds.Contains(m_selectedCardId) ? m_selectedCardId : default);
			m_projectedCardRevision = cards.Revision;
			m_projectedBattleRevision = m_tabletop.BattleRevision;
			if (ReadableCardId.IsValid)
			{
				ReadableCardChanged?.Invoke();
			}
		}

		/// <summary>
		/// 解除权威牌桌绑定，并释放该绑定创建的全部视图与资源句柄。
		/// </summary>
		public void Unbind()
		{
			if (m_tabletop != null)
			{
				m_tabletop.ActionSettled -= OnTabletopActionSettled;
				m_tabletop.PresentationCueRequested -= OnTabletopPresentationCueRequested;
			}
			SetReadableCards(default, default);
			foreach (ActionInstance action in new List<ActionInstance>(m_actionProgressViews.Keys))
			{
				ReleaseActionProgressView(action);
			}
			foreach (Battle battle in new List<Battle>(m_battleAreaViews.Keys))
			{
				ReleaseBattleAreaView(battle);
			}
			foreach (Battle battle in new List<Battle>(m_projectileViews.Keys))
			{
				ReleaseProjectileView(battle);
			}
			ReleaseAllCardSmokeEffects();
			ReleaseAllHitResultViews();
			foreach (TabletopCardId cardId in new List<TabletopCardId>(m_views.Keys))
			{
				ReleaseView(cardId);
			}
			foreach (ResourceHandle<Texture2D> handle in m_artHandles.Values)
			{
				ResourceSystem.ReleaseAsset(handle);
			}
			m_artHandles.Clear();
			foreach (ResourceHandle<Material> handle in m_surfaceHandles.Values)
			{
				ResourceSystem.ReleaseAsset(handle);
			}
			m_surfaceHandles.Clear();
			foreach (ResourceHandle<Texture2D> handle in m_cardBuyerCurrencyArtHandles.Values)
			{
				ResourceSystem.ReleaseAsset(handle);
			}
			m_cardBuyerCurrencyArtHandles.Clear();
			m_cardBuyerCurrencyArtwork.Clear();
			m_cardSpawnPresentationOrigins.Clear();
			m_hasLocalDraggedStack = false;
			m_localDraggedStackAnchorCardId = default(TabletopCardId);
			m_highlightedDropTargetCardIds.Clear();
			m_highlightRemovalBuffer.Clear();
			m_tabletop = null;
			m_projectedCardRevision = 0;
			m_projectedBattleRevision = 0;
		}

		internal void SetHoveredCard(TabletopCardId cardId)
		{
			RequireLiveCardOrEmpty(cardId, "悬浮");
			SetReadableCards(cardId, m_selectedCardId);
		}

		internal void SelectCard(TabletopCardId cardId)
		{
			RequireLiveCardOrEmpty(cardId, "选择");
			SetReadableCards(m_hoveredCardId, cardId);
		}

		/// <summary>
		/// 读取当前可读卡牌及其静态作者内容；局内状态仍从返回的卡牌对象读取。
		/// </summary>
		public bool TryGetReadableCard(out TabletopCard card, out CardDefinition definition)
		{
			TabletopCardId cardId = ReadableCardId;
			if (!cardId.IsValid)
			{
				card = null;
				definition = null;
				return false;
			}

			if (!m_tabletop.Cards.TryGetCard(cardId, out card))
			{
				throw new InvalidOperationException($"当前可读卡牌已经不属于牌桌：{cardId}。");
			}
			definition = GetRequiredCardDefinition(card);
			return true;
		}

		internal bool TryGetCardView(TabletopCardId cardId, out TabletopCardView view)
		{
			if (cardId.IsValid &&
				m_views.TryGetValue(cardId, out ViewEntry entry) &&
				entry.View != null)
			{
				view = entry.View;
				return true;
			}

			view = null;
			return false;
		}

		/// <summary>
		/// 读取当前被拖卡牌视图的实际牌桌坐标。
		/// 释放判定必须使用视图已经落到的卡牌位置，不能退回鼠标坐标或未提交的逻辑请求坐标。
		/// </summary>
		internal Vector2 GetCardTablePosition(TabletopCardId cardId)
		{
			if (!m_views.TryGetValue(cardId, out ViewEntry entry) || entry.View == null)
			{
				throw new InvalidOperationException(
					$"牌桌视图找不到释放卡牌 {cardId}，无法读取 StackCraft 的实际释放位置。" );
			}

			return TabletopCoordinateSpace.ToTablePosition(entry.View.transform.localPosition);
		}

		internal bool TryFindNearestCardViewWithinAttachRadius(
			TabletopCardId sourceCardId,
			TabletopCardStack excludedStack,
			IReadOnlyList<TabletopCardId> allowedStackBottomCardIds,
			out TabletopCardView view)
		{
			if (m_settings == null)
			{
				throw new InvalidOperationException("牌桌视图缺少视图设置，无法按 StackCraft 目标吸附半径查找卡牌。");
			}

			float attachRadius = m_settings.AttachRadius;
			if (!float.IsFinite(attachRadius) || attachRadius < 0f)
			{
				throw new InvalidOperationException("牌桌视图设置的目标吸附半径必须是大于等于 0 的有限值。");
			}
			if (attachRadius <= 0f)
			{
				view = null;
				return false;
			}

			SyncPhysicsTransformsForStackCraftPointerQuery();
			Vector3 overlapSphereCenter = RequireStackCraftOverlapSphereCenter(sourceCardId);
			Collider[] hits = Physics.OverlapSphere(
				overlapSphereCenter,
				attachRadius,
				Physics.DefaultRaycastLayers,
				QueryTriggerInteraction.Ignore);
			TabletopCardView bestView = null;
			float bestCenterSqrDistance = float.PositiveInfinity;
			HashSet<TabletopCardStack> checkedStacks = new HashSet<TabletopCardStack>();
			for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
			{
				// StackCraft 的吸附扫描只认卡牌根碰撞体，角色装备面板等子碰撞体不能扩大可吸附范围。
				TabletopCardView hitView = hits[hitIndex].GetComponent<TabletopCardView>();
				if (hitView == null ||
					!hitView.CardId.IsValid ||
					!m_views.TryGetValue(hitView.CardId, out ViewEntry hitEntry) ||
					!ReferenceEquals(hitView, hitEntry.View) ||
					hitEntry.TabletopCard == null ||
					hitEntry.TabletopCard.Stack == null)
				{
					continue;
				}

				TabletopCardStack candidateStack = hitEntry.TabletopCard.Stack;
				TabletopCardId bottomCardId = candidateStack.BottomCard.Id;
				if ((excludedStack != null && ReferenceEquals(candidateStack, excludedStack)) ||
					(allowedStackBottomCardIds != null &&
					 !ContainsCardId(allowedStackBottomCardIds, bottomCardId)) ||
					!checkedStacks.Add(candidateStack) ||
					!m_views.TryGetValue(bottomCardId, out ViewEntry bottomEntry) ||
					bottomEntry.View == null)
				{
					continue;
				}

				float centerSqrDistance = GetStackCraftClosestCardCenterSqrDistance(
						candidateStack,
						overlapSphereCenter);
				if (float.IsFinite(centerSqrDistance) &&
					(bestView == null ||
					 centerSqrDistance < bestCenterSqrDistance - 0.0001f))
				{
					bestView = bottomEntry.View;
					bestCenterSqrDistance = centerSqrDistance;
				}
			}

			view = bestView;
			return view != null;
		}

		internal bool TryFindNearestVisibleCardViewWithinAttachRadius(
			TabletopCardId sourceCardId,
			TabletopCardStack excludedStack,
			Func<TabletopCardView, CardDefinition, bool> predicate,
			out TabletopCardView view)
		{
			if (m_settings == null)
			{
				throw new InvalidOperationException("牌桌视图缺少视图设置，无法按 StackCraft 目标吸附半径查找卡牌。");
			}
			if (predicate == null)
			{
				throw new ArgumentNullException(nameof(predicate));
			}

			float attachRadius = CardAttachRadius;
			if (!float.IsFinite(attachRadius) || attachRadius < 0f)
			{
				throw new InvalidOperationException("牌桌视图设置的目标吸附半径必须是大于等于 0 的有限值。");
			}
			if (attachRadius <= 0f)
			{
				view = null;
				return false;
			}

			SyncPhysicsTransformsForStackCraftPointerQuery();
			Vector3 overlapSphereCenter = RequireStackCraftOverlapSphereCenter(sourceCardId);
			Collider[] hits = Physics.OverlapSphere(
				overlapSphereCenter,
				attachRadius,
				Physics.DefaultRaycastLayers,
				QueryTriggerInteraction.Ignore);
			TabletopCardView bestView = null;
			float bestCenterSqrDistance = float.PositiveInfinity;
			for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
			{
				// 终端释放同样只用卡牌根碰撞体做物理命中，点击冒泡不参与吸附目标判定。
				TabletopCardView hitView = hits[hitIndex].GetComponent<TabletopCardView>();
				if (hitView == null ||
					!hitView.CardId.IsValid ||
					!m_views.TryGetValue(hitView.CardId, out ViewEntry entry) ||
					!ReferenceEquals(hitView, entry.View) ||
					entry.View == null ||
					entry.TabletopCard == null ||
					entry.TabletopCard.Stack == null ||
					(excludedStack != null && ReferenceEquals(entry.TabletopCard.Stack, excludedStack)) ||
					!predicate(entry.View, entry.Definition))
				{
					continue;
				}

				float sqrDistance = (entry.View.transform.position - overlapSphereCenter).sqrMagnitude;
				if (bestView == null || sqrDistance < bestCenterSqrDistance - 0.0001f)
				{
					bestView = entry.View;
					bestCenterSqrDistance = sqrDistance;
				}
			}

			view = bestView;
			return view != null;
		}

		private Vector3 RequireStackCraftOverlapSphereCenter(TabletopCardId sourceCardId)
		{
			if (!sourceCardId.IsValid)
			{
				throw new ArgumentException("StackCraft 目标扫描必须引用当前被拖动的有效卡牌。", nameof(sourceCardId));
			}
			if (!m_views.TryGetValue(sourceCardId, out ViewEntry entry) ||
				entry.View == null)
			{
				throw new InvalidOperationException(
					$"StackCraft 目标扫描找不到被拖动卡牌 {sourceCardId} 的当前视图，无法对齐模板的 Transform 命中语义。");
			}
			return entry.View.transform.position;
		}

		private static void SyncPhysicsTransformsForStackCraftPointerQuery()
		{
			// 本帧刚把拖拽卡牌 Transform 贴到鼠标位置，随后立刻用物理查询找释放目标。
			// Unity 物理世界需要显式同步，才能对齐 StackCraft 直接用当前卡牌 Transform 命中。
			Physics.SyncTransforms();
		}

		private float GetStackCraftClosestCardCenterSqrDistance(
			TabletopCardStack stack,
			Vector3 worldPosition)
		{
			float centerSqrDistance = float.PositiveInfinity;
			for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
			{
				if (!m_views.TryGetValue(stack.Cards[cardIndex].Id, out ViewEntry entry) ||
					entry.View == null)
				{
					continue;
				}

				float sqrDistance = (entry.View.transform.position - worldPosition).sqrMagnitude;
				if (sqrDistance < centerSqrDistance)
				{
					centerSqrDistance = sqrDistance;
				}
			}
			return centerSqrDistance;
		}

		internal void SetLocalDraggedStack(TabletopCardId anchorCardId)
		{
			if (!anchorCardId.IsValid)
			{
				throw new ArgumentException("本地拖拽牌堆必须引用有效的局内卡牌。", nameof(anchorCardId));
			}
			if (IsBound &&
				(m_projectedCardRevision != m_tabletop.Cards.Revision ||
				 m_projectedBattleRevision != m_tabletop.BattleRevision))
			{
				// StackCraft 按下拆堆后立刻重投影原堆；不能等 LateUpdate 才让露出的卡牌回到正确层级。
				Refresh();
			}
			m_localDraggedStackAnchorCardId = anchorCardId;
			m_hasLocalDraggedStack = true;
			ApplyLocalDraggedStackPose();
		}

		internal void ClearLocalDraggedStack()
		{
			if (m_hasLocalDraggedStack)
			{
				TabletopCardStack draggedStack = null;
				if (IsBound)
				{
					m_tabletop.Cards.TryGetStackContaining(m_localDraggedStackAnchorCardId, out draggedStack);
				}
				m_hasLocalDraggedStack = false;
				m_localDraggedStackAnchorCardId = default(TabletopCardId);
				if (IsBound &&
					(m_projectedCardRevision != m_tabletop.Cards.Revision ||
					 m_projectedBattleRevision != m_tabletop.BattleRevision))
				{
					// StackCraft 释放后会立即对受影响牌堆 SetTargetPosition / ResolveOverlaps；
					// 这里同步刷新整张牌桌，避免合堆、交易或战斗改变目标堆后等到下一帧才落位。
					Refresh();
					return;
				}
				if (draggedStack != null)
				{
					ApplyAuthoritativeStackPose(draggedStack);
				}
			}
		}

		internal void SetDropTargetHighlights(IReadOnlyList<TabletopCardId> cardIds)
		{
			if (cardIds == null || cardIds.Count == 0)
			{
				ClearDropTargetHighlights();
				return;
			}

			m_highlightRemovalBuffer.Clear();
			foreach (TabletopCardId highlightedCardId in m_highlightedDropTargetCardIds)
			{
				if (!ContainsCardId(cardIds, highlightedCardId))
				{
					m_highlightRemovalBuffer.Add(highlightedCardId);
				}
			}

			for (int index = 0; index < m_highlightRemovalBuffer.Count; index++)
			{
				TabletopCardId cardId = m_highlightRemovalBuffer[index];
				m_highlightedDropTargetCardIds.Remove(cardId);
				SetCardHighlight(cardId, highlighted: false);
			}
			m_highlightRemovalBuffer.Clear();

			for (int index = 0; index < cardIds.Count; index++)
			{
				TabletopCardId cardId = cardIds[index];
				if (!cardId.IsValid)
				{
					throw new InvalidOperationException("牌桌目标高亮列表包含无效卡牌 ID。");
				}
				RequireLiveCardOrEmpty(cardId, "高亮");
				if (m_highlightedDropTargetCardIds.Add(cardId))
				{
					SetCardHighlight(cardId, highlighted: true);
				}
			}
		}

		internal void ClearDropTargetHighlights()
		{
			if (m_highlightedDropTargetCardIds.Count == 0)
			{
				return;
			}

			foreach (TabletopCardId cardId in m_highlightedDropTargetCardIds)
			{
				SetCardHighlight(cardId, highlighted: false);
			}
			m_highlightedDropTargetCardIds.Clear();
			m_highlightRemovalBuffer.Clear();
		}

		private static bool ContainsCardId(IReadOnlyList<TabletopCardId> cardIds, TabletopCardId cardId)
		{
			for (int index = 0; index < cardIds.Count; index++)
			{
				if (cardIds[index] == cardId)
				{
					return true;
				}
			}
			return false;
		}

		private void SetCardHighlight(TabletopCardId cardId, bool highlighted)
		{
			if (m_views.TryGetValue(cardId, out ViewEntry entry) && entry.View != null)
			{
				entry.View.SetHighlighted(highlighted);
			}
		}

		private void OnDestroy()
		{
			Unbind();
		}

		private void OnEnable()
		{
			EventKit.Type.Register<AbilitySystemDamageResolvedPresentationEvent>(
				OnAbilitySystemDamageResolved);
			EventKit.Type.Register<ScenarioFeedingPresentationEvent>(
				OnScenarioFeedingPresentation);
		}

		private void OnDisable()
		{
			EventKit.Type.UnRegister<AbilitySystemDamageResolvedPresentationEvent>(
				OnAbilitySystemDamageResolved);
			EventKit.Type.UnRegister<ScenarioFeedingPresentationEvent>(
				OnScenarioFeedingPresentation);
		}

		private void LateUpdate()
		{
			if (!IsBound)
			{
				return;
			}
			if (m_projectedCardRevision != m_tabletop.Cards.Revision ||
				m_projectedBattleRevision != m_tabletop.BattleRevision)
			{
				Refresh();
			}
			RefreshActionProgressViews();
			RefreshPackVendorSurfaces();
			RefreshBattleAttackAudio();
			RefreshProjectileViews();
			RefreshCardSmokeEffects();
			RefreshHitResultViews();
		}

		private void OnAbilitySystemDamageResolved(
			AbilitySystemDamageResolvedPresentationEvent damageEvent)
		{
			if (!IsBound)
			{
				return;
			}

			if (!TryFindDamageTargetView(damageEvent.TargetAbilitySystem, out ViewEntry entry))
			{
				return;
			}

			PlayBattleDamageAudio(damageEvent);
			TabletopCardView targetView = entry.View;
			if (targetView == null)
			{
				return;
			}
			if (!damageEvent.VisualFlags.HasFlag(EEffectVisualFlags.NoFloatingText))
			{
				RequestHitResultView(targetView, damageEvent);
			}
			if (!damageEvent.IsMissed && damageEvent.AppliedDamage > 0)
			{
				targetView.PlayHurtFeedback();
			}
		}

		internal void PlayPresentationCue(TabletopPresentationCueKind cue)
		{
			PlayPresentationCue(TabletopPresentationCue.Global(cue));
		}

		private void PlayPresentationCue(TabletopPresentationCue cue)
		{
			if (cue.Kind == TabletopPresentationCueKind.CardSpawn)
			{
				RecordCardSpawnPresentation(cue);
				return;
			}
			if (cue.Kind == TabletopPresentationCueKind.CameraFocus)
			{
				return;
			}
			if (cue.Kind == TabletopPresentationCueKind.CardHighlight)
			{
				PlayCardHighlight(cue);
				return;
			}
			if (m_settings == null)
			{
				throw new InvalidOperationException("牌桌视图缺少视图设置，无法播放牌桌表现反馈。");
			}

			PlayAudio(m_settings.GetPresentationAudio(cue.Kind));
			if (cue.Kind != TabletopPresentationCueKind.CardSmoke)
			{
				return;
			}
			if (!cue.HasTablePosition)
			{
				throw new InvalidOperationException("卡牌烟雾粒子反馈必须带有牌桌坐标。");
			}

			RequestCardSmokeEffect(cue.TablePosition);
		}

		private void RecordCardSpawnPresentation(TabletopPresentationCue cue)
		{
			if (!cue.HasCardId || !cue.HasTablePosition)
			{
				throw new InvalidOperationException("卡牌出生表现提示必须同时携带局内卡牌和牌桌坐标。");
			}
			if (m_settings == null)
			{
				throw new InvalidOperationException("牌桌视图缺少视图设置，无法记录卡牌出生表现。");
			}
			if (m_views.TryGetValue(cue.CardId, out ViewEntry existingEntry) && existingEntry.View != null)
			{
				throw new InvalidOperationException(
					$"卡牌 {cue.CardId} 已经有视图，不能再次登记出生表现。");
			}
			if (m_tabletop == null || !m_tabletop.Cards.TryGetCard(cue.CardId, out _))
			{
				throw new InvalidOperationException(
					$"卡牌出生表现引用了不属于当前牌桌的卡牌：{cue.CardId}。");
			}

			Vector3 localSpawnPosition = TabletopCoordinateSpace.ToLocalPosition(cue.TablePosition);
			localSpawnPosition.y = ResolveCardSpawnBaseHeight(cue) + cue.SpawnHeightOffset;
			m_cardSpawnPresentationOrigins[cue.CardId] = localSpawnPosition;
		}

		private float ResolveCardSpawnBaseHeight(TabletopPresentationCue cue)
		{
			if (!cue.UsesDragHeight)
			{
				return 0f;
			}
			if (!cue.HasSpawnOriginCardId)
			{
				throw new InvalidOperationException("StackCraft 拖拽高度出生提示缺少来源卡牌。");
			}
			if (!m_views.TryGetValue(cue.SpawnOriginCardId, out ViewEntry originEntry) ||
				originEntry.View == null)
			{
				throw new InvalidOperationException(
					$"StackCraft 拖拽高度出生提示找不到来源卡牌视图：{cue.SpawnOriginCardId}。");
			}

			return originEntry.View.transform.localPosition.y;
		}

		private void PlayCardHighlight(TabletopPresentationCue cue)
		{
			if (!cue.HasCardId)
			{
				throw new InvalidOperationException("卡牌提示高亮必须带有局内卡牌 ID。");
			}
			if (m_views.TryGetValue(cue.CardId, out ViewEntry entry) && entry.View != null)
			{
				entry.View.ShowPresentationHighlight(PresentationHighlightSeconds);
			}
		}

		private bool TryFindDamageTargetView(
			AbilitySystemCell targetAbilitySystem,
			out ViewEntry targetEntry)
		{
			foreach (ViewEntry entry in m_views.Values)
			{
				if (entry.TabletopCard is CharacterCard character &&
					ReferenceEquals(character.AbilitySystem, targetAbilitySystem))
				{
					targetEntry = entry;
					return true;
				}
			}

			targetEntry = null;
			return false;
		}

		private void RefreshActionProgressViews()
		{
			HashSet<ActionInstance> visibleActions = new HashSet<ActionInstance>();
			for (int actionIndex = 0; actionIndex < m_tabletop.ActiveActions.Count; actionIndex++)
			{
				ActionInstance action = m_tabletop.ActiveActions[actionIndex];
				if (action.State != ActionInstanceState.Running &&
					action.State != ActionInstanceState.Paused)
				{
					throw new InvalidOperationException(
						$"牌桌活动行动集合包含不可投影的状态 {action.State}：{action.ActionId}。");
				}
				if (!TryGetActionProgressAnchor(action, out TabletopCardId anchorCardId) ||
					!m_views.TryGetValue(anchorCardId, out ViewEntry anchorEntry) ||
					anchorEntry.View == null)
				{
					ReleaseActionProgressView(action);
					continue;
				}

				visibleActions.Add(action);
				if (!m_actionProgressViews.TryGetValue(action, out ActionProgressEntry progressEntry))
				{
					RequestActionProgressView(action, anchorCardId);
					continue;
				}
				ApplyActionProgressView(progressEntry, anchorEntry);
			}

			foreach (ActionInstance action in new List<ActionInstance>(m_actionProgressViews.Keys))
			{
				if (!visibleActions.Contains(action))
				{
					ReleaseActionProgressView(action);
				}
			}
		}

		private void RefreshBattleAreaViews()
		{
			HashSet<Battle> liveBattles = new HashSet<Battle>();
			for (int index = 0; index < m_tabletop.ActiveBattles.Count; index++)
			{
				Battle battle = m_tabletop.ActiveBattles[index];
				liveBattles.Add(battle);
				if (!m_battleAreaViews.TryGetValue(battle, out BattleAreaEntry entry))
				{
					RequestBattleAreaView(battle);
				}
				else
				{
					ApplyBattleAreaView(entry);
				}
			}

			foreach (Battle battle in new List<Battle>(m_battleAreaViews.Keys))
			{
				if (!liveBattles.Contains(battle))
				{
					ReleaseBattleAreaView(battle);
				}
			}
		}

		private void RefreshPackVendorSurfaces()
		{
			foreach (ViewEntry entry in m_views.Values)
			{
				ApplyPackVendorSurface(entry);
			}
		}

		private void ApplyPackVendorSurface(ViewEntry entry)
		{
			if (entry.View == null ||
				entry.TabletopCard is not PackVendorCard vendorCard ||
				entry.Definition is not PackVendorDefinition vendorDefinition)
			{
				return;
			}

			CardPackDefinition offeredPack = m_tabletop.ContentIndex.TryGet(
				vendorDefinition.OfferedPackId,
				out CardPackDefinition resolvedPack)
				? resolvedPack
				: throw new InvalidOperationException(
					$"卡包商贩 {vendorDefinition.ContentId} 引用的卡包 {vendorDefinition.OfferedPackId} 不在当前牌桌内容集合中。");
			entry.View.ApplyPackVendorSurface(
				offeredPack.DisplayName,
				vendorCard.RemainingPrice,
				offeredPack.GetCollectionProgress(m_tabletop.IsContentDiscovered));
		}

		private void ApplyRequiredCardBuyerCurrencyArtwork(ViewEntry entry)
		{
			if (entry.View == null || entry.Definition is not CardBuyerDefinition buyerDefinition)
			{
				return;
			}
			if (m_cardBuyerCurrencyArtwork.TryGetValue(buyerDefinition.ContentId, out Texture2D cachedArtwork))
			{
				entry.View.ApplyCardBuyerSurface(cachedArtwork);
				return;
			}
			CardDefinition currencyDefinition;
			if (!buyerDefinition.CurrencyCardId.IsValid ||
				!m_tabletop.ContentIndex.TryGet(
					buyerDefinition.CurrencyCardId,
					out currencyDefinition))
			{
				throw new InvalidOperationException(
					$"收购点 {buyerDefinition.ContentId} 缺少有效货币内容 {buyerDefinition.CurrencyCardId}，不能以缺少图标的状态进入牌桌。");
			}

			SoftAssetReference<Texture2D> artReference = currencyDefinition.Artwork;
			if (artReference == null || !artReference.IsValid())
			{
				throw new InvalidOperationException(
					$"收购点 {buyerDefinition.ContentId} 的货币 {currencyDefinition.ContentId} 缺少有效卡面图片，不能以缺少图标的状态进入牌桌。");
			}

			if (!m_cardBuyerCurrencyArtHandles.TryGetValue(
				buyerDefinition.ContentId,
				out ResourceHandle<Texture2D> handle))
			{
				handle = ResourceSystem.LoadAssetAsync<Texture2D>(artReference.Address);
				m_cardBuyerCurrencyArtHandles.Add(buyerDefinition.ContentId, handle);
			}

			Texture2D artwork = handle.WaitForCompletion();
			if (artwork == null)
			{
				throw new InvalidOperationException(
					$"收购点 {buyerDefinition.ContentId} 的货币图标加载结果为空：{artReference.Address}。");
			}
			m_cardBuyerCurrencyArtwork[buyerDefinition.ContentId] = artwork;
			entry.View.ApplyCardBuyerSurface(artwork);
		}

		private void RefreshProjectileViews()
		{
			for (int battleIndex = 0; battleIndex < m_tabletop.ActiveBattles.Count; battleIndex++)
			{
				Battle battle = m_tabletop.ActiveBattles[battleIndex];
				if (!battle.TryGetPendingAttackPresentation(out BattleAttackPresentation presentation) ||
					!ShouldShowProjectile(presentation.CombatTypeTagCode))
				{
					continue;
				}

				if (m_projectileViews.TryGetValue(battle, out ProjectileEntry existing))
				{
					if (existing.Sequence == presentation.Sequence)
					{
						continue;
					}
					ReleaseProjectileView(battle);
				}

				if (TryGetProjectileEndpoints(presentation, out Vector3 start, out Vector3 end))
				{
					RequestProjectileView(battle, presentation, start, end);
				}
			}

			foreach (Battle battle in new List<Battle>(m_projectileViews.Keys))
			{
				ProjectileEntry entry = m_projectileViews[battle];
				if (!IsActiveBattle(battle))
				{
					ReleaseProjectileView(battle);
					continue;
				}
				if (entry.View != null && !entry.View.IsPlaying)
				{
					ReleaseProjectileView(battle);
				}
				else if (entry.View == null &&
					(!battle.TryGetPendingAttackPresentation(out BattleAttackPresentation presentation) ||
					presentation.Sequence != entry.Sequence))
				{
					ReleaseProjectileView(battle);
				}
			}
		}

		private void RefreshCardSmokeEffects()
		{
			for (int index = m_cardSmokeEffects.Count - 1; index >= 0; index--)
			{
				CardSmokeEffectEntry entry = m_cardSmokeEffects[index];
				if (entry.View != null && !entry.View.IsPlaying)
				{
					ReleaseCardSmokeEffect(entry);
				}
			}
		}

		private void RefreshHitResultViews()
		{
			for (int index = m_hitResultViews.Count - 1; index >= 0; index--)
			{
				HitResultEntry entry = m_hitResultViews[index];
				if (entry.View != null && !entry.View.IsPlaying)
				{
					ReleaseHitResultView(entry);
				}
			}
		}

		private void RefreshBattleAttackAudio()
		{
			for (int battleIndex = 0; battleIndex < m_tabletop.ActiveBattles.Count; battleIndex++)
			{
				Battle battle = m_tabletop.ActiveBattles[battleIndex];
				if (battle.TryConsumeAttackStartedPresentation(out BattleAttackPresentation presentation))
				{
					PlayAudio(m_settings.GetAttackAudio(presentation.CombatTypeTagCode));
				}
			}
		}

		private void OnTabletopActionSettled(
			ContentId _,
			ActionSettlementResult result)
		{
			if (!isActiveAndEnabled)
			{
				return;
			}
			for (int cueIndex = 0; cueIndex < result.PresentationCues.Count; cueIndex++)
			{
				PlayPresentationCue(result.PresentationCues[cueIndex]);
			}
		}

		private void OnTabletopPresentationCueRequested(TabletopPresentationCue cue)
		{
			if (!isActiveAndEnabled)
			{
				return;
			}

			PlayPresentationCue(cue);
		}

		private void OnScenarioFeedingPresentation(ScenarioFeedingPresentationEvent feedingEvent)
		{
			if (!isActiveAndEnabled || !ReferenceEquals(feedingEvent.Tabletop, m_tabletop))
			{
				return;
			}

			PlayPresentationCue(TabletopPresentationCueKind.CardSwipe);
			StartCoroutine(PlayDelayedPresentationCue(
				TabletopPresentationCue.Global(TabletopPresentationCueKind.Eat),
				0.2f,
				feedingEvent.Tabletop));
			if (feedingEvent.FoodWillBeConsumed)
			{
				StartCoroutine(PlayDelayedPresentationCue(
					TabletopPresentationCue.AtTablePosition(
						TabletopPresentationCueKind.CardSmoke,
						feedingEvent.FoodPosition),
					0.2f,
					feedingEvent.Tabletop));
			}
		}

		private IEnumerator PlayDelayedPresentationCue(
			TabletopPresentationCue cue,
			float delaySeconds,
			global::Gameplay.Tabletop.Tabletop expectedTabletop)
		{
			yield return new WaitForSecondsRealtime(delaySeconds);
			if (isActiveAndEnabled && ReferenceEquals(expectedTabletop, m_tabletop))
			{
				PlayPresentationCue(cue);
			}
		}

		private void PlayBattleDamageAudio(AbilitySystemDamageResolvedPresentationEvent damageEvent)
		{
			if (!TryGetCurrentAttackCombatTypeTag(
				damageEvent.TargetAbilitySystem,
				out int combatTypeTagCode))
			{
				return;
			}

			if (damageEvent.IsSilent)
			{
				return;
			}
			if (damageEvent.IsMissed)
			{
				PlayAudio(m_settings.MissAudio);
				return;
			}

			PlayAudio(m_settings.GetHitAudio(combatTypeTagCode));
			if (damageEvent.IsCriticalHit)
			{
				PlayAudio(m_settings.CriticalAudio);
			}
		}

		private bool TryGetCurrentAttackCombatTypeTag(
			AbilitySystemCell targetAbilitySystem,
			out int combatTypeTagCode)
		{
			for (int battleIndex = 0; battleIndex < m_tabletop.ActiveBattles.Count; battleIndex++)
			{
				Battle battle = m_tabletop.ActiveBattles[battleIndex];
				if (!battle.TryGetExecutingAttackPresentation(out BattleAttackPresentation presentation))
				{
					continue;
				}
				if (!m_tabletop.Cards.TryGetCard(presentation.TargetCardId, out TabletopCard targetCard) ||
					targetCard is not CharacterCard targetCharacter ||
					!ReferenceEquals(targetCharacter.AbilitySystem, targetAbilitySystem))
				{
					continue;
				}

				combatTypeTagCode = presentation.CombatTypeTagCode;
				return true;
			}

			combatTypeTagCode = 0;
			return false;
		}

		private static void PlayAudio(AudioClipResolver audio)
		{
			if (audio != null)
			{
				EventKit.Type.Send(new AudioPlaybackRequestedEvent(audio));
			}
		}

		private static bool ShouldShowProjectile(int combatTypeTagCode)
		{
			return combatTypeTagCode == GAS.Runtime.XTag.Combat_Ranged ||
				combatTypeTagCode == GAS.Runtime.XTag.Combat_Magic;
		}

		private bool IsActiveBattle(Battle battle)
		{
			for (int index = 0; index < m_tabletop.ActiveBattles.Count; index++)
			{
				if (ReferenceEquals(m_tabletop.ActiveBattles[index], battle))
				{
					return true;
				}
			}
			return false;
		}

		private bool TryGetProjectileEndpoints(
			BattleAttackPresentation presentation,
			out Vector3 start,
			out Vector3 end)
		{
			start = default;
			end = default;
			if (!m_views.TryGetValue(presentation.SourceCardId, out ViewEntry sourceEntry) ||
				!m_views.TryGetValue(presentation.TargetCardId, out ViewEntry targetEntry) ||
				sourceEntry.View == null ||
				targetEntry.View == null)
			{
				return false;
			}

			Vector3 offset = Vector3.up * 0.05f;
			start = sourceEntry.View.transform.localPosition + offset;
			end = targetEntry.View.transform.localPosition + offset;
			return true;
		}

		private void RequestProjectileView(
			Battle battle,
			BattleAttackPresentation presentation,
			Vector3 start,
			Vector3 end)
		{
			ResourceHandle<GameObject> handle = ResourceSystem.InstantiateAsync<GameObject>(
				m_settings.ProjectileViewPrefab.Address,
				transform);
			ProjectileEntry entry = new ProjectileEntry(battle, presentation.Sequence, handle);
			m_projectileViews.Add(battle, entry);
			handle.RegisterCallback(instance =>
			{
				if (!m_projectileViews.TryGetValue(battle, out ProjectileEntry current) ||
					!current.InstanceHandle.Equals(handle))
				{
					if (handle.IsValid())
					{
						ResourceSystem.ReleaseInstance(handle);
					}
					return;
				}
				if (instance == null)
				{
					Debug.LogError(
						"投射物视图实例化返回空对象：" + m_settings.ProjectileViewPrefab.Address,
						this);
					ReleaseProjectileView(battle);
					return;
				}

				TabletopProjectileView component = instance.GetComponent<TabletopProjectileView>();
				if (component == null)
				{
					Debug.LogError(
						"投射物视图预制体缺少 TabletopProjectileView：" +
						m_settings.ProjectileViewPrefab.Address,
						instance);
					ReleaseProjectileView(battle);
					return;
				}

				current.View = component;
				component.Play(
					start,
					end,
					presentation.DurationSeconds,
					m_settings.ProjectileSortingOrder,
					presentation.CombatTypeTagCode);
			});
		}

		private void RequestCardSmokeEffect(Vector2 tablePosition)
		{
			ResourceHandle<GameObject> handle = ResourceSystem.InstantiateAsync<GameObject>(
				m_settings.CardSmokeEffectPrefab.Address,
				transform);
			CardSmokeEffectEntry entry = new CardSmokeEffectEntry(handle);
			m_cardSmokeEffects.Add(entry);
			handle.RegisterCallback(instance =>
			{
				if (!m_cardSmokeEffects.Contains(entry) || !entry.InstanceHandle.Equals(handle))
				{
					if (handle.IsValid())
					{
						ResourceSystem.ReleaseInstance(handle);
					}
					return;
				}
				if (instance == null)
				{
					Debug.LogError(
						"卡牌烟雾粒子实例化返回空对象：" + m_settings.CardSmokeEffectPrefab.Address,
						this);
					ReleaseCardSmokeEffect(entry);
					return;
				}

				TabletopCardSmokeEffectView component = instance.GetComponent<TabletopCardSmokeEffectView>();
				if (component == null)
				{
					Debug.LogError(
						"卡牌烟雾粒子预制体缺少 TabletopCardSmokeEffectView：" +
						m_settings.CardSmokeEffectPrefab.Address,
						instance);
					ReleaseCardSmokeEffect(entry);
					return;
				}

				entry.View = component;
				component.Play(tablePosition, m_settings.CardSmokeSortingOrder);
			});
		}

		private void RequestHitResultView(
			TabletopCardView targetView,
			AbilitySystemDamageResolvedPresentationEvent damageEvent)
		{
			if (targetView == null)
			{
				throw new ArgumentNullException(nameof(targetView));
			}

			Vector3 worldPosition = targetView.transform.TransformPoint(new Vector3(0.3f, 0.1f, 0.4f));
			Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
			ResourceHandle<GameObject> handle = ResourceSystem.InstantiateAsync<GameObject>(
				m_settings.HitResultViewPrefab.Address,
				transform);
			HitResultEntry entry = new HitResultEntry(handle);
			m_hitResultViews.Add(entry);
			handle.RegisterCallback(instance =>
			{
				if (!m_hitResultViews.Contains(entry) || !entry.InstanceHandle.Equals(handle))
				{
					if (handle.IsValid())
					{
						ResourceSystem.ReleaseInstance(handle);
					}
					return;
				}
				if (instance == null)
				{
					Debug.LogError(
						"命中结果视图实例化返回空对象：" + m_settings.HitResultViewPrefab.Address,
						this);
					ReleaseHitResultView(entry);
					return;
				}

				TabletopHitResultView component = instance.GetComponent<TabletopHitResultView>();
				if (component == null)
				{
					Debug.LogError(
						"命中结果视图预制体缺少 TabletopHitResultView：" +
						m_settings.HitResultViewPrefab.Address,
						instance);
					ReleaseHitResultView(entry);
					return;
				}

				entry.View = component;
				component.Play(
					damageEvent.AppliedDamage,
					damageEvent.IsMissed,
					damageEvent.IsCriticalHit,
					damageEvent.MatchupResult,
					localPosition,
					m_settings.HitResultSortingOrder);
			});
		}

		private void RequestBattleAreaView(Battle battle)
		{
			ResourceHandle<GameObject> handle = ResourceSystem.InstantiateAsync<GameObject>(
				m_settings.BattleAreaViewPrefab.Address,
				transform);
			BattleAreaEntry entry = new BattleAreaEntry(battle, handle);
			m_battleAreaViews.Add(battle, entry);
			handle.RegisterCallback(instance =>
			{
				if (!m_battleAreaViews.TryGetValue(battle, out BattleAreaEntry current) ||
					!current.InstanceHandle.Equals(handle))
				{
					if (handle.IsValid())
					{
						ResourceSystem.ReleaseInstance(handle);
					}
					return;
				}
				if (instance == null)
				{
					Debug.LogError(
						"战斗区域视图实例化返回空对象：" + m_settings.BattleAreaViewPrefab.Address,
						this);
					ReleaseBattleAreaView(battle);
					return;
				}

				TabletopBattleAreaView component = instance.GetComponent<TabletopBattleAreaView>();
				if (component == null)
				{
					Debug.LogError(
						"战斗区域视图预制体缺少 TabletopBattleAreaView：" +
						m_settings.BattleAreaViewPrefab.Address,
						instance);
					ReleaseBattleAreaView(battle);
					return;
				}

				current.View = component;
				component.Bind(battle);
				ApplyBattleAreaView(current);
			});
		}

		private void ApplyBattleAreaView(BattleAreaEntry entry)
		{
			if (entry.View != null)
			{
				entry.View.ApplyArea(
					m_tabletop.GetBattleArea(entry.Battle),
					m_settings.BattleBaseSortingOrder - 1);
			}
		}

		private static bool TryGetActionProgressAnchor(
			ActionInstance action,
			out TabletopCardId anchorCardId)
		{
			return action.TryGetFirstParticipantCardId(out anchorCardId);
		}

		private void RequestActionProgressView(
			ActionInstance action,
			TabletopCardId anchorCardId)
		{
			ResourceHandle<GameObject> handle = ResourceSystem.InstantiateAsync<GameObject>(
				m_settings.ActionProgressViewPrefab.Address,
				transform);
			ActionProgressEntry entry = new ActionProgressEntry(action, anchorCardId, handle);
			m_actionProgressViews.Add(action, entry);
			handle.RegisterCallback(delegate(GameObject instance)
			{
				if (!m_actionProgressViews.TryGetValue(action, out ActionProgressEntry current) ||
					!current.InstanceHandle.Equals(handle))
				{
					if (handle.IsValid())
					{
						ResourceSystem.ReleaseInstance(handle);
					}
				}
				else if (instance == null)
				{
					Debug.LogError(
						"行动进度视图实例化返回空对象：" + m_settings.ActionProgressViewPrefab.Address,
						this);
					ReleaseActionProgressView(action);
				}
				else
				{
					TabletopActionProgressView component =
						instance.GetComponent<TabletopActionProgressView>();
					if (component == null)
					{
						Debug.LogError(
							"行动进度视图预制体缺少 TabletopActionProgressView：" +
							m_settings.ActionProgressViewPrefab.Address,
							instance);
						ReleaseActionProgressView(action);
					}
					else
					{
						instance.SetActive(false);
						current.View = component;
					}
				}
			});
		}

		private static void ApplyActionProgressView(
			ActionProgressEntry progressEntry,
			ViewEntry anchorEntry)
		{
			if (progressEntry.View == null)
			{
				return;
			}
			if (progressEntry.View.transform.parent != anchorEntry.View.transform)
			{
				progressEntry.View.transform.SetParent(
					anchorEntry.View.transform,
					worldPositionStays: false);
			}
			progressEntry.View.Show(
				progressEntry.Action.Progress,
				progressEntry.Action.State == ActionInstanceState.Paused,
				anchorEntry.View.SortingOrder + 10);
		}

		private void RequestView(TabletopCard tabletopCard, CardDefinition contentAsset)
		{
			ResourceHandle<GameObject> handle = ResourceSystem.InstantiateAsync<GameObject>(
				m_settings.CardViewPrefab.Address,
				transform);
			ViewEntry entry = new ViewEntry(tabletopCard, contentAsset, handle);
			m_views.Add(tabletopCard.Id, entry);
			handle.RegisterCallback(delegate(GameObject instance)
			{
				if (!m_views.TryGetValue(tabletopCard.Id, out var value) || !value.InstanceHandle.Equals(handle))
				{
					if (handle.IsValid())
					{
						ResourceSystem.ReleaseInstance(handle);
					}
				}
				else if (instance == null)
				{
					Debug.LogError("卡牌视图实例化返回空对象：" + m_settings.CardViewPrefab.Address, this);
					ReleaseView(tabletopCard.Id);
				}
				else
				{
					TabletopCardView component = instance.GetComponent<TabletopCardView>();
					if (component == null)
					{
						Debug.LogError("卡牌视图预制体缺少 TabletopCardView：" + m_settings.CardViewPrefab.Address, instance);
						ReleaseView(tabletopCard.Id);
					}
					else
					{
						value.View = component;
						component.Bind(value.TabletopCard, value.Definition);
						ApplyRequiredCardFace(value);
						component.ApplySize(value.Definition.GetViewSize(m_tabletop.PlacementRules.Geometry.CardSize));
						component.SetHighlighted(m_highlightedDropTargetCardIds.Contains(value.TabletopCard.Id));
						if (TryConsumeCardSpawnPresentationOrigin(
							value.TabletopCard.Id,
							out Vector3 spawnOrigin))
						{
							ApplyCardSpawnPresentationPose(value, spawnOrigin);
						}
						else
						{
							ApplyCurrentPose(value);
						}
						ApplyPackVendorSurface(value);
						ApplyRequiredCardBuyerCurrencyArtwork(value);
					}
				}
			});
		}

		private bool TryConsumeCardSpawnPresentationOrigin(
			TabletopCardId cardId,
			out Vector3 spawnOrigin)
		{
			if (m_cardSpawnPresentationOrigins.TryGetValue(cardId, out spawnOrigin))
			{
				m_cardSpawnPresentationOrigins.Remove(cardId);
				return true;
			}
			spawnOrigin = default;
			return false;
		}

		private void ApplyCardSpawnPresentationPose(
			ViewEntry entry,
			Vector3 spawnOrigin)
		{
			TabletopCardStack stack = entry.TabletopCard.Stack;
			if (stack == null)
			{
				return;
			}

			int cardIndex = stack.IndexOf(entry.TabletopCard.Id);
			if (cardIndex < 0)
			{
				throw new InvalidOperationException(
					$"卡牌 {entry.TabletopCard.Id} 声明属于牌堆，但牌堆成员中不存在该卡牌。");
			}

			TabletopCardLayoutParameters layoutParameters =
				m_settings.CreateLayoutParameters(m_tabletop.PlacementRules.Geometry);
			entry.View.ApplyPose(
				new TabletopCardPose(spawnOrigin, layoutParameters.BaseSortingOrder),
				0f,
				m_settings.MoveEase);
			StartCoroutine(ApplyCardSpawnPresentationTargetPoseNextFrame(entry.TabletopCard.Id));
		}

		private IEnumerator ApplyCardSpawnPresentationTargetPoseNextFrame(TabletopCardId cardId)
		{
			yield return null;

			if (!IsBound ||
				!m_views.TryGetValue(cardId, out ViewEntry entry) ||
				entry.View == null)
			{
				yield break;
			}

			TabletopCardStack stack = entry.TabletopCard.Stack;
			if (stack == null)
			{
				yield break;
			}

			int cardIndex = stack.IndexOf(cardId);
			if (cardIndex < 0)
			{
				throw new InvalidOperationException(
					$"卡牌 {cardId} 声明属于牌堆，但牌堆成员中不存在该卡牌。");
			}

			ApplyCardPose(entry, stack, cardIndex);
		}

		private void ApplyCurrentPose(ViewEntry entry)
		{
			TabletopCardStack stack = entry.TabletopCard.Stack;
			if (stack != null)
			{
				int cardIndex = stack.IndexOf(entry.TabletopCard.Id);
				if (cardIndex < 0)
				{
					throw new InvalidOperationException(
						$"卡牌 {entry.TabletopCard.Id} 声明属于牌堆，但牌堆成员中不存在该卡牌。");
				}
				ApplyCardPose(entry, stack, cardIndex, immediate: true);
			}
		}

		private void ApplyCardPose(
			ViewEntry entry,
			TabletopCardStack stack,
			int cardIndex,
			bool immediate = false)
		{
			if (!(entry.View == null))
			{
				bool hasBattlePose = m_tabletop.TryGetBattlePose(
						entry.TabletopCard.Id,
						m_settings.BattleBaseSortingOrder,
						out TabletopCardPose battlePose);
				// StackCraft 的生命数字属于角色卡自身，不按牌堆位置额外隐藏。
				entry.View.SetCharacterStatusVisible(true);
				if (hasBattlePose)
				{
					entry.View.ApplyPose(
						battlePose,
						immediate ? 0f : m_settings.MoveDurationSeconds,
					m_settings.MoveEase);
				}
				else
				{
					int draggedSegmentStartIndex = (m_hasLocalDraggedStack ? stack.IndexOf(m_localDraggedStackAnchorCardId) : (-1));
					if (draggedSegmentStartIndex >= 0 && cardIndex >= draggedSegmentStartIndex)
					{
						int draggedSegmentIndex = cardIndex - draggedSegmentStartIndex;
						entry.View.ApplyDragPose(CreateLocalDraggedStackPose(stack, cardIndex, draggedSegmentIndex), draggedSegmentIndex == 0, m_settings.DragFollowSharpness);
					}
					else
					{
						entry.View.ApplyPose(
							TabletopCardLayout.Calculate(
								stack,
								cardIndex,
								m_settings.CreateLayoutParameters(m_tabletop.PlacementRules.Geometry)),
							immediate ? 0f : m_settings.MoveDurationSeconds,
						m_settings.MoveEase);
					}
				}
			}
		}

		private TabletopCardPose CreateLocalDraggedStackPose(
			TabletopCardStack stack,
			int cardIndex,
			int draggedSegmentIndex)
		{
			TabletopCardLayoutParameters layoutParameters =
				m_settings.CreateLayoutParameters(m_tabletop.PlacementRules.Geometry);
			if (draggedSegmentIndex == 0)
			{
				return new TabletopCardPose(
					TabletopCoordinateSpace.ToLocalPosition(stack.Position, m_settings.DragHeight),
					layoutParameters.BaseSortingOrder);
			}

			Vector3 targetPosition = TabletopCoordinateSpace.ToLocalPosition(stack.Position, m_settings.DragHeight) +
				layoutParameters.StackVisualStep * draggedSegmentIndex;
			TabletopCard precedingCard = stack.Cards[cardIndex - 1];
			if (m_views.TryGetValue(precedingCard.Id, out var precedingEntry) && precedingEntry.View != null)
			{
				targetPosition = precedingEntry.View.transform.localPosition + layoutParameters.StackVisualStep;
			}

			return new TabletopCardPose(
				targetPosition,
				layoutParameters.BaseSortingOrder);
		}

		private void ApplyLocalDraggedStackPose()
		{
			if (!IsBound)
			{
				return;
			}
			TabletopCardStack stack = m_tabletop.Cards.GetStackContaining(m_localDraggedStackAnchorCardId);
			ApplyAuthoritativeStackPose(stack);
		}

		private void ApplyAuthoritativeStackPose(TabletopCardStack stack)
		{
			for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
			{
				TabletopCard tabletopCard = stack.Cards[cardIndex];
				if (m_views.TryGetValue(tabletopCard.Id, out var entry))
				{
					ApplyCardPose(entry, stack, cardIndex);
				}
			}
		}

		private void ApplyRequiredCardFace(ViewEntry entry)
		{
			Material surfaceMaterial = LoadRequiredCardSurface(entry.Definition);
			Texture2D artwork = LoadRequiredCardArtwork(entry.Definition);
			entry.View.SetSurfaceMaterial(surfaceMaterial);
			entry.View.SetArtwork(artwork);
		}

		private Texture2D LoadRequiredCardArtwork(CardDefinition contentAsset)
		{
			SoftAssetReference<Texture2D> artReference = contentAsset.Artwork;
			if (artReference == null || !artReference.IsValid())
			{
				throw new InvalidOperationException(
					$"卡牌 {contentAsset.ContentId} 缺少有效卡面图片引用，不能以默认卡面进入牌桌。");
			}

			if (!m_artHandles.TryGetValue(contentAsset.ContentId, out ResourceHandle<Texture2D> handle))
			{
				handle = ResourceSystem.LoadAssetAsync<Texture2D>(artReference.Address);
				m_artHandles.Add(contentAsset.ContentId, handle);
			}

			Texture2D artwork = handle.WaitForCompletion();
			return artwork ?? throw new InvalidOperationException(
				$"卡牌 {contentAsset.ContentId} 的卡面图片加载结果为空：{artReference.Address}。");
		}

		private Material LoadRequiredCardSurface(CardDefinition contentAsset)
		{
			SoftAssetReference<Material> surfaceReference = contentAsset.CardSurface;
			if (surfaceReference == null || !surfaceReference.IsValid())
			{
				throw new InvalidOperationException(
					$"卡牌 {contentAsset.ContentId} 缺少有效表面材质引用，不能以默认角色材质进入牌桌。");
			}

			if (!m_surfaceHandles.TryGetValue(contentAsset.ContentId, out ResourceHandle<Material> handle))
			{
				handle = ResourceSystem.LoadAssetAsync<Material>(surfaceReference.Address);
				m_surfaceHandles.Add(contentAsset.ContentId, handle);
			}

			Material surfaceMaterial = handle.WaitForCompletion();
			return surfaceMaterial ?? throw new InvalidOperationException(
				$"卡牌 {contentAsset.ContentId} 的表面材质加载结果为空：{surfaceReference.Address}。");
		}

		private void ReleaseActionProgressView(ActionInstance action)
		{
			if (m_actionProgressViews.TryGetValue(action, out ActionProgressEntry entry))
			{
				m_actionProgressViews.Remove(action);
				if (entry.InstanceHandle.IsValid())
				{
					ResourceSystem.ReleaseInstance(entry.InstanceHandle);
				}
			}
		}

		private void ReleaseBattleAreaView(Battle battle)
		{
			if (m_battleAreaViews.TryGetValue(battle, out BattleAreaEntry entry))
			{
				m_battleAreaViews.Remove(battle);
				if (entry.InstanceHandle.IsValid())
				{
					ResourceSystem.ReleaseInstance(entry.InstanceHandle);
				}
			}
		}

		private void ReleaseProjectileView(Battle battle)
		{
			if (m_projectileViews.TryGetValue(battle, out ProjectileEntry entry))
			{
				m_projectileViews.Remove(battle);
				if (entry.InstanceHandle.IsValid())
				{
					ResourceSystem.ReleaseInstance(entry.InstanceHandle);
				}
			}
		}

		private void ReleaseCardSmokeEffect(CardSmokeEffectEntry entry)
		{
			if (!m_cardSmokeEffects.Remove(entry))
			{
				return;
			}
			if (entry.InstanceHandle.IsValid())
			{
				ResourceSystem.ReleaseInstance(entry.InstanceHandle);
			}
		}

		private void ReleaseAllCardSmokeEffects()
		{
			for (int index = m_cardSmokeEffects.Count - 1; index >= 0; index--)
			{
				ReleaseCardSmokeEffect(m_cardSmokeEffects[index]);
			}
		}

		private void ReleaseHitResultView(HitResultEntry entry)
		{
			if (!m_hitResultViews.Remove(entry))
			{
				return;
			}
			if (entry.InstanceHandle.IsValid())
			{
				ResourceSystem.ReleaseInstance(entry.InstanceHandle);
			}
		}

		private void ReleaseAllHitResultViews()
		{
			for (int index = m_hitResultViews.Count - 1; index >= 0; index--)
			{
				ReleaseHitResultView(m_hitResultViews[index]);
			}
		}

		private void ReleaseView(TabletopCardId cardId)
		{
			foreach (ActionInstance action in new List<ActionInstance>(m_actionProgressViews.Keys))
			{
				if (m_actionProgressViews[action].AnchorCardId == cardId)
				{
					ReleaseActionProgressView(action);
				}
			}
			if (m_views.TryGetValue(cardId, out var entry))
			{
				m_highlightedDropTargetCardIds.Remove(cardId);
				m_views.Remove(cardId);
				if (entry.InstanceHandle.IsValid())
				{
					ResourceSystem.ReleaseInstance(entry.InstanceHandle);
				}
			}
		}

		private CardDefinition GetRequiredCardDefinition(TabletopCard tabletopCard)
		{
			if (m_tabletop.ContentIndex.TryGet(tabletopCard.ContentId, out CardDefinition cardDefinition))
			{
				return cardDefinition;
			}
			throw new InvalidOperationException($"可堆叠卡牌 {tabletopCard.Id} 引用了非卡牌内容或缺失内容：{tabletopCard.ContentId}。");
		}

		private void RequireLiveCardOrEmpty(TabletopCardId cardId, string operation)
		{
			if (cardId.IsValid && (m_tabletop == null || !m_tabletop.Cards.TryGetCard(cardId, out _)))
			{
				throw new InvalidOperationException($"不能{operation}不属于当前牌桌的卡牌：{cardId}。");
			}
		}

		private void SetReadableCards(TabletopCardId hoveredCardId, TabletopCardId selectedCardId)
		{
			TabletopCardId previousReadableCardId = ReadableCardId;
			m_hoveredCardId = hoveredCardId;
			m_selectedCardId = selectedCardId;
			if (previousReadableCardId != ReadableCardId)
			{
				ReadableCardChanged?.Invoke();
			}
		}
	}
}
