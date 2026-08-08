using NUnit.Framework;
using UnityEngine;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证卡牌堆栈从底到顶的表现位置与渲染顺序计算。
    /// </summary>
    public sealed class TabletopCardLayoutEditModeTests
    {
        [Test]
        public void Calculate_UsesBottomToTopIndexForVisualOffsetAndSorting()
        {
            var state = new TabletopCardState();
            TabletopCard bottom = state.CreateCard("test.bottom", new Vector2(3f, 4f));
            TabletopCard top = state.CreateCard("test.top", new Vector2(8f, 9f));
            TabletopCardStack stack = state.MergeStackOnto(top.Id, bottom.Id);
            var parameters = new TabletopCardLayoutParameters(new Vector3(0.1f, 0.2f, -0.05f), 10);

            TabletopCardPose bottomPose = TabletopCardLayout.Calculate(stack, 0, parameters);
            TabletopCardPose topPose = TabletopCardLayout.Calculate(stack, 1, parameters);

            Assert.That(bottomPose.LocalPosition, Is.EqualTo(new Vector3(3f, 4f, 0f)));
            Assert.That(topPose.LocalPosition, Is.EqualTo(new Vector3(3.1f, 4.2f, -0.05f)));
            Assert.That(bottomPose.SortingOrder, Is.EqualTo(10));
            Assert.That(topPose.SortingOrder, Is.EqualTo(11));
        }

        [Test]
        public void Calculate_RejectsAnIndexOutsideTheStack()
        {
            var state = new TabletopCardState();
            TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
            var parameters = new TabletopCardLayoutParameters(Vector3.zero, 0);

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => TabletopCardLayout.Calculate(
                    state.GetStackContaining(tabletopCard.Id),
                    1,
                    parameters));
        }
    }
}
