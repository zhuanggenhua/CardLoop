using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using InvalidOperationException = System.InvalidOperationException;
using GAS.Runtime;
using GameCore;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using InputSystemApi = UnityEngine.InputSystem.InputSystem;
using YokiFrame;
using YooAsset;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using Gameplay.Tests.Support;
using RuntimeTabletop = Gameplay.Tabletop.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证地基场景通过真实 YooAsset、正式输入 owner 和物理命中跑通牌桌拖拽表现链路。
    /// </summary>
	public sealed class FoundationTestScenePlayModeTests : InputTestFixture
	{
		private const string FoundationScenePath = "Assets/Scenes/FoundationTest.unity";

		[UnityTest]
		public IEnumerator FoundationTabletop_HoverTemporarilyOverridesSelectedReadableCard()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationReadableCardMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationReadableCardKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			TabletopView tabletopView = Object.FindAnyObjectByType<TabletopView>();
			TabletopCardInfoPanel infoPanel = Object.FindAnyObjectByType<TabletopCardInfoPanel>();
			TabletopCardView selectedView = FindView(controller.MiddleCardId);
			TabletopCardView hoveredView = FindView(controller.TargetCardId);
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			yield return null;

			Physics.SyncTransforms();
			Camera camera = Camera.main;
			BoxCollider selectedCollider = selectedView.GetComponent<BoxCollider>();
			Vector3 exposedSelectedPoint = selectedCollider.bounds.center +
				Vector3.left * selectedCollider.bounds.extents.x * 0.82f;
			Vector2 selectedScreenPosition = camera.WorldToScreenPoint(exposedSelectedPoint);
			Vector2 hoveredScreenPosition = camera.WorldToScreenPoint(hoveredView.transform.position);
			Vector2 emptyScreenPosition = new(Screen.width * 0.5f, Screen.height * 0.9f);

			Move(mouse.position, selectedScreenPosition);
			yield return null;
			Assert.That(tabletopView.HoveredCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(tabletopView.ReadableCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(infoPanel.DisplayedCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(infoPanel.DisplayedTitle, Is.EqualTo("Foundation Test Card"));
			Assert.That(infoPanel.DisplayedDescription, Does.Contain("YooAsset content discovery"));

			Press(mouse.leftButton);
			yield return null;
			Release(mouse.leftButton);
			yield return null;
			Assert.That(tabletopView.SelectedCardId, Is.EqualTo(controller.MiddleCardId));

			Move(mouse.position, hoveredScreenPosition);
			yield return null;
			Assert.That(tabletopView.HoveredCardId, Is.EqualTo(controller.TargetCardId));
			Assert.That(tabletopView.ReadableCardId, Is.EqualTo(controller.TargetCardId));
			Assert.That(infoPanel.DisplayedCardId, Is.EqualTo(controller.TargetCardId));

			Move(mouse.position, emptyScreenPosition);
			yield return null;
			Assert.That(tabletopView.HoveredCardId.IsValid, Is.False);
			Assert.That(tabletopView.SelectedCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(tabletopView.ReadableCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(infoPanel.DisplayedCardId, Is.EqualTo(controller.MiddleCardId));
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_RemovingReadableCardClearsCardInfo()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationRemovedReadableCardMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationRemovedReadableCardKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			TabletopView tabletopView = Object.FindAnyObjectByType<TabletopView>();
			TabletopCardInfoPanel infoPanel = Object.FindAnyObjectByType<TabletopCardInfoPanel>();
			TabletopCardView targetView = FindView(controller.TargetCardId);
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			yield return null;

			Physics.SyncTransforms();
			Vector2 targetScreenPosition = Camera.main.WorldToScreenPoint(targetView.transform.position);
			Move(mouse.position, targetScreenPosition);
			yield return null;
			Press(mouse.leftButton);
			yield return null;
			Release(mouse.leftButton);
			yield return null;
			Assert.That(infoPanel.DisplayedCardId, Is.EqualTo(controller.TargetCardId));

			Move(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.9f));
			yield return null;
			controller.ScenarioRun.Tabletop.RemoveCard(controller.TargetCardId);
			yield return null;
			yield return null;

			Assert.That(tabletopView.ReadableCardId.IsValid, Is.False);
			Assert.That(infoPanel.DisplayedCardId.IsValid, Is.False);
			Assert.That(infoPanel.DisplayedTitle, Is.Empty);
			Assert.That(infoPanel.DisplayedDescription, Is.Empty);
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_CharacterCardOwnsAndReleasesItsAbilitySystem()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			RuntimeTabletop tabletop = controller.ScenarioRun.Tabletop;
			CharacterCard characterCard = tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				new Vector2(0f, 2f)) as CharacterCard;

			Assert.That(characterCard, Is.Not.Null);
			AbilitySystemCell abilitySystem = characterCard.AbilitySystem;
			Assert.That(abilitySystem.HasTag(XTag.State), Is.True);

			tabletop.RemoveCard(characterCard.Id);

			Assert.That(
				abilitySystem.Entity,
				Is.EqualTo(Unity.Entities.Entity.Null),
				"移除角色卡后必须直接释放其唯一 EX-GAS 实体，不能再维护第二份释放状态。");
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_CharacterCardViewProjectsLiveGasHealth()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			Assert.That(
				controller.Cards.TryGetCard(controller.TargetCardId, out TabletopCard tabletopCard),
				Is.True);
			CharacterCard character = (CharacterCard)tabletopCard;
			TabletopCardView view = FindView(character.Id);
			TabletopCardView bottomView = FindView(controller.BottomCardId);
			TabletopCardView middleView = FindView(controller.MiddleCardId);
			TabletopCardView topView = FindView(controller.TopCardId);

			Assert.That(view.DisplaysCharacterStatus, Is.True);
			Assert.That(view.DisplayedHealthText, Is.EqualTo("100/100"));
			Assert.That(bottomView.DisplaysCharacterStatus, Is.False);
			Assert.That(middleView.DisplaysCharacterStatus, Is.False);
			Assert.That(topView.DisplaysCharacterStatus, Is.True);

			character.AbilitySystem.SetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.Health,
				73f);
			float timeoutAt = Time.realtimeSinceStartup + 2f;
			while (view.DisplayedHealthText != "73/100")
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"角色唯一 EX-GAS 生命已经改变，但角色卡状态投影没有同步更新。");
				yield return null;
			}

			character.AbilitySystem.SetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.MaxHealth,
				120f);
			while (view.DisplayedHealthText != "73/120")
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"角色唯一 EX-GAS 生命上限已经改变，但角色卡状态投影没有同步更新。");
				yield return null;
			}
		}

        [UnityTest]
        public IEnumerator FoundationRuntime_DestroyingGameManager_ReleasesOwnedInfrastructure()
        {
            yield return LoadFoundationTabletop();

            Assert.That(ResourceSystem.Initialized, Is.True);
            Assert.That(ModAPI.Initialized, Is.True);
            Assert.That(GASManager.IsInitialized, Is.True);
            Assert.That(GASManager.IsRunning, Is.True);
            Assert.That(YooInit.Initialized, Is.True);
            Assert.That(YooAssets.IsInitialized, Is.True);

            Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
            yield return null;

            Assert.That(GameManager.Exists(), Is.False);
            Assert.That(ResourceSystem.Initialized, Is.False);
            Assert.That(ModAPI.Initialized, Is.False);
            Assert.That(GASManager.IsInitialized, Is.False);
            Assert.That(GASManager.IsRunning, Is.False);
            Assert.That(GASManager.ExWorld, Is.Null);
            Assert.That(YooInit.Initialized, Is.False);
            Assert.That(YooAssets.IsInitialized, Is.False);
        }

        [UnityTest]
        public IEnumerator FoundationInput_UsesOnePlayerInputAndOneUIKitEventSystem()
        {
            yield return LoadFoundationTabletop();

            PlayerInput[] playerInputs = Object.FindObjectsByType<PlayerInput>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            StandaloneInputModule[] legacyInputModules = Object.FindObjectsByType<StandaloneInputModule>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            Assert.That(playerInputs, Has.Length.EqualTo(1));
            Assert.That(eventSystems, Has.Length.EqualTo(1));
            Assert.That(legacyInputModules, Is.Empty);

            PlayerInput playerInput = playerInputs[0];
            EventSystem eventSystem = eventSystems[0];
            InputSystemUIInputModule uiInputModule = eventSystem.GetComponent<InputSystemUIInputModule>();

            Assert.That(UIRoot.ExistingInstance, Is.Not.Null);
            Assert.That(UIRoot.ExistingInstance.EventSystem, Is.SameAs(eventSystem));
            Assert.That(uiInputModule, Is.Not.Null);
            Assert.That(uiInputModule.actionsAsset, Is.SameAs(playerInput.actions));
            Assert.That(playerInput.uiInputModule, Is.SameAs(uiInputModule));
        }

		[UnityTest]
		public IEnumerator FoundationTabletop_InstantiatesViewsAndLoadsArtworkThroughYooAsset()
		{
            yield return LoadFoundationTabletop();

            FoundationTestSceneHarness controller =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            TabletopCardView[] views = Object.FindObjectsByType<TabletopCardView>();

            Assert.That(controller.Cards.CardCount, Is.EqualTo(4));
            Assert.That(controller.Cards.StackCount, Is.EqualTo(2));
            Assert.That(views, Has.Length.EqualTo(4));
            Assert.That(views.All(view => view.GetComponent<BoxCollider>() != null), Is.True);
            Assert.That(
                views.All(view => view.GetComponent<SpriteRenderer>()?.sprite?.name == "Square"),
                Is.True,
				"卡面必须由内容作者源地址经过 ResourceSystem/YooAsset 写入视图。");
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_DisablingConsumerUnbindsViewProjection()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			TabletopView tabletopView = Object.FindAnyObjectByType<TabletopView>();
			Assert.That(tabletopView.IsBound, Is.True);

			controller.enabled = false;
			yield return null;

			Assert.That(tabletopView.IsBound, Is.False);
			Assert.That(Object.FindObjectsByType<TabletopCardView>(), Is.Empty);
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_BattleProjectsFormationAndKeepsParticipantsOnTheTableUntilItEnds()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			RuntimeTabletop tabletop = controller.ScenarioRun.Tabletop;
			TabletopCardView playerView = FindView(controller.BottomCardId);
			TabletopCardView enemyView = FindView(controller.TargetCardId);
			Vector3 playerStackViewPosition = playerView.transform.localPosition;
			Vector3 enemyStackViewPosition = enemyView.transform.localPosition;
			Vector2 playerStackPosition = controller.Cards
				.GetStackContaining(controller.BottomCardId)
				.Position;
			Vector2 enemyStackPosition = controller.Cards
				.GetStackContaining(controller.TargetCardId)
				.Position;
			Vector2 battleAnchor = (playerStackPosition + enemyStackPosition) * 0.5f;
			Battle battle = tabletop.StartBattle(
				new[] { controller.BottomCardId },
				new[] { controller.TargetCardId });
			yield return null;

			Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(1));
			Assert.That(battle.Sides, Has.Count.EqualTo(2));
			CollectionAssert.AreEqual(new[] { controller.BottomCardId }, battle.Sides[0].CardIds);
			CollectionAssert.AreEqual(new[] { controller.TargetCardId }, battle.Sides[1].CardIds);
			Assert.That(
				Vector3.Distance(
					playerView.transform.localPosition,
					new Vector3(battleAnchor.x - 1.5f, battleAnchor.y, 0f)),
				Is.LessThan(0.001f),
				"开战后玩家阵营卡牌必须进入剧本阵型派生的位置。");
			Assert.That(
				Vector3.Distance(
					enemyView.transform.localPosition,
					new Vector3(battleAnchor.x + 1.5f, battleAnchor.y, 0f)),
				Is.LessThan(0.001f),
				"开战后敌对阵营卡牌必须进入剧本阵型派生的位置。");
			Assert.That(playerView.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(100));
			Assert.That(enemyView.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(101));
			Assert.That(
				controller.Cards.GetStackContaining(controller.BottomCardId).Position,
				Is.EqualTo(playerStackPosition),
				"阵型表现不能改写玩家卡牌的权威牌堆位置。");
			Assert.That(
				controller.Cards.GetStackContaining(controller.TargetCardId).Position,
				Is.EqualTo(enemyStackPosition),
				"阵型表现不能改写敌对卡牌的权威牌堆位置。");
			Assert.Throws<InvalidOperationException>(() => tabletop.RemoveCard(controller.TargetCardId));

			tabletop.LeaveBattle(battle, controller.TargetCardId);
			yield return null;

			Assert.That(battle.IsEnded, Is.True);
			Assert.That(tabletop.ActiveBattles, Is.Empty);
			Assert.That(
				Vector3.Distance(playerView.transform.localPosition, playerStackViewPosition),
				Is.LessThan(0.001f),
				"战斗结束后玩家卡牌必须回到权威牌堆视图位置。");
			Assert.That(
				Vector3.Distance(enemyView.transform.localPosition, enemyStackViewPosition),
				Is.LessThan(0.001f),
				"战斗结束后敌对卡牌必须回到权威牌堆视图位置。");
			Assert.That(playerView.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(10));
			Assert.That(enemyView.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(10));
			Assert.DoesNotThrow(() => tabletop.RemoveCard(controller.TargetCardId));
			Assert.That(controller.Cards.CardCount, Is.EqualTo(3));
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_BattleAbilityAppliesConfiguredDamageToTargetGasHealth()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			RuntimeTabletop tabletop = controller.ScenarioRun.Tabletop;
			CharacterCard attacker = (CharacterCard)tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				new Vector2(-1f, 2f));
			CharacterCard target = (CharacterCard)tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				new Vector2(1f, 2f));
			Battle battle = tabletop.StartBattle(
				new[] { attacker.Id },
				new[] { target.Id });
			float healthBefore = target.AbilitySystem.GetAttrCurrentValue(
				XAttrSet.FightUnit,
				XAttribute.Health);

			AbilityActivationResult result = tabletop.RequestBattleAbilityActivation(
				battle,
				attacker.Id,
				target.Id,
				XAbility.ABILITY_TabletopBasicAttack);

			Assert.That(result, Is.EqualTo(AbilityActivationResult.Success));
			float timeoutAt = Time.realtimeSinceStartup + 2f;
			while (target.AbilitySystem.GetAttrCurrentValue(
				       XAttrSet.FightUnit,
				       XAttribute.Health) >= healthBefore)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"牌桌已成功提交 EX-GAS Ability，但正式 Timeline / GameplayEffect 没有降低目标生命。");
				yield return null;
			}

			Assert.That(
				target.AbilitySystem.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Health),
				Is.LessThan(healthBefore));
			while (attacker.AbilitySystem.IsAbilityActive(XAbility.ABILITY_TabletopBasicAttack))
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"牌桌攻击已经造成伤害，但 EX-GAS Ability 没有按正式 Timeline 生命周期结束。");
				yield return null;
			}
		}

		[UnityTest]
		public IEnumerator FoundationStageB_ActionScheduleQuestTravelAndBattleShareOneScenarioRun()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			ScenarioRun run = controller.ScenarioRun;
			RuntimeTabletop sourceTabletop = run.Tabletop;
			CharacterCard traveler = (CharacterCard)sourceTabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				new Vector2(0f, 2f));
			AbilitySystemCell travelerAbilitySystem = traveler.AbilitySystem;

			IReadOnlyList<ActionCandidate> candidates = controller.QueryTestActionCandidates(
				controller.MiddleCardId,
				controller.TargetCardId);
			Assert.That(candidates.Count, Is.EqualTo(1));
			ActionInstance action = controller.StartSelectedAction(candidates[0].Action.ContentId);
			scenarioDirector.ConfirmTurn();
			scenarioDirector.ConfirmTurn();

			Assert.That(action.State, Is.EqualTo(ActionInstanceState.Completed));
			Assert.That(run.ConfirmedTurnIndex, Is.EqualTo(2));
			Assert.That(run.CurrentDay, Is.EqualTo(2));
			Assert.That(
				run.QuestLog.GetQuest(new ContentId(FoundationTestSceneHarness.TestQuestContentId)).Status,
				Is.EqualTo(QuestStatus.Completed));

			yield return scenarioDirector.TravelAsync(
				new ContentId(FoundationTestSceneHarness.TestBattleRegionContentId),
				new[] { traveler.Id }).ToCoroutine();

			Assert.That(scenarioDirector.ActiveRun, Is.SameAs(run));
			Assert.That(run.ConfirmedTurnIndex, Is.EqualTo(2));
			Assert.That(run.CurrentDay, Is.EqualTo(2));
			Assert.That(run.Tabletop, Is.Not.SameAs(sourceTabletop));
			Assert.That(run.Tabletop.Cards.TryGetCard(traveler.Id, out TabletopCard moved), Is.True);
			Assert.That(moved, Is.SameAs(traveler));
			Assert.That(traveler.AbilitySystem, Is.SameAs(travelerAbilitySystem));

			CharacterCard enemy = (CharacterCard)run.Tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				new Vector2(2f, 2f));
			float healthBefore = enemy.AbilitySystem.GetAttrCurrentValue(
				XAttrSet.FightUnit,
				XAttribute.Health);
			Battle battle = run.Tabletop.StartBattle(
				new[] { traveler.Id },
				new[] { enemy.Id });
			AbilityActivationResult activation = run.Tabletop.RequestBattleAbilityActivation(
				battle,
				traveler.Id,
				enemy.Id,
				XAbility.ABILITY_TabletopBasicAttack);
			Assert.That(activation, Is.EqualTo(AbilityActivationResult.Success));

			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (enemy.AbilitySystem.GetAttrCurrentValue(
				       XAttrSet.FightUnit,
				       XAttribute.Health) >= healthBefore)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, "阶段 B 战斗没有通过正式 GAS 链造成伤害。");
				yield return null;
			}
			while (traveler.AbilitySystem.IsAbilityActive(XAbility.ABILITY_TabletopBasicAttack))
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"阶段 B 攻击已经造成伤害，但 EX-GAS Ability 没有按正式 Timeline 完成收尾。");
				yield return null;
			}

			run.Tabletop.EndBattle(battle);
			Assert.That(run.Tabletop.ActiveBattles, Is.Empty);
			Assert.That(run.ConfirmedTurnIndex, Is.EqualTo(2));
			Assert.That(
				run.QuestLog.GetQuest(new ContentId(FoundationTestSceneHarness.TestQuestContentId)).Status,
				Is.EqualTo(QuestStatus.Completed));
			Assert.That(traveler.AbilitySystem, Is.SameAs(travelerAbilitySystem));
		}

		[UnityTest]
        public IEnumerator FoundationTabletop_DraggingMiddleCardBeyondBoundaryClampsDetachedTail()
        {
            Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationPlaceStackMouse");
            Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationPlaceStackKeyboard");
            // 设备必须先于场景中的 PlayerInput 启用，才能走它的正式用户与控制方案配对流程。
            yield return LoadFoundationTabletop();

            FoundationTestSceneHarness controller =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            TabletopCardView middleView = FindView(controller.MiddleCardId);
            TabletopCardView topView = FindView(controller.TopCardId);
            Vector3 middleOriginalPosition = middleView.transform.position;
            Vector3 topOriginalPosition = topView.transform.position;

            PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
            Assert.That(
                playerInput.SwitchCurrentControlScheme(keyboard, mouse),
                Is.True,
                "测试设备必须能匹配正式 Keyboard&Mouse 控制方案。");
            GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
            yield return null;

            Physics.SyncTransforms();
            Camera camera = Camera.main;
			BoxCollider middleCollider = middleView.GetComponent<BoxCollider>();
			Vector3 exposedMiddlePoint = middleCollider.bounds.center +
				Vector3.left * middleCollider.bounds.extents.x * 0.82f;
			Vector2 sourceStackPosition = controller.Cards
				.GetStackContaining(controller.MiddleCardId)
				.Position;
			Vector2 pressTablePosition = new Vector2(exposedMiddlePoint.x, exposedMiddlePoint.y);
			Vector2 pressScreenPosition = camera.WorldToScreenPoint(exposedMiddlePoint);
			var releaseTablePosition = new Vector2(0f, -4f);
			Vector2 releaseScreenPosition = camera.WorldToScreenPoint(releaseTablePosition);
			Vector2 requestedStackPosition = releaseTablePosition +
				(sourceStackPosition - pressTablePosition);

            Move(mouse.position, pressScreenPosition);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
			Move(mouse.position, releaseScreenPosition);
			yield return null;
			yield return null;
			Assert.That(
				Vector2.Distance(middleView.transform.localPosition, requestedStackPosition),
				Is.LessThan(0.001f),
				"从卡牌边缘开始拖拽时，选中卡牌必须保持按下点偏移，不能跳到鼠标中心。");
			Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.That(controller.ReleaseIntentCount, Is.EqualTo(1));
			Assert.That(controller.LastReleaseIntent.CardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(controller.LastReleaseIntent.TargetCardId.IsValid, Is.False);
			Assert.That(
				Vector2.Distance(controller.LastReleaseIntent.ReleasePointerPosition, releaseTablePosition),
				Is.LessThan(0.001f));
			Assert.That(
				Vector2.Distance(controller.LastReleaseIntent.RequestedStackPosition, requestedStackPosition),
				Is.LessThan(0.001f));
            Assert.That(controller.ActionCandidateQueryCount, Is.Zero, "空白桌面释放不应冒充行动查询。");
            Assert.That(controller.LastActionCandidates, Is.Empty);
            Assert.That(controller.Cards.StackCount, Is.EqualTo(3));

            TabletopCardStack bottomStack = controller.Cards.GetStackContaining(controller.BottomCardId);
            TabletopCardStack placedStack = controller.Cards.GetStackContaining(controller.MiddleCardId);
            Assert.That(placedStack, Is.Not.SameAs(bottomStack));
            Assert.That(
                controller.Cards.GetStackContaining(controller.TopCardId),
                Is.SameAs(placedStack));
            CollectionAssert.AreEqual(
                new[] { controller.MiddleCardId, controller.TopCardId },
                placedStack.Cards.Select(card => card.Id));
			Rect releasedFootprint = controller.PlacementRules.Geometry.CalculateFootprint(
				requestedStackPosition,
                cardCount: 2);
            Rect placementBounds = controller.PlacementRules.Area.Bounds;
            Vector2 halfSize = releasedFootprint.size * 0.5f;
            Vector2 expectedCenter = new(
                Mathf.Clamp(
                    releasedFootprint.center.x,
                    placementBounds.xMin + halfSize.x,
                    placementBounds.xMax - halfSize.x),
                Mathf.Clamp(
                    releasedFootprint.center.y,
                    placementBounds.yMin + halfSize.y,
                    placementBounds.yMax - halfSize.y));
			Vector2 expectedPosition = expectedCenter - (releasedFootprint.center - requestedStackPosition);
            Assert.That(Vector2.Distance(placedStack.Position, expectedPosition), Is.LessThan(0.001f));
            Assert.That(placedStack.Position, Is.Not.EqualTo(releaseTablePosition));
            Assert.That(
                Vector2.Distance(middleView.transform.localPosition, expectedPosition),
                Is.LessThan(0.001f),
                "视图必须依靠权威状态修订自动投影到新堆位置。");
            Assert.That(middleView.transform.position, Is.Not.EqualTo(middleOriginalPosition));
            Assert.That(topView.transform.position, Is.Not.EqualTo(topOriginalPosition));
        }

        [UnityTest]
        public IEnumerator FoundationTabletop_BlankReleaseOverlappingAnotherStackSeparatesWholeStacks()
        {
            Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationOverlapMouse");
            Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationOverlapKeyboard");
            // 设备必须先于场景中的 PlayerInput 启用，才能走它的正式用户与控制方案配对流程。
            yield return LoadFoundationTabletop();

            FoundationTestSceneHarness controller =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            TabletopCardView middleView = FindView(controller.MiddleCardId);
            TabletopCardView topView = FindView(controller.TopCardId);
            TabletopCardView targetView = FindView(controller.TargetCardId);
            Vector3 middleOriginalPosition = middleView.transform.position;
            Vector3 topOriginalPosition = topView.transform.position;
            Vector2 targetOriginalPosition = controller.Cards
                .GetStackContaining(controller.TargetCardId)
                .Position;

            PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
            Assert.That(
                playerInput.SwitchCurrentControlScheme(keyboard, mouse),
                Is.True,
                "测试设备必须能匹配正式 Keyboard&Mouse 控制方案。");
            GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
            yield return null;

            Physics.SyncTransforms();
            Camera camera = Camera.main;
            BoxCollider middleCollider = middleView.GetComponent<BoxCollider>();
            Vector3 exposedMiddlePoint = middleCollider.bounds.center +
                Vector3.left * middleCollider.bounds.extents.x * 0.82f;
            Vector2 pressScreenPosition = camera.WorldToScreenPoint(exposedMiddlePoint);
            var releaseTablePosition = new Vector2(0.9f, 0.35f);
            Vector2 releaseScreenPosition = camera.WorldToScreenPoint(releaseTablePosition);
            Ray releaseRay = camera.ScreenPointToRay(releaseScreenPosition);
            Assert.That(
                Physics.RaycastAll(releaseRay, 100f)
                    .All(hit => hit.collider.GetComponentInParent<TabletopCardView>() == null),
                Is.True,
                "重叠验收的指针落点必须是空白桌面，不能通过命中目标卡牌进入行动查询分支。");

            Move(mouse.position, pressScreenPosition);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Move(mouse.position, releaseScreenPosition);
            yield return null;
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.That(controller.ReleaseIntentCount, Is.EqualTo(1));
            Assert.That(controller.LastReleaseIntent.TargetCardId.IsValid, Is.False);
            Assert.That(controller.ActionCandidateQueryCount, Is.Zero);
            Assert.That(controller.LastActionCandidates, Is.Empty);
            Assert.That(controller.Cards.StackCount, Is.EqualTo(3));

            TabletopCardStack placedStack = controller.Cards.GetStackContaining(controller.MiddleCardId);
            TabletopCardStack targetStack = controller.Cards.GetStackContaining(controller.TargetCardId);
            Assert.That(controller.Cards.GetStackContaining(controller.TopCardId), Is.SameAs(placedStack));
            CollectionAssert.AreEqual(
                new[] { controller.MiddleCardId, controller.TopCardId },
                placedStack.Cards.Select(card => card.Id));
            Assert.That(placedStack.Position, Is.Not.EqualTo(releaseTablePosition));
            Assert.That(targetStack.Position, Is.Not.EqualTo(targetOriginalPosition));

            TabletopCardStackGeometry geometry = controller.PlacementRules.Geometry;
            Rect placedFootprint = geometry.CalculateFootprint(
                placedStack.Position,
                placedStack.Cards.Count);
            Rect targetFootprint = geometry.CalculateFootprint(
                targetStack.Position,
                targetStack.Cards.Count);
            float overlapX = (placedFootprint.width + targetFootprint.width) * 0.5f -
                Mathf.Abs(placedFootprint.center.x - targetFootprint.center.x);
            float overlapY = (placedFootprint.height + targetFootprint.height) * 0.5f -
                Mathf.Abs(placedFootprint.center.y - targetFootprint.center.y);
            Assert.That(
                overlapX <= 0.001f || overlapY <= 0.001f,
                Is.True,
                "空白放置完成后，两个整堆的权威占地不能继续重叠。");

            Assert.That(
                Vector2.Distance(middleView.transform.localPosition, placedStack.Position),
                Is.LessThan(0.001f));
            Assert.That(
                Vector2.Distance(targetView.transform.localPosition, targetStack.Position),
                Is.LessThan(0.001f));
            Assert.That(middleView.transform.position, Is.Not.EqualTo(middleOriginalPosition));
            Assert.That(topView.transform.position, Is.Not.EqualTo(topOriginalPosition));
        }

        [UnityTest]
        public IEnumerator FoundationTabletop_SelectedActionUsesTurnTruthAcrossProgressionModeSwitch()
        {
            Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationMouse");
            Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationKeyboard");
            // 设备必须先于场景中的 PlayerInput 启用，才能走它的正式用户与控制方案配对流程。
            yield return LoadFoundationTabletop();

            FoundationTestSceneHarness controller =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            TabletopCardView sourceView = FindView(controller.MiddleCardId);
            TabletopCardView topView = FindView(controller.TopCardId);
            TabletopCardView targetView = FindView(controller.TargetCardId);
            TabletopCardDragInput dragInput = Object.FindAnyObjectByType<TabletopCardDragInput>();
            Vector3 sourceAuthoritativePosition = sourceView.transform.position;
            Vector3 topAuthoritativePosition = topView.transform.position;

            PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
            Assert.That(
                playerInput.SwitchCurrentControlScheme(keyboard, mouse),
                Is.True,
                "测试设备必须能匹配正式 Keyboard&Mouse 控制方案。");
            Assert.That(playerInput.user.valid, Is.True);
            Assert.That(playerInput.devices.Any(device => device == mouse), Is.True);
            GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
            yield return null;

            InputAction pointAction = playerInput.actions.FindActionMap("Gameplay").FindAction("Point");
            Assert.That(pointAction.enabled, Is.True);
            Assert.That(
                pointAction.controls.Any(control => control.device == mouse),
                Is.True,
                "Gameplay/Point 没有解析到已配对的测试鼠标控件。");

            // 视图由 YooAsset 异步实例化并在同一帧设置 Transform；读取 Collider 边界前先同步物理世界，
            // 避免测试顺序和帧耗时决定 bounds 仍停留在预制体原点还是已到权威位置。
            Physics.SyncTransforms();
            Camera camera = Camera.main;
            BoxCollider sourceCollider = sourceView.GetComponent<BoxCollider>();
            Vector3 exposedMiddlePoint = sourceCollider.bounds.center +
                Vector3.left * sourceCollider.bounds.extents.x * 0.82f;
            Vector2 pressScreenPosition = camera.WorldToScreenPoint(exposedMiddlePoint);
            Vector2 targetScreenPosition = camera.WorldToScreenPoint(targetView.transform.position);

            Ray pressRay = camera.ScreenPointToRay(pressScreenPosition);
            Assert.That(Physics.Raycast(pressRay, out RaycastHit pressHit, 100f), Is.True);
            Assert.That(
                pressHit.collider.GetComponentInParent<TabletopCardView>()?.CardId,
                Is.EqualTo(controller.MiddleCardId),
                "中间卡牌暴露区域必须先命中中间卡牌，而不是顶部或底部卡牌。");

            Move(mouse.position, pressScreenPosition);
            yield return null;
            Assert.That(Vector2.Distance(mouse.position.ReadValue(), pressScreenPosition), Is.LessThan(0.5f));
            Assert.That(
                Vector2.Distance(pointAction.ReadValue<Vector2>(), pressScreenPosition),
                Is.LessThan(0.5f),
                "PlayerInput 的 Gameplay/Point 没有读取到配对鼠标位置。");
            Assert.That(
                Vector2.Distance(
                    GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay),
                    pressScreenPosition),
                Is.LessThan(0.5f));
            Press(mouse.leftButton);
            yield return null;
            Assert.That(dragInput.IsPointerSessionActive, Is.True, "主指针按下后没有建立牌桌拖拽会话。");

			EventSystem eventSystem = GameManager.EventSystem;
			Assert.That(eventSystem.pixelDragThreshold, Is.GreaterThan(1));
			Vector2 belowDragThresholdPosition = pressScreenPosition +
				Vector2.right * (eventSystem.pixelDragThreshold - 1f);
			Move(mouse.position, belowDragThresholdPosition);
			yield return null;
			Assert.That(dragInput.IsDragging, Is.False, "屏幕位移未达到正式 UI 像素阈值时不能开始拖拽。");
			Assert.That(sourceView.transform.position, Is.EqualTo(sourceAuthoritativePosition));
			Assert.That(topView.transform.position, Is.EqualTo(topAuthoritativePosition));

            Move(mouse.position, targetScreenPosition);
            yield return null;
            yield return null;

            Assert.That(dragInput.IsDragging, Is.True);
            Assert.That(sourceView.transform.position, Is.Not.EqualTo(sourceAuthoritativePosition));
            Assert.That(topView.transform.position, Is.Not.EqualTo(topAuthoritativePosition));
            Assert.That(targetView.IsHighlighted, Is.True);
            Assert.That(controller.ReleaseIntentCount, Is.Zero);

            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.That(controller.ReleaseIntentCount, Is.EqualTo(1));
            Assert.That(controller.LastReleaseIntent.CardId, Is.EqualTo(controller.MiddleCardId));
            Assert.That(controller.LastReleaseIntent.TargetCardId, Is.EqualTo(controller.TargetCardId));
            Assert.That(controller.LastReleaseIntent.IsDrag, Is.True);
            Assert.That(controller.ActionCandidateQueryCount, Is.EqualTo(1));
            Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
            Assert.That(
                controller.ScenarioRun.IsContentDiscovered(
                    new ContentId(FoundationTestSceneHarness.TestActionContentId)),
                Is.True,
                "统一测试场景里的测试行动必须先进入局内发现状态，再参与候选解析。");
            ActionCandidate candidate = controller.LastActionCandidates[0];
            Assert.That(candidate.Action.ContentId.Value, Is.EqualTo("test.foundation.action"));
            Assert.That(candidate.IsReady, Is.True);
            Assert.That(candidate.MissingParticipantCount, Is.Zero);
            Assert.That(candidate.Bindings.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { controller.MiddleCardId, controller.TargetCardId },
                candidate.Bindings[0].CardIds);
            Assert.That(controller.Cards.StackCount, Is.EqualTo(2), "释放意图不能提前合并正式堆栈。");
            Assert.That(
                controller.Cards.GetStackContaining(controller.MiddleCardId),
                Is.SameAs(controller.Cards.GetStackContaining(controller.BottomCardId)));
            Assert.That(
                controller.Cards.GetStackContaining(controller.MiddleCardId),
                Is.Not.SameAs(controller.Cards.GetStackContaining(controller.TargetCardId)));
            Assert.That(Vector3.Distance(sourceView.transform.position, sourceAuthoritativePosition), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(topView.transform.position, topAuthoritativePosition), Is.LessThan(0.0001f));
            Assert.That(targetView.IsHighlighted, Is.False);

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            RuntimeTabletop tabletop = scenarioDirector.ActiveRun.Tabletop;
            yield return SelectActionThroughPanel(controller, tabletop, candidate, mouse, playerInput);
            ActionInstance actionInstance = tabletop.ActiveActions.Single();
            Assert.That(actionInstance.State, Is.EqualTo(ActionInstanceState.Running));
            Assert.That(actionInstance.ActionId, Is.EqualTo(candidate.Action.ContentId));
            Assert.That(actionInstance.Bindings, Is.Not.SameAs(candidate.Bindings));
            Assert.That(actionInstance.Bindings.Count, Is.EqualTo(candidate.Bindings.Count));
            Assert.That(actionInstance.Bindings[0].Slot, Is.SameAs(candidate.Action.ParticipationSlots[0]));
            CollectionAssert.AreEqual(candidate.Bindings[0].CardIds, actionInstance.Bindings[0].CardIds);
            Assert.That(tabletop.ActiveActions.Count, Is.EqualTo(1));
            Assert.That(
                tabletop.ProgressionMode,
                Is.EqualTo(ActionProgressionMode.TurnBased));

            TabletopActionProgressView actionProgressView = null;
            float progressViewTimeoutAt = Time.realtimeSinceStartup + 2f;
            while (actionProgressView == null)
            {
                Assert.Less(
                    Time.realtimeSinceStartup,
                    progressViewTimeoutAt,
                    "活动行动开始后没有创建牌桌进度视图。");
                actionProgressView = Object.FindAnyObjectByType<TabletopActionProgressView>();
                yield return null;
            }
            Assert.That(
                actionProgressView.GetComponentInParent<TabletopCardView>()?.CardId,
                Is.EqualTo(controller.MiddleCardId),
                "进度视图必须锚定在行动的首个参与卡牌上。");
            Assert.That(actionProgressView.NormalizedProgress, Is.Zero);
            Assert.That(actionProgressView.IsPaused, Is.False);

            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(actionInstance.ProgressedTurns, Is.Zero, "默认回合制不能因为现实时间流逝而推进普通行动。");
            Assert.That(actionProgressView.NormalizedProgress, Is.Zero);

            Assert.That(scenarioDirector.ActiveRun.ConfirmedTurnIndex, Is.Zero);
            Assert.That(scenarioDirector.ConfirmTurn(), Is.EqualTo(1));
            Assert.That(actionInstance.ProgressedTurns, Is.EqualTo(1f));
            Assert.That(actionInstance.Progress, Is.EqualTo(0.5f));
            yield return null;
            Assert.That(actionProgressView.NormalizedProgress, Is.EqualTo(0.5f).Within(0.0001f));

			Assert.That(scenarioDirector.ActiveRun.SecondsPerTurn, Is.EqualTo(0.35f).Within(0.0001f));
            scenarioDirector.ActiveRun.UseRealTimeProgression();
            Assert.That(
                tabletop.ProgressionMode,
                Is.EqualTo(ActionProgressionMode.RealTime));

            Time.timeScale = 0f;
            float globallyPausedTurns = actionInstance.ProgressedTurns;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(actionInstance.ProgressedTurns, Is.EqualTo(globallyPausedTurns).Within(0.0001f));
            Time.timeScale = 1f;

            tabletop.PauseAction(actionInstance);
            float pausedTurns = actionInstance.ProgressedTurns;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(actionInstance.State, Is.EqualTo(ActionInstanceState.Paused));
            Assert.That(actionInstance.ProgressedTurns, Is.EqualTo(pausedTurns).Within(0.0001f));
            Assert.That(actionProgressView.IsPaused, Is.True);
            Assert.That(
                actionProgressView.NormalizedProgress,
                Is.EqualTo(pausedTurns / actionInstance.TurnCost).Within(0.0001f));

            Time.timeScale = 2f;
            tabletop.ResumeAction(actionInstance);
            float completionTimeoutAt = Time.realtimeSinceStartup + 2f;
            while (actionInstance.State != ActionInstanceState.Completed)
            {
                Assert.Less(Time.realtimeSinceStartup, completionTimeoutAt, "恢复后的实时行动行动实例没有完成。");
                yield return null;
            }
            Time.timeScale = 1f;

            Assert.That(actionInstance.Progress, Is.EqualTo(1f));
            Assert.That(actionInstance.ProgressedTurns, Is.EqualTo(actionInstance.TurnCost).Within(0.0001f));
            Assert.That(tabletop.ActiveActions, Is.Empty);
            yield return null;
            Assert.That(
                Object.FindObjectsByType<TabletopActionProgressView>(),
                Is.Empty,
                "行动完成后不能保留没有权威行动的进度视图。");

            ActionResultBranchDefinition selectedBranch = candidate.Action.ResultBranches
                .Single(branch => branch.Key == actionInstance.ResultBranchKey);
            int expectedProductCount = selectedBranch.ResultIntents
                .OfType<CreateCardsResultIntent>()
                .Sum(intent => intent.Count);
            Assert.That(controller.Cards.CardCount, Is.EqualTo(2 + expectedProductCount));
            Assert.That(controller.Cards.StackCount, Is.EqualTo(1 + expectedProductCount));
            Assert.That(controller.Cards.TryGetCard(controller.MiddleCardId, out _), Is.False);
            Assert.That(controller.Cards.TryGetCard(controller.TargetCardId, out _), Is.False);

            TabletopCardStack[] productStacks = controller.Cards.Stacks
                .Where(stack => stack.Cards.Count == 1 &&
                                stack.Cards[0].ContentId.Value ==
                                FoundationTestSceneHarness.TestProductContentId)
                .ToArray();
            Assert.That(productStacks, Has.Length.EqualTo(expectedProductCount));
            Rect placementBounds = tabletop.PlacementRules.Area.Bounds;
            Assert.That(productStacks.All(stack =>
            {
                Rect footprint = tabletop.PlacementRules.Geometry.CalculateFootprint(
                    stack.Position,
                    stack.Cards.Count);
                return footprint.xMin >= placementBounds.xMin - 0.0001f &&
                       footprint.xMax <= placementBounds.xMax + 0.0001f &&
                       footprint.yMin >= placementBounds.yMin - 0.0001f &&
                       footprint.yMax <= placementBounds.yMax + 0.0001f;
            }), Is.True, "行动产物解开牌堆重叠后必须完整留在当前剧本牌桌边界内。");
            TabletopCardView[] liveViews = Object.FindObjectsByType<TabletopCardView>();
            Assert.That(liveViews, Has.Length.EqualTo(2 + expectedProductCount));
            Assert.That(
                liveViews.All(view => controller.Cards.TryGetCard(view.CardId, out _)),
                Is.True,
                "牌桌视图必须投影当前权威卡牌，不能保留已被行动结果移除的旧卡牌视图。");
            Assert.That(
                liveViews.Count(view =>
                    view.ContentId.Value == FoundationTestSceneHarness.TestProductContentId),
                Is.EqualTo(expectedProductCount),
                "行动结果生成的每张产物卡牌都必须自动获得对应视图。");
			Assert.That(
				scenarioDirector.ActiveRun.QuestLog.GetQuest(
					new ContentId(FoundationTestSceneHarness.TestQuestContentId)).Status,
                Is.EqualTo(QuestStatus.Completed),
                "成功结算测试行动后，活动任务子项必须消费同一个行动完成事实并完成。");
        }

        [UnityTest]
        public IEnumerator FoundationTabletop_RemovedParticipantCancelsBeforeProgressAndResult()
        {
            yield return LoadFoundationTabletop();

            FoundationTestSceneHarness controller =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            IReadOnlyList<ActionCandidate> candidates =
                controller.QueryTestActionCandidates(controller.MiddleCardId, controller.TargetCardId);
            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates[0].IsReady, Is.True);

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            RuntimeTabletop tabletop = scenarioDirector.ActiveRun.Tabletop;
            ActionInstance actionInstance = controller.StartSelectedAction(candidates[0].Action.ContentId);
            tabletop.RemoveCard(controller.TargetCardId);

            Assert.That(scenarioDirector.ConfirmTurn(), Is.EqualTo(1));

            Assert.That(actionInstance.State, Is.EqualTo(ActionInstanceState.Cancelled));
            Assert.That(
                actionInstance.CancellationReason,
                Is.EqualTo(ActionCancellationReason.ParticipantInvalidated));
            Assert.That(actionInstance.ProgressedTurns, Is.Zero);
            Assert.That(tabletop.ActiveActions, Is.Empty);
            Assert.That(controller.Cards.CardCount, Is.EqualTo(3));
            Assert.That(controller.Cards.TryGetCard(controller.MiddleCardId, out _), Is.True);
            Assert.That(
                controller.Cards.Stacks.Any(stack =>
                    stack.Cards.Any(card =>
                        card.ContentId.Value == FoundationTestSceneHarness.TestProductContentId)),
                Is.False);
			Assert.That(
				scenarioDirector.ActiveRun.QuestLog.GetQuest(
					new ContentId(FoundationTestSceneHarness.TestQuestContentId)).Status,
                Is.EqualTo(QuestStatus.Active),
                "参与者失效取消的行动不能发布成功完成事实或推进任务子项。");

            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<TabletopCardView>(), Has.Length.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator FoundationTabletop_PlayerConfirmsActionThroughUIKitAndRestoresGameplayInput()
        {
            Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationActionChoiceMouse");
            Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationActionChoiceKeyboard");
            yield return LoadFoundationTabletop();

            FoundationTestSceneHarness controller =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
            Assert.That(
                playerInput.SwitchCurrentControlScheme(keyboard, mouse),
                Is.True,
                "测试设备必须能匹配正式 Keyboard&Mouse 控制方案。");
            GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
            yield return null;

            TabletopCardView sourceView = FindView(controller.MiddleCardId);
            TabletopCardView targetView = FindView(controller.TargetCardId);
            Physics.SyncTransforms();
            Camera camera = Camera.main;
            BoxCollider sourceCollider = sourceView.GetComponent<BoxCollider>();
            Vector2 pressScreenPosition = camera.WorldToScreenPoint(
                sourceCollider.bounds.center + Vector3.left * sourceCollider.bounds.extents.x * 0.82f);
            Vector2 targetScreenPosition = camera.WorldToScreenPoint(targetView.transform.position);

            Move(mouse.position, pressScreenPosition);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Move(mouse.position, targetScreenPosition);
            yield return null;
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
            ActionCandidate candidate = controller.LastActionCandidates[0];
            Assert.That(candidate.IsReady, Is.True);

            RuntimeTabletop tabletop = controller.ScenarioRun.Tabletop;
            yield return SelectActionThroughPanel(controller, tabletop, candidate, mouse, playerInput);

            Assert.That(tabletop.ActiveActions.Count, Is.EqualTo(1));
            Assert.That(tabletop.ActiveActions[0].ActionId, Is.EqualTo(candidate.Action.ContentId));
            Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Gameplay));
            Assert.That(playerInput.currentActionMap.name, Is.EqualTo(EActionMap.Gameplay.ToString()));
        }

		[UnityTest]
		public IEnumerator FoundationTabletop_PlayerFillsAndSubmitsActionPlanThroughUIKit()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationActionPlanMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationActionPlanKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			controller.DiscoverActionPlanTestContent();
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			yield return null;

			TabletopCardView sourceView = FindView(controller.MiddleCardId);
			TabletopCardView targetView = FindView(controller.TargetCardId);
			Physics.SyncTransforms();
			Camera camera = Camera.main;
			BoxCollider sourceCollider = sourceView.GetComponent<BoxCollider>();
			Vector2 sourcePosition = camera.WorldToScreenPoint(
				sourceCollider.bounds.center + Vector3.left * sourceCollider.bounds.extents.x * 0.82f);
			Vector2 targetPosition = camera.WorldToScreenPoint(targetView.transform.position);
			yield return Drag(mouse, sourcePosition, targetPosition);

			Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(2));
			ActionCandidate planCandidate = controller.LastActionCandidates.Single(candidate =>
				candidate.Action.ContentId.Value == FoundationTestSceneHarness.TestActionPlanContentId);
			Assert.That(planCandidate.IsReady, Is.False);
			Assert.That(planCandidate.MissingParticipantCount, Is.EqualTo(1));

			TabletopActionChoicePanel choicePanel = null;
			float choiceTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (choicePanel == null || !choicePanel.gameObject.activeInHierarchy)
			{
				Assert.Less(Time.realtimeSinceStartup, choiceTimeoutAt);
				choicePanel = Object.FindAnyObjectByType<TabletopActionChoicePanel>();
				yield return null;
			}
			Button planChoiceButton = choicePanel.GetComponentsInChildren<Button>(true)
				.Single(button => button.GetComponentInChildren<TMP_Text>(true)?.text.Contains("协同行动") == true);
			yield return ClickButton(mouse, planChoiceButton);

			TabletopActionPlanPanel planPanel = null;
			float planTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (planPanel == null || !planPanel.gameObject.activeInHierarchy)
			{
				Assert.Less(Time.realtimeSinceStartup, planTimeoutAt);
				planPanel = Object.FindAnyObjectByType<TabletopActionPlanPanel>();
				yield return null;
			}
			Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Gameplay));
			Assert.That(playerInput.currentActionMap.name, Is.EqualTo(EActionMap.Gameplay.ToString()));
			Assert.That(controller.ScenarioRun.Tabletop.ActionPlans, Has.Count.EqualTo(1));
			Assert.That(planPanel.Plan, Is.SameAs(controller.ScenarioRun.Tabletop.ActionPlans[0]));
			Assert.That(planPanel.Plan.MissingParticipantCount, Is.EqualTo(1));
			TabletopActionPlanSlotView slotView =
				planPanel.GetComponentInChildren<TabletopActionPlanSlotView>();
			RectTransform slotRect = slotView.GetComponent<RectTransform>();
			Canvas slotCanvas = slotView.GetComponentInParent<Canvas>();
			Camera slotCamera = slotCanvas.renderMode == RenderMode.ScreenSpaceOverlay
				? null
				: slotCanvas.worldCamera;
			Vector2 slotPosition = RectTransformUtility.WorldToScreenPoint(slotCamera, slotRect.position);
			TabletopCardView additionalView = FindView(controller.BottomCardId);
			Vector2 additionalPosition = camera.WorldToScreenPoint(additionalView.transform.position);
			yield return Drag(mouse, additionalPosition, slotPosition);

			Assert.That(planPanel.Plan.IsReady, Is.True);
			Assert.That(planPanel.Plan.Bindings[0].CardIds, Has.Count.EqualTo(3));
			Button submitButton = planPanel.GetComponentsInChildren<Button>(true)
				.Single(button => button.gameObject.name == "SubmitPlan");
			Assert.That(submitButton.interactable, Is.True);
			yield return ClickButton(mouse, submitButton);

			Assert.That(controller.ScenarioRun.Tabletop.ActionPlans, Is.Empty);
			Assert.That(controller.ScenarioRun.Tabletop.ActiveActions, Has.Count.EqualTo(1));
			Assert.That(
				controller.ScenarioRun.Tabletop.ActiveActions[0].ActionId.Value,
				Is.EqualTo(FoundationTestSceneHarness.TestActionPlanContentId));
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_ActionPlanPanelKeepsMultiplePlansReachable()
		{
			yield return LoadFoundationTabletop();
			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			controller.DiscoverActionPlanTestContent();
			ActionCandidate candidate = controller
				.QueryTestActionCandidates(controller.MiddleCardId, controller.TargetCardId)
				.Single(item => item.Action.ContentId.Value ==
					FoundationTestSceneHarness.TestActionPlanContentId);
			ActionPlan first = controller.ScenarioRun.CreateActionPlan(candidate);
			ActionPlan second = controller.ScenarioRun.CreateActionPlan(candidate);

			UIKit.OpenPanelAsync<TabletopActionPlanPanel>(
				level: UILevel.Pop,
				data: new TabletopActionPlanPanelData(controller.ScenarioRun, first));
			TabletopActionPlanPanel panel = null;
			float panelTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (panel == null || !panel.gameObject.activeInHierarchy)
			{
				Assert.Less(Time.realtimeSinceStartup, panelTimeoutAt);
				panel = Object.FindAnyObjectByType<TabletopActionPlanPanel>();
				yield return null;
			}
			UIKit.OpenPanelAsync<TabletopActionPlanPanel>(
				level: UILevel.Pop,
				data: new TabletopActionPlanPanelData(controller.ScenarioRun, second));
			yield return null;

			Assert.That(panel.Plan, Is.SameAs(second));
			Assert.That(controller.ScenarioRun.Tabletop.ActionPlans.Count, Is.EqualTo(2));

			Button cancelButton = panel.GetComponentsInChildren<Button>(true)
				.Single(button => button.gameObject.name == "CancelPlan");
			cancelButton.onClick.Invoke();
			yield return null;

			Assert.That(panel.gameObject.activeInHierarchy, Is.True);
			Assert.That(panel.Plan, Is.SameAs(first));
			Assert.That(controller.ScenarioRun.Tabletop.ActionPlans.Count, Is.EqualTo(1));
			controller.ScenarioRun.Tabletop.CancelActionPlan(first);
			UIKit.ClosePanel(panel);
		}

        [UnityTest]
        public IEnumerator FoundationTabletop_PlayerAdvancesTurnWithHudButton()
        {
            Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationTurnHudMouse");
            Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationTurnHudKeyboard");
            yield return LoadFoundationTabletop();

            PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
            Assert.That(
                playerInput.SwitchCurrentControlScheme(keyboard, mouse),
                Is.True,
                "测试设备必须能匹配正式 Keyboard&Mouse 控制方案。");
            GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
            yield return null;

            Button confirmTurnButton = null;
            float buttonTimeoutAt = Time.realtimeSinceStartup + 2f;
            while (confirmTurnButton == null)
            {
                confirmTurnButton = Object.FindObjectsByType<Button>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .SingleOrDefault(button => button.gameObject.name == "ConfirmTurn");
                Assert.Less(
                    Time.realtimeSinceStartup,
                    buttonTimeoutAt,
                    "统一地基场景缺少玩家可点击的推进回合 HUD 按钮。");
                yield return null;
            }

            EventSystem eventSystem = GameManager.EventSystem;
            Assert.That(eventSystem, Is.Not.Null);
            InputSystemUIInputModule inputModule =
                eventSystem.GetComponent<InputSystemUIInputModule>();
            Assert.That(inputModule, Is.Not.Null);
            Assert.That(inputModule.actionsAsset, Is.SameAs(playerInput.actions));
            Assert.That(playerInput.uiInputModule, Is.SameAs(inputModule));
            Assert.That(inputModule.enabled, Is.True,
                "常驻回合 HUD 必须能在玩法状态接收正式 UI 鼠标输入。");

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            ScenarioTurnPanel turnPanel = Object.FindAnyObjectByType<ScenarioTurnPanel>();
            Assert.That(turnPanel.DisplayedTurnsInCurrentDay, Is.Zero);
            Assert.That(turnPanel.DisplayedTurnsPerDay, Is.EqualTo(2));
            Assert.That(turnPanel.DisplayedDayProgress, Is.Zero);
            Assert.That(turnPanel.CanConfirmTurn, Is.True);
            int previousTurn = scenarioDirector.ActiveRun.ConfirmedTurnIndex;
            yield return ClickConfirmTurnButton(mouse);

            float turnTimeoutAt = Time.realtimeSinceStartup + 2f;
            while (scenarioDirector.ActiveRun.ConfirmedTurnIndex == previousTurn)
            {
                Assert.Less(
                    Time.realtimeSinceStartup,
                    turnTimeoutAt,
                    "点击推进回合 HUD 按钮后，当前剧本回合没有推进。");
                yield return null;
            }

            Assert.That(
                scenarioDirector.ActiveRun.ConfirmedTurnIndex,
                Is.EqualTo(previousTurn + 1));
            Assert.That(turnPanel.DisplayedTurnIndex, Is.EqualTo(previousTurn + 1));
            Assert.That(turnPanel.DisplayedDay, Is.EqualTo(scenarioDirector.ActiveRun.CurrentDay));
            Assert.That(turnPanel.DisplayedTurnsInCurrentDay, Is.EqualTo(1));
            Assert.That(turnPanel.DisplayedTurnsPerDay, Is.EqualTo(2));
            Assert.That(turnPanel.DisplayedDayProgress, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(turnPanel.CanConfirmTurn, Is.True);
        }

        [UnityTest]
        public IEnumerator FoundationTabletop_RealTimeScheduleUpdatesHudAndDisablesManualAdvance()
        {
            yield return LoadFoundationTabletop();

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            ScenarioRun run = scenarioDirector.ActiveRun;
            ScenarioTurnPanel turnPanel = Object.FindAnyObjectByType<ScenarioTurnPanel>();
            Button confirmButton = turnPanel.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.name == "ConfirmTurn");

            run.UseRealTimeProgression();
            yield return null;

            Assert.That(turnPanel.CanConfirmTurn, Is.False);
            Assert.That(confirmButton.interactable, Is.False);
            float timeoutAt = Time.realtimeSinceStartup + 1f;
            while (turnPanel.DisplayedDayProgress <= 0f)
            {
                Assert.Less(
                    Time.realtimeSinceStartup,
                    timeoutAt,
                    "剧本已经切换即时制并推进时间，但日程 HUD 没有显示当前日内进度。");
                yield return null;
            }
            Assert.That(turnPanel.DisplayedDayProgress, Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator FoundationTabletop_PlayerCompletesTurnBasedActionThroughHud()
        {
            Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationTurnBasedHudMouse");
            Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationTurnBasedHudKeyboard");
            yield return LoadFoundationTabletop();

            FoundationTestSceneHarness controller =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
            Assert.That(
                playerInput.SwitchCurrentControlScheme(keyboard, mouse),
                Is.True,
                "测试设备必须能匹配正式 Keyboard&Mouse 控制方案。");
            GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
            yield return null;

            TabletopCardView sourceView = FindView(controller.MiddleCardId);
            TabletopCardView targetView = FindView(controller.TargetCardId);
            Physics.SyncTransforms();
            Camera camera = Camera.main;
            BoxCollider sourceCollider = sourceView.GetComponent<BoxCollider>();
            Vector2 pressScreenPosition = camera.WorldToScreenPoint(
                sourceCollider.bounds.center + Vector3.left * sourceCollider.bounds.extents.x * 0.82f);
            Vector2 targetScreenPosition = camera.WorldToScreenPoint(targetView.transform.position);

            Move(mouse.position, pressScreenPosition);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Move(mouse.position, targetScreenPosition);
            yield return null;
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
            ActionCandidate candidate = controller.LastActionCandidates[0];
            Assert.That(candidate.IsReady, Is.True);

            RuntimeTabletop tabletop = controller.ScenarioRun.Tabletop;
            yield return SelectActionThroughPanel(controller, tabletop, candidate, mouse, playerInput);
            Assert.That(tabletop.ActiveActions.Count, Is.EqualTo(1));
            ActionInstance actionInstance = tabletop.ActiveActions[0];
            Assert.That(actionInstance.TurnCost, Is.EqualTo(2f));
            int releaseIntentCountBeforeHud = controller.ReleaseIntentCount;

            yield return ClickConfirmTurnButton(mouse);
            Assert.That(actionInstance.State, Is.EqualTo(ActionInstanceState.Running));
            Assert.That(actionInstance.ProgressedTurns, Is.EqualTo(1f));
            yield return ClickConfirmTurnButton(mouse);
            yield return null;

            Assert.That(actionInstance.State, Is.EqualTo(ActionInstanceState.Completed));
            Assert.That(tabletop.ActiveActions, Is.Empty);
            Assert.That(controller.ReleaseIntentCount, Is.EqualTo(releaseIntentCountBeforeHud),
                "点击回合 HUD 必须由 UI 消费，不能再生成牌桌拖拽释放意图。");

            ActionResultBranchDefinition selectedBranch = candidate.Action.ResultBranches
                .Single(branch => branch.Key == actionInstance.ResultBranchKey);
            int expectedProductCount = selectedBranch.ResultIntents
                .OfType<CreateCardsResultIntent>()
                .Sum(intent => intent.Count);
            Assert.That(controller.Cards.CardCount, Is.EqualTo(2 + expectedProductCount));
            Assert.That(
                controller.Cards.Stacks.Count(stack =>
                    stack.Cards.Count == 1 &&
                    stack.Cards[0].ContentId.Value == FoundationTestSceneHarness.TestProductContentId),
                Is.EqualTo(expectedProductCount));

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            Assert.That(scenarioDirector.ActiveRun.ConfirmedTurnIndex, Is.EqualTo(2));
			Assert.That(
				scenarioDirector.ActiveRun.QuestLog.GetQuest(
					new ContentId(FoundationTestSceneHarness.TestQuestContentId)).Status,
                Is.EqualTo(QuestStatus.Completed));
        }

        [UnityTearDown]
        public IEnumerator DestroyGameManagerAfterTest()
        {
            Time.timeScale = 1f;
            if (GameManager.Exists())
            {
                Object.Destroy(GameManager.Instance.gameObject);
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator LoadFoundationTabletop()
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
            Assert.That(
                GameManager.HasSystem<ScenarioDirector>(),
                Is.True,
                "统一地基场景必须由剧本父级统一装配并启动任务生命周期。");

            PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
            // 官方 InputTestFixture 会隔离并恢复 InputUser 的全局监听计数；测试期间关闭自动设备切换，
            // 避免 PlayerInput 销毁时重复扣减。需要切换设备的用例仍显式调用正式控制方案 API。
            playerInput.neverAutoSwitchControlSchemes = true;

            FoundationTestSceneHarness controller = null;
            while (controller == null || !controller.IsReady ||
                   !AreAllViewsReady())
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "牌桌测试视图实例化超时。");
                controller = Object.FindAnyObjectByType<FoundationTestSceneHarness>();
                yield return null;
            }

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            Assert.That(scenarioDirector.HasActiveScenario, Is.True);
            Assert.That(
                scenarioDirector.ActiveScenarioId.Value,
                Is.EqualTo(FoundationTestSceneHarness.TestScenarioContentId));
			Assert.That(
				scenarioDirector.ActiveRun.QuestLog.GetQuest(
					new ContentId(FoundationTestSceneHarness.TestQuestContentId)).Status,
                Is.EqualTo(QuestStatus.Active));
            Assert.That(scenarioDirector.ActiveRun.ConfirmedTurnIndex, Is.Zero);
        }

        private static TabletopCardView FindView(TabletopCardId cardId)
        {
            return Object.FindObjectsByType<TabletopCardView>()
                .Single(view => view.CardId == cardId);
        }

        private static bool AreAllViewsReady()
        {
            TabletopCardView[] views = Object.FindObjectsByType<TabletopCardView>();
            return views.Length >= 4 &&
                views.All(view => view.GetComponent<SpriteRenderer>()?.sprite != null);
        }

        private IEnumerator ClickConfirmTurnButton(Mouse mouse)
        {
            Button confirmTurnButton = null;
            float buttonTimeoutAt = Time.realtimeSinceStartup + 2f;
            while (confirmTurnButton == null)
            {
                confirmTurnButton = Object.FindObjectsByType<Button>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .SingleOrDefault(button => button.gameObject.name == "ConfirmTurn");
                Assert.Less(
                    Time.realtimeSinceStartup,
                    buttonTimeoutAt,
                    "统一地基场景缺少玩家可点击的推进回合 HUD 按钮。");
                yield return null;
            }

            Canvas canvas = confirmTurnButton.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 buttonScreenPosition = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                confirmTurnButton.transform.position);
            Move(mouse.position, buttonScreenPosition);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
        }

		private IEnumerator Drag(Mouse mouse, Vector2 start, Vector2 end)
		{
			Move(mouse.position, start);
			yield return null;
			Press(mouse.leftButton);
			yield return null;
			Move(mouse.position, end);
			yield return null;
			yield return null;
			Release(mouse.leftButton);
			yield return null;
			yield return null;
		}

		private IEnumerator ClickButton(Mouse mouse, Button button)
		{
			Canvas canvas = button.GetComponentInParent<Canvas>();
			Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
				? canvas.worldCamera
				: null;
			Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
				eventCamera,
				button.transform.position);
			Move(mouse.position, screenPosition);
			yield return null;
			Press(mouse.leftButton);
			yield return null;
			Release(mouse.leftButton);
			yield return null;
		}

        private IEnumerator SelectActionThroughPanel(
            FoundationTestSceneHarness controller,
            RuntimeTabletop tabletop,
            ActionCandidate candidate,
            Mouse mouse,
            PlayerInput playerInput)
        {
            TabletopActionChoicePanel panel = null;
            float panelTimeoutAt = Time.realtimeSinceStartup + 5f;
            while (panel == null || !panel.gameObject.activeInHierarchy)
            {
                Assert.Less(
                    Time.realtimeSinceStartup,
                    panelTimeoutAt,
                    "释放行动候选后没有通过 UIKit 打开行动选择面板。");
                panel = Object.FindAnyObjectByType<TabletopActionChoicePanel>();
                yield return null;
            }

            Assert.That(panel.ChoiceCount, Is.EqualTo(controller.LastActionCandidates.Count));
            Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Dialogue));
            Assert.That(playerInput.currentActionMap.name, Is.EqualTo(EActionMap.UI.ToString()));

            EventSystem eventSystem = GameManager.EventSystem;
            Assert.That(eventSystem, Is.Not.Null);
            InputSystemUIInputModule inputModule =
                eventSystem.GetComponent<InputSystemUIInputModule>();
            Assert.That(inputModule, Is.Not.Null);
            Assert.That(inputModule.actionsAsset, Is.SameAs(playerInput.actions));
            Assert.That(playerInput.uiInputModule, Is.SameAs(inputModule));
            Assert.That(inputModule.enabled, Is.True);

            Button choiceButton = panel.GetComponentsInChildren<Button>(includeInactive: true)
                .Single(button => button.gameObject.name == "ActionChoice_0");
            Canvas canvas = choiceButton.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 buttonScreenPosition = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                choiceButton.transform.position);

            Move(mouse.position, buttonScreenPosition);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);

            float actionTimeoutAt = Time.realtimeSinceStartup + 5f;
            while (tabletop.ActiveActions.Count == 0)
            {
                Assert.Less(
                    Time.realtimeSinceStartup,
                    actionTimeoutAt,
                    $"点击行动候选 {candidate.Action.ContentId} 后没有创建活动行动实例。");
                yield return null;
            }

            yield return null;
            Assert.That(playerInput.currentActionMap.name, Is.EqualTo(EActionMap.Gameplay.ToString()));
            Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Gameplay));
            Assert.That(inputModule.actionsAsset, Is.SameAs(playerInput.actions));
            Assert.That(playerInput.uiInputModule, Is.SameAs(inputModule));
            Assert.That(inputModule.enabled, Is.True,
                "返回玩法状态后，常驻回合 HUD 必须继续接收同一份 UI 鼠标输入。");
        }

	}
}
