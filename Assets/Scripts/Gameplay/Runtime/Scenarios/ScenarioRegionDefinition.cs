using System;
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

		public string SceneAddress => m_sceneAddress ?? string.Empty;

		public TabletopCardPlacementDefinition TabletopPlacement => m_tabletopPlacement;

		public Vector2 ArrivalPosition => m_arrivalPosition;

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
		}
	}
}
