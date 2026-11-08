using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using GameCore;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Editor.Content;
using Gameplay.Editor.Scenarios;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>验证首个内置剧本《寒夜营地》的最小可玩闭环。</summary>
	public sealed class HanYeCampScenarioEditModeTests
	{
		[SetUp]
		public void SetUp()
		{
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
		}

		[TearDown]
		public void TearDown()
		{
			InvokeFormalGasBootstrap("Shutdown");
		}

		[Test]
		public void RebuildScenarioAssets_CreatesValidDefenseAndExplorationLoop()
		{
			ScenarioDefinition scenario = HanYeCampScenarioAssetMenu.RebuildScenarioAssets();
			IReadOnlyList<ContentAsset> assets = HanYeCampScenarioAssetMenu.LoadScenarioContentAssets();
			ContentValidationReport report =
				ContentValidationMenu.ValidateContentAssetsForEditor(assets);
			Assert.That(
				report.HasErrors,
				Is.False,
				string.Join(Environment.NewLine, report.Issues.Select(issue => issue.Code + ": " + issue.Message)));

			ContentIndex content = ContentIndex.Build(assets);
			ScenarioRun run = new ScenarioRun(scenario, content, 12345u);
			try
			{
				run.ActivateInitialQuests();
				Assert.That(run.Tabletop.Cards.CardCount, Is.EqualTo(10));
				Assert.That(run.GetDiscoveredActions().Select(action => action.ContentId.Value), Is.EquivalentTo(new[]
				{
					HanYeCampScenarioAssetMenu.ExploreLocationActionId,
					HanYeCampScenarioAssetMenu.ScavengeSuppliesActionId,
					HanYeCampScenarioAssetMenu.DefendCampActionId
				}));
				Assert.That(CountCards(run, HanYeCampScenarioAssetMenu.ScoutId), Is.EqualTo(1));
				Assert.That(CountCards(run, HanYeCampScenarioAssetMenu.MechanicId), Is.EqualTo(1));
				Assert.That(CountCards(run, HanYeCampScenarioAssetMenu.SurvivorAningId), Is.EqualTo(1));
				Assert.That(CountCards(run, HanYeCampScenarioAssetMenu.LocationStoreId), Is.EqualTo(1));
				Assert.That(CountCards(run, HanYeCampScenarioAssetMenu.ThreatBrokenFenceId), Is.EqualTo(1));

				ActionCandidate explore = FindDragCandidate(
					run,
					HanYeCampScenarioAssetMenu.ScoutId,
					HanYeCampScenarioAssetMenu.LocationStoreId,
					HanYeCampScenarioAssetMenu.ExploreLocationActionId);
				int cardsBeforeExplore = run.Tabletop.Cards.CardCount;
				run.StartAction(ActionRequest.FromCandidate(explore));
				run.ConfirmTurn();
				Assert.That(run.Tabletop.Cards.CardCount, Is.GreaterThan(cardsBeforeExplore));
				Assert.That(
					run.QuestLog.GetQuest(new ContentId(HanYeCampScenarioAssetMenu.StabilizeCampQuestId))
						.Tasks[0]
						.Progress
						.CurrentAmount,
					Is.EqualTo(1));

				ActionCandidate defend = FindDragCandidate(
					run,
					HanYeCampScenarioAssetMenu.MechanicId,
					HanYeCampScenarioAssetMenu.ThreatBrokenFenceId,
					HanYeCampScenarioAssetMenu.DefendCampActionId);
				int brokenFenceBeforeDefend = CountCards(run, HanYeCampScenarioAssetMenu.ThreatBrokenFenceId);
				run.StartAction(ActionRequest.FromCandidate(defend));
				run.ConfirmTurn();
				Assert.That(
					CountCards(run, HanYeCampScenarioAssetMenu.ThreatBrokenFenceId),
					Is.EqualTo(brokenFenceBeforeDefend - 1));
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingFeedingConfirmation));

				ScenarioTabletopStats statsBeforeFeeding = run.GetTabletopStats();
				run.ContinueDayCycle();
				ScenarioTabletopStats statsAfterFeeding = run.GetTabletopStats();
				Assert.That(
					statsAfterFeeding.TotalFoodNutrition,
					Is.EqualTo(statsBeforeFeeding.TotalFoodNutrition - statsBeforeFeeding.NutritionNeed));
				Assert.That(statsAfterFeeding.TotalFoodNutrition, Is.GreaterThanOrEqualTo(2));
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingNewDayConfirmation));
			}
			finally
			{
				run.End();
			}
		}

		private static ActionCandidate FindDragCandidate(
			ScenarioRun run,
			string sourceContentId,
			string targetContentId,
			string actionContentId)
		{
			TabletopCard source = FindCard(run, sourceContentId);
			TabletopCard target = FindCard(run, targetContentId);
			ActionCandidate[] candidates = run.FindActionCandidates(
				new TabletopCardPointerReleaseIntent(
					source.Id,
					source.Position,
					target.Position,
					source.Position,
					isDrag: true,
					target.Id));
			ActionCandidate candidate = candidates.SingleOrDefault(candidate =>
				candidate.Action.ContentId.Value == actionContentId &&
				candidate.IsReady);
			Assert.That(
				candidate,
				Is.Not.Null,
				$"找不到可执行行动：{actionContentId}，候选数={candidates.Length}。");
			return candidate;
		}

		private static TabletopCard FindCard(ScenarioRun run, string contentId)
		{
			for (int stackIndex = 0; stackIndex < run.Tabletop.Cards.Stacks.Count; stackIndex++)
			{
				IReadOnlyList<TabletopCard> cards = run.Tabletop.Cards.Stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					if (cards[cardIndex].ContentId.Value == contentId)
					{
						return cards[cardIndex];
					}
				}
			}
			throw new InvalidOperationException($"牌桌上找不到内容：{contentId}");
		}

		private static int CountCards(ScenarioRun run, string contentId)
		{
			int count = 0;
			for (int stackIndex = 0; stackIndex < run.Tabletop.Cards.Stacks.Count; stackIndex++)
			{
				IReadOnlyList<TabletopCard> cards = run.Tabletop.Cards.Stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					if (cards[cardIndex].ContentId.Value == contentId)
					{
						count++;
					}
				}
			}
			return count;
		}

		private static void InvokeFormalGasBootstrap(string methodName)
		{
			Type bootstrapType = typeof(GameManager).Assembly.GetType(
				"GameCore.FormalAbilityRuntimeBootstrap",
				throwOnError: true);
			MethodInfo method = bootstrapType.GetMethod(
				methodName,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				throw new InvalidOperationException($"找不到 FormalAbilityRuntimeBootstrap.{methodName}。");
			}
			method.Invoke(null, null);
		}
	}
}
