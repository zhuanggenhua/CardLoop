using System;
using Cysharp.Threading.Tasks;
using GameCore;
using Gameplay.Content;
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
		[Tooltip("StackCraft HUD 当前时间推进图标。")]
		private Image m_paceImage;

		[SerializeField]
		[Tooltip("StackCraft HUD 时间推进速度图标：0=暂停，1=正常，2=加速。")]
		private Sprite[] m_paceIcons;

		[SerializeField]
		[Tooltip("StackCraft DayTimeUI 的显隐与输入射线控制组。")]
		private CanvasGroup m_dayTimeGroup;

		[SerializeField]
		[Tooltip("StackCraft CardStatsUI 的显隐与输入射线控制组。")]
		private CanvasGroup m_cardStatsGroup;

		[SerializeField]
		[Tooltip("StackCraft HUD 营养统计图标。")]
		private Image m_nutritionIcon;

		[SerializeField]
		[Tooltip("StackCraft HUD 营养统计数值。")]
		private TMP_Text m_nutritionLabel;

		[SerializeField]
		[Tooltip("StackCraft HUD 货币统计图标。")]
		private Image m_currencyIcon;

		[SerializeField]
		[Tooltip("StackCraft HUD 货币统计数值。")]
		private TMP_Text m_currencyLabel;

		[SerializeField]
		[Tooltip("StackCraft HUD 卡牌容量统计图标。")]
		private Image m_cardCountIcon;

		[SerializeField]
		[Tooltip("StackCraft HUD 卡牌容量统计数值。")]
		private TMP_Text m_cardCountLabel;

		[SerializeField]
		[Tooltip("显示当前推进模式下的主要操作。")]
		private TMP_Text m_confirmTurnLabel;

        [SerializeField]
        [Tooltip("确认当前剧本下一回合的玩家按钮。")]
        private Button m_confirmTurnButton;

		[SerializeField]
		[Tooltip("切换普通行动的回合制 / 即时制推进模式。战斗始终按真实秒数推进，不受此按钮影响。")]
		private Button m_progressionModeButton;

		[SerializeField]
		[Tooltip("显示普通行动推进模式按钮的当前操作。")]
		private TMP_Text m_progressionModeLabel;

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

		public bool CanSwitchProgressionMode { get; private set; }

		public ScenarioTimePace DisplayedTimePace { get; private set; }

		public ScenarioDayCyclePhase DisplayedDayCyclePhase { get; private set; }

		public int DisplayedExcessCardCount { get; private set; }

		public int DisplayedTotalFoodNutrition { get; private set; }

		public int DisplayedNutritionNeed { get; private set; }

		public int DisplayedCurrency { get; private set; }

		public int DisplayedCardsOwned { get; private set; }

		public int DisplayedCardLimit { get; private set; }

        protected override void OnInit(IUIData data = null)
        {
            if (m_turnLabel == null || m_dayProgressFill == null ||
				m_paceImage == null ||
				m_dayTimeGroup == null || m_cardStatsGroup == null ||
				m_nutritionIcon == null || m_nutritionLabel == null ||
				m_currencyIcon == null || m_currencyLabel == null ||
				m_cardCountIcon == null || m_cardCountLabel == null ||
				m_confirmTurnButton == null)
            {
                throw new InvalidOperationException("剧本回合 HUD 预制体缺少必要 UI 引用。");
            }
			if (m_paceIcons == null ||
				m_paceIcons.Length != 3 ||
				m_paceIcons[(int)ScenarioTimePace.Paused] == null ||
				m_paceIcons[(int)ScenarioTimePace.Normal] == null ||
				m_paceIcons[(int)ScenarioTimePace.Fast] == null)
			{
				throw new InvalidOperationException("剧本回合 HUD 预制体缺少 StackCraft 三档时间速度图标。");
			}

            m_confirmTurnButton.onClick.AddListener(ConfirmTurn);
			if (m_progressionModeButton != null)
			{
				m_progressionModeButton.onClick.AddListener(SwitchProgressionMode);
			}
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
			EventKit.Type.Register<ScenarioDayCycleChangedEvent>(OnScenarioDayCycleChanged);
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
			ScenarioTabletopStats stats = run.GetTabletopStats();
			bool canConfirmTurn = CanUsePrimaryAction(run);
			bool canSwitchProgressionMode = CanUseProgressionModeSwitch(run);
			ScenarioTimePace displayedTimePace = GetDisplayedTimePace(run);
			if (DisplayedTurnIndex != run.ConfirmedTurnIndex ||
				DisplayedDay != run.CurrentDay ||
				DisplayedTurnsInCurrentDay != run.ConfirmedTurnsInCurrentDay ||
				!Mathf.Approximately(DisplayedDayProgress, run.NormalizedDayProgress) ||
				DisplayedDayCyclePhase != run.DayCyclePhase ||
				DisplayedExcessCardCount != run.ExcessCardCount ||
				DisplayedTotalFoodNutrition != stats.TotalFoodNutrition ||
				DisplayedNutritionNeed != stats.NutritionNeed ||
				DisplayedCurrency != stats.Currency ||
				DisplayedCardsOwned != stats.CardsOwned ||
				DisplayedCardLimit != stats.CardLimit ||
				DisplayedTimePace != displayedTimePace ||
				CanConfirmTurn != canConfirmTurn ||
				CanSwitchProgressionMode != canSwitchProgressionMode)
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
			if (m_progressionModeButton != null)
			{
				m_progressionModeButton.onClick.RemoveListener(SwitchProgressionMode);
			}

            Unsubscribe();
            m_director = null;
        }

        private void ConfirmTurn()
        {
			ScenarioDirector director = RequireActiveDirector();
			if (director.ActiveRun.DayCyclePhase == ScenarioDayCyclePhase.GameOver)
			{
				director.GameOverAsync().Forget();
				return;
			}
			if (director.ActiveRun.DayCyclePhase != ScenarioDayCyclePhase.Inactive)
			{
				director.ContinueDayCycle();
				return;
			}
			if (director.ActiveRun.ProgressionMode == ActionProgressionMode.RealTime)
			{
				director.ActiveRun.CycleTimePace();
				Refresh();
				return;
			}
			director.ConfirmTurn();
        }

		private void SwitchProgressionMode()
		{
			ScenarioDirector director = RequireActiveDirector();
			ScenarioRun run = director.ActiveRun;
			if (run.DayCyclePhase != ScenarioDayCyclePhase.Inactive)
			{
				throw new InvalidOperationException("日终阶段正在等待玩家处理，不能切换普通行动推进模式。");
			}

			if (run.ProgressionMode == ActionProgressionMode.TurnBased)
			{
				run.UseRealTimeProgression();
			}
			else
			{
				run.UseTurnBasedProgression();
			}

			Refresh();
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

		private void OnScenarioDayCycleChanged(ScenarioDayCycleChangedEvent changedEvent)
		{
			if (m_director == null ||
				!changedEvent.ScenarioId.Equals(m_director.ActiveScenarioId))
			{
				return;
			}
			Refresh();
		}

        private void Refresh()
        {
            ScenarioRun run = RequireActiveDirector().ActiveRun;
			ScenarioTabletopStats stats = run.GetTabletopStats();
            DisplayedTurnIndex = run.ConfirmedTurnIndex;
            DisplayedDay = run.CurrentDay;
			DisplayedTurnsInCurrentDay = run.ConfirmedTurnsInCurrentDay;
			DisplayedTurnsPerDay = run.TurnsPerDay;
			DisplayedDayProgress = run.NormalizedDayProgress;
			DisplayedDayCyclePhase = run.DayCyclePhase;
			DisplayedExcessCardCount = run.ExcessCardCount;
			DisplayedTotalFoodNutrition = stats.TotalFoodNutrition;
			DisplayedNutritionNeed = stats.NutritionNeed;
			DisplayedCurrency = stats.Currency;
			DisplayedCardsOwned = stats.CardsOwned;
			DisplayedCardLimit = stats.CardLimit;
			DisplayedTimePace = GetDisplayedTimePace(run);
			CanConfirmTurn = CanUsePrimaryAction(run);
			CanSwitchProgressionMode = CanUseProgressionModeSwitch(run);
			m_turnLabel.text = GetTurnLabel(run);
			RefreshStatsLabels(stats);
			m_dayProgressFill.fillAmount = DisplayedDayProgress;
			m_paceImage.sprite = m_paceIcons[(int)DisplayedTimePace];
			bool isDayHudVisible = run.DayCyclePhase == ScenarioDayCyclePhase.Inactive;
			SetHudGroupVisible(m_dayTimeGroup, isDayHudVisible);
			SetHudGroupVisible(m_cardStatsGroup, isDayHudVisible);
			m_confirmTurnButton.interactable = CanUsePrimaryButton(run);
			if (m_confirmTurnLabel != null)
			{
				m_confirmTurnLabel.text = GetPrimaryActionLabel(run);
			}
			if (m_progressionModeButton != null)
			{
				m_progressionModeButton.interactable = CanSwitchProgressionMode;
			}
			if (m_progressionModeLabel != null)
			{
				m_progressionModeLabel.text = GetProgressionModeActionLabel(run);
			}
        }

		private static void SetHudGroupVisible(CanvasGroup group, bool isVisible)
		{
			group.alpha = isVisible ? 1f : 0f;
			group.blocksRaycasts = isVisible;
		}

		private static bool CanUsePrimaryAction(ScenarioRun run)
		{
			return run.DayCyclePhase != ScenarioDayCyclePhase.AwaitingExcessCardResolution &&
				run.DayCyclePhase == ScenarioDayCyclePhase.Inactive &&
				run.ProgressionMode == ActionProgressionMode.TurnBased;
		}

		private static bool CanUsePrimaryButton(ScenarioRun run)
		{
			return run.DayCyclePhase != ScenarioDayCyclePhase.AwaitingExcessCardResolution &&
				(run.DayCyclePhase != ScenarioDayCyclePhase.Inactive ||
				 run.ProgressionMode == ActionProgressionMode.TurnBased ||
				 run.ProgressionMode == ActionProgressionMode.RealTime);
		}

		private static bool CanUseProgressionModeSwitch(ScenarioRun run)
		{
			return run.DayCyclePhase == ScenarioDayCyclePhase.Inactive &&
				(run.ProgressionMode == ActionProgressionMode.TurnBased ||
				 run.CanReturnToTurnBasedProgression);
		}

		private void RefreshStatsLabels(ScenarioTabletopStats stats)
		{
			m_nutritionLabel.text = $"{stats.TotalFoodNutrition}/{stats.NutritionNeed}";
			m_currencyLabel.text = $"{stats.Currency}";
			m_cardCountLabel.text = $"{stats.CardsOwned}/{stats.CardLimit}";
		}

		private static string GetTurnLabel(ScenarioRun run)
		{
			if (run.DayCyclePhase == ScenarioDayCyclePhase.GameOver)
			{
				return "游戏结束";
			}

			if (run.DayCyclePhase == ScenarioDayCyclePhase.Inactive)
			{
				return $"第 {run.CurrentDay} 天";
			}
			return $"第 {run.CurrentDay} 天";
		}

		private static string GetPrimaryActionLabel(ScenarioRun run)
		{
			return run.DayCyclePhase switch
			{
				ScenarioDayCyclePhase.AwaitingFeedingConfirmation => "分配食物",
				ScenarioDayCyclePhase.AwaitingExcessCardResolution => $"处理超限 {run.ExcessCardCount} 张",
				ScenarioDayCyclePhase.AwaitingNewDayConfirmation => $"开始第 {run.CurrentDay + 1} 天",
				ScenarioDayCyclePhase.GameOver => "返回标题",
				_ => run.ProgressionMode == ActionProgressionMode.TurnBased
					? "推进回合"
					: GetTimePaceActionLabel(run.TimePace)
			};
		}

		private static ScenarioTimePace GetDisplayedTimePace(ScenarioRun run)
		{
			return run.DayCyclePhase == ScenarioDayCyclePhase.Inactive &&
				run.ProgressionMode == ActionProgressionMode.RealTime
					? run.TimePace
					: ScenarioTimePace.Paused;
		}

		private static string GetTimePaceActionLabel(ScenarioTimePace pace)
		{
			return pace switch
			{
				ScenarioTimePace.Paused => "恢复速度",
				ScenarioTimePace.Normal => "切换加速",
				ScenarioTimePace.Fast => "暂停时间",
				_ => throw new ArgumentOutOfRangeException(nameof(pace), pace, "未知剧本时间速度。")
			};
		}

		private static string GetProgressionModeActionLabel(ScenarioRun run)
		{
			if (run.DayCyclePhase != ScenarioDayCyclePhase.Inactive)
			{
				return "日终处理中";
			}

			return run.ProgressionMode == ActionProgressionMode.TurnBased
				? "开启即时"
				: run.CanReturnToTurnBasedProgression
					? "切回回合"
					: "即时推进中";
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
			EventKit.Type.UnRegister<ScenarioDayCycleChangedEvent>(OnScenarioDayCycleChanged);
            m_isSubscribed = false;
        }
    }
}
