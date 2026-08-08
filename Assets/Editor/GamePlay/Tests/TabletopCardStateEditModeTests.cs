using NUnit.Framework;
using UnityEngine;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证局内卡牌身份、卡牌堆栈顺序、拆分、合并和位置锁定不变量。
    /// </summary>
    public sealed class TabletopCardStateEditModeTests
    {
        [Test]
        public void CreateCard_SameContentCreatesDifferentRuntimeCards()
        {
            var state = new TabletopCardState();
            var contentId = new ContentId("test.wood");

            TabletopCard first = state.CreateCard(contentId, new Vector2(1f, 2f));
            TabletopCard second = state.CreateCard(contentId, new Vector2(3f, 4f));

            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(first.ContentId, Is.EqualTo(contentId));
            Assert.That(second.ContentId, Is.EqualTo(contentId));
            Assert.That(state.CardCount, Is.EqualTo(2));
            Assert.That(state.StackCount, Is.EqualTo(2));
        }

        [Test]
        public void MergeStackOnto_PreservesBottomToTopOrderAndTargetPosition()
        {
            var state = new TabletopCardState();
            TabletopCard target = state.CreateCard("test.target", new Vector2(1f, 2f));
            TabletopCard source = state.CreateCard("test.source", new Vector2(8f, 9f));

            TabletopCardStack merged = state.MergeStackOnto(source.Id, target.Id);

            Assert.That(state.StackCount, Is.EqualTo(1));
            Assert.That(merged.Position, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(merged.Cards, Is.EqualTo(new[] { target, source }));
            Assert.That(state.GetStackContaining(source.Id), Is.SameAs(merged));
            Assert.That(state.GetStackContaining(target.Id), Is.SameAs(merged));
        }

        [Test]
        public void DetachStackAt_SelectedCardAndCardsAboveFormNewStack()
        {
            var state = new TabletopCardState();
            TabletopCard bottom = state.CreateCard("test.bottom", new Vector2(2f, 3f));
            TabletopCard middle = state.CreateCard("test.middle", new Vector2(4f, 5f));
            TabletopCard top = state.CreateCard("test.top", new Vector2(6f, 7f));
            state.MergeStackOnto(middle.Id, bottom.Id);
            TabletopCardStack original = state.MergeStackOnto(top.Id, middle.Id);

            TabletopCardStack detached = state.DetachStackAt(middle.Id);

            Assert.That(state.StackCount, Is.EqualTo(2));
            Assert.That(original.Cards, Is.EqualTo(new[] { bottom }));
            Assert.That(detached.Cards, Is.EqualTo(new[] { middle, top }));
            Assert.That(original.Position, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(detached.Position, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(state.GetStackContaining(bottom.Id), Is.SameAs(original));
            Assert.That(state.GetStackContaining(middle.Id), Is.SameAs(detached));
            Assert.That(state.GetStackContaining(top.Id), Is.SameAs(detached));
        }

        [Test]
        public void LockedBottom_RejectsWholeStackMoveButAllowsDetachingCardsAbove()
        {
            var state = new TabletopCardState();
            TabletopCard fixedBottom = state.CreateCard(
                "test.fixed-bottom",
                new Vector2(2f, 3f),
                isPlacementLocked: true);
            TabletopCard movableTop = state.CreateCard("test.movable-top", new Vector2(4f, 5f));
            state.MergeStackOnto(movableTop.Id, fixedBottom.Id);

            Assert.Throws<System.InvalidOperationException>(
                () => state.MoveStack(movableTop.Id, new Vector2(8f, 9f)));

            TabletopCardStack detached = state.DetachStackAt(movableTop.Id);
            state.MoveStack(movableTop.Id, new Vector2(8f, 9f));

            Assert.That(detached.IsPlacementLocked, Is.False);
            Assert.That(detached.Position, Is.EqualTo(new Vector2(8f, 9f)));
            Assert.That(state.GetStackContaining(fixedBottom.Id).Position, Is.EqualTo(new Vector2(2f, 3f)));
        }

        [Test]
        public void MergeStackOnto_WhenSourceIsLockedRejectsMutation()
        {
            var state = new TabletopCardState();
            TabletopCard lockedSource = state.CreateCard(
                "test.locked-source",
                new Vector2(2f, 3f),
                isPlacementLocked: true);
            TabletopCard target = state.CreateCard("test.target", new Vector2(8f, 9f));

            Assert.Throws<System.InvalidOperationException>(
                () => state.MergeStackOnto(lockedSource.Id, target.Id));

            Assert.That(state.StackCount, Is.EqualTo(2));
            Assert.That(state.GetStackContaining(lockedSource.Id).Position, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(state.GetStackContaining(target.Id).Position, Is.EqualTo(new Vector2(8f, 9f)));
        }
    }
}
