namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌表现层可播放的模板反馈类型；它不是作者源 ID，也不参与规则结算。
	/// </summary>
	internal enum TabletopPresentationCueKind
	{
		CardPick,
		CardDrop,
		CardSwipe,
		Eat,
		Pop,
		CardSmoke,
		CameraFocus,
		CardHighlight,
		Coin,
		Coins,
		CashRegister
	}

	/// <summary>
	/// 已提交玩法事实附带的只读表现提示，可选择携带牌桌坐标或局内卡牌 ID 给表现层使用。
	/// </summary>
	internal readonly struct TabletopPresentationCue : System.IEquatable<TabletopPresentationCue>
	{
		private readonly UnityEngine.Vector2 m_tablePosition;
		private readonly TabletopCardId m_cardId;

		private TabletopPresentationCue(
			TabletopPresentationCueKind kind,
			bool hasTablePosition,
			UnityEngine.Vector2 tablePosition,
			bool hasCardId,
			TabletopCardId cardId)
		{
			Kind = kind;
			HasTablePosition = hasTablePosition;
			m_tablePosition = tablePosition;
			HasCardId = hasCardId;
			m_cardId = cardId;
		}

		public TabletopPresentationCueKind Kind { get; }

		public bool HasTablePosition { get; }

		public bool HasCardId { get; }

		public UnityEngine.Vector2 TablePosition
		{
			get
			{
				if (!HasTablePosition)
				{
					throw new System.InvalidOperationException(
						$"牌桌表现提示 {Kind} 没有关联牌桌坐标。");
				}
				return m_tablePosition;
			}
		}

		public TabletopCardId CardId
		{
			get
			{
				if (!HasCardId)
				{
					throw new System.InvalidOperationException(
						$"牌桌表现提示 {Kind} 没有关联局内卡牌。");
				}
				return m_cardId;
			}
		}

		public static TabletopPresentationCue Global(TabletopPresentationCueKind kind)
		{
			return new TabletopPresentationCue(kind, false, default, false, default);
		}

		public static TabletopPresentationCue AtTablePosition(
			TabletopPresentationCueKind kind,
			UnityEngine.Vector2 tablePosition)
		{
			if (!float.IsFinite(tablePosition.x) || !float.IsFinite(tablePosition.y))
			{
				throw new System.ArgumentException(
					$"牌桌表现提示 {kind} 的坐标必须是有限值。",
					nameof(tablePosition));
			}
			return new TabletopPresentationCue(kind, true, tablePosition, false, default);
		}

		public static TabletopPresentationCue AtCard(
			TabletopPresentationCueKind kind,
			TabletopCardId cardId)
		{
			if (!cardId.IsValid)
			{
				throw new System.ArgumentException(
					$"牌桌表现提示 {kind} 必须引用有效的局内卡牌。",
					nameof(cardId));
			}
			return new TabletopPresentationCue(kind, false, default, true, cardId);
		}

		public bool Equals(TabletopPresentationCue other)
		{
			return Kind == other.Kind &&
				HasTablePosition == other.HasTablePosition &&
				(!HasTablePosition || m_tablePosition == other.m_tablePosition) &&
				HasCardId == other.HasCardId &&
				(!HasCardId || m_cardId == other.m_cardId);
		}

		public override bool Equals(object obj)
		{
			return obj is TabletopPresentationCue other && Equals(other);
		}

		public override int GetHashCode()
		{
			return System.HashCode.Combine(Kind, HasTablePosition, m_tablePosition, HasCardId, m_cardId);
		}
	}
}
