using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 负责把牌桌卡牌结果意图一次性提交给 <see cref="TabletopCardState"/>。
    /// 先验证全部移除与生成计划，再写入状态，避免部分结果已经提交后才发现作者配置错误。
    /// </summary>
    internal static class TabletopCardActionResultSettlement
    {
        internal static void Commit(
            TabletopCardActionJob job,
            GamePlayActionDefinition action,
            TabletopCardState state,
            GamePlayContentIndex contentIndex)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (contentIndex == null) throw new ArgumentNullException(nameof(contentIndex));
            if (job.State != TabletopCardActionJobState.Completed)
                throw new InvalidOperationException($"行动 {job.ActionId} 尚未完成，不能提交结果。");
            if (!action.ContentId.Equals(job.ActionId))
                throw new InvalidOperationException($"行动结果的作者源 {action.ContentId} 与作业 {job.ActionId} 不一致。");

            var removals = new List<TabletopCardId>();
            var removalSet = new HashSet<TabletopCardId>();
            var creations = new List<CardCreationPlan>();
            int totalCreationCount = 0;

            for (int i = 0; i < action.ResultIntents.Count; i++)
            {
                AddIntentPlan(
                    job,
                    state,
                    contentIndex,
                    action.ResultIntents[i],
                    removals,
                    removalSet,
                    creations,
                    ref totalCreationCount);
            }

            if (action.ResultBranches.Count > 0)
            {
                GamePlayActionResultBranch branch = FindBranch(action, job.ResultBranchKey);
                for (int i = 0; i < branch.ResultIntents.Count; i++)
                {
                    AddIntentPlan(
                        job,
                        state,
                        contentIndex,
                        branch.ResultIntents[i],
                        removals,
                        removalSet,
                        creations,
                        ref totalCreationCount);
                }
            }

            state.EnsureCanCreateCards(totalCreationCount);
            for (int i = 0; i < removals.Count; i++)
            {
                state.RemoveCard(removals[i]);
            }

            for (int i = 0; i < creations.Count; i++)
            {
                CardCreationPlan plan = creations[i];
                for (int cardIndex = 0; cardIndex < plan.Count; cardIndex++)
                {
                    state.CreateCard(plan.ContentId, plan.Position);
                }
            }
        }

        private static void AddIntentPlan(
            TabletopCardActionJob job,
            TabletopCardState state,
            GamePlayContentIndex contentIndex,
            GamePlayActionResultIntent intent,
            List<TabletopCardId> removals,
            HashSet<TabletopCardId> removalSet,
            List<CardCreationPlan> creations,
            ref int totalCreationCount)
        {
            switch (intent)
            {
                case TabletopCardRemoveResultIntent removeIntent:
                    AddRemovalPlan(job, state, removeIntent, removals, removalSet);
                    break;
                case TabletopCardCreateResultIntent createIntent:
                    CardCreationPlan plan = CreateCreationPlan(job, state, contentIndex, createIntent);
                    totalCreationCount = checked(totalCreationCount + plan.Count);
                    creations.Add(plan);
                    break;
                case null:
                    throw new InvalidOperationException($"行动 {job.ActionId} 包含空结果意图。");
                default:
                    throw new InvalidOperationException(
                        $"行动 {job.ActionId} 的结果意图类型 {intent.GetType().FullName} 没有注册牌桌结算入口。");
            }
        }

        private static GamePlayActionResultBranch FindBranch(
            GamePlayActionDefinition action,
            string branchKey)
        {
            if (string.IsNullOrWhiteSpace(branchKey))
            {
                throw new InvalidOperationException($"行动 {action.ContentId} 缺少已选随机结果分支键。");
            }

            for (int i = 0; i < action.ResultBranches.Count; i++)
            {
                GamePlayActionResultBranch branch = action.ResultBranches[i];
                if (branch != null && string.Equals(branch.Key, branchKey, StringComparison.Ordinal))
                {
                    return branch;
                }
            }

            throw new InvalidOperationException(
                $"行动 {action.ContentId} 的作业记录了不存在的随机结果分支 {branchKey}。");
        }

        private static void AddRemovalPlan(
            TabletopCardActionJob job,
            TabletopCardState state,
            TabletopCardRemoveResultIntent intent,
            List<TabletopCardId> removals,
            HashSet<TabletopCardId> removalSet)
        {
            TabletopCardActionSlotBinding binding = FindBinding(job, intent.SlotKey);
            for (int i = 0; i < binding.CardIds.Count; i++)
            {
                TabletopCardId cardId = binding.CardIds[i];
                if (!state.TryGetCard(cardId, out _))
                    throw new InvalidOperationException($"行动 {job.ActionId} 的结果引用了不存在的牌桌卡牌 {cardId}。");
                if (!removalSet.Add(cardId))
                    throw new InvalidOperationException($"行动 {job.ActionId} 的结果重复移除牌桌卡牌 {cardId}。");

                removals.Add(cardId);
            }
        }

        private static CardCreationPlan CreateCreationPlan(
            TabletopCardActionJob job,
            TabletopCardState state,
            GamePlayContentIndex contentIndex,
            TabletopCardCreateResultIntent intent)
        {
            if (!intent.ContentId.IsValid || !contentIndex.TryGet(intent.ContentId, out _))
                throw new InvalidOperationException(
                    $"行动 {job.ActionId} 的产物内容 {intent.ContentId} 不在当前 GamePlay 内容索引中。");
            if (intent.Count <= 0)
                throw new InvalidOperationException($"行动 {job.ActionId} 的产物生成数量必须大于 0。");

            TabletopCardActionSlotBinding anchorBinding = FindBinding(job, intent.AnchorSlotKey);
            if (anchorBinding.CardIds.Count == 0)
                throw new InvalidOperationException(
                    $"行动 {job.ActionId} 的产物位置来源槽位 {intent.AnchorSlotKey} 没有绑定牌桌卡牌。");

            TabletopCardStack anchorStack = state.GetStackContaining(anchorBinding.CardIds[0]);
            return new CardCreationPlan(intent.ContentId, intent.Count, anchorStack.Position);
        }

        private static TabletopCardActionSlotBinding FindBinding(TabletopCardActionJob job, string slotKey)
        {
            if (string.IsNullOrWhiteSpace(slotKey))
                throw new InvalidOperationException($"行动 {job.ActionId} 的结果意图缺少参与槽位键。");

            for (int i = 0; i < job.Bindings.Count; i++)
            {
                TabletopCardActionSlotBinding binding = job.Bindings[i];
                if (binding.Slot.Key == slotKey)
                {
                    return binding;
                }
            }

            throw new InvalidOperationException($"行动 {job.ActionId} 的结果引用了不存在的参与槽位 {slotKey}。");
        }

        private readonly struct CardCreationPlan
        {
            internal CardCreationPlan(GamePlayContentId contentId, int count, Vector2 position)
            {
                ContentId = contentId;
                Count = count;
                Position = position;
            }

            internal GamePlayContentId ContentId { get; }
            internal int Count { get; }
            internal Vector2 Position { get; }
        }
    }
}
