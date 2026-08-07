using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 不依赖场景组件的卡牌二维边界与重叠解算器。
    /// </summary>
    public static class TabletopCardOverlapSolver
    {
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// 在牌桌边界、禁放区域和卡牌占地之间执行确定性迭代解算。
        /// 输入会按局内卡牌 ID 排序，因此调用方传入顺序不会改变结果；返回值明确报告是否完全收敛。
        /// </summary>
        public static TabletopCardSpatialResult Solve(
            TabletopCardPlacementArea area,
            IReadOnlyList<TabletopCardSpatialBody> bodies,
            int maxIterations)
        {
            if (area == null)
            {
                throw new ArgumentNullException(nameof(area));
            }

            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            if (maxIterations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations));
            }

            var solvedBodies = new List<TabletopCardSpatialBody>(bodies);
            solvedBodies.Sort((left, right) => left.CardId.Value.CompareTo(right.CardId.Value));
            ValidateUniqueCardIds(solvedBodies);

            int iterations = 0;
            for (; iterations < maxIterations; iterations++)
            {
                bool moved = ResolveAreaConstraints(area, solvedBodies);
                moved |= ResolveBodyOverlaps(solvedBodies);

                if (!moved)
                {
                    iterations++;
                    break;
                }
            }

            bool converged = !HasUnresolvedConstraints(area, solvedBodies);
            return new TabletopCardSpatialResult(solvedBodies, iterations, converged);
        }

        private static void ValidateUniqueCardIds(IReadOnlyList<TabletopCardSpatialBody> bodies)
        {
            for (int i = 1; i < bodies.Count; i++)
            {
                if (bodies[i - 1].CardId == bodies[i].CardId)
                {
                    throw new ArgumentException($"空间解算输入重复包含局内卡牌 {bodies[i].CardId}。", nameof(bodies));
                }
            }
        }

        private static bool ResolveAreaConstraints(
            TabletopCardPlacementArea area,
            List<TabletopCardSpatialBody> bodies)
        {
            bool moved = false;

            for (int i = 0; i < bodies.Count; i++)
            {
                TabletopCardSpatialBody body = bodies[i];
                // 锁定卡牌是权威状态约束，不能为了让解算结果好看而偷偷改位置。
                if (body.IsLocked)
                {
                    continue;
                }

                Vector2 position = ClampToBounds(area.Bounds, body.Position, body.Size, out _);
                for (int restrictedIndex = 0; restrictedIndex < area.RestrictedAreas.Count; restrictedIndex++)
                {
                    Rect restricted = area.RestrictedAreas[restrictedIndex];
                    if (TryCalculateSeparation(position, body.Size, restricted.center, restricted.size, 1, out Vector2 separation))
                    {
                        position += separation;
                    }
                }

                position = ClampToBounds(area.Bounds, position, body.Size, out _);
                if ((position - body.Position).sqrMagnitude > Epsilon * Epsilon)
                {
                    bodies[i] = body.WithPosition(position);
                    moved = true;
                }
            }

            return moved;
        }

        private static bool ResolveBodyOverlaps(List<TabletopCardSpatialBody> bodies)
        {
            bool moved = false;

            // bodies 已按局内卡牌 ID 排序。同中心时使用这个稳定顺序决定分离方向，
            // 避免联机端或回放因为集合遍历顺序不同而得到不同位置。
            for (int firstIndex = 0; firstIndex < bodies.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < bodies.Count; secondIndex++)
                {
                    TabletopCardSpatialBody first = bodies[firstIndex];
                    TabletopCardSpatialBody second = bodies[secondIndex];
                    if (!TryCalculateSeparation(
                            first.Position,
                            first.Size,
                            second.Position,
                            second.Size,
                            first.CardId.Value < second.CardId.Value ? -1 : 1,
                            out Vector2 separation))
                    {
                        continue;
                    }

                    if (first.IsLocked && second.IsLocked)
                    {
                        continue;
                    }

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

            return moved;
        }

        private static bool HasUnresolvedConstraints(
            TabletopCardPlacementArea area,
            IReadOnlyList<TabletopCardSpatialBody> bodies)
        {
            // 迭代次数耗尽或全部卡牌都被锁定时，不能用“本轮没有移动”冒充已经收敛。
            for (int i = 0; i < bodies.Count; i++)
            {
                TabletopCardSpatialBody body = bodies[i];
                if (!FitsInside(area.Bounds, body.Position, body.Size))
                {
                    return true;
                }

                for (int restrictedIndex = 0; restrictedIndex < area.RestrictedAreas.Count; restrictedIndex++)
                {
                    Rect restricted = area.RestrictedAreas[restrictedIndex];
                    if (Overlaps(body.Position, body.Size, restricted.center, restricted.size))
                    {
                        return true;
                    }
                }

                for (int otherIndex = i + 1; otherIndex < bodies.Count; otherIndex++)
                {
                    if (Overlaps(body.Position, body.Size, bodies[otherIndex].Position, bodies[otherIndex].Size))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Vector2 ClampToBounds(
            Rect bounds,
            Vector2 position,
            Vector2 size,
            out bool fits)
        {
            Vector2 halfSize = size * 0.5f;
            float minX = bounds.xMin + halfSize.x;
            float maxX = bounds.xMax - halfSize.x;
            float minY = bounds.yMin + halfSize.y;
            float maxY = bounds.yMax - halfSize.y;

            fits = minX <= maxX && minY <= maxY;
            if (!fits)
            {
                return bounds.center;
            }

            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY));
        }

        private static bool FitsInside(Rect bounds, Vector2 position, Vector2 size)
        {
            Vector2 halfSize = size * 0.5f;
            return
                position.x - halfSize.x >= bounds.xMin - Epsilon &&
                position.x + halfSize.x <= bounds.xMax + Epsilon &&
                position.y - halfSize.y >= bounds.yMin - Epsilon &&
                position.y + halfSize.y <= bounds.yMax + Epsilon;
        }

        private static bool Overlaps(
            Vector2 firstPosition,
            Vector2 firstSize,
            Vector2 secondPosition,
            Vector2 secondSize)
        {
            Vector2 firstHalf = firstSize * 0.5f;
            Vector2 secondHalf = secondSize * 0.5f;
            return
                firstHalf.x + secondHalf.x - Mathf.Abs(firstPosition.x - secondPosition.x) > Epsilon &&
                firstHalf.y + secondHalf.y - Mathf.Abs(firstPosition.y - secondPosition.y) > Epsilon;
        }

        private static bool TryCalculateSeparation(
            Vector2 firstPosition,
            Vector2 firstSize,
            Vector2 secondPosition,
            Vector2 secondSize,
            int coincidentDirection,
            out Vector2 separation)
        {
            // separation 始终表示“第一张卡牌需要移动的完整位移”。
            // 两张卡牌都可动时，调用方再把它平均分摊到双方。
            Vector2 firstHalf = firstSize * 0.5f;
            Vector2 secondHalf = secondSize * 0.5f;
            float deltaX = firstPosition.x - secondPosition.x;
            float penetrationX = firstHalf.x + secondHalf.x - Mathf.Abs(deltaX);
            if (penetrationX <= Epsilon)
            {
                separation = default;
                return false;
            }

            float deltaY = firstPosition.y - secondPosition.y;
            float penetrationY = firstHalf.y + secondHalf.y - Mathf.Abs(deltaY);
            if (penetrationY <= Epsilon)
            {
                separation = default;
                return false;
            }

            if (penetrationX <= penetrationY)
            {
                float direction = Mathf.Abs(deltaX) > Epsilon ? Mathf.Sign(deltaX) : coincidentDirection;
                separation = new Vector2(penetrationX * direction, 0f);
            }
            else
            {
                float direction = Mathf.Abs(deltaY) > Epsilon ? Mathf.Sign(deltaY) : coincidentDirection;
                separation = new Vector2(0f, penetrationY * direction);
            }

            return true;
        }
    }
}
