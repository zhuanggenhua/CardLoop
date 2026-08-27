using System.Collections.Generic;
using Gameplay.Tabletop;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证整堆边界、禁放区和重叠解算规则。
	/// </summary>
	public sealed class TabletopCardStackPlacementSolverEditModeTests
	{
		private static readonly TabletopCardStackGeometry SingleCardGeometry = new TabletopCardStackGeometry(Vector2.one * 2f, Vector2.zero);

		[Test]
		public void Solve_ClampsUsingTheWholeFootprint()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
			TabletopCardPlacementArea area = new TabletopCardPlacementArea(new Rect(-5f, -4f, 10f, 8f));
			TabletopCardStackSpatialBody body = SingleCardGeometry.CreateSpatialBody(tabletopCard.Id, new Vector2(-20f, 20f), 1, isLocked: false);
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(area, new TabletopCardStackSpatialBody[1] { body });
			Assert.That<Vector2>(result.GetPosition(tabletopCard.Id), (IResolveConstraint)(object)Is.EqualTo((object)new Vector2(-4f, 3f)));
			Assert.That<bool>(result.Converged, (IResolveConstraint)(object)Is.True);
		}

		[Test]
		public void Solve_SeparatesCoincidentBodiesWithStableIdOrder()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard first = state.CreateCard("test.first", Vector2.zero);
			TabletopCard second = state.CreateCard("test.second", Vector2.zero);
			TabletopCardPlacementArea area = new TabletopCardPlacementArea(new Rect(-10f, -10f, 20f, 20f));
			TabletopCardStackSpatialBody[] bodies = new TabletopCardStackSpatialBody[2]
			{
				SingleCardGeometry.CreateSpatialBody(second.Id, Vector2.zero, 1, isLocked: false),
				SingleCardGeometry.CreateSpatialBody(first.Id, Vector2.zero, 1, isLocked: false)
			};
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(area, bodies);
			Vector2 firstPosition = result.GetPosition(first.Id);
			Vector2 secondPosition = result.GetPosition(second.Id);
			Assert.That<bool>(result.Converged, (IResolveConstraint)(object)Is.True);
			Assert.That<float>(firstPosition.x, (IResolveConstraint)(object)Is.LessThan((object)secondPosition.x));
			Assert.That<float>(secondPosition.x - firstPosition.x, (IResolveConstraint)(object)Is.GreaterThanOrEqualTo((object)2f));
		}

		[Test]
		public void Solve_MovesOnlyUnlockedBodyWhenOverlapIncludesLockedBody()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard locked = state.CreateCard("test.locked", Vector2.zero);
			TabletopCard movable = state.CreateCard("test.movable", new Vector2(0.5f, 0f));
			TabletopCardPlacementArea area = new TabletopCardPlacementArea(new Rect(-10f, -10f, 20f, 20f));
			TabletopCardStackSpatialBody[] bodies = new TabletopCardStackSpatialBody[2]
			{
				SingleCardGeometry.CreateSpatialBody(locked.Id, Vector2.zero, 1, isLocked: true),
				SingleCardGeometry.CreateSpatialBody(movable.Id, new Vector2(0.5f, 0f), 1, isLocked: false)
			};
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(area, bodies);
			Assert.That<bool>(result.Converged, (IResolveConstraint)(object)Is.True);
			Assert.That<Vector2>(result.GetPosition(locked.Id), (IResolveConstraint)(object)Is.EqualTo((object)Vector2.zero));
			Assert.That<float>(result.GetPosition(movable.Id).x, (IResolveConstraint)(object)Is.GreaterThanOrEqualTo((object)2f));
		}

		[Test]
		public void Solve_ReportsUnresolvedWhenBothOverlappingBodiesAreLocked()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard first = state.CreateCard("test.first", Vector2.zero);
			TabletopCard second = state.CreateCard("test.second", Vector2.zero);
			TabletopCardPlacementArea area = new TabletopCardPlacementArea(new Rect(-10f, -10f, 20f, 20f));
			TabletopCardStackSpatialBody[] bodies = new TabletopCardStackSpatialBody[2]
			{
				SingleCardGeometry.CreateSpatialBody(first.Id, Vector2.zero, 1, isLocked: true),
				SingleCardGeometry.CreateSpatialBody(second.Id, Vector2.zero, 1, isLocked: true)
			};
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(area, bodies);
			Assert.That<bool>(result.Converged, (IResolveConstraint)(object)Is.False);
			Assert.That<Vector2>(result.GetPosition(first.Id), (IResolveConstraint)(object)Is.EqualTo((object)Vector2.zero));
			Assert.That<Vector2>(result.GetPosition(second.Id), (IResolveConstraint)(object)Is.EqualTo((object)Vector2.zero));
		}

		[Test]
		public void Solve_PushesBodyOutOfRestrictedArea()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
			TabletopCardPlacementArea area = new TabletopCardPlacementArea(new Rect(-10f, -10f, 20f, 20f), (IReadOnlyList<Rect>)(object)new Rect[1]
			{
				new Rect(-1f, -1f, 2f, 2f)
			});
			TabletopCardStackSpatialBody body = SingleCardGeometry.CreateSpatialBody(tabletopCard.Id, Vector2.zero, 1, isLocked: false);
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(area, new TabletopCardStackSpatialBody[1] { body });
			Assert.That<bool>(result.Converged, (IResolveConstraint)(object)Is.True);
			Assert.That<float>(result.GetPosition(tabletopCard.Id).x, (IResolveConstraint)(object)Is.GreaterThanOrEqualTo((object)2f));
		}

		[Test]
		public void Solve_StackCraftHeaderBandSnapsTopEdgeDownAtBoardSide()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard tabletopCard = state.CreateCard("test.header-edge-stack", Vector2.zero);
			TabletopCardPlacementArea area = new TabletopCardPlacementArea(
				new Rect(-6f, -4f, 12f, 8f),
				new[] { new Rect(-6f, 2.5f, 12f, 1.5f) });
			TabletopCardStackGeometry geometry = new TabletopCardStackGeometry(
				new Vector2(0.8f, 1f),
				new Vector2(0f, -0.18f),
				new Vector2(0.1f, 0.1f));
			TabletopCardStackSpatialBody body = geometry.CreateSpatialBody(
				tabletopCard.Id,
				new Vector2(5.55f, 3.45f),
				3,
				isLocked: false);

			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				area,
				new TabletopCardStackSpatialBody[1] { body });

			Vector2 position = result.GetPosition(tabletopCard.Id);
			Assert.That(result.Converged, Is.True);
			Assert.That(position.x, Is.EqualTo(5.55f).Within(0.0001f));
			Assert.That(position.y, Is.EqualTo(1.95f).Within(0.0001f));
		}

		[Test]
		public void StackGeometry_UsesAllMembersForFootprintCenterAndSize()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard bottom = state.CreateCard("test.stack", Vector2.zero);
			TabletopCardStackSpatialBody body = new TabletopCardStackGeometry(new Vector2(1.4f, 2f), new Vector2(0.35f, 0.22f)).CreateSpatialBody(bottom.Id, new Vector2(2f, 3f), 3, isLocked: false);
			Assert.That<float>(Vector2.Distance(body.FootprintCenterOffset, new Vector2(0.35f, 0.22f)), (IResolveConstraint)(object)Is.LessThan((object)0.0001f));
			Assert.That<float>(Vector2.Distance(body.Size, new Vector2(2.1f, 2.44f)), (IResolveConstraint)(object)Is.LessThan((object)0.0001f));
		}
	}
}
