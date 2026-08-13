using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 在候选快照上处理整堆边界与重叠的内部纯解算器。
	/// </summary>
	internal static class TabletopCardStackPlacementSolver
	{
		private const float Epsilon = 0.0001f;
		private const int MaxIterations = 64;

		public static TabletopCardStackSpatialResult Solve(TabletopCardPlacementArea area, IReadOnlyList<TabletopCardStackSpatialBody> bodies)
		{
			if (area == null)
			{
				throw new ArgumentNullException("area");
			}
			if (bodies == null)
			{
				throw new ArgumentNullException("bodies");
			}
			List<TabletopCardStackSpatialBody> solvedBodies = new List<TabletopCardStackSpatialBody>(bodies);
			solvedBodies.Sort((TabletopCardStackSpatialBody left, TabletopCardStackSpatialBody right) => left.BottomCardId.Value.CompareTo(right.BottomCardId.Value));
			ValidateUniqueBottomCardIds(solvedBodies);
			int iterations;
			for (iterations = 0; iterations < MaxIterations; iterations++)
			{
				bool moved = ResolveAreaConstraints(area, solvedBodies);
				if (!(moved | ResolveBodyOverlaps(solvedBodies)))
				{
					iterations++;
					break;
				}
			}
			bool converged = !HasUnresolvedConstraints(area, solvedBodies);
			return new TabletopCardStackSpatialResult(solvedBodies, iterations, converged);
		}

		private static void ValidateUniqueBottomCardIds(IReadOnlyList<TabletopCardStackSpatialBody> bodies)
		{
			for (int i = 1; i < bodies.Count; i++)
			{
				if (bodies[i - 1].BottomCardId == bodies[i].BottomCardId)
				{
					throw new ArgumentException($"空间解算输入重复包含底牌为 {bodies[i].BottomCardId} 的堆栈。", "bodies");
				}
			}
		}

		private static bool ResolveAreaConstraints(TabletopCardPlacementArea area, List<TabletopCardStackSpatialBody> bodies)
		{
			bool moved = false;
			for (int i = 0; i < bodies.Count; i++)
			{
				TabletopCardStackSpatialBody body = bodies[i];
				if (body.IsLocked)
				{
					continue;
				}
				Vector2 center = ClampToBounds(area.Bounds, body.FootprintCenter, body.Size);
				for (int restrictedIndex = 0; restrictedIndex < area.RestrictedAreas.Count; restrictedIndex++)
				{
					Rect restricted = area.RestrictedAreas[restrictedIndex];
					if (TryCalculateSeparation(center, body.Size, restricted.center, restricted.size, 1, out var separation))
					{
						center += separation;
					}
				}
				center = ClampToBounds(area.Bounds, center, body.Size);
				Vector2 position = center - body.FootprintCenterOffset;
				if ((position - body.Position).sqrMagnitude > 9.999999E-09f)
				{
					bodies[i] = body.WithPosition(position);
					moved = true;
				}
			}
			return moved;
		}

		private static bool ResolveBodyOverlaps(List<TabletopCardStackSpatialBody> bodies)
		{
			bool moved = false;
			for (int firstIndex = 0; firstIndex < bodies.Count; firstIndex++)
			{
				for (int secondIndex = firstIndex + 1; secondIndex < bodies.Count; secondIndex++)
				{
					TabletopCardStackSpatialBody first = bodies[firstIndex];
					TabletopCardStackSpatialBody second = bodies[secondIndex];
					if (TryCalculateSeparation(first.FootprintCenter, first.Size, second.FootprintCenter, second.Size, (first.BottomCardId.Value >= second.BottomCardId.Value) ? 1 : (-1), out var separation) && (!first.IsLocked || !second.IsLocked))
					{
						if (first.IsLocked)
						{
							bodies[secondIndex] = second.WithPosition(second.Position - separation);
						}
						else if (second.IsLocked)
						{
							bodies[firstIndex] = first.WithPosition(first.Position + separation);
						}
						else
						{
							Vector2 halfSeparation = separation * 0.5f;
							bodies[firstIndex] = first.WithPosition(first.Position + halfSeparation);
							bodies[secondIndex] = second.WithPosition(second.Position - halfSeparation);
						}
						moved = true;
					}
				}
			}
			return moved;
		}

		private static bool HasUnresolvedConstraints(TabletopCardPlacementArea area, IReadOnlyList<TabletopCardStackSpatialBody> bodies)
		{
			for (int i = 0; i < bodies.Count; i++)
			{
				TabletopCardStackSpatialBody body = bodies[i];
				if (!FitsInside(area.Bounds, body.FootprintCenter, body.Size))
				{
					return true;
				}
				for (int restrictedIndex = 0; restrictedIndex < area.RestrictedAreas.Count; restrictedIndex++)
				{
					Rect restricted = area.RestrictedAreas[restrictedIndex];
					if (Overlaps(body.FootprintCenter, body.Size, restricted.center, restricted.size))
					{
						return true;
					}
				}
				for (int otherIndex = i + 1; otherIndex < bodies.Count; otherIndex++)
				{
					TabletopCardStackSpatialBody other = bodies[otherIndex];
					if (Overlaps(body.FootprintCenter, body.Size, other.FootprintCenter, other.Size))
					{
						return true;
					}
				}
			}
			return false;
		}

		private static Vector2 ClampToBounds(Rect bounds, Vector2 center, Vector2 size)
		{
			Vector2 halfSize = size * 0.5f;
			float minX = bounds.xMin + halfSize.x;
			float maxX = bounds.xMax - halfSize.x;
			float minY = bounds.yMin + halfSize.y;
			float maxY = bounds.yMax - halfSize.y;
			if (minX > maxX || minY > maxY)
			{
				return bounds.center;
			}
			return new Vector2(Mathf.Clamp(center.x, minX, maxX), Mathf.Clamp(center.y, minY, maxY));
		}

		private static bool FitsInside(Rect bounds, Vector2 center, Vector2 size)
		{
			Vector2 halfSize = size * 0.5f;
			return center.x - halfSize.x >= bounds.xMin - 0.0001f && center.x + halfSize.x <= bounds.xMax + 0.0001f && center.y - halfSize.y >= bounds.yMin - 0.0001f && center.y + halfSize.y <= bounds.yMax + 0.0001f;
		}

		private static bool Overlaps(Vector2 firstCenter, Vector2 firstSize, Vector2 secondCenter, Vector2 secondSize)
		{
			Vector2 firstHalf = firstSize * 0.5f;
			Vector2 secondHalf = secondSize * 0.5f;
			return firstHalf.x + secondHalf.x - Mathf.Abs(firstCenter.x - secondCenter.x) > 0.0001f && firstHalf.y + secondHalf.y - Mathf.Abs(firstCenter.y - secondCenter.y) > 0.0001f;
		}

		private static bool TryCalculateSeparation(Vector2 firstCenter, Vector2 firstSize, Vector2 secondCenter, Vector2 secondSize, int coincidentDirection, out Vector2 separation)
		{
			Vector2 firstHalf = firstSize * 0.5f;
			Vector2 secondHalf = secondSize * 0.5f;
			float deltaX = firstCenter.x - secondCenter.x;
			float penetrationX = firstHalf.x + secondHalf.x - Mathf.Abs(deltaX);
			if (penetrationX <= 0.0001f)
			{
				separation = default(Vector2);
				return false;
			}
			float deltaY = firstCenter.y - secondCenter.y;
			float penetrationY = firstHalf.y + secondHalf.y - Mathf.Abs(deltaY);
			if (penetrationY <= 0.0001f)
			{
				separation = default(Vector2);
				return false;
			}
			if (penetrationX <= penetrationY)
			{
				float direction = ((Mathf.Abs(deltaX) > 0.0001f) ? Mathf.Sign(deltaX) : ((float)coincidentDirection));
				separation = new Vector2(penetrationX * direction, 0f);
			}
			else
			{
				float direction2 = ((Mathf.Abs(deltaY) > 0.0001f) ? Mathf.Sign(deltaY) : ((float)coincidentDirection));
				separation = new Vector2(0f, penetrationY * direction2);
			}
			return true;
		}
	}
}
