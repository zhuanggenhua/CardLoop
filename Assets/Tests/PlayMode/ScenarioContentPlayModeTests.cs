using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GAS.Runtime;
using GameCore;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YokiFrame;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using Gameplay.Tests.Support;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证内容作者资产经过 YooAsset 包发现后，只进入当前剧本单局的只读内容索引。
    /// </summary>
    public sealed class ScenarioContentPlayModeTests
    {
        private const string FoundationScenePath = "Assets/Scenes/地基测试.unity";
        private const string FoundationMapScenePath = "Assets/Scenes/地基地图测试.unity";
        private const string FoundationSecondMapScenePath = "Assets/Scenes/地基第二地图测试.unity";
        private string m_saveDirectory;

        [UnitySetUp]
        public IEnumerator ConfigureIsolatedSaveDirectory()
        {
            m_saveDirectory = Path.Combine(
                Application.temporaryCachePath,
                "Gameplay-ScenarioContentTests",
                System.Guid.NewGuid().ToString("N"));
            SaveSystem.ResetSaveKitConfigurationForTests();
            SaveSystem.ConfigureSaveKit(m_saveDirectory);
            yield return null;
        }

        [UnityTest]
		public IEnumerator FoundationScene_LoadsCardAndActionAuthorSourcesIntoIndex()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

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

			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			Assert.That(scenarioDirector.HasActiveScenario, Is.True);
			ContentIndex contentIndex = scenarioDirector.ActiveRun.ContentIndex;
			Assert.That(
				contentIndex.TryGet(
                    new ContentId("test.foundation.card"),
                    out ContentAsset contentAsset),
                Is.True);
			Assert.That(contentAsset, Is.TypeOf<CharacterCardDefinition>());

			var actionId = new ContentId("test.foundation.action");
			Assert.That(
				contentIndex.TryGet(actionId, out ActionDefinition actionDefinition),
                Is.True);
            Assert.That(actionDefinition.DisplayName, Is.EqualTo("测试行动"));
			Assert.That(
				contentIndex.TryGet(actionId, out CardDefinition _),
                Is.False,
                "行动作者源不能因为同样进入内容索引就被解释成卡牌作者源。");

        }

        [UnityTest]
        public IEnumerator FoundationAction_MatchesExplicitParticipantSlotWithoutExecutingSideEffects()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

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

			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			ContentIndex contentIndex = scenarioDirector.ActiveRun.ContentIndex;
			Assert.That(
				contentIndex.TryGet(
                    new ContentId("test.foundation.card"),
                    out CardDefinition cardDefinition),
                Is.True);
			Assert.That(
				contentIndex.TryGet(
                    new ContentId("test.foundation.action"),
                    out ActionDefinition actionDefinition),
                Is.True);

            Assert.That(actionDefinition.ParticipationSlots.Count, Is.EqualTo(1));
            ActionSlotDefinition slot = actionDefinition.ParticipationSlots[0];
            Assert.That(
                slot.Key,
                Is.EqualTo("slot-1"),
                "参与槽位的内部 key 应由作者源自动生成，测试资产不能要求策划手填。");
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
					new[] { XTag.State, XTag.Ability_Die },
                    System.Array.Empty<AttrSetConfig>(),
                    System.Array.Empty<AbilityConfig>());
                nonMatchingCell.Init(
                    new[] { XTag.Ability_Magic },
                    System.Array.Empty<AttrSetConfig>(),
                    System.Array.Empty<AbilityConfig>());
				blockedCell.Init(
					new[] { XTag.State, XTag.Ability_Die, XTag.State_Debuff },
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

        [UnityTest]
        public IEnumerator EndScenario_ReleasesItsContentResourceHandle()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

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

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            FieldInfo contentHandleField = typeof(ScenarioDirector).GetField(
                "m_contentHandle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(contentHandleField, Is.Not.Null);
            ResourceHandle<IList<ContentAsset>> contentHandle =
                (ResourceHandle<IList<ContentAsset>>)contentHandleField.GetValue(scenarioDirector);
            Assert.That(contentHandle.IsValid(), Is.True, "活动单局必须持有加载内容的有效资源句柄。");

            yield return scenarioDirector.EndScenarioAsync().ToCoroutine();

            Assert.That(scenarioDirector.HasActiveScenario, Is.False);
            Assert.That(contentHandle.IsValid(), Is.False, "结束单局必须释放该局内容资源句柄。");
        }

        [UnityTest]
        public IEnumerator EndScenario_AllowsASeparateFreshRun()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

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

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            ScenarioRun firstRun = scenarioDirector.ActiveRun;
            scenarioDirector.ConfirmTurn();
            Assert.That(firstRun.ConfirmedTurnIndex, Is.EqualTo(1));
            Assert.Throws<System.InvalidOperationException>(() =>
                scenarioDirector.StartScenarioAsync(firstRun.ScenarioId).GetAwaiter().GetResult());

            yield return scenarioDirector.EndScenarioAsync().ToCoroutine();
            yield return scenarioDirector.StartScenarioAsync(firstRun.ScenarioId).ToCoroutine();
            ScenarioRun secondRun = scenarioDirector.ActiveRun;

            Assert.That(secondRun, Is.Not.SameAs(firstRun));
            Assert.That(secondRun.ConfirmedTurnIndex, Is.Zero);
            Assert.That(secondRun.ScenarioId, Is.EqualTo(firstRun.ScenarioId));
            Assert.Throws<System.InvalidOperationException>(() => firstRun.ConfirmTurn());
        }

		[UnityTest]
		public IEnumerator SaveAndLoadSlot_RestoresRunAndRebindsTheVisibleTabletop()
		{
			const int testSlotId = 31;
			GameCore.SaveSystem.DeleteSaveData(testSlotId);
			yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
				FoundationScenePath,
				new LoadSceneParameters(LoadSceneMode.Single));

			float timeoutAt = Time.realtimeSinceStartup + 20f;
			while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, "GameManager 启动超时。");
				yield return null;
			}

			FoundationTestSceneHarness harness = Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			while (harness == null || !harness.IsReady)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, "统一地基场景完成牌桌装配超时。");
				harness = Object.FindAnyObjectByType<FoundationTestSceneHarness>();
				yield return null;
			}

			ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();
			ScenarioRun savedRun = director.ActiveRun;
			List<TabletopCardId> initialCardIds = savedRun.Tabletop.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.Select(card => card.Id)
				.ToList();
			for (int i = 0; i < initialCardIds.Count; i++)
			{
				savedRun.Tabletop.RemoveCard(initialCardIds[i]);
			}
			TabletopCard savedCard = savedRun.Tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestProductContentId),
				new Vector2(1.25f, -0.5f));
			CharacterCard savedCharacter = (CharacterCard)savedRun.Tabletop.CreateCard(
				new ContentId("test.foundation.card"),
				new Vector2(-1.25f, -0.5f));
			float savedHealth = savedCharacter.AbilitySystem.GetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.Health) - 1f;
			savedCharacter.AbilitySystem.SetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.Health,
				savedHealth);
			savedCharacter.AbilitySystem.SetLevel(4);
			director.ConfirmTurn();

			try
			{
				Assert.That(director.SaveActiveRunToSlot(testSlotId), Is.True);
				savedRun.Tabletop.RemoveCard(savedCard.Id);
				director.ConfirmTurn();
				Assert.That(savedRun.ConfirmedTurnIndex, Is.EqualTo(2));

				yield return director.LoadRunFromSlotAsync(testSlotId).ToCoroutine();
				yield return null;

				ScenarioRun restoredRun = director.ActiveRun;
				TabletopView tabletopView = Object.FindAnyObjectByType<TabletopView>();
				TabletopInteraction interaction = Object.FindAnyObjectByType<TabletopInteraction>();
				Assert.That(restoredRun, Is.Not.SameAs(savedRun));
				Assert.That(savedRun.IsEnded, Is.True);
				Assert.That(restoredRun.ConfirmedTurnIndex, Is.EqualTo(1));
				Assert.That(restoredRun.Tabletop.Cards.TryGetCard(savedCard.Id, out TabletopCard restoredCard), Is.True);
				Assert.That(restoredCard.ContentId.Value, Is.EqualTo(FoundationTestSceneHarness.TestProductContentId));
				Assert.That(
					restoredRun.Tabletop.Cards.TryGetCard(savedCharacter.Id, out TabletopCard restoredCharacterCard),
					Is.True);
				Assert.That(restoredCharacterCard, Is.TypeOf<CharacterCard>());
				CharacterCard restoredCharacter = (CharacterCard)restoredCharacterCard;
				Assert.That(restoredCharacter.AbilitySystem.GetLevel(), Is.EqualTo(4));
				Assert.That(
					restoredCharacter.AbilitySystem.GetAttrBaseValue(XAttrSet.FightUnit, XAttribute.Health),
					Is.EqualTo(savedHealth));
				Assert.That(harness.ScenarioRun, Is.SameAs(restoredRun));
				Assert.That(harness.Cards, Is.SameAs(restoredRun.Tabletop.Cards));
				Assert.That(tabletopView.BoundTabletop, Is.SameAs(restoredRun.Tabletop));
				Assert.That(interaction.IsBound, Is.True);
			}
			finally
			{
				GameCore.SaveSystem.DeleteSaveData(testSlotId);
			}
		}

        [UnityTest]
        public IEnumerator ScenarioDirector_ComposesConfiguredSceneAndReturnsOnEnd()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

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

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            yield return scenarioDirector.EndScenarioAsync().ToCoroutine();
            string mapSceneAddress = System.IO.Path.GetFileNameWithoutExtension(FoundationMapScenePath);
            string sourceSceneAddress = System.IO.Path.GetFileNameWithoutExtension(FoundationSecondMapScenePath);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationSecondMapScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sourceSceneAddress));

            bool targetSceneLoaded = false;
            bool runExistedWhenTargetSceneLoaded = false;
            void OnSceneLoaded(SceneLoadedEvent _)
            {
                if (!string.Equals(
                        SceneManager.GetActiveScene().name,
                        mapSceneAddress,
                        System.StringComparison.Ordinal))
                {
                    return;
                }

                targetSceneLoaded = true;
                runExistedWhenTargetSceneLoaded = scenarioDirector.HasActiveScenario;
            }

            YokiFrame.EventKit.Type.Register<SceneLoadedEvent>(OnSceneLoaded);
            try
            {
                UniTask startTask = scenarioDirector.StartScenarioAsync(
                    new ContentId(FoundationTestSceneHarness.TestSceneScenarioContentId));
                Assert.That(scenarioDirector.IsChangingScenario, Is.True);
                Assert.That(scenarioDirector.HasActiveScenario, Is.False,
                    "目标场景完成组合前不能发布活动单局。");

                yield return startTask.ToCoroutine();

                Assert.That(targetSceneLoaded, Is.True, "剧本配置的目标场景没有经过 SceneSystem 加载。");
                Assert.That(runExistedWhenTargetSceneLoaded, Is.False,
                    "SceneSystem 报告场景加载完成时，剧本导演不应提前暴露尚未组合完成的单局。");
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(mapSceneAddress));
                Assert.That(scenarioDirector.ActiveScenarioId, Is.EqualTo(
                    new ContentId(FoundationTestSceneHarness.TestSceneScenarioContentId)));

                ScenarioRun sceneRun = scenarioDirector.ActiveRun;
                FieldInfo contentHandleField = typeof(ScenarioDirector).GetField(
                    "m_contentHandle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(contentHandleField, Is.Not.Null);
                ResourceHandle<IList<ContentAsset>> contentHandle =
                    (ResourceHandle<IList<ContentAsset>>)contentHandleField.GetValue(scenarioDirector);
                Assert.That(contentHandle.IsValid(), Is.True);

				Assert.That(
					sceneRun.ContentIndex.TryGet(
						new ContentId(FoundationTestSceneHarness.TestContentId),
						out CardDefinition travelerDefinition),
					Is.True);
				ScenarioRegion sourceRegion = sceneRun.ActiveRegion;
				TabletopCard stayingCard = sceneRun.Tabletop.CreateCard(
					travelerDefinition.ContentId,
					Vector2.left);
				CharacterCard traveler = sceneRun.Tabletop.CreateCard(
					travelerDefinition.ContentId,
					Vector2.right) as CharacterCard;
				Assert.That(traveler, Is.Not.Null);
				AbilitySystemCell travelerAbilitySystem = traveler.AbilitySystem;

				yield return scenarioDirector.TravelAsync(
					new ContentId(FoundationTestSceneHarness.TestSecondSceneRegionContentId),
					new[] { traveler.Id }).ToCoroutine();

				Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sourceSceneAddress));
				Assert.That(
					sceneRun.ActiveRegion.Id,
					Is.EqualTo(new ContentId(FoundationTestSceneHarness.TestSecondSceneRegionContentId)));
				Assert.That(sourceRegion.Tabletop.Cards.TryGetCard(stayingCard.Id, out _), Is.True,
					"离开地区后，未旅行卡牌必须继续保存在原地区牌桌。");
				Assert.That(sourceRegion.Tabletop.Cards.TryGetCard(traveler.Id, out _), Is.False);
				Assert.That(sceneRun.Tabletop.Cards.TryGetCard(traveler.Id, out TabletopCard moved), Is.True);
				Assert.That(moved, Is.SameAs(traveler));
				Assert.That(traveler.AbilitySystem, Is.SameAs(travelerAbilitySystem));
				Assert.That(traveler.AbilitySystem.HasTag(XTag.State), Is.True,
					"角色跨地区后必须保留原 EX-GAS 状态，不能从内容定义重建角色副本。");

				yield return scenarioDirector.TravelAsync(
					new ContentId(FoundationTestSceneHarness.TestSceneRegionContentId),
					new[] { traveler.Id }).ToCoroutine();
				Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(mapSceneAddress));
				Assert.That(sceneRun.ActiveRegion, Is.SameAs(sourceRegion));
				Assert.That(sourceRegion.Tabletop.Cards.TryGetCard(stayingCard.Id, out _), Is.True);
				Assert.That(sourceRegion.Tabletop.Cards.TryGetCard(traveler.Id, out TabletopCard returned), Is.True);
				Assert.That(returned, Is.SameAs(traveler),
					"重返地区必须继续使用原地区牌桌和原旅行卡牌实例。");

                UniTask endTask = scenarioDirector.EndScenarioAsync();
                Assert.That(scenarioDirector.IsChangingScenario, Is.True);
                Assert.That(scenarioDirector.HasActiveScenario, Is.False,
                    "结束剧本后必须先关闭旧单局，再返回来源场景。");
                Assert.Throws<System.InvalidOperationException>(() => sceneRun.ConfirmTurn());

                yield return endTask.ToCoroutine();

                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sourceSceneAddress));
                Assert.That(contentHandle.IsValid(), Is.False,
                    "场景型剧本结束后必须释放本局内容资源句柄。");
            }
            finally
            {
                YokiFrame.EventKit.Type.UnRegister<SceneLoadedEvent>(OnSceneLoaded);
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
        public IEnumerator FoundationScene_SwitchesThroughSceneKit()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

            float timeoutAt = Time.realtimeSinceStartup + 20f;
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "GameManager 启动超时。");
                yield return null;
            }

            Assert.That(GameManager.StartupState, Is.EqualTo(GameManagerStartupState.Ready),
                GameManager.StartupException?.ToString());

            SceneSystem sceneSystem = GameManager.GetSystem<SceneSystem>();
            var lifecycle = new List<string>();

            void OnTransitionStarted(SceneTransitionStartedEvent _) => lifecycle.Add("TransitionStarted");
            void OnSceneLoading(SceneLoadingEvent _) => lifecycle.Add("SceneLoading");
            void OnSceneLoaded(SceneLoadedEvent _) => lifecycle.Add("SceneLoaded");
            void OnSceneUnloading(SceneUnloadingEvent _) => lifecycle.Add("SceneUnloading");
            void OnSceneUnloaded(SceneUnloadedEvent _) => lifecycle.Add("SceneUnloaded");
            void OnTransitionCompleted(SceneTransitionCompletedEvent _) => lifecycle.Add("TransitionCompleted");
            void OnTransitionEnded(SceneTransitionEndedEvent _) => lifecycle.Add("TransitionEnded");

            YokiFrame.EventKit.Type.Register<SceneTransitionStartedEvent>(OnTransitionStarted);
            YokiFrame.EventKit.Type.Register<SceneLoadingEvent>(OnSceneLoading);
            YokiFrame.EventKit.Type.Register<SceneLoadedEvent>(OnSceneLoaded);
            YokiFrame.EventKit.Type.Register<SceneUnloadingEvent>(OnSceneUnloading);
            YokiFrame.EventKit.Type.Register<SceneUnloadedEvent>(OnSceneUnloaded);
            YokiFrame.EventKit.Type.Register<SceneTransitionCompletedEvent>(OnTransitionCompleted);
            YokiFrame.EventKit.Type.Register<SceneTransitionEndedEvent>(OnTransitionEnded);
            try
            {
                yield return sceneSystem.TransitionToAsync("地基地图测试").ToCoroutine();

                Assert.That(sceneSystem.CurrentSceneAddress, Is.EqualTo("地基地图测试"));
                Assert.That(YokiFrame.SceneKit.IsSceneLoaded("地基地图测试"), Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("地基地图测试"));
                Assert.That(
                    SceneManager.GetActiveScene().GetRootGameObjects()
                        .Any(root => root.name == "地基地图测试标记"),
                    Is.True);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "TransitionStarted",
                        "SceneLoading",
                        "SceneLoaded",
                        "TransitionCompleted",
                        "TransitionEnded"
                    },
                    lifecycle);

                lifecycle.Clear();
                yield return sceneSystem.TransitionToAsync("地基第二地图测试").ToCoroutine();

                Assert.That(sceneSystem.CurrentSceneAddress, Is.EqualTo("地基第二地图测试"));
                Assert.That(YokiFrame.SceneKit.IsSceneLoaded("地基地图测试"), Is.False);
                Assert.That(YokiFrame.SceneKit.IsSceneLoaded("地基第二地图测试"), Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("地基第二地图测试"));
                Assert.That(
                    SceneManager.GetActiveScene().GetRootGameObjects()
                        .Any(root => root.name == "地基第二地图测试标记"),
                    Is.True);
                Scene unloadedScene = SceneManager.GetSceneByName("地基地图测试");
                Assert.That(!unloadedScene.IsValid() || !unloadedScene.isLoaded, Is.True);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "TransitionStarted",
                        "SceneUnloading",
                        "SceneLoading",
                        "SceneUnloaded",
                        "SceneLoaded",
                        "TransitionCompleted",
                        "TransitionEnded"
                    },
                    lifecycle);
            }
            finally
            {
                YokiFrame.EventKit.Type.UnRegister<SceneTransitionStartedEvent>(OnTransitionStarted);
                YokiFrame.EventKit.Type.UnRegister<SceneLoadingEvent>(OnSceneLoading);
                YokiFrame.EventKit.Type.UnRegister<SceneLoadedEvent>(OnSceneLoaded);
                YokiFrame.EventKit.Type.UnRegister<SceneUnloadingEvent>(OnSceneUnloading);
                YokiFrame.EventKit.Type.UnRegister<SceneUnloadedEvent>(OnSceneUnloaded);
                YokiFrame.EventKit.Type.UnRegister<SceneTransitionCompletedEvent>(OnTransitionCompleted);
                YokiFrame.EventKit.Type.UnRegister<SceneTransitionEndedEvent>(OnTransitionEnded);
            }
        }

        [UnityTest]
        public IEnumerator FoundationScene_ExplicitAdditiveUnloadReleasesPackageUsage()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

            float timeoutAt = Time.realtimeSinceStartup + 20f;
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "GameManager 启动超时。");
                yield return null;
            }

            Assert.That(GameManager.StartupState, Is.EqualTo(GameManagerStartupState.Ready),
                GameManager.StartupException?.ToString());

            ISceneLoaderPool sceneLoaderPool = YokiFrame.SceneKit.GetLoaderPool();
            ISceneLoader sceneLoader = sceneLoaderPool.Allocate();
            Scene loadedScene = default;
            bool loadCompleted = false;
            bool unloadCompleted = false;

            sceneLoader.LoadAsync(
                "地基地图测试",
                YokiFrame.SceneLoadMode.Additive,
                scene =>
                {
                    loadedScene = scene;
                    loadCompleted = true;
                });

            timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!loadCompleted)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "附加场景加载超时。");
                yield return null;
            }

            string packageName = ResourceSystem.DefaultPackage.PackageName;
            bool usedBeforeUnload = IsPackageInUse(sceneLoaderPool, packageName);

            sceneLoader.UnloadAsync(loadedScene, () => unloadCompleted = true);
            timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!unloadCompleted)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "附加场景卸载超时。");
                yield return null;
            }

            bool usedAfterUnload = IsPackageInUse(sceneLoaderPool, packageName);
            sceneLoaderPool.Recycle(sceneLoader);

            Assert.That(usedBeforeUnload, Is.True, "加载完成后，资源包必须被标记为正在使用。");
            Assert.That(usedAfterUnload, Is.False, "显式卸载完成后，资源包不能继续被场景加载器占用。");
            Scene unloadedScene = SceneManager.GetSceneByName("地基地图测试");
            Assert.That(!unloadedScene.IsValid() || !unloadedScene.isLoaded, Is.True);
        }

        private static bool IsPackageInUse(ISceneLoaderPool sceneLoaderPool, string packageName)
        {
            MethodInfo usesPackage = sceneLoaderPool.GetType().GetMethod(
                "UsesPackage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(usesPackage, Is.Not.Null,
                "项目 SceneKit 场景后端必须提供资源包占用校验，供资源系统拒绝卸载正在使用的 Mod 包。");

            return (bool)usesPackage.Invoke(sceneLoaderPool, new object[] { packageName });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Exists())
            {
                Object.Destroy(GameManager.Instance.gameObject);
                yield return null;
            }

            SaveSystem.ResetSaveKitConfigurationForTests();
            if (!string.IsNullOrWhiteSpace(m_saveDirectory) && Directory.Exists(m_saveDirectory))
            {
                Directory.Delete(m_saveDirectory, true);
            }

            yield return null;
        }
    }
}
