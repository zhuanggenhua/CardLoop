using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GAS.Runtime;
using GameCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证内容作者资产经过 YooAsset 包发现后，能在正式启动链中进入唯一 ID 索引。
    /// </summary>
    public sealed class ContentRegistryPlayModeTests
    {
        [UnityTest]
        public IEnumerator FoundationScene_LoadsCardActionAndTurnTimingAuthorSourcesIntoIndex()
        {
            yield return SceneManager.LoadSceneAsync("FoundationTest", LoadSceneMode.Single);

            float timeoutAt = Time.realtimeSinceStartup + 20f;
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "GameManager 启动超时。");
                yield return null;
            }

            Assert.That(GameManager.StartupState, Is.EqualTo(GameManagerStartupState.Ready),
                GameManager.StartupException?.ToString());

            FoundationTestSceneHarness tabletopController =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            Assert.That(tabletopController, Is.Not.Null, "统一地基场景缺少牌桌测试装配器。");
            while (!tabletopController.IsReady)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "统一地基场景完成牌桌装配超时。");
                yield return null;
            }

            ContentRegistrySystem contentSystem = GameManager.GetSystem<ContentRegistrySystem>();
            Assert.That(contentSystem.IsInitialized, Is.True);
            Assert.That(
                contentSystem.Index.TryGet(
                    new ContentId("test.foundation.card"),
                    out ContentAsset contentAsset),
                Is.True);
            Assert.That(contentAsset, Is.TypeOf<CardDefinition>());

            var actionId = new ContentId("test.foundation.action");
            Assert.That(
                contentSystem.Index.TryGet(actionId, out ActionDefinition actionDefinition),
                Is.True);
            Assert.That(actionDefinition.DisplayName, Is.EqualTo("地基测试行动"));
            Assert.That(
                contentSystem.Index.TryGet(actionId, out CardDefinition _),
                Is.False,
                "行动作者源不能因为同样进入内容索引就被解释成卡牌作者源。");

            Assert.That(
                contentSystem.Index.TryGet(
                    new ContentId(FoundationTestSceneHarness.TestTurnTimingContentId),
                    out TurnTimingDefinition turnTiming),
                Is.True);
            Assert.That(turnTiming.SecondsPerTurn, Is.EqualTo(0.35f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator FoundationAction_MatchesExplicitParticipantSlotWithoutExecutingSideEffects()
        {
            yield return SceneManager.LoadSceneAsync("FoundationTest", LoadSceneMode.Single);

            float timeoutAt = Time.realtimeSinceStartup + 20f;
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "GameManager 启动超时。");
                yield return null;
            }

            Assert.That(GameManager.StartupState, Is.EqualTo(GameManagerStartupState.Ready),
                GameManager.StartupException?.ToString());

            FoundationTestSceneHarness tabletopController =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            Assert.That(tabletopController, Is.Not.Null, "统一地基场景缺少牌桌测试装配器。");
            while (!tabletopController.IsReady)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "统一地基场景完成牌桌装配超时。");
                yield return null;
            }

            ContentRegistrySystem contentSystem = GameManager.GetSystem<ContentRegistrySystem>();
            Assert.That(
                contentSystem.Index.TryGet(
                    new ContentId("test.foundation.card"),
                    out CardDefinition cardDefinition),
                Is.True);
            Assert.That(
                contentSystem.Index.TryGet(
                    new ContentId("test.foundation.action"),
                    out ActionDefinition actionDefinition),
                Is.True);

            Assert.That(actionDefinition.ParticipationSlots.Count, Is.EqualTo(1));
            ActionSlotDefinition slot = actionDefinition.ParticipationSlots[0];
            Assert.That(slot.Key, Is.EqualTo("participant"));
            Assert.That(ActionParticipationEvaluator.IsParticipantCountSatisfied(slot, 1), Is.False);
            Assert.That(ActionParticipationEvaluator.IsParticipantCountSatisfied(slot, 2), Is.True);
            Assert.That(ActionParticipationEvaluator.IsParticipantCountSatisfied(slot, 3), Is.False);

            ActionSlotDefinition unlimitedSlot = JsonUtility.FromJson<ActionSlotDefinition>(
                "{\"m_key\":\"rest\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":0}");
            Assert.That(ActionParticipationEvaluator.IsParticipantCountSatisfied(unlimitedSlot, 0), Is.False);
            Assert.That(ActionParticipationEvaluator.IsParticipantCountSatisfied(unlimitedSlot, 20), Is.True);
            Assert.That(
                ActionParticipationEvaluator.MatchesParticipant(unlimitedSlot, cardDefinition, null),
                Is.True,
                "没有角色动态标签条件的物品或工位槽位不应强制要求 AbilitySystemCell。");

            var matchingCell = new AbilitySystemCell();
            var nonMatchingCell = new AbilitySystemCell();
            var blockedCell = new AbilitySystemCell();
            CardDefinition wrongSymbolContent = CreateTransientCardDefinition(
                "test.foundation.card",
                XTag.State_Buff);
            CardDefinition blockedSymbolContent = CreateTransientCardDefinition(
                "test.foundation.card",
                XTag.Faction_Player,
                XTag.State_Debuff);
            CardDefinition wrongIdentityContent = CreateTransientCardDefinition(
                "test.foundation.other-card",
                XTag.Faction_Player);
            try
            {
                matchingCell.Init(
                    new[] { XTag.Ability_Gun_Shoot },
                    System.Array.Empty<AttrSetConfig>(),
                    System.Array.Empty<AbilityConfig>());
                nonMatchingCell.Init(
                    new[] { XTag.Ability_Magic },
                    System.Array.Empty<AttrSetConfig>(),
                    System.Array.Empty<AbilityConfig>());
                blockedCell.Init(
                    new[] { XTag.Ability_Gun_Shoot, XTag.State_Debuff },
                    System.Array.Empty<AttrSetConfig>(),
                    System.Array.Empty<AbilityConfig>());

                Assert.That(
                    ActionParticipationEvaluator.MatchesParticipant(
                        slot,
                        cardDefinition,
                        matchingCell),
                    Is.True,
                    "固定内容、内容符号和角色动态标签同时满足时，应允许进入该槽位。");
                Assert.That(
                    ActionParticipationEvaluator.MatchesParticipant(
                        slot,
                        actionDefinition,
                        matchingCell),
                    Is.False,
                    "槽位声明了固定内容时，不能把其它内容资产当作参与者。");
                Assert.That(
                    ActionParticipationEvaluator.MatchesParticipant(
                        slot,
                        cardDefinition,
                        nonMatchingCell),
                    Is.False,
                    "角色动态标签不满足时，不能只凭卡牌静态标签通过。");
                Assert.That(
                    ActionParticipationEvaluator.MatchesParticipant(
                        slot,
                        wrongSymbolContent,
                        matchingCell),
                    Is.False,
                    "唯一内容 ID 相同也不能绕过内容静态标签条件。");
                Assert.That(
                    ActionParticipationEvaluator.MatchesParticipant(
                        slot,
                        wrongIdentityContent,
                        matchingCell),
                    Is.False,
                    "内容符号满足时也不能绕过固定内容白名单。");
                Assert.That(
                    ActionParticipationEvaluator.MatchesParticipant(
                        slot,
                        blockedSymbolContent,
                        matchingCell),
                    Is.False,
                    "内容命中禁止标签时，必须拒绝进入槽位。");
                Assert.That(
                    ActionParticipationEvaluator.MatchesParticipant(
                        slot,
                        cardDefinition,
                        blockedCell),
                    Is.False,
                    "角色当前命中禁止标签时，必须拒绝进入槽位。");
                Assert.That(
                    ActionParticipationEvaluator.MatchesParticipant(slot, cardDefinition, null),
                    Is.False,
                    "声明了角色动态标签条件时，缺少 AbilitySystemCell 不能视为满足。");
            }
            finally
            {
                matchingCell.Dispose();
                nonMatchingCell.Dispose();
                blockedCell.Dispose();
                Object.Destroy(wrongSymbolContent);
                Object.Destroy(blockedSymbolContent);
                Object.Destroy(wrongIdentityContent);
            }
        }

        private static CardDefinition CreateTransientCardDefinition(
            string contentId,
            params int[] tagCodes)
        {
            CardDefinition content = ScriptableObject.CreateInstance<CardDefinition>();
            string tags = string.Join(",", tagCodes);
            JsonUtility.FromJsonOverwrite(
                $"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}},\"m_tagCodes\":[{tags}]}}",
                content);
            return content;
        }

        [UnityTest]
        public IEnumerator FoundationMap_SwitchesThroughSceneKit()
        {
            yield return SceneManager.LoadSceneAsync("FoundationTest", LoadSceneMode.Single);

            float timeoutAt = Time.realtimeSinceStartup + 20f;
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "GameManager 启动超时。");
                yield return null;
            }

            Assert.That(GameManager.StartupState, Is.EqualTo(GameManagerStartupState.Ready),
                GameManager.StartupException?.ToString());

            MapSystem mapSystem = GameManager.GetSystem<MapSystem>();
            var lifecycle = new List<string>();

            void OnTransitionStarted(MapTransitionStartedEvent _) => lifecycle.Add("TransitionStarted");
            void OnMapLoading(MapLoadingEvent _) => lifecycle.Add("MapLoading");
            void OnMapLoaded(MapLoadedEvent _) => lifecycle.Add("MapLoaded");
            void OnMapUnloading(MapUnloadingEvent _) => lifecycle.Add("MapUnloading");
            void OnMapUnloaded(MapUnloadedEvent _) => lifecycle.Add("MapUnloaded");
            void OnTransitionCompleted(MapTransitionCompletedEvent _) => lifecycle.Add("TransitionCompleted");

            YokiFrame.EventKit.Type.Register<MapTransitionStartedEvent>(OnTransitionStarted);
            YokiFrame.EventKit.Type.Register<MapLoadingEvent>(OnMapLoading);
            YokiFrame.EventKit.Type.Register<MapLoadedEvent>(OnMapLoaded);
            YokiFrame.EventKit.Type.Register<MapUnloadingEvent>(OnMapUnloading);
            YokiFrame.EventKit.Type.Register<MapUnloadedEvent>(OnMapUnloaded);
            YokiFrame.EventKit.Type.Register<MapTransitionCompletedEvent>(OnTransitionCompleted);
            try
            {
                yield return mapSystem.RequestTransitionAsync("FoundationMapTest").ToCoroutine();

                Assert.That(mapSystem.GetCurrentSceneAddress(), Is.EqualTo("FoundationMapTest"));
                Assert.That(YokiFrame.SceneKit.IsSceneLoaded("FoundationMapTest"), Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("FoundationMapTest"));
                Assert.That(
                    SceneManager.GetActiveScene().GetRootGameObjects()
                        .Any(root => root.name == "FoundationMapMarker"),
                    Is.True);
                CollectionAssert.AreEqual(
                    new[] { "TransitionStarted", "MapLoading", "MapLoaded", "TransitionCompleted" },
                    lifecycle);

                lifecycle.Clear();
                yield return mapSystem.RequestTransitionAsync("FoundationSecondMapTest").ToCoroutine();

                Assert.That(mapSystem.GetCurrentSceneAddress(), Is.EqualTo("FoundationSecondMapTest"));
                Assert.That(YokiFrame.SceneKit.IsSceneLoaded("FoundationMapTest"), Is.False);
                Assert.That(YokiFrame.SceneKit.IsSceneLoaded("FoundationSecondMapTest"), Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("FoundationSecondMapTest"));
                Assert.That(
                    SceneManager.GetActiveScene().GetRootGameObjects()
                        .Any(root => root.name == "FoundationSecondMapMarker"),
                    Is.True);
                Scene unloadedScene = SceneManager.GetSceneByName("FoundationMapTest");
                Assert.That(!unloadedScene.IsValid() || !unloadedScene.isLoaded, Is.True);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "TransitionStarted",
                        "MapUnloading",
                        "MapLoading",
                        "MapUnloaded",
                        "MapLoaded",
                        "TransitionCompleted"
                    },
                    lifecycle);
            }
            finally
            {
                YokiFrame.EventKit.Type.UnRegister<MapTransitionStartedEvent>(OnTransitionStarted);
                YokiFrame.EventKit.Type.UnRegister<MapLoadingEvent>(OnMapLoading);
                YokiFrame.EventKit.Type.UnRegister<MapLoadedEvent>(OnMapLoaded);
                YokiFrame.EventKit.Type.UnRegister<MapUnloadingEvent>(OnMapUnloading);
                YokiFrame.EventKit.Type.UnRegister<MapUnloadedEvent>(OnMapUnloaded);
                YokiFrame.EventKit.Type.UnRegister<MapTransitionCompletedEvent>(OnTransitionCompleted);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Exists())
            {
                Object.Destroy(GameManager.Instance.gameObject);
                yield return null;
            }

            yield return null;
        }
    }
}
