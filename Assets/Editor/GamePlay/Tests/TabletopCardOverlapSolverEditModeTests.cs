using NUnit.Framework;
using UnityEngine;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证卡牌占地在牌桌边界、禁放区和重叠情况下的确定性解算结果。
    /// </summary>
    public sealed class TabletopCardOverlapSolverEditModeTests
    {
        [Test]
        public void Solve_ClampsUsingTheWholeFootprint()
        {
            var state = new TabletopCardState();
            TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
            var area = new TabletopCardPlacementArea(new Rect(-5f, -4f, 10f, 8f));
            var body = new TabletopCardSpatialBody(
                tabletopCard.Id,
                new Vector2(-20f, 20f),
                new Vector2(2f, 2f),
                isLocked: false);

            TabletopCardSpatialResult result = TabletopCardOverlapSolver.Solve(area, new[] { body }, 4);

            Assert.That(result.GetPosition(tabletopCard.Id), Is.EqualTo(new Vector2(-4f, 3f)));
            Assert.That(result.Converged, Is.True);
        }

        [Test]
        public void Solve_SeparatesCoincidentBodiesWithStableIdOrder()
        {
            var state = new TabletopCardState();
            TabletopCard first = state.CreateCard("test.first", Vector2.zero);
            TabletopCard second = state.CreateCard("test.second", Vector2.zero);
            var area = new TabletopCardPlacementArea(new Rect(-10f, -10f, 20f, 20f));
            var bodies = new[]
            {
                new TabletopCardSpatialBody(second.Id, Vector2.zero, Vector2.one * 2f, false),
                new TabletopCardSpatialBody(first.Id, Vector2.zero, Vector2.one * 2f, false),
            };

            TabletopCardSpatialResult result = TabletopCardOverlapSolver.Solve(area, bodies, 16);
            Vector2 firstPosition = result.GetPosition(first.Id);
            Vector2 secondPosition = result.GetPosition(second.Id);

            Assert.That(result.Converged, Is.True);
            Assert.That(firstPosition.x, Is.LessThan(secondPosition.x));
            Assert.That(secondPosition.x - firstPosition.x, Is.GreaterThanOrEqualTo(2f));
        }

        [Test]
        public void Solve_MovesOnlyUnlockedBodyWhenOverlapIncludesLockedBody()
        {
            var state = new TabletopCardState();
            TabletopCard locked = state.CreateCard("test.locked", Vector2.zero);
            TabletopCard movable = state.CreateCard("test.movable", new Vector2(0.5f, 0f));
            var area = new TabletopCardPlacementArea(new Rect(-10f, -10f, 20f, 20f));
            var bodies = new[]
            {
                new TabletopCardSpatialBody(locked.Id, Vector2.zero, Vector2.one * 2f, true),
                new TabletopCardSpatialBody(movable.Id, new Vector2(0.5f, 0f), Vector2.one * 2f, false),
            };

            TabletopCardSpatialResult result = TabletopCardOverlapSolver.Solve(area, bodies, 16);

            Assert.That(result.Converged, Is.True);
            Assert.That(result.GetPosition(locked.Id), Is.EqualTo(Vector2.zero));
            Assert.That(result.GetPosition(movable.Id).x, Is.GreaterThanOrEqualTo(2f));
        }

        [Test]
        public void Solve_ReportsUnresolvedWhenBothOverlappingBodiesAreLocked()
        {
            var state = new TabletopCardState();
            TabletopCard first = state.CreateCard("test.first", Vector2.zero);
            TabletopCard second = state.CreateCard("test.second", Vector2.zero);
            var area = new TabletopCardPlacementArea(new Rect(-10f, -10f, 20f, 20f));
            var bodies = new[]
            {
                new TabletopCardSpatialBody(first.Id, Vector2.zero, Vector2.one * 2f, true),
                new TabletopCardSpatialBody(second.Id, Vector2.zero, Vector2.one * 2f, true),
            };

            TabletopCardSpatialResult result = TabletopCardOverlapSolver.Solve(area, bodies, 16);

            Assert.That(result.Converged, Is.False);
            Assert.That(result.GetPosition(first.Id), Is.EqualTo(Vector2.zero));
            Assert.That(result.GetPosition(second.Id), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Solve_PushesBodyOutOfRestrictedArea()
        {
            var state = new TabletopCardState();
            TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
            var area = new TabletopCardPlacementArea(
                new Rect(-10f, -10f, 20f, 20f),
                new[] { new Rect(-1f, -1f, 2f, 2f) });
            var body = new TabletopCardSpatialBody(
                tabletopCard.Id,
                Vector2.zero,
                Vector2.one,
                isLocked: false);

            TabletopCardSpatialResult result = TabletopCardOverlapSolver.Solve(area, new[] { body }, 16);

            Assert.That(result.Converged, Is.True);
            Assert.That(result.GetPosition(tabletopCard.Id).x, Is.GreaterThanOrEqualTo(1.5f));
        }
    }
}
