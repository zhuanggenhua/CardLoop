using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YokiFrame;
using Object = UnityEngine.Object;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证世界回合只有唯一负责系统可以确认，并把确认事实直接发布给 YokiFrame 订阅者。
    /// </summary>
    public sealed class ScenarioTurnSystemEditModeTests
    {
        [Test]
        public void ConfirmTurn_IncrementsAuthoritativeIndexAndPublishesTheSameFact()
        {
            GameObject systemObject = new("ScenarioTurnSystemTests");
            ScenarioTurnSystem scenarioTurnSystem = systemObject.AddComponent<ScenarioTurnSystem>();
            var receivedTurnIndices = new List<int>();

            void OnScenarioTurnConfirmed(ScenarioTurnConfirmedEvent confirmedEvent)
            {
                receivedTurnIndices.Add(confirmedEvent.ConfirmedTurnIndex);
            }

            EventKit.Type.Register<ScenarioTurnConfirmedEvent>(OnScenarioTurnConfirmed);
            try
            {
                scenarioTurnSystem.OnSystemStart();

                Assert.That(scenarioTurnSystem.ConfirmedTurnIndex, Is.Zero);
                Assert.That(scenarioTurnSystem.ConfirmTurn(), Is.EqualTo(1));
                Assert.That(scenarioTurnSystem.ConfirmTurn(), Is.EqualTo(2));
                Assert.That(scenarioTurnSystem.ConfirmedTurnIndex, Is.EqualTo(2));
                CollectionAssert.AreEqual(new[] { 1, 2 }, receivedTurnIndices);

                scenarioTurnSystem.ResetConfirmedTurns();
                Assert.That(scenarioTurnSystem.ConfirmedTurnIndex, Is.Zero);
            }
            finally
            {
                EventKit.Type.UnRegister<ScenarioTurnConfirmedEvent>(OnScenarioTurnConfirmed);
                scenarioTurnSystem.OnSystemStop();
                scenarioTurnSystem.OnSystemShutdown();
                Object.DestroyImmediate(systemObject);
            }
        }

        [Test]
        public void ConfirmTurn_BeforeSystemStartFailsClearly()
        {
            GameObject systemObject = new("ScenarioTurnSystemTests");
            ScenarioTurnSystem scenarioTurnSystem = systemObject.AddComponent<ScenarioTurnSystem>();

            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => scenarioTurnSystem.ConfirmTurn());
                StringAssert.Contains("尚未启动", exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(systemObject);
            }
        }
    }
}
