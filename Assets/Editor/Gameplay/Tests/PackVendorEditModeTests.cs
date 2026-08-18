using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Gameplay.Scenarios;
using System;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	public sealed class PackVendorEditModeTests
	{
		[Test]
		public void Vendor_UnlocksFromCompletedQuestCountWithoutSavingDuplicateUnlockState()
		{
			PackVendorDefinition definition = CreateVendorDefinition(
				"test.vendor",
				"test.pack",
				price: 2,
				minimumCompletedQuests: 3);
			try
			{
				Assert.That(definition.IsUnlocked(2), Is.False);
				Assert.That(definition.IsUnlocked(3), Is.True);
			}
			finally
			{
				Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void Vendor_AcceptsPartialPaymentsAndResetsOnlyAfterPurchase()
		{
			PackVendorCard vendor = new PackVendorCard(
				new TabletopCardId(1),
				new ContentId("test.vendor"),
				price: 2);

			Assert.That(vendor.Pay(1), Is.False);
			Assert.That(vendor.PaidAmount, Is.EqualTo(1));
			Assert.That(vendor.RemainingPrice, Is.EqualTo(1));
			Assert.That(vendor.Pay(1), Is.True);
			Assert.That(vendor.PaidAmount, Is.EqualTo(2));

			vendor.CompletePurchase();
			Assert.That(vendor.PaidAmount, Is.Zero);
			Assert.That(vendor.RemainingPrice, Is.EqualTo(2));
		}

		[Test]
		public void Vendor_RuntimeStateRoundTripsThroughCardSnapshot()
		{
			PackVendorCard vendor = new PackVendorCard(
				new TabletopCardId(7),
				new ContentId("test.vendor"),
				price: 3);
			vendor.Pay(2);

			TabletopCardSnapshot snapshot = JsonUtility.FromJson<TabletopCardSnapshot>(
				JsonUtility.ToJson(vendor.CreateSnapshot()));
			PackVendorCard restored = new PackVendorCard(
				snapshot.CardId,
				snapshot.ContentId,
				price: 3,
				snapshot.RuntimeState);

			Assert.That(restored.PaidAmount, Is.EqualTo(2));
			Assert.That(restored.RemainingPrice, Is.EqualTo(1));
		}

		[Test]
		public void ScenarioRun_LockedVendorDoesNotOfferPurchaseCandidate()
		{
			using VendorScenarioContext context = CreateScenario(minimumCompletedQuests: 1);
			TabletopCard coin = context.Run.Tabletop.CreateCard(context.Coin.ContentId, new Vector2(-1f, 0f));
			TabletopCard vendor = context.Run.Tabletop.CreateCard(context.Vendor.ContentId, new Vector2(1f, 0f));

			Assert.That(context.FindCandidates(coin, vendor), Is.Empty);
		}

		[Test]
		public void ScenarioRun_CompletingQuestAcrossVendorRequirementRequestsUnlockCues()
		{
			CardDefinition worker = CreateCard("test.vendor.unlock.worker");
			CardDefinition reward = CreateCard("test.vendor.unlock.reward");
			CardPackDefinition pack = CreatePack("test.vendor.unlock.pack", reward.ContentId.Value);
			PackVendorDefinition vendor = CreateVendorDefinition(
				"test.vendor.unlock.offer",
				pack.ContentId.Value,
				price: 1,
				minimumCompletedQuests: 1);
			ActionDefinition action = CreateSingleCardAction(
				"test.vendor.unlock.action",
				worker.ContentId.Value,
				turnCost: 0);
			QuestDefinition quest = CreateActionCompletionQuest(
				"test.vendor.unlock.quest",
				action.ContentId.Value);
			ScenarioRegionDefinition region = CreateRegion("test.vendor.unlock.region");
			ScenarioDefinition scenario = CreateScenarioDefinition(
				"test.vendor.unlock.scenario",
				region.ContentId.Value,
				quest.ContentId.Value);
			List<TabletopPresentationCue> cues = new List<TabletopPresentationCue>();
			ScenarioRun run = null;
			try
			{
				ContentIndex content = ContentIndex.Build(
					new ContentAsset[] { worker, reward, pack, vendor, action, quest, region, scenario });
				run = new ScenarioRun(scenario, content, 12345u);
				run.ActivateInitialQuests();
				run.DiscoverContent(action.ContentId);
				TabletopCard workerCard = run.Tabletop.CreateCard(worker.ContentId, new Vector2(-1f, 0f));
				PackVendorCard vendorCard = (PackVendorCard)run.Tabletop.CreateCard(
					vendor.ContentId,
					new Vector2(2f, 0f));
				run.Tabletop.PresentationCueRequested += OnPresentationCueRequested;

				ActionCandidate candidate = run.FindActionCandidates(new TabletopCardPointerReleaseIntent(
					workerCard.Id,
					workerCard.Position,
					workerCard.Position + Vector2.right,
					workerCard.Position,
					isDrag: true,
					default)).Single();
				run.StartAction(ActionRequest.FromCandidate(candidate));

				Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(cues, Has.Some.Matches<TabletopPresentationCue>(cue =>
					cue.Kind == TabletopPresentationCueKind.CameraFocus &&
					cue.HasTablePosition &&
					cue.TablePosition == vendorCard.Position));
				Assert.That(cues, Has.Some.Matches<TabletopPresentationCue>(cue =>
					cue.Kind == TabletopPresentationCueKind.CardHighlight &&
					cue.HasCardId &&
					cue.CardId == vendorCard.Id));
			}
			finally
			{
				if (run != null)
				{
					run.Tabletop.PresentationCueRequested -= OnPresentationCueRequested;
					run.End();
				}
				Object.DestroyImmediate(worker);
				Object.DestroyImmediate(reward);
				Object.DestroyImmediate(pack);
				Object.DestroyImmediate(vendor);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(region);
				Object.DestroyImmediate(scenario);
			}

			void OnPresentationCueRequested(TabletopPresentationCue cue)
			{
				cues.Add(cue);
			}
		}

		[Test]
		public void ScenarioRun_PartialPaymentThenPurchaseConsumesCurrencyAndCreatesPack()
		{
			using VendorScenarioContext context = CreateScenario(minimumCompletedQuests: 0);
			TabletopCard vendorCard = context.Run.Tabletop.CreateCard(context.Vendor.ContentId, new Vector2(1f, 0f));
			PackVendorCard vendor = (PackVendorCard)vendorCard;

			TabletopCard firstCoin = context.Run.Tabletop.CreateCard(context.Coin.ContentId, new Vector2(-1f, 0f));
			ActionCandidate firstCandidate = context.FindCandidates(firstCoin, vendor).Single();
			context.Run.StartAction(ActionRequest.FromCandidate(firstCandidate));

			Assert.That(context.Run.Tabletop.Cards.TryGetCard(firstCoin.Id, out _), Is.False);
			Assert.That(vendor.PaidAmount, Is.EqualTo(1));
			Assert.That(context.CountCards(context.Pack.ContentId), Is.Zero);

			TabletopCard secondCoin = context.Run.Tabletop.CreateCard(context.Coin.ContentId, new Vector2(-1f, 0f));
			ActionCandidate secondCandidate = context.FindCandidates(secondCoin, vendor).Single();
			context.Run.StartAction(ActionRequest.FromCandidate(secondCandidate));

			Assert.That(context.Run.Tabletop.Cards.TryGetCard(secondCoin.Id, out _), Is.False);
			Assert.That(vendor.PaidAmount, Is.Zero);
			Assert.That(context.CountCards(context.Pack.ContentId), Is.EqualTo(1));
		}

		[Test]
		public void ScenarioRun_PartialVendorPaymentRestoresFromFullRunSnapshot()
		{
			using VendorScenarioContext context = CreateScenario(minimumCompletedQuests: 0);
			TabletopCard vendor = context.Run.Tabletop.CreateCard(context.Vendor.ContentId, new Vector2(1f, 0f));
			TabletopCard coin = context.Run.Tabletop.CreateCard(context.Coin.ContentId, new Vector2(-1f, 0f));
			context.Run.StartAction(ActionRequest.FromCandidate(context.FindCandidates(coin, vendor).Single()));

			ScenarioRunSnapshot snapshot = JsonUtility.FromJson<ScenarioRunSnapshot>(
				JsonUtility.ToJson(context.Run.CreateSnapshot()));
			ScenarioRun restored = ScenarioRun.Restore(context.Scenario, context.Content, snapshot);
			try
			{
				Assert.That(restored.Tabletop.Cards.TryGetCard(vendor.Id, out TabletopCard restoredCard), Is.True);
				Assert.That(restoredCard, Is.TypeOf<PackVendorCard>());
				Assert.That(((PackVendorCard)restoredCard).PaidAmount, Is.EqualTo(1));
			}
			finally
			{
				restored.End();
			}
		}

		[Test]
		public void PackCollectionProgress_CountsUniqueCardsAndRecipes()
		{
			CardPackDefinition pack = ScriptableObject.CreateInstance<CardPackDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.pack\"},\"m_slots\":[" +
				"{\"m_entries\":[{\"m_cardId\":{\"m_value\":\"test.card.one\"},\"m_weight\":1}]," +
				"\"m_recipeEntries\":[{\"m_actionId\":{\"m_value\":\"test.recipe\"},\"m_recipeCardId\":{\"m_value\":\"test.recipe.card\"}}]}," +
				"{\"m_entries\":[{\"m_cardId\":{\"m_value\":\"test.card.one\"},\"m_weight\":1},{\"m_cardId\":{\"m_value\":\"test.card.two\"},\"m_weight\":1}]," +
				"\"m_recipeEntries\":[{\"m_actionId\":{\"m_value\":\"test.recipe\"},\"m_recipeCardId\":{\"m_value\":\"test.recipe.card\"}}]}]}",
				pack);
			try
			{
				CardPackCollectionProgress progress = pack.GetCollectionProgress(
					contentId => contentId.Value == "test.card.one" || contentId.Value == "test.recipe");

				Assert.That(progress.DiscoveredCount, Is.EqualTo(2));
				Assert.That(progress.TotalCount, Is.EqualTo(3));
				Assert.That(progress.IsComplete, Is.False);
			}
			finally
			{
				Object.DestroyImmediate(pack);
			}
		}

		[Test]
		public void PackPurchaseQuestTask_OnlyCountsMatchingPurchasedPack()
		{
			CardPackPurchaseQuestTaskDefinition definition = new CardPackPurchaseQuestTaskDefinition();
			JsonUtility.FromJsonOverwrite(
				"{\"m_packId\":{\"m_value\":\"test.pack\"},\"m_requiredPurchaseCount\":2}",
				definition);
			QuestTaskRuntimeState state = definition.CreateRuntimeStateForQuestLog();

			Assert.That(
				state.RecordFactFromQuestLog(new CardPackPurchasedQuestTaskFact(new ContentId("other.pack"))),
				Is.False);
			Assert.That(
				state.RecordFactFromQuestLog(new CardPackPurchasedQuestTaskFact(new ContentId("test.pack"))),
				Is.True);
			Assert.That(state.Progress.CurrentAmount, Is.EqualTo(1));
			Assert.That(state.IsCompleted, Is.False);
			Assert.That(
				state.RecordFactFromQuestLog(new CardPackPurchasedQuestTaskFact(new ContentId("test.pack"))),
				Is.True);
			Assert.That(state.IsCompleted, Is.True);
		}

		[Test]
		public void PackPurchaseQuestTask_EmptyTargetCountsAnyPurchasedPack()
		{
			CardPackPurchaseQuestTaskDefinition definition = new CardPackPurchaseQuestTaskDefinition();
			JsonUtility.FromJsonOverwrite(
				"{\"m_packId\":{\"m_value\":\"\"},\"m_requiredPurchaseCount\":2}",
				definition);
			QuestTaskRuntimeState state = definition.CreateRuntimeStateForQuestLog();

			Assert.That(
				state.RecordFactFromQuestLog(new CardPackPurchasedQuestTaskFact(new ContentId("first.pack"))),
				Is.True);
			Assert.That(state.Progress.CurrentAmount, Is.EqualTo(1));
			Assert.That(state.IsCompleted, Is.False);
			Assert.That(
				state.RecordFactFromQuestLog(new CardPackPurchasedQuestTaskFact(new ContentId("second.pack"))),
				Is.True);
			Assert.That(state.IsCompleted, Is.True);
		}

		[Test]
		public void PackPurchaseQuestTask_EmptyTargetPassesAuthorValidation()
		{
			QuestDefinition quest = CreatePackPurchaseQuest(
				"test.quest.buy-any-pack",
				string.Empty,
				1);
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(new ContentAsset[] { quest });

				Assert.That(
					report.HasErrors,
					Is.False,
					string.Join(", ", report.Issues.Select(issue => issue.Code + ": " + issue.Message)));
			}
			finally
			{
				Object.DestroyImmediate(quest);
			}
		}

		private static PackVendorDefinition CreateVendorDefinition(
			string contentId,
			string offeredPackId,
			int price,
			int minimumCompletedQuests)
		{
			PackVendorDefinition definition = ScriptableObject.CreateInstance<PackVendorDefinition>();
			SerializedObject serialized = new SerializedObject(definition);
			serialized.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serialized.FindProperty("m_offeredPackId").FindPropertyRelative("m_value").stringValue = offeredPackId;
			serialized.FindProperty("m_price").intValue = price;
			serialized.FindProperty("m_minimumCompletedQuests").intValue = minimumCompletedQuests;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreatePackPurchaseQuest(
			string contentId,
			string packId,
			int requiredPurchaseCount)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serialized = new SerializedObject(definition);
			SerializedProperty tasks = serialized.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue = new CardPackPurchaseQuestTaskDefinition();
			SerializedProperty task = tasks.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_packId").FindPropertyRelative("m_value").stringValue = packId;
			task.FindPropertyRelative("m_requiredPurchaseCount").intValue = requiredPurchaseCount;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static CardPackDefinition CreatePack(string contentId, string rewardCardId)
		{
			CardPackDefinition pack = ScriptableObject.CreateInstance<CardPackDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
				"\"},\"m_slots\":[{\"m_entries\":[{\"m_cardId\":{\"m_value\":\"" +
				rewardCardId + "\"},\"m_weight\":1}]}]}",
				pack);
			return pack;
		}

		private static ActionDefinition CreateSingleCardAction(
			string contentId,
			string cardContentId,
			int turnCost)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
				"\"},\"m_turnCost\":" + turnCost +
				",\"m_participationSlots\":[{\"m_key\":\"slot-1\",\"m_minimumParticipants\":1," +
				"\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"" +
				cardContentId + "\"}]}]}",
				definition);
			return definition;
		}

		private static QuestDefinition CreateActionCompletionQuest(
			string contentId,
			string actionId)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serialized = new SerializedObject(definition);
			SerializedProperty tasks = serialized.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new ActionCompletionQuestTaskDefinition();
			serialized.ApplyModifiedPropertiesWithoutUndo();
			serialized.Update();
			SerializedProperty task = serialized.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_actionId").FindPropertyRelative("m_value").stringValue = actionId;
			task.FindPropertyRelative("m_requiredCompletionCount").intValue = 1;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static ScenarioRegionDefinition CreateRegion(string contentId)
		{
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				region);
			return region;
		}

		private static ScenarioDefinition CreateScenarioDefinition(
			string contentId,
			string regionId,
			string questId)
		{
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
				"\"},\"m_initialRegionId\":{\"m_value\":\"" + regionId +
				"\"},\"m_regionIds\":[{\"m_value\":\"" + regionId +
				"\"}],\"m_questIds\":[{\"m_value\":\"" + questId + "\"}]}",
				scenario);
			return scenario;
		}

		private static VendorScenarioContext CreateScenario(int minimumCompletedQuests)
		{
			CardDefinition coin = CreateCard("test.vendor.coin");
			CardDefinition reward = CreateCard("test.vendor.reward");
			CardPackDefinition pack = ScriptableObject.CreateInstance<CardPackDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.vendor.pack\"},\"m_slots\":[{\"m_entries\":[{\"m_cardId\":{\"m_value\":\"test.vendor.reward\"},\"m_weight\":1}]}]}",
				pack);
			PackVendorDefinition vendor = CreateVendorDefinition(
				"test.vendor.offer",
				pack.ContentId.Value,
				price: 2,
				minimumCompletedQuests);
			ActionDefinition purchaseAction = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.vendor.purchase\"},\"m_turnCost\":0," +
				"\"m_participationSlots\":[" +
				"{\"m_key\":\"payment\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":0,\"m_allowedContentIds\":[{\"m_value\":\"test.vendor.coin\"}]}," +
				"{\"m_key\":\"vendor\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"test.vendor.offer\"}]}]}",
				purchaseAction);
			SerializedObject serializedAction = new SerializedObject(purchaseAction);
			SerializedProperty conditions = serializedAction.FindProperty("m_conditions");
			conditions.arraySize = 1;
			PackVendorUnlockedCondition condition = new PackVendorUnlockedCondition();
			JsonUtility.FromJsonOverwrite("{\"m_vendorSlotKey\":\"vendor\"}", condition);
			conditions.GetArrayElementAtIndex(0).managedReferenceValue = condition;
			SerializedProperty results = serializedAction.FindProperty("m_resultIntents");
			results.arraySize = 1;
			PurchaseCardPackResultIntent purchase = new PurchaseCardPackResultIntent();
			JsonUtility.FromJsonOverwrite(
				"{\"m_vendorSlotKey\":\"vendor\",\"m_paymentSlotKey\":\"payment\"}",
				purchase);
			results.GetArrayElementAtIndex(0).managedReferenceValue = purchase;
			serializedAction.ApplyModifiedPropertiesWithoutUndo();

			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.vendor.region\"}}",
				region);
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.vendor.scenario\"}," +
				"\"m_initialRegionId\":{\"m_value\":\"test.vendor.region\"}," +
				"\"m_regionIds\":[{\"m_value\":\"test.vendor.region\"}]}",
				scenario);
			ContentAsset[] assets = { coin, reward, pack, vendor, purchaseAction, region, scenario };
			ContentIndex content = ContentIndex.Build(assets);
			ScenarioRun run = new ScenarioRun(scenario, content, 12345u);
			run.DiscoverContent(purchaseAction.ContentId);
			return new VendorScenarioContext(run, coin, reward, pack, vendor, purchaseAction, region, scenario);
		}

		private static CardDefinition CreateCard(string contentId)
		{
			CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}", card);
			return card;
		}

		private sealed class VendorScenarioContext : IDisposable
		{
			private readonly UnityEngine.Object[] m_assets;

			internal ScenarioRun Run { get; }
			internal CardDefinition Coin { get; }
			internal CardPackDefinition Pack { get; }
			internal PackVendorDefinition Vendor { get; }
			internal ScenarioDefinition Scenario { get; }
			internal ContentIndex Content { get; }

			internal VendorScenarioContext(
				ScenarioRun run,
				CardDefinition coin,
				CardDefinition reward,
				CardPackDefinition pack,
				PackVendorDefinition vendor,
				ActionDefinition action,
				ScenarioRegionDefinition region,
				ScenarioDefinition scenario)
			{
				Run = run;
				Coin = coin;
				Pack = pack;
				Vendor = vendor;
				Scenario = scenario;
				Content = run.ContentIndex;
				m_assets = new UnityEngine.Object[] { coin, reward, pack, vendor, action, region, scenario };
			}

			internal ActionCandidate[] FindCandidates(TabletopCard source, TabletopCard target)
			{
				return Run.FindActionCandidates(new TabletopCardPointerReleaseIntent(
					source.Id,
					source.Position,
					target.Position,
					source.Position,
					isDrag: true,
					target.Id));
			}

			internal int CountCards(ContentId contentId)
			{
				return Run.Tabletop.Cards.Stacks.SelectMany(stack => stack.Cards)
					.Count(card => card.ContentId == contentId);
			}

			public void Dispose()
			{
				Run.End();
				for (int i = 0; i < m_assets.Length; i++)
				{
					Object.DestroyImmediate(m_assets[i]);
				}
			}
		}
	}
}
