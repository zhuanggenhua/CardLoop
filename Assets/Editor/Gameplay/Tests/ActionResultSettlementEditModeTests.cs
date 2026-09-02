using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证行动结果计划冻结、权威随机和原子牌桌结算。
	/// </summary>
	public sealed class ActionResultSettlementEditModeTests
	{
		private sealed class ResultTestContext : IDisposable
		{
			internal CardDefinition Participant { get; }

			internal CardDefinition Product { get; }

			internal ActionDefinition Action { get; }

			internal ScenarioDefinition Scenario { get; }

			internal ScenarioRegionDefinition Region { get; }

			internal ScenarioRun ScenarioRun { get; }

			internal TabletopCards State { get; }

			internal TabletopCard Source { get; }

			internal TabletopCard Target { get; }

			internal Vector2 SourcePosition { get; }

			internal ActionCandidate Candidate { get; }

			internal Gameplay.Tabletop.Tabletop Tabletop => ScenarioRun.Tabletop;

			private readonly Object[] m_extraObjects;

			internal ResultTestContext(
				CardDefinition participant,
				CardDefinition product,
				ActionDefinition action,
				ScenarioDefinition scenario,
				ScenarioRegionDefinition region,
				ScenarioRun scenarioRun,
				TabletopCards state,
				TabletopCard source,
				TabletopCard target,
				Vector2 sourcePosition,
				ActionCandidate candidate,
				IReadOnlyList<Object> extraObjects = null)
			{
				Participant = participant;
				Product = product;
				Action = action;
				Scenario = scenario;
				Region = region;
				ScenarioRun = scenarioRun;
				State = state;
				Source = source;
				Target = target;
				SourcePosition = sourcePosition;
				Candidate = candidate;
				m_extraObjects = extraObjects?.ToArray() ?? Array.Empty<Object>();
			}

			public void Dispose()
			{
				Object.DestroyImmediate((Object)(object)Participant);
				Object.DestroyImmediate((Object)(object)Product);
				Object.DestroyImmediate((Object)(object)Action);
				Object.DestroyImmediate((Object)(object)Scenario);
				Object.DestroyImmediate((Object)(object)Region);
				for (int i = 0; i < m_extraObjects.Length; i++)
				{
					Object.DestroyImmediate(m_extraObjects[i]);
				}
			}
		}

		private sealed class ResultBranchDefinition
		{
			internal string Key { get; }

			internal int Weight { get; }

			internal IReadOnlyList<ActionResultIntent> Intents { get; }

			internal ResultBranchDefinition(string key, int weight, IReadOnlyList<ActionResultIntent> intents)
			{
				Key = key;
				Weight = weight;
				Intents = intents;
			}
		}

		private const string ParticipantContentId = "test.result.participant";

		private const string ProductContentId = "test.result.product";

		private const string ActionContentId = "test.result.action";

		private const string ParticipantSlotKey = "participant";

		[SetUp]
		public void SetUp()
		{
			InvokeFormalGasBootstrap("Shutdown");
			InvokeFormalGasBootstrap("EnsureInitialized");
		}

		[TearDown]
		public void TearDown()
		{
			InvokeFormalGasBootstrap("Shutdown");
		}

		[Test]
		public void StartAction_ImmediateActionRemovesBoundCardsAndCreatesProductsAtomically()
		{
			using ResultTestContext context = CreateContext(0, CreateRemoveIntent(), CreateProductIntent("test.result.product", 2));
			ActionInstance actionInstance = context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
			Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Completed));
			Assert.That<IReadOnlyList<ActionInstance>>(context.Tabletop.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			AssertSettled(context, 2);
		}

		[Test]
		public void StartAction_ExplorationGeneratesWeightedLootFromAreaDeterministically()
		{
			using ResultTestContext firstContext = CreateExplorationContext(12345u);
			firstContext.Tabletop.StartAction(ActionRequest.FromCandidate(firstContext.Candidate));

			ContentId firstLootContentId = FindExplorationLootContentId(firstContext);
			Assert.That(firstContext.Tabletop.ActiveActions, Is.Empty);
			Assert.That(firstContext.State.CardCount, Is.EqualTo(3));
			Assert.That(firstLootContentId.IsValid, Is.True);

			using ResultTestContext secondContext = CreateExplorationContext(12345u);
			secondContext.Tabletop.StartAction(ActionRequest.FromCandidate(secondContext.Candidate));

			Assert.That(FindExplorationLootContentId(secondContext), Is.EqualTo(firstLootContentId));
		}

		[Test]
		public void StartAction_UsingCardsConsumesOneUseAndRemovesThemOnlyWhenDepleted()
		{
			using ResultTestContext context = CreateContextCore(
				0,
				null,
				new ActionResultIntent[] { CreateUseIntent() },
				Array.Empty<ResultBranchDefinition>(),
				participantInitialUses: 2);

			context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));

			Assert.That(context.State.TryGetCard(context.Source.Id, out TabletopCard source), Is.True);
			Assert.That(context.State.TryGetCard(context.Target.Id, out TabletopCard target), Is.True);
			Assert.That(source.RemainingUses, Is.EqualTo(1));
			Assert.That(target.RemainingUses, Is.EqualTo(1));

			context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));

			Assert.That(context.State.TryGetCard(context.Source.Id, out _), Is.False);
			Assert.That(context.State.TryGetCard(context.Target.Id, out _), Is.False);
		}

		[Test]
		public void TryDropStackOnto_RunningCraftAcceptsMatchingIngredientWithoutResettingProgress()
		{
			CardDefinition material = CreateTaggedCardDefinition("test.running-craft.material", XTag.Card_Category_Resource);
			CardDefinition otherStackableCard = CreateTaggedCardDefinition("test.running-craft.other", XTag.Card_Category_Character);
			ActionDefinition action = CreateStackCraftCraftActionDefinition(
				"test.running-craft.action",
				material.ContentId.Value,
				turnCost: 2,
				allowExcessCardsInStack: true,
				recheckStackAfterCompletion: false);
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			try
			{
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.running-craft.region\"}}",
					region);
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.running-craft.scenario\"}," +
					"\"m_initialRegionId\":{\"m_value\":\"test.running-craft.region\"}," +
					"\"m_regionIds\":[{\"m_value\":\"test.running-craft.region\"}]}",
					scenario);
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { material, otherStackableCard, action, region, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.DiscoverContent(action.ContentId);
				Gameplay.Tabletop.Tabletop tabletop = run.Tabletop;
				TabletopCard first = tabletop.CreateCard(material.ContentId, Vector2.zero);
				TabletopCard second = tabletop.CreateCard(material.ContentId, Vector2.zero);
				tabletop.Cards.MergeStackOnto(second.Id, first.Id);
				TabletopCardStack craftStack = tabletop.Cards.GetStackContaining(first.Id);
				ActionCandidate[] candidates = run.FindStackActionCandidates(craftStack);
				Assert.That(candidates, Has.Length.EqualTo(1));
				ActionInstance runningAction = run.StartAction(ActionRequest.FromCandidate(candidates[0]));
				run.ConfirmTurn();
				Assert.That(runningAction.ProgressedTurns, Is.EqualTo(1f));

				TabletopCard blockedCard = tabletop.CreateCard(otherStackableCard.ContentId, Vector2.one);
				Assert.That(tabletop.TryDropStackOnto(blockedCard.Id, craftStack.BottomCard.Id, blockedCard.Stack.Position, out _), Is.False);
				TabletopCard extraIngredient = tabletop.CreateCard(material.ContentId, Vector2.one);
				TabletopCard carriedTail = tabletop.CreateCard(otherStackableCard.ContentId, Vector2.one);
				tabletop.Cards.MergeStackOnto(carriedTail.Id, extraIngredient.Id);

				Assert.That(tabletop.TryDropStackOnto(extraIngredient.Id, craftStack.BottomCard.Id, extraIngredient.Stack.Position, out TabletopCardStack mergedStack), Is.True);

				Assert.That(tabletop.ActiveActions, Has.Count.EqualTo(1));
				Assert.That(tabletop.ActiveActions[0], Is.SameAs(runningAction));
				Assert.That(runningAction.ProgressedTurns, Is.EqualTo(1f));
				Assert.That(mergedStack.Cards, Has.Count.EqualTo(4));
			}
			finally
			{
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(otherStackableCard);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(region);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void ConfirmTurn_RechecksStackAfterCraftCompletionAndStartsUniqueNextCraft()
		{
			CardDefinition material = CreateTaggedCardDefinition("test.recheck-craft.material", XTag.Card_Category_Resource);
			ActionDefinition action = CreateStackCraftCraftActionDefinition(
				"test.recheck-craft.action",
				material.ContentId.Value,
				turnCost: 1,
				allowExcessCardsInStack: true,
				recheckStackAfterCompletion: true);
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			try
			{
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.recheck-craft.region\"}}",
					region);
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.recheck-craft.scenario\"}," +
					"\"m_initialRegionId\":{\"m_value\":\"test.recheck-craft.region\"}," +
					"\"m_regionIds\":[{\"m_value\":\"test.recheck-craft.region\"}]}",
					scenario);
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { material, action, region, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.DiscoverContent(action.ContentId);
				Gameplay.Tabletop.Tabletop tabletop = run.Tabletop;
				TabletopCard first = tabletop.CreateCard(material.ContentId, Vector2.zero);
				TabletopCard second = tabletop.CreateCard(material.ContentId, Vector2.zero);
				TabletopCard third = tabletop.CreateCard(material.ContentId, Vector2.zero);
				TabletopCard fourth = tabletop.CreateCard(material.ContentId, Vector2.zero);
				tabletop.Cards.MergeStackOnto(second.Id, first.Id);
				tabletop.Cards.MergeStackOnto(third.Id, first.Id);
				tabletop.Cards.MergeStackOnto(fourth.Id, first.Id);
				TabletopCardStack craftStack = tabletop.Cards.GetStackContaining(first.Id);
				ActionCandidate[] candidates = run.FindStackActionCandidates(craftStack);
				Assert.That(candidates, Has.Length.EqualTo(1));
				ActionInstance firstAction = run.StartAction(ActionRequest.FromCandidate(candidates[0]));

				run.ConfirmTurn();

				Assert.That(firstAction.State, Is.EqualTo(ActionInstanceState.Completed));
				Assert.That(tabletop.Cards.CardCount, Is.EqualTo(2));
				Assert.That(tabletop.ActiveActions, Has.Count.EqualTo(1));
				Assert.That(tabletop.ActiveActions[0], Is.Not.SameAs(firstAction));
				Assert.That(tabletop.ActiveActions[0].ProgressedTurns, Is.Zero);

				run.ConfirmTurn();

				Assert.That(tabletop.ActiveActions, Is.Empty);
				Assert.That(tabletop.Cards.CardCount, Is.Zero);
				Assert.That(tabletop.Cards.StackCount, Is.Zero);
			}
			finally
			{
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(region);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void ConfirmTurn_RechecksStackAfterCraftCompletionAndSelectsWeightedRecipeCandidate()
		{
			CardDefinition material = CreateTaggedCardDefinition("test.weighted-recheck.material", XTag.Card_Category_Resource);
			ActionDefinition zeroWeightAction = CreateStackCraftCraftActionDefinition(
				"test.weighted-recheck.action.zero",
				material.ContentId.Value,
				turnCost: 1,
				allowExcessCardsInStack: true,
				recheckStackAfterCompletion: true);
			ActionDefinition weightedAction = CreateStackCraftCraftActionDefinition(
				"test.weighted-recheck.action.weighted",
				material.ContentId.Value,
				turnCost: 1,
				allowExcessCardsInStack: true,
				recheckStackAfterCompletion: true);
			SetRecipeSelectionWeight(zeroWeightAction, 0f);
			SetRecipeSelectionWeight(weightedAction, 1f);
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			try
			{
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.weighted-recheck.region\"}}",
					region);
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.weighted-recheck.scenario\"}," +
					"\"m_initialRegionId\":{\"m_value\":\"test.weighted-recheck.region\"}," +
					"\"m_regionIds\":[{\"m_value\":\"test.weighted-recheck.region\"}]}",
					scenario);
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { material, zeroWeightAction, weightedAction, region, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.DiscoverContent(zeroWeightAction.ContentId);
				run.DiscoverContent(weightedAction.ContentId);
				Gameplay.Tabletop.Tabletop tabletop = run.Tabletop;
				TabletopCard first = tabletop.CreateCard(material.ContentId, Vector2.zero);
				TabletopCard second = tabletop.CreateCard(material.ContentId, Vector2.zero);
				TabletopCard third = tabletop.CreateCard(material.ContentId, Vector2.zero);
				TabletopCard fourth = tabletop.CreateCard(material.ContentId, Vector2.zero);
				tabletop.Cards.MergeStackOnto(second.Id, first.Id);
				tabletop.Cards.MergeStackOnto(third.Id, first.Id);
				tabletop.Cards.MergeStackOnto(fourth.Id, first.Id);
				TabletopCardStack craftStack = tabletop.Cards.GetStackContaining(first.Id);
				ActionCandidate[] candidates = run.FindStackActionCandidates(craftStack);
				Assert.That(candidates, Has.Length.EqualTo(2));
				ActionCandidate zeroWeightCandidate = candidates.First(candidate =>
					candidate.Action.ContentId == zeroWeightAction.ContentId);
				ActionInstance firstAction = run.StartAction(ActionRequest.FromCandidate(zeroWeightCandidate));

				run.ConfirmTurn();

				Assert.That(firstAction.State, Is.EqualTo(ActionInstanceState.Completed));
				Assert.That(tabletop.Cards.CardCount, Is.EqualTo(2));
				Assert.That(tabletop.ActiveActions, Has.Count.EqualTo(1));
				Assert.That(tabletop.ActiveActions[0].ActionId, Is.EqualTo(weightedAction.ContentId));
				Assert.That(tabletop.ActiveActions[0].ProgressedTurns, Is.Zero);
			}
			finally
			{
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(zeroWeightAction);
				Object.DestroyImmediate(weightedAction);
				Object.DestroyImmediate(region);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void CardSnapshot_PreservesRemainingUses()
		{
			using ResultTestContext context = CreateContextCore(
				0,
				null,
				new ActionResultIntent[] { CreateUseIntent() },
				Array.Empty<ResultBranchDefinition>(),
				participantInitialUses: 3);
			context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
			TabletopCardStateSnapshot snapshot = JsonUtility.FromJson<TabletopCardStateSnapshot>(
				JsonUtility.ToJson(context.State.CreateSnapshot()));
			TabletopCards restored = TabletopCards.Restore(
				snapshot,
				new TabletopCardIdSequence(context.State.CardIdSequence.NextValue));

			Assert.That(restored.TryGetCard(context.Source.Id, out TabletopCard restoredSource), Is.True);
			Assert.That(restoredSource.RemainingUses, Is.EqualTo(2));
			Assert.That(restored.TryGetCard(context.Target.Id, out TabletopCard restoredTarget), Is.True);
			Assert.That(restoredTarget.RemainingUses, Is.EqualTo(2));
		}

		[Test]
		public void ActiveActionSnapshot_PreservesFrozenCardUseResults()
		{
			using ResultTestContext context = CreateContextCore(
				2,
				null,
				new ActionResultIntent[] { CreateUseIntent() },
				Array.Empty<ResultBranchDefinition>(),
				participantInitialUses: 3);
			context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
			ActionInstanceSnapshot snapshot = JsonUtility.FromJson<ActionInstanceSnapshot>(
				JsonUtility.ToJson(context.Tabletop.CreateActiveActionSnapshots()[0]));

			ActionResultPlan restoredPlan = snapshot.RestoreResultPlan();

			CollectionAssert.AreEquivalent(
				new[] { context.Source.Id, context.Target.Id },
				restoredPlan.UseCardIds);
		}

		[Test]
		public void StartAction_WhenSpawnProductsDoNotFullyConvergeStillCommitsStackCraftSpawn()
		{
			using ResultTestContext context = CreateConstrainedContext(
				CreateRemoveIntent(),
				CreateProductIntent(ProductContentId, 1),
				CreateProductIntent(ProductContentId, 1),
				CreateProductIntent(ProductContentId, 1));
			ulong originalRevision = context.State.Revision;

			ActionInstance actionInstance = context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));

			Assert.That(actionInstance.State, Is.EqualTo(ActionInstanceState.Completed));
			Assert.That(context.State.Revision, Is.GreaterThan(originalRevision));
			Assert.That(context.State.TryGetCard(context.Source.Id, out _), Is.False);
			Assert.That(context.State.TryGetCard(context.Target.Id, out _), Is.False);
			Assert.That(
				context.State.Stacks.SelectMany(stack => stack.Cards)
					.Count(card => card.ContentId == context.Product.ContentId),
				Is.EqualTo(3));
			AssertStackFootprintsStayInsideBoard(context);
		}

		[Test]
		public void StartAction_PublishesCompletionFactAfterSuccessfulResultCommit()
		{
			ResultTestContext context = CreateContext(0, CreateRemoveIntent(), CreateProductIntent("test.result.product", 2));
			ActionCompletedEvent? receivedEvent;
			try
			{
				receivedEvent = null;
				EventKit.Type.Register<ActionCompletedEvent>(OnActionCompleted);
				try
				{
					context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				}
				finally
				{
					EventKit.Type.UnRegister<ActionCompletedEvent>(OnActionCompleted);
				}
				Assert.That<bool>(receivedEvent.HasValue, (IResolveConstraint)(object)Is.True);
				Assert.That<ContentId>(receivedEvent.Value.ActionId, (IResolveConstraint)(object)Is.EqualTo((object)context.Action.ContentId));
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
			void OnActionCompleted(ActionCompletedEvent completedEvent)
			{
				AssertSettled(context, 2);
				receivedEvent = completedEvent;
			}
		}

		[Test]
		public void ConfirmedWorldTurn_DelayedActionSettlesOnlyAfterRequiredTurnsComplete()
		{
			using ResultTestContext context = CreateContext(2, CreateRemoveIntent(), CreateProductIntent("test.result.product", 1));
			ActionInstance actionInstance = context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
			context.ScenarioRun.ConfirmTurn();
			Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Running));
			AssertUnchanged(context);
			context.ScenarioRun.ConfirmTurn();
			Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Completed));
			Assert.That<IReadOnlyList<ActionInstance>>(context.Tabletop.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			AssertSettled(context, 1);
		}

		[Test]
		public void StartedAction_UsesTheResultPlanCommittedAtStart()
		{
			using ResultTestContext context = CreateContext(1, CreateRemoveIntent(), CreateProductIntent("test.result.product", 1));
			ActionInstance actionInstance = context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
			SetResultIntents(context.Action, CreateProductIntent("test.result.product", 2));
			context.ScenarioRun.ConfirmTurn();
			Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Completed));
			AssertSettled(context, 1);
		}

		[Test]
		public void SellCards_FreezesSellValueAtActionStartAndCreatesCurrencyCards()
		{
			using ResultTestContext context = CreateSellContext();
			SetSellValue(context.Participant, 2);

			ActionInstance actionInstance = context.Tabletop.StartAction(
				ActionRequest.FromCandidate(context.Candidate));
			SetSellValue(context.Participant, 9);
			context.ScenarioRun.ConfirmTurn();

			Assert.That(actionInstance.State, Is.EqualTo(ActionInstanceState.Completed));
			Assert.That(context.State.TryGetCard(context.Source.Id, out _), Is.False);
			Assert.That(context.State.TryGetCard(context.Target.Id, out TabletopCard buyerCard), Is.True);
			Assert.That(buyerCard.ContentId, Is.EqualTo(context.Target.ContentId));
			AssertCurrencyCards(context, 2);
		}

		[Test]
		public void ContentIndexBuild_UnknownProductIsRejectedBeforeRuntimeSettlement()
		{
			CardDefinition participant = CreateCardDefinition("test.result.participant");
			CardDefinition product = CreateCardDefinition("test.result.product");
			ActionDefinition action = CreateActionDefinition(0, new ActionResultIntent[2]
			{
				CreateRemoveIntent(),
				CreateProductIntent("test.result.missing", 1)
			}, Array.Empty<ResultBranchDefinition>());
			try
			{
				InvalidOperationException exception = Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					ContentIndex.Build(new ContentAsset[3] { participant, product, action });
				});
				StringAssert.Contains("ACTION_RESULT_CREATE_CONTENT_UNKNOWN", exception.Message);
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)participant);
				Object.DestroyImmediate((Object)(object)product);
				Object.DestroyImmediate((Object)(object)action);
			}
		}

		[Test]
		public void StartAction_DuplicateRemovalLeavesTabletopStateUnchanged()
		{
			ResultTestContext context = CreateContext(0, CreateRemoveIntent(), CreateRemoveIntent());
			int completedEventCount;
			try
			{
				completedEventCount = 0;
				EventKit.Type.Register<ActionCompletedEvent>(OnActionCompleted);
				try
				{
					Assert.Throws<InvalidOperationException>((TestDelegate)delegate
					{
						context.Tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
					});
				}
				finally
				{
					EventKit.Type.UnRegister<ActionCompletedEvent>(OnActionCompleted);
				}
				AssertUnchanged(context);
				Assert.That<int>(completedEventCount, (IResolveConstraint)(object)Is.Zero);
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
			void OnActionCompleted(ActionCompletedEvent _)
			{
				completedEventCount++;
			}
		}

		[Test]
		public void StartAction_SettledCandidateMustBeQueriedAgain()
		{
			ResultTestContext context = CreateContext(0, CreateRemoveIntent(), CreateProductIntent("test.result.product", 1));
			try
			{
				context.ScenarioRun.DiscoverContent(context.Action.ContentId);
				context.ScenarioRun.StartAction(ActionRequest.FromCandidate(context.Candidate));
				Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					context.ScenarioRun.StartAction(ActionRequest.FromCandidate(context.Candidate));
				});
				AssertSettled(context, 1);
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
		}

		[Test]
		public void StartAction_WeightedResultUsesAuthoritativeSeedAndSettlesSelectedBranch()
		{
			ResultBranchDefinition[] branches = new ResultBranchDefinition[2]
			{
				CreateBranch("one-product", 1, CreateProductIntent("test.result.product", 1)),
				CreateBranch("two-products", 3, CreateProductIntent("test.result.product", 2))
			};
			using ResultTestContext first = CreateContextWithBranches(12345u, branches);
			using ResultTestContext replay = CreateContextWithBranches(12345u, branches);
			ActionInstance firstAction = first.Tabletop.StartAction(
				ActionRequest.FromCandidate(first.Candidate));
			ActionInstance replayAction = replay.Tabletop.StartAction(
				ActionRequest.FromCandidate(replay.Candidate));

			Assert.That(replayAction.ResultBranchKey, Is.EqualTo(firstAction.ResultBranchKey));
			int expectedProductCount = firstAction.ResultBranchKey == "one-product" ? 1 : 2;
			AssertSettled(first, expectedProductCount);
			AssertSettled(replay, expectedProductCount);
		}

		[Test]
		public void Research_SelectsOnlyUndiscoveredContentAndStopsCreatingCardsWhenComplete()
		{
			CardDefinition participant = CreateCardDefinition(ParticipantContentId);
			CardDefinition firstRecipeCard = CreateCardDefinition("test.research.recipe-card.first");
			CardDefinition secondRecipeCard = CreateCardDefinition("test.research.recipe-card.second");
			ActionDefinition firstUnlockedAction = CreateEmptyActionDefinition("test.research.unlocked-action.first");
			ActionDefinition secondUnlockedAction = CreateEmptyActionDefinition("test.research.unlocked-action.second");
			ResearchDiscoveryResultIntent researchIntent = CreateResearchIntent(
				(firstUnlockedAction.ContentId, firstRecipeCard.ContentId),
				(secondUnlockedAction.ContentId, secondRecipeCard.ContentId));
			ActionDefinition researchAction = CreateActionDefinition(
				2,
				new ActionResultIntent[] { researchIntent });
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.scenario.research.region\"}}",
				region);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.scenario.research\"}," +
				"\"m_initialRegionId\":{\"m_value\":\"test.scenario.research.region\"}," +
				"\"m_regionIds\":[{\"m_value\":\"test.scenario.research.region\"}]}",
				scenario);

			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[]
				{
					participant,
					firstRecipeCard,
					secondRecipeCard,
					firstUnlockedAction,
					secondUnlockedAction,
					researchAction,
					region,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				TabletopCard source = run.Tabletop.CreateCard(participant.ContentId, new Vector2(-1f, 0f));
				TabletopCard target = run.Tabletop.CreateCard(participant.ContentId, new Vector2(1f, 0f));
				TabletopCardPointerReleaseIntent pointerIntent = new(
					source.Id,
					new Vector2(-1f, 0f),
					new Vector2(1f, 0f),
					new Vector2(-1f, 0f),
					isDrag: true,
					target.Id);
				ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(
					pointerIntent,
					run.Tabletop.Cards,
					contentIndex,
					new[] { researchAction });

				Assert.That(candidates, Has.Length.EqualTo(1));
				run.DiscoverContent(firstUnlockedAction.ContentId);
				run.Tabletop.StartAction(ActionRequest.FromCandidate(candidates[0]));
				ActionInstanceSnapshot snapshot = JsonUtility.FromJson<ActionInstanceSnapshot>(
					JsonUtility.ToJson(run.Tabletop.CreateActiveActionSnapshots()[0]));

				ActionResultPlan restoredPlan = snapshot.RestoreResultPlan();
				Assert.That(restoredPlan.ResearchDiscoveries.Count, Is.EqualTo(1));
				Assert.That(restoredPlan.ResearchDiscoveries[0].Entries.Count, Is.EqualTo(2));
				run.ConfirmTurn();
				run.ConfirmTurn();

				Assert.That(run.IsContentDiscovered(secondUnlockedAction.ContentId), Is.True);
				Assert.That(
					run.Tabletop.Cards.Stacks.SelectMany(stack => stack.Cards)
						.Count(card => card.ContentId == firstRecipeCard.ContentId),
					Is.Zero);
				Assert.That(
					run.Tabletop.Cards.Stacks.SelectMany(stack => stack.Cards)
						.Count(card => card.ContentId == secondRecipeCard.ContentId),
					Is.EqualTo(1));

				run.Tabletop.StartAction(ActionRequest.FromCandidate(candidates[0]));
				run.ConfirmTurn();
				run.ConfirmTurn();

				Assert.That(
					run.Tabletop.Cards.Stacks.SelectMany(stack => stack.Cards)
						.Count(card => card.ContentId == secondRecipeCard.ContentId),
					Is.EqualTo(1));
			}
			finally
			{
				Object.DestroyImmediate(participant);
				Object.DestroyImmediate(firstRecipeCard);
				Object.DestroyImmediate(secondRecipeCard);
				Object.DestroyImmediate(firstUnlockedAction);
				Object.DestroyImmediate(secondUnlockedAction);
				Object.DestroyImmediate(researchAction);
				Object.DestroyImmediate(region);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void ContentIndexBuild_InvalidWeightedResultIsRejectedBeforeRuntimeRandom()
		{
			CardDefinition participant = CreateCardDefinition("test.result.participant");
			CardDefinition product = CreateCardDefinition("test.result.product");
			ActionDefinition action = CreateActionDefinition(0, new RemoveCardsResultIntent[1] { CreateRemoveIntent() }, new ResultBranchDefinition[1] { CreateBranch("invalid", 0, CreateProductIntent("test.result.product", 1)) });
			try
			{
				InvalidOperationException exception = Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					ContentIndex.Build(new ContentAsset[3] { participant, product, action });
				});
				StringAssert.Contains("ACTION_RESULT_BRANCH_WEIGHT_INVALID", exception.Message);
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)participant);
				Object.DestroyImmediate((Object)(object)product);
				Object.DestroyImmediate((Object)(object)action);
			}
		}

		private static ResultTestContext CreateContext(int turnCost, params ActionResultIntent[] resultIntents)
		{
			return CreateContextCore(turnCost, null, resultIntents, Array.Empty<ResultBranchDefinition>());
		}

		private static ResultTestContext CreateContextWithBranches(uint? seed, params ResultBranchDefinition[] branches)
		{
			return CreateContextCore(0, seed, new RemoveCardsResultIntent[1] { CreateRemoveIntent() }, branches);
		}

		private static ResultTestContext CreateConstrainedContext(params ActionResultIntent[] resultIntents)
		{
			return CreateContextCore(
				0,
				null,
				resultIntents,
				Array.Empty<ResultBranchDefinition>(),
				configureRegion: region =>
				{
					SerializedObject serializedRegion = new SerializedObject(region);
					SerializedProperty placement = serializedRegion.FindProperty("m_tabletopPlacement");
					placement.FindPropertyRelative("m_bounds").rectValue = new Rect(-2.25f, -1.2f, 4.5f, 2.4f);
					placement.FindPropertyRelative("m_cardSize").vector2Value = new Vector2(2f, 2f);
					placement.FindPropertyRelative("m_stackStep").vector2Value = Vector2.zero;
					serializedRegion.ApplyModifiedPropertiesWithoutUndo();
				});
		}

		private static ResultTestContext CreateSellContext()
		{
			CardDefinition participant = CreateCardDefinition("test.result.participant");
			CardDefinition product = CreateCardDefinition("test.result.product");
			CardBuyerDefinition buyer = CreateBuyerDefinition("test.result.buyer", product.ContentId);
			ActionDefinition action = CreateSellActionDefinition(
				1,
				new ActionResultIntent[] { CreateSellIntent(product.ContentId.Value, "sold", "buyer") });
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.scenario.sell.region\"}}",
				region);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.scenario.sell\"}," +
				"\"m_initialRegionId\":{\"m_value\":\"test.scenario.sell.region\"}," +
				"\"m_regionIds\":[{\"m_value\":\"test.scenario.sell.region\"}]}",
				scenario);
			ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { participant, product, buyer, action, region, scenario });
			ScenarioRun scenarioRun = new ScenarioRun(scenario, contentIndex, 12345u);
			Gameplay.Tabletop.Tabletop tabletop = scenarioRun.Tabletop;
			TabletopCards state = tabletop.Cards;
			Vector2 soldPosition = new Vector2(-1f, 0f);
			Vector2 buyerPosition = new Vector2(1f, 0f);
			TabletopCard soldCard = tabletop.CreateCard(participant.ContentId, soldPosition);
			TabletopCard buyerCard = tabletop.CreateCard(buyer.ContentId, buyerPosition);
			TabletopCardPointerReleaseIntent pointerIntent = new TabletopCardPointerReleaseIntent(
				soldCard.Id,
				soldPosition,
				buyerPosition,
				soldPosition,
				isDrag: true,
				buyerCard.Id);
			ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(
				pointerIntent,
				state,
				contentIndex,
				new[] { action });
			Assert.That(candidates, Has.Length.EqualTo(1));
			Assert.That(candidates[0].IsReady, Is.True);
			return new ResultTestContext(
				participant,
				product,
				action,
				scenario,
				region,
				scenarioRun,
				state,
				soldCard,
				buyerCard,
				soldPosition,
				candidates[0],
				new Object[] { buyer });
		}

		private static ResultTestContext CreateContextCore(
			int turnCost,
			uint? seed,
			IReadOnlyList<ActionResultIntent> resultIntents,
			IReadOnlyList<ResultBranchDefinition> branches,
			Action<ScenarioRegionDefinition> configureRegion = null,
			int participantInitialUses = 1)
		{
			CardDefinition participant = CreateCardDefinition("test.result.participant", participantInitialUses);
			CardDefinition product = CreateCardDefinition("test.result.product");
			ActionDefinition action = CreateActionDefinition(turnCost, resultIntents, branches);
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.scenario.result-settlement.region\"}}",
				region);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.scenario.result-settlement\"}," +
				"\"m_initialRegionId\":{\"m_value\":\"test.scenario.result-settlement.region\"}," +
				"\"m_regionIds\":[{\"m_value\":\"test.scenario.result-settlement.region\"}]}",
				scenario);
			configureRegion?.Invoke(region);
			ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[5] { participant, product, action, region, scenario });
			ScenarioRun scenarioRun = new ScenarioRun(
				scenario,
				contentIndex,
				seed ?? 12345u);
			Gameplay.Tabletop.Tabletop tabletop = scenarioRun.Tabletop;
			TabletopCards state = tabletop.Cards;
			Vector2 sourcePosition = new Vector2(-1f, 0f);
			Vector2 targetPosition = new Vector2(1f, 0f);
			TabletopCard source = tabletop.CreateCard(participant.ContentId, sourcePosition);
			TabletopCard target = tabletop.CreateCard(participant.ContentId, targetPosition);
			TabletopCardPointerReleaseIntent pointerIntent = new TabletopCardPointerReleaseIntent(source.Id, sourcePosition, targetPosition, sourcePosition, isDrag: true, target.Id);
			ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(pointerIntent, state, contentIndex, new ActionDefinition[1] { action });
			Assert.That<ActionCandidate[]>(candidates, (IResolveConstraint)(object)((ConstraintExpression)Has.Length).EqualTo((object)1));
			Assert.That<bool>(candidates[0].IsReady, (IResolveConstraint)(object)Is.True);
			return new ResultTestContext(participant, product, action, scenario, region, scenarioRun, state, source, target, sourcePosition, candidates[0]);
		}

		private static ResultTestContext CreateExplorationContext(uint seed)
		{
			CardDefinition area = CreateTaggedCardDefinition(ParticipantContentId, XTag.Card_Category_Area);
			CardDefinition firstProduct = CreateCardDefinition(ProductContentId);
			CardDefinition secondProduct = CreateCardDefinition("test.result.exploration-product-second");
			SetCardLoot(
				area,
				new[]
				{
					(firstProduct.ContentId.Value, 1),
					(secondProduct.ContentId.Value, 1)
				});
			ActionDefinition action = CreateActionDefinition(0, new ActionResultIntent[] { CreateExploreLootIntent() });
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.scenario.exploration.region\"}}",
				region);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.scenario.exploration\"}," +
				"\"m_initialRegionId\":{\"m_value\":\"test.scenario.exploration.region\"}," +
				"\"m_regionIds\":[{\"m_value\":\"test.scenario.exploration.region\"}]}",
				scenario);
			ContentIndex contentIndex = ContentIndex.Build(
				new ContentAsset[] { area, firstProduct, secondProduct, action, region, scenario });
			ScenarioRun scenarioRun = new ScenarioRun(scenario, contentIndex, seed);
			Gameplay.Tabletop.Tabletop tabletop = scenarioRun.Tabletop;
			TabletopCards state = tabletop.Cards;
			Vector2 sourcePosition = new Vector2(-1f, 0f);
			Vector2 targetPosition = new Vector2(1f, 0f);
			TabletopCard source = tabletop.CreateCard(area.ContentId, sourcePosition);
			TabletopCard target = tabletop.CreateCard(area.ContentId, targetPosition);
			TabletopCardPointerReleaseIntent pointerIntent = new TabletopCardPointerReleaseIntent(
				source.Id,
				sourcePosition,
				targetPosition,
				sourcePosition,
				isDrag: true,
				target.Id);
			ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(
				pointerIntent,
				state,
				contentIndex,
				new ActionDefinition[] { action });
			Assert.That(candidates, Has.Length.EqualTo(1));
			Assert.That(candidates[0].IsReady, Is.True);
			return new ResultTestContext(
				area,
				firstProduct,
				action,
				scenario,
				region,
				scenarioRun,
				state,
				source,
				target,
				sourcePosition,
				candidates[0],
				new Object[] { secondProduct });
		}

		private static CardDefinition CreateCardDefinition(string contentId, int initialUses = 1)
		{
			CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
			SerializedObject serializedDefinition = new SerializedObject((Object)(object)definition);
			serializedDefinition.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serializedDefinition.FindProperty("m_initialUses").intValue = initialUses;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static CardDefinition CreateTaggedCardDefinition(string contentId, int tagCode)
		{
			CardDefinition definition = CreateCardDefinition(contentId);
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty tags = serializedDefinition.FindProperty("m_tagCodes");
			tags.arraySize = 1;
			tags.GetArrayElementAtIndex(0).intValue = tagCode;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static void SetCardLoot(
			CardDefinition area,
			IReadOnlyList<(string ContentId, int Weight)> entries)
		{
			SerializedObject serializedDefinition = new SerializedObject(area);
			SerializedProperty loot = serializedDefinition.FindProperty("m_loot");
			loot.arraySize = entries.Count;
			for (int i = 0; i < entries.Count; i++)
			{
				SerializedProperty entry = loot.GetArrayElementAtIndex(i);
				entry.FindPropertyRelative("m_cardId").FindPropertyRelative("m_value").stringValue = entries[i].ContentId;
				entry.FindPropertyRelative("m_weight").intValue = entries[i].Weight;
			}
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
		}

		private static ActionDefinition CreateStackCraftCraftActionDefinition(
			string actionId,
			string materialContentId,
			int turnCost,
			bool allowExcessCardsInStack,
			bool recheckStackAfterCompletion)
		{
			ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + actionId + "\"}," +
				"\"m_turnCost\":" + turnCost + "," +
				"\"m_allowExcessCardsInStack\":" + (allowExcessCardsInStack ? "true" : "false") + "," +
				"\"m_recheckStackAfterCompletion\":" + (recheckStackAfterCompletion ? "true" : "false") + "," +
				"\"m_participationSlots\":[{\"m_key\":\"material\",\"m_minimumParticipants\":2," +
				"\"m_maximumParticipants\":2,\"m_allowedContentIds\":[{\"m_value\":\"" + materialContentId + "\"}]}]}",
				action);
			SetResultIntents(action, new ActionResultIntent[] { CreateRemoveIntent("material") });
			return action;
		}

		private static void SetRecipeSelectionWeight(ActionDefinition action, float weight)
		{
			SerializedObject serializedAction = new SerializedObject(action);
			serializedAction.FindProperty("m_recipeSelectionWeight").floatValue = weight;
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
		}

		private static CardBuyerDefinition CreateBuyerDefinition(string contentId, ContentId currencyContentId)
		{
			CardBuyerDefinition definition = ScriptableObject.CreateInstance<CardBuyerDefinition>();
			SerializedObject serializedDefinition = new SerializedObject(definition);
			serializedDefinition.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serializedDefinition.FindProperty("m_currencyCardId").FindPropertyRelative("m_value").stringValue =
				currencyContentId.Value;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static ActionDefinition CreateActionDefinition(int turnCost, IReadOnlyList<ActionResultIntent> resultIntents, IReadOnlyList<ResultBranchDefinition> branches = null)
		{
			ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"test.result.action\"}," + $"\"m_turnCost\":{turnCost}," + "\"m_participationSlots\":[{\"m_key\":\"participant\",\"m_minimumParticipants\":2,\"m_maximumParticipants\":2,\"m_allowedContentIds\":[{\"m_value\":\"test.result.participant\"}]}]}", (object)action);
			SerializedObject serializedAction = new SerializedObject((Object)(object)action);
			SerializedProperty intentsProperty = serializedAction.FindProperty("m_resultIntents");
			intentsProperty.arraySize = resultIntents.Count;
			for (int i = 0; i < resultIntents.Count; i++)
			{
				intentsProperty.GetArrayElementAtIndex(i).managedReferenceValue = resultIntents[i];
			}
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
			SerializedObject branchesObject = new SerializedObject((Object)(object)action);
			SerializedProperty branchesProperty = branchesObject.FindProperty("m_resultBranches");
			branchesProperty.arraySize = branches?.Count ?? 0;
			for (int j = 0; j < (branches?.Count ?? 0); j++)
			{
				ResultBranchDefinition branchDefinition = branches[j];
				SerializedProperty branchProperty = branchesProperty.GetArrayElementAtIndex(j);
				branchProperty.FindPropertyRelative("m_key").stringValue = branchDefinition.Key;
				branchProperty.FindPropertyRelative("m_weight").intValue = branchDefinition.Weight;
				SerializedProperty branchIntents = branchProperty.FindPropertyRelative("m_resultIntents");
				branchIntents.arraySize = branchDefinition.Intents.Count;
				for (int intentIndex = 0; intentIndex < branchDefinition.Intents.Count; intentIndex++)
				{
					branchIntents.GetArrayElementAtIndex(intentIndex).managedReferenceValue = branchDefinition.Intents[intentIndex];
				}
			}
			branchesObject.ApplyModifiedPropertiesWithoutUndo();
			return action;
		}

		private static ActionDefinition CreateSellActionDefinition(
			int turnCost,
			IReadOnlyList<ActionResultIntent> resultIntents)
		{
			ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.result.action\"}," +
				$"\"m_turnCost\":{turnCost}," +
				"\"m_participationSlots\":[" +
				"{\"m_key\":\"sold\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"test.result.participant\"}]}," +
				"{\"m_key\":\"buyer\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"test.result.buyer\"}]}" +
				"]}",
				action);
			SetResultIntents(action, resultIntents.ToArray());
			return action;
		}

		private static ActionDefinition CreateEmptyActionDefinition(string contentId)
		{
			ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"},\"m_turnCost\":0}",
				action);
			return action;
		}

		private static void SetResultIntents(ActionDefinition action, params ActionResultIntent[] resultIntents)
		{
			SerializedObject serializedAction = new SerializedObject((Object)(object)action);
			SerializedProperty intentsProperty = serializedAction.FindProperty("m_resultIntents");
			intentsProperty.arraySize = resultIntents.Length;
			for (int i = 0; i < resultIntents.Length; i++)
			{
				intentsProperty.GetArrayElementAtIndex(i).managedReferenceValue = resultIntents[i];
			}
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
		}

		private static ResultBranchDefinition CreateBranch(string key, int weight, params ActionResultIntent[] intents)
		{
			return new ResultBranchDefinition(key, weight, intents);
		}

		private static RemoveCardsResultIntent CreateRemoveIntent()
		{
			return CreateRemoveIntent(ParticipantSlotKey);
		}

		private static RemoveCardsResultIntent CreateRemoveIntent(string slotKey)
		{
			RemoveCardsResultIntent intent = new RemoveCardsResultIntent();
			JsonUtility.FromJsonOverwrite("{\"m_slotKey\":\"" + slotKey + "\"}", (object)intent);
			return intent;
		}

		private static UseCardsResultIntent CreateUseIntent()
		{
			UseCardsResultIntent intent = new UseCardsResultIntent();
			JsonUtility.FromJsonOverwrite("{\"m_slotKey\":\"participant\"}", intent);
			return intent;
		}

		private static ExploreLootResultIntent CreateExploreLootIntent()
		{
			ExploreLootResultIntent intent = new ExploreLootResultIntent();
			JsonUtility.FromJsonOverwrite("{\"m_areaSlotKey\":\"participant\"}", intent);
			return intent;
		}

		private static ContentId FindExplorationLootContentId(ResultTestContext context)
		{
			foreach (TabletopCardStack stack in context.State.Stacks)
			{
				foreach (TabletopCard card in stack.Cards)
				{
					if (card.ContentId == context.Product.ContentId)
					{
						return card.ContentId;
					}
					if (card.ContentId.Value == "test.result.exploration-product-second")
					{
						return card.ContentId;
					}
				}
			}
			return default;
		}

		private static CreateCardsResultIntent CreateProductIntent(string contentId, int count)
		{
			CreateCardsResultIntent intent = new CreateCardsResultIntent();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}," + $"\"m_count\":{count}," + "\"m_anchorSlotKey\":\"participant\"}", (object)intent);
			return intent;
		}

		private static ResearchDiscoveryResultIntent CreateResearchIntent(
			params (ContentId ActionId, ContentId RecipeCardId)[] entries)
		{
			ResearchDiscoveryResultIntent intent = new ResearchDiscoveryResultIntent();
			string entriesJson = string.Join(
				",",
				entries.Select(entry =>
					"{\"m_actionId\":{\"m_value\":\"" + entry.ActionId.Value +
					"\"},\"m_recipeCardId\":{\"m_value\":\"" + entry.RecipeCardId.Value + "\"}}"));
			JsonUtility.FromJsonOverwrite(
				"{\"m_entries\":[" + entriesJson + "],\"m_anchorSlotKey\":\"participant\"}",
				intent);
			return intent;
		}

		private static SellCardsResultIntent CreateSellIntent(
			string currencyContentId,
			string soldSlotKey = "participant",
			string anchorSlotKey = "participant")
		{
			SellCardsResultIntent intent = new SellCardsResultIntent();
			JsonUtility.FromJsonOverwrite(
				"{\"m_soldSlotKey\":\"" + soldSlotKey + "\"," +
				"\"m_currencyCardId\":{\"m_value\":\"" + currencyContentId + "\"}," +
				"\"m_anchorSlotKey\":\"" + anchorSlotKey + "\"}",
				intent);
			return intent;
		}

		private static void SetSellValue(CardDefinition card, int sellValue)
		{
			SerializedObject serializedCard = new SerializedObject(card);
			serializedCard.FindProperty("m_sellValue").intValue = sellValue;
			serializedCard.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void AssertUnchanged(ResultTestContext context)
		{
			Assert.That<int>(context.State.CardCount, (IResolveConstraint)(object)Is.EqualTo((object)2));
			Assert.That<int>(context.State.StackCount, (IResolveConstraint)(object)Is.EqualTo((object)2));
			Assert.That<bool>(context.State.TryGetCard(context.Source.Id, out var tabletopCard), (IResolveConstraint)(object)Is.True);
			Assert.That<bool>(context.State.TryGetCard(context.Target.Id, out tabletopCard), (IResolveConstraint)(object)Is.True);
		}

		private static void AssertSettled(
			ResultTestContext context,
			int expectedProductCount,
			int? expectedProductStackCount = null)
		{
			Assert.That<bool>(context.State.TryGetCard(context.Source.Id, out var tabletopCard), (IResolveConstraint)(object)Is.False);
			Assert.That<bool>(context.State.TryGetCard(context.Target.Id, out tabletopCard), (IResolveConstraint)(object)Is.False);
			Assert.That<int>(context.State.CardCount, (IResolveConstraint)(object)Is.EqualTo((object)expectedProductCount));
			Assert.That<int>(
				context.State.StackCount,
				(IResolveConstraint)(object)Is.EqualTo((object)(expectedProductStackCount ?? expectedProductCount)));
			int productCount = 0;
			for (int stackIndex = 0; stackIndex < context.State.Stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = context.State.Stacks[stackIndex];
				Rect footprint = context.Tabletop.PlacementRules.Geometry.CalculateFootprint(
					stack.Position,
					stack.Cards.Count);
				Rect bounds = context.Tabletop.PlacementRules.Area.Bounds;
				Assert.That(footprint.xMin, Is.GreaterThanOrEqualTo(bounds.xMin - 0.0001f));
				Assert.That(footprint.xMax, Is.LessThanOrEqualTo(bounds.xMax + 0.0001f));
				Assert.That(footprint.yMin, Is.GreaterThanOrEqualTo(bounds.yMin - 0.0001f));
				Assert.That(footprint.yMax, Is.LessThanOrEqualTo(bounds.yMax + 0.0001f));
				for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
				{
					Assert.That<ContentId>(stack.Cards[cardIndex].ContentId, (IResolveConstraint)(object)Is.EqualTo((object)context.Product.ContentId));
					productCount++;
				}
			}
			Assert.That<int>(productCount, (IResolveConstraint)(object)Is.EqualTo((object)expectedProductCount));
		}

		private static void AssertStackFootprintsStayInsideBoard(ResultTestContext context)
		{
			Rect bounds = context.Tabletop.PlacementRules.Area.Bounds;
			for (int stackIndex = 0; stackIndex < context.State.Stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = context.State.Stacks[stackIndex];
				Rect footprint = context.Tabletop.PlacementRules.Geometry.CalculateFootprint(
					stack.Position,
					stack.Cards.Count);
				Assert.That(footprint.xMin, Is.GreaterThanOrEqualTo(bounds.xMin - 0.0001f));
				Assert.That(footprint.xMax, Is.LessThanOrEqualTo(bounds.xMax + 0.0001f));
				Assert.That(footprint.yMin, Is.GreaterThanOrEqualTo(bounds.yMin - 0.0001f));
				Assert.That(footprint.yMax, Is.LessThanOrEqualTo(bounds.yMax + 0.0001f));
			}
		}

		private static void AssertCurrencyCards(ResultTestContext context, int expectedCurrencyCount)
		{
			int currencyCount = 0;
			for (int stackIndex = 0; stackIndex < context.State.Stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = context.State.Stacks[stackIndex];
				for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
				{
					if (stack.Cards[cardIndex].ContentId == context.Product.ContentId)
					{
						currencyCount++;
					}
				}
			}
			Assert.That(currencyCount, Is.EqualTo(expectedCurrencyCount));
			Assert.That(context.State.CardCount, Is.EqualTo(expectedCurrencyCount + 1));
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
