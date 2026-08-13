using System;
using System.Collections.Generic;
using Gameplay.Tabletop.Actions;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>一个地区牌桌的卡牌、活动行动与权威随机事实。</summary>
	[Serializable]
	public sealed class TabletopSnapshot
	{
		[SerializeField]
		private TabletopCardStateSnapshot m_cards;

		[SerializeField]
		private ActionInstanceSnapshot[] m_activeActions;

		[SerializeField]
		private uint m_authoritativeRandomState;

		public TabletopCardStateSnapshot Cards => m_cards;

		public IReadOnlyList<ActionInstanceSnapshot> ActiveActions => m_activeActions;

		public uint AuthoritativeRandomState => m_authoritativeRandomState;

		internal TabletopSnapshot(
			TabletopCardStateSnapshot cards,
			ActionInstanceSnapshot[] activeActions,
			uint authoritativeRandomState)
		{
			m_cards = cards ?? throw new ArgumentNullException(nameof(cards));
			m_activeActions = activeActions ?? throw new ArgumentNullException(nameof(activeActions));
			m_authoritativeRandomState = authoritativeRandomState;
		}
	}
}
