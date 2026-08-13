using System;
using System.Collections;
using System.Collections.Generic;
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
	/// 验证行动实例的进度、暂停、取消、快照和参与者失效规则。
	/// </summary>
	public sealed class ActionInstanceEditModeTests
	{
		private sealed class TestActionContext : IDisposable
		{
			internal CardDefinition Card { get; }

			internal ActionDefinition Action { get; }

			internal ScenarioDefinition Scenario { get; }

			internal ScenarioRegionDefinition Region { get; }

			internal ScenarioRun ScenarioRun { get; }

			internal Gameplay.Tabletop.Tabletop Tabletop => ScenarioRun.Tabletop;

			internal TabletopCards State { get; }

			internal TabletopCard Source { get; }

			internal TabletopCard Target { get; }

			internal ContentIndex ContentIndex { get; }

			internal ActionCandidate Candidate { get; }

			internal TestActionContext(CardDefinition card, ActionDefinition action, ScenarioDefinition scenario, ScenarioRegionDefinition region, ScenarioRun scenarioRun, TabletopCards state, TabletopCard source, TabletopCard target, ContentIndex contentIndex, ActionCandidate candidate)
			{
				Card = card;
				Action = action;
				Scenario = scenario;
				Region = region;
				ScenarioRun = scenarioRun;
				State = state;
				Source = source;
				Target = target;
				ContentIndex = contentIndex;
				Candidate = candidate;
			}

			public void Dispose()
			{
				Object.DestroyImmediate((Object)(object)Card);
				Object.DestroyImmediate((Object)(object)Action);
				Object.DestroyImmediate((Object)(object)Scenario);
				Object.DestroyImmediate((Object)(object)Region);
			}
		}

		[Test]
		public void StartActionRequest_CreatesRunningActionFromTheSelectedCandidateData()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionInstance actionInstance = tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Running));
				Assert.That<ContentId>(actionInstance.ActionId, (IResolveConstraint)(object)Is.EqualTo((object)context.Action.ContentId));
				Assert.That<int>(actionInstance.TurnCost, (IResolveConstraint)(object)Is.EqualTo((object)2));
				Assert.That<float>(actionInstance.ProgressedTurns, (IResolveConstraint)(object)Is.Zero);
				Assert.That<float>(actionInstance.Progress, (IResolveConstraint)(object)Is.Zero);
				Assert.That<IReadOnlyList<ActionSlotBinding>>(actionInstance.Bindings, (IResolveConstraint)(object)Is.Not.SameAs((object)context.Candidate.Bindings));
				CollectionAssert.AreEqual((IEnumerable)context.Candidate.Bindings[0].CardIds, (IEnumerable)actionInstance.Bindings[0].CardIds);
				Assert.That<IReadOnlyList<ActionInstance>>(tabletop.ActiveActions, (IResolveConstraint)(object)((ConstraintExpression)Has.Count).EqualTo((object)1));
				Assert.That<ActionInstance>(tabletop.ActiveActions[0], (IResolveConstraint)(object)Is.SameAs((object)actionInstance));
				Assert.That<ActionProgressionMode>(tabletop.ProgressionMode, (IResolveConstraint)(object)Is.EqualTo((object)ActionProgressionMode.TurnBased));
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void PauseResumeAndCancel_KeepOneLegalLifecycleState()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionInstance actionInstance = tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				tabletop.PauseAction(actionInstance);
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Paused));
				tabletop.ResumeAction(actionInstance);
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Running));
				tabletop.CancelAction(actionInstance);
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Cancelled));
				Assert.That<ActionCancellationReason>(actionInstance.CancellationReason, (IResolveConstraint)(object)Is.EqualTo((object)ActionCancellationReason.Requested));
				Assert.That<IReadOnlyList<ActionInstance>>(tabletop.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void ConfirmedWorldTurn_RemovedParticipantCancelsBeforeProgress()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionInstance actionInstance = tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				context.State.RemoveCard(context.Target.Id);
				context.ScenarioRun.ConfirmTurn();
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Cancelled));
				Assert.That<ActionCancellationReason>(actionInstance.CancellationReason, (IResolveConstraint)(object)Is.EqualTo((object)ActionCancellationReason.ParticipantInvalidated));
				Assert.That<float>(actionInstance.ProgressedTurns, (IResolveConstraint)(object)Is.Zero);
				Assert.That<IReadOnlyList<ActionInstance>>(tabletop.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void EndScenario_CancelsActiveActionWithScenarioEndedReason()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionInstance actionInstance = tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				context.ScenarioRun.End();
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Cancelled));
				Assert.That<ActionCancellationReason>(actionInstance.CancellationReason, (IResolveConstraint)(object)Is.EqualTo((object)ActionCancellationReason.ScenarioEnded));
				Assert.That<IReadOnlyList<ActionInstance>>(tabletop.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void ConfirmedWorldTurn_UsesTheActionTurnCostAsTheOnlyProgressTruth()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionInstance actionInstance = tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				context.ScenarioRun.ConfirmTurn();
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Running));
				Assert.That<float>(actionInstance.ProgressedTurns, (IResolveConstraint)(object)Is.EqualTo((object)1f));
				Assert.That<float>(actionInstance.Progress, (IResolveConstraint)(object)Is.EqualTo((object)0.5f));
				context.ScenarioRun.ConfirmTurn();
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Completed));
				Assert.That<float>(actionInstance.ProgressedTurns, (IResolveConstraint)(object)Is.EqualTo((object)2f));
				Assert.That<float>(actionInstance.Progress, (IResolveConstraint)(object)Is.EqualTo((object)1f));
				Assert.That<IReadOnlyList<ActionInstance>>(tabletop.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void StartAction_ZeroTurnCostCompletesImmediately()
		{
			TestActionContext context = CreateReadyCandidate(0);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionInstance actionInstance = tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Completed));
				Assert.That<float>(actionInstance.ProgressedTurns, (IResolveConstraint)(object)Is.Zero);
				Assert.That<float>(actionInstance.Progress, (IResolveConstraint)(object)Is.EqualTo((object)1f));
				Assert.That<IReadOnlyList<ActionInstance>>(tabletop.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void StartActionRequest_RevalidatesCurrentStateBeforeCreatingAction()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionRequest request = ActionRequest.FromCandidate(context.Candidate);
				context.State.RemoveCard(context.Target.Id);
				Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					tabletop.StartAction(request);
				});
				Assert.That<IReadOnlyList<ActionInstance>>(tabletop.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void StartActionRequest_RebuildsCandidateFromRequest()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionRequest request = ActionRequest.FromCandidate(context.Candidate);
				ActionInstance actionInstance = tabletop.StartAction(request);
				Assert.That<ActionInstanceState>(actionInstance.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Running));
				Assert.That<ContentId>(actionInstance.ActionId, (IResolveConstraint)(object)Is.EqualTo((object)context.Action.ContentId));
				Assert.That<IReadOnlyList<ActionSlotBinding>>(actionInstance.Bindings, (IResolveConstraint)(object)Is.Not.SameAs((object)context.Candidate.Bindings));
				Assert.That<int>(actionInstance.Bindings.Count, (IResolveConstraint)(object)Is.EqualTo((object)context.Candidate.Bindings.Count));
				Assert.That<ActionSlotDefinition>(actionInstance.Bindings[0].Slot, (IResolveConstraint)(object)Is.SameAs((object)context.Action.ParticipationSlots[0]));
				CollectionAssert.AreEqual((IEnumerable)context.Candidate.Bindings[0].CardIds, (IEnumerable)actionInstance.Bindings[0].CardIds);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void StartActionRequest_RejectsDuplicateCardBindings()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionRequest request = new ActionRequest(context.Action.ContentId, new ActionRequestBinding[1]
				{
					new ActionRequestBinding("participant", new TabletopCardId[2]
					{
						context.Source.Id,
						context.Source.Id
					})
				});
				Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					tabletop.StartAction(request);
				});
				Assert.That<IReadOnlyList<ActionInstance>>(tabletop.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void CreateActiveActionSnapshots_CapturesCurrentRunningActionFacts()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionInstance actionInstance = tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				context.ScenarioRun.ConfirmTurn();
				ActionInstanceSnapshot[] snapshots = tabletop.CreateActiveActionSnapshots();
				Assert.That<ActionInstanceSnapshot[]>(snapshots, (IResolveConstraint)(object)((ConstraintExpression)Has.Length).EqualTo((object)1));
				ActionInstanceSnapshot snapshot = snapshots[0];
				Assert.That<ContentId>(snapshot.ActionId, (IResolveConstraint)(object)Is.EqualTo((object)context.Action.ContentId));
				Assert.That<int>(snapshot.TurnCost, (IResolveConstraint)(object)Is.EqualTo((object)actionInstance.TurnCost));
				Assert.That<float>(snapshot.ProgressedTurns, (IResolveConstraint)(object)Is.EqualTo((object)1f));
				Assert.That<ActionInstanceState>(snapshot.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Running));
				Assert.That<string>(snapshot.ResultBranchKey, (IResolveConstraint)(object)Is.Empty);
				Assert.That(snapshot.Bindings.Count, Is.EqualTo(1));
				Assert.That<string>(snapshot.Bindings[0].SlotKey, (IResolveConstraint)(object)Is.EqualTo((object)"participant"));
				CollectionAssert.AreEqual((IEnumerable)actionInstance.Bindings[0].CardIds, (IEnumerable)snapshot.Bindings[0].CardIds);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void ActiveActionSnapshot_JsonRoundTripPreservesRuntimeFactsAndFrozenResultPlan()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ConfigureCreateResult(context.Action, context.Card.ContentId);
				ActionInstance actionInstance = tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				context.ScenarioRun.ConfirmTurn();
				ActionInstanceSnapshot original = tabletop.CreateActiveActionSnapshots()[0];
				string json = JsonUtility.ToJson(original);
				ActionInstanceSnapshot deserialized = JsonUtility.FromJson<ActionInstanceSnapshot>(json);

				Assert.That(json, Does.Contain("m_actionId"));
				Assert.That<ContentId>(deserialized.ActionId, (IResolveConstraint)(object)Is.EqualTo((object)context.Action.ContentId));
				Assert.That<int>(deserialized.TurnCost, (IResolveConstraint)(object)Is.EqualTo((object)2));
				Assert.That<float>(deserialized.ProgressedTurns, (IResolveConstraint)(object)Is.EqualTo((object)1f));
				Assert.That<ActionInstanceState>(deserialized.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Running));
				Assert.That(deserialized.Bindings.Count, Is.EqualTo(1));
				Assert.That<ActionResultPlanSnapshot>(deserialized.ResultPlan, (IResolveConstraint)(object)Is.Not.Null);
				Assert.That(deserialized.ResultPlan.Creations.Count, Is.EqualTo(1));
				Assert.That<ContentId>(deserialized.ResultPlan.Creations[0].ContentId, (IResolveConstraint)(object)Is.EqualTo((object)context.Card.ContentId));
				Assert.That<int>(deserialized.ResultPlan.Creations[0].Count, (IResolveConstraint)(object)Is.EqualTo((object)1));
				Assert.That<TabletopCardId>(deserialized.ResultPlan.Creations[0].AnchorCardId, (IResolveConstraint)(object)Is.EqualTo((object)context.Source.Id));
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void RestoreTabletop_ContinuesRunningActionWithFrozenResultPlan()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ConfigureCreateResult(context.Action, context.Card.ContentId);
				tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				TabletopCardStateSnapshot cardStateSnapshot = JsonRoundTrip(tabletop.Cards.CreateSnapshot());
				ActionInstanceSnapshot[] actionSnapshots = new ActionInstanceSnapshot[1]
				{
					JsonRoundTrip(tabletop.CreateActiveActionSnapshots()[0])
				};
				SetResultIntents(context.Action, Array.Empty<ActionResultIntent>());
				List<ContentId> completedActionIds = new List<ContentId>();
				Gameplay.Tabletop.Tabletop restored = new Gameplay.Tabletop.Tabletop(
					context.ContentIndex,
					cardStateSnapshot,
					TabletopTestPlacement.Rules,
					actionSnapshots,
					completedActionIds.Add,
					cardIdSequence: new TabletopCardIdSequence(context.State.CardIdSequence.NextValue));

				Assert.That<IReadOnlyList<ActionInstance>>(restored.ActiveActions, (IResolveConstraint)(object)((ConstraintExpression)Has.Count).EqualTo((object)1));
				Assert.That<ActionInstanceState>(restored.ActiveActions[0].State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Running));
				restored.AdvanceConfirmedTurn();
				Assert.That(restored.ActiveActions.Count, Is.EqualTo(1));
				Assert.That(restored.ActiveActions[0].ProgressedTurns, Is.EqualTo(1f));
				restored.AdvanceConfirmedTurn();

				Assert.That<IReadOnlyList<ActionInstance>>(restored.ActiveActions, (IResolveConstraint)(object)Is.Empty);
				CollectionAssert.AreEqual((IEnumerable)new ContentId[1] { context.Action.ContentId }, (IEnumerable)completedActionIds);
				Assert.That<int>(restored.Cards.CardCount, (IResolveConstraint)(object)Is.EqualTo((object)3));
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void RestoreTabletop_LeavesPausedActionPausedUntilExplicitResume()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				ActionInstance actionInstance = tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				tabletop.PauseAction(actionInstance);
				Gameplay.Tabletop.Tabletop restored = new Gameplay.Tabletop.Tabletop(
					context.ContentIndex,
					JsonRoundTrip(tabletop.Cards.CreateSnapshot()),
					TabletopTestPlacement.Rules,
					new ActionInstanceSnapshot[1] { JsonRoundTrip(tabletop.CreateActiveActionSnapshots()[0]) },
					_ => { },
					cardIdSequence: new TabletopCardIdSequence(context.State.CardIdSequence.NextValue));
				ActionInstance restoredAction = restored.ActiveActions[0];

				restored.AdvanceConfirmedTurn();
				Assert.That<ActionInstanceState>(restoredAction.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Paused));
				Assert.That<float>(restoredAction.ProgressedTurns, (IResolveConstraint)(object)Is.Zero);
				restored.ResumeAction(restoredAction);
				restored.AdvanceConfirmedTurn();
				restored.AdvanceConfirmedTurn();

				Assert.That<ActionInstanceState>(restoredAction.State, (IResolveConstraint)(object)Is.EqualTo((object)ActionInstanceState.Completed));
				Assert.That<IReadOnlyList<ActionInstance>>(restored.ActiveActions, (IResolveConstraint)(object)Is.Empty);
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void RestoreTabletop_RejectsMalformedActiveActionSnapshotBeforePublishingIt()
		{
			TestActionContext context = CreateReadyCandidate(2);
			try
			{
				ActionInstanceSnapshot malformed = JsonUtility.FromJson<ActionInstanceSnapshot>("{}");
				Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					new Gameplay.Tabletop.Tabletop(
						context.ContentIndex,
						context.Tabletop.Cards.CreateSnapshot(),
						TabletopTestPlacement.Rules,
						new ActionInstanceSnapshot[1] { malformed },
						_ => { },
						cardIdSequence: new TabletopCardIdSequence(context.State.CardIdSequence.NextValue));
				});
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void RestoreTabletop_RejectsSnapshotWhenParticipantCardIsMissing()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				ActionInstanceSnapshot actionSnapshot = JsonRoundTrip(tabletop.CreateActiveActionSnapshots()[0]);
				context.State.RemoveCard(context.Target.Id);

				Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					new Gameplay.Tabletop.Tabletop(
						context.ContentIndex,
						JsonRoundTrip(context.State.CreateSnapshot()),
						TabletopTestPlacement.Rules,
						new ActionInstanceSnapshot[1] { actionSnapshot },
						_ => { },
						cardIdSequence: new TabletopCardIdSequence(context.State.CardIdSequence.NextValue));
				});
			}
			finally
			{
				context.Dispose();
			}
		}

		[Test]
		public void RestoreTabletop_RejectsSnapshotWhenActionTurnCostChanged()
		{
			TestActionContext context = CreateReadyCandidate(2);
			Gameplay.Tabletop.Tabletop tabletop = context.Tabletop;
			try
			{
				tabletop.StartAction(ActionRequest.FromCandidate(context.Candidate));
				ActionInstanceSnapshot actionSnapshot = JsonRoundTrip(tabletop.CreateActiveActionSnapshots()[0]);
				JsonUtility.FromJsonOverwrite("{\"m_turnCost\":3}", context.Action);

				Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					new Gameplay.Tabletop.Tabletop(
						context.ContentIndex,
						JsonRoundTrip(tabletop.Cards.CreateSnapshot()),
						TabletopTestPlacement.Rules,
						new ActionInstanceSnapshot[1] { actionSnapshot },
						_ => { },
						cardIdSequence: new TabletopCardIdSequence(context.State.CardIdSequence.NextValue));
				});
			}
			finally
			{
				context.Dispose();
			}
		}

		private static T JsonRoundTrip<T>(T source) where T : class
		{
			return JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
		}

		private static void ConfigureCreateResult(ActionDefinition action, ContentId contentId)
		{
			CreateCardsResultIntent intent = new CreateCardsResultIntent();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId.Value + "\"},\"m_count\":1,\"m_anchorSlotKey\":\"participant\"}",
				intent);
			SetResultIntents(action, new ActionResultIntent[1] { intent });
		}

		private static void SetResultIntents(ActionDefinition action, ActionResultIntent[] intents)
		{
			FieldInfo field = typeof(ActionDefinition).GetField(
				"m_resultIntents",
				BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null)
			{
				throw new InvalidOperationException("测试无法定位行动作者源的结果意图字段。");
			}
			field.SetValue(action, intents);
		}

		private static TestActionContext CreateReadyCandidate(int turnCost)
		{
			CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"test.card\"}}", (object)card);
			ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"test.action\"}," + $"\"m_turnCost\":{turnCost}," + "\"m_participationSlots\":[{\"m_key\":\"participant\",\"m_minimumParticipants\":2,\"m_maximumParticipants\":2,\"m_allowedContentIds\":[{\"m_value\":\"test.card\"}]}]}", (object)action);
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"test.scenario.region\"}}", region);
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.scenario\"}," +
				"\"m_initialRegionId\":{\"m_value\":\"test.scenario.region\"}," +
				"\"m_regionIds\":[{\"m_value\":\"test.scenario.region\"}]}",
				scenario);
			ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[4] { card, action, region, scenario });
			ScenarioRun scenarioRun = new ScenarioRun(scenario, contentIndex, 12345u);
			TabletopCards state = scenarioRun.Tabletop.Cards;
			TabletopCard source = state.CreateCard(card.ContentId, Vector2.zero);
			TabletopCard target = state.CreateCard(card.ContentId, Vector2.one);
			TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(source.Id, Vector2.zero, Vector2.one, Vector2.zero, isDrag: true, target.Id);
			ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(intent, state, contentIndex, new ActionDefinition[1] { action });
			Assert.That<ActionCandidate[]>(candidates, (IResolveConstraint)(object)((ConstraintExpression)Has.Length).EqualTo((object)1));
			Assert.That<bool>(candidates[0].IsReady, (IResolveConstraint)(object)Is.True);
			return new TestActionContext(card, action, scenario, region, scenarioRun, state, source, target, contentIndex, candidates[0]);
		}
	}
}
