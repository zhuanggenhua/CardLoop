using System;
using System.Collections.Generic;
using GameCore;
using Gameplay.Content;
using Gameplay.Tabletop;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本地区的内容作者源。地区拥有自己的场景载体、牌桌规则和默认抵达位置。
	/// </summary>
	[CreateAssetMenu(fileName = "地区_", menuName = "Gameplay/内容/剧本地区")]
	public class ScenarioRegionDefinition : DisplayableContentAsset
	{
		[SerializeField]
		[SceneAddressSelector]
		[LabelText("场景")]
		[Tooltip("进入该地区时由 SceneSystem 加载的 YooAsset 场景地址。为空表示地区共用当前场景；地址不作为内容 ID。")]
		private string m_sceneAddress = string.Empty;

		[SerializeField]
		[InlineProperty]
		[LabelText("卡牌放置规则")]
		[Tooltip("该地区牌桌的边界、禁放区域、卡牌规则尺寸和重叠解算配置。")]
		private TabletopCardPlacementDefinition m_tabletopPlacement = new TabletopCardPlacementDefinition();

		[SerializeField]
		[LabelText("默认抵达位置")]
		[Tooltip("卡牌旅行进入该地区时使用的牌桌位置；具体入口以后可由地点或行动覆盖。")]
		private Vector2 m_arrivalPosition = Vector2.zero;

		[SerializeField]
		[LabelText("开局牌桌")]
		[Tooltip("单局创建该地区时自动放到该地区牌桌上的卡牌。只保存内容、数量、位置和是否固定，不保存局内卡牌 ID。")]
		private ScenarioInitialCardPlacementDefinition[] m_initialCards =
			Array.Empty<ScenarioInitialCardPlacementDefinition>();

		public string SceneAddress => m_sceneAddress ?? string.Empty;

		public TabletopCardPlacementDefinition TabletopPlacement => m_tabletopPlacement;

		public Vector2 ArrivalPosition => m_arrivalPosition;

		public IReadOnlyList<ScenarioInitialCardPlacementDefinition> InitialCards =>
			m_initialCards ?? Array.Empty<ScenarioInitialCardPlacementDefinition>();

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (m_tabletopPlacement == null)
			{
				context.AddError(
					"SCENARIO_REGION_TABLETOP_PLACEMENT_MISSING",
					$"剧本地区 {ContentId} 缺少牌桌放置规则。",
					this);
			}
			else
			{
				try
				{
					m_tabletopPlacement.CreateRuntime();
				}
				catch (Exception exception)
				{
					context.AddError(
						"SCENARIO_REGION_TABLETOP_PLACEMENT_INVALID",
						$"剧本地区 {ContentId} 的牌桌放置规则无效：{exception.Message}",
						this);
				}
			}

			if (!float.IsFinite(m_arrivalPosition.x) || !float.IsFinite(m_arrivalPosition.y))
			{
				context.AddError(
					"SCENARIO_REGION_ARRIVAL_POSITION_INVALID",
					$"剧本地区 {ContentId} 的默认抵达位置必须是有限二维坐标。",
					this);
			}

			for (int i = 0; i < InitialCards.Count; i++)
			{
				ScenarioInitialCardPlacementDefinition initialCard = InitialCards[i];
				if (initialCard == null)
				{
					context.AddError(
						"SCENARIO_REGION_INITIAL_CARD_NULL",
						$"剧本地区 {ContentId} 的第 {i + 1} 个开局牌桌配置为空。",
						this);
					continue;
				}

				initialCard.Validate(context, this, i);
			}
		}
	}

	/// <summary>
	/// 地区开局牌桌上的一类牌堆；地区拥有牌桌，所以开局牌桌也归地区作者源。
	/// </summary>
	[Serializable]
	public sealed class ScenarioInitialCardPlacementDefinition
	{
		[SerializeField]
		[ContentIdReference(typeof(CardDefinition))]
		[LabelText("卡牌")]
		[Tooltip("开局要放到地区牌桌上的卡牌内容。")]
		private ContentId m_cardId;

		[SerializeField]
		[Min(1)]
		[LabelText("数量")]
		[Tooltip("同一位置创建的卡牌数量；大于 1 时作为同一牌堆生成。")]
		private int m_count = 1;

		[SerializeField]
		[LabelText("位置")]
		[Tooltip("开局牌堆在牌桌上的二维位置。")]
		private Vector2 m_position;

		[SerializeField]
		[LabelText("固定位置")]
		[Tooltip("开启后该牌堆不能被整体拖走，适合基地、固定地点等场景节点。")]
		private bool m_isPlacementLocked;

		public ContentId CardId => m_cardId;

		public int Count => m_count;

		public Vector2 Position => m_position;

		public bool IsPlacementLocked => m_isPlacementLocked;

		internal void Validate(
			ContentValidationContext context,
			ScenarioRegionDefinition region,
			int index)
		{
			if (!CardId.IsValid)
			{
				context.AddError(
					"SCENARIO_REGION_INITIAL_CARD_ID_INVALID",
					$"剧本地区 {region.ContentId} 的第 {index + 1} 个开局牌桌配置缺少有效卡牌 ID。",
					region);
			}
			else if (!context.TryGet(CardId, out CardDefinition _))
			{
				context.AddError(
					"SCENARIO_REGION_INITIAL_CARD_UNKNOWN",
					$"剧本地区 {region.ContentId} 的开局牌桌引用了不存在或类型错误的卡牌 {CardId}。",
					region);
			}

			if (Count <= 0)
			{
				context.AddError(
					"SCENARIO_REGION_INITIAL_CARD_COUNT_INVALID",
					$"剧本地区 {region.ContentId} 的开局牌桌卡牌 {CardId} 数量必须大于 0，当前值为 {Count}。",
					region);
			}

			if (!float.IsFinite(Position.x) || !float.IsFinite(Position.y))
			{
				context.AddError(
					"SCENARIO_REGION_INITIAL_CARD_POSITION_INVALID",
					$"剧本地区 {region.ContentId} 的开局牌桌卡牌 {CardId} 位置必须是有限二维坐标。",
					region);
			}
		}
	}
}
