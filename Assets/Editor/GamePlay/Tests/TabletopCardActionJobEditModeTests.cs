using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GamePlay.Tests
{
    /// <summary>
    /// 验证玩家显式选择牌桌行动后，作业只以行动回合消耗作为进度真相。
    /// </summary>
    public sealed class TabletopCardActionJobEditModeTests
    {
        [Test]
        public void StartActionRequest_CreatesRunningJobFromTheSelectedCandidateData()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 2);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);

                TabletopCardActionJob job = actionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate));

                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Running));
                Assert.That(job.ActionId, Is.EqualTo(context.Action.ContentId));
                Assert.That(job.TurnCost, Is.EqualTo(2));
                Assert.That(job.ProgressedTurns, Is.Zero);
                Assert.That(job.Progress, Is.Zero);
                Assert.That(job.Bindings, Is.Not.SameAs(context.Candidate.Bindings));
                CollectionAssert.AreEqual(
                    context.Candidate.Bindings[0].CardIds,
                    job.Bindings[0].CardIds);
                Assert.That(actionSystem.ActiveJobs, Has.Count.EqualTo(1));
                Assert.That(actionSystem.ActiveJobs[0], Is.SameAs(job));
                Assert.That(
                    actionSystem.ProgressionMode,
                    Is.EqualTo(TabletopCardActionProgressionMode.TurnBased));
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        [Test]
        public void PauseResumeAndCancel_KeepOneLegalLifecycleState()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 2);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);
                TabletopCardActionJob job = actionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate));

                actionSystem.PauseAction(job);
                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Paused));

                actionSystem.ResumeAction(job);
                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Running));

                actionSystem.CancelAction(job);
                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Cancelled));
                Assert.That(
                    job.CancellationReason,
                    Is.EqualTo(TabletopCardActionCancellationReason.Requested));
                Assert.That(actionSystem.ActiveJobs, Is.Empty);
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        [Test]
        public void ConfirmedWorldTurn_RemovedParticipantCancelsBeforeProgress()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 2);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);
                TabletopCardActionJob job = actionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate));

                context.State.RemoveCard(context.Target.Id);
                worldTurnSystem.ConfirmTurn();

                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Cancelled));
                Assert.That(
                    job.CancellationReason,
                    Is.EqualTo(TabletopCardActionCancellationReason.ParticipantInvalidated));
                Assert.That(job.ProgressedTurns, Is.Zero);
                Assert.That(actionSystem.ActiveJobs, Is.Empty);
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        [Test]
        public void OnSystemStop_CancelsActiveJobWithSystemStoppedReason()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 2);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);
                TabletopCardActionJob job = actionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate));

                actionSystem.OnSystemStop();

                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Cancelled));
                Assert.That(
                    job.CancellationReason,
                    Is.EqualTo(TabletopCardActionCancellationReason.SystemStopped));
                Assert.That(actionSystem.ActiveJobs, Is.Empty);
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        [Test]
        public void ConfirmedWorldTurn_UsesTheActionTurnCostAsTheOnlyProgressTruth()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 2);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);
                TabletopCardActionJob job = actionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate));

                worldTurnSystem.ConfirmTurn();
                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Running));
                Assert.That(job.ProgressedTurns, Is.EqualTo(1f));
                Assert.That(job.Progress, Is.EqualTo(0.5f));

                worldTurnSystem.ConfirmTurn();
                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Completed));
                Assert.That(job.ProgressedTurns, Is.EqualTo(2f));
                Assert.That(job.Progress, Is.EqualTo(1f));
                Assert.That(actionSystem.ActiveJobs, Is.Empty);
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        [Test]
        public void StartAction_ZeroTurnCostCompletesImmediately()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 0);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);

                TabletopCardActionJob job = actionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate));

                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Completed));
                Assert.That(job.ProgressedTurns, Is.Zero);
                Assert.That(job.Progress, Is.EqualTo(1f));
                Assert.That(actionSystem.ActiveJobs, Is.Empty);
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        [Test]
        public void StartActionRequest_RevalidatesCurrentStateBeforeCreatingJob()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 2);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);
                TabletopCardActionRequest request = TabletopCardActionRequest.FromCandidate(context.Candidate);
                context.State.RemoveCard(context.Target.Id);

                Assert.Throws<InvalidOperationException>(() => actionSystem.StartAction(request));
                Assert.That(actionSystem.ActiveJobs, Is.Empty);
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        [Test]
        public void StartActionRequest_RebuildsCandidateFromRequest()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 2);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);
                TabletopCardActionRequest request = TabletopCardActionRequest.FromCandidate(context.Candidate);

                TabletopCardActionJob job = actionSystem.StartAction(request);

                Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Running));
                Assert.That(job.ActionId, Is.EqualTo(context.Action.ContentId));
                Assert.That(job.Bindings, Is.Not.SameAs(context.Candidate.Bindings));
                Assert.That(job.Bindings.Count, Is.EqualTo(context.Candidate.Bindings.Count));
                Assert.That(job.Bindings[0].Slot, Is.SameAs(context.Action.ParticipationSlots[0]));
                CollectionAssert.AreEqual(
                    context.Candidate.Bindings[0].CardIds,
                    job.Bindings[0].CardIds);
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        [Test]
        public void StartActionRequest_RejectsDuplicateCardBindings()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 2);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);
                var request = new TabletopCardActionRequest(
                    context.Action.ContentId,
                    new[]
                    {
                        new TabletopCardActionRequestBinding(
                            "participant",
                            new[] { context.Source.Id, context.Source.Id })
                    });

                Assert.Throws<InvalidOperationException>(() => actionSystem.StartAction(request));
                Assert.That(actionSystem.ActiveJobs, Is.Empty);
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        [Test]
        public void CreateActiveJobSnapshots_CapturesCurrentRunningJobFacts()
        {
            TestActionContext context = CreateReadyCandidate(turnCost: 2);
            GameObject systemObject = new("TabletopCardActionSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();

            try
            {
                worldTurnSystem.OnSystemStart();
                actionSystem.OnSystemStart();
                actionSystem.BindTabletopActionState(context.State, context.ContentIndex);
                TabletopCardActionJob job = actionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate));

                worldTurnSystem.ConfirmTurn();
                TabletopCardActionJobSnapshot[] snapshots = actionSystem.CreateActiveJobSnapshots();

                Assert.That(snapshots, Has.Length.EqualTo(1));
                TabletopCardActionJobSnapshot snapshot = snapshots[0];
                Assert.That(snapshot.ActionId, Is.EqualTo(context.Action.ContentId));
                Assert.That(snapshot.TurnCost, Is.EqualTo(job.TurnCost));
                Assert.That(snapshot.ProgressedTurns, Is.EqualTo(1f));
                Assert.That(snapshot.State, Is.EqualTo(TabletopCardActionJobState.Running));
                Assert.That(snapshot.CancellationReason, Is.EqualTo(TabletopCardActionCancellationReason.None));
                Assert.That(snapshot.ResultBranchKey, Is.Empty);
                Assert.That(snapshot.Bindings, Has.Count.EqualTo(1));
                Assert.That(snapshot.Bindings[0].SlotKey, Is.EqualTo("participant"));
                CollectionAssert.AreEqual(job.Bindings[0].CardIds, snapshot.Bindings[0].CardIds);
            }
            finally
            {
                actionSystem.OnSystemStop();
                worldTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                context.Dispose();
            }
        }

        private static TestActionContext CreateReadyCandidate(int turnCost)
        {
            GamePlayCardDefinition card = ScriptableObject.CreateInstance<GamePlayCardDefinition>();
            JsonUtility.FromJsonOverwrite(
                "{\"m_contentId\":{\"m_value\":\"test.card\"}}",
                card);

            GamePlayActionDefinition action = ScriptableObject.CreateInstance<GamePlayActionDefinition>();
            JsonUtility.FromJsonOverwrite(
                "{" +
                "\"m_contentId\":{\"m_value\":\"test.action\"}," +
                $"\"m_turnCost\":{turnCost}," +
                "\"m_participationSlots\":[{" +
                "\"m_key\":\"participant\"," +
                "\"m_minimumParticipants\":2," +
                "\"m_maximumParticipants\":2," +
                "\"m_allowedContentIds\":[{\"m_value\":\"test.card\"}]" +
                "}]}" ,
                action);

            GamePlayContentIndex contentIndex = GamePlayContentIndex.Build(
                new GamePlayContentAsset[] { card, action });
            var state = new TabletopCardState();
            TabletopCard source = state.CreateCard(card.ContentId, Vector2.zero);
            TabletopCard target = state.CreateCard(card.ContentId, Vector2.one);
            var intent = new TabletopCardPointerReleaseIntent(
                source.Id,
                Vector2.zero,
                Vector2.one,
                isDrag: true,
                target.Id);
            TabletopCardActionCandidate[] candidates = TabletopCardActionCandidateResolver.FindCandidates(
                intent,
                state,
                contentIndex,
                new[] { action });

            Assert.That(candidates, Has.Length.EqualTo(1));
            Assert.That(candidates[0].IsReady, Is.True);
            return new TestActionContext(card, action, state, source, target, contentIndex, candidates[0]);
        }

        private sealed class TestActionContext : System.IDisposable
        {
            internal TestActionContext(
                GamePlayCardDefinition card,
                GamePlayActionDefinition action,
                TabletopCardState state,
                TabletopCard source,
                TabletopCard target,
                GamePlayContentIndex contentIndex,
                TabletopCardActionCandidate candidate)
            {
                Card = card;
                Action = action;
                State = state;
                Source = source;
                Target = target;
                ContentIndex = contentIndex;
                Candidate = candidate;
            }

            internal GamePlayCardDefinition Card { get; }
            internal GamePlayActionDefinition Action { get; }
            internal TabletopCardState State { get; }
            internal TabletopCard Source { get; }
            internal TabletopCard Target { get; }
            internal GamePlayContentIndex ContentIndex { get; }
            internal TabletopCardActionCandidate Candidate { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(Card);
                Object.DestroyImmediate(Action);
            }
        }
    }
}
