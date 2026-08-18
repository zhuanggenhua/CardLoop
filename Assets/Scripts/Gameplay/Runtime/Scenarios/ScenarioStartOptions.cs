using System;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 玩家开始一局剧本时选择的运行选项；它随单局保存，不替代剧本作者源。
	/// </summary>
	public readonly struct ScenarioStartOptions
	{
		public static ScenarioStartOptions Default { get; } = new ScenarioStartOptions(false);

		public bool FriendlyMode { get; }

		public float? DayDurationSecondsOverride { get; }

		public ScenarioStartOptions(bool friendlyMode)
			: this(friendlyMode, null)
		{
		}

		public ScenarioStartOptions(bool friendlyMode, float? dayDurationSecondsOverride)
		{
			if (dayDurationSecondsOverride.HasValue &&
				(!float.IsFinite(dayDurationSecondsOverride.Value) ||
				 dayDurationSecondsOverride.Value <= 0f))
			{
				throw new ArgumentOutOfRangeException(
					nameof(dayDurationSecondsOverride),
					"开局日长覆盖值必须是大于 0 的有限秒数。");
			}
			FriendlyMode = friendlyMode;
			DayDurationSecondsOverride = dayDurationSecondsOverride;
		}

		public override string ToString()
		{
			return DayDurationSecondsOverride.HasValue
				? FormattableString.Invariant(
					$"FriendlyMode={FriendlyMode}, DayDurationSeconds={DayDurationSecondsOverride.Value}")
				: FormattableString.Invariant($"FriendlyMode={FriendlyMode}");
		}
	}
}
