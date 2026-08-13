namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本导演已经提交活动单局变化后的领域事实；表现层据此改绑，不保存第二份活动单局状态。
	/// </summary>
	public readonly struct ScenarioRunChangedEvent
	{
		public ScenarioRun PreviousRun { get; }

		public ScenarioRun CurrentRun { get; }

		public ScenarioRunChangedEvent(ScenarioRun previousRun, ScenarioRun currentRun)
		{
			PreviousRun = previousRun;
			CurrentRun = currentRun;
		}
	}
}
