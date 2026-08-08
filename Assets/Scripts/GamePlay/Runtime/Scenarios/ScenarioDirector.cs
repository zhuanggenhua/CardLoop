using System;
using System.Collections.Generic;
using GameCore;
using Gameplay.Content;
using Gameplay.Quests;
using UnityEngine;

namespace Gameplay.Scenarios
{
    /// <summary>
    /// 当前单局剧本的父级生命周期入口。
    /// 它只负责激活一个剧本身份并统一开始 / 结束已经存在的子模块，不解释任务条件、地图或世界规则。
    /// Director 表达流程编排职责；AGameSystem 基类已经表达系统注册身份，因此类型名不重复追加 System。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioDirector : AGameSystem
    {
        private static readonly Type[] SystemStartupDependencies =
            { typeof(QuestSystem), typeof(ScenarioTurnSystem) };

        [SerializeField, InspectorName("任务子系统"), Tooltip("由剧本导演统一开始和结束任务集合。必须引用同一 GameManager 层级下登记的 QuestSystem。")]
        private QuestSystem m_questSystem;

        [SerializeField, InspectorName("回合子系统"), Tooltip("由剧本父级统一开始、确认和结束世界回合。必须引用同一 GameManager 层级下登记的 ScenarioTurnSystem。")]
        private ScenarioTurnSystem m_turnSystem;
        private ContentId m_activeScenarioId;
        private bool m_isRunning;

        /// <summary>当前是否已有一个活动剧本。</summary>
        public bool HasActiveScenario => m_activeScenarioId.IsValid;

        /// <summary>当前活动剧本的唯一内容 ID；没有活动剧本时为无效 ID。</summary>
        public ContentId ActiveScenarioId => m_activeScenarioId;

        /// <summary>剧本开始前必须先启动任务和世界回合子模块。</summary>
        public override IReadOnlyCollection<Type> StartupDependencies =>
            SystemStartupDependencies;

        // GameManager 完成系统收集前不开放剧本入口。
        private void Awake()
        {
            enabled = false;
        }

        /// <summary>进入正式运行阶段前验证场景已经显式装配任务和世界回合子模块。</summary>
        public override void OnSystemStart()
        {
            if (m_isRunning)
            {
                return;
            }

            if (m_questSystem == null)
            {
                throw new InvalidOperationException(
                    "剧本系统缺少任务子系统引用，不能开始剧本生命周期。");
            }

            if (m_turnSystem == null)
            {
                throw new InvalidOperationException(
                    "剧本系统缺少回合子系统引用，不能开始剧本生命周期。");
            }

            m_isRunning = true;
            enabled = true;
        }

        /// <summary>停止运行阶段前先结束当前剧本及其任务集合。</summary>
        public override void OnSystemStop()
        {
            if (m_isRunning && HasActiveScenario)
            {
                EndActiveScenario();
            }

            enabled = false;
            m_isRunning = false;
            m_activeScenarioId = default;
        }

        /// <summary>关闭系统时清除活动剧本身份。</summary>
        public override void OnSystemShutdown()
        {
            m_activeScenarioId = default;
        }

        /// <summary>
        /// 通过正式内容索引开始一个剧本，并由本入口统一开始它声明的任务集合。
        /// 同一时间只能存在一个活动剧本；空任务列表不会创建空任务集合。
        /// </summary>
        public void StartScenario(ContentId scenarioId, ContentIndex contentIndex)
        {
            RequireRunningSystem();
            if (HasActiveScenario)
            {
                throw new InvalidOperationException(
                    $"剧本 {m_activeScenarioId} 仍在运行，不能同时开始剧本 {scenarioId}。");
            }

            if (contentIndex == null)
            {
                throw new ArgumentNullException(nameof(contentIndex));
            }

            if (!scenarioId.IsValid ||
                !contentIndex.TryGet(scenarioId, out ScenarioDefinition definition))
            {
                throw new InvalidOperationException(
                    $"内容 {scenarioId} 不存在或不是剧本定义。");
            }

            // 子模块会同步发布事实，先提交父级身份，再依次开始回合和任务，保证订阅者读到完整剧本上下文。
            m_activeScenarioId = scenarioId;
            try
            {
                m_turnSystem.ResetConfirmedTurns();
                if (definition.QuestIds.Count > 0)
                {
                    m_questSystem.StartQuestSet(definition.QuestIds, contentIndex);
                }
            }
            catch
            {
                if (m_questSystem.HasQuestSet)
                {
                    m_questSystem.EndQuestSet();
                }

                m_turnSystem.ResetConfirmedTurns();

                m_activeScenarioId = default;
                throw;
            }
        }

        /// <summary>结束当前活动剧本，并由同一父级入口结束它开始的任务集合。</summary>
        public void EndScenario()
        {
            RequireRunningSystem();
            if (!HasActiveScenario)
            {
                throw new InvalidOperationException("当前没有活动剧本，不能结束剧本。");
            }

            EndActiveScenario();
        }

        /// <summary>
        /// 确认当前活动剧本的下一次世界回合。
        /// 这是玩家、UI 和网络命令进入世界回合事实的唯一正式入口；行动系统只消费已确认事实。
        /// </summary>
        public int ConfirmTurn()
        {
            RequireRunningSystem();
            if (!HasActiveScenario)
            {
                throw new InvalidOperationException("当前没有活动剧本，不能确认世界回合。");
            }

            return m_turnSystem.ConfirmTurn();
        }

        private void EndActiveScenario()
        {
            if (m_questSystem.HasQuestSet)
            {
                m_questSystem.EndQuestSet();
            }

            m_turnSystem.ResetConfirmedTurns();

            m_activeScenarioId = default;
        }

        private void RequireRunningSystem()
        {
            if (!m_isRunning)
            {
                throw new InvalidOperationException("剧本系统尚未启动，不能开始或结束剧本。");
            }
        }
    }
}
