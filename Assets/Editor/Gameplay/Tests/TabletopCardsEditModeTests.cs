using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证牌桌卡牌、牌堆、快照恢复和原子放置不变量。
	/// </summary>
	public sealed class TabletopCardsEditModeTests
	{
		private sealed class AutomaticMovementCardDefinition : CardDefinition
		{
			public override bool UsesAutomaticMovement => true;
		}

		[Test]
		public void Tabletop_OwnsOnePlacementRuleSetUsedByEveryPlacement()
		{
			CardDefinition cardDefinition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"test.tabletop-owned-placement\"}}", cardDefinition);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { cardDefinition });
				TabletopCardPlacementRules placementRules = new TabletopCardPlacementRules(
					new TabletopCardPlacementArea(new Rect(-5f, -5f, 10f, 10f)),
					new TabletopCardStackGeometry(new Vector2(2f, 2f), Vector2.zero));
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					placementRules,
					_ => false,
					(_, __) => { },
					_ => { });
				TabletopCard card = tabletop.CreateCard(cardDefinition.ContentId, Vector2.zero);

				Assert.That(tabletop.PlacementRules, Is.SameAs(placementRules));
				Assert.That(tabletop.TryPlaceStack(card.Id, new Vector2(20f, 20f), out TabletopCardStack placed), Is.True);
				Assert.That(placed.Position.x, Is.LessThanOrEqualTo(4f));
				Assert.That(placed.Position.y, Is.LessThanOrEqualTo(4f));
			}
			finally
			{
				Object.DestroyImmediate(cardDefinition);
			}
		}

		[Test]
		public void Tabletop_PlacementBoundsFollowCardLimitBonusAndReflowOnShrink()
		{
			CardDefinition normal = ScriptableObject.CreateInstance<CardDefinition>();
			CardDefinition booster = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.tabletop.dynamic-board.normal\"}}",
				normal);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.tabletop.dynamic-board.booster\"},\"m_cardLimitBonus\":5}",
				booster);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { normal, booster });
				TabletopCardPlacementRules placementRules = new TabletopCardPlacementRules(
					new TabletopCardPlacementArea(new Rect(-2f, -2f, 4f, 4f)),
					new TabletopCardStackGeometry(Vector2.one, Vector2.zero),
					cardLimitBonusExpansionPerPoint: new Vector2(0.2f, 0f));
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					placementRules,
					_ => false,
					(_, __) => { },
					_ => { });

				TabletopCard boosterCard = tabletop.CreateCard(booster.ContentId, Vector2.zero);

				Assert.That(tabletop.CardLimitBonus, Is.EqualTo(5));
				Assert.That(tabletop.PlacementRules.Area.Bounds.xMax, Is.EqualTo(3f));

				TabletopCard normalCard = tabletop.CreateCard(normal.ContentId, new Vector2(2.5f, 0f));
				Assert.That(normalCard.Position.x, Is.EqualTo(2.5f));

				tabletop.RemoveCard(boosterCard.Id);

				Assert.That(tabletop.CardLimitBonus, Is.Zero);
				Assert.That(tabletop.PlacementRules.Area.Bounds.xMax, Is.EqualTo(2f));
				Assert.That(normalCard.Position.x, Is.LessThanOrEqualTo(1.5001f));
			}
			finally
			{
				Object.DestroyImmediate(normal);
				Object.DestroyImmediate(booster);
			}
		}

		[Test]
		public void Tabletop_PlacementBoundsMoveStackCraftHeaderRestrictionWithCardLimitBonus()
		{
			TabletopCardPlacementRules rules = new TabletopCardPlacementRules(
				new TabletopCardPlacementArea(
					new Rect(-6f, -4f, 12f, 8f),
					new[] { new Rect(-6f, 2.5f, 12f, 1.5f), new Rect(-1f, -1f, 2f, 2f) }),
				new TabletopCardStackGeometry(Vector2.one, Vector2.zero),
				cardLimitBonusExpansionPerPoint: new Vector2(0.06f, 0.04f));

			TabletopCardPlacementRules expanded = rules.CreateForCardLimitBonus(100);

			Assert.That(expanded.Area.Bounds, Is.EqualTo(new Rect(-12f, -8f, 24f, 16f)));
			Assert.That(expanded.Area.RestrictedAreas[0], Is.EqualTo(new Rect(-12f, 6.5f, 24f, 1.5f)));
			Assert.That(expanded.Area.RestrictedAreas[1], Is.EqualTo(new Rect(-1f, -1f, 2f, 2f)));
		}

		[Test]
		public void Tabletop_CardLimitBonusMovesLockedHeaderStacksWithStackCraftBoard()
		{
			CardDefinition tradeZone = ScriptableObject.CreateInstance<CardDefinition>();
			CardDefinition booster = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.tabletop.header.trade-zone\"}}",
				tradeZone);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.tabletop.header.booster\"},\"m_cardLimitBonus\":100}",
				booster);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { tradeZone, booster });
				TabletopCardPlacementRules placementRules = new TabletopCardPlacementRules(
					new TabletopCardPlacementArea(
						new Rect(-6f, -4f, 12f, 8f),
						new[] { new Rect(-6f, 2.5f, 12f, 1.5f) }),
					new TabletopCardStackGeometry(
						new Vector2(0.8f, 1f),
						new Vector2(0f, -0.18f),
						new Vector2(0.1f, 0.1f)),
					cardLimitBonusExpansionPerPoint: new Vector2(0.06f, 0.04f));
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					placementRules,
					_ => false,
					(_, __) => { },
					_ => { });
				TabletopCard tradeZoneCard = tabletop.CreateCard(
					tradeZone.ContentId,
					new Vector2(-0.55f, 3.25f),
					isPlacementLocked: true);

				tabletop.CreateCard(booster.ContentId, Vector2.zero);

				Assert.That(tabletop.PlacementRules.Area.Bounds, Is.EqualTo(new Rect(-12f, -8f, 24f, 16f)));
				Assert.That(tabletop.PlacementRules.Area.RestrictedAreas[0], Is.EqualTo(new Rect(-12f, 6.5f, 24f, 1.5f)));
				Assert.That(tradeZoneCard.Position.x, Is.EqualTo(-0.55f).Within(0.0001f));
				Assert.That(tradeZoneCard.Position.y, Is.EqualTo(7.25f).Within(0.0001f));
			}
			finally
			{
				Object.DestroyImmediate(tradeZone);
				Object.DestroyImmediate(booster);
			}
		}

		[Test]
		public void DragClamp_UsesStackCraftBoardBoundsWithoutHeaderRestriction()
		{
			TabletopCards cards = new TabletopCards();
			TabletopCard top = cards.CreateCard("test.drag-clamp.top", Vector2.zero);
			TabletopCard tail = cards.CreateCard("test.drag-clamp.tail", Vector2.zero);
			TabletopCardStack stack = cards.MergeStackOnto(tail.Id, top.Id);
			TabletopCardPlacementRules placementRules = new TabletopCardPlacementRules(
				new TabletopCardPlacementArea(
					new Rect(-6f, -4f, 12f, 8f),
					new[] { new Rect(-6f, 2.5f, 12f, 1.5f) }),
				new TabletopCardStackGeometry(
					new Vector2(0.8f, 1f),
					new Vector2(0f, -0.18f),
					new Vector2(0.1f, 0.1f)));

			Vector2 clamped = cards.ClampStackPositionToBounds(
				stack.TopCard.Id,
				new Vector2(99f, 99f),
				placementRules);

			Assert.That(clamped.x, Is.EqualTo(5.55f).Within(0.0001f));
			Assert.That(clamped.y, Is.EqualTo(3.45f).Within(0.0001f));
			Assert.That(
				clamped.y,
				Is.GreaterThan(2.5f),
				"StackCraft 拖拽中只执行 Board.ClampToBounds；顶部页眉禁放区要等释放时的 EnforcePlacementRules 再处理。");
		}

		[Test]
		public void MoveStackDuringLocalDrag_UpdatesStackTargetWithoutStructuralRevision()
		{
			TabletopCards cards = new TabletopCards();
			TabletopCard top = cards.CreateCard("test.drag-live-position.top", Vector2.zero);
			TabletopCard tail = cards.CreateCard("test.drag-live-position.tail", Vector2.zero);
			TabletopCardStack stack = cards.MergeStackOnto(tail.Id, top.Id);
			TabletopCardPlacementRules placementRules = new TabletopCardPlacementRules(
				new TabletopCardPlacementArea(new Rect(-6f, -4f, 12f, 8f)),
				new TabletopCardStackGeometry(
					new Vector2(0.8f, 1f),
					new Vector2(0f, -0.18f),
					new Vector2(0.1f, 0.1f)));
			ulong revisionBeforeDragMove = cards.Revision;

			Vector2 clamped = cards.MoveStackDuringLocalDrag(
				stack.TopCard.Id,
				new Vector2(2f, 1f),
				placementRules);

			Assert.That(stack.Position, Is.EqualTo(clamped));
			Assert.That(stack.Position, Is.EqualTo(new Vector2(2f, 1f)));
			Assert.That(
				cards.Revision,
				Is.EqualTo(revisionBeforeDragMove),
				"StackCraft SetDragTargetPosition 在拖拽中只改当前牌堆目标位置；如果递增结构版本，释放清理会误触整桌 Refresh，造成非拖拽牌闪动。");
		}

		[Test]
		public void CreateCard_SameContentCreatesDifferentRuntimeCards()
		{
			TabletopCards state = new TabletopCards();
			ContentId contentId = new ContentId("test.wood");
			TabletopCard first = state.CreateCard(contentId, new Vector2(1f, 2f));
			TabletopCard second = state.CreateCard(contentId, new Vector2(3f, 4f));
			Assert.That<TabletopCardId>(first.Id, (IResolveConstraint)(object)Is.Not.EqualTo((object)second.Id));
			Assert.That<ContentId>(first.ContentId, (IResolveConstraint)(object)Is.EqualTo((object)contentId));
			Assert.That<ContentId>(second.ContentId, (IResolveConstraint)(object)Is.EqualTo((object)contentId));
			Assert.That<int>(state.CardCount, (IResolveConstraint)(object)Is.EqualTo((object)2));
			Assert.That<int>(state.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)2));
		}

		[Test]
		public void CreateCardStack_CreatesOneStackWithSequentialRuntimeCards()
		{
			TabletopCards state = new TabletopCards();
			ContentId contentId = new ContentId("test.coin");
			TabletopCardStack stack = state.CreateCardStack(
				contentId,
				4,
				new Vector2(1f, 2f),
				TabletopTestPlacement.Rules);

			Assert.That<int>(state.CardCount, (IResolveConstraint)(object)Is.EqualTo((object)4));
			Assert.That<int>(state.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)1));
			CollectionAssert.AreEqual(new ulong[] { 1uL, 2uL, 3uL, 4uL }, stack.Cards.Select(card => card.Id.Value));
			Assert.That(stack.Cards.All(card => card.ContentId == contentId), Is.True);
		}

		[Test]
		public void Card_TracksItsOwningStackAcrossMergeDetachAndRemoval()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard bottom = state.CreateCard("test.bottom", new Vector2(1f, 2f));
			TabletopCard top = state.CreateCard("test.top", new Vector2(5f, 6f));

			Assert.That(bottom.Stack, Is.SameAs(state.GetStackContaining(bottom.Id)));
			Assert.That(bottom.Position, Is.EqualTo(new Vector2(1f, 2f)));

			TabletopCardStack merged = state.MergeStackOnto(top.Id, bottom.Id);
			Assert.That(top.Stack, Is.SameAs(merged));
			Assert.That(top.Position, Is.EqualTo(merged.Position));

			TabletopCardStack detached = state.DetachStackAt(top.Id);
			Assert.That(top.Stack, Is.SameAs(detached));
			Assert.That(top.Position, Is.EqualTo(detached.Position));
			Assert.That(state.StackCount, Is.EqualTo(2));
			CollectionAssert.AreEqual(new TabletopCard[] { bottom }, merged.Cards);
			CollectionAssert.AreEqual(new TabletopCard[] { top }, detached.Cards);

			state.RemoveCard(top.Id);
			Assert.That(top.Stack, Is.Null);
		}

		[Test]
		public void DetachStackAt_MergedTopCardCreatesSingleCardStack()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard bottom = state.CreateCard("test.detach-top.bottom", new Vector2(1f, 2f));
			TabletopCard top = state.CreateCard("test.detach-top.top", new Vector2(5f, 6f));
			TabletopCardStack original = state.MergeStackOnto(top.Id, bottom.Id);

			TabletopCardStack detached = state.DetachStackAt(top.Id);

			Assert.That(state.StackCount, Is.EqualTo(2));
			Assert.That(original.Cards, Is.EqualTo(new TabletopCard[] { bottom }));
			Assert.That(detached.Cards, Is.EqualTo(new TabletopCard[] { top }));
			Assert.That(state.GetStackContaining(bottom.Id), Is.SameAs(original));
			Assert.That(state.GetStackContaining(top.Id), Is.SameAs(detached));
		}

		[Test]
		public void MergeStackOnto_PreservesBottomToTopOrderAndTargetPosition()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard target = state.CreateCard("test.target", new Vector2(1f, 2f));
			TabletopCard source = state.CreateCard("test.source", new Vector2(8f, 9f));
			TabletopCardStack merged = state.MergeStackOnto(source.Id, target.Id);
			Assert.That<int>(state.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)1));
			Assert.That<Vector2>(merged.Position, (IResolveConstraint)(object)Is.EqualTo((object)new Vector2(1f, 2f)));
			Assert.That<IReadOnlyList<TabletopCard>>(merged.Cards, (IResolveConstraint)(object)Is.EqualTo((object)new TabletopCard[2] { target, source }));
			Assert.That<TabletopCardStack>(state.GetStackContaining(source.Id), (IResolveConstraint)(object)Is.SameAs((object)merged));
			Assert.That<TabletopCardStack>(state.GetStackContaining(target.Id), (IResolveConstraint)(object)Is.SameAs((object)merged));
		}

		[Test]
		public void DetachStackAt_SelectedCardAndCardsAboveFormNewStack()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard bottom = state.CreateCard("test.bottom", new Vector2(2f, 3f));
			TabletopCard middle = state.CreateCard("test.middle", new Vector2(4f, 5f));
			TabletopCard top = state.CreateCard("test.top", new Vector2(6f, 7f));
			state.MergeStackOnto(middle.Id, bottom.Id);
			TabletopCardStack original = state.MergeStackOnto(top.Id, middle.Id);
			TabletopCardStack detached = state.DetachStackAt(middle.Id);
			Assert.That<int>(state.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)2));
			Assert.That<IReadOnlyList<TabletopCard>>(original.Cards, (IResolveConstraint)(object)Is.EqualTo((object)new TabletopCard[1] { bottom }));
			Assert.That<IReadOnlyList<TabletopCard>>(detached.Cards, (IResolveConstraint)(object)Is.EqualTo((object)new TabletopCard[2] { middle, top }));
			Assert.That<Vector2>(original.Position, (IResolveConstraint)(object)Is.EqualTo((object)new Vector2(2f, 3f)));
			Assert.That<Vector2>(detached.Position, (IResolveConstraint)(object)Is.EqualTo((object)new Vector2(2f, 3f)));
			Assert.That<TabletopCardStack>(state.GetStackContaining(bottom.Id), (IResolveConstraint)(object)Is.SameAs((object)original));
			Assert.That<TabletopCardStack>(state.GetStackContaining(middle.Id), (IResolveConstraint)(object)Is.SameAs((object)detached));
			Assert.That<TabletopCardStack>(state.GetStackContaining(top.Id), (IResolveConstraint)(object)Is.SameAs((object)detached));
		}

		[Test]
		public void LockedBottom_RejectsWholeStackDetachButAllowsDetachingCardsAbove()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard fixedBottom = state.CreateCard("test.fixed-bottom", new Vector2(2f, 3f), isPlacementLocked: true);
			TabletopCard movableTop = state.CreateCard("test.movable-top", new Vector2(4f, 5f));
			state.MergeStackOnto(movableTop.Id, fixedBottom.Id);
			Assert.Throws<InvalidOperationException>(() => state.DetachStackAt(fixedBottom.Id));
			TabletopCardStack detached = state.DetachStackAt(movableTop.Id);
			Assert.That<bool>(detached.IsPlacementLocked, (IResolveConstraint)(object)Is.False);
			Assert.That<Vector2>(detached.Position, (IResolveConstraint)(object)Is.EqualTo((object)new Vector2(2f, 3f)));
			Assert.That<Vector2>(state.GetStackContaining(fixedBottom.Id).Position, (IResolveConstraint)(object)Is.EqualTo((object)new Vector2(2f, 3f)));
		}

		[Test]
		public void MergeStackOnto_WhenSourceIsLockedRejectsMutation()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard lockedSource = state.CreateCard("test.locked-source", new Vector2(2f, 3f), isPlacementLocked: true);
			TabletopCard target = state.CreateCard("test.target", new Vector2(8f, 9f));
			Assert.Throws<InvalidOperationException>((TestDelegate)delegate
			{
				state.MergeStackOnto(lockedSource.Id, target.Id);
			});
			Assert.That<int>(state.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)2));
			Assert.That<Vector2>(state.GetStackContaining(lockedSource.Id).Position, (IResolveConstraint)(object)Is.EqualTo((object)new Vector2(2f, 3f)));
			Assert.That<Vector2>(state.GetStackContaining(target.Id).Position, (IResolveConstraint)(object)Is.EqualTo((object)new Vector2(8f, 9f)));
		}

		[Test]
		public void RestoreSnapshot_PreservesRuntimeIdsStackOrderAndNextAllocation()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard bottom = state.CreateCard("test.bottom", new Vector2(2f, 3f), isPlacementLocked: true);
			TabletopCard middle = state.CreateCard("test.middle", new Vector2(4f, 5f));
			TabletopCard removed = state.CreateCard("test.removed", new Vector2(6f, 7f));
			TabletopCard top = state.CreateCard("test.top", new Vector2(8f, 9f));
			TabletopCard removedHighest = state.CreateCard("test.removed-highest", new Vector2(10f, 11f));
			state.MergeStackOnto(middle.Id, bottom.Id);
			state.MergeStackOnto(top.Id, bottom.Id);
			state.RemoveCard(removed.Id);
			state.RemoveCard(removedHighest.Id);
			TabletopCardStateSnapshot snapshot = state.CreateSnapshot();
			TabletopCardIdSequence sequence = new TabletopCardIdSequence(state.CardIdSequence.NextValue);
			string json = JsonUtility.ToJson((object)snapshot);
			TabletopCardStateSnapshot deserialized = JsonUtility.FromJson<TabletopCardStateSnapshot>(json);
			TabletopCards restored = TabletopCards.Restore(deserialized, sequence);
			Assert.That<int>(restored.CardCount, (IResolveConstraint)(object)Is.EqualTo((object)3));
			Assert.That<int>(restored.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)1));
			TabletopCardStack restoredStack = restored.GetStackContaining(bottom.Id);
			Assert.That<bool>(restoredStack.IsPlacementLocked, (IResolveConstraint)(object)Is.True);
			Assert.That<Vector2>(restoredStack.Position, (IResolveConstraint)(object)Is.EqualTo((object)new Vector2(2f, 3f)));
			CollectionAssert.AreEqual((IEnumerable)new TabletopCardId[3] { bottom.Id, middle.Id, top.Id }, (IEnumerable)restoredStack.Cards.Select((TabletopCard card) => card.Id));
			Assert.That<TabletopCardStack>(restored.GetStackContaining(middle.Id), (IResolveConstraint)(object)Is.SameAs((object)restoredStack));
			Assert.That<TabletopCardStack>(restored.GetStackContaining(top.Id), (IResolveConstraint)(object)Is.SameAs((object)restoredStack));
			TabletopCard next = restored.CreateCard("test.next", Vector2.zero);
			Assert.That<ulong>(next.Id.Value, (IResolveConstraint)(object)Is.EqualTo((object)6uL), "恢复后不能复用快照前已经分配过的局内卡牌 ID。", Array.Empty<object>());
		}

		[Test]
		public void RestoreTabletop_WhenSnapshotReferencesNonCardContentRejectsBeforePublishingState()
		{
			ActionDefinition nonCardContent = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"test.not-a-card\"}}", (object)nonCardContent);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[1] { nonCardContent });
				TabletopCards source = new TabletopCards();
				source.CreateCard(nonCardContent.ContentId, Vector2.zero);
				TabletopCardStateSnapshot snapshot = source.CreateSnapshot();
				InvalidOperationException exception = Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					new Gameplay.Tabletop.Tabletop(
						contentIndex,
						snapshot,
						TabletopTestPlacement.Rules,
						_ => false,
						(_, __) => { },
						_ => { },
						cardIdSequence: new TabletopCardIdSequence(source.CardIdSequence.NextValue));
				});
				StringAssert.Contains(nonCardContent.ContentId.Value, exception.Message);
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)nonCardContent);
			}
		}

		[Test]
		public void TryPlaceStack_FromMiddleCardResolvesTheDetachedTailAsOneStack()
		{
			CardDefinition cardDefinition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"test.place-card\"}}", (object)cardDefinition);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[1] { cardDefinition });
				TabletopCardPlacementRules placementRules = new TabletopCardPlacementRules(new TabletopCardPlacementArea(new Rect(-10f, -10f, 20f, 20f)), new TabletopCardStackGeometry(new Vector2(2f, 2f), new Vector2(0f, 0.25f)));
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(contentIndex, placementRules, _ => false, (_, __) => { }, _ => { });
				TabletopCard bottom = tabletop.CreateCard(cardDefinition.ContentId, new Vector2(-2f, 1f));
				TabletopCard middle = tabletop.CreateCard(cardDefinition.ContentId, new Vector2(-2f, 1f));
				TabletopCard top = tabletop.CreateCard(cardDefinition.ContentId, new Vector2(-2f, 1f));
				TabletopCard blocker = tabletop.CreateCard(cardDefinition.ContentId, new Vector2(4f, -3f));
				tabletop.MergeStackOnto(middle.Id, bottom.Id);
				tabletop.MergeStackOnto(top.Id, bottom.Id);
				TabletopCardStack placed;
				bool accepted = tabletop.TryPlaceStack(middle.Id, new Vector2(4f, -3f), out placed);
				Assert.That<bool>(accepted, (IResolveConstraint)(object)Is.True);
				Assert.That<int>(tabletop.Cards.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)3));
				CollectionAssert.AreEqual((IEnumerable)new TabletopCard[1] { bottom }, (IEnumerable)tabletop.Cards.GetStackContaining(bottom.Id).Cards);
				CollectionAssert.AreEqual((IEnumerable)new TabletopCard[2] { middle, top }, (IEnumerable)placed.Cards);
				Assert.That<TabletopCardStack>(tabletop.Cards.GetStackContaining(middle.Id), (IResolveConstraint)(object)Is.SameAs((object)placed));
				Assert.That<TabletopCardStack>(tabletop.Cards.GetStackContaining(top.Id), (IResolveConstraint)(object)Is.SameAs((object)placed));
				TabletopCardStack blockerStack = tabletop.Cards.GetStackContaining(blocker.Id);
				Rect placedFootprint = placementRules.Geometry.CalculateFootprint(placed.Position, placed.Cards.Count);
				Rect blockerFootprint = placementRules.Geometry.CalculateFootprint(
					blockerStack.Position,
					blockerStack.Cards.Count);
				Assert.That(
					placedFootprint.Overlaps(blockerFootprint),
					Is.False,
					"重叠解算必须把拖拽尾段当作一个整堆推离阻挡牌堆，不能把中间卡和顶牌拆成两个空间对象。");
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)cardDefinition);
			}
		}

		[Test]
		public void TryPlaceSingleCard_FromMiddleCardMovesOnlySelectedCard()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard bottom = state.CreateCard("test.single.bottom", new Vector2(-2f, 1f));
			TabletopCard middle = state.CreateCard("test.single.middle", new Vector2(-2f, 1f));
			TabletopCard top = state.CreateCard("test.single.top", new Vector2(-2f, 1f));
			state.MergeStackOnto(middle.Id, bottom.Id);
			TabletopCardStack original = state.MergeStackOnto(top.Id, bottom.Id);
			middle.AdvancePeriodicProduction(1.25f, 10f);

			bool accepted = state.TryPlaceSingleCard(
				middle.Id,
				new Vector2(4f, -3f),
				TabletopTestPlacement.Rules,
				out TabletopCardStack placed);

			Assert.That(accepted, Is.True);
			Assert.That(state.StackCount, Is.EqualTo(2));
			Assert.That(placed.Cards, Is.EqualTo(new TabletopCard[] { middle }));
			Assert.That(original.Cards, Is.EqualTo(new TabletopCard[] { bottom, top }));
			Assert.That(state.GetStackContaining(middle.Id), Is.SameAs(placed));
			Assert.That(state.GetStackContaining(top.Id), Is.SameAs(original));
			Assert.That(middle.PeriodicProductionElapsedSeconds, Is.EqualTo(1.25f));
			Assert.That(placed.Position, Is.EqualTo(new Vector2(4f, -3f)));
		}

		[Test]
		public void TryPlaceStack_WhenOverlapCannotFullyResolveStillAcceptsStackCraftDrop()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard bottom = state.CreateCard("test.stackcraft-drop.bottom", new Vector2(-1f, 0f));
			TabletopCard top = state.CreateCard("test.stackcraft-drop.top", new Vector2(-1f, 0f));
			TabletopCard lockedBlocker = state.CreateCard("test.stackcraft-drop.locked", new Vector2(1f, 0f), isPlacementLocked: true);
			state.MergeStackOnto(top.Id, bottom.Id);
			TabletopCardPlacementRules placementRules = new TabletopCardPlacementRules(
				new TabletopCardPlacementArea(new Rect(-2f, -1f, 4f, 2f)),
				new TabletopCardStackGeometry(new Vector2(2f, 2f), Vector2.zero));
			ulong originalRevision = state.Revision;

			bool accepted = state.TryPlaceStack(top.Id, new Vector2(1f, 0f), placementRules, out TabletopCardStack placed);

			Assert.That<bool>(accepted, (IResolveConstraint)(object)Is.True);
			Assert.That<TabletopCardStack>(placed, (IResolveConstraint)(object)Is.Not.Null);
			Assert.That<ulong>(state.Revision, (IResolveConstraint)(object)Is.GreaterThan((object)originalRevision));
			Assert.That<int>(state.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)3));
			CollectionAssert.AreEqual((IEnumerable)new TabletopCard[1] { bottom }, (IEnumerable)state.GetStackContaining(bottom.Id).Cards);
			CollectionAssert.AreEqual((IEnumerable)new TabletopCard[1] { top }, (IEnumerable)placed.Cards);
			Assert.That<TabletopCardStack>(state.GetStackContaining(top.Id), (IResolveConstraint)(object)Is.SameAs((object)placed));
			Assert.That<TabletopCardStack>(state.GetStackContaining(lockedBlocker.Id), (IResolveConstraint)(object)Is.Not.SameAs((object)placed));
			Assert.That<float>(placed.Position.x, (IResolveConstraint)(object)Is.InRange(-1f, 1f));
			Assert.That<float>(placed.Position.y, (IResolveConstraint)(object)Is.EqualTo((object)0f).Within(0.0001f));
		}

		[Test]
		public void TryDropStackOnto_ReflowsNeighbouringStacksAfterMerge()
		{
			CardDefinition resource = null;
			bool gasRuntimeInitialized = false;
			try
			{
				InvokeFormalGasBootstrap("Shutdown");
				InvokeFormalGasBootstrap("EnsureInitialized");
				gasRuntimeInitialized = true;
				resource = ScriptableObject.CreateInstance<CardDefinition>();
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.drop-merge.reflow.resource\"},\"m_tagCodes\":[" +
					GAS.Runtime.XTag.Card_Category_Resource +
					"]}",
					resource);
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { resource });
				TabletopCardPlacementRules placementRules = new TabletopCardPlacementRules(
					new TabletopCardPlacementArea(new Rect(-10f, -10f, 20f, 20f)),
					new TabletopCardStackGeometry(new Vector2(2f, 2f), new Vector2(0f, -1f)),
					stackingRules: TabletopStackingRulesDefinition.CreateStackCraftDefault().CreateRuntime());
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					placementRules,
					_ => false,
					(_, __) => { },
					_ => { });
				TabletopCard target = tabletop.CreateCard(resource.ContentId, Vector2.zero);
				TabletopCard source = tabletop.CreateCard(resource.ContentId, new Vector2(4f, 0f));
				TabletopCard blocker = tabletop.CreateCard(resource.ContentId, new Vector2(4f, -4f));
				Vector2 blockingPosition = new Vector2(0f, -2.5f);
				tabletop.Cards.MoveStackDuringLocalDrag(blocker.Id, blockingPosition, placementRules);
				ulong revisionAfterSetup = tabletop.Cards.Revision;

				bool accepted = tabletop.TryDropStackOnto(source.Id, target.Id, source.Stack.Position, out TabletopCardStack merged);

				Assert.That(accepted, Is.True);
				CollectionAssert.AreEqual(new TabletopCard[] { target, source }, merged.Cards);
				TabletopCardStack blockerStack = tabletop.Cards.GetStackContaining(blocker.Id);
				Assert.That(
					blockerStack.Position,
					Is.Not.EqualTo(blockingPosition),
					"StackCraft 普通合堆后会立即 ResolveOverlaps，合堆变长压到邻近牌堆时不能等下一帧或其它链路再推开。");
				Assert.That(tabletop.Cards.Revision, Is.GreaterThan(revisionAfterSetup));
			}
			finally
			{
				if (resource != null)
				{
					Object.DestroyImmediate(resource);
				}
				if (gasRuntimeInitialized)
				{
					InvokeFormalGasBootstrap("Shutdown");
				}
			}
		}

		[Test]
		public void CreateCard_AutomaticBehaviorsWaitForTemplateInitialDelay()
		{
			CardDefinition producer = ScriptableObject.CreateInstance<CardDefinition>();
			CardDefinition product = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.auto-delay.producer\"}," +
				"\"m_periodicProductionCardId\":{\"m_value\":\"test.auto-delay.product\"}," +
				"\"m_periodicProductionIntervalSeconds\":1.0}",
				producer);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.auto-delay.product\"}}",
				product);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { producer, product });
				TabletopCardPlacementRules rules = TabletopTestPlacement.CreateRules(1f, 1f, 1);
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					rules,
					_ => false,
					(_, __) => { },
					_ => { });
				tabletop.InitializeAuthoritativeRandom(12345u);
				TabletopCard card = tabletop.CreateCard(producer.ContentId, Vector2.zero);

				Assert.That(card.PeriodicProductionInitialDelaySeconds, Is.InRange(0.5f, 1.0f));
				Assert.That(card.AutomaticMovementInitialDelaySeconds, Is.InRange(0.5f, 1.0f));
				tabletop.AdvanceRealTime(1.49f);

				Assert.That(CountCards(tabletop, product.ContentId), Is.Zero);
				Assert.That(card.Position, Is.EqualTo(Vector2.zero));

				tabletop.AdvanceRealTime(0.52f);

				Assert.That(CountCards(tabletop, product.ContentId), Is.EqualTo(1));
				Assert.That(card.Position, Is.Not.EqualTo(Vector2.zero));
			}
			finally
			{
				Object.DestroyImmediate(producer);
				Object.DestroyImmediate(product);
			}
		}

		[Test]
		public void AdvanceRealTime_AutomaticMovementMovesOnlySelectedCard()
		{
			CardDefinition staticCard = ScriptableObject.CreateInstance<CardDefinition>();
			AutomaticMovementCardDefinition movingCard = ScriptableObject.CreateInstance<AutomaticMovementCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.auto-move.static\"}}",
				staticCard);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.auto-move.moving\"}}",
				movingCard);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { staticCard, movingCard });
				TabletopCardPlacementRules rules = TabletopTestPlacement.CreateRules(1f, 2f, 1);
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					rules,
					_ => false,
					(_, __) => { },
					_ => { });
				tabletop.InitializeAuthoritativeRandom(12345u);
				Vector2 originalPosition = new Vector2(-2f, 1f);
				TabletopCard bottom = tabletop.CreateCard(staticCard.ContentId, originalPosition);
				TabletopCard middle = tabletop.CreateCard(movingCard.ContentId, originalPosition);
				TabletopCard top = tabletop.CreateCard(staticCard.ContentId, originalPosition);
				tabletop.MergeStackOnto(middle.Id, bottom.Id);
				TabletopCardStack original = tabletop.MergeStackOnto(top.Id, bottom.Id);

				tabletop.AdvanceRealTime(0.99f);

				Assert.That(tabletop.Cards.GetStackContaining(middle.Id), Is.SameAs(original));
				CollectionAssert.AreEqual(new TabletopCard[] { bottom, middle, top }, original.Cards);

				tabletop.AdvanceRealTime(1.02f);

				TabletopCardStack moved = tabletop.Cards.GetStackContaining(middle.Id);
				Assert.That(tabletop.Cards.StackCount, Is.EqualTo(2));
				Assert.That(moved, Is.Not.SameAs(original));
				CollectionAssert.AreEqual(new TabletopCard[] { middle }, moved.Cards);
				CollectionAssert.AreEqual(new TabletopCard[] { bottom, top }, original.Cards);
				Assert.That(moved.Position, Is.Not.EqualTo(originalPosition));
			}
			finally
			{
				Object.DestroyImmediate(staticCard);
				Object.DestroyImmediate(movingCard);
			}
		}

		[Test]
		public void AdvanceRealTime_AutomaticMovementDetachesBeforePatrolEvenWhenNoMoveCandidateIsValid()
		{
			CardDefinition staticCard = ScriptableObject.CreateInstance<CardDefinition>();
			AutomaticMovementCardDefinition movingCard = ScriptableObject.CreateInstance<AutomaticMovementCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.auto-move.detach-static\"}}",
				staticCard);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.auto-move.detach-moving\"}}",
				movingCard);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { staticCard, movingCard });
				TabletopCardPlacementRules rules = TabletopTestPlacement.CreateRules(
					automaticMovementIntervalSeconds: 1f,
					automaticMovementRadius: 10000f,
					automaticMovementMaxAttempts: 1);
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					rules,
					_ => false,
					(_, __) => { },
					_ => { });
				tabletop.InitializeAuthoritativeRandom(12345u);
				Vector2 originalPosition = new Vector2(-2f, 1f);
				TabletopCard bottom = tabletop.CreateCard(staticCard.ContentId, originalPosition);
				TabletopCard middle = tabletop.CreateCard(movingCard.ContentId, originalPosition);
				TabletopCard top = tabletop.CreateCard(staticCard.ContentId, originalPosition);
				tabletop.MergeStackOnto(middle.Id, bottom.Id);
				TabletopCardStack original = tabletop.MergeStackOnto(top.Id, bottom.Id);

				tabletop.AdvanceRealTime(2.01f);

				TabletopCardStack detached = tabletop.Cards.GetStackContaining(middle.Id);
				Assert.That(tabletop.Cards.StackCount, Is.EqualTo(2));
				Assert.That(detached, Is.Not.SameAs(original));
				CollectionAssert.AreEqual(new TabletopCard[] { middle }, detached.Cards);
				CollectionAssert.AreEqual(new TabletopCard[] { bottom, top }, original.Cards);
				Assert.That(
					detached.Position,
					Is.EqualTo(originalPosition),
					"StackCraft 的 CardAI 会在巡逻判点前先 DetachFromStack；候选移动失败也不能把卡留在旧堆里。");
			}
			finally
			{
				Object.DestroyImmediate(staticCard);
				Object.DestroyImmediate(movingCard);
			}
		}

		[Test]
		public void AdvanceRealTime_AutomaticMovementRetentionKeepsCardsWithinCapacity()
		{
			CardDefinition enclosure = ScriptableObject.CreateInstance<CardDefinition>();
			AutomaticMovementCardDefinition movingCard = ScriptableObject.CreateInstance<AutomaticMovementCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.auto-move.retention\"}," +
				"\"m_automaticMovementRetentionCapacity\":1}",
				enclosure);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.auto-move.retained-card\"}}",
				movingCard);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { enclosure, movingCard });
				TabletopCardPlacementRules rules = TabletopTestPlacement.CreateRules(1f, 2f, 1);
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					rules,
					_ => false,
					(_, __) => { },
					_ => { });
				tabletop.InitializeAuthoritativeRandom(12345u);
				Vector2 originalPosition = new Vector2(-2f, 1f);
				TabletopCard bottom = tabletop.CreateCard(enclosure.ContentId, originalPosition);
				TabletopCard retained = tabletop.CreateCard(movingCard.ContentId, originalPosition);
				TabletopCard released = tabletop.CreateCard(movingCard.ContentId, originalPosition);
				tabletop.MergeStackOnto(retained.Id, bottom.Id);
				TabletopCardStack original = tabletop.MergeStackOnto(released.Id, bottom.Id);

				tabletop.AdvanceRealTime(2.01f);

				TabletopCardStack retainedStack = tabletop.Cards.GetStackContaining(retained.Id);
				TabletopCardStack releasedStack = tabletop.Cards.GetStackContaining(released.Id);
				Assert.That(tabletop.Cards.StackCount, Is.EqualTo(2));
				Assert.That(retainedStack, Is.SameAs(original));
				Assert.That(releasedStack, Is.Not.SameAs(original));
				CollectionAssert.AreEqual(new TabletopCard[] { bottom, retained }, original.Cards);
				CollectionAssert.AreEqual(new TabletopCard[] { released }, releasedStack.Cards);
				Assert.That(releasedStack.Position, Is.Not.EqualTo(originalPosition));
			}
			finally
			{
				Object.DestroyImmediate(enclosure);
				Object.DestroyImmediate(movingCard);
			}
		}

		[Test]
		public void AdvanceRealTime_LocalInputHeldCardSkipsAutomaticMovementUntilReleased()
		{
			AutomaticMovementCardDefinition movingCard = ScriptableObject.CreateInstance<AutomaticMovementCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.auto-move.local-input\"}}",
				movingCard);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { movingCard });
				TabletopCardPlacementRules rules = TabletopTestPlacement.CreateRules(1f, 2f, 1);
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					rules,
					_ => false,
					(_, __) => { },
					_ => { });
				tabletop.InitializeAuthoritativeRandom(12345u);
				Vector2 originalPosition = new Vector2(-2f, 1f);
				TabletopCard card = tabletop.CreateCard(movingCard.ContentId, originalPosition);

				tabletop.HoldAutomaticBehaviorForLocalInput(card.Id);
				tabletop.AdvanceRealTime(1.01f);

				Assert.That(tabletop.Cards.GetStackContaining(card.Id).Position, Is.EqualTo(originalPosition));
				Assert.That(card.AutomaticMovementElapsedSeconds, Is.Zero);

				tabletop.ReleaseAutomaticBehaviorForLocalInput(card.Id);
				tabletop.AdvanceRealTime(0.99f);

				Assert.That(tabletop.Cards.GetStackContaining(card.Id).Position, Is.EqualTo(originalPosition));

				tabletop.AdvanceRealTime(1.02f);

				Assert.That(tabletop.Cards.GetStackContaining(card.Id).Position, Is.Not.EqualTo(originalPosition));
			}
			finally
			{
				Object.DestroyImmediate(movingCard);
			}
		}

		[Test]
		public void AdvanceRealTime_LocalInputHeldCardSkipsPeriodicProductionUntilReleased()
		{
			CardDefinition producer = ScriptableObject.CreateInstance<CardDefinition>();
			CardDefinition product = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.periodic.local-input.producer\"}," +
				"\"m_periodicProductionCardId\":{\"m_value\":\"test.periodic.local-input.product\"}," +
				"\"m_periodicProductionIntervalSeconds\":1.0}",
				producer);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.periodic.local-input.product\"}}",
				product);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { producer, product });
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					TabletopTestPlacement.Rules,
					_ => false,
					(_, __) => { },
					_ => { });
				tabletop.InitializeAuthoritativeRandom(12345u);
				TabletopCard card = tabletop.CreateCard(producer.ContentId, Vector2.zero);

				tabletop.HoldAutomaticBehaviorForLocalInput(card.Id);
				tabletop.AdvanceRealTime(1.01f);

				Assert.That(CountCards(tabletop, product.ContentId), Is.Zero);
				Assert.That(card.PeriodicProductionElapsedSeconds, Is.Zero);

				tabletop.ReleaseAutomaticBehaviorForLocalInput(card.Id);
				tabletop.AdvanceRealTime(0.99f);

				Assert.That(CountCards(tabletop, product.ContentId), Is.Zero);

				tabletop.AdvanceRealTime(1.02f);

				Assert.That(CountCards(tabletop, product.ContentId), Is.EqualTo(1));
			}
			finally
			{
				Object.DestroyImmediate(producer);
				Object.DestroyImmediate(product);
			}
		}

		[Test]
		public void AdvanceRealTime_PeriodicProducerCreatesConfiguredCardAndRequestsSmokeCue()
		{
			CardDefinition producer = ScriptableObject.CreateInstance<CardDefinition>();
			CardDefinition product = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.periodic.producer\"}," +
				"\"m_periodicProductionCardId\":{\"m_value\":\"test.periodic.product\"}," +
				"\"m_periodicProductionIntervalSeconds\":2.0}",
				producer);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.periodic.product\"}}",
				product);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { producer, product });
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					TabletopTestPlacement.Rules,
					_ => false,
					(_, __) => { },
					_ => { });
				tabletop.InitializeAuthoritativeRandom(12345u);
				List<TabletopPresentationCue> cues = new List<TabletopPresentationCue>();
				tabletop.PresentationCueRequested += cues.Add;
				TabletopCard source = tabletop.CreateCard(producer.ContentId, Vector2.zero);

				tabletop.AdvanceRealTime(1.99f);

				Assert.That(CountCards(tabletop, product.ContentId), Is.Zero);
				Assert.That(cues, Is.Empty);

				tabletop.AdvanceRealTime(1.02f);

				Assert.That(CountCards(tabletop, product.ContentId), Is.EqualTo(1));
				Assert.That(cues, Has.Count.EqualTo(2));
				Assert.That(cues[0].Kind, Is.EqualTo(TabletopPresentationCueKind.CardSpawn));
				Assert.That(cues[0].HasCardId, Is.True);
				Assert.That(cues[0].HasTablePosition, Is.True);
				Assert.That(cues[0].TablePosition, Is.EqualTo(source.Position));
				Assert.That(cues[0].UsesDragHeight, Is.False);
				Assert.That(cues[0].SpawnHeightOffset, Is.Zero);
				Assert.That(cues[1].Kind, Is.EqualTo(TabletopPresentationCueKind.CardSmoke));
				Assert.That(cues[1].HasTablePosition, Is.True);
				Assert.That(cues[1].TablePosition, Is.EqualTo(source.Position));
			}
			finally
			{
				Object.DestroyImmediate(producer);
				Object.DestroyImmediate(product);
			}
		}

		[Test]
		public void AdvanceRealTime_ExtraCardInCraftingStackSkipsPeriodicProduction()
		{
			CardDefinition material = ScriptableObject.CreateInstance<CardDefinition>();
			CardDefinition producer = ScriptableObject.CreateInstance<CardDefinition>();
			CardDefinition product = ScriptableObject.CreateInstance<CardDefinition>();
			ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.crafting-extra.material\"}}",
				material);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.crafting-extra.producer\"}," +
				"\"m_periodicProductionCardId\":{\"m_value\":\"test.crafting-extra.product\"}," +
				"\"m_periodicProductionIntervalSeconds\":1.0}",
				producer);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.crafting-extra.product\"}}",
				product);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.crafting-extra.action\"}," +
				"\"m_turnCost\":2,\"m_allowExcessCardsInStack\":true," +
				"\"m_participationSlots\":[{\"m_key\":\"material\",\"m_minimumParticipants\":1," +
				"\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"test.crafting-extra.material\"}]}]}",
				action);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.crafting-extra.region\"}}",
				region);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.crafting-extra.scenario\"}," +
				"\"m_initialRegionId\":{\"m_value\":\"test.crafting-extra.region\"}," +
				"\"m_regionIds\":[{\"m_value\":\"test.crafting-extra.region\"}]}",
				scenario);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { material, producer, product, action, region, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.DiscoverContent(action.ContentId);
				Gameplay.Tabletop.Tabletop tabletop = run.Tabletop;
				TabletopCard materialCard = tabletop.CreateCard(material.ContentId, Vector2.zero);
				TabletopCard producerCard = tabletop.CreateCard(producer.ContentId, Vector2.zero);
				tabletop.Cards.MergeStackOnto(producerCard.Id, materialCard.Id);
				TabletopCardStack craftStack = tabletop.Cards.GetStackContaining(materialCard.Id);
				ActionCandidate[] candidates = run.FindStackActionCandidates(craftStack);
				Assert.That(candidates, Has.Length.EqualTo(1));
				run.StartAction(ActionRequest.FromCandidate(candidates[0]));

				tabletop.AdvanceRealTime(1.01f);

				Assert.That(CountCards(tabletop, product.ContentId), Is.Zero);
				Assert.That(producerCard.PeriodicProductionElapsedSeconds, Is.Zero);
			}
			finally
			{
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(producer);
				Object.DestroyImmediate(product);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(region);
				Object.DestroyImmediate(scenario);
			}
		}

		private static int CountCards(Gameplay.Tabletop.Tabletop tabletop, ContentId contentId)
		{
			int count = 0;
			for (int stackIndex = 0; stackIndex < tabletop.Cards.Stacks.Count; stackIndex++)
			{
				IReadOnlyList<TabletopCard> cards = tabletop.Cards.Stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					if (cards[cardIndex].ContentId == contentId)
					{
						count++;
					}
				}
			}
			return count;
		}

		private static void InvokeFormalGasBootstrap(string methodName)
		{
			Type bootstrapType = typeof(GameCore.GameManager).Assembly.GetType(
				"GameCore.FormalAbilityRuntimeBootstrap",
				throwOnError: true);
			MethodInfo method = bootstrapType.GetMethod(
				methodName,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				throw new InvalidOperationException($"找不到 FormalAbilityRuntimeBootstrap.{methodName}。");
			}
			try
			{
				method.Invoke(null, null);
			}
			catch (TargetInvocationException exception) when (exception.InnerException != null)
			{
				throw new InvalidOperationException(
					$"通过项目正式入口调用 EX-GAS 生命周期 {methodName} 失败。",
					exception.InnerException);
			}
		}
	}
}
