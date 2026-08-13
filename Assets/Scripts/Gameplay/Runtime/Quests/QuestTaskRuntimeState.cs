using System;
using Gameplay.Content;

namespace Gameplay.Quests
{
	/// <summary>
	/// 任务子项对外可读的数值进度；复杂任务可以用多个子项表达，不把任务规则集中到这里。
	/// </summary>
	public readonly struct QuestTaskProgressSnapshot
	{
		public int CurrentAmount { get; }

		public int RequiredAmount { get; }

		public bool IsCompleted => CurrentAmount >= RequiredAmount;

		public QuestTaskProgressSnapshot(int currentAmount, int requiredAmount)
		{
			if (currentAmount < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(currentAmount));
			}
			if (requiredAmount <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(requiredAmount));
			}
			CurrentAmount = currentAmount;
			RequiredAmount = requiredAmount;
		}
	}

	/// <summary>
	/// 单局任务日志交给任务子项解释的已提交事实；它不是全局事件总线或第二份玩法状态。
	/// </summary>
	public abstract class QuestTaskFact
	{
	}

	/// <summary>
	/// 普通牌桌行动成功结算后，所属剧本单局交给任务日志的行动完成事实。
	/// </summary>
	public sealed class ActionCompletedQuestTaskFact : QuestTaskFact
	{
		public ActionCompletedQuestTaskFact(ContentId actionId)
		{
			if (!actionId.IsValid)
			{
				throw new ArgumentException("行动完成事实必须引用有效内容 ID。", nameof(actionId));
			}

			ActionId = actionId;
		}

		public ContentId ActionId { get; }
	}

	/// <summary>
	/// 剧本单局跨入指定游戏日后交给任务日志的日期事实。
	/// </summary>
	public sealed class DayReachedQuestTaskFact : QuestTaskFact
	{
		public DayReachedQuestTaskFact(int currentDay)
		{
			if (currentDay <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(currentDay),
					currentDay,
					"到达天数必须大于 0。");
			}

			CurrentDay = currentDay;
		}

		public int CurrentDay { get; }
	}

	/// <summary>
	/// 剧本单局已确认某个内容进入当前发现集合后的事实。
	/// </summary>
	public sealed class ContentDiscoveredQuestTaskFact : QuestTaskFact
	{
		public ContentDiscoveredQuestTaskFact(ContentId contentId)
		{
			if (!contentId.IsValid)
			{
				throw new ArgumentException("内容发现事实必须引用有效内容 ID。", nameof(contentId));
			}

			ContentId = contentId;
		}

		public ContentId ContentId { get; }
	}

	/// <summary>
	/// 一个任务子项在当前单局中的可写进度。任务日志是唯一创建者和事实分发者。
	/// </summary>
	public abstract class QuestTaskRuntimeState
	{
		public bool IsCompleted => Progress.IsCompleted;

		public abstract QuestTaskProgressSnapshot Progress { get; }

		internal bool RecordFactFromQuestLog(QuestTaskFact fact)
		{
			return RecordFact(fact ?? throw new ArgumentNullException(nameof(fact)));
		}

		internal QuestTaskStateSnapshot CreateSnapshotForQuestLog()
		{
			QuestTaskStateSnapshot snapshot = CreateStateSnapshot();
			return snapshot ?? throw new InvalidOperationException(
				$"任务子项 {GetType().FullName} 返回了空存档状态。");
		}

		internal void RestoreSnapshotForQuestLog(QuestTaskStateSnapshot snapshot)
		{
			RestoreStateSnapshot(snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
		}

		/// <summary>为自定义任务子项生成自己的可序列化状态。</summary>
		protected virtual QuestTaskStateSnapshot CreateStateSnapshot()
		{
			throw new InvalidOperationException(
				$"任务子项 {GetType().FullName} 没有实现存档状态创建，不能生成不完整任务快照。");
		}

		/// <summary>从自定义任务子项自己的状态恢复；类型不匹配时应直接报错。</summary>
		protected virtual void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
		{
			throw new InvalidOperationException(
				$"任务子项 {GetType().FullName} 没有实现存档状态恢复，不能静默丢失任务进度。");
		}

		/// <summary>解释所属单局已提交的任务事实；Mod 任务状态可覆盖这个入口。</summary>
		protected abstract bool RecordFact(QuestTaskFact fact);
	}
}
