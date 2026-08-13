using System;
using Gameplay.Actions;
using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>
	/// 声明行动完成后移除某个参与槽位中的牌桌卡牌。
	/// </summary>
	[Serializable]
	public sealed class RemoveCardsResultIntent : ActionResultIntent
	{
		[SerializeField]
		[ActionSlotReference]
		[LabelText("移除槽位")]
		[Tooltip("选择行动完成后要移除卡牌的参与槽位。单槽位行动可以保留自动推导；多槽位行动必须明确选择。")]
		private string m_slotKey;

		public string SlotKey => m_slotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(SlotKey, "ACTION_RESULT_REMOVE_SLOT_UNKNOWN");
		}
	}

	/// <summary>
	/// 声明行动完成后在参与卡牌位置生成指定内容卡牌。
	/// </summary>
	[Serializable]
	public sealed class CreateCardsResultIntent : ActionResultIntent
	{
		[SerializeField]
		[ContentIdReference(typeof(CardDefinition))]
		[LabelText("产物卡牌")]
		[Tooltip("行动完成后生成的卡牌定义。编辑器自动保存其唯一内容 ID，不需要手填。")]
		private ContentId m_contentId;

		[SerializeField]
		[Min(1f)]
		[LabelText("生成数量")]
		[Tooltip("行动完成后生成的卡牌数量，必须大于 0。")]
		private int m_count = 1;

		[SerializeField]
		[ActionSlotReference]
		[LabelText("生成位置")]
		[Tooltip("选择产物生成位置所依据的参与槽位。单槽位行动可以保留自动推导；多槽位行动必须明确选择。")]
		private string m_anchorSlotKey;

		public ContentId ContentId => m_contentId;

		public int Count => m_count;

		public string AnchorSlotKey => m_anchorSlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			if (!ContentId.IsValid)
			{
				context.AddError(
					"ACTION_RESULT_CREATE_CONTENT_INVALID",
					$"行动 {context.Action.ContentId} 的产物内容 ID 无效：{ContentId}。");
			}
			else if (!context.Content.TryGet(ContentId, out ContentAsset _))
			{
				context.AddError(
					"ACTION_RESULT_CREATE_CONTENT_UNKNOWN",
					$"行动 {context.Action.ContentId} 的产物内容 {ContentId} 没有进入当前内容作者源集合。");
			}

			if (Count <= 0)
			{
				context.AddError(
					"ACTION_RESULT_CREATE_COUNT_INVALID",
					$"行动 {context.Action.ContentId} 的产物生成数量必须大于 0，当前值为 {Count}。");
			}
			context.ValidateSlotReference(
				AnchorSlotKey,
				"ACTION_RESULT_CREATE_ANCHOR_SLOT_UNKNOWN");
		}
	}
}
