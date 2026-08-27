using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>活动战斗区域的只读表现；区域事实始终来自所属牌桌。</summary>
	public sealed class TabletopBattleAreaView : MonoBehaviour
	{
		[SerializeField]
		[LabelText("区域渲染器")]
		[Tooltip("战斗区域的桌面贴图渲染器；Prefab 根节点必须旋转到 XZ 牌桌平面，区域尺寸由牌桌权威战斗区域派生。")]
		private SpriteRenderer m_renderer;

		public Battle Battle { get; private set; }

		public Rect DisplayedArea { get; private set; }

		internal void Bind(Battle battle)
		{
			Battle = battle ?? throw new ArgumentNullException(nameof(battle));
		}

		internal void ApplyArea(Rect area, int sortingOrder)
		{
			if (Battle == null)
			{
				throw new InvalidOperationException("战斗区域视图尚未绑定活动战斗。");
			}
			if (m_renderer == null)
			{
				throw new InvalidOperationException("战斗区域视图缺少 SpriteRenderer。");
			}

			DisplayedArea = area;
			transform.localPosition = TabletopCoordinateSpace.ToLocalPosition(area.center, -0.002f);
			transform.localScale = new Vector3(area.width, area.height, 1f);
			m_renderer.sortingOrder = sortingOrder;
			gameObject.SetActive(true);
		}
	}
}
