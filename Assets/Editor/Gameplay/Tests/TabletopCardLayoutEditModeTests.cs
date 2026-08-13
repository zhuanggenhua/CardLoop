using System;
using Gameplay.Tabletop;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证牌堆顺序到卡牌视图姿态的纯布局规则。
	/// </summary>
	public sealed class TabletopCardLayoutEditModeTests
	{
		[Test]
		public void Calculate_UsesBottomToTopIndexForVisualOffsetAndSorting()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard bottom = state.CreateCard("test.bottom", new Vector2(3f, 4f));
			TabletopCard top = state.CreateCard("test.top", new Vector2(8f, 9f));
			TabletopCardStack stack = state.MergeStackOnto(top.Id, bottom.Id);
			TabletopCardLayoutParameters parameters = new TabletopCardLayoutParameters(new Vector3(0.1f, 0.2f, -0.05f), 10);
			TabletopCardPose bottomPose = TabletopCardLayout.Calculate(stack, 0, parameters);
			TabletopCardPose topPose = TabletopCardLayout.Calculate(stack, 1, parameters);
			Assert.That<Vector3>(bottomPose.LocalPosition, (IResolveConstraint)(object)Is.EqualTo((object)new Vector3(3f, 4f, 0f)));
			Assert.That<Vector3>(topPose.LocalPosition, (IResolveConstraint)(object)Is.EqualTo((object)new Vector3(3.1f, 4.2f, -0.05f)));
			Assert.That<int>(bottomPose.SortingOrder, (IResolveConstraint)(object)Is.EqualTo((object)10));
			Assert.That<int>(topPose.SortingOrder, (IResolveConstraint)(object)Is.EqualTo((object)11));
		}

		[Test]
		public void Calculate_RejectsAnIndexOutsideTheStack()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
			TabletopCardLayoutParameters parameters = new TabletopCardLayoutParameters(Vector3.zero, 0);
			Assert.Throws<ArgumentOutOfRangeException>((TestDelegate)delegate
			{
				TabletopCardLayout.Calculate(state.GetStackContaining(tabletopCard.Id), 1, parameters);
			});
		}
	}
}
