using System;
using System.Collections.Generic;
using GAS.Runtime;
using GameCore;
using MathematicsRandom = Unity.Mathematics.Random;
using UnityEngine;
using YokiFrame;

namespace GamePlay
{
    /// <summary>
    /// 持有并推进当前牌桌中的普通行动作业。
    /// 它默认消费已确认的回合推进；切换即时制后只把游戏秒数换算成同一份回合进度。
    /// 战斗始终由实时战斗系统推进，不进入本系统的模式切换。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabletopCardActionSystem : AGameSystem
    {
        private static readonly Type[] WorldTurnStartupDependencies =
            { typeof(GamePlayWorldTurnSystem) };
        private readonly List<TabletopCardActionJob> m_activeJobs = new();
        private bool m_isRunning;
        private GamePlayTurnTimingDefinition m_realTimeTiming;
        private TabletopCardState m_tabletopCardState;
        private GamePlayContentIndex m_contentIndex;
        private Func<TabletopCardId, AbilitySystemCell> m_abilitySystemCellResolver;
        private MathematicsRandom m_authoritativeRandom;
        private bool m_hasAuthoritativeRandom;

        /// <summary>当前仍在运行或暂停的作业；完成和取消的作业不会保留在这里。</summary>
        public IReadOnlyList<TabletopCardActionJob> ActiveJobs => m_activeJobs;

        /// <summary>当前普通行动推进方式；系统每次启动默认回合制。</summary>
        public TabletopCardActionProgressionMode ProgressionMode { get; private set; } =
            TabletopCardActionProgressionMode.TurnBased;

        /// <summary>
        /// 普通行动只能消费世界回合系统已确认的事实；不把回合入口保留在行动系统上。
        /// </summary>
        public override IReadOnlyCollection<Type> StartupDependencies =>
            WorldTurnStartupDependencies;

        // GameManager 会先发现组件，再调用正式系统启动阶段；启动前禁用组件，避免提前推进作业。
        private void Awake()
        {
            enabled = false;
        }

        /// <summary>进入 GameManager 正式运行阶段，并把普通行动恢复为默认回合制。</summary>
        public override void OnSystemStart()
        {
            if (m_isRunning)
            {
                return;
            }

            m_isRunning = true;
            ProgressionMode = TabletopCardActionProgressionMode.TurnBased;
            m_realTimeTiming = null;
            m_tabletopCardState = null;
            m_contentIndex = null;
            m_abilitySystemCellResolver = null;
            m_hasAuthoritativeRandom = false;
            EventKit.Type.Register<GamePlayWorldTurnConfirmedEvent>(OnWorldTurnConfirmed);
            enabled = true;
        }

        /// <summary>停止系统时取消尚未结束的作业，并清空唯一活动集合。</summary>
        public override void OnSystemStop()
        {
            enabled = false;
            EventKit.Type.UnRegister<GamePlayWorldTurnConfirmedEvent>(OnWorldTurnConfirmed);
            m_isRunning = false;
            ProgressionMode = TabletopCardActionProgressionMode.TurnBased;
            m_realTimeTiming = null;
            m_tabletopCardState = null;
            m_contentIndex = null;
            m_abilitySystemCellResolver = null;
            m_hasAuthoritativeRandom = false;

            for (int i = m_activeJobs.Count - 1; i >= 0; i--)
            {
                m_activeJobs[i].Cancel(TabletopCardActionCancellationReason.SystemStopped);
            }

            m_activeJobs.Clear();
        }

        /// <summary>
        /// 从可同步 / 可保存的行动请求启动作业。请求必须由当前牌桌状态和内容索引重新解析，
        /// 不能信任客户端或旧 UI 持有的候选对象引用。
        /// </summary>
        public TabletopCardActionJob StartAction(TabletopCardActionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return StartValidatedCandidate(CreateCandidateFromRequest(request));
        }

        /// <summary>
        /// 导出当前活动作业快照，供存档、断线恢复或调试系统读取。
        /// 本入口不提供恢复能力，避免在正式存档 owner 出现前提前写入第二套状态。
        /// </summary>
        public TabletopCardActionJobSnapshot[] CreateActiveJobSnapshots()
        {
            var snapshots = new TabletopCardActionJobSnapshot[m_activeJobs.Count];
            for (int i = 0; i < m_activeJobs.Count; i++)
            {
                snapshots[i] = m_activeJobs[i].CreateSnapshot();
            }

            return snapshots;
        }

        /// <summary>
        /// 绑定当前牌桌状态和内容索引，供行动开始、推进时复核参与条件，并在完成时提交结果。
        /// 绑定只允许在没有活动作业时更换，避免一条作业跨两个牌桌行动状态运行和结算。
        /// </summary>
        public void BindTabletopActionState(
            TabletopCardState state,
            GamePlayContentIndex contentIndex)
        {
            BindTabletopActionStateInternal(state, contentIndex, abilitySystemCellResolver: null);
        }

        /// <summary>
        /// 绑定需要角色当前 GAS 标签参与复核的牌桌行动状态。
        /// 只有角色运行状态系统应使用本入口；纯物品和静态标签行动使用无 GAS 参数的绑定入口。
        /// </summary>
        public void BindTabletopActionStateWithAbilitySystem(
            TabletopCardState state,
            GamePlayContentIndex contentIndex,
            Func<TabletopCardId, AbilitySystemCell> abilitySystemCellResolver)
        {
            if (abilitySystemCellResolver == null)
            {
                throw new ArgumentNullException(nameof(abilitySystemCellResolver));
            }

            BindTabletopActionStateInternal(state, contentIndex, abilitySystemCellResolver);
        }

        private void BindTabletopActionStateInternal(
            TabletopCardState state,
            GamePlayContentIndex contentIndex,
            Func<TabletopCardId, AbilitySystemCell> abilitySystemCellResolver)
        {
            RequireRunningSystem();
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (contentIndex == null) throw new ArgumentNullException(nameof(contentIndex));
            if (m_activeJobs.Count > 0)
                throw new InvalidOperationException("存在活动行动作业时不能更换牌桌行动状态。");

            m_tabletopCardState = state;
            m_contentIndex = contentIndex;
            m_abilitySystemCellResolver = abilitySystemCellResolver;
        }

        /// <summary>
        /// 由单局权威 owner 注入本次行动链的确定性随机种子。
        /// 同一系统只初始化一次，避免中途重置随机序列破坏回放和联机裁决。
        /// </summary>
        public void InitializeAuthoritativeRandom(uint seed)
        {
            RequireRunningSystem();
            if (seed == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seed), "权威随机种子不能为 0。");
            }

