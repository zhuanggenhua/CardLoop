using System;
using System.Collections.Generic;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Tabletop;
using UnityEngine;

namespace Gameplay.Scenarios
{
	/// <summary>一次活动剧本单局的完整、可序列化运行事实。</summary>
	[Serializable]
	public sealed class ScenarioRunSnapshot
	{
		[SerializeField]
		private ContentId m_scenarioId;

		[SerializeField]
		private ContentSetSnapshot m_contentSet;

		[SerializeField]
		private ContentId m_activeRegionId;

		[SerializeField]
		private ScenarioRegionSnapshot[] m_regions;

		[SerializeField]
		private ContentId[] m_discoveredContentIds;

		[SerializeField]
		private QuestLogSnapshot m_questLog;

		[SerializeField]
		private ulong m_nextCardId;

		[SerializeField]
		private int m_confirmedTurnIndex;

		[SerializeField]
		private ActionProgressionMode m_progressionMode;

		[SerializeField]
		private double m_realTimeElapsedSecondsInTurn;

		public ContentId ScenarioId => m_scenarioId;
		public ContentSetSnapshot ContentSet => m_contentSet;
		public ContentId ActiveRegionId => m_activeRegionId;
		public IReadOnlyList<ScenarioRegionSnapshot> Regions => m_regions;
		public IReadOnlyList<ContentId> DiscoveredContentIds => m_discoveredContentIds;
		public QuestLogSnapshot QuestLog => m_questLog;
		public ulong NextCardId => m_nextCardId;
		public int ConfirmedTurnIndex => m_confirmedTurnIndex;
		public ActionProgressionMode ProgressionMode => m_progressionMode;
		public double RealTimeElapsedSecondsInTurn => m_realTimeElapsedSecondsInTurn;

		internal ScenarioRunSnapshot(
			ContentId scenarioId,
			ContentSetSnapshot contentSet,
			ContentId activeRegionId,
			ScenarioRegionSnapshot[] regions,
			ContentId[] discoveredContentIds,
			QuestLogSnapshot questLog,
			ulong nextCardId,
			int confirmedTurnIndex,
			ActionProgressionMode progressionMode,
			double realTimeElapsedSecondsInTurn)
		{
			m_scenarioId = scenarioId;
			m_contentSet = contentSet ?? throw new ArgumentNullException(nameof(contentSet));
			m_activeRegionId = activeRegionId;
			m_regions = regions ?? throw new ArgumentNullException(nameof(regions));
			m_discoveredContentIds = discoveredContentIds ?? throw new ArgumentNullException(nameof(discoveredContentIds));
			m_questLog = questLog ?? throw new ArgumentNullException(nameof(questLog));
			m_nextCardId = nextCardId;
			m_confirmedTurnIndex = confirmedTurnIndex;
			m_progressionMode = progressionMode;
			m_realTimeElapsedSecondsInTurn = realTimeElapsedSecondsInTurn;
		}
	}

	/// <summary>一个剧本地区长期保留的牌桌事实。</summary>
	[Serializable]
	public sealed class ScenarioRegionSnapshot
	{
		[SerializeField]
		private ContentId m_regionId;

		[SerializeField]
		private TabletopSnapshot m_tabletop;

		public ContentId RegionId => m_regionId;
		public TabletopSnapshot Tabletop => m_tabletop;

		internal ScenarioRegionSnapshot(ContentId regionId, TabletopSnapshot tabletop)
		{
			m_regionId = regionId;
			m_tabletop = tabletop ?? throw new ArgumentNullException(nameof(tabletop));
		}
	}
}
