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
