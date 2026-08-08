using System;
using GameCore;
using UnityEngine;
using YokiFrame;

namespace Gameplay.Scenarios
{
    /// <summary>
    /// 持有当前活动剧本已确认的世界回合编号，并发布回合确认事实。
    /// 它不解释一天、阶段、遭遇、行动或战斗；这些职责后续由各自系统消费同一事实。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioTurnSystem : AGameSystem
    {
        private bool m_isRunning;

        /// <summary>当前活动剧本已经确认的世界回合数；剧本开始和结束都会重置。</summary>
        public int ConfirmedTurnIndex { get; private set; }

        // GameManager 会先收集系统，再进入正式生命周期；启动前禁用组件，避免场景脚本提前确认回合。
        private void Awake()
        {
            enabled = false;
        }

        /// <summary>进入正式运行阶段后允许确认世界回合。</summary>
        public override void OnSystemStart()
        {
            if (m_isRunning)
            {
                return;
            }

            m_isRunning = true;
            enabled = true;
        }

        /// <summary>停止时关闭确认入口并清空活动剧本的回合事实。</summary>
        public override void OnSystemStop()
        {
            enabled = false;
            m_isRunning = false;
            ConfirmedTurnIndex = 0;
        }

        /// <summary>结束当前单局生命周期时清空回合事实，避免进程重启沿用上一局编号。</summary>
        public override void OnSystemShutdown()
        {
            ConfirmedTurnIndex = 0;
        }

        /// <summary>由剧本父级在开始或结束剧本时清空回合编号。</summary>
        internal void ResetConfirmedTurns()
        {
            RequireRunningSystem();
            ConfirmedTurnIndex = 0;
        }

        /// <summary>
        /// 由剧本父级确认并发布下一次世界回合。
        /// 调用者只能确认事实，不能越过这个唯一写入口直接推进某个具体玩法系统。
        /// </summary>
        internal int ConfirmTurn()
        {
            RequireRunningSystem();
            ConfirmedTurnIndex = checked(ConfirmedTurnIndex + 1);
            EventKit.Type.Send(new ScenarioTurnConfirmedEvent(ConfirmedTurnIndex));
            return ConfirmedTurnIndex;
        }

        private void RequireRunningSystem()
        {
            if (!m_isRunning)
            {
                throw new InvalidOperationException("世界回合系统尚未启动，不能修改剧本回合。");
            }
        }
    }
}
