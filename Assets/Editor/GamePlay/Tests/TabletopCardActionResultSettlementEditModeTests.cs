using System;
using System.Collections.Generic;
using NUnit.Framework;
using MathematicsRandom = Unity.Mathematics.Random;
using UnityEditor;
using UnityEngine;

namespace GamePlay.Tests
{
    /// <summary>
    /// 验证行动完成后的牌桌结果只在完成点提交，并在作者配置非法时保持牌桌状态不变。
    /// </summary>
    public sealed class TabletopCardActionResultSettlementEditModeTests
    {
        private const string ParticipantContentId = "test.result.participant";
        private const string ProductContentId = "test.result.product";
        private const string ActionContentId = "test.result.action";
        private const string ParticipantSlotKey = "participant";

        [Test]
        public void StartAction_ImmediateActionRemovesBoundCardsAndCreatesProductsAtomically()
        {
            using ResultTestContext context = CreateContext(
                turnCost: 0,
                CreateRemoveIntent(),
                CreateProductIntent(ProductContentId, count: 2));

            TabletopCardActionJob job = context.ActionSystem.StartAction(
                TabletopCardActionRequest.FromCandidate(context.Candidate));

            Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Completed));
            Assert.That(context.ActionSystem.ActiveJobs, Is.Empty);
            AssertSettled(context, expectedProductCount: 2);
        }

        [Test]
        public void ConfirmedWorldTurn_DelayedActionSettlesOnlyAfterRequiredTurnsComplete()
        {
            using ResultTestContext context = CreateContext(
                turnCost: 2,
                CreateRemoveIntent(),
                CreateProductIntent(ProductContentId, count: 1));

            TabletopCardActionJob job = context.ActionSystem.StartAction(
                TabletopCardActionRequest.FromCandidate(context.Candidate));
            context.WorldTurnSystem.ConfirmTurn();

            Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Running));
            AssertUnchanged(context);

            context.WorldTurnSystem.ConfirmTurn();

            Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Completed));
            Assert.That(context.ActionSystem.ActiveJobs, Is.Empty);
            AssertSettled(context, expectedProductCount: 1);
        }

        [Test]
        public void ContentIndexBuild_UnknownProductIsRejectedBeforeRuntimeSettlement()
        {
            GamePlayCardDefinition participant = CreateCardDefinition(ParticipantContentId);
            GamePlayCardDefinition product = CreateCardDefinition(ProductContentId);
            GamePlayActionDefinition action = CreateActionDefinition(
                0,
                new GamePlayActionResultIntent[]
                {
                CreateRemoveIntent(),
                    CreateProductIntent("test.result.missing", count: 1)
                },
                Array.Empty<ResultBranchDefinition>());

            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => GamePlayContentIndex.Build(
                        new GamePlayContentAsset[] { participant, product, action }));

                StringAssert.Contains("ACTION_RESULT_CREATE_CONTENT_UNKNOWN", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(participant);
                UnityEngine.Object.DestroyImmediate(product);
                UnityEngine.Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void StartAction_DuplicateRemovalLeavesTabletopStateUnchanged()
        {
            using ResultTestContext context = CreateContext(
                turnCost: 0,
                CreateRemoveIntent(),
                CreateRemoveIntent());

            Assert.Throws<InvalidOperationException>(
                () => context.ActionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate)));

            AssertUnchanged(context);
        }

        [Test]
        public void StartAction_SettledCandidateMustBeQueriedAgain()
        {
            using ResultTestContext context = CreateContext(
                turnCost: 0,
                CreateRemoveIntent(),
                CreateProductIntent(ProductContentId, count: 1));

            context.ActionSystem.StartAction(
                TabletopCardActionRequest.FromCandidate(context.Candidate));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => context.ActionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate)));
            StringAssert.Contains("重新查询行动候选", exception.Message);
            AssertSettled(context, expectedProductCount: 1);
        }

        [Test]
        public void StartAction_WeightedResultUsesAuthoritativeSeedAndSettlesSelectedBranch()
        {
            const uint seed = 12345;
            var branches = new[]
            {
                CreateBranch("one-product", weight: 1, CreateProductIntent(ProductContentId, count: 1)),
                CreateBranch("two-products", weight: 3, CreateProductIntent(ProductContentId, count: 2))
            };
            using ResultTestContext context = CreateContextWithBranches(seed, branches);

            MathematicsRandom expectedRandom = new(seed);
            uint roll = expectedRandom.NextUInt(4);
            string expectedBranch = roll < 1 ? "one-product" : "two-products";
            int expectedProductCount = expectedBranch == "one-product" ? 1 : 2;

            TabletopCardActionJob job = context.ActionSystem.StartAction(
                TabletopCardActionRequest.FromCandidate(context.Candidate));

            Assert.That(job.ResultBranchKey, Is.EqualTo(expectedBranch));
            AssertSettled(context, expectedProductCount);
        }

        [Test]
        public void StartAction_WeightedResultRequiresAuthoritativeRandom()
        {
            using ResultTestContext context = CreateContextWithBranches(
                null,
                CreateBranch("one-product", weight: 1, CreateProductIntent(ProductContentId, count: 1)));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => context.ActionSystem.StartAction(
                    TabletopCardActionRequest.FromCandidate(context.Candidate)));

            StringAssert.Contains("尚未初始化权威随机流", exception.Message);
            AssertUnchanged(context);
        }

        [Test]
        public void ContentIndexBuild_InvalidWeightedResultIsRejectedBeforeRuntimeRandom()
        {
            GamePlayCardDefinition participant = CreateCardDefinition(ParticipantContentId);
            GamePlayCardDefinition product = CreateCardDefinition(ProductContentId);
            GamePlayActionDefinition action = CreateActionDefinition(
                0,
                new[] { CreateRemoveIntent() },
                new[]
                {
                    CreateBranch("invalid", weight: 0, CreateProductIntent(ProductContentId, count: 1))
                });

            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => GamePlayContentIndex.Build(
                        new GamePlayContentAsset[] { participant, product, action }));

                StringAssert.Contains("ACTION_RESULT_BRANCH_WEIGHT_INVALID", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(participant);
                UnityEngine.Object.DestroyImmediate(product);
                UnityEngine.Object.DestroyImmediate(action);
            }
        }

        private static ResultTestContext CreateContext(
            int turnCost,
            params GamePlayActionResultIntent[] resultIntents)
        {
            return CreateContextCore(turnCost, seed: null, resultIntents, Array.Empty<ResultBranchDefinition>());
        }

        private static ResultTestContext CreateContextWithBranches(
            uint? seed,
            params ResultBranchDefinition[] branches)
        {
            return CreateContextCore(
                0,
                seed,
                new[] { CreateRemoveIntent() },
                branches);
        }

        private static ResultTestContext CreateContextCore(
            int turnCost,
            uint? seed,
            IReadOnlyList<GamePlayActionResultIntent> resultIntents,
            IReadOnlyList<ResultBranchDefinition> branches)
        {
            GamePlayCardDefinition participant = CreateCardDefinition(ParticipantContentId);
            GamePlayCardDefinition product = CreateCardDefinition(ProductContentId);
            GamePlayActionDefinition action = CreateActionDefinition(turnCost, resultIntents, branches);
            GamePlayContentIndex contentIndex = GamePlayContentIndex.Build(
                new GamePlayContentAsset[] { participant, product, action });

            var state = new TabletopCardState();
            Vector2 sourcePosition = new(-2f, 1f);
            TabletopCard source = state.CreateCard(participant.ContentId, sourcePosition);
            TabletopCard target = state.CreateCard(participant.ContentId, new Vector2(3f, -1f));
            var pointerIntent = new TabletopCardPointerReleaseIntent(
                source.Id,
                sourcePosition,
                releasePosition: new Vector2(3f, -1f),
                isDrag: true,
                targetCardId: target.Id);
            TabletopCardActionCandidate[] candidates = TabletopCardActionCandidateResolver.FindCandidates(
                pointerIntent,
                state,
                contentIndex,
                new[] { action });
            Assert.That(candidates, Has.Length.EqualTo(1));
            Assert.That(candidates[0].IsReady, Is.True);

            GameObject systemObject = new("TabletopCardActionResultSettlementTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            TabletopCardActionSystem actionSystem = systemObject.AddComponent<TabletopCardActionSystem>();
            worldTurnSystem.OnSystemStart();
            actionSystem.OnSystemStart();
            actionSystem.BindTabletopActionState(state, contentIndex);
            if (seed.HasValue)
            {
                actionSystem.InitializeAuthoritativeRandom(seed.Value);
            }
            return new ResultTestContext(
                participant,
                product,
                action,
                state,
                source,
                target,
                sourcePosition,
                candidates[0],
                systemObject,
                worldTurnSystem,
                actionSystem);
        }

        private static GamePlayCardDefinition CreateCardDefinition(string contentId)
        {
            GamePlayCardDefinition definition = ScriptableObject.CreateInstance<GamePlayCardDefinition>();
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static GamePlayActionDefinition CreateActionDefinition(
            int turnCost,
            IReadOnlyList<GamePlayActionResultIntent> resultIntents,
            IReadOnlyList<ResultBranchDefinition> branches = null)
        {
            GamePlayActionDefinition action = ScriptableObject.CreateInstance<GamePlayActionDefinition>();
            JsonUtility.FromJsonOverwrite(
                "{" +
                $"\"m_contentId\":{{\"m_value\":\"{ActionContentId}\"}}," +
                $"\"m_turnCost\":{turnCost}," +
                "\"m_participationSlots\":[{" +
                $"\"m_key\":\"{ParticipantSlotKey}\"," +
                "\"m_minimumParticipants\":2," +
                "\"m_maximumParticipants\":2," +
                $"\"m_allowedContentIds\":[{{\"m_value\":\"{ParticipantContentId}\"}}]" +
                "}]}",
                action);

            var serializedAction = new SerializedObject(action);
            SerializedProperty intentsProperty = serializedAction.FindProperty("m_resultIntents");
            intentsProperty.arraySize = resultIntents.Count;
            for (int i = 0; i < resultIntents.Count; i++)
            {
                intentsProperty.GetArrayElementAtIndex(i).managedReferenceValue = resultIntents[i];
            }

            serializedAction.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject branchesObject = new(action);
            SerializedProperty branchesProperty = branchesObject.FindProperty("m_resultBranches");
            branchesProperty.arraySize = branches?.Count ?? 0;
            for (int i = 0; i < (branches?.Count ?? 0); i++)
            {
                ResultBranchDefinition branchDefinition = branches[i];
                SerializedProperty branchProperty = branchesProperty.GetArrayElementAtIndex(i);
                branchProperty.FindPropertyRelative("m_key").stringValue = branchDefinition.Key;
                branchProperty.FindPropertyRelative("m_weight").intValue = branchDefinition.Weight;
                SerializedProperty branchIntents = branchProperty.FindPropertyRelative("m_resultIntents");
                branchIntents.arraySize = branchDefinition.Intents.Count;
                for (int intentIndex = 0; intentIndex < branchDefinition.Intents.Count; intentIndex++)
                {
                    branchIntents.GetArrayElementAtIndex(intentIndex).managedReferenceValue =
                        branchDefinition.Intents[intentIndex];
                }
            }

            branchesObject.ApplyModifiedPropertiesWithoutUndo();
            return action;
        }

        private static ResultBranchDefinition CreateBranch(
            string key,
            int weight,
            params GamePlayActionResultIntent[] intents)
        {
            return new ResultBranchDefinition(key, weight, intents);
        }

        private static TabletopCardRemoveResultIntent CreateRemoveIntent()
        {
            var intent = new TabletopCardRemoveResultIntent();
            JsonUtility.FromJsonOverwrite($"{{\"m_slotKey\":\"{ParticipantSlotKey}\"}}", intent);
            return intent;
        }

        private static TabletopCardCreateResultIntent CreateProductIntent(string contentId, int count)
        {
            var intent = new TabletopCardCreateResultIntent();
            JsonUtility.FromJsonOverwrite(
                "{" +
                $"\"m_contentId\":{{\"m_value\":\"{contentId}\"}}," +
                $"\"m_count\":{count}," +
                $"\"m_anchorSlotKey\":\"{ParticipantSlotKey}\"" +
                "}",
                intent);
            return intent;
        }

        private static void AssertUnchanged(ResultTestContext context)
        {
            Assert.That(context.State.CardCount, Is.EqualTo(2));
            Assert.That(context.State.StackCount, Is.EqualTo(2));
            Assert.That(context.State.TryGetCard(context.Source.Id, out _), Is.True);
            Assert.That(context.State.TryGetCard(context.Target.Id, out _), Is.True);
        }

        private static void AssertSettled(ResultTestContext context, int expectedProductCount)
        {
            Assert.That(context.State.TryGetCard(context.Source.Id, out _), Is.False);
            Assert.That(context.State.TryGetCard(context.Target.Id, out _), Is.False);
            Assert.That(context.State.CardCount, Is.EqualTo(expectedProductCount));
            Assert.That(context.State.StackCount, Is.EqualTo(expectedProductCount));

            int productCount = 0;
            for (int stackIndex = 0; stackIndex < context.State.Stacks.Count; stackIndex++)
            {
                TabletopCardStack stack = context.State.Stacks[stackIndex];
                Assert.That(stack.Position, Is.EqualTo(context.SourcePosition));
                for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
                {
                    Assert.That(stack.Cards[cardIndex].ContentId, Is.EqualTo(context.Product.ContentId));
                    productCount++;
                }
            }

            Assert.That(productCount, Is.EqualTo(expectedProductCount));
        }

        private sealed class ResultTestContext : IDisposable
        {
            internal ResultTestContext(
                GamePlayCardDefinition participant,
                GamePlayCardDefinition product,
                GamePlayActionDefinition action,
                TabletopCardState state,
                TabletopCard source,
                TabletopCard target,
                Vector2 sourcePosition,
                TabletopCardActionCandidate candidate,
                GameObject systemObject,
                GamePlayWorldTurnSystem worldTurnSystem,
                TabletopCardActionSystem actionSystem)
            {
                Participant = participant;
                Product = product;
                Action = action;
                State = state;
                Source = source;
                Target = target;
                SourcePosition = sourcePosition;
                Candidate = candidate;
                SystemObject = systemObject;
                WorldTurnSystem = worldTurnSystem;
                ActionSystem = actionSystem;
            }

            internal GamePlayCardDefinition Participant { get; }
            internal GamePlayCardDefinition Product { get; }
            internal GamePlayActionDefinition Action { get; }
            internal TabletopCardState State { get; }
            internal TabletopCard Source { get; }
            internal TabletopCard Target { get; }
            internal Vector2 SourcePosition { get; }
            internal TabletopCardActionCandidate Candidate { get; }
            internal GameObject SystemObject { get; }
            internal GamePlayWorldTurnSystem WorldTurnSystem { get; }
            internal TabletopCardActionSystem ActionSystem { get; }

            public void Dispose()
            {
                ActionSystem.OnSystemStop();
                WorldTurnSystem.OnSystemStop();
                UnityEngine.Object.DestroyImmediate(SystemObject);
                UnityEngine.Object.DestroyImmediate(Participant);
                UnityEngine.Object.DestroyImmediate(Product);
                UnityEngine.Object.DestroyImmediate(Action);
            }
        }

        private sealed class ResultBranchDefinition
        {
            internal ResultBranchDefinition(
                string key,
                int weight,
                IReadOnlyList<GamePlayActionResultIntent> intents)
            {
                Key = key;
                Weight = weight;
                Intents = intents;
            }

            internal string Key { get; }
            internal int Weight { get; }
            internal IReadOnlyList<GamePlayActionResultIntent> Intents { get; }
        }
    }
}
