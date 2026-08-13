using System;
using System.Collections.Generic;
using UnityEngine;
using Gameplay.Content;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌卡牌状态的可序列化快照，保留实例 ID 与牌堆事实。
	/// 下一分配号属于剧本单局共享序列，不在每个地区重复保存。
	/// </summary>
	[Serializable]
	public sealed class TabletopCardStateSnapshot
	{
		[SerializeField]
		private TabletopCardStackSnapshot[] m_stacks;

		public IReadOnlyList<TabletopCardStackSnapshot> Stacks => m_stacks;

		internal TabletopCardStateSnapshot(TabletopCardStackSnapshot[] stacks)
		{
			m_stacks = stacks ?? throw new ArgumentNullException("stacks");
		}
	}

	/// <summary>
	/// 牌桌快照中的单个牌堆事实。
	/// </summary>
	[Serializable]
	public sealed class TabletopCardStackSnapshot
	{
		[SerializeField]
		private Vector2 m_position;

		[SerializeField]
		private bool m_isPlacementLocked;

		[SerializeField]
		private TabletopCardSnapshot[] m_cards;

		public Vector2 Position => m_position;

		public bool IsPlacementLocked => m_isPlacementLocked;

		public IReadOnlyList<TabletopCardSnapshot> Cards => m_cards;

		internal TabletopCardStackSnapshot(Vector2 position, bool isPlacementLocked, TabletopCardSnapshot[] cards)
		{
			m_position = position;
			m_isPlacementLocked = isPlacementLocked;
			m_cards = cards ?? throw new ArgumentNullException("cards");
		}
	}

	/// <summary>
	/// 牌桌快照中的单张卡牌实例事实。
	/// </summary>
	[Serializable]
	public sealed class TabletopCardSnapshot
	{
		[SerializeField]
		private ulong m_cardId;

		[SerializeField]
		private ContentId m_contentId;

		public TabletopCardId CardId => new TabletopCardId(m_cardId);

		public ContentId ContentId => m_contentId;

		internal TabletopCardSnapshot(TabletopCardId cardId, ContentId contentId)
		{
			m_cardId = cardId.Value;
			m_contentId = contentId;
		}
	}
}
