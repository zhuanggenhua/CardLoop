using NUnit.Framework;
using UnityEngine;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证设备无关的卡牌拖拽会话只产出点击或拖拽意图，不提前修改牌桌状态。
    /// </summary>
    public sealed class TabletopCardDragSessionEditModeTests
    {
        [Test]
        public void End_BelowThresholdProducesClickIntent()
        {
            var state = new TabletopCardState();
            TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
            var session = new TabletopCardDragSession(0.5f);

            session.Begin(tabletopCard.Id, new Vector2(1f, 2f));
            TabletopCardPointerReleaseIntent result = session.End(new Vector2(1.1f, 2.1f));

            Assert.That(result.CardId, Is.EqualTo(tabletopCard.Id));
            Assert.That(result.IsDrag, Is.False);
            Assert.That(session.IsActive, Is.False);
        }

        [Test]
        public void Update_CrossingThresholdKeepsDragStateUntilRelease()
        {
            var state = new TabletopCardState();
            TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
            var session = new TabletopCardDragSession(0.5f);

            session.Begin(tabletopCard.Id, Vector2.zero);

            Assert.That(session.Update(new Vector2(0.5f, 0f)), Is.True);
            Assert.That(session.Update(new Vector2(0.1f, 0f)), Is.True);

            TabletopCardPointerReleaseIntent result = session.End(new Vector2(0.1f, 0f));
            Assert.That(result.IsDrag, Is.True);
        }

        [Test]
        public void Cancel_ClearsSessionWithoutProducingIntent()
        {
            var state = new TabletopCardState();
            TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
            var session = new TabletopCardDragSession(0f);

            session.Begin(tabletopCard.Id, Vector2.zero);
            session.Update(Vector2.one);
            session.Cancel();

            Assert.That(session.IsActive, Is.False);
            Assert.That(session.IsDragging, Is.False);
            Assert.That(session.CardId.IsValid, Is.False);
        }

        [Test]
        public void End_PreservesSpatialTargetWithoutExecutingIt()
        {
            var state = new TabletopCardState();
            TabletopCard source = state.CreateCard("test.source", Vector2.zero);
            TabletopCard target = state.CreateCard("test.target", Vector2.one);
            var session = new TabletopCardDragSession(0.1f);

            session.Begin(source.Id, Vector2.zero);
            TabletopCardPointerReleaseIntent result = session.End(Vector2.one, target.Id);

            Assert.That(result.IsDrag, Is.True);
            Assert.That(result.TargetCardId, Is.EqualTo(target.Id));
            Assert.That(state.StackCount, Is.EqualTo(2), "意图产生阶段不能提前合并堆栈。");
        }
    }
}
