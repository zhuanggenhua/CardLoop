using System;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌二维坐标到 Unity 本地坐标的唯一映射：二维 Y 表达 StackCraft 桌面 Z，Unity Y 只表达离桌高度。
	/// </summary>
	internal static class TabletopCoordinateSpace
	{
		public static Vector3 ToLocalPosition(Vector2 tablePosition, float height = 0f)
		{
			if (!float.IsFinite(tablePosition.x) || !float.IsFinite(tablePosition.y) || !float.IsFinite(height))
			{
				throw new ArgumentException("牌桌坐标和离桌高度必须是有限数值。", nameof(tablePosition));
			}

			return new Vector3(tablePosition.x, height, tablePosition.y);
		}

		public static Vector2 ToTablePosition(Vector3 localPosition)
		{
			if (!float.IsFinite(localPosition.x) || !float.IsFinite(localPosition.z))
			{
				throw new ArgumentException("Unity 本地坐标必须能映射回有限牌桌坐标。", nameof(localPosition));
			}

			return new Vector2(localPosition.x, localPosition.z);
		}

		public static Vector3 ToLocalDelta(Vector2 tableDelta, float heightDelta = 0f)
		{
			if (!float.IsFinite(tableDelta.x) || !float.IsFinite(tableDelta.y) || !float.IsFinite(heightDelta))
			{
				throw new ArgumentException("牌桌位移和高度位移必须是有限数值。", nameof(tableDelta));
			}

			return new Vector3(tableDelta.x, heightDelta, tableDelta.y);
		}

		public static Plane CreateTablePlane(Transform tableTransform)
		{
			if (tableTransform == null)
			{
				throw new ArgumentNullException(nameof(tableTransform));
			}

			return new Plane(tableTransform.up, tableTransform.position);
		}
	}
}
