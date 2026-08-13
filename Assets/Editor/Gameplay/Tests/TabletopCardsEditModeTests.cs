using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Tabletop;
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

			state.RemoveCard(top.Id);
			Assert.That(top.Stack, Is.Null);
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
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(contentIndex, placementRules, _ => { });
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
				Assert.That<float>(tabletop.Cards.GetStackContaining(blocker.Id).Position.x - placed.Position.x, (IResolveConstraint)(object)Is.GreaterThanOrEqualTo((object)2f), "重叠解算必须移动整堆，不能把同一堆的中间卡和顶牌拆成两个空间对象。", Array.Empty<object>());
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)cardDefinition);
			}
		}

		[Test]
		public void TryPlaceStack_WhenAreaCannotFitCardRejectsWithoutPartialMutation()
		{
			CardDefinition cardDefinition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"test.reject-place-card\"}}", (object)cardDefinition);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[1] { cardDefinition });
				TabletopCardPlacementRules placementRules = new TabletopCardPlacementRules(
					new TabletopCardPlacementArea(new Rect(-2f, -1f, 4f, 2f)),
					new TabletopCardStackGeometry(new Vector2(2f, 2f), Vector2.zero));
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(contentIndex, placementRules, _ => { });
				Vector2 requestedPosition = new Vector2(-1f, 0f);
				TabletopCard bottom = tabletop.CreateCard(cardDefinition.ContentId, requestedPosition);
				TabletopCard top = tabletop.CreateCard(cardDefinition.ContentId, requestedPosition);
				tabletop.MergeStackOnto(top.Id, bottom.Id);
				tabletop.CreateCard(cardDefinition.ContentId, new Vector2(1f, 0f), isPlacementLocked: true);
				Vector2 originalPosition = tabletop.Cards.GetStackContaining(bottom.Id).Position;
				ulong originalRevision = tabletop.Cards.Revision;
				TabletopCardStack placed;
				bool accepted = tabletop.TryPlaceStack(top.Id, new Vector2(1f, 0f), out placed);
				Assert.That<bool>(accepted, (IResolveConstraint)(object)Is.False);
				Assert.That<TabletopCardStack>(placed, (IResolveConstraint)(object)Is.Null);
				Assert.That<ulong>(tabletop.Cards.Revision, (IResolveConstraint)(object)Is.EqualTo((object)originalRevision));
				Assert.That<int>(tabletop.Cards.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)2));
				Assert.That<Vector2>(tabletop.Cards.GetStackContaining(bottom.Id).Position, (IResolveConstraint)(object)Is.EqualTo((object)originalPosition));
				CollectionAssert.AreEqual((IEnumerable)new TabletopCard[2] { bottom, top }, (IEnumerable)tabletop.Cards.GetStackContaining(bottom.Id).Cards);
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)cardDefinition);
			}
		}
	}
}
