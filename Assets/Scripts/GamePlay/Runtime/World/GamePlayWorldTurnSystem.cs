using System;
using GameCore;
using UnityEngine;
using YokiFrame;

namespace GamePlay
{
    /// <summary>
    /// 持有当前单局已确认的世界回合编号，并发布回合确认事实。
    /// 它不解释一天、阶段、遭遇、行动或战斗；这些职责后续由各自系统消费同一事实。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePlayWorldTurnSystem : AGameSystem
    {
        private bool m_isRunning;

        /// <summary>当前单局已经确认的世界回合数；新单局由正式单局流程重置。</summary>
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

        /// <summary>停止时关闭确认入口，但保留本局回合事实，等待正式单局流程决定是否重置。</summary>
        public override void OnSystemStop()
        {
            enabled = false;
            m_isRunning = false;
        }

        /// <summary>结束当前单局生命周期时清空回合事实，避免进程重启沿用上一局编号。</summary>
        public override void OnSystemShutdown()
        {
            ConfirmedTurnIndex = 0;
        }

        /// <summary>
        /// 确认并发布下一次世界回合。
        /// 调用者只能确认事实，不能越过这个唯一写入口直接推进某个具体玩法系统。
        /// </summary>
        public int ConfirmTurn()
        {
            if (!m_isRunning)
            {
                throw new InvalidOperationException("世界回合系统尚未启动，不能确认回合。");
            }

            ConfirmedTurnIndex = checked(ConfirmedTurnIndex + 1);
            EventKit.Type.Send(new GamePlayWorldTurnConfirmedEvent(ConfirmedTurnIndex));
            return ConfirmedTurnIndex;
        }
    }
}
