using System;
using System.Linq;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	public sealed class ChestCardEditModeTests
	{
		[Test]
		public void ChestCard_StoresCurrencyAndRoundTripsThroughCardSnapshot()
		{
			ChestCard chest = new ChestCard(new TabletopCardId(7), new ContentId("test.chest"), capacity: 3);
			chest.DepositCurrency(2);
			chest.WithdrawCurrency(1);

			TabletopCardSnapshot snapshot = JsonUtility.FromJson<TabletopCardSnapshot>(
				JsonUtility.ToJson(chest.CreateSnapshot()));
			ChestCard restored = new ChestCard(snapshot.CardId, snapshot.ContentId, capacity: 3, snapshot.RuntimeState);

			Assert.That(restored.StoredCurrencyCount, Is.EqualTo(1));
			Assert.That(restored.RemainingCapacity, Is.EqualTo(2));
		}

		[Test]
		public void ScenarioRun_DepositsCurrencyIntoChestUntilCapacityAndWithdrawsOneCurrency()
		{
			using ChestScenarioContext context = CreateScenario(includeVendor: false);
			TabletopCardStack coins = context.Run.Tabletop.CreateCardStack(context.Coin.ContentId, 3, new Vector2(-1f, 0f));
			ChestCard chest = (ChestCard)context.Run.Tabletop.CreateCard(context.Chest.ContentId, new Vector2(1f, 0f));

			ActionCandidate depositCandidate = context.FindCandidates(coins.Cards[0], chest).Single(candidate => candidate.Action == context.DepositAction);
			context.Run.StartAction(ActionRequest.FromCandidate(depositCandidate));

			Assert.That(chest.StoredCurrencyCount, Is.EqualTo(2));
			Assert.That(context.CountCards(context.Coin.ContentId), Is.EqualTo(1));

			ActionCandidate withdrawCandidate = context.FindClickCandidates(chest).Single(candidate => candidate.Action == context.WithdrawAction);
			context.Run.StartAction(ActionRequest.FromCandidate(withdrawCandidate));

			Assert.That(chest.StoredCurrencyCount, Is.EqualTo(1));
			Assert.That(context.CountCards(context.Coin.ContentId), Is.EqualTo(2));
		}

		[Test]
		public void ScenarioRun_DepositsOnlyCurrencyFromMixedStackAndLeavesOtherCards()
		{
			using ChestScenarioContext context = CreateScenario(includeVendor: false);
			TabletopCard firstCoin = context.Run.Tabletop.CreateCard(context.Coin.ContentId, new Vector2(-1f, 0f));
			TabletopCard filler = context.Run.Tabletop.CreateCard(context.Buyer.ContentId, new Vector2(-0.7f, 0f));
			TabletopCard secondCoin = context.Run.Tabletop.CreateCard(context.Coin.ContentId, new Vector2(-0.4f, 0f));
			context.Run.Tabletop.MergeStackOnto(filler.Id, firstCoin.Id);
			TabletopCardStack mixedStack = context.Run.Tabletop.MergeStackOnto(secondCoin.Id, firstCoin.Id);
			ChestCard chest = (ChestCard)context.Run.Tabletop.CreateCard(context.Chest.ContentId, new Vector2(1f, 0f));

			ActionCandidate depositCandidate = context.FindCandidates(mixedStack.TopCard, chest).Single(candidate => candidate.Action == context.DepositAction);
			context.Run.StartAction(ActionRequest.FromCandidate(depositCandidate));

			Assert.That(chest.StoredCurrencyCount, Is.EqualTo(2));
			Assert.That(context.CountCards(context.Coin.ContentId), Is.Zero);
			Assert.That(context.CountCards(context.Buyer.ContentId), Is.EqualTo(1));
			Assert.That(context.Run.Tabletop.Cards.TryGetStackContaining(filler.Id, out TabletopCardStack remainingStack), Is.True);
			Assert.That(remainingStack.Cards.Select(card => card.Id), Is.EqualTo(new[] { filler.Id }));
		}

		[Test]
		public void ScenarioRun_ChestPaysPackVendorWithoutRemovingChest()
		{
			using ChestScenarioContext context = CreateScenario(includeVendor: true);
			ChestCard chest = (ChestCard)context.Run.Tabletop.CreateCard(context.Chest.ContentId, new Vector2(-1f, 0f));
			chest.DepositCurrency(2);
			PackVendorCard vendor = (PackVendorCard)context.Run.Tabletop.CreateCard(context.Vendor.ContentId, new Vector2(1f, 0f));

			ActionCandidate candidate = context.FindCandidates(chest, vendor).Single(candidate => candidate.Action == context.PurchaseAction);
			context.Run.StartAction(ActionRequest.FromCandidate(candidate));

			Assert.That(context.Run.Tabletop.Cards.TryGetCard(chest.Id, out TabletopCard stillPresent), Is.True);
			Assert.That(stillPresent, Is.SameAs(chest));
			Assert.That(chest.StoredCurrencyCount, Is.Zero);
			Assert.That(vendor.PaidAmount, Is.Zero);
			Assert.That(context.CountCards(context.Pack.ContentId), Is.EqualTo(1));
		}

		[Test]
		public void ScenarioRun_NonEmptyChestCannotBeSold()
		{
			using ChestScenarioContext context = CreateScenario(includeVendor: false);
			ChestCard chest = (ChestCard)context.Run.Tabletop.CreateCard(context.Chest.ContentId, new Vector2(-1f, 0f));
			chest.DepositCurrency(1);
			TabletopCard buyer = context.Run.Tabletop.CreateCard(context.Buyer.ContentId, new Vector2(1f, 0f));
			ActionCandidate candidate = context.FindCandidates(chest, buyer).Single(candidate => candidate.Action == context.SellAction);

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
				context.Run.StartAction(ActionRequest.FromCandidate(candidate)));

			StringAssert.Contains("仍存有货币", exception.Message);
			Assert.That(context.Run.Tabletop.Cards.TryGetCard(chest.Id, out _), Is.True);
			Assert.That(context.CountCards(context.Coin.ContentId), Is.Zero);
		}

		private static ChestScenarioContext CreateScenario(bool includeVendor)
		{
			CardDefinition coin = CreateCard("test.chest.coin");
			JsonUtility.FromJsonOverwrite("{\"m_countsTowardCardLimit\":false}", coin);
			CardBuyerDefinition buyer = CreateBuyer("test.chest.buyer", coin.ContentId);
			ChestCardDefinition chest = CreateChest("test.chest.card", coin.ContentId, capacity: 2, sellValue: 3);
			ActionDefinition depositAction = CreateDepositAction(chest.ContentId, coin.ContentId);
			ActionDefinition withdrawAction = CreateWithdrawAction(chest.ContentId);
			ActionDefinition sellAction = CreateSellAction(chest.ContentId, buyer.ContentId, coin.ContentId);
			CardDefinition reward = CreateCard("test.chest.reward");
			CardPackDefinition pack = ScriptableObject.CreateInstance<CardPackDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.chest.pack\"},\"m_countsTowardCardLimit\":false,\"m_slots\":[{\"m_entries\":[{\"m_cardId\":{\"m_value\":\"test.chest.reward\"},\"m_weight\":1}]}]}",
				pack);
			PackVendorDefinition vendor = CreateVendor("test.chest.vendor", pack.ContentId, price: 2);
			ActionDefinition purchaseAction = CreatePurchaseAction(chest.ContentId, coin.ContentId, vendor.ContentId);
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.chest.region\"}}",
				region);
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.chest.scenario\"}," +
				"\"m_initialRegionId\":{\"m_value\":\"test.chest.region\"}," +
				"\"m_regionIds\":[{\"m_value\":\"test.chest.region\"}]}",
				scenario);

			ContentAsset[] contentAssets = includeVendor
				? new ContentAsset[] { coin, buyer, chest, depositAction, withdrawAction, sellAction, reward, pack, vendor, purchaseAction, region, scenario }
				: new ContentAsset[] { coin, buyer, chest, depositAction, withdrawAction, sellAction, region, scenario };
			Object[] disposableAssets = new Object[] { coin, buyer, chest, depositAction, withdrawAction, sellAction, reward, pack, vendor, purchaseAction, region, scenario };
			ContentIndex content = ContentIndex.Build(contentAssets);
			ScenarioRun run = new ScenarioRun(scenario, content, 12345u);
			run.DiscoverContent(depositAction.ContentId);
			run.DiscoverContent(withdrawAction.ContentId);
			run.DiscoverContent(sellAction.ContentId);
			if (includeVendor)
			{
				run.DiscoverContent(purchaseAction.ContentId);
			}
			return new ChestScenarioContext(run, disposableAssets, coin, buyer, chest, depositAction, withdrawAction, sellAction, pack, vendor, purchaseAction);
		}

		private static CardDefinition CreateCard(string contentId)
		{
			CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}", card);
			return card;
		}

		private static CardBuyerDefinition CreateBuyer(string contentId, ContentId currencyCardId)
		{
			CardBuyerDefinition buyer = ScriptableObject.CreateInstance<CardBuyerDefinition>();
			SerializedObject serialized = new SerializedObject(buyer);
			serialized.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serialized.FindProperty("m_currencyCardId").FindPropertyRelative("m_value").stringValue = currencyCardId.Value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return buyer;
		}

		private static ChestCardDefinition CreateChest(string contentId, ContentId currencyCardId, int capacity, int sellValue)
		{
			ChestCardDefinition chest = ScriptableObject.CreateInstance<ChestCardDefinition>();
			SerializedObject serialized = new SerializedObject(chest);
			serialized.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serialized.FindProperty("m_capacity").intValue = capacity;
			serialized.FindProperty("m_currencyCardId").FindPropertyRelative("m_value").stringValue = currencyCardId.Value;
			serialized.FindProperty("m_sellValue").intValue = sellValue;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return chest;
		}

		private static PackVendorDefinition CreateVendor(string contentId, ContentId packId, int price)
		{
			PackVendorDefinition vendor = ScriptableObject.CreateInstance<PackVendorDefinition>();
			SerializedObject serialized = new SerializedObject(vendor);
			serialized.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serialized.FindProperty("m_offeredPackId").FindPropertyRelative("m_value").stringValue = packId.Value;
			serialized.FindProperty("m_price").intValue = price;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return vendor;
		}

		private static ActionDefinition CreateDepositAction(ContentId chestId, ContentId coinId)
		{
			ActionDefinition action = CreateActionCore(
				"test.chest.deposit",
				"{\"m_key\":\"currency\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":0,\"m_allowedContentIds\":[{\"m_value\":\"" + coinId.Value + "\"}]}," +
				"{\"m_key\":\"chest\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"" + chestId.Value + "\"}]}");
			SerializedObject serialized = new SerializedObject(action);
			SetCondition(serialized, 0, new ChestHasCapacityCondition(), "{\"m_chestSlotKey\":\"chest\"}");
			SetResult(serialized, 0, new DepositCurrencyIntoChestResultIntent(), "{\"m_chestSlotKey\":\"chest\",\"m_currencySlotKey\":\"currency\"}");
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return action;
		}

		private static ActionDefinition CreateWithdrawAction(ContentId chestId)
		{
			ActionDefinition action = CreateActionCore(
				"test.chest.withdraw",
				"{\"m_key\":\"chest\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"" + chestId.Value + "\"}]}",
				canStartFromClick: true);
			SerializedObject serialized = new SerializedObject(action);
			SetCondition(serialized, 0, new ChestHasStoredCurrencyCondition(), "{\"m_chestSlotKey\":\"chest\"}");
			SetResult(serialized, 0, new WithdrawCurrencyFromChestResultIntent(), "{\"m_chestSlotKey\":\"chest\"}");
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return action;
		}

		private static ActionDefinition CreateSellAction(ContentId chestId, ContentId buyerId, ContentId coinId)
		{
			ActionDefinition action = CreateActionCore(
				"test.chest.sell",
				"{\"m_key\":\"sold\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"" + chestId.Value + "\"}]}," +
				"{\"m_key\":\"buyer\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"" + buyerId.Value + "\"}]}");
			SerializedObject serialized = new SerializedObject(action);
			SetResult(serialized, 0, new SellCardsResultIntent(),
				"{\"m_soldSlotKey\":\"sold\",\"m_currencyCardId\":{\"m_value\":\"" + coinId.Value + "\"},\"m_anchorSlotKey\":\"buyer\"}");
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return action;
		}

		private static ActionDefinition CreatePurchaseAction(ContentId chestId, ContentId coinId, ContentId vendorId)
		{
			ActionDefinition action = CreateActionCore(
				"test.chest.purchase",
				"{\"m_key\":\"payment\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":0,\"m_allowedContentIds\":[{\"m_value\":\"" + coinId.Value + "\"},{\"m_value\":\"" + chestId.Value + "\"}]}," +
				"{\"m_key\":\"vendor\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"" + vendorId.Value + "\"}]}");
			SerializedObject serialized = new SerializedObject(action);
			SetCondition(serialized, 0, new PackVendorUnlockedCondition(), "{\"m_vendorSlotKey\":\"vendor\"}");
			SetCondition(serialized, 1, new CardPaymentSourceAvailableCondition(), "{\"m_paymentSlotKey\":\"payment\"}");
			SetResult(serialized, 0, new PurchaseCardPackResultIntent(), "{\"m_vendorSlotKey\":\"vendor\",\"m_paymentSlotKey\":\"payment\"}");
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return action;
		}

		private static ActionDefinition CreateActionCore(string contentId, string slotsJson, bool canStartFromClick = false)
		{
			ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"},\"m_turnCost\":0," +
				"\"m_canStartFromClick\":" + (canStartFromClick ? "true" : "false") + "," +
				"\"m_participationSlots\":[" + slotsJson + "]}",
				action);
			return action;
		}

		private static void SetCondition(SerializedObject serializedAction, int index, ActionCondition condition, string json)
		{
			JsonUtility.FromJsonOverwrite(json, condition);
			SerializedProperty conditions = serializedAction.FindProperty("m_conditions");
			conditions.arraySize = Math.Max(conditions.arraySize, index + 1);
			conditions.GetArrayElementAtIndex(index).managedReferenceValue = condition;
		}

		private static void SetResult(SerializedObject serializedAction, int index, ActionResultIntent intent, string json)
		{
			JsonUtility.FromJsonOverwrite(json, intent);
			SerializedProperty results = serializedAction.FindProperty("m_resultIntents");
			results.arraySize = Math.Max(results.arraySize, index + 1);
			results.GetArrayElementAtIndex(index).managedReferenceValue = intent;
		}

		private sealed class ChestScenarioContext : IDisposable
		{
			private readonly UnityEngine.Object[] m_assets;

			internal ScenarioRun Run { get; }
			internal CardDefinition Coin { get; }
			internal CardDefinition Buyer { get; }
			internal ChestCardDefinition Chest { get; }
			internal ActionDefinition DepositAction { get; }
			internal ActionDefinition WithdrawAction { get; }
			internal ActionDefinition SellAction { get; }
			internal CardPackDefinition Pack { get; }
			internal PackVendorDefinition Vendor { get; }
			internal ActionDefinition PurchaseAction { get; }

			internal ChestScenarioContext(
				ScenarioRun run,
				UnityEngine.Object[] assets,
				CardDefinition coin,
				CardBuyerDefinition buyer,
				ChestCardDefinition chest,
				ActionDefinition depositAction,
				ActionDefinition withdrawAction,
				ActionDefinition sellAction,
				CardPackDefinition pack,
				PackVendorDefinition vendor,
				ActionDefinition purchaseAction)
			{
				Run = run;
				m_assets = assets;
				Coin = coin;
				Buyer = buyer;
				Chest = chest;
				DepositAction = depositAction;
				WithdrawAction = withdrawAction;
				SellAction = sellAction;
				Pack = pack;
				Vendor = vendor;
				PurchaseAction = purchaseAction;
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

			internal ActionCandidate[] FindClickCandidates(TabletopCard card)
			{
				return Run.FindActionCandidates(new TabletopCardPointerReleaseIntent(
					card.Id,
					card.Position,
					card.Position,
					card.Position,
					isDrag: false,
					default));
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
