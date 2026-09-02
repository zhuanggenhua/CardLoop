namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌表现层可播放的模板反馈类型；它不是作者源 ID，也不参与规则结算。
	/// </summary>
	internal enum TabletopPresentationCueKind
	{
		CardSpawn,
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
		private readonly TabletopCardId m_spawnOriginCardId;
		private readonly bool m_usesDragHeight;
		private readonly float m_spawnHeightOffset;

		private TabletopPresentationCue(
			TabletopPresentationCueKind kind,
			bool hasTablePosition,
			UnityEngine.Vector2 tablePosition,
			bool hasCardId,
			TabletopCardId cardId,
			TabletopCardId spawnOriginCardId,
			bool usesDragHeight,
			float spawnHeightOffset)
		{
			Kind = kind;
			HasTablePosition = hasTablePosition;
			m_tablePosition = tablePosition;
			HasCardId = hasCardId;
			m_cardId = cardId;
			m_spawnOriginCardId = spawnOriginCardId;
			m_usesDragHeight = usesDragHeight;
			m_spawnHeightOffset = spawnHeightOffset;
		}

		public TabletopPresentationCueKind Kind { get; }

		public bool HasTablePosition { get; }

		public bool HasCardId { get; }

		public bool HasSpawnOriginCardId => m_spawnOriginCardId.IsValid;

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

		public TabletopCardId SpawnOriginCardId
		{
			get
			{
				if (!HasSpawnOriginCardId)
				{
					throw new System.InvalidOperationException(
						$"牌桌表现提示 {Kind} 没有关联出生来源卡牌。");
				}
				return m_spawnOriginCardId;
			}
		}

		public bool UsesDragHeight
		{
			get
			{
				if (Kind != TabletopPresentationCueKind.CardSpawn)
				{
					throw new System.InvalidOperationException(
						$"牌桌表现提示 {Kind} 不是卡牌出生提示，没有拖拽高度语义。");
				}
				return m_usesDragHeight;
			}
		}

		public float SpawnHeightOffset
		{
			get
			{
				if (Kind != TabletopPresentationCueKind.CardSpawn)
				{
					throw new System.InvalidOperationException(
						$"牌桌表现提示 {Kind} 不是卡牌出生提示，没有出生高度偏移语义。");
				}
				return m_spawnHeightOffset;
			}
		}

		public static TabletopPresentationCue Global(TabletopPresentationCueKind kind)
		{
			return new TabletopPresentationCue(kind, false, default, false, default, default, false, 0f);
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
			return new TabletopPresentationCue(kind, true, tablePosition, false, default, default, false, 0f);
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
			return new TabletopPresentationCue(kind, false, default, true, cardId, default, false, 0f);
		}

		public static TabletopPresentationCue CardSpawn(
			TabletopCardId cardId,
			UnityEngine.Vector2 tablePosition,
			bool usesDragHeight,
			float spawnHeightOffset = 0f,
			TabletopCardId spawnOriginCardId = default)
		{
			if (!cardId.IsValid)
			{
				throw new System.ArgumentException(
					"卡牌出生表现提示必须引用有效的局内卡牌。",
					nameof(cardId));
			}
			if (!float.IsFinite(tablePosition.x) || !float.IsFinite(tablePosition.y))
			{
				throw new System.ArgumentException(
					"卡牌出生表现提示的牌桌坐标必须是有限值。",
					nameof(tablePosition));
			}
			if (!float.IsFinite(spawnHeightOffset) || spawnHeightOffset < 0f)
			{
				throw new System.ArgumentException(
					"卡牌出生表现提示的高度偏移必须是大于等于 0 的有限值。",
					nameof(spawnHeightOffset));
			}
			if (usesDragHeight && !spawnOriginCardId.IsValid)
			{
				throw new System.ArgumentException(
					"使用拖拽高度的新卡出生提示必须带有当前被拖起的来源卡牌。",
					nameof(spawnOriginCardId));
			}
			return new TabletopPresentationCue(
				TabletopPresentationCueKind.CardSpawn,
				true,
				tablePosition,
				true,
				cardId,
				spawnOriginCardId,
				usesDragHeight,
				spawnHeightOffset);
		}

		public bool Equals(TabletopPresentationCue other)
		{
			return Kind == other.Kind &&
				HasTablePosition == other.HasTablePosition &&
				(!HasTablePosition || m_tablePosition == other.m_tablePosition) &&
				HasCardId == other.HasCardId &&
				(!HasCardId || m_cardId == other.m_cardId) &&
				m_spawnOriginCardId == other.m_spawnOriginCardId &&
				m_usesDragHeight == other.m_usesDragHeight &&
				m_spawnHeightOffset.Equals(other.m_spawnHeightOffset);
		}

		public override bool Equals(object obj)
		{
			return obj is TabletopPresentationCue other && Equals(other);
		}

		public override int GetHashCode()
		{
			return System.HashCode.Combine(
				System.HashCode.Combine(Kind, HasTablePosition, m_tablePosition, HasCardId),
				System.HashCode.Combine(m_cardId, m_spawnOriginCardId, m_usesDragHeight, m_spawnHeightOffset));
		}
	}
}
