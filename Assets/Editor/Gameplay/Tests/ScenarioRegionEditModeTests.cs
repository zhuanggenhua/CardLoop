using System;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Tests
{
	/// <summary>验证剧本地区长期拥有牌桌，以及旅行保留卡牌运行身份。</summary>
	public sealed class ScenarioRegionEditModeTests
	{
		[Test]
		public void Travel_PreservesRegionTabletopsAndMovesTheSameCardInstance()
		{
			CardDefinition card = CreateCard("test.region.card");
			ScenarioRegionDefinition forest = CreateRegion("test.region.forest");
			ScenarioRegionDefinition beach = CreateRegion("test.region.beach");
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario.regions",
				forest.ContentId.Value,
				forest.ContentId.Value,
				beach.ContentId.Value);
			ScenarioRun run = null;
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[]
				{
					card,
					forest,
					beach,
					scenario
				});
				run = new ScenarioRun(scenario, contentIndex, 12345u);
				ScenarioRegion sourceRegion = run.ActiveRegion;
				TabletopCard traveler = run.Tabletop.CreateCard(card.ContentId, Vector2.zero);
				ScenarioRegion targetRegion = run.GetRegion(beach.ContentId);
				TabletopCard resident = targetRegion.Tabletop.CreateCard(card.ContentId, Vector2.one);

				Assert.That(resident.Id, Is.Not.EqualTo(traveler.Id),
					"不同地区必须共享剧本级卡牌实例号序列。");
				ScenarioTravelPlan travel = run.BeginTravel(
					beach.ContentId,
					new[] { traveler.Id });
				travel.Commit();

				Assert.That(run.ActiveRegion, Is.SameAs(targetRegion));
				Assert.That(sourceRegion.Tabletop.Cards.TryGetCard(traveler.Id, out _), Is.False);
				Assert.That(targetRegion.Tabletop.Cards.TryGetCard(traveler.Id, out TabletopCard moved), Is.True);
				Assert.That(moved, Is.SameAs(traveler),
					"旅行必须迁移原卡牌实例，不能按内容 ID 重建副本。");
				Assert.That(sourceRegion.Tabletop.Cards.CardCount, Is.Zero);
				Assert.That(targetRegion.Tabletop.Cards.CardCount, Is.EqualTo(2));
			}
			finally
			{
				run?.End();
				UnityEngine.Object.DestroyImmediate(card);
				UnityEngine.Object.DestroyImmediate(forest);
				UnityEngine.Object.DestroyImmediate(beach);
				UnityEngine.Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void CreateRun_CreatesInitialRegionCardsOnTabletop()
		{
			CardDefinition scout = CreateCard("test.region.initial.scout");
			CardDefinition food = CreateCard("test.region.initial.food");
			ScenarioRegionDefinition camp = CreateRegionWithInitialCards(
				"test.region.initial.camp",
				"{\"m_cardId\":{\"m_value\":\"test.region.initial.scout\"}," +
				"\"m_count\":1,\"m_position\":{\"x\":-1.5,\"y\":0.5},\"m_isPlacementLocked\":false}",
				"{\"m_cardId\":{\"m_value\":\"test.region.initial.food\"}," +
				"\"m_count\":2,\"m_position\":{\"x\":1.5,\"y\":0.5},\"m_isPlacementLocked\":true}");
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario.initial-region-cards",
				camp.ContentId.Value,
				camp.ContentId.Value);
			ScenarioRun run = null;
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[]
				{
					scout,
					food,
					camp,
					scenario
				});
				run = new ScenarioRun(scenario, contentIndex, 12345u);

				Assert.That(run.Tabletop.Cards.CardCount, Is.EqualTo(3));
				Assert.That(CountCards(run.Tabletop, scout.ContentId), Is.EqualTo(1));
				Assert.That(CountCards(run.Tabletop, food.ContentId), Is.EqualTo(2));
				Assert.That(run.Tabletop.Cards.Stacks[0].Position, Is.EqualTo(new Vector2(-1.5f, 0.5f)));
				Assert.That(run.Tabletop.Cards.Stacks[1].IsPlacementLocked, Is.True);
			}
			finally
			{
				run?.End();
				UnityEngine.Object.DestroyImmediate(scout);
				UnityEngine.Object.DestroyImmediate(food);
				UnityEngine.Object.DestroyImmediate(camp);
				UnityEngine.Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void Restore_DoesNotCreateInitialRegionCardsAgain()
		{
			CardDefinition card = CreateCard("test.region.initial.restore-card");
			ScenarioRegionDefinition camp = CreateRegionWithInitialCards(
				"test.region.initial.restore-camp",
				"{\"m_cardId\":{\"m_value\":\"test.region.initial.restore-card\"}," +
				"\"m_count\":2,\"m_position\":{\"x\":0,\"y\":0},\"m_isPlacementLocked\":false}");
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario.initial-region-restore",
				camp.ContentId.Value,
				camp.ContentId.Value);
			ScenarioRun run = null;
			ScenarioRun restored = null;
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[]
				{
					card,
					camp,
					scenario
				});
				run = new ScenarioRun(scenario, contentIndex, 12345u);
				ScenarioRunSnapshot snapshot = run.CreateSnapshot();
				restored = ScenarioRun.Restore(scenario, contentIndex, snapshot);

				Assert.That(run.Tabletop.Cards.CardCount, Is.EqualTo(2));
				Assert.That(restored.Tabletop.Cards.CardCount, Is.EqualTo(2));
				Assert.That(CountCards(restored.Tabletop, card.ContentId), Is.EqualTo(2));
			}
			finally
			{
				restored?.End();
				run?.End();
				UnityEngine.Object.DestroyImmediate(card);
				UnityEngine.Object.DestroyImmediate(camp);
				UnityEngine.Object.DestroyImmediate(scenario);
			}
		}

		private static CardDefinition CreateCard(string contentId)
		{
			CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				$"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}}}}",
				definition);
			return definition;
		}

		private static ScenarioRegionDefinition CreateRegion(string contentId)
		{
			ScenarioRegionDefinition definition =
				ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				$"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}}}}",
				definition);
			return definition;
		}

		private static ScenarioRegionDefinition CreateRegionWithInitialCards(
			string contentId,
			params string[] initialCardJson)
		{
			ScenarioRegionDefinition definition =
				ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				$"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}}," +
				$"\"m_initialCards\":[{string.Join(",", initialCardJson)}]}}",
				definition);
			return definition;
		}

		private static int CountCards(Gameplay.Tabletop.Tabletop tabletop, ContentId contentId)
		{
			int count = 0;
			for (int stackIndex = 0; stackIndex < tabletop.Cards.Stacks.Count; stackIndex++)
			{
				var cards = tabletop.Cards.Stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					if (cards[cardIndex].ContentId.Equals(contentId))
					{
						count++;
					}
				}
			}
			return count;
		}

		private static ScenarioDefinition CreateScenario(
			string contentId,
			string initialRegionId,
			params string[] regionIds)
		{
			ScenarioDefinition definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
			string regionJson = string.Join(",", Array.ConvertAll(
				regionIds,
				regionId => $"{{\"m_value\":\"{regionId}\"}}"));
			JsonUtility.FromJsonOverwrite(
				$"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}}," +
				$"\"m_initialRegionId\":{{\"m_value\":\"{initialRegionId}\"}}," +
				$"\"m_regionIds\":[{regionJson}]}}",
				definition);
			return definition;
		}
	}
}
