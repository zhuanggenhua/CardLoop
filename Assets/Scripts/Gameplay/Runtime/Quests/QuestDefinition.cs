using System;
using System.Collections.Generic;
using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Quests
{
	/// <summary>
	/// 任务的 ScriptableObject 作者源，声明前置任务和可结算子项；代码 Mod 可派生并补充作者数据与校验。
	/// </summary>
	[CreateAssetMenu(fileName = "任务_", menuName = "Gameplay/内容/任务")]
	public class QuestDefinition : DisplayableContentAsset
	{
		[SerializeField]
		[ContentIdReference(typeof(QuestDefinition))]
		[LabelText("前置任务")]
		[Tooltip("本任务激活前必须全部完成的任务。编辑器自动保存所选任务的唯一内容 ID，只维护这一份单向关系。")]
		private ContentId[] m_prerequisiteQuestIds = Array.Empty<ContentId>();

		[SerializeReference]
		[LabelText("任务子项")]
		[Tooltip("任务内部需要完成的子项或步骤。子项只声明作者数据，运行进度由当前单局的任务日志创建并持有。")]
		private QuestTaskDefinition[] m_tasks = Array.Empty<QuestTaskDefinition>();

		public IReadOnlyList<ContentId> PrerequisiteQuestIds => m_prerequisiteQuestIds ?? Array.Empty<ContentId>();

		public IReadOnlyList<QuestTaskDefinition> Tasks => m_tasks ?? Array.Empty<QuestTaskDefinition>();

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			ValidatePrerequisites(context);
			ValidateTasks(context);
			ValidatePrerequisiteCycle(context);
		}

		private void ValidatePrerequisites(ContentValidationContext context)
		{
			HashSet<ContentId> prerequisiteIds = new();
			for (int i = 0; i < PrerequisiteQuestIds.Count; i++)
			{
				ContentId prerequisiteId = PrerequisiteQuestIds[i];
				if (!prerequisiteId.IsValid)
				{
					context.AddError(
						"QUEST_PREREQUISITE_INVALID",
						$"任务 {ContentId} 引用了无效前置任务 ID：{prerequisiteId}。",
						this);
				}
				else if (!prerequisiteIds.Add(prerequisiteId))
				{
					context.AddError(
						"QUEST_PREREQUISITE_DUPLICATE",
						$"任务 {ContentId} 重复引用前置任务 {prerequisiteId}。",
						this);
				}
				else if (prerequisiteId.Equals(ContentId))
				{
					context.AddError(
						"QUEST_PREREQUISITE_SELF",
						$"任务 {ContentId} 不能把自己设为前置任务。",
						this);
				}
				else if (!context.TryGet(prerequisiteId, out ContentAsset prerequisiteAsset))
				{
					context.AddError(
						"QUEST_PREREQUISITE_UNKNOWN",
						$"任务 {ContentId} 引用了不存在的前置任务 {prerequisiteId}。",
						this);
				}
				else if (prerequisiteAsset is not QuestDefinition)
				{
					context.AddError(
						"QUEST_PREREQUISITE_TYPE_INVALID",
						$"任务 {ContentId} 引用的前置内容 {prerequisiteId} 不是任务定义。",
						this);
				}
			}
		}

		private void ValidateTasks(ContentValidationContext context)
		{
			QuestTaskValidationContext taskContext = new(this, context);
			for (int taskIndex = 0; taskIndex < Tasks.Count; taskIndex++)
			{
				QuestTaskDefinition task = Tasks[taskIndex];
				if (task == null)
				{
					context.AddError(
						"QUEST_TASK_NULL",
						$"任务 {ContentId} 的第 {taskIndex + 1} 个任务子项为空。",
						this);
					continue;
				}
				task.ValidateTask(taskContext);
			}
		}

		private void ValidatePrerequisiteCycle(ContentValidationContext context)
		{
			if (!ContentId.IsValid)
			{
				return;
			}

			HashSet<ContentId> visiting = new();
			HashSet<ContentId> completed = new();
			List<ContentId> path = new();
			if (!TryFindPrerequisiteCycle(
					ContentId,
					context,
					visiting,
					completed,
					path,
					out List<ContentId> cycle))
			{
				return;
			}

			string smallestId = cycle[0].Value;
			for (int i = 1; i < cycle.Count - 1; i++)
			{
				if (string.CompareOrdinal(cycle[i].Value, smallestId) < 0)
				{
					smallestId = cycle[i].Value;
				}
			}
			if (string.Equals(ContentId.Value, smallestId, StringComparison.Ordinal))
			{
				context.AddError(
					"QUEST_PREREQUISITE_CYCLE",
					"任务前置关系形成循环：" + string.Join(" -> ", cycle) + "。",
					this);
			}
		}

		private static bool TryFindPrerequisiteCycle(
			ContentId questId,
			ContentValidationContext context,
			HashSet<ContentId> visiting,
			HashSet<ContentId> completed,
			List<ContentId> path,
			out List<ContentId> cycle)
		{
			if (visiting.Contains(questId))
			{
				int cycleStartIndex = path.IndexOf(questId);
				cycle = path.GetRange(cycleStartIndex, path.Count - cycleStartIndex);
				cycle.Add(questId);
				return true;
			}
			if (completed.Contains(questId) ||
				!context.TryGet(questId, out QuestDefinition quest))
			{
				cycle = null;
				return false;
			}

			visiting.Add(questId);
			path.Add(questId);
			for (int i = 0; i < quest.PrerequisiteQuestIds.Count; i++)
			{
				ContentId prerequisiteId = quest.PrerequisiteQuestIds[i];
				if (!prerequisiteId.IsValid || prerequisiteId.Equals(questId))
				{
					continue;
				}
				if (TryFindPrerequisiteCycle(
						prerequisiteId,
						context,
						visiting,
						completed,
						path,
						out cycle))
				{
					return true;
				}
			}

			path.RemoveAt(path.Count - 1);
			visiting.Remove(questId);
			completed.Add(questId);
			cycle = null;
			return false;
		}
	}
}
