using System;
using System.Collections.Generic;
using Gameplay.Tabletop;
using UnityEngine;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 一次已经完整校验、等待场景载体切换后提交的地区旅行。
	/// </summary>
	internal sealed class ScenarioTravelPlan
	{
		private readonly ScenarioRun m_run;
		private readonly TabletopCardId[] m_travelerCardIds;
		private readonly Vector2[] m_targetPositions;

		internal ScenarioRegion SourceRegion { get; }

		internal ScenarioRegion TargetRegion { get; }

		internal string TargetSceneAddress => TargetRegion.SceneAddress;

		internal IReadOnlyList<TabletopCardId> TravelerCardIds => m_travelerCardIds;

		internal IReadOnlyList<Vector2> TargetPositions => m_targetPositions;

		internal bool IsCommitted { get; private set; }

		internal ScenarioTravelPlan(
			ScenarioRun run,
			ScenarioRegion sourceRegion,
			ScenarioRegion targetRegion,
			IReadOnlyList<TabletopCardId> travelerCardIds,
			IReadOnlyList<Vector2> targetPositions)
		{
			m_run = run ?? throw new ArgumentNullException(nameof(run));
			SourceRegion = sourceRegion ?? throw new ArgumentNullException(nameof(sourceRegion));
			TargetRegion = targetRegion ?? throw new ArgumentNullException(nameof(targetRegion));
			m_travelerCardIds = new List<TabletopCardId>(
				travelerCardIds ?? throw new ArgumentNullException(nameof(travelerCardIds))).ToArray();
			m_targetPositions = new List<Vector2>(
				targetPositions ?? throw new ArgumentNullException(nameof(targetPositions))).ToArray();
		}

		internal void Commit()
		{
			m_run.CommitTravel(this);
		}

		internal void MarkCommitted()
		{
			IsCommitted = true;
		}
	}
}
