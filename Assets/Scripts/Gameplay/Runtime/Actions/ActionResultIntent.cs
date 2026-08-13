using System;
using System.Collections.Generic;
using UnityEngine;
using Gameplay.Content;
using Sirenix.OdinInspector;

namespace Gameplay.Actions
{
	/// <summary>
	/// 行动结果意图的作者校验上下文，提供当前行动槽位和活动内容查询。
	/// </summary>
	public sealed class ActionResultValidationContext
	{
		private readonly ISet<string> m_slotKeys;

		public ActionDefinition Action { get; }

		public ContentValidationContext Content { get; }

		public int SlotCount => m_slotKeys.Count;

		internal ActionResultValidationContext(
			ActionDefinition action,
			ISet<string> slotKeys,
			ContentValidationContext content)
		{
			Action = action ?? throw new ArgumentNullException(nameof(action));
			m_slotKeys = slotKeys ?? throw new ArgumentNullException(nameof(slotKeys));
			Content = content ?? throw new ArgumentNullException(nameof(content));
		}

		public bool HasSlot(string slotKey)
		{
			return ActionLocalKeyUtility.IsValidKey(slotKey) && m_slotKeys.Contains(slotKey);
		}

		public void ValidateSlotReference(string slotKey, string issueCode)
		{
			if (HasSlot(slotKey) || (string.IsNullOrWhiteSpace(slotKey) && SlotCount == 1))
			{
				return;
			}

			string readableSlotKey = string.IsNullOrWhiteSpace(slotKey)
				? "未指定；只有单槽位行动才能自动推导"
				: slotKey;
			AddError(issueCode, $"行动 {Action.ContentId} 的结果引用了不存在的参与槽位：{readableSlotKey}。");
		}

		public void AddError(string code, string message)
		{
			Content.AddError(code, message, Action);
		}
	}

	/// <summary>
	/// 行动结果声明的多态基类；只表达结算意图，不直接修改任何玩法状态。
	/// </summary>
	[Serializable]
	public abstract class ActionResultIntent
	{
		internal void ValidateIntent(ActionResultValidationContext context)
		{
			ValidateResult(context ?? throw new ArgumentNullException(nameof(context)));
		}

		/// <summary>校验当前结果意图的作者数据；Mod 结果意图可覆盖该入口。</summary>
		protected virtual void ValidateResult(ActionResultValidationContext context)
		{
		}
	}

	/// <summary>
	/// 行动开始时由权威随机流选择的结果分支作者数据。
	/// </summary>
	[Serializable]
	public sealed class ActionResultBranchDefinition
	{
		[SerializeField]
		[HideInInspector]
		private string m_key;

		[SerializeField]
		[Min(1f)]
		[LabelText("权重")]
		[Tooltip("正整数相对权重。权重越大，被权威随机选中的相对概率越高；0 或负数属于作者配置错误。")]
		private int m_weight = 1;

		[SerializeReference]
		[LabelText("分支结果")]
		[Tooltip("该分支被选中后提交给正式状态 owner 的结果意图；这里只声明，不直接执行副作用。")]
		private ActionResultIntent[] m_resultIntents = Array.Empty<ActionResultIntent>();

		public string Key => m_key ?? string.Empty;

		public int Weight => m_weight;

		public IReadOnlyList<ActionResultIntent> ResultIntents => m_resultIntents ?? Array.Empty<ActionResultIntent>();

		internal void EnsureLocalKey(string prefix, int zeroBasedIndex, ISet<string> usedKeys)
		{
			m_key = ActionLocalKeyUtility.EnsureUniqueKey(m_key, prefix, zeroBasedIndex, usedKeys);
		}
	}

	/// <summary>
	/// 为行动资产内部的槽位和分支生成稳定且唯一的隐藏 key，避免作者手工维护。
	/// </summary>
	internal static class ActionLocalKeyUtility
	{
		internal static bool IsValidKey(string value)
		{
			if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
			{
				return false;
			}
			foreach (char c in value)
			{
				if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
				{
					return false;
				}
			}
			return true;
		}

		internal static string EnsureUniqueKey(string currentKey, string prefix, int zeroBasedIndex, ISet<string> usedKeys)
		{
			if (usedKeys == null)
			{
				throw new ArgumentNullException("usedKeys");
			}
			if (IsValidKey(currentKey) && !usedKeys.Contains(currentKey))
			{
				usedKeys.Add(currentKey);
				return currentKey;
			}
			string baseKey = prefix + "-" + (zeroBasedIndex + 1);
			string candidate = baseKey;
			int suffix = 2;
			while (usedKeys.Contains(candidate))
			{
				candidate = baseKey + "-" + suffix;
				suffix++;
			}
			usedKeys.Add(candidate);
			return candidate;
		}
	}
}
