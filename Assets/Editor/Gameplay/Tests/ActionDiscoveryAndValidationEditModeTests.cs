using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证行动发现、内部 key 与内容校验的 EditMode 行为合同。
	/// </summary>
	public sealed class ActionDiscoveryAndValidationEditModeTests
	{
		private static readonly Dictionary<ScenarioDefinition, ScenarioRegionDefinition> ScenarioRegions =
			new Dictionary<ScenarioDefinition, ScenarioRegionDefinition>();

		[TearDown]
		public void DestroyScenarioRegions()
		{
			foreach (ScenarioRegionDefinition region in ScenarioRegions.Values)
			{
				Object.DestroyImmediate(region);
			}
			ScenarioRegions.Clear();
		}
		private readonly struct BranchSeed
		{
			internal int Weight { get; }

			internal BranchSeed(int weight)
			{
				Weight = weight;
			}
		}

		[Test]
		public void ScenarioRun_HidesUndiscoveredActionsBeforeCandidateQuery()
		{
			CardDefinition card = CreateCardDefinition("test.card");
			ActionDefinition visibleAction = CreateActionDefinition("test.action.visible", "test.card");
			ActionDefinition hiddenAction = CreateActionDefinition("test.action.hidden", "test.card");
			ScenarioDefinition scenario = CreateScenarioDefinition("test.scenario.discovery");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[4] { card, visibleAction, hiddenAction, scenario });
				ScenarioRun scenarioRun = new ScenarioRun(scenario, contentIndex, 12345u);
				scenarioRun.DiscoverContent(visibleAction.ContentId);
				TabletopCard source = scenarioRun.Tabletop.CreateCard(card.ContentId, Vector2.zero);
				TabletopCard target = scenarioRun.Tabletop.CreateCard(card.ContentId, Vector2.one);
				TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(source.Id, Vector2.zero, Vector2.one, Vector2.zero, isDrag: true, target.Id);
				ActionCandidate[] firstCandidates = scenarioRun.FindActionCandidates(intent);
				Assert.That<ActionCandidate[]>(firstCandidates, (IResolveConstraint)(object)((ConstraintExpression)Has.Length).EqualTo((object)1));
				Assert.That<ActionDefinition>(firstCandidates[0].Action, (IResolveConstraint)(object)Is.SameAs((object)visibleAction));
				scenarioRun.DiscoverContent(hiddenAction.ContentId);
				ActionCandidate[] secondCandidates = scenarioRun.FindActionCandidates(intent);
				Assert.That<ActionCandidate[]>(secondCandidates, (IResolveConstraint)(object)((ConstraintExpression)Has.Length).EqualTo((object)2));
				Assert.That<ActionDefinition>(secondCandidates[0].Action, (IResolveConstraint)(object)Is.SameAs((object)visibleAction));
				Assert.That<ActionDefinition>(secondCandidates[1].Action, (IResolveConstraint)(object)Is.SameAs((object)hiddenAction));
			}
			finally
			{
				Destroy((Object)card, (Object)visibleAction, (Object)hiddenAction, (Object)scenario);
			}
		}

		[Test]
		public void ScenarioRun_ResolvesAvailableActionsFromItsOwnContentIndex()
		{
			CardDefinition card = CreateCardDefinition("test.card");
			ActionDefinition indexedAction = CreateActionDefinition("test.action.indexed", "test.card");
			ScenarioDefinition scenario = CreateScenarioDefinition("test.scenario.action-source");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[3] { card, indexedAction, scenario });
				ScenarioRun scenarioRun = new ScenarioRun(scenario, contentIndex, 12345u);
				scenarioRun.DiscoverContent(indexedAction.ContentId);
				TabletopCard source = scenarioRun.Tabletop.CreateCard(card.ContentId, Vector2.zero);
				TabletopCard target = scenarioRun.Tabletop.CreateCard(card.ContentId, Vector2.one);
				TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(source.Id, Vector2.zero, Vector2.one, Vector2.zero, isDrag: true, target.Id);

				ActionCandidate[] candidates = scenarioRun.FindActionCandidates(intent);

				Assert.That(candidates, Has.Length.EqualTo(1));
				Assert.That(candidates[0].Action, Is.SameAs(indexedAction));
			}
			finally
			{
				Destroy(card, indexedAction, scenario);
			}
		}

		[Test]
		public void ScenarioRun_RejectsUndiscoveredActionRequestsAtTheCommandBoundary()
		{
			CardDefinition card = CreateCardDefinition("test.card");
			ActionDefinition action = CreateActionDefinition("test.action.command", "test.card");
			ScenarioDefinition scenario = CreateScenarioDefinition("test.scenario.action-command");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[3] { card, action, scenario });
				ScenarioRun scenarioRun = new ScenarioRun(scenario, contentIndex, 12345u);
				TabletopCard source = scenarioRun.Tabletop.CreateCard(card.ContentId, Vector2.zero);
				TabletopCard target = scenarioRun.Tabletop.CreateCard(card.ContentId, Vector2.one);
				ActionRequest request = new ActionRequest(
					action.ContentId,
					new[] { new ActionRequestBinding("participant", new[] { source.Id, target.Id }) });

				Assert.Throws<InvalidOperationException>(() => scenarioRun.StartAction(request));
				scenarioRun.DiscoverContent(action.ContentId);
				Assert.DoesNotThrow(() => scenarioRun.StartAction(request));
			}
			finally
			{
				Destroy(card, action, scenario);
			}
		}

		[Test]
		public void ScenarioRun_RejectsUnknownDiscoveryInsteadOfCreatingPlaceholder()
		{
			CardDefinition card = CreateCardDefinition("test.card");
			ScenarioDefinition scenario = CreateScenarioDefinition("test.scenario.discovery-missing");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[2] { card, scenario });
				ScenarioRun scenarioRun = new ScenarioRun(scenario, contentIndex, 12345u);
				InvalidOperationException exception = Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					scenarioRun.DiscoverContent("test.missing");
				});
				StringAssert.Contains("不存在", exception.Message);
				Assert.That<int>(scenarioRun.DiscoveredContentCount, (IResolveConstraint)(object)Is.Zero);
			}
			finally
			{
				Destroy((Object)card, (Object)scenario);
			}
		}

		[Test]
		public void ContentValidator_ReportsActionReferenceErrorsBeforeRuntimeSettlement()
		{
			CardDefinition card = CreateCardDefinition("test.card");
			ActionDefinition action = CreateActionDefinition("test.action.invalid", "test.missing-card");
			SetResultIntents(action, CreateRemoveIntent("missing-slot"), CreateProductIntent("test.missing-product", 0, "participant"));
			SetBranches(action, new BranchSeed(0), new BranchSeed(1));
			action.EnsureLocalAuthoringKeys();
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(new ContentAsset[2] { card, action });
				Assert.That<bool>(report.HasErrors, (IResolveConstraint)(object)Is.True);
				AssertIssue(report, "ACTION_SLOT_ALLOWED_CONTENT_UNKNOWN");
				AssertIssue(report, "ACTION_RESULT_REMOVE_SLOT_UNKNOWN");
				AssertIssue(report, "ACTION_RESULT_CREATE_CONTENT_UNKNOWN");
				AssertIssue(report, "ACTION_RESULT_CREATE_COUNT_INVALID");
				AssertIssue(report, "ACTION_RESULT_BRANCH_WEIGHT_INVALID");
				Assert.That<IEnumerable<string>>(action.ResultBranches.Select((ActionResultBranchDefinition branch) => branch.Key), (IResolveConstraint)(object)Is.Unique, "随机分支的内部键应由行动资产自动维护，不能要求内容作者手填。", Array.Empty<object>());
			}
			finally
			{
				Destroy((Object)card, (Object)action);
			}
		}

		[Test]
		public void ContentValidator_AllowsMultipleActionsWithTheSameParticipantConditions()
		{
			CardDefinition card = CreateCardDefinition("test.card");
			ActionDefinition firstAction = CreateActionDefinition("test.action.first", "test.card");
			ActionDefinition secondAction = CreateActionDefinition("test.action.second", "test.card");
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(new ContentAsset[3] { card, firstAction, secondAction });
				Assert.That<bool>(report.HasErrors, (IResolveConstraint)(object)Is.False);
				AssertNoIssue(report, "ACTION_CONDITION_SIGNATURE_SHARED");
				Assert.DoesNotThrow((TestDelegate)delegate
				{
					ContentIndex.Build(new ContentAsset[3] { card, firstAction, secondAction });
				}, "同一组参与条件可以产生多个玩家可选行动，不属于配方冲突。", Array.Empty<object>());
			}
			finally
			{
				Destroy((Object)card, (Object)firstAction, (Object)secondAction);
			}
		}

		[Test]
		public void ContentValidator_RejectsNegativeTurnCostBeforeRuntime()
		{
			CardDefinition card = CreateCardDefinition("test.card");
			ActionDefinition action = CreateActionDefinition("test.action.invalid-turn-cost", "test.card");
			JsonUtility.FromJsonOverwrite("{\"m_turnCost\":-1}", action);
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(new ContentAsset[2] { card, action });

				Assert.That(report.HasErrors, Is.True);
				AssertIssue(report, "ACTION_TURN_COST_INVALID");
			}
			finally
			{
				Destroy(card, action);
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

		private static ScenarioDefinition CreateScenarioDefinition(string contentId)
		{
			ScenarioDefinition definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			string regionId = contentId + ".region";
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + regionId + "\"}}",
				region);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
				"\"},\"m_initialRegionId\":{\"m_value\":\"" + regionId +
				"\"},\"m_regionIds\":[{\"m_value\":\"" + regionId + "\"}]}",
				definition);
			ScenarioRegions.Add(definition, region);
			return definition;
		}

		private static ContentIndex BuildContentIndex(IEnumerable<ContentAsset> assets)
		{
			List<ContentAsset> content = new List<ContentAsset>(assets);
			for (int i = 0; i < content.Count; i++)
			{
				if (content[i] is ScenarioDefinition scenario &&
					ScenarioRegions.TryGetValue(scenario, out ScenarioRegionDefinition region))
				{
					content.Add(region);
				}
			}
			return ContentIndex.Build(content);
		}

		private static void SetResultIntents(ActionDefinition action, params ActionResultIntent[] intents)
		{
			SerializedObject serializedAction = new SerializedObject((Object)(object)action);
			SerializedProperty intentsProperty = serializedAction.FindProperty("m_resultIntents");
			intentsProperty.arraySize = intents.Length;
			for (int i = 0; i < intents.Length; i++)
			{
				intentsProperty.GetArrayElementAtIndex(i).managedReferenceValue = intents[i];
			}
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetBranches(ActionDefinition action, params BranchSeed[] branches)
		{
			SerializedObject serializedAction = new SerializedObject((Object)(object)action);
			SerializedProperty branchesProperty = serializedAction.FindProperty("m_resultBranches");
			branchesProperty.arraySize = branches.Length;
			for (int i = 0; i < branches.Length; i++)
			{
				SerializedProperty branchProperty = branchesProperty.GetArrayElementAtIndex(i);
				branchProperty.FindPropertyRelative("m_weight").intValue = branches[i].Weight;
			}
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
		}

		private static RemoveCardsResultIntent CreateRemoveIntent(string slotKey)
		{
			RemoveCardsResultIntent intent = new RemoveCardsResultIntent();
			JsonUtility.FromJsonOverwrite("{\"m_slotKey\":\"" + slotKey + "\"}", (object)intent);
			return intent;
		}

		private static CreateCardsResultIntent CreateProductIntent(string contentId, int count, string anchorSlotKey)
		{
			CreateCardsResultIntent intent = new CreateCardsResultIntent();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}," + $"\"m_count\":{count}," + "\"m_anchorSlotKey\":\"" + anchorSlotKey + "\"}", (object)intent);
			return intent;
		}

		private static void AssertIssue(ContentValidationReport report, string code)
		{
			Assert.That<bool>(report.Issues.Any((ContentValidationIssue issue) => issue.Code == code), (IResolveConstraint)(object)Is.True, "校验报告缺少问题码：" + code, Array.Empty<object>());
		}

		private static void AssertNoIssue(ContentValidationReport report, string code)
		{
			Assert.That(report.Issues.Any(issue => issue.Code == code), Is.False, "校验报告不应包含问题码：" + code);
		}

		private static void Destroy(params Object[] objects)
		{
			foreach (Object obj in objects)
			{
				if (obj != (Object)null)
				{
					Object.DestroyImmediate(obj);
				}
			}
		}
	}
}
