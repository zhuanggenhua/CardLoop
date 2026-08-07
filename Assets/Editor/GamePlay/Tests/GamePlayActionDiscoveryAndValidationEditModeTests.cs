using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GamePlay.Tests
{
    /// <summary>
    /// 验证 StackCraft 的配方发现与冲突提示被吸收为 GamePlay 的发现过滤和作者源校验，
    /// 而不是研究随机、配方 UI 或自动执行链路。
    /// </summary>
    public sealed class GamePlayActionDiscoveryAndValidationEditModeTests
    {
        [Test]
        public void DiscoveryFilter_HidesUndiscoveredActionsBeforeCandidateQuery()
        {
            GamePlayCardDefinition card = CreateCardDefinition("test.card");
            GamePlayActionDefinition visibleAction =
                CreateActionDefinition("test.action.visible", "test.card");
            GamePlayActionDefinition hiddenAction =
                CreateActionDefinition("test.action.hidden", "test.card");

            try
            {
                GamePlayContentIndex contentIndex = GamePlayContentIndex.Build(
                    new GamePlayContentAsset[] { card, visibleAction, hiddenAction });
                var discoveryState = new GamePlayContentDiscoveryState();
                discoveryState.MarkDiscovered(visibleAction.ContentId, contentIndex);
                var tabletopState = new TabletopCardState();
                TabletopCard source = tabletopState.CreateCard(card.ContentId, Vector2.zero);
                TabletopCard target = tabletopState.CreateCard(card.ContentId, Vector2.one);
                var intent = new TabletopCardPointerReleaseIntent(
                    source.Id,
                    Vector2.zero,
                    Vector2.one,
                    isDrag: true,
                    target.Id);

                GamePlayActionDefinition[] firstAvailable =
                    GamePlayActionDiscoveryFilter.FilterDiscoveredActions(
                        new[] { visibleAction, hiddenAction },
                        discoveryState);
                TabletopCardActionCandidate[] firstCandidates =
                    TabletopCardActionCandidateResolver.FindCandidates(
                        intent,
                        tabletopState,
                        contentIndex,
                        firstAvailable);

                Assert.That(firstCandidates, Has.Length.EqualTo(1));
                Assert.That(firstCandidates[0].Action, Is.SameAs(visibleAction));

                discoveryState.MarkDiscovered(hiddenAction.ContentId, contentIndex);
                GamePlayActionDefinition[] secondAvailable =
                    GamePlayActionDiscoveryFilter.FilterDiscoveredActions(
                        new[] { visibleAction, hiddenAction },
                        discoveryState);
                TabletopCardActionCandidate[] secondCandidates =
                    TabletopCardActionCandidateResolver.FindCandidates(
                        intent,
                        tabletopState,
                        contentIndex,
                        secondAvailable);

                Assert.That(secondCandidates, Has.Length.EqualTo(2));
                Assert.That(secondCandidates[0].Action, Is.SameAs(visibleAction));
                Assert.That(secondCandidates[1].Action, Is.SameAs(hiddenAction));
            }
            finally
            {
                Destroy(card, visibleAction, hiddenAction);
            }
        }

        [Test]
        public void DiscoveryState_RejectsUnknownContentInsteadOfCreatingPlaceholderDiscoveries()
        {
            GamePlayCardDefinition card = CreateCardDefinition("test.card");

            try
            {
                GamePlayContentIndex contentIndex = GamePlayContentIndex.Build(new[] { card });
                var discoveryState = new GamePlayContentDiscoveryState();

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => discoveryState.MarkDiscovered("test.missing", contentIndex));

                StringAssert.Contains("不存在", exception.Message);
                Assert.That(discoveryState.Count, Is.Zero);
            }
            finally
            {
                Destroy(card);
            }
        }

        [Test]
        public void ContentValidator_ReportsActionReferenceErrorsBeforeRuntimeSettlement()
        {
            GamePlayCardDefinition card = CreateCardDefinition("test.card");
            GamePlayActionDefinition action =
                CreateActionDefinition("test.action.invalid", "test.missing-card");
            SetResultIntents(
                action,
                CreateRemoveIntent("missing-slot"),
                CreateProductIntent("test.missing-product", count: 0, anchorSlotKey: "participant"));
            SetBranches(
                action,
                new BranchSeed("duplicate", 0),
                new BranchSeed("duplicate", 1));

            try
            {
                GamePlayContentValidationReport report =
                    GamePlayContentValidator.ValidateContentAssets(new GamePlayContentAsset[] { card, action });

                Assert.That(report.HasErrors, Is.True);
                AssertIssue(report, "ACTION_SLOT_ALLOWED_CONTENT_UNKNOWN");
                AssertIssue(report, "ACTION_RESULT_REMOVE_SLOT_UNKNOWN");
                AssertIssue(report, "ACTION_RESULT_CREATE_CONTENT_UNKNOWN");
                AssertIssue(report, "ACTION_RESULT_CREATE_COUNT_INVALID");
                AssertIssue(report, "ACTION_RESULT_BRANCH_WEIGHT_INVALID");
                AssertIssue(report, "ACTION_RESULT_BRANCH_KEY_DUPLICATE");
            }
            finally
            {
                Destroy(card, action);
            }
        }

        [Test]
        public void ContentValidator_WarnsWhenActionsShareSameParticipantSignature()
        {
            GamePlayCardDefinition card = CreateCardDefinition("test.card");
            GamePlayActionDefinition firstAction = CreateActionDefinition("test.action.first", "test.card");
            GamePlayActionDefinition secondAction = CreateActionDefinition("test.action.second", "test.card");

            try
            {
                GamePlayContentValidationReport report = GamePlayContentValidator.ValidateContentAssets(
                    new GamePlayContentAsset[] { card, firstAction, secondAction });

                Assert.That(report.HasErrors, Is.False);
                AssertIssue(report, "ACTION_CONDITION_SIGNATURE_SHARED");
                Assert.DoesNotThrow(
                    () => GamePlayContentIndex.Build(
                        new GamePlayContentAsset[] { card, firstAction, secondAction }),
                    "同条件多行动是合法的多选项交互，只能提示作者确认，不能阻止内容索引建立。");
            }
            finally
            {
                Destroy(card, firstAction, secondAction);
            }
        }

        private static GamePlayCardDefinition CreateCardDefinition(string contentId)
        {
            GamePlayCardDefinition definition = ScriptableObject.CreateInstance<GamePlayCardDefinition>();
            JsonUtility.FromJsonOverwrite(
                $"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}}}}",
                definition);
            return definition;
        }

        private static GamePlayActionDefinition CreateActionDefinition(
            string actionId,
            string allowedContentId)
        {
            GamePlayActionDefinition definition = ScriptableObject.CreateInstance<GamePlayActionDefinition>();
            JsonUtility.FromJsonOverwrite(
                "{" +
                $"\"m_contentId\":{{\"m_value\":\"{actionId}\"}}," +
                "\"m_participationSlots\":[{" +
                "\"m_key\":\"participant\"," +
                "\"m_minimumParticipants\":2," +
                "\"m_maximumParticipants\":2," +
                $"\"m_allowedContentIds\":[{{\"m_value\":\"{allowedContentId}\"}}]" +
                "}]}",
                definition);
            return definition;
        }

        private static void SetResultIntents(
            GamePlayActionDefinition action,
            params GamePlayActionResultIntent[] intents)
        {
            var serializedAction = new SerializedObject(action);
            SerializedProperty intentsProperty = serializedAction.FindProperty("m_resultIntents");
            intentsProperty.arraySize = intents.Length;
            for (int i = 0; i < intents.Length; i++)
            {
                intentsProperty.GetArrayElementAtIndex(i).managedReferenceValue = intents[i];
            }

            serializedAction.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBranches(
            GamePlayActionDefinition action,
            params BranchSeed[] branches)
        {
            var serializedAction = new SerializedObject(action);
            SerializedProperty branchesProperty = serializedAction.FindProperty("m_resultBranches");
            branchesProperty.arraySize = branches.Length;
            for (int i = 0; i < branches.Length; i++)
            {
                SerializedProperty branchProperty = branchesProperty.GetArrayElementAtIndex(i);
                branchProperty.FindPropertyRelative("m_key").stringValue = branches[i].Key;
                branchProperty.FindPropertyRelative("m_weight").intValue = branches[i].Weight;
            }

            serializedAction.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TabletopCardRemoveResultIntent CreateRemoveIntent(string slotKey)
        {
            var intent = new TabletopCardRemoveResultIntent();
            JsonUtility.FromJsonOverwrite($"{{\"m_slotKey\":\"{slotKey}\"}}", intent);
            return intent;
        }

        private static TabletopCardCreateResultIntent CreateProductIntent(
            string contentId,
            int count,
            string anchorSlotKey)
        {
            var intent = new TabletopCardCreateResultIntent();
            JsonUtility.FromJsonOverwrite(
                "{" +
                $"\"m_contentId\":{{\"m_value\":\"{contentId}\"}}," +
                $"\"m_count\":{count}," +
                $"\"m_anchorSlotKey\":\"{anchorSlotKey}\"" +
                "}",
                intent);
            return intent;
        }

        private static void AssertIssue(GamePlayContentValidationReport report, string code)
        {
            Assert.That(
                report.Issues.Any(issue => issue.Code == code),
                Is.True,
                $"校验报告缺少问题码：{code}");
        }

        private static void Destroy(params UnityEngine.Object[] objects)
        {
            foreach (UnityEngine.Object obj in objects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }

        private readonly struct BranchSeed
        {
            internal BranchSeed(string key, int weight)
            {
                Key = key;
                Weight = weight;
            }

            internal string Key { get; }
            internal int Weight { get; }
        }
    }
}