            if (m_hasAuthoritativeRandom)
            {
                throw new InvalidOperationException("牌桌行动系统的权威随机流已经初始化，不能在同一局中重置。");
            }

            if (m_activeJobs.Count > 0)
            {
                throw new InvalidOperationException("存在活动行动作业时不能初始化权威随机流。");
            }

            m_authoritativeRandom = new MathematicsRandom(seed);
            m_hasAuthoritativeRandom = true;
        }

        /// <summary>
        /// 把普通行动切换为即时推进。行动作者数据和现有作业进度仍保持回合单位，
        /// 每帧只使用所选回合规则换算增量。
        /// </summary>
        public void UseRealTimeProgression(GamePlayTurnTimingDefinition timingDefinition)
        {
            RequireRunningSystem();
            ValidateTimingDefinition(timingDefinition);
            m_realTimeTiming = timingDefinition;
            ProgressionMode = TabletopCardActionProgressionMode.RealTime;
        }

        /// <summary>把普通行动恢复为回合推进；现有作业的回合进度不会改变。</summary>
        public void UseTurnBasedProgression()
        {
            RequireRunningSystem();
            ProgressionMode = TabletopCardActionProgressionMode.TurnBased;
            m_realTimeTiming = null;
        }

        /// <summary>暂停一个由本系统持有的运行中作业。</summary>
        public void PauseAction(TabletopCardActionJob job)
        {
            RequireActiveJob(job);
            job.Pause();
        }

        /// <summary>恢复一个由本系统持有的暂停作业。</summary>
        public void ResumeAction(TabletopCardActionJob job)
        {
            RequireActiveJob(job);
            job.Resume();
        }

        /// <summary>取消一个由本系统持有的运行中或暂停作业。</summary>
        public void CancelAction(TabletopCardActionJob job)
        {
            RequireActiveJob(job);
            job.Cancel(TabletopCardActionCancellationReason.Requested);
            m_activeJobs.Remove(job);
        }

        private void Update()
        {
            if (ProgressionMode != TabletopCardActionProgressionMode.RealTime)
            {
                return;
            }

            float secondsPerTurn = ValidateTimingDefinition(m_realTimeTiming);
            // 使用缩放后的游戏时间，现有 GameStateSystem 的全局暂停和倍速才能自然作用于普通行动。
            float deltaSeconds = Time.deltaTime;
            if (deltaSeconds == 0f)
            {
                return;
            }

            AdvanceActiveJobs(deltaSeconds / secondsPerTurn);
        }

        /// <summary>
        /// 直接消费世界回合系统发布的确认事实。
        /// 普通行动切到即时制后仍保留同一世界回合事实，但自身只由游戏秒数换算推进。
        /// </summary>
        private void OnWorldTurnConfirmed(GamePlayWorldTurnConfirmedEvent _)
        {
            if (ProgressionMode == TabletopCardActionProgressionMode.TurnBased)
            {
                AdvanceActiveJobs(1f);
            }
        }

        private void AdvanceActiveJobs(float turnUnits)
        {
            for (int i = m_activeJobs.Count - 1; i >= 0; i--)
            {
                TabletopCardActionJob job = m_activeJobs[i];
                if (!AreJobParticipantsValid(job))
                {
                    job.Cancel(TabletopCardActionCancellationReason.ParticipantInvalidated);
                    m_activeJobs.RemoveAt(i);
                    continue;
                }

                job.Advance(turnUnits);
                if (job.State == TabletopCardActionJobState.Completed)
                {
                    m_activeJobs.RemoveAt(i);
                    CommitCompletedJob(job);
                }
            }
        }

        private void CommitCompletedJob(TabletopCardActionJob job)
        {
            if (!job.RequiresResultSettlement)
            {
                return;
            }

            if (m_tabletopCardState == null || m_contentIndex == null)
                throw new InvalidOperationException($"行动 {job.ActionId} 完成时缺少牌桌结果结算状态。");
            if (!m_contentIndex.TryGet(job.ActionId, out GamePlayActionDefinition action))
                throw new InvalidOperationException($"行动 {job.ActionId} 完成时无法从当前内容索引解析作者源。");

            TabletopCardActionResultSettlement.Commit(job, action, m_tabletopCardState, m_contentIndex);
        }

        private string SelectResultBranch(GamePlayActionDefinition action)
        {
            if (action.ResultBranches.Count == 0)
            {
                return string.Empty;
            }

            if (!m_hasAuthoritativeRandom)
            {
                throw new InvalidOperationException(
                    $"行动 {action.ContentId} 声明了随机结果分支，但牌桌行动系统尚未初始化权威随机流。");
            }

            uint totalWeight = 0;
            for (int i = 0; i < action.ResultBranches.Count; i++)
            {
                GamePlayActionResultBranch branch = action.ResultBranches[i];
                if (branch == null)
                {
                    throw new InvalidOperationException($"行动 {action.ContentId} 包含空的随机结果分支。");
                }

                if (string.IsNullOrWhiteSpace(branch.Key))
                {
                    throw new InvalidOperationException($"行动 {action.ContentId} 的随机结果分支缺少分支键。");
                }

                if (branch.Weight <= 0)
                {
                    throw new InvalidOperationException(
                        $"行动 {action.ContentId} 的随机结果分支 {branch.Key} 权重必须大于 0，当前值为 {branch.Weight}。");
                }

                for (int previousIndex = 0; previousIndex < i; previousIndex++)
                {
                    GamePlayActionResultBranch previousBranch = action.ResultBranches[previousIndex];
                    if (previousBranch != null &&
                        string.Equals(previousBranch.Key, branch.Key, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"行动 {action.ContentId} 的随机结果分支键重复：{branch.Key}。");
                    }
                }

                totalWeight = checked(totalWeight + (uint)branch.Weight);
            }

            uint roll = m_authoritativeRandom.NextUInt(totalWeight);
            for (int i = 0; i < action.ResultBranches.Count; i++)
            {
                GamePlayActionResultBranch branch = action.ResultBranches[i];
                uint branchWeight = (uint)branch.Weight;
                if (roll < branchWeight)
                {
                    return branch.Key;
                }

                roll -= branchWeight;
            }

            throw new InvalidOperationException($"行动 {action.ContentId} 的权威随机结果没有命中任何分支。");
        }

        private void RequireRunningSystem()
        {
            if (!m_isRunning)
            {
                throw new InvalidOperationException("牌桌行动系统尚未启动，不能改变行动推进状态。");
            }
        }

        private static float ValidateTimingDefinition(GamePlayTurnTimingDefinition timingDefinition)
        {
            if (timingDefinition == null)
            {
                throw new ArgumentNullException(nameof(timingDefinition));
            }

            float secondsPerTurn = timingDefinition.SecondsPerTurn;
            if (!float.IsFinite(secondsPerTurn) || secondsPerTurn <= 0f)
            {
                throw new InvalidOperationException(
                    $"普通行动时间换算 {timingDefinition.ContentId} 的每回合秒数必须是大于 0 的有限值，当前值为 {secondsPerTurn}。");
            }

            return secondsPerTurn;
        }

        private void RequireActiveJob(TabletopCardActionJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (!m_activeJobs.Contains(job))
            {
                throw new InvalidOperationException(
                    $"行动作业 {job.ActionId} 不属于当前牌桌行动系统的活动集合。");
            }
        }

        private bool AreJobParticipantsValid(TabletopCardActionJob job)
        {
            if (!m_contentIndex.TryGet(job.ActionId, out GamePlayActionDefinition action))
            {
                throw new InvalidOperationException($"行动作业 {job.ActionId} 无法从当前内容索引解析作者源。");
            }

            return ValidateBindings(action, job.Bindings);
        }

        /// <summary>
        /// 候选只能在它引用的行动作者源、参与卡和动态标签仍属于当前正式状态时开始。
        /// 查询后的正常状态变化要求调用方重新查询；内部结构不一致则直接报错。
        /// </summary>
        private void ValidateCandidateBindings(
            TabletopCardActionCandidate selectedCandidate,
            bool requireFreshCandidate)
        {
            if (!m_contentIndex.TryGet(
                    selectedCandidate.Action.ContentId,
                    out GamePlayActionDefinition currentAction))
            {
                throw new InvalidOperationException(
                    $"行动 {selectedCandidate.Action.ContentId} 不在当前内容索引中。请重新查询行动候选。");
            }

            if (requireFreshCandidate && !ReferenceEquals(currentAction, selectedCandidate.Action))
            {
                throw new InvalidOperationException(
                    $"行动 {selectedCandidate.Action.ContentId} 的作者源已变化。请重新查询行动候选。");
            }

            if (!ValidateBindings(currentAction, selectedCandidate.Bindings))
            {
                throw new InvalidOperationException(
                    $"行动 {selectedCandidate.Action.ContentId} 的参与对象已变化或不再满足条件。请重新查询行动候选。");
            }
        }

        private TabletopCardActionCandidate CreateCandidateFromRequest(TabletopCardActionRequest request)
        {
            if (!m_isRunning)
            {
                throw new InvalidOperationException("牌桌行动系统尚未启动，不能解析行动请求。");
            }

            if (m_tabletopCardState == null || m_contentIndex == null)
            {
                throw new InvalidOperationException("牌桌行动系统尚未绑定牌桌状态和内容索引，不能解析行动请求。");
            }

            if (!m_contentIndex.TryGet(request.ActionId, out GamePlayActionDefinition action))
            {
                throw new InvalidOperationException($"行动请求引用的行动 {request.ActionId} 不在当前内容索引中。");
            }

            var requestBindings = new Dictionary<string, TabletopCardActionRequestBinding>(StringComparer.Ordinal);
            var usedCards = new HashSet<TabletopCardId>();
            for (int i = 0; i < request.Bindings.Count; i++)
            {
                TabletopCardActionRequestBinding requestBinding = request.Bindings[i];
                if (requestBindings.ContainsKey(requestBinding.SlotKey))
                {
                    throw new InvalidOperationException($"行动请求 {request.ActionId} 重复提交槽位 {requestBinding.SlotKey}。");
                }

                requestBindings.Add(requestBinding.SlotKey, requestBinding);
                for (int cardIndex = 0; cardIndex < requestBinding.CardIds.Count; cardIndex++)
                {
                    TabletopCardId cardId = requestBinding.CardIds[cardIndex];
                    if (!cardId.IsValid)
                    {
                        throw new InvalidOperationException($"行动请求 {request.ActionId} 的槽位 {requestBinding.SlotKey} 包含无效卡牌 ID。");
                    }

                    if (!usedCards.Add(cardId))
                    {
                        throw new InvalidOperationException($"行动请求 {request.ActionId} 重复绑定牌桌卡牌 {cardId}。");
                    }
                }
            }

            var bindings = new List<TabletopCardActionSlotBinding>(action.ParticipationSlots.Count);
            for (int slotIndex = 0; slotIndex < action.ParticipationSlots.Count; slotIndex++)
            {
                GamePlayActionSlotDefinition slot = action.ParticipationSlots[slotIndex];
                if (!requestBindings.Remove(slot.Key, out TabletopCardActionRequestBinding requestBinding))
                {
                    throw new InvalidOperationException($"行动请求 {request.ActionId} 缺少参与槽位 {slot.Key}。");
                }

                bindings.Add(new TabletopCardActionSlotBinding(slot, requestBinding.CardIds));
            }

            if (requestBindings.Count > 0)
            {
                foreach (string unknownSlotKey in requestBindings.Keys)
                {
                    throw new InvalidOperationException($"行动请求 {request.ActionId} 包含当前作者源不存在的槽位 {unknownSlotKey}。");
                }
            }

            int missingParticipantCount = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                TabletopCardActionSlotBinding binding = bindings[i];
                missingParticipantCount += Math.Max(0, binding.Slot.MinimumParticipants - binding.CardIds.Count);
            }

            return new TabletopCardActionCandidate(action, bindings, missingParticipantCount);
        }

        private TabletopCardActionJob StartValidatedCandidate(TabletopCardActionCandidate selectedCandidate)
        {
            if (!selectedCandidate.IsReady)
            {
                throw new InvalidOperationException(
                    $"行动 {selectedCandidate.Action.ContentId} 仍缺少 {selectedCandidate.MissingParticipantCount} 个参与对象，不能开始作业。");
            }

            int turnCost = selectedCandidate.Action.TurnCost;
            if (turnCost < 0)
            {
                throw new InvalidOperationException(
                    $"行动 {selectedCandidate.Action.ContentId} 的回合消耗必须大于或等于 0，当前值为 {turnCost}。");
            }

            ValidateCandidateBindings(selectedCandidate, requireFreshCandidate: true);

            string resultBranchKey = SelectResultBranch(selectedCandidate.Action);
            var job = new TabletopCardActionJob(selectedCandidate, turnCost, resultBranchKey);
            if (job.State == TabletopCardActionJobState.Running)
            {
                m_activeJobs.Add(job);
            }
            else
            {
                CommitCompletedJob(job);
            }

            return job;
        }

        private bool ValidateBindings(
            GamePlayActionDefinition action,
            IReadOnlyList<TabletopCardActionSlotBinding> bindings)
        {
            if (bindings.Count != action.ParticipationSlots.Count)
            {
                throw new InvalidOperationException(
                    $"行动 {action.ContentId} 的候选绑定数量与作者槽位数量不一致。");
            }

            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                TabletopCardActionSlotBinding binding = bindings[bindingIndex];
                GamePlayActionSlotDefinition currentSlot = action.ParticipationSlots[bindingIndex];
                if (!ReferenceEquals(binding.Slot, currentSlot))
                {
                    throw new InvalidOperationException(
                        $"行动 {action.ContentId} 的候选槽位 {binding.Slot.Key} 已不是当前作者槽位。请重新查询行动候选。");
                }

                if (!GamePlayActionParticipationEvaluator.IsParticipantCountSatisfied(
                        currentSlot,
                        binding.CardIds.Count))
                {
                    return false;
                }

                for (int cardIndex = 0; cardIndex < binding.CardIds.Count; cardIndex++)
                {
                    TabletopCardId cardId = binding.CardIds[cardIndex];
                    if (!m_tabletopCardState.TryGetCard(cardId, out TabletopCard card) ||
                        !m_contentIndex.TryGet(card.ContentId, out GamePlayContentAsset contentAsset))
                    {
                        return false;
                    }

                    AbilitySystemCell abilitySystemCell = m_abilitySystemCellResolver?.Invoke(cardId);
                    if (!GamePlayActionParticipationEvaluator.MatchesParticipant(
                            currentSlot,
                            contentAsset,
                            abilitySystemCell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
