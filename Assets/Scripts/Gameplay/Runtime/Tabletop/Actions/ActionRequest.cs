using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Tabletop;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>
	/// 已确认行动计划形成的提交请求，只携带稳定内容 ID、槽位 key 与局内卡牌 ID。
	/// </summary>
	public sealed class ActionRequest
	{
		private readonly ReadOnlyCollection<ActionRequestBinding> m_bindings;

		public ContentId ActionId { get; }

		public IReadOnlyList<ActionRequestBinding> Bindings => m_bindings;

		public ActionRequest(ContentId actionId, IReadOnlyList<ActionRequestBinding> bindings)
		{
			if (!actionId.IsValid)
			{
				throw new ArgumentException($"行动请求包含无效行动内容 ID：{actionId}。", "actionId");
			}
			ActionId = actionId;
			if (bindings == null)
			{
				throw new ArgumentNullException("bindings");
			}
			List<ActionRequestBinding> copiedBindings = new List<ActionRequestBinding>(bindings.Count);
			for (int i = 0; i < bindings.Count; i++)
			{
				copiedBindings.Add(bindings[i] ?? throw new ArgumentException($"行动请求 {actionId} 的第 {i} 个槽位绑定为空。", "bindings"));
			}
			m_bindings = copiedBindings.AsReadOnly();
		}

		public static ActionRequest FromCandidate(ActionCandidate candidate)
		{
			if (candidate == null)
			{
				throw new ArgumentNullException("candidate");
			}
			if (!candidate.IsReady)
			{
				throw new InvalidOperationException(
					$"行动 {candidate.Action.ContentId} 仍缺少 {candidate.MissingParticipantCount} 个参与对象，不能创建提交请求。");
			}
			List<ActionRequestBinding> requestBindings = new List<ActionRequestBinding>(candidate.Bindings.Count);
			for (int i = 0; i < candidate.Bindings.Count; i++)
			{
				ActionSlotBinding binding = candidate.Bindings[i];
				requestBindings.Add(new ActionRequestBinding(binding.Slot.Key, binding.CardIds));
			}
			return new ActionRequest(candidate.Action.ContentId, requestBindings);
		}

		/// <summary>
		/// 从活动行动快照恢复可重新复核的请求；结果计划和进度仍由快照本身持有。
		/// </summary>
		public static ActionRequest FromSnapshot(ActionInstanceSnapshot snapshot)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException("snapshot");
			}
			if (!snapshot.ActionId.IsValid)
			{
				throw new InvalidOperationException("行动实例快照缺少有效行动内容 ID。");
			}
			if (snapshot.Bindings == null)
			{
				throw new InvalidOperationException($"行动实例快照 {snapshot.ActionId} 缺少槽位绑定集合。");
			}

			List<ActionRequestBinding> requestBindings = new List<ActionRequestBinding>(snapshot.Bindings.Count);
			for (int i = 0; i < snapshot.Bindings.Count; i++)
			{
				ActionInstanceBindingSnapshot binding = snapshot.Bindings[i];
				if (binding == null)
				{
					throw new InvalidOperationException($"行动实例快照 {snapshot.ActionId} 的第 {i} 个槽位绑定为空。");
				}
				requestBindings.Add(new ActionRequestBinding(binding.SlotKey, binding.CreateCardIds()));
			}
			return new ActionRequest(snapshot.ActionId, requestBindings);
		}
	}

	/// <summary>
	/// 行动请求中一个槽位的局内卡牌 ID 集合。
	/// </summary>
	public sealed class ActionRequestBinding
	{
		private readonly ReadOnlyCollection<TabletopCardId> m_cardIds;

		public string SlotKey { get; }

		public IReadOnlyList<TabletopCardId> CardIds => m_cardIds;

		public ActionRequestBinding(string slotKey, IReadOnlyList<TabletopCardId> cardIds)
		{
			if (!ActionLocalKeyUtility.IsValidKey(slotKey))
			{
				throw new ArgumentException("行动请求槽位键无效：" + slotKey + "。", "slotKey");
			}
			SlotKey = slotKey;
			m_cardIds = new List<TabletopCardId>(cardIds ?? throw new ArgumentNullException("cardIds")).AsReadOnly();
		}
	}
}
