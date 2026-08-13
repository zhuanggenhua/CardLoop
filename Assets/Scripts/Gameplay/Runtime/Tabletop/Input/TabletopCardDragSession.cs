using UnityEngine;
using System;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 一次指针释放的输入事实，区分点击、拖拽、空白落点和目标卡牌。
	/// </summary>
	public readonly struct TabletopCardPointerReleaseIntent
	{
		public TabletopCardId CardId { get; }

		/// <summary>指针按下时的牌桌坐标。</summary>
		public Vector2 PressPointerPosition { get; }

		/// <summary>指针释放时的牌桌坐标。</summary>
		public Vector2 ReleasePointerPosition { get; }

		/// <summary>保持按下偏移后，请求放置的牌堆锚点。</summary>
		public Vector2 RequestedStackPosition { get; }

		public bool IsDrag { get; }

		public TabletopCardId TargetCardId { get; }

		internal TabletopCardPointerReleaseIntent(
			TabletopCardId cardId,
			Vector2 pressPointerPosition,
			Vector2 releasePointerPosition,
			Vector2 requestedStackPosition,
			bool isDrag,
			TabletopCardId targetCardId = default)
		{
			if (!cardId.IsValid)
			{
				throw new ArgumentException("牌桌指针释放必须引用有效的局内卡牌。", nameof(cardId));
			}
			EnsureFinite(pressPointerPosition, nameof(pressPointerPosition));
			EnsureFinite(releasePointerPosition, nameof(releasePointerPosition));
			EnsureFinite(requestedStackPosition, nameof(requestedStackPosition));
			if (!isDrag && targetCardId.IsValid)
			{
				throw new ArgumentException("点击释放不能携带拖拽目标卡牌。", nameof(targetCardId));
			}
			if (targetCardId == cardId)
			{
				throw new ArgumentException("拖拽来源卡牌不能同时作为自己的释放目标。", nameof(targetCardId));
			}
			CardId = cardId;
			PressPointerPosition = pressPointerPosition;
			ReleasePointerPosition = releasePointerPosition;
			RequestedStackPosition = requestedStackPosition;
			IsDrag = isDrag;
			TargetCardId = targetCardId;
		}

		private static void EnsureFinite(Vector2 position, string parameterName)
		{
			if (!float.IsFinite(position.x) || !float.IsFinite(position.y))
			{
				throw new ArgumentException("牌桌指针释放位置必须是有限坐标。", parameterName);
			}
		}
	}

	/// <summary>
	/// 单次指针按下到释放的拖拽状态机，负责阈值判定而不提交玩法状态。
	/// </summary>
	internal sealed class TabletopCardDragSession
	{
		private readonly float m_dragStartScreenDistanceSquared;

		public bool IsActive { get; private set; }

		public bool IsDragging { get; private set; }

		public TabletopCardId CardId { get; private set; }

		public Vector2 PressPointerTablePosition { get; private set; }

		public Vector2 CurrentPointerTablePosition { get; private set; }

		public Vector2 CurrentStackPosition { get; private set; }

		private Vector2 PressPointerScreenPosition { get; set; }

		private Vector2 PointerToStackTableOffset { get; set; }

		public TabletopCardDragSession(float dragStartScreenDistance)
		{
			if (!float.IsFinite(dragStartScreenDistance) || dragStartScreenDistance < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(dragStartScreenDistance),
					"拖拽起始屏幕距离不能为负数或非有限值。");
			}
			m_dragStartScreenDistanceSquared = dragStartScreenDistance * dragStartScreenDistance;
		}

		public void Begin(
			TabletopCardId cardId,
			Vector2 pointerScreenPosition,
			Vector2 pointerTablePosition,
			Vector2 stackPosition)
		{
			if (IsActive)
			{
				throw new InvalidOperationException("当前已有未结束的牌桌拖拽会话。");
			}
			if (!cardId.IsValid)
			{
				throw new ArgumentException("拖拽会话必须引用有效的局内卡牌。", "cardId");
			}
			EnsureFinite(pointerScreenPosition, nameof(pointerScreenPosition));
			EnsureFinite(pointerTablePosition, nameof(pointerTablePosition));
			EnsureFinite(stackPosition, nameof(stackPosition));
			IsActive = true;
			IsDragging = false;
			CardId = cardId;
			PressPointerScreenPosition = pointerScreenPosition;
			PressPointerTablePosition = pointerTablePosition;
			CurrentPointerTablePosition = pointerTablePosition;
			CurrentStackPosition = stackPosition;
			PointerToStackTableOffset = stackPosition - pointerTablePosition;
		}

		public bool Update(Vector2 pointerScreenPosition, Vector2 pointerTablePosition)
		{
			EnsureActive();
			EnsureFinite(pointerScreenPosition, nameof(pointerScreenPosition));
			EnsureFinite(pointerTablePosition, nameof(pointerTablePosition));
			CurrentPointerTablePosition = pointerTablePosition;
			CurrentStackPosition = pointerTablePosition + PointerToStackTableOffset;
			if (!IsDragging &&
				(pointerScreenPosition - PressPointerScreenPosition).sqrMagnitude >=
				m_dragStartScreenDistanceSquared)
			{
				IsDragging = true;
			}
			return IsDragging;
		}

		public TabletopCardPointerReleaseIntent End(
			Vector2 pointerScreenPosition,
			Vector2 pointerTablePosition,
			TabletopCardId targetCardId = default)
		{
			Update(pointerScreenPosition, pointerTablePosition);
			TabletopCardPointerReleaseIntent result = new TabletopCardPointerReleaseIntent(
				CardId,
				PressPointerTablePosition,
				CurrentPointerTablePosition,
				CurrentStackPosition,
				IsDragging,
				IsDragging ? targetCardId : default);
			Reset();
			return result;
		}

		public void Cancel()
		{
			EnsureActive();
			Reset();
		}

		private void EnsureActive()
		{
			if (!IsActive)
			{
				throw new InvalidOperationException("当前没有可更新或结束的牌桌拖拽会话。");
			}
		}

		private static void EnsureFinite(Vector2 position, string parameterName)
		{
			if (!float.IsFinite(position.x) || !float.IsFinite(position.y))
			{
				throw new ArgumentException("牌桌指针位置必须是有限坐标。", parameterName);
			}
		}

		private void Reset()
		{
			IsActive = false;
			IsDragging = false;
			CardId = default(TabletopCardId);
			PressPointerScreenPosition = default;
			PressPointerTablePosition = default;
			CurrentPointerTablePosition = default;
			CurrentStackPosition = default;
			PointerToStackTableOffset = default;
		}
	}
}
