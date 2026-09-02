using System;
using System.Collections;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证牌桌交互生成零到多个行动候选且不提前修改状态。
	/// </summary>
	public sealed class ActionCandidateEditModeTests
	{
		[Test]
		public void FindCandidates_ReturnsZeroOneOrManyWithoutMutatingState()
		{
			CardDefinition cardDefinition = CreateCardDefinition("test.card");
			ActionDefinition firstAction = CreateActionDefinition("test.action.first", "test.card");
			ActionDefinition secondAction = CreateActionDefinition("test.action.second", "test.card");
			ActionDefinition blockedAction = CreateActionDefinition("test.action.blocked", "test.other-card");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new CardDefinition[1] { cardDefinition });
				TabletopCards state = new TabletopCards();
				TabletopCard source = state.CreateCard("test.card", Vector2.zero);
				TabletopCard target = state.CreateCard("test.card", Vector2.one);
				TabletopCardPointerReleaseIntent completeIntent = new TabletopCardPointerReleaseIntent(source.Id, Vector2.zero, Vector2.one, Vector2.zero, isDrag: true, target.Id);
				TabletopCardPointerReleaseIntent partialIntent = new TabletopCardPointerReleaseIntent(source.Id, Vector2.zero, Vector2.right, Vector2.right, isDrag: true);
				ActionCandidate[] zero = ActionCandidateResolver.FindCandidates(completeIntent, state, contentIndex, new ActionDefinition[1] { blockedAction });
				ActionCandidate[] one = ActionCandidateResolver.FindCandidates(completeIntent, state, contentIndex, new ActionDefinition[1] { firstAction });
				ActionCandidate[] many = ActionCandidateResolver.FindCandidates(completeIntent, state, contentIndex, new ActionDefinition[2] { firstAction, secondAction });
				ActionCandidate[] partial = ActionCandidateResolver.FindCandidates(partialIntent, state, contentIndex, new ActionDefinition[1] { firstAction });
				Assert.That<ActionCandidate[]>(zero, (IResolveConstraint)(object)Is.Empty);
				Assert.That<ActionCandidate[]>(one, (IResolveConstraint)(object)((ConstraintExpression)Has.Length).EqualTo((object)1));
				Assert.That<ActionDefinition>(one[0].Action, (IResolveConstraint)(object)Is.SameAs((object)firstAction));
				Assert.That<bool>(one[0].IsReady, (IResolveConstraint)(object)Is.True);
				Assert.That<int>(one[0].MissingParticipantCount, (IResolveConstraint)(object)Is.Zero);
				Assert.That<IReadOnlyList<ActionSlotBinding>>(one[0].Bindings, (IResolveConstraint)(object)((ConstraintExpression)Has.Count).EqualTo((object)1));
				Assert.That<string>(one[0].Bindings[0].Slot.Key, (IResolveConstraint)(object)Is.EqualTo((object)"participant"));
				CollectionAssert.AreEqual((IEnumerable)new TabletopCardId[2] { source.Id, target.Id }, (IEnumerable)one[0].Bindings[0].CardIds);
				Assert.That<ActionCandidate[]>(many, (IResolveConstraint)(object)((ConstraintExpression)Has.Length).EqualTo((object)2));
				Assert.That<ActionDefinition>(many[0].Action, (IResolveConstraint)(object)Is.SameAs((object)firstAction));
				Assert.That<ActionDefinition>(many[1].Action, (IResolveConstraint)(object)Is.SameAs((object)secondAction));
				Assert.That<ActionCandidate[]>(partial, (IResolveConstraint)(object)((ConstraintExpression)Has.Length).EqualTo((object)1));
				Assert.That<bool>(partial[0].IsReady, (IResolveConstraint)(object)Is.False);
				Assert.That<int>(partial[0].MissingParticipantCount, (IResolveConstraint)(object)Is.EqualTo((object)1));
				CollectionAssert.AreEqual((IEnumerable)new TabletopCardId[1] { source.Id }, (IEnumerable)partial[0].Bindings[0].CardIds);
				Assert.Throws<InvalidOperationException>(() => ActionRequest.FromCandidate(partial[0]), "待填充候选不能直接变成可提交的行动请求。");
				Assert.That<int>(state.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)2), "候选查询不能自动合堆。", Array.Empty<object>());
				Assert.That<TabletopCardStack>(state.GetStackContaining(source.Id), (IResolveConstraint)(object)Is.Not.SameAs((object)state.GetStackContaining(target.Id)));
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)cardDefinition);
				Object.DestroyImmediate((Object)(object)firstAction);
				Object.DestroyImmediate((Object)(object)secondAction);
				Object.DestroyImmediate((Object)(object)blockedAction);
			}
		}

		[Test]
		public void FindStackCandidates_AllowsOptedInExcessCardsWithoutBindingThem()
		{
			CardDefinition station = CreateCardDefinition("test.stack.station");
			CardDefinition ingredient = CreateCardDefinition("test.stack.ingredient");
			CardDefinition unrelatedCard = CreateCardDefinition("test.stack.unrelated");
			ActionDefinition strictAction = CreateWorkbenchCraftActionDefinition(
				"test.stack.strict",
				station.ContentId.Value,
				ingredient.ContentId.Value,
				allowExcessCards: false);
			ActionDefinition excessAction = CreateWorkbenchCraftActionDefinition(
				"test.stack.excess",
				station.ContentId.Value,
				ingredient.ContentId.Value,
				allowExcessCards: true);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { station, ingredient, unrelatedCard, strictAction, excessAction });
				TabletopCards state = new TabletopCards();
				TabletopCard stationCard = state.CreateCard(station.ContentId, Vector2.zero);
				TabletopCard firstIngredient = state.CreateCard(ingredient.ContentId, Vector2.zero);
				TabletopCard secondIngredient = state.CreateCard(ingredient.ContentId, Vector2.zero);
				TabletopCard extraUnrelatedCard = state.CreateCard(unrelatedCard.ContentId, Vector2.zero);
				state.MergeStackOnto(firstIngredient.Id, stationCard.Id);
				state.MergeStackOnto(secondIngredient.Id, stationCard.Id);
				state.MergeStackOnto(extraUnrelatedCard.Id, stationCard.Id);
				TabletopCardStack stack = state.GetStackContaining(stationCard.Id);

				ActionCandidate[] strictCandidates = ActionCandidateResolver.FindStackCandidates(
					stack,
					state,
					contentIndex,
					new[] { strictAction });
				ActionCandidate[] excessCandidates = ActionCandidateResolver.FindStackCandidates(
					stack,
					state,
					contentIndex,
					new[] { excessAction });

				Assert.That(strictCandidates, Is.Empty);
				Assert.That(excessCandidates, Has.Length.EqualTo(1));
				Assert.That(excessCandidates[0].IsReady, Is.True);
				CollectionAssert.AreEqual(
					new[] { stationCard.Id },
					excessCandidates[0].Bindings[0].CardIds);
				CollectionAssert.AreEqual(
					new[] { firstIngredient.Id, secondIngredient.Id },
					excessCandidates[0].Bindings[1].CardIds);
			}
			finally
			{
				Object.DestroyImmediate(station);
				Object.DestroyImmediate(ingredient);
				Object.DestroyImmediate(unrelatedCard);
				Object.DestroyImmediate(strictAction);
				Object.DestroyImmediate(excessAction);
			}
		}

		[Test]
		public void FindStackCandidates_MatchesStackCraftStandardRecipeExcessOnlyForOptedInResourceSlot()
		{
			CardDefinition soil = CreateCardDefinition("test.stackcraft.soil");
			CardDefinition berry = CreateCardDefinition("test.stackcraft.berry");
			CardDefinition wood = CreateCardDefinition("test.stackcraft.wood");
			CardDefinition villager = CreateCardDefinition("test.stackcraft.villager");
			CardDefinition unrelated = CreateCardDefinition("test.stackcraft.unrelated");
			ActionDefinition growBerryBushAction = CreateStackCraftStandardRecipeActionDefinition(
				"test.stackcraft.recipe.grow-berry-bush",
				new RecipeSlotSpec("soil", soil.ContentId.Value, 1, allowAdditionalMatchingParticipantsInStack: true),
				new RecipeSlotSpec("berry", berry.ContentId.Value, 1, allowAdditionalMatchingParticipantsInStack: false));
			ActionDefinition makeTimberAction = CreateStackCraftStandardRecipeActionDefinition(
				"test.stackcraft.recipe.make-timber",
				new RecipeSlotSpec("wood", wood.ContentId.Value, 2, allowAdditionalMatchingParticipantsInStack: false),
				new RecipeSlotSpec("villager", villager.ContentId.Value, 1, allowAdditionalMatchingParticipantsInStack: false));
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { soil, berry, wood, villager, unrelated, growBerryBushAction, makeTimberAction });
				TabletopCards exactResourceExcessState = new TabletopCards();
				TabletopCard firstSoil = exactResourceExcessState.CreateCard(soil.ContentId, Vector2.zero);
				TabletopCard secondSoil = exactResourceExcessState.CreateCard(soil.ContentId, Vector2.zero);
				TabletopCard berryCard = exactResourceExcessState.CreateCard(berry.ContentId, Vector2.zero);
				exactResourceExcessState.MergeStackOnto(secondSoil.Id, firstSoil.Id);
				exactResourceExcessState.MergeStackOnto(berryCard.Id, firstSoil.Id);
				TabletopCardStack resourceExcessStack = exactResourceExcessState.GetStackContaining(firstSoil.Id);

				ActionCandidate[] resourceExcessCandidates = ActionCandidateResolver.FindStackCandidates(
					resourceExcessStack,
					exactResourceExcessState,
					contentIndex,
					new[] { growBerryBushAction });

				Assert.That(resourceExcessCandidates, Has.Length.EqualTo(1));
				Assert.That(resourceExcessCandidates[0].IsReady, Is.True);
				CollectionAssert.AreEqual(new[] { firstSoil.Id }, resourceExcessCandidates[0].Bindings[0].CardIds);
				CollectionAssert.AreEqual(new[] { berryCard.Id }, resourceExcessCandidates[0].Bindings[1].CardIds);

				TabletopCards unrelatedState = new TabletopCards();
				TabletopCard unrelatedFirstSoil = unrelatedState.CreateCard(soil.ContentId, Vector2.zero);
				TabletopCard unrelatedSecondSoil = unrelatedState.CreateCard(soil.ContentId, Vector2.zero);
				TabletopCard unrelatedBerry = unrelatedState.CreateCard(berry.ContentId, Vector2.zero);
				TabletopCard unrelatedCard = unrelatedState.CreateCard(unrelated.ContentId, Vector2.zero);
				unrelatedState.MergeStackOnto(unrelatedSecondSoil.Id, unrelatedFirstSoil.Id);
				unrelatedState.MergeStackOnto(unrelatedBerry.Id, unrelatedFirstSoil.Id);
				unrelatedState.MergeStackOnto(unrelatedCard.Id, unrelatedFirstSoil.Id);

				ActionCandidate[] unrelatedCandidates = ActionCandidateResolver.FindStackCandidates(
					unrelatedState.GetStackContaining(unrelatedFirstSoil.Id),
					unrelatedState,
					contentIndex,
					new[] { growBerryBushAction });

				Assert.That(unrelatedCandidates, Is.Empty);

				TabletopCards materialExcessState = new TabletopCards();
				TabletopCard firstWood = materialExcessState.CreateCard(wood.ContentId, Vector2.zero);
				TabletopCard secondWood = materialExcessState.CreateCard(wood.ContentId, Vector2.zero);
				TabletopCard thirdWood = materialExcessState.CreateCard(wood.ContentId, Vector2.zero);
				TabletopCard villagerCard = materialExcessState.CreateCard(villager.ContentId, Vector2.zero);
				materialExcessState.MergeStackOnto(secondWood.Id, firstWood.Id);
				materialExcessState.MergeStackOnto(thirdWood.Id, firstWood.Id);
				materialExcessState.MergeStackOnto(villagerCard.Id, firstWood.Id);

				ActionCandidate[] materialExcessCandidates = ActionCandidateResolver.FindStackCandidates(
					materialExcessState.GetStackContaining(firstWood.Id),
					materialExcessState,
					contentIndex,
					new[] { makeTimberAction });

				Assert.That(materialExcessCandidates, Is.Empty);
			}
			finally
			{
				Object.DestroyImmediate(soil);
				Object.DestroyImmediate(berry);
				Object.DestroyImmediate(wood);
				Object.DestroyImmediate(villager);
				Object.DestroyImmediate(unrelated);
				Object.DestroyImmediate(growBerryBushAction);
				Object.DestroyImmediate(makeTimberAction);
			}
		}

		[Test]
		public void FindCandidates_UsesPointerOrderAsTheDeterministicTieBreakForEquivalentSlots()
		{
			CardDefinition cardDefinition = CreateCardDefinition("test.card");
			ActionDefinition action = CreateTwoSlotActionDefinition("test.action.directional", "test.card");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new CardDefinition[1] { cardDefinition });
				TabletopCards state = new TabletopCards();
				TabletopCard source = state.CreateCard("test.card", Vector2.zero);
				TabletopCard target = state.CreateCard("test.card", Vector2.one);
				TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(source.Id, Vector2.zero, Vector2.one, Vector2.zero, isDrag: true, target.Id);
				ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(intent, state, contentIndex, new ActionDefinition[2] { action, action });
				Assert.That<ActionCandidate[]>(candidates, (IResolveConstraint)(object)((ConstraintExpression)Has.Length).EqualTo((object)1), "重复提供同一行动不能生成重复按钮。", Array.Empty<object>());
				Assert.That<bool>(candidates[0].IsReady, (IResolveConstraint)(object)Is.True);
				Assert.That<int>(candidates[0].Bindings.Count, (IResolveConstraint)(object)Is.EqualTo((object)2));
				Assert.That<string>(candidates[0].Bindings[0].Slot.Key, (IResolveConstraint)(object)Is.EqualTo((object)"initiator"));
				CollectionAssert.AreEqual((IEnumerable)new TabletopCardId[1] { source.Id }, (IEnumerable)candidates[0].Bindings[0].CardIds);
				Assert.That<string>(candidates[0].Bindings[1].Slot.Key, (IResolveConstraint)(object)Is.EqualTo((object)"target"));
				CollectionAssert.AreEqual((IEnumerable)new TabletopCardId[1] { target.Id }, (IEnumerable)candidates[0].Bindings[1].CardIds);
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)cardDefinition);
				Object.DestroyImmediate((Object)(object)action);
			}
		}

		[Test]
		public void FindCandidates_UnlimitedSourceSlotIncludesDraggedStackTail()
		{
			CardDefinition sellable = CreateCardDefinition("test.sellable");
			CardDefinition buyer = CreateCardDefinition("test.buyer");
			ActionDefinition action = CreateTradeActionDefinition(
				"test.action.sell-stack",
				sellable.ContentId.Value,
				buyer.ContentId.Value,
				unlimitedSourceSlot: true);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { sellable, buyer, action });
				TabletopCards state = new TabletopCards();
				TabletopCard bottom = state.CreateCard("test.sellable", Vector2.zero);
				TabletopCard middle = state.CreateCard("test.sellable", Vector2.zero);
				TabletopCard top = state.CreateCard("test.sellable", Vector2.zero);
				TabletopCard buyerCard = state.CreateCard("test.buyer", Vector2.one);
				state.MergeStackOnto(middle.Id, bottom.Id);
				state.MergeStackOnto(top.Id, bottom.Id);

				TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(
					middle.Id,
					Vector2.zero,
					Vector2.one,
					Vector2.zero,
					isDrag: true,
					buyerCard.Id);
				ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(
					intent,
					state,
					contentIndex,
					new[] { action });

				Assert.That(candidates, Has.Length.EqualTo(1));
				ActionCandidate candidate = candidates[0];
				Assert.That(candidate.IsReady, Is.True);
				CollectionAssert.AreEqual(
					new[] { middle.Id, top.Id },
					candidate.Bindings[0].CardIds);
				CollectionAssert.AreEqual(
					new[] { buyerCard.Id },
					candidate.Bindings[1].CardIds);
				Assert.That(state.GetStackContaining(middle.Id), Is.SameAs(state.GetStackContaining(bottom.Id)));
			}
			finally
			{
				Object.DestroyImmediate(sellable);
				Object.DestroyImmediate(buyer);
				Object.DestroyImmediate(action);
			}
		}

		[Test]
		public void FindCandidates_UnlimitedSourceSlotRejectsDraggedTailWithNonMatchingCard()
		{
			CardDefinition sellable = CreateCardDefinition("test.sellable");
			CardDefinition unsellable = CreateCardDefinition("test.unsellable");
			CardDefinition buyer = CreateCardDefinition("test.buyer");
			ActionDefinition action = CreateTradeActionDefinition(
				"test.action.sell-stack",
				sellable.ContentId.Value,
				buyer.ContentId.Value,
				unlimitedSourceSlot: true);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { sellable, unsellable, buyer, action });
				TabletopCards state = new TabletopCards();
				TabletopCard bottom = state.CreateCard("test.sellable", Vector2.zero);
				TabletopCard middle = state.CreateCard("test.sellable", Vector2.zero);
				TabletopCard top = state.CreateCard("test.unsellable", Vector2.zero);
				TabletopCard buyerCard = state.CreateCard("test.buyer", Vector2.one);
				state.MergeStackOnto(middle.Id, bottom.Id);
				state.MergeStackOnto(top.Id, bottom.Id);

				TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(
					middle.Id,
					Vector2.zero,
					Vector2.one,
					Vector2.zero,
					isDrag: true,
					buyerCard.Id);
				ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(
					intent,
					state,
					contentIndex,
					new[] { action });

				Assert.That(candidates, Is.Empty);
			}
			finally
			{
				Object.DestroyImmediate(sellable);
				Object.DestroyImmediate(unsellable);
				Object.DestroyImmediate(buyer);
				Object.DestroyImmediate(action);
			}
		}

		[Test]
		public void FindCandidates_DraggedStackTailDoesNotReplacePreciseTargetInteraction()
		{
			CardDefinition cardDefinition = CreateCardDefinition("test.card");
			ActionDefinition action = CreateActionDefinition("test.action.precise", "test.card");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { cardDefinition, action });
				TabletopCards state = new TabletopCards();
				TabletopCard bottom = state.CreateCard("test.card", Vector2.zero);
				TabletopCard middle = state.CreateCard("test.card", Vector2.zero);
				TabletopCard top = state.CreateCard("test.card", Vector2.zero);
				TabletopCard target = state.CreateCard("test.card", Vector2.one);
				state.MergeStackOnto(middle.Id, bottom.Id);
				state.MergeStackOnto(top.Id, bottom.Id);

				TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(
					middle.Id,
					Vector2.zero,
					Vector2.one,
					Vector2.zero,
					isDrag: true,
					target.Id);
				ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(
					intent,
					state,
					contentIndex,
					new[] { action });

				Assert.That(candidates, Has.Length.EqualTo(1));
				Assert.That(candidates[0].IsReady, Is.True);
				CollectionAssert.AreEqual(
					new[] { middle.Id, target.Id },
					candidates[0].Bindings[0].CardIds);
			}
			finally
			{
				Object.DestroyImmediate(cardDefinition);
				Object.DestroyImmediate(action);
			}
		}

		[Test]
		public void ActionPlan_BelongsToTabletopAndSubmitsOnlyAfterItsSlotsAreComplete()
		{
			CardDefinition cardDefinition = CreateCardDefinition("test.card");
			ActionDefinition action = CreateActionDefinition("test.action.plan", "test.card");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { cardDefinition, action });
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					TabletopTestPlacement.Rules,
					_ => false,
					(_, __) => { },
					_ => { });
				tabletop.InitializeAuthoritativeRandom(12345u);
				TabletopCard first = tabletop.CreateCard(cardDefinition.ContentId, Vector2.zero);
				TabletopCard second = tabletop.CreateCard(cardDefinition.ContentId, Vector2.one);
				ActionCandidate candidate = tabletop.FindCandidates(
					new TabletopCardPointerReleaseIntent(
						first.Id,
						Vector2.zero,
						Vector2.right,
						Vector2.right,
						isDrag: true,
						default),
					new[] { action })[0];

				ActionPlan plan = tabletop.CreateActionPlan(candidate);

				Assert.That(tabletop.ActionPlans, Has.Count.EqualTo(1));
				Assert.That(tabletop.ActionPlans[0], Is.SameAs(plan));
				Assert.That(plan.IsReady, Is.False);
				Assert.That(plan.MissingParticipantCount, Is.EqualTo(1));
				Assert.Throws<InvalidOperationException>(() => tabletop.SubmitActionPlan(plan));

				tabletop.AddCardToActionPlan(plan, candidate.Bindings[0].Slot.Key, second.Id);

				Assert.That(plan.IsReady, Is.True);
				CollectionAssert.AreEqual(
					new[] { first.Id, second.Id },
					plan.Bindings[0].CardIds);
				ActionInstance instance = tabletop.SubmitActionPlan(plan);
				Assert.That(tabletop.ActionPlans, Is.Empty);
				Assert.That(tabletop.ActiveActions, Has.Count.EqualTo(1));
				CollectionAssert.AreEqual(plan.Bindings[0].CardIds, instance.Bindings[0].CardIds);
			}
			finally
			{
				Object.DestroyImmediate(cardDefinition);
				Object.DestroyImmediate(action);
			}
		}

		[Test]
		public void CancelActionPlan_RemovesTheSamePlanWithoutStartingAnAction()
		{
			CardDefinition cardDefinition = CreateCardDefinition("test.card");
			ActionDefinition action = CreateActionDefinition("test.action.plan.cancel", "test.card");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { cardDefinition, action });
				Gameplay.Tabletop.Tabletop tabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					TabletopTestPlacement.Rules,
					_ => false,
					(_, __) => { },
					_ => { });
				TabletopCard card = tabletop.CreateCard(cardDefinition.ContentId, Vector2.zero);
				ActionCandidate candidate = tabletop.FindCandidates(
					new TabletopCardPointerReleaseIntent(
						card.Id,
						Vector2.zero,
						Vector2.right,
						Vector2.right,
						isDrag: true,
						default),
					new[] { action })[0];
				ActionPlan plan = tabletop.CreateActionPlan(candidate);

				tabletop.CancelActionPlan(plan);

				Assert.That(tabletop.ActionPlans, Is.Empty);
				Assert.That(tabletop.ActiveActions, Is.Empty);
				Assert.Throws<InvalidOperationException>(() => tabletop.CancelActionPlan(plan));
			}
			finally
			{
				Object.DestroyImmediate(cardDefinition);
				Object.DestroyImmediate(action);
			}
		}

		[Test]
		public void RemovingCardUnbindsItFromPlanAndTravelRejectsBoundCard()
		{
			CardDefinition cardDefinition = CreateCardDefinition("test.card");
			ActionDefinition action = CreateActionDefinition("test.action.plan.lifecycle", "test.card");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { cardDefinition, action });
				Gameplay.Tabletop.Tabletop sourceTabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					TabletopTestPlacement.Rules,
					_ => false,
					(_, __) => { },
					_ => { });
				Gameplay.Tabletop.Tabletop targetTabletop = new Gameplay.Tabletop.Tabletop(
					contentIndex,
					TabletopTestPlacement.Rules,
					_ => false,
					(_, __) => { },
					_ => { });
				TabletopCard first = sourceTabletop.CreateCard(cardDefinition.ContentId, Vector2.zero);
				TabletopCard second = sourceTabletop.CreateCard(cardDefinition.ContentId, Vector2.one);
				ActionCandidate candidate = sourceTabletop.FindCandidates(
					new TabletopCardPointerReleaseIntent(
						first.Id,
						Vector2.zero,
						Vector2.right,
						Vector2.right,
						isDrag: true,
						default),
					new[] { action })[0];
				ActionPlan plan = sourceTabletop.CreateActionPlan(candidate);

				sourceTabletop.AddCardToActionPlan(plan, plan.Bindings[0].Slot.Key, second.Id);
				Assert.Throws<InvalidOperationException>(() =>
					sourceTabletop.RequireCardsCanTransferTo(
						targetTabletop,
						new[] { second.Id },
						new[] { Vector2.zero }));

				sourceTabletop.RemoveCard(first.Id);

				CollectionAssert.AreEqual(new[] { second.Id }, plan.Bindings[0].CardIds);
				Assert.That(plan.MissingParticipantCount, Is.EqualTo(1));
			}
			finally
			{
				Object.DestroyImmediate(cardDefinition);
				Object.DestroyImmediate(action);
			}
		}

		private static CardDefinition CreateCardDefinition(string contentId)
		{
			CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}", (object)definition);
			return definition;
		}

		private static ActionDefinition CreateActionDefinition(string actionId, string allowedContentId)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + actionId + "\"},\"m_participationSlots\":[{\"m_key\":\"participant\",\"m_minimumParticipants\":2,\"m_maximumParticipants\":2,\"m_allowedContentIds\":[{\"m_value\":\"" + allowedContentId + "\"}]}]}", (object)definition);
			return definition;
		}

		private static ActionDefinition CreateWorkbenchCraftActionDefinition(
			string actionId,
			string stationContentId,
			string ingredientContentId,
			bool allowExcessCards)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + actionId + "\"}," +
				"\"m_turnCost\":1," +
				"\"m_allowExcessCardsInStack\":" + (allowExcessCards ? "true" : "false") + "," +
				"\"m_participationSlots\":[" +
				"{\"m_key\":\"station\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"" + stationContentId + "\"}]}," +
				"{\"m_key\":\"ingredient\",\"m_minimumParticipants\":2,\"m_maximumParticipants\":2,\"m_allowedContentIds\":[{\"m_value\":\"" + ingredientContentId + "\"}]}]}",
				definition);
			return definition;
		}
		private static ActionDefinition CreateTwoSlotActionDefinition(string actionId, string allowedContentId)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			string allowed = "\"m_allowedContentIds\":[{\"m_value\":\"" + allowedContentId + "\"}]";
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + actionId + "\"},\"m_participationSlots\":[{\"m_key\":\"initiator\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1," + allowed + "},{\"m_key\":\"target\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1," + allowed + "}]}", (object)definition);
			return definition;
		}

		private static ActionDefinition CreateTradeActionDefinition(
			string actionId,
			string soldContentId,
			string buyerContentId,
			bool unlimitedSourceSlot)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			int sourceMaximum = unlimitedSourceSlot ? 0 : 1;
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + actionId + "\"},\"m_participationSlots\":[" +
				"{\"m_key\":\"sold\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":" + sourceMaximum + ",\"m_allowedContentIds\":[{\"m_value\":\"" + soldContentId + "\"}]}," +
				"{\"m_key\":\"buyer\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"" + buyerContentId + "\"}]}]}",
				definition);
			return definition;
		}

		private readonly struct RecipeSlotSpec
		{
			internal RecipeSlotSpec(
				string key,
				string contentId,
				int count,
				bool allowAdditionalMatchingParticipantsInStack)
			{
				Key = key;
				ContentId = contentId;
				Count = count;
				AllowAdditionalMatchingParticipantsInStack = allowAdditionalMatchingParticipantsInStack;
			}

			internal string Key { get; }

			internal string ContentId { get; }

			internal int Count { get; }

			internal bool AllowAdditionalMatchingParticipantsInStack { get; }
		}

		private static ActionDefinition CreateStackCraftStandardRecipeActionDefinition(
			string actionId,
			params RecipeSlotSpec[] slotSpecs)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			System.Text.StringBuilder json = new System.Text.StringBuilder();
			json.Append("{\"m_contentId\":{\"m_value\":\"");
			json.Append(actionId);
			json.Append("\"},\"m_turnCost\":1,\"m_allowExcessCardsInStack\":false,\"m_participationSlots\":[");
			for (int slotIndex = 0; slotIndex < slotSpecs.Length; slotIndex++)
			{
				if (slotIndex > 0)
				{
					json.Append(',');
				}
				RecipeSlotSpec spec = slotSpecs[slotIndex];
				json.Append("{\"m_key\":\"");
				json.Append(spec.Key);
				json.Append("\",\"m_minimumParticipants\":");
				json.Append(spec.Count);
				json.Append(",\"m_maximumParticipants\":");
				json.Append(spec.Count);
				json.Append(",\"m_allowAdditionalMatchingParticipantsInStack\":");
				json.Append(spec.AllowAdditionalMatchingParticipantsInStack ? "true" : "false");
				json.Append(",\"m_allowedContentIds\":[{\"m_value\":\"");
				json.Append(spec.ContentId);
				json.Append("\"}]}");
			}
			json.Append("]}");
			JsonUtility.FromJsonOverwrite(json.ToString(), definition);
			return definition;
		}
	}
}
