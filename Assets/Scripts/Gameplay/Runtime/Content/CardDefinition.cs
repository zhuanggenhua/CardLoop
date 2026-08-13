using GameCore;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>
	/// 可实例化到牌桌的卡牌内容作者源，补充卡面图片等卡牌专属数据。
	/// </summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/卡牌", fileName = "卡牌_")]
	public class CardDefinition : DisplayableContentAsset
	{
		[Header("卡牌表现")]
		[SerializeField]
		[LabelText("卡面美术")]
		[Tooltip("卡牌正面使用的图片地址。它只负责表现，不替代内容 ID。")]
		private SoftAssetReference<Sprite> m_cardArt;

		public SoftAssetReference<Sprite> CardArt => m_cardArt;

		public SoftAssetReference<Sprite> Artwork => CardArt != null && CardArt.IsValid() ? CardArt : base.Icon;
	}
}
