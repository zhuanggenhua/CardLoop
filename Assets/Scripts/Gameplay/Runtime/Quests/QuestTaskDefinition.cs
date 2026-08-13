using System;
using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Quests
{
	/// <summary>
	/// 任务子项的作者校验上下文，提供所属任务和活动内容查询。
	/// </summary>
	public sealed class QuestTaskValidationContext
	{
		public QuestDefinition Quest { get; }

		public ContentValidationContext Content { get; }

		internal QuestTaskValidationContext(
			QuestDefinition quest,
			ContentValidationContext content)
		{
			Quest = quest ?? throw new ArgumentNullException(nameof(quest));
			Content = content ?? throw new ArgumentNullException(nameof(content));
		}

		public void AddError(string code, string message)
		{
			Content.AddError(code, message, Quest);
		}
	}

	/// <summary>
	/// 任务子项作者声明的多态基类。
	/// </summary>
	[Serializable]
	public abstract class QuestTaskDefinition
	{
		internal void ValidateTask(QuestTaskValidationContext context)
		{
			ValidateDefinition(context ?? throw new ArgumentNullException(nameof(context)));
		}

		internal QuestTaskRuntimeState CreateRuntimeStateForQuestLog()
		{
			QuestTaskRuntimeState state = CreateRuntimeState();
			if (state == null)
			{
				throw new InvalidOperationException(
					$"任务子项类型 {GetType().FullName} 没有创建运行时进度状态。");
			}

			return state;
		}

		/// <summary>校验当前任务子项的作者数据；Mod 子项可覆盖该入口。</summary>
		protected virtual void ValidateDefinition(QuestTaskValidationContext context)
		{
		}

		/// <summary>创建当前单局使用的进度状态；Mod 子项必须通过该入口接入任务日志。</summary>
		protected abstract QuestTaskRuntimeState CreateRuntimeState();
	}

	/// <summary>
	/// 通过已提交的行动完成事实累计进度的任务子项。
	/// </summary>
	[Serializable]
	public sealed class ActionCompletionQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField]
		[ContentIdReference(typeof(Gameplay.Actions.ActionDefinition))]
		[LabelText("要求行动")]
		[Tooltip("需要成功完成的具体行动。编辑器自动保存其唯一内容 ID；它不是行动类型枚举，也不替代行动自身的 EX-GAS 标签。")]
		private ContentId m_actionId;

		[SerializeField]
		[Min(1f)]
		[LabelText("完成次数")]
		[Tooltip("本任务子项需要累计的成功行动次数。必须大于 0。")]
		private int m_requiredCompletionCount = 1;

		public ContentId ActionId => m_actionId;

		public int RequiredCompletionCount => m_requiredCompletionCount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!ActionId.IsValid)
			{
				context.AddError(
					"QUEST_ACTION_TASK_ACTION_INVALID",
					$"任务 {context.Quest.ContentId} 的行动完成子项引用了无效行动 ID：{ActionId}。");
			}
			else if (!context.Content.TryGet(ActionId, out ContentAsset actionAsset))
			{
				context.AddError(
					"QUEST_ACTION_TASK_ACTION_UNKNOWN",
					$"任务 {context.Quest.ContentId} 的行动完成子项引用了不存在的行动 {ActionId}。");
			}
			else if (actionAsset is not Gameplay.Actions.ActionDefinition)
			{
				context.AddError(
					"QUEST_ACTION_TASK_ACTION_TYPE_INVALID",
					$"任务 {context.Quest.ContentId} 的行动完成子项引用的内容 {ActionId} 不是行动定义。");
			}

			if (RequiredCompletionCount <= 0)
			{
				context.AddError(
					"QUEST_ACTION_TASK_COUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的行动完成次数必须大于 0，当前值为 {RequiredCompletionCount}。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new ActionCompletionQuestTaskRuntimeState(this);
		}

		private sealed class ActionCompletionQuestTaskRuntimeState : QuestTaskRuntimeState
		{
			private readonly ActionCompletionQuestTaskDefinition m_definition;

			private int m_completedCount;

			internal ActionCompletionQuestTaskRuntimeState(ActionCompletionQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress => new QuestTaskProgressSnapshot(
				m_completedCount,
				m_definition.RequiredCompletionCount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not ActionCompletedQuestTaskFact actionCompleted ||
					actionCompleted.ActionId != m_definition.ActionId)
				{
					return false;
				}

				checked
				{
					m_completedCount++;
					return true;
				}
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_completedCount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 ||
					amount.CurrentAmount > m_definition.RequiredCompletionCount)
				{
					throw new InvalidOperationException("行动完成任务子项的存档进度无效。");
				}
				m_completedCount = amount.CurrentAmount;
			}
		}
	}

	/// <summary>
	/// 在剧本单局到达指定游戏日后完成的任务子项。
	/// </summary>
	[Serializable]
	public sealed class DayReachedQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField]
		[Min(1f)]
		[LabelText("要求到达天数")]
		[Tooltip("当前单局首次到达这个游戏日时完成子项。游戏日由剧本的每日确认回合数和总确认回合推导。")]
		private int m_requiredDay = 1;

		public int RequiredDay => m_requiredDay;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (RequiredDay <= 0)
			{
				context.AddError(
					"QUEST_DAY_TASK_DAY_INVALID",
					$"任务 {context.Quest.ContentId} 的要求到达天数必须大于 0，当前值为 {RequiredDay}。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new DayReachedQuestTaskRuntimeState(this);
		}

		private sealed class DayReachedQuestTaskRuntimeState : QuestTaskRuntimeState
		{
			private readonly DayReachedQuestTaskDefinition m_definition;

			private int m_currentDay;

			internal DayReachedQuestTaskRuntimeState(DayReachedQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress => new QuestTaskProgressSnapshot(
				m_currentDay,
				m_definition.RequiredDay);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not DayReachedQuestTaskFact dayReached)
				{
					return false;
				}

				bool reachedRequiredDay = dayReached.CurrentDay >= m_definition.RequiredDay;
				m_currentDay = dayReached.CurrentDay;
				return reachedRequiredDay;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_currentDay);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount || amount.CurrentAmount < 0)
				{
					throw new InvalidOperationException("到达天数任务子项的存档进度无效。");
				}
				m_currentDay = amount.CurrentAmount;
			}
		}
	}

	/// <summary>
	/// 在当前剧本单局发现指定内容后完成的任务子项。
	/// </summary>
	[Serializable]
	public sealed class ContentDiscoveryQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField]
		[ContentIdReference]
		[LabelText("要求发现内容")]
		[Tooltip("当前剧本单局必须已经发现的具体内容。选择器只保存唯一内容 ID；发现状态由剧本单局持有。")]
		private ContentId m_discoveredContentId;

		public ContentId DiscoveredContentId => m_discoveredContentId;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!DiscoveredContentId.IsValid)
			{
				context.AddError(
					"QUEST_DISCOVERY_TASK_CONTENT_INVALID",
					$"任务 {context.Quest.ContentId} 的发现内容子项引用了无效内容 ID：{DiscoveredContentId}。");
			}
			else if (!context.Content.TryGet(DiscoveredContentId, out ContentAsset _))
			{
				context.AddError(
					"QUEST_DISCOVERY_TASK_CONTENT_UNKNOWN",
					$"任务 {context.Quest.ContentId} 的发现内容子项引用了不存在的内容 {DiscoveredContentId}。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new ContentDiscoveryQuestTaskRuntimeState(this);
		}

		private sealed class ContentDiscoveryQuestTaskRuntimeState : QuestTaskRuntimeState
		{
			private readonly ContentDiscoveryQuestTaskDefinition m_definition;

			private bool m_hasDiscoveredContent;

			internal ContentDiscoveryQuestTaskRuntimeState(ContentDiscoveryQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress => new QuestTaskProgressSnapshot(
				m_hasDiscoveredContent ? 1 : 0,
				1);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not ContentDiscoveredQuestTaskFact contentDiscovered ||
					contentDiscovered.ContentId != m_definition.DiscoveredContentId)
				{
					return false;
				}

				m_hasDiscoveredContent = true;
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_hasDiscoveredContent ? 1 : 0);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					(amount.CurrentAmount != 0 && amount.CurrentAmount != 1))
				{
					throw new InvalidOperationException("内容发现任务子项的存档进度无效。");
				}
				m_hasDiscoveredContent = amount.CurrentAmount == 1;
			}
		}
	}
}
