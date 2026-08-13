using System;
using System.Collections.Generic;
using GameCore;
using Gameplay.Content;
using Gameplay.Tabletop.Actions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 当前牌桌的 Unity 表现对象，统一管理卡牌、行动进度、资源句柄和临时交互表现。
	/// </summary>
	public sealed class TabletopView : MonoBehaviour
	{
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

		[Header("牌桌表现")]
		[SerializeField]
		[LabelText("视图设置")]
		[Tooltip("提供卡牌视图资源、渲染层级和拖拽手感；规则几何由绑定的牌桌提供。")]
		private TabletopViewSettings m_settings;

		private readonly Dictionary<TabletopCardId, ViewEntry> m_views = new Dictionary<TabletopCardId, ViewEntry>();

		private readonly Dictionary<ActionInstance, ActionProgressEntry> m_actionProgressViews =
			new Dictionary<ActionInstance, ActionProgressEntry>();

		private readonly Dictionary<ContentId, ResourceHandle<Sprite>> m_artHandles = new Dictionary<ContentId, ResourceHandle<Sprite>>();

		private Tabletop m_tabletop;

		private TabletopCardId m_dragPreviewCardId;

		private TabletopCardId m_highlightedTargetCardId;

		private TabletopCardId m_hoveredCardId;

		private TabletopCardId m_selectedCardId;

		private Vector2 m_dragPreviewPosition;

		private bool m_hasDragPreview;

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
			if (!float.IsFinite(m_settings.DragFollowSharpness) || m_settings.DragFollowSharpness <= 0f)
			{
				throw new InvalidOperationException("牌桌视图设置的拖拽跟随锐度必须是有限正数。");
			}
			m_settings.CreateLayoutParameters(tabletop.PlacementRules.Geometry);
			Unbind();
			m_tabletop = tabletop;
			Refresh();
		}

		private void Refresh()
		{
			if (!IsBound)
			{
				return;
			}
			TabletopCards cards = m_tabletop.Cards;
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
						EnsureArtwork(entry.Definition);
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
			SetReadableCards(
				liveCardIds.Contains(m_hoveredCardId) ? m_hoveredCardId : default,
				liveCardIds.Contains(m_selectedCardId) ? m_selectedCardId : default);
			m_projectedCardRevision = cards.Revision;
			m_projectedBattleRevision = m_tabletop.BattleRevision;
		}

		/// <summary>
		/// 解除权威牌桌绑定，并释放该绑定创建的全部视图与资源句柄。
		/// </summary>
		public void Unbind()
		{
			SetReadableCards(default, default);
			foreach (ActionInstance action in new List<ActionInstance>(m_actionProgressViews.Keys))
			{
				ReleaseActionProgressView(action);
			}
			foreach (TabletopCardId cardId in new List<TabletopCardId>(m_views.Keys))
			{
				ReleaseView(cardId);
			}
			foreach (ResourceHandle<Sprite> handle in m_artHandles.Values)
			{
				ResourceSystem.ReleaseAsset(handle);
			}
			m_artHandles.Clear();
			m_hasDragPreview = false;
			m_dragPreviewCardId = default(TabletopCardId);
			m_highlightedTargetCardId = default(TabletopCardId);
			m_dragPreviewPosition = default(Vector2);
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

		internal void SetDragPreview(TabletopCardId cardId, Vector2 tablePosition)
		{
			if (!cardId.IsValid)
			{
				throw new ArgumentException("拖拽预览必须引用有效的局内卡牌。", "cardId");
			}
			if (!float.IsFinite(tablePosition.x) || !float.IsFinite(tablePosition.y))
			{
				throw new ArgumentException("拖拽预览位置必须是有限坐标。", "tablePosition");
			}
			m_dragPreviewCardId = cardId;
			m_dragPreviewPosition = tablePosition;
			m_hasDragPreview = true;
			ApplyDragPreview();
		}

		internal void ClearDragPreview()
		{
			if (m_hasDragPreview)
			{
				TabletopCardStack previewStack = null;
				if (IsBound)
				{
					m_tabletop.Cards.TryGetStackContaining(m_dragPreviewCardId, out previewStack);
				}
				m_hasDragPreview = false;
				m_dragPreviewCardId = default(TabletopCardId);
				m_dragPreviewPosition = default(Vector2);
				if (previewStack != null)
				{
					ApplyAuthoritativeStackPose(previewStack);
				}
			}
		}

		internal void SetDropTargetHighlight(TabletopCardId cardId)
		{
			if (!(m_highlightedTargetCardId == cardId))
			{
				if (m_views.TryGetValue(m_highlightedTargetCardId, out var previous) && previous.View != null)
				{
					previous.View.SetHighlighted(highlighted: false);
				}
				m_highlightedTargetCardId = cardId;
				if (cardId.IsValid && m_views.TryGetValue(cardId, out var current) && current.View != null)
				{
					current.View.SetHighlighted(highlighted: true);
				}
			}
		}

		private void OnDestroy()
		{
			Unbind();
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
		}

		private void RefreshActionProgressViews()
		{
			Dictionary<TabletopCardId, int> nextStackedIndices =
				new Dictionary<TabletopCardId, int>();
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

				int stackedIndex = 0;
				if (nextStackedIndices.TryGetValue(anchorCardId, out int nextStackedIndex))
				{
					stackedIndex = nextStackedIndex;
				}
				nextStackedIndices[anchorCardId] = stackedIndex + 1;
				visibleActions.Add(action);
				if (!m_actionProgressViews.TryGetValue(action, out ActionProgressEntry progressEntry))
				{
					RequestActionProgressView(action, anchorCardId);
					continue;
				}
				ApplyActionProgressView(progressEntry, anchorEntry, stackedIndex);
			}

			foreach (ActionInstance action in new List<ActionInstance>(m_actionProgressViews.Keys))
			{
				if (!visibleActions.Contains(action))
				{
					ReleaseActionProgressView(action);
				}
			}
		}

		private static bool TryGetActionProgressAnchor(
			ActionInstance action,
			out TabletopCardId anchorCardId)
		{
			for (int bindingIndex = 0; bindingIndex < action.Bindings.Count; bindingIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = action.Bindings[bindingIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					if (cardIds[cardIndex].IsValid)
					{
						anchorCardId = cardIds[cardIndex];
						return true;
					}
				}
			}
			anchorCardId = default(TabletopCardId);
			return false;
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
			ViewEntry anchorEntry,
			int stackedIndex)
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
				stackedIndex,
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
						component.ApplySize(m_tabletop.PlacementRules.Geometry.CardSize);
						component.SetHighlighted(value.TabletopCard.Id == m_highlightedTargetCardId);
						ApplyCurrentPose(value);
						EnsureArtwork(value.Definition);
					}
				}
			});
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
				ApplyCardPose(entry, stack, cardIndex);
			}
		}

		private void ApplyCardPose(ViewEntry entry, TabletopCardStack stack, int cardIndex)
		{
			if (!(entry.View == null))
			{
				bool hasBattlePose = m_tabletop.TryGetBattlePose(
						entry.TabletopCard.Id,
						m_settings.BattleBaseSortingOrder,
						out TabletopCardPose battlePose);
				entry.View.SetCharacterStatusVisible(
					hasBattlePose || cardIndex == stack.Cards.Count - 1);
				if (hasBattlePose)
				{
					entry.View.ApplyPose(battlePose);
				}
				else
				{
					int previewStartIndex = (m_hasDragPreview ? stack.IndexOf(m_dragPreviewCardId) : (-1));
					if (previewStartIndex >= 0 && cardIndex >= previewStartIndex)
					{
						int previewCardIndex = cardIndex - previewStartIndex;
						entry.View.ApplyDragPose(TabletopCardLayout.Calculate(m_dragPreviewPosition, previewCardIndex, m_settings.CreateLayoutParameters(m_tabletop.PlacementRules.Geometry)), previewCardIndex == 0, m_settings.DragFollowSharpness);
					}
					else
					{
						entry.View.ApplyPose(TabletopCardLayout.Calculate(stack, cardIndex, m_settings.CreateLayoutParameters(m_tabletop.PlacementRules.Geometry)));
					}
				}
			}
		}

		private void ApplyDragPreview()
		{
			if (!IsBound)
			{
				return;
			}
			TabletopCardStack stack = m_tabletop.Cards.GetStackContaining(m_dragPreviewCardId);
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

		private void EnsureArtwork(CardDefinition contentAsset)
		{
			SoftAssetReference<Sprite> artReference = contentAsset.Artwork;
			if (artReference == null || !artReference.IsValid() || m_artHandles.ContainsKey(contentAsset.ContentId))
			{
				return;
			}
			try
			{
				ResourceHandle<Sprite> handle = ResourceSystem.LoadAssetAsync<Sprite>(artReference.Address);
				m_artHandles.Add(contentAsset.ContentId, handle);
				handle.RegisterCallback(delegate(Sprite artwork)
				{
					ApplyArtwork(contentAsset.ContentId, artwork);
				});
			}
			catch (Exception arg)
			{
				Debug.LogError($"无法加载卡面图片，内容 ID：{contentAsset.ContentId}，资源地址：{artReference.Address}。\n{arg}", this);
			}
		}

		private void ApplyArtwork(ContentId contentId, Sprite artwork)
		{
			foreach (ViewEntry entry in m_views.Values)
			{
				if (entry.Definition.ContentId.Equals(contentId) && entry.View != null)
				{
					entry.View.SetArtwork(artwork);
				}
			}
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
