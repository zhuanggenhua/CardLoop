using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>可在日终进食阶段提供营养的卡牌定义。</summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/卡牌/食物", fileName = "食物_")]
	public class FoodCardDefinition : CardDefinition
	{
		[SerializeField]
		[Min(1f)]
		[LabelText("每次使用提供营养")]
		[Tooltip("日终自动进食每消耗一次卡牌使用次数时满足的饥饿值。")]
		private int m_nutritionPerUse = 1;

		public int NutritionPerUse => m_nutritionPerUse;

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (NutritionPerUse <= 0)
			{
				context.AddError(
					"FOOD_CARD_NUTRITION_INVALID",
					$"食物卡 {ContentId} 每次使用提供的营养必须大于 0。",
					this);
			}
		}
	}
}
