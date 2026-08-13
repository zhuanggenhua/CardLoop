using System;
using GameCore;
using Gameplay.Tabletop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Gameplay.Scenarios
{
    /// <summary>
    /// 打开剧本回合 HUD 所需的唯一运行对象；面板不保存回合真相。
    /// </summary>
    public sealed class ScenarioTurnPanelData : IUIData
    {
        public ScenarioDirector Director { get; }

        public ScenarioTurnPanelData(ScenarioDirector director)
        {
            Director = director ?? throw new ArgumentNullException(nameof(director));
        }
    }

    /// <summary>
    /// 当前剧本的常驻回合 HUD。
    /// 它只显示活动单局已有的日程，并把玩家确认交给剧本导演的唯一正式入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioTurnPanel : UIPanel
    {
        [Header("面板组件")]
        [SerializeField]
        [Tooltip("显示当前剧本的日程信息。")]
        private TMP_Text m_turnLabel;

		[SerializeField]
		[Tooltip("显示当前游戏日的回合或即时进度。")]
		private Image m_dayProgressFill;

		[SerializeField]
		[Tooltip("显示当前推进模式下的主要操作。")]
		private TMP_Text m_confirmTurnLabel;

        [SerializeField]
        [Tooltip("确认当前剧本下一回合的玩家按钮。")]
        private Button m_confirmTurnButton;

        private ScenarioDirector m_director;
        private bool m_isSubscribed;

        /// <summary>当前显示的已确认总回合数；它只是 UI 缓存。</summary>
        public int DisplayedTurnIndex { get; private set; }

        /// <summary>当前显示的剧本日；它只是 UI 缓存。</summary>
        public int DisplayedDay { get; private set; }

		public int DisplayedTurnsInCurrentDay { get; private set; }

		public int DisplayedTurnsPerDay { get; private set; }

		public float DisplayedDayProgress { get; private set; }

		public bool CanConfirmTurn { get; private set; }

        protected override void OnInit(IUIData data = null)
        {
            if (m_turnLabel == null || m_dayProgressFill == null ||
				m_confirmTurnButton == null || m_confirmTurnLabel == null)
            {
                throw new InvalidOperationException("剧本回合 HUD 预制体缺少必要 UI 引用。");
            }

            m_confirmTurnButton.onClick.AddListener(ConfirmTurn);
        }

        protected override void OnOpen(IUIData data = null)
        {
            if (m_director != null || m_isSubscribed)
            {
                throw new InvalidOperationException("剧本回合 HUD 尚未关闭，不能覆盖上一局剧本。");
            }
            if (data is not ScenarioTurnPanelData panelData)
            {
                throw new ArgumentException(
                    "剧本回合 HUD 必须使用 ScenarioTurnPanelData 打开。",
                    nameof(data));
            }

            m_director = panelData.Director;
            EventKit.Type.Register<ScenarioTurnConfirmedEvent>(OnScenarioTurnConfirmed);
            m_isSubscribed = true;
            Refresh();
        }

		private void Update()
		{
			if (m_director == null || !m_director.HasActiveScenario)
			{
				return;
			}
			ScenarioRun run = m_director.ActiveRun;
			bool canConfirmTurn = run.ProgressionMode == ActionProgressionMode.TurnBased;
			if (DisplayedTurnIndex != run.ConfirmedTurnIndex ||
				DisplayedDay != run.CurrentDay ||
				DisplayedTurnsInCurrentDay != run.ConfirmedTurnsInCurrentDay ||
				!Mathf.Approximately(DisplayedDayProgress, run.NormalizedDayProgress) ||
				CanConfirmTurn != canConfirmTurn)
			{
				Refresh();
			}
		}

        protected override void OnClose()
        {
            Unsubscribe();
            m_director = null;
        }

        protected override void ClearUIComponents()
        {
            if (m_confirmTurnButton != null)
            {
                m_confirmTurnButton.onClick.RemoveListener(ConfirmTurn);
            }

            Unsubscribe();
            m_director = null;
        }

        private void ConfirmTurn()
        {
            RequireActiveDirector().ConfirmTurn();
        }

        private void OnScenarioTurnConfirmed(ScenarioTurnConfirmedEvent confirmedEvent)
        {
            if (m_director == null ||
                !confirmedEvent.ScenarioId.Equals(m_director.ActiveScenarioId))
            {
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            ScenarioRun run = RequireActiveDirector().ActiveRun;
            DisplayedTurnIndex = run.ConfirmedTurnIndex;
            DisplayedDay = run.CurrentDay;
			DisplayedTurnsInCurrentDay = run.ConfirmedTurnsInCurrentDay;
			DisplayedTurnsPerDay = run.TurnsPerDay;
			DisplayedDayProgress = run.NormalizedDayProgress;
			CanConfirmTurn = run.ProgressionMode == ActionProgressionMode.TurnBased;
			m_turnLabel.text = $"第 {DisplayedDay} 天  {DisplayedTurnsInCurrentDay}/{DisplayedTurnsPerDay}";
			m_dayProgressFill.fillAmount = DisplayedDayProgress;
			m_confirmTurnButton.interactable = CanConfirmTurn;
			m_confirmTurnLabel.text = CanConfirmTurn ? "推进回合" : "即时推进中";
        }

        private ScenarioDirector RequireActiveDirector()
        {
            if (m_director == null || !m_director.HasActiveScenario)
            {
                throw new InvalidOperationException("当前没有活动剧本，不能显示或确认剧本回合。");
            }

            return m_director;
        }

        private void Unsubscribe()
        {
            if (!m_isSubscribed)
            {
                return;
            }

            EventKit.Type.UnRegister<ScenarioTurnConfirmedEvent>(OnScenarioTurnConfirmed);
            m_isSubscribed = false;
        }
    }
}
