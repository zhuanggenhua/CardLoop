using System;
using Gameplay.Tabletop;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证单次指针拖拽会话的阈值与释放事实。
	/// </summary>
	public sealed class TabletopCardDragSessionEditModeTests
	{
		[Test]
		public void Drag_PreservesPointerOffsetFromStackAnchor()
		{
			TabletopCards cards = new TabletopCards();
			TabletopCard card = cards.CreateCard("test.offset", Vector2.zero);
			TabletopCardDragSession session = new TabletopCardDragSession(0.1f);
			Vector2 pointerPressPosition = new Vector2(1f, 2f);
			Vector2 stackPosition = new Vector2(-2f, 3f);

			session.Begin(card.Id, new Vector2(10f, 20f), pointerPressPosition, stackPosition);
			session.Update(new Vector2(30f, 50f), new Vector2(3f, 5f));
			TabletopCardPointerReleaseIntent intent = session.End(
				new Vector2(40f, 70f),
				new Vector2(4f, 7f));

			Assert.That(session.IsActive, Is.False);
			Assert.That(intent.PressPointerPosition, Is.EqualTo(pointerPressPosition));
			Assert.That(intent.ReleasePointerPosition, Is.EqualTo(new Vector2(4f, 7f)));
			Assert.That(intent.RequestedStackPosition, Is.EqualTo(new Vector2(1f, 8f)));
		}

		[Test]
		public void Update_UsesTableDistanceForStackCraftClickThreshold()
		{
			TabletopCards cards = new TabletopCards();
			TabletopCard card = cards.CreateCard("test.table-threshold", Vector2.zero);
			TabletopCardDragSession session = new TabletopCardDragSession(0.5f);
			session.Begin(card.Id, Vector2.zero, Vector2.zero, card.Position);

			Assert.That(session.Update(new Vector2(100f, 0f), new Vector2(0.49f, 0f)), Is.False);
			Assert.That(session.Update(new Vector2(100f, 0f), new Vector2(0.5f, 0f)), Is.True);
		}

		[Test]
		public void Update_BelowThresholdStillRefreshesPreviewStackPosition()
		{
			TabletopCards cards = new TabletopCards();
			TabletopCard card = cards.CreateCard("test.preview-before-drag", new Vector2(2f, 3f));
			TabletopCardDragSession session = new TabletopCardDragSession(0.5f);
			session.Begin(card.Id, Vector2.zero, Vector2.one, card.Position);

			Assert.That(session.Update(new Vector2(10f, 10f), new Vector2(1.25f, 1.1f)), Is.False);
			Assert.That(
				session.CurrentStackPosition,
				Is.EqualTo(new Vector2(2.25f, 3.1f)),
				"StackCraft 按下后立即让卡牌跟随指针；点击阈值只决定释放是否按点击处理。");
		}

		[Test]
		public void End_BelowThresholdProducesClickIntent()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
			TabletopCardDragSession session = new TabletopCardDragSession(0.5f);
			session.Begin(
				tabletopCard.Id,
				new Vector2(100f, 200f),
				new Vector2(1f, 2f),
				tabletopCard.Position);
			TabletopCardPointerReleaseIntent result = session.End(
				new Vector2(100.1f, 200.1f),
				new Vector2(1.1f, 2.1f));
			Assert.That<TabletopCardId>(result.CardId, (IResolveConstraint)(object)Is.EqualTo((object)tabletopCard.Id));
			Assert.That<bool>(result.IsDrag, (IResolveConstraint)(object)Is.False);
			Assert.That<bool>(session.IsActive, (IResolveConstraint)(object)Is.False);
		}

		[Test]
		public void Update_CrossingThresholdKeepsDragStateUntilRelease()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
			TabletopCardDragSession session = new TabletopCardDragSession(0.5f);
			session.Begin(tabletopCard.Id, Vector2.zero, Vector2.zero, tabletopCard.Position);
			Assert.That<bool>(session.Update(new Vector2(0.5f, 0f), new Vector2(5f, 0f)), (IResolveConstraint)(object)Is.True);
			Assert.That<bool>(session.Update(new Vector2(0.1f, 0f), new Vector2(1f, 0f)), (IResolveConstraint)(object)Is.True);
			Assert.That<bool>(session.End(new Vector2(0.1f, 0f), new Vector2(1f, 0f)).IsDrag, (IResolveConstraint)(object)Is.True);
		}

		[Test]
		public void Cancel_ClearsSessionWithoutProducingIntent()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
			TabletopCardDragSession session = new TabletopCardDragSession(0f);
			session.Begin(tabletopCard.Id, Vector2.zero, Vector2.zero, tabletopCard.Position);
			session.Update(Vector2.one, Vector2.one);
			session.Cancel();
			Assert.That<bool>(session.IsActive, (IResolveConstraint)(object)Is.False);
			Assert.That<bool>(session.IsDragging, (IResolveConstraint)(object)Is.False);
			Assert.That<bool>(session.CardId.IsValid, (IResolveConstraint)(object)Is.False);
		}

		[Test]
		public void End_PreservesSpatialTargetWithoutExecutingIt()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard source = state.CreateCard("test.source", Vector2.zero);
			TabletopCard target = state.CreateCard("test.target", Vector2.one);
			TabletopCardDragSession session = new TabletopCardDragSession(0.1f);
			session.Begin(source.Id, Vector2.zero, Vector2.zero, source.Position);
			TabletopCardPointerReleaseIntent result = session.End(Vector2.one, Vector2.one, target.Id);
			Assert.That<bool>(result.IsDrag, (IResolveConstraint)(object)Is.True);
			Assert.That<TabletopCardId>(result.TargetCardId, (IResolveConstraint)(object)Is.EqualTo((object)target.Id));
			Assert.That<int>(state.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)2), "意图产生阶段不能提前合并堆栈。", Array.Empty<object>());
		}
	}
}
