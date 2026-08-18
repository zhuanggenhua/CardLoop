using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>
	/// 装备槽位作者源。槽位是可由内容包扩展的内容 ID，不使用 C# 枚举固定死。
	/// </summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/装备槽位", fileName = "装备槽位_")]
	public sealed class EquipmentSlotDefinition : DisplayableContentAsset
	{
	}
}
