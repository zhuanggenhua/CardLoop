using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YokiFrame;
using Object = UnityEngine.Object;

namespace GamePlay.Tests
{
    /// <summary>
    /// 验证世界回合只有唯一负责系统可以确认，并把确认事实直接发布给 YokiFrame 订阅者。
    /// </summary>
    public sealed class GamePlayWorldTurnSystemEditModeTests
    {
        [Test]
        public void ConfirmTurn_IncrementsAuthoritativeIndexAndPublishesTheSameFact()
        {
            GameObject systemObject = new("GamePlayWorldTurnSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();
            var receivedTurnIndices = new List<int>();

            void OnWorldTurnConfirmed(GamePlayWorldTurnConfirmedEvent confirmedEvent)
            {
                receivedTurnIndices.Add(confirmedEvent.ConfirmedTurnIndex);
            }

            EventKit.Type.Register<GamePlayWorldTurnConfirmedEvent>(OnWorldTurnConfirmed);
            try
            {
                worldTurnSystem.OnSystemStart();

                Assert.That(worldTurnSystem.ConfirmedTurnIndex, Is.Zero);
                Assert.That(worldTurnSystem.ConfirmTurn(), Is.EqualTo(1));
                Assert.That(worldTurnSystem.ConfirmTurn(), Is.EqualTo(2));
                Assert.That(worldTurnSystem.ConfirmedTurnIndex, Is.EqualTo(2));
                CollectionAssert.AreEqual(new[] { 1, 2 }, receivedTurnIndices);
            }
            finally
            {
                EventKit.Type.UnRegister<GamePlayWorldTurnConfirmedEvent>(OnWorldTurnConfirmed);
                worldTurnSystem.OnSystemStop();
                worldTurnSystem.OnSystemShutdown();
                Object.DestroyImmediate(systemObject);
            }
        }

        [Test]
        public void ConfirmTurn_BeforeSystemStartFailsClearly()
        {
            GameObject systemObject = new("GamePlayWorldTurnSystemTests");
            GamePlayWorldTurnSystem worldTurnSystem = systemObject.AddComponent<GamePlayWorldTurnSystem>();

            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => worldTurnSystem.ConfirmTurn());
                StringAssert.Contains("尚未启动", exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(systemObject);
            }
        }
    }
}
