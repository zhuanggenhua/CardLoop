using System;
using System.Collections.Generic;
using GameCore;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 把可堆叠卡牌状态投影为 Unity 卡牌视图的表现组件。
    /// 它只读取卡牌状态和卡牌作者源，使用项目现有 ResourceSystem 创建/释放对象。
    /// </summary>
    public sealed class TabletopCardViewProjector : MonoBehaviour
    {
        [Header("表现配置")]
        [SerializeField, InspectorName("表现设置")]
        [Tooltip("提供卡牌视图预制体地址和堆栈布局参数的 Gameplay 表现配置。")]
        private TabletopCardPresentationSettings m_settings;

        [SerializeField, InspectorName("视图根节点")]
        [Tooltip("所有卡牌视图的父节点。为空时使用当前对象作为根节点。")]
        private Transform m_viewRoot;

        private readonly Dictionary<TabletopCardId, ViewEntry> m_views = new();
        private readonly Dictionary<ContentId, ResourceHandle<Sprite>> m_artHandles = new();

        private TabletopCardState m_state;
        private ContentIndex m_contentIndex;
        private TabletopCardId m_dragPreviewCardId;
        private TabletopCardId m_highlightedTargetCardId;
        private Vector2 m_dragPreviewPosition;
        private bool m_hasDragPreview;

        /// <summary>
        /// 当前是否已经绑定可堆叠卡牌状态和内容索引。
        /// </summary>
        public bool IsBound => m_state != null && m_contentIndex != null;

        /// <summary>
        /// 绑定一局可堆叠卡牌状态和对应内容索引，并立即刷新现有投影。
        /// 绑定只建立表现关系；卡牌与堆栈修改仍必须经过 TabletopCardState。
        /// </summary>
        public void Bind(TabletopCardState state, ContentIndex contentIndex)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (contentIndex == null)
            {
                throw new ArgumentNullException(nameof(contentIndex));
            }

            if (m_settings == null)
            {
                throw new InvalidOperationException("卡牌视图投影缺少表现设置资产。");
            }

            if (!m_settings.CardViewPrefab.IsValid())
            {
                throw new InvalidOperationException("卡牌视图投影缺少有效的卡牌视图预制体地址。");
            }

            Clear();
            m_state = state;
            m_contentIndex = contentIndex;
            Refresh();
        }

        /// <summary>
        /// 根据当前卡牌状态创建、更新和移除视图。
        /// 异步资源尚未完成时保留待创建记录，重复刷新不会重复实例化同一张局内卡牌。
        /// </summary>
        public void Refresh()
        {
            if (!IsBound)
            {
                return;
            }

            var liveCardIds = new HashSet<TabletopCardId>();
            for (int stackIndex = 0; stackIndex < m_state.Stacks.Count; stackIndex++)
            {
                TabletopCardStack stack = m_state.Stacks[stackIndex];
                for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
                {
                    TabletopCard tabletopCard = stack.Cards[cardIndex];
                    liveCardIds.Add(tabletopCard.Id);

                    if (!m_views.TryGetValue(tabletopCard.Id, out ViewEntry entry))
                    {
                        CardDefinition contentAsset = GetRequiredCardDefinition(tabletopCard);
                        RequestView(tabletopCard, contentAsset);

                        continue;
                    }

                    ApplyCardPose(entry, stack, cardIndex);
                    EnsureArtwork(entry.ContentAsset);
                }
            }

            foreach (TabletopCardId cardId in new List<TabletopCardId>(m_views.Keys))
            {
                if (!liveCardIds.Contains(cardId))
                {
                    ReleaseView(cardId);
                }
            }
        }

        /// <summary>
        /// 释放所有视图实例和由本投影器持有的图片句柄，但保留绑定关系以便下一次 Refresh 重建。
        /// </summary>
        public void Clear()
        {
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
            m_dragPreviewCardId = default;
            m_highlightedTargetCardId = default;
            m_dragPreviewPosition = default;
        }

        /// <summary>
        /// 设置一段不修改权威卡牌状态的拖拽预览。
        /// 从指定卡牌开始到堆栈顶部的视图会跟随临时位置，其余成员仍读取正式状态布局。
        /// </summary>
        public void SetDragPreview(TabletopCardId cardId, Vector2 tablePosition)
        {
            if (!cardId.IsValid)
            {
                throw new ArgumentException("拖拽预览必须引用有效的局内卡牌。", nameof(cardId));
            }

            if (!float.IsFinite(tablePosition.x) || !float.IsFinite(tablePosition.y))
            {
                throw new ArgumentException("拖拽预览位置必须是有限坐标。", nameof(tablePosition));
            }

            m_dragPreviewCardId = cardId;
            m_dragPreviewPosition = tablePosition;
            m_hasDragPreview = true;
            ApplyDragPreview();
        }

        /// <summary>
        /// 清除拖拽预览并恢复为权威卡牌状态布局。
        /// </summary>
        public void ClearDragPreview()
        {
            if (!m_hasDragPreview)
            {
                return;
            }

            TabletopCardStack previewStack = null;
            if (IsBound)
            {
                m_state.TryGetStackContaining(m_dragPreviewCardId, out previewStack);
            }
            m_hasDragPreview = false;
            m_dragPreviewCardId = default;
            m_dragPreviewPosition = default;

            if (previewStack != null)
            {
                ApplyAuthoritativeStackPose(previewStack);
            }
        }

        /// <summary>
        /// 设置当前拖拽的空间候选高亮。无效卡牌 ID 表示清除高亮。
        /// 本方法不校验规则可接受性，也不提交任何牌桌变化。
        /// </summary>
        public void SetDropTargetHighlight(TabletopCardId cardId)
        {
            if (m_highlightedTargetCardId == cardId)
            {
                return;
            }

            if (m_views.TryGetValue(m_highlightedTargetCardId, out ViewEntry previous) && previous.View != null)
            {
                previous.View.SetHighlighted(false);
            }

            m_highlightedTargetCardId = cardId;
            if (cardId.IsValid && m_views.TryGetValue(cardId, out ViewEntry current) && current.View != null)
            {
                current.View.SetHighlighted(true);
            }
        }

        private void OnDestroy()
        {
            // 资源句柄由投影器持有，组件销毁时必须统一释放，不能等待作者资产或场景对象代管。
            Clear();
            m_state = null;
            m_contentIndex = null;
        }

        private void RequestView(TabletopCard tabletopCard, CardDefinition contentAsset)
        {
            Transform root = m_viewRoot == null ? transform : m_viewRoot;
            ResourceHandle<GameObject> handle = ResourceSystem.InstantiateAsync<GameObject>(
                m_settings.CardViewPrefab.Address,
                root);
            var entry = new ViewEntry(tabletopCard, contentAsset, handle);
            m_views.Add(tabletopCard.Id, entry);

            handle.RegisterCallback(instance =>
            {
                if (!m_views.TryGetValue(tabletopCard.Id, out ViewEntry current) ||
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
                    Debug.LogError($"卡牌视图实例化返回空对象：{m_settings.CardViewPrefab.Address}", this);
                    ReleaseView(tabletopCard.Id);
                    return;
                }

                TabletopCardView view = instance.GetComponent<TabletopCardView>();
                if (view == null)
                {
                    Debug.LogError(
                        $"卡牌视图预制体缺少 {nameof(TabletopCardView)}：{m_settings.CardViewPrefab.Address}",
                        instance);
                    ReleaseView(tabletopCard.Id);
                    return;
                }

                current.View = view;
                view.Bind(current.TabletopCard, current.ContentAsset);
                view.SetHighlighted(current.TabletopCard.Id == m_highlightedTargetCardId);
                ApplyCurrentPose(current);
                EnsureArtwork(current.ContentAsset);
            });
        }

        private void ApplyCurrentPose(ViewEntry entry)
        {
            if (!TryFindPose(entry.TabletopCard.Id, out TabletopCardStack stack, out int cardIndex))
            {
                return;
            }

            ApplyCardPose(entry, stack, cardIndex);
        }

        private bool TryFindPose(
            TabletopCardId cardId,
            out TabletopCardStack stack,
            out int cardIndex)
        {
            for (int stackIndex = 0; stackIndex < m_state.Stacks.Count; stackIndex++)
            {
                TabletopCardStack candidate = m_state.Stacks[stackIndex];
                int candidateIndex = candidate.IndexOf(cardId);
                if (candidateIndex >= 0)
                {
                    stack = candidate;
                    cardIndex = candidateIndex;
                    return true;
                }
            }

            stack = null;
            cardIndex = -1;
            return false;
        }

        private void ApplyCardPose(ViewEntry entry, TabletopCardStack stack, int cardIndex)
        {
            if (entry.View == null)
            {
                return;
            }

            int previewStartIndex = m_hasDragPreview ? stack.IndexOf(m_dragPreviewCardId) : -1;
            if (previewStartIndex >= 0 && cardIndex >= previewStartIndex)
            {
                int previewCardIndex = cardIndex - previewStartIndex;
                entry.View.ApplyDragPose(
                    TabletopCardLayout.Calculate(
                        m_dragPreviewPosition,
                        previewCardIndex,
                        m_settings.LayoutParameters),
                    immediate: previewCardIndex == 0,
                    followSharpness: m_settings.DragFollowSharpness);
                return;
            }

            entry.View.ApplyPose(
                TabletopCardLayout.Calculate(
                    stack,
                    cardIndex,
                    m_settings.LayoutParameters));
        }

        private void ApplyDragPreview()
        {
            if (!IsBound)
            {
                return;
            }

            TabletopCardStack stack = m_state.GetStackContaining(m_dragPreviewCardId);
            int previewStartIndex = stack.IndexOf(m_dragPreviewCardId);
            for (int cardIndex = previewStartIndex; cardIndex < stack.Cards.Count; cardIndex++)
            {
                TabletopCard tabletopCard = stack.Cards[cardIndex];
                if (!m_views.TryGetValue(tabletopCard.Id, out ViewEntry entry) || entry.View == null)
                {
                    continue;
                }

                int previewCardIndex = cardIndex - previewStartIndex;
                entry.View.ApplyDragPose(
                    TabletopCardLayout.Calculate(
                        m_dragPreviewPosition,
                        previewCardIndex,
                        m_settings.LayoutParameters),
                    immediate: previewCardIndex == 0,
                    followSharpness: m_settings.DragFollowSharpness);
            }
        }

        private void ApplyAuthoritativeStackPose(TabletopCardStack stack)
        {
            for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
            {
                TabletopCard tabletopCard = stack.Cards[cardIndex];
                if (m_views.TryGetValue(tabletopCard.Id, out ViewEntry entry))
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
                // 句柄归投影器按内容 ID 统一持有，避免作者资产上的 SoftAssetReference 承担场景视图生命周期。
                ResourceHandle<Sprite> handle = ResourceSystem.LoadAssetAsync<Sprite>(artReference.Address);
                m_artHandles.Add(contentAsset.ContentId, handle);
                handle.RegisterCallback(artwork => ApplyArtwork(contentAsset.ContentId, artwork));
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"无法加载卡面图片，内容 ID：{contentAsset.ContentId}，资源地址：{artReference.Address}。\n{exception}",
                    this);
            }
        }

        private void ApplyArtwork(ContentId contentId, Sprite artwork)
        {
            foreach (ViewEntry entry in m_views.Values)
            {
                if (entry.ContentAsset.ContentId == contentId && entry.View != null)
                {
                    entry.View.SetArtwork(artwork);
                }
            }
        }

        private void ReleaseView(TabletopCardId cardId)
        {
            if (!m_views.TryGetValue(cardId, out ViewEntry entry))
            {
                return;
            }

            m_views.Remove(cardId);
            if (entry.InstanceHandle.IsValid())
            {
                ResourceSystem.ReleaseInstance(entry.InstanceHandle);
            }
        }

        /// <summary>
        /// 仅供当前投影器关联局内卡牌、作者资产、资源句柄和已完成视图的内部记录。
        /// 成员使用 internal 是为了让外层类型访问；私有嵌套类型不会暴露成程序集或模块 API。
        /// </summary>
        private sealed class ViewEntry
        {
            internal ViewEntry(
                TabletopCard tabletopCard,
                CardDefinition contentAsset,
                ResourceHandle<GameObject> instanceHandle)
            {
                TabletopCard = tabletopCard;
                ContentAsset = contentAsset;
                InstanceHandle = instanceHandle;
            }

            internal TabletopCard TabletopCard { get; }
            internal CardDefinition ContentAsset { get; }
            internal ResourceHandle<GameObject> InstanceHandle { get; }
            internal TabletopCardView View { get; set; }
        }

        private CardDefinition GetRequiredCardDefinition(TabletopCard tabletopCard)
        {
            if (m_contentIndex.TryGet(tabletopCard.ContentId, out CardDefinition cardDefinition))
            {
                return cardDefinition;
            }

            throw new InvalidOperationException(
                $"可堆叠卡牌 {tabletopCard.Id} 引用了非卡牌内容或缺失内容：{tabletopCard.ContentId}。");
        }
    }
}
