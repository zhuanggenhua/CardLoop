using System;
using NUnit.Framework;
using UnityEngine;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证牌桌释放事实只产生可选择的行动候选，不自动修改卡牌状态或执行行动。
    /// </summary>
    public sealed class TabletopCardActionCandidateEditModeTests
    {
        [Test]
        public void FindCandidates_ReturnsZeroOneOrManyAndRequiresExplicitSelection()
        {
            CardDefinition cardDefinition = CreateCardDefinition("test.card");
            ActionDefinition firstAction = CreateActionDefinition("test.action.first", "test.card");
            ActionDefinition secondAction = CreateActionDefinition("test.action.second", "test.card");
            ActionDefinition blockedAction = CreateActionDefinition("test.action.blocked", "test.other-card");

            try
            {
                ContentIndex contentIndex = ContentIndex.Build(new[] { cardDefinition });
                var state = new TabletopCardState();
                TabletopCard source = state.CreateCard("test.card", Vector2.zero);
                TabletopCard target = state.CreateCard("test.card", Vector2.one);
                var completeIntent = new TabletopCardPointerReleaseIntent(
                    source.Id,
                    Vector2.zero,
                    Vector2.one,
                    isDrag: true,
                    target.Id);
                var partialIntent = new TabletopCardPointerReleaseIntent(
                    source.Id,
                    Vector2.zero,
                    Vector2.right,
                    isDrag: true);

                TabletopCardActionCandidate[] zero = TabletopCardActionCandidateResolver.FindCandidates(
                    completeIntent,
                    state,
                    contentIndex,
                    new[] { blockedAction });
                TabletopCardActionCandidate[] one = TabletopCardActionCandidateResolver.FindCandidates(
                    completeIntent,
                    state,
                    contentIndex,
                    new[] { firstAction });
                TabletopCardActionCandidate[] many = TabletopCardActionCandidateResolver.FindCandidates(
                    completeIntent,
                    state,
                    contentIndex,
                    new[] { firstAction, secondAction });
                TabletopCardActionCandidate[] partial = TabletopCardActionCandidateResolver.FindCandidates(
                    partialIntent,
                    state,
                    contentIndex,
                    new[] { firstAction });

                Assert.That(zero, Is.Empty);
                Assert.That(one, Has.Length.EqualTo(1));
                Assert.That(one[0].Action, Is.SameAs(firstAction));
                Assert.That(one[0].IsReady, Is.True);
                Assert.That(one[0].MissingParticipantCount, Is.Zero);
                Assert.That(one[0].Bindings, Has.Count.EqualTo(1));
                Assert.That(one[0].Bindings[0].Slot.Key, Is.EqualTo("participant"));
                CollectionAssert.AreEqual(
                    new[] { source.Id, target.Id },
                    one[0].Bindings[0].CardIds);

                Assert.That(many, Has.Length.EqualTo(2));
                Assert.That(many[0].Action, Is.SameAs(firstAction));
                Assert.That(many[1].Action, Is.SameAs(secondAction));

                Assert.That(partial, Has.Length.EqualTo(1));
                Assert.That(partial[0].IsReady, Is.False);
                Assert.That(partial[0].MissingParticipantCount, Is.EqualTo(1));
                CollectionAssert.AreEqual(new[] { source.Id }, partial[0].Bindings[0].CardIds);

                Assert.That(
                    TabletopCardActionCandidateSelector.TrySelect(
                        many,
                        secondAction.ContentId,
                        out TabletopCardActionCandidate selected),
                    Is.True);
                Assert.That(selected.Action, Is.SameAs(secondAction));
                Assert.That(
                    TabletopCardActionCandidateSelector.TrySelect(
                        many,
                        blockedAction.ContentId,
                        out _),
                    Is.False,
                    "玩家只能选择本次查询实际返回的候选，不能用任意行动 ID 绕过条件。");

                Assert.That(state.StackCount, Is.EqualTo(2), "候选查询和选择不能自动合堆。");
                Assert.That(state.GetStackContaining(source.Id), Is.Not.SameAs(state.GetStackContaining(target.Id)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cardDefinition);
                UnityEngine.Object.DestroyImmediate(firstAction);
                UnityEngine.Object.DestroyImmediate(secondAction);
                UnityEngine.Object.DestroyImmediate(blockedAction);
            }
        }

        [Test]
        public void FindCandidates_UsesPointerOrderAsTheDeterministicTieBreakForEquivalentSlots()
        {
            CardDefinition cardDefinition = CreateCardDefinition("test.card");
            ActionDefinition action = CreateTwoSlotActionDefinition("test.action.directional", "test.card");

            try
            {
                ContentIndex contentIndex = ContentIndex.Build(new[] { cardDefinition });
                var state = new TabletopCardState();
                TabletopCard source = state.CreateCard("test.card", Vector2.zero);
                TabletopCard target = state.CreateCard("test.card", Vector2.one);
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
                    new[] { action, action });

                Assert.That(candidates, Has.Length.EqualTo(1), "重复提供同一行动不能生成重复按钮。");
                Assert.That(candidates[0].IsReady, Is.True);
                Assert.That(candidates[0].Bindings.Count, Is.EqualTo(2));
                Assert.That(candidates[0].Bindings[0].Slot.Key, Is.EqualTo("initiator"));
                CollectionAssert.AreEqual(new[] { source.Id }, candidates[0].Bindings[0].CardIds);
                Assert.That(candidates[0].Bindings[1].Slot.Key, Is.EqualTo("target"));
                CollectionAssert.AreEqual(new[] { target.Id }, candidates[0].Bindings[1].CardIds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cardDefinition);
                UnityEngine.Object.DestroyImmediate(action);
            }
        }

        private static CardDefinition CreateCardDefinition(string contentId)
        {
            CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
            JsonUtility.FromJsonOverwrite(
                $"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}}}}",
                definition);
            return definition;
        }

        private static ActionDefinition CreateActionDefinition(
            string actionId,
            string allowedContentId)
        {
            ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
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

        private static ActionDefinition CreateTwoSlotActionDefinition(
            string actionId,
            string allowedContentId)
        {
            ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
            string allowed = $"\"m_allowedContentIds\":[{{\"m_value\":\"{allowedContentId}\"}}]";
            JsonUtility.FromJsonOverwrite(
                "{" +
                $"\"m_contentId\":{{\"m_value\":\"{actionId}\"}}," +
                "\"m_participationSlots\":[{" +
                "\"m_key\":\"initiator\"," +
                "\"m_minimumParticipants\":1," +
                "\"m_maximumParticipants\":1," +
                allowed +
                "},{" +
                "\"m_key\":\"target\"," +
                "\"m_minimumParticipants\":1," +
                "\"m_maximumParticipants\":1," +
                allowed +
                "}]}",
                definition);
            return definition;
        }
    }
}
