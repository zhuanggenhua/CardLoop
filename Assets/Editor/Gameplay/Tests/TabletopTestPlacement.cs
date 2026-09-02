using Gameplay.Content;
using Gameplay.Tabletop;
using UnityEngine;

namespace Gameplay.Tests
{
	/// <summary>
	/// EditMode 测试构造牌桌对象时使用的固定规则；极小占地让非空间测试保留原始坐标。
	/// </summary>
	internal static class TabletopTestPlacement
	{
		internal static readonly TabletopCardPlacementRules Rules =
			CreateRules();

		internal static TabletopCardPlacementRules CreateRules(
			float automaticMovementIntervalSeconds = TabletopCardPlacementRules.DefaultAutomaticMovementIntervalSeconds,
			float automaticMovementRadius = TabletopCardPlacementRules.DefaultAutomaticMovementRadius,
			int automaticMovementMaxAttempts = TabletopCardPlacementRules.DefaultAutomaticMovementMaxAttempts)
		{
			return new TabletopCardPlacementRules(
				new TabletopCardPlacementArea(new Rect(-1000f, -1000f, 2000f, 2000f)),
				new TabletopCardStackGeometry(new Vector2(0.00001f, 0.00001f), Vector2.zero),
				automaticMovementIntervalSeconds: automaticMovementIntervalSeconds,
				automaticMovementRadius: automaticMovementRadius,
				automaticMovementMaxAttempts: automaticMovementMaxAttempts);
		}

		internal static TabletopCard CreateCard(
			this TabletopCards cards,
			ContentId contentId,
			Vector2 position,
			bool isPlacementLocked = false)
		{
			return cards.CreateCard(contentId, position, Rules, isPlacementLocked);
		}
	}
}
