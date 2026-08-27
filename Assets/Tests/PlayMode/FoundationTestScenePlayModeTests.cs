using System.Collections;
using System.Collections.Generic;
using System.IO;
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
		private const string FoundationE2EContactSheetFileName =
			"_contactsheet-foundation-e2e-sequence-latest.png";
		private static readonly string[] FoundationE2EVisualEvidenceFiles =
		{
			"foundation-e2e-sequence-01-ready.png",
			"foundation-e2e-sequence-02-damage-feedback.png",
			"foundation-e2e-sequence-03-action-choice.png",
			"foundation-e2e-sequence-04-action-progress-start.png",
			"foundation-e2e-sequence-05-action-progress-half.png",
			"foundation-e2e-sequence-06-action-completed-product.png",
		};
		private string m_saveDirectory;

		[UnitySetUp]
		public IEnumerator ConfigureIsolatedSaveDirectory()
		{
			m_saveDirectory = Path.Combine(
				Application.temporaryCachePath,
				"Gameplay-FoundationTestSceneTests",
				System.Guid.NewGuid().ToString("N"));
			SaveSystem.ResetSaveKitConfigurationForTests();
			SaveSystem.ConfigureSaveKit(m_saveDirectory);
			yield return null;
		}

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
			Camera camera = GetMainCamera();
			BoxCollider selectedCollider = selectedView.GetComponent<BoxCollider>();
			Vector2 selectedScreenPosition = FindScreenPointThatHitsCard(
				camera,
				selectedCollider,
				controller.MiddleCardId);
			Vector2 hoveredScreenPosition = camera.WorldToScreenPoint(hoveredView.transform.position);
			Vector2 emptyScreenPosition = new(Screen.width * 0.5f, Screen.height * 0.9f);

			Move(mouse.position, selectedScreenPosition);
			yield return null;
			Assert.That(tabletopView.HoveredCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(tabletopView.ReadableCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(infoPanel.DisplayedCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(infoPanel.DisplayedTitle, Is.EqualTo("村民"));
			Assert.That(infoPanel.DisplayedDescription, Does.Contain("健康的村民。"));

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
			Vector2 targetScreenPosition = GetMainCamera().WorldToScreenPoint(targetView.transform.position);
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
		public IEnumerator FoundationTabletop_SequenceMessageOverridesReadableCardInfoPanel()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationSequenceInfoPanelMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationSequenceInfoPanelKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			TabletopCardInfoPanel infoPanel = Object.FindAnyObjectByType<TabletopCardInfoPanel>();
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			yield return null;

			yield return ClickCard(mouse, controller.MiddleCardId);
			Assert.That(infoPanel.DisplayedCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(infoPanel.DisplayedTitle, Is.EqualTo("村民"));

			EventKit.Type.Send(new ScenarioSequenceMessageEvent(
				controller.ScenarioRun.ScenarioId,
				"卡包已解锁",
				"这里可以购买开端卡包。",
				0.05f));
			yield return null;

			Assert.That(infoPanel.IsSequenceMessageActive, Is.True);
			Assert.That(infoPanel.DisplayedCardId.IsValid, Is.False);
			Assert.That(infoPanel.DisplayedSequenceHeader, Is.EqualTo("卡包已解锁"));
			Assert.That(infoPanel.DisplayedTitle, Is.EqualTo("卡包已解锁"));
			Assert.That(
				infoPanel.DisplayedDescription,
				Is.EqualTo("这里可以购买开端卡包。"));

			yield return new WaitForSecondsRealtime(0.1f);
			yield return null;

			Assert.That(infoPanel.IsSequenceMessageActive, Is.False);
			Assert.That(infoPanel.DisplayedCardId, Is.EqualTo(controller.MiddleCardId));
			Assert.That(infoPanel.DisplayedTitle, Is.EqualTo("村民"));
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
			Assert.That(view.DisplayedHealthText, Is.EqualTo("15"));
			Assert.That(bottomView.DisplaysCharacterStatus, Is.False);
			Assert.That(middleView.DisplaysCharacterStatus, Is.False);
			Assert.That(topView.DisplaysCharacterStatus, Is.True);

			character.AbilitySystem.SetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.Health,
				9f);
			float timeoutAt = Time.realtimeSinceStartup + 2f;
			while (view.DisplayedHealthText != "9")
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
				20f);
			while (view.DisplayedHealthText != "9")
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"角色唯一 EX-GAS 生命上限已经改变，但角色卡状态投影没有同步更新。");
				yield return null;
			}
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_ProjectsGasDamageFeedbackOnTargetCard()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			Assert.That(
				controller.Cards.TryGetCard(controller.TargetCardId, out TabletopCard tabletopCard),
				Is.True);
			CharacterCard character = (CharacterCard)tabletopCard;
			TabletopCardView view = FindView(character.Id);

			EventKit.Type.Send(new AbilitySystemDamageResolvedPresentationEvent(
				character.AbilitySystem,
				0,
				isMissed: true,
				isCriticalHit: false,
				isSilent: false,
				damageType: EDamageType.Physical,
				visualFlags: EEffectVisualFlags.None));
			yield return null;

			TabletopHitResultView missResult = FindHitResult(candidate =>
				candidate.DisplayedHitSprite != null &&
				candidate.DisplayedHitSprite.name == "未命中图标");
			Assert.That(missResult.DisplayedDamageText, Is.Empty);
			Assert.That(missResult.DisplaysEffectivenessIcon, Is.False);
			Assert.That(view.IsHurtFeedbackActive, Is.False);

			EventKit.Type.Send(new AbilitySystemDamageResolvedPresentationEvent(
				character.AbilitySystem,
				28,
				isMissed: false,
				isCriticalHit: true,
				isSilent: false,
				damageType: EDamageType.Physical,
				visualFlags: EEffectVisualFlags.None,
				matchupResult: DamageMatchupResult.Advantage));
			yield return null;

			TabletopHitResultView criticalResult = FindHitResult(candidate =>
				candidate.DisplayedDamageText == "28");
			Assert.That(criticalResult.DisplayedHitSprite, Is.Not.Null);
			Assert.That(criticalResult.DisplayedHitSprite.name, Is.EqualTo("暴击图标"));
			Assert.That(criticalResult.DisplaysEffectivenessIcon, Is.True);
			Assert.That(criticalResult.DisplayedEffectivenessSprite, Is.Not.Null);
			Assert.That(
				criticalResult.DisplayedEffectivenessSprite.name,
				Is.EqualTo("优势图标"));
			Assert.That(view.IsHurtFeedbackActive, Is.True);

			EventKit.Type.Send(new AbilitySystemDamageResolvedPresentationEvent(
				character.AbilitySystem,
				7,
				isMissed: false,
				isCriticalHit: false,
				isSilent: false,
				damageType: EDamageType.Physical,
				visualFlags: EEffectVisualFlags.None,
				matchupResult: DamageMatchupResult.Disadvantage));
			yield return null;

			TabletopHitResultView normalResult = FindHitResult(candidate =>
				candidate.DisplayedDamageText == "7");
			Assert.That(normalResult.DisplayedHitSprite, Is.Not.Null);
			Assert.That(normalResult.DisplayedHitSprite.name, Is.EqualTo("普通命中图标"));
			Assert.That(normalResult.DisplaysEffectivenessIcon, Is.True);
			Assert.That(normalResult.DisplayedEffectivenessSprite, Is.Not.Null);
			Assert.That(
				normalResult.DisplayedEffectivenessSprite.name,
				Is.EqualTo("劣势图标"));
			Assert.That(view.IsHurtFeedbackActive, Is.True);
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_CapturesE2EVisualEvidenceSequence()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationE2EVisualMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationE2EVisualKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			yield return null;

			yield return CaptureVisualEvidence(FoundationE2EVisualEvidenceFiles[0]);

			Assert.That(
				controller.Cards.TryGetCard(controller.TargetCardId, out TabletopCard tabletopCard),
				Is.True);
			CharacterCard character = (CharacterCard)tabletopCard;
			EventKit.Type.Send(new AbilitySystemDamageResolvedPresentationEvent(
				character.AbilitySystem,
				28,
				isMissed: false,
				isCriticalHit: true,
				isSilent: false,
				damageType: EDamageType.Physical,
				visualFlags: EEffectVisualFlags.None,
				matchupResult: DamageMatchupResult.Advantage));
			yield return null;
			yield return CaptureVisualEvidence(FoundationE2EVisualEvidenceFiles[1]);

			yield return DragCardOntoCard(mouse, controller.MiddleCardId, controller.TargetCardId);
			Assert.That(
				controller.LastActionCandidates.Count,
				Is.EqualTo(1),
				BuildLastReleaseDiagnostic(controller, "端到端截图链拖拽后没有得到行动候选。") +
				" " + controller.BuildActionCandidateDiagnostic(controller.MiddleCardId, controller.TargetCardId));
			ActionCandidate candidate = controller.LastActionCandidates[0];
			Assert.That(candidate.Action.ContentId.Value, Is.EqualTo(FoundationTestSceneHarness.TestActionContentId));
			yield return CaptureVisualEvidence(FoundationE2EVisualEvidenceFiles[2]);

			RuntimeTabletop tabletop = controller.ScenarioRun.Tabletop;
			yield return SelectActionThroughPanel(controller, tabletop, candidate, mouse, playerInput);
			Assert.That(tabletop.ActiveActions.Count, Is.EqualTo(1));
			ActionInstance actionInstance = tabletop.ActiveActions.Single();
			yield return CaptureVisualEvidence(FoundationE2EVisualEvidenceFiles[3]);

			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			Assert.That(scenarioDirector.ConfirmTurn(), Is.EqualTo(1));
			yield return null;
			Assert.That(actionInstance.Progress, Is.EqualTo(0.5f));
			yield return CaptureVisualEvidence(FoundationE2EVisualEvidenceFiles[4]);

			Assert.That(scenarioDirector.ConfirmTurn(), Is.EqualTo(2));
			float completionTimeoutAt = Time.realtimeSinceStartup + 2f;
			while (tabletop.ActiveActions.Count > 0)
			{
				Assert.Less(Time.realtimeSinceStartup, completionTimeoutAt, "推进第二回合后行动没有完成。");
				yield return null;
			}
			yield return null;
			Assert.That(actionInstance.State, Is.EqualTo(ActionInstanceState.Completed));
			Assert.That(
				CountCards(controller, FoundationTestSceneHarness.TestProductContentId),
				Is.GreaterThan(0));
			yield return WaitUntilViewsDisplayArtwork(
				FoundationTestSceneHarness.TestProductContentId,
				"行动产物卡已经创建，但木头业务卡面没有完成 YooAsset 投影。");
			yield return CaptureVisualEvidence(FoundationE2EVisualEvidenceFiles[5]);
			CreateVisualEvidenceContactSheet(
				FoundationE2EVisualEvidenceFiles,
				FoundationE2EContactSheetFileName);
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_GasDamageFeedbackShakesCameraOnResolvedHit()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			Assert.That(
				controller.Cards.TryGetCard(controller.TargetCardId, out TabletopCard tabletopCard),
				Is.True);
			CharacterCard character = (CharacterCard)tabletopCard;
			Camera camera = GetMainCamera();
			Assert.That(camera, Is.Not.Null);
			Assert.That(camera.GetComponent<CameraShake>(), Is.Not.Null);
			Assert.That(
				GameManager.Config.cameraShakeSources.HasFlag(
					ECameraShakeSources.AbilitySystemDamageResolved),
				Is.True,
				"地基测试场景必须打开纯牌桌能力系统伤害后的镜头震动来源。");

			Transform cameraTransform = camera.transform;
			Vector3 initialPosition = cameraTransform.localPosition;
			EventKit.Type.Send(new AbilitySystemDamageResolvedPresentationEvent(
				character.AbilitySystem,
				17,
				isMissed: false,
				isCriticalHit: false,
				isSilent: false,
				damageType: EDamageType.Physical,
				visualFlags: EEffectVisualFlags.None));

			float movedTimeoutAt = Time.realtimeSinceStartup + 1f;
			while (Vector3.Distance(cameraTransform.localPosition, initialPosition) <= 0.0001f)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					movedTimeoutAt,
					"纯牌桌 GAS 命中表现事件没有驱动现有 CameraShake 组件。");
				yield return null;
			}

			float restoredTimeoutAt = Time.realtimeSinceStartup + 1f;
			while (Vector3.Distance(cameraTransform.localPosition, initialPosition) > 0.0001f)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					restoredTimeoutAt,
					"镜头震动结束后没有回到启动震动前的位置。");
				yield return null;
			}
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_CameraControllerPansAndFocusesFromFormalInputAndCue()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationTabletopCameraMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationTabletopCameraKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			yield return null;

			Camera camera = GetMainCamera();
			Assert.That(camera, Is.Not.Null);
			Assert.That(camera.orthographic, Is.False, "StackCraft 镜头复刻必须使用透视主相机。");
			TabletopCameraController cameraController = camera.GetComponentInParent<TabletopCameraController>();
			Assert.That(cameraController, Is.Not.Null);
			TabletopView tabletopView = Object.FindAnyObjectByType<TabletopView>();
			Assert.That(tabletopView, Is.Not.Null);
			Vector3 initialPosition = cameraController.transform.position;

			Vector2 dragStart = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
			Vector2 dragEnd = dragStart + new Vector2(160f, 0f);
			Move(mouse.position, dragStart);
			yield return null;
			Assert.That(
				Vector2.Distance(GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay), dragStart),
				Is.LessThan(0.1f),
				"正式 Gameplay 指针输入没有读到镜头平移起点。");
			Press(mouse.middleButton);
			yield return null;
			Assert.That(
				GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.MiddleClick),
				Is.True,
				"正式 Gameplay 中键输入没有进入按住状态。");
			Assert.That(
				GameManager.InputSystem.IsGameplayActionBlocked(EGameplayInputAction.MiddleClick),
				Is.False,
				"正式 Gameplay 中键输入被动作图释放门禁阻挡。");
			yield return null;
			Move(mouse.position, dragEnd);
			yield return null;
			Assert.That(
				Vector2.Distance(GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay), dragEnd),
				Is.LessThan(0.1f),
				"正式 Gameplay 指针输入没有读到镜头平移终点。");
			Assert.That(
				GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.MiddleClick),
				Is.True,
				"移动指针后正式 Gameplay 中键输入不再保持按住状态。");

			float panTimeoutAt = Time.realtimeSinceStartup + 2f;
			while (Vector3.Distance(cameraController.transform.position, initialPosition) <= 0.01f)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					panTimeoutAt,
					"正式中键输入没有驱动牌桌镜头控制根平移。");
				yield return null;
			}
			Release(mouse.middleButton);
			yield return null;

			Vector2 focusPosition = new Vector2(1.6f, 1.1f);
			controller.ScenarioRun.Tabletop.RequestPresentationCue(
				TabletopPresentationCue.AtTablePosition(
					TabletopPresentationCueKind.CameraFocus,
					focusPosition));

			float focusTimeoutAt = Time.realtimeSinceStartup + 2f;
			while (!TryProjectScreenToTable(
					camera,
					tabletopView.transform,
					new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
					out Vector2 centeredTablePosition) ||
				Vector2.Distance(centeredTablePosition, focusPosition) > 0.2f)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					focusTimeoutAt,
					"牌桌聚焦表现提示没有把主相机画面中心移动到目标牌桌位置。");
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
		public IEnumerator FoundationMenu_PauseSettingsAndContinueUseFormalMenuStack()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationPauseMenuMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationPauseMenuKeyboard");
			yield return LoadFoundationTabletop();

			ScenarioScreenEffectView screenEffect = Object.FindAnyObjectByType<ScenarioScreenEffectView>();
			Assert.That(screenEffect, Is.Not.Null);
			Assert.That(screenEffect.DisplayedGrayscaleAmount, Is.EqualTo(0f).Within(0.001f));
			Assert.That(screenEffect.DisplayedVignetteIntensity, Is.EqualTo(0f).Within(0.001f));
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(
				playerInput.SwitchCurrentControlScheme(keyboard, mouse),
				Is.True,
				"测试设备必须能匹配正式 Keyboard&Mouse 控制方案。");
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			yield return null;

			Press(keyboard.escapeKey);
			yield return null;
			Release(keyboard.escapeKey);
			yield return null;
			yield return null;

			ScenarioPausePanel pausePanel = null;
			float panelTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (pausePanel == null || !pausePanel.gameObject.activeInHierarchy)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					panelTimeoutAt,
					"按下正式打开菜单输入后，没有通过 UIKit 打开剧本暂停菜单。");
				pausePanel = Object.FindAnyObjectByType<ScenarioPausePanel>();
				yield return null;
			}

			Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Menu));
			Assert.That(Time.timeScale, Is.EqualTo(0f));
			Assert.That(playerInput.currentActionMap.name, Is.EqualTo(EActionMap.UI.ToString()));
			yield return WaitUntilScreenEffectAtLeast(
				() => screenEffect.DisplayedGrayscaleAmount,
				0.8f,
				"暂停菜单打开后没有驱动 StackCraft 对应的灰阶屏幕反馈。");

			Button settingsButton = pausePanel.GetComponentsInChildren<Button>(includeInactive: true)
				.Single(button => button.gameObject.name == "Settings");
			yield return ClickButton(mouse, settingsButton);

			UISettings settingsPanel = null;
			panelTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (settingsPanel == null || !settingsPanel.gameObject.activeInHierarchy)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					panelTimeoutAt,
					"暂停菜单点击设置后，没有压入正式设置面板。");
				settingsPanel = Object.FindAnyObjectByType<UISettings>();
				yield return null;
			}

			Assert.That(pausePanel.gameObject.activeInHierarchy, Is.True);
			Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Menu));
			Assert.That(Time.timeScale, Is.EqualTo(0f));

			Button closeSettingsButton = settingsPanel.GetComponentsInChildren<Button>(includeInactive: true)
				.Single(button => button.gameObject.name == "Close");
			yield return ClickButton(mouse, closeSettingsButton);

			panelTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (settingsPanel != null && settingsPanel.gameObject.activeInHierarchy)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					panelTimeoutAt,
					"设置面板关闭后没有回到暂停菜单栈顶。");
				yield return null;
			}

			Assert.That(pausePanel.gameObject.activeInHierarchy, Is.True);
			Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Menu));
			Assert.That(Time.timeScale, Is.EqualTo(0f));

			Press(keyboard.escapeKey);
			yield return null;
			Release(keyboard.escapeKey);
			yield return null;
			yield return null;

			panelTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (pausePanel != null && pausePanel.gameObject.activeInHierarchy)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					panelTimeoutAt,
					"暂停菜单收到 UI Cancel 后没有退出菜单栈。");
				yield return null;
			}

			Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Gameplay));
			Assert.That(Time.timeScale, Is.EqualTo(1f));
			Assert.That(playerInput.currentActionMap.name, Is.EqualTo(EActionMap.Gameplay.ToString()));
			yield return WaitUntilScreenEffectAtMost(
				() => screenEffect.DisplayedGrayscaleAmount,
				0.05f,
				"暂停菜单退出后灰阶屏幕反馈没有恢复。");

			Press(keyboard.escapeKey);
			yield return null;
			Release(keyboard.escapeKey);
			yield return null;
			yield return null;

			pausePanel = null;
			panelTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (pausePanel == null || !pausePanel.gameObject.activeInHierarchy)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					panelTimeoutAt,
					"再次按下正式打开菜单输入后，没有重新打开剧本暂停菜单。");
				pausePanel = Object.FindAnyObjectByType<ScenarioPausePanel>();
				yield return null;
			}

			Button continueButton = pausePanel.GetComponentsInChildren<Button>(includeInactive: true)
				.Single(button => button.gameObject.name == "Continue");
			yield return ClickButton(mouse, continueButton);

			panelTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (pausePanel != null && pausePanel.gameObject.activeInHierarchy)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					panelTimeoutAt,
					"点击继续后暂停菜单没有关闭并恢复玩法状态。");
				yield return null;
			}

			Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Gameplay));
			Assert.That(Time.timeScale, Is.EqualTo(1f));
			Assert.That(playerInput.currentActionMap.name, Is.EqualTo(EActionMap.Gameplay.ToString()));
			yield return WaitUntilScreenEffectAtMost(
				() => screenEffect.DisplayedGrayscaleAmount,
				0.05f,
				"点击继续后灰阶屏幕反馈没有恢复。");
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
                views.All(view => view.DisplaysArtwork && view.DisplayedArtwork.name != "卡牌占位图"),
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
		public IEnumerator FoundationTabletop_MergingTwoBattlesProjectsOneFormationUntilTheBattleEnds()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			RuntimeTabletop tabletop = controller.ScenarioRun.Tabletop;
			TabletopCardView playerView = FindView(controller.BottomCardId);
			TabletopCardView enemyView = FindView(controller.TargetCardId);
			TabletopCardView secondAllyView = FindView(controller.TopCardId);
			TabletopCardView secondEnemyView = FindView(controller.MiddleCardId);
			Vector2 requestedSeparatedAllyPosition = new Vector2(-2.2f, -2f);
			Vector2 requestedSeparatedEnemyPosition = new Vector2(-2.2f, -2.1f);
			Assert.That(
				tabletop.TryPlaceSingleCard(
					controller.TopCardId,
					requestedSeparatedAllyPosition,
					out _),
				Is.True,
				"第二场战斗布景必须先把友方卡牌放到真实分离区域。");
			Assert.That(
				tabletop.TryPlaceSingleCard(
					controller.MiddleCardId,
					requestedSeparatedEnemyPosition,
					out _),
				Is.True,
				"第二场战斗布景必须先把敌方卡牌放到真实分离区域。");
			Vector2 separatedAllyPosition = controller.Cards
				.GetStackContaining(controller.TopCardId)
				.Position;
			Vector2 separatedEnemyPosition = controller.Cards
				.GetStackContaining(controller.MiddleCardId)
				.Position;
			yield return WaitUntilCardViewAt(
				secondAllyView,
				separatedAllyPosition,
				"第二场战斗友方布景卡没有移动到分离战斗区域。");
			yield return WaitUntilCardViewAt(
				secondEnemyView,
				separatedEnemyPosition,
				"第二场战斗敌方布景卡没有移动到分离战斗区域。");
			Vector3 playerStackViewPosition = playerView.transform.localPosition;
			Vector3 enemyStackViewPosition = enemyView.transform.localPosition;
			Vector3 secondAllyStackViewPosition = secondAllyView.transform.localPosition;
			Vector3 secondEnemyStackViewPosition = secondEnemyView.transform.localPosition;
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
			Battle sourceBattle = tabletop.StartBattle(
				new[] { controller.TopCardId },
				new[] { controller.MiddleCardId });
			float areaViewTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (Object.FindObjectsByType<TabletopBattleAreaView>().Length != 2)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					areaViewTimeoutAt,
					"两场分离战斗没有投影为两个可见战斗区域。");
				yield return null;
			}

			Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(2));
			TabletopBattleAreaView destinationAreaView = Object
				.FindObjectsByType<TabletopBattleAreaView>()
				.Single(view => ReferenceEquals(view.Battle, battle));
			float destinationWidthBeforeReinforcement = destinationAreaView.DisplayedArea.width;
			Assert.That(battle.Sides, Has.Count.EqualTo(2));
			CollectionAssert.AreEqual(new[] { controller.BottomCardId }, battle.Sides[0].CardIds);
			CollectionAssert.AreEqual(new[] { controller.TargetCardId }, battle.Sides[1].CardIds);
			Vector2 playerBattlePosition = new Vector2(battleAnchor.x - 1.5f, battleAnchor.y);
			Vector2 enemyBattlePosition = new Vector2(battleAnchor.x + 1.5f, battleAnchor.y);
			yield return WaitUntilCardViewAt(
				playerView,
				playerBattlePosition,
				"开战后玩家阵营卡牌没有进入剧本阵型派生的位置。");
			yield return WaitUntilCardViewAt(
				enemyView,
				enemyBattlePosition,
				"开战后敌对阵营卡牌没有进入剧本阵型派生的位置。");
			Assert.That(
				Vector3.Distance(
					playerView.transform.localPosition,
					TabletopCoordinateSpace.ToLocalPosition(playerBattlePosition)),
				Is.LessThan(0.001f),
				"开战后玩家阵营卡牌必须进入剧本阵型派生的位置。");
			Assert.That(
				Vector3.Distance(
					enemyView.transform.localPosition,
					TabletopCoordinateSpace.ToLocalPosition(enemyBattlePosition)),
				Is.LessThan(0.001f),
				"开战后敌对阵营卡牌必须进入剧本阵型派生的位置。");
			Assert.That(playerView.SortingOrder, Is.EqualTo(100));
			Assert.That(enemyView.SortingOrder, Is.EqualTo(101));
			Assert.That(
				controller.Cards.GetStackContaining(controller.BottomCardId).Position,
				Is.EqualTo(playerStackPosition),
				"阵型表现不能改写玩家卡牌的权威牌堆位置。");
			Assert.That(
				controller.Cards.GetStackContaining(controller.TargetCardId).Position,
				Is.EqualTo(enemyStackPosition),
				"阵型表现不能改写敌对卡牌的权威牌堆位置。");
			Assert.Throws<InvalidOperationException>(() => tabletop.RemoveCard(controller.TargetCardId));

			Vector3 secondAllyBattlePosition = secondAllyView.transform.localPosition;
			Vector3 secondEnemyBattlePosition = secondEnemyView.transform.localPosition;
			CharacterCard reinforcement = (CharacterCard)tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				Vector2.zero);
			CharacterCard secondReinforcement = (CharacterCard)tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				Vector2.zero);
			tabletop.JoinBattle(battle, sideIndex: 0, reinforcement.Id);
			tabletop.JoinBattle(battle, sideIndex: 0, secondReinforcement.Id);
			areaViewTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (Object.FindObjectsByType<TabletopBattleAreaView>().Length != 1)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					areaViewTimeoutAt,
					"成员加入后扩张并重叠的战斗区域没有自动合并。");
				yield return null;
			}

			Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(1));
			Assert.That(tabletop.ActiveBattles[0], Is.SameAs(battle));
			Assert.That(sourceBattle.IsEnded, Is.True);
			CollectionAssert.AreEqual(
				new[]
				{
					controller.BottomCardId,
					reinforcement.Id,
					secondReinforcement.Id,
					controller.TopCardId
				},
				battle.Sides[0].CardIds);
			CollectionAssert.AreEqual(
				new[] { controller.TargetCardId, controller.MiddleCardId },
				battle.Sides[1].CardIds);
			Assert.That(playerView.SortingOrder, Is.EqualTo(100));
			Assert.That(secondAllyView.SortingOrder, Is.EqualTo(103));
			Assert.That(enemyView.SortingOrder, Is.EqualTo(104));
			Assert.That(secondEnemyView.SortingOrder, Is.EqualTo(105));
			TabletopBattleAreaView mergedAreaView = Object.FindAnyObjectByType<TabletopBattleAreaView>();
			Assert.That(mergedAreaView.Battle, Is.SameAs(battle));
			Assert.That(
				mergedAreaView.DisplayedArea.width,
				Is.GreaterThan(destinationWidthBeforeReinforcement));
			Assert.That(
				Vector3.Distance(secondAllyView.transform.localPosition, secondAllyBattlePosition),
				Is.GreaterThan(0.001f),
				"第二场战斗的第一方必须立即进入目标战斗的合并阵型。");
			Assert.That(
				Vector3.Distance(secondEnemyView.transform.localPosition, secondEnemyBattlePosition),
				Is.GreaterThan(0.001f),
				"第二场战斗的第二方必须立即进入目标战斗的合并阵型。");

			tabletop.EndBattle(battle);
			areaViewTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (Object.FindObjectsByType<TabletopBattleAreaView>().Length != 0)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					areaViewTimeoutAt,
					"战斗结束后战斗区域视图没有释放。");
				yield return null;
			}

			Assert.That(battle.IsEnded, Is.True);
			Assert.That(tabletop.ActiveBattles, Is.Empty);
			yield return WaitUntilCardViewAt(
				playerView,
				playerStackPosition,
				"战斗结束后玩家卡牌没有回到权威牌堆视图位置。");
			yield return WaitUntilCardViewAt(
				enemyView,
				enemyStackPosition,
				"战斗结束后敌对卡牌没有回到权威牌堆视图位置。");
			yield return WaitUntilCardViewAt(
				secondAllyView,
				TabletopCoordinateSpace.ToTablePosition(secondAllyStackViewPosition),
				"战斗结束后第二方友军卡牌没有回到权威牌堆视图位置。");
			yield return WaitUntilCardViewAt(
				secondEnemyView,
				TabletopCoordinateSpace.ToTablePosition(secondEnemyStackViewPosition),
				"战斗结束后第二方敌军卡牌没有回到权威牌堆视图位置。");
			Assert.That(
				Vector3.Distance(playerView.transform.localPosition, playerStackViewPosition),
				Is.LessThan(0.001f),
				"战斗结束后玩家卡牌必须回到权威牌堆视图位置。");
			Assert.That(
				Vector3.Distance(enemyView.transform.localPosition, enemyStackViewPosition),
				Is.LessThan(0.001f),
				"战斗结束后敌对卡牌必须回到权威牌堆视图位置。");
			Assert.That(playerView.SortingOrder, Is.EqualTo(10));
			Assert.That(enemyView.SortingOrder, Is.EqualTo(10));
			Assert.That(
				Vector3.Distance(secondAllyView.transform.localPosition, secondAllyStackViewPosition),
				Is.LessThan(0.001f));
			Assert.That(
				Vector3.Distance(secondEnemyView.transform.localPosition, secondEnemyStackViewPosition),
				Is.LessThan(0.001f));
			Assert.DoesNotThrow(() => tabletop.RemoveCard(controller.TargetCardId));
			Assert.That(controller.Cards.CardCount, Is.EqualTo(5));
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_DraggingBattleParticipantOutsideAreaLeavesBattle()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationBattleFleeMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationBattleFleeKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			RuntimeTabletop tabletop = controller.ScenarioRun.Tabletop;
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(
				playerInput.SwitchCurrentControlScheme(keyboard, mouse),
				Is.True,
				"测试设备必须能匹配正式 Keyboard&Mouse 控制方案。");
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			yield return null;

			TabletopCardView playerView = FindView(controller.BottomCardId);
			TabletopCardView enemyView = FindView(controller.TargetCardId);
			Vector2 playerStackPosition = controller.Cards
				.GetStackContaining(controller.BottomCardId)
				.Position;
			Battle battle = tabletop.StartBattle(
				new[] { controller.BottomCardId },
				new[] { controller.TargetCardId });
			float areaViewTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (Object.FindObjectsByType<TabletopBattleAreaView>().Length != 1)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					areaViewTimeoutAt,
					"开战后没有生成可见战斗区域，无法验证拖出战斗区离战。");
				yield return null;
			}

			Assert.That(
				tabletop.TryGetBattlePose(
					controller.BottomCardId,
					0,
					out TabletopCardPose playerBattlePose),
				Is.True,
				"开战后玩家卡牌没有进入正式战斗阵型。");
			Assert.That(
				tabletop.TryGetBattlePose(
					controller.TargetCardId,
					0,
					out TabletopCardPose enemyBattlePose),
				Is.True,
				"开战后敌对卡牌没有进入正式战斗阵型。");
			Vector2 expectedPlayerBattlePosition =
				TabletopCoordinateSpace.ToTablePosition(playerBattlePose.LocalPosition);
			Vector2 expectedEnemyBattlePosition =
				TabletopCoordinateSpace.ToTablePosition(enemyBattlePose.LocalPosition);
			yield return WaitUntilCardViewAt(
				playerView,
				expectedPlayerBattlePosition,
				"离战测试开战后玩家卡牌还没有移动到稳定战斗位置。");
			yield return WaitUntilCardViewAt(
				enemyView,
				expectedEnemyBattlePosition,
				"离战测试开战后敌对卡牌还没有移动到稳定战斗位置。");

			Rect battleArea = tabletop.GetBattleArea(battle);
			Vector2 battlePose = expectedPlayerBattlePosition;
			Assert.That(Vector2.Distance(battlePose, playerStackPosition), Is.GreaterThan(0.001f));
			Physics.SyncTransforms();
			Camera camera = GetMainCamera();
			TabletopView tabletopView = Object.FindAnyObjectByType<TabletopView>();
			Vector2 pressScreenPosition = FindScreenPointThatHitsCard(
				camera,
				playerView.GetComponent<BoxCollider>(),
				controller.BottomCardId);
			Vector2 pressTablePosition = ScreenToTablePosition(camera, playerView, pressScreenPosition);
			Assert.That(
				FindCardHitAt(camera, pressScreenPosition).CardId,
				Is.EqualTo(controller.BottomCardId),
				"离战测试按下点必须通过正式物理射线命中参战玩家卡牌。");
			Assert.That(
				UIPointerUtility.IsPositionOverUI(pressScreenPosition),
				Is.False,
				"离战测试按下点被正式 UI 射线挡住，牌桌输入不会开始拖拽。" +
				" UI命中=" + DescribeUiRaycastAt(pressScreenPosition));
			Rect placementBounds = controller.PlacementRules.Area.Bounds;
			Vector2 releaseTablePosition = new Vector2(battleArea.xMin - 1f, battleArea.center.y);
			Assert.That(
				battleArea.Contains(releaseTablePosition),
				Is.False,
				"离战测试释放点必须位于战斗区域外。");
			Assert.That(
				placementBounds.Contains(releaseTablePosition),
				Is.True,
				"离战测试释放点必须仍位于牌桌可放置边界内。");
			Vector2 releaseScreenPosition = TableToScreenPoint(camera, playerView, releaseTablePosition);
			Assert.That(
				UIPointerUtility.IsPositionOverUI(releaseScreenPosition),
				Is.False,
				"离战测试释放点被正式 UI 射线接管，无法验证牌桌空白释放语义。" +
				" UI命中=" + DescribeUiRaycastAt(releaseScreenPosition));
			Assert.That(
				TryProjectScreenToTable(
					camera,
					tabletopView.transform,
					releaseScreenPosition,
					out Vector2 projectedReleaseTablePosition),
				Is.True,
				"离战测试释放屏幕点必须能投影回正式牌桌平面。");
			Assert.That(
				Vector2.Distance(projectedReleaseTablePosition, releaseTablePosition),
				Is.LessThan(0.001f),
				"离战测试释放屏幕点和牌桌坐标必须互相对应。");

			TabletopCardDragInput dragInput = Object.FindAnyObjectByType<TabletopCardDragInput>();
			Assert.That(dragInput, Is.Not.Null, "统一地基场景缺少正式牌桌拖拽输入组件。");
			Assert.That(dragInput.isActiveAndEnabled, Is.True, "正式牌桌拖拽输入组件没有处于启用状态。");
			Assert.That(
				GameManager.InputSystem.IsGameplayInputLocked,
				Is.False,
				"离战测试开始前 Gameplay 输入被正式流程锁定。");
			Assert.That(
				GameManager.InputSystem.IsGameplayActionBlocked(EGameplayInputAction.Click),
				Is.False,
				"离战测试开始前 Gameplay 点击输入被动作图释放门禁阻挡。");
			int releaseCountBeforeDrag = controller.ReleaseIntentCount;
			Move(mouse.position, pressScreenPosition);
			yield return null;
			Assert.That(
				Vector2.Distance(
					GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay),
					pressScreenPosition),
				Is.LessThan(0.1f),
				"正式 Gameplay 指针输入没有读到离战拖拽按下点。");
			Press(mouse.leftButton);
			yield return null;
			Assert.That(
				GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.Click),
				Is.True,
				"按下鼠标左键后 Gameplay 点击 Action 没有进入按下态。");
			Assert.That(
				GameManager.InputSystem.IsGameplayInputLocked,
				Is.False,
				"按下参战卡牌后 Gameplay 输入被其它正式流程锁定，拖拽会话被取消。");
			Assert.That(
				GameManager.InputSystem.IsGameplayActionBlocked(EGameplayInputAction.Click),
				Is.False,
				"按下参战卡牌后 Gameplay 点击输入被释放门禁阻挡。");
			Assert.That(
				dragInput.IsPointerSessionActive,
				Is.True,
				"离战测试按下参战卡牌后没有启动正式牌桌拖拽会话。");
			Move(mouse.position, releaseScreenPosition);
			yield return null;
			yield return null;
			Assert.That(
				dragInput.IsDragging,
				Is.True,
				"离战测试移动指针超过正式阈值后没有进入拖拽状态。");
			Release(mouse.leftButton);
			yield return null;
			yield return null;
			Assert.That(
				controller.ReleaseIntentCount,
				Is.GreaterThan(releaseCountBeforeDrag),
				BuildBattleLeaveDiagnostic(
					controller,
					battleArea,
					placementBounds,
					releaseTablePosition,
					pressTablePosition,
					battlePose,
					"参战卡牌拖出战斗区域没有形成正式释放事实。"));
			Assert.That(controller.LastReleaseIntent.CardId, Is.EqualTo(controller.BottomCardId));
			Assert.That(
				controller.LastReleaseIntent.IsDrag,
				Is.True,
				BuildBattleLeaveDiagnostic(
					controller,
					battleArea,
					placementBounds,
					releaseTablePosition,
					pressTablePosition,
					battlePose,
					"参战卡牌拖出战斗区域形成的不是拖拽释放事实。"));
			Assert.That(
				battleArea.Contains(controller.LastReleaseIntent.ReleasePointerPosition),
				Is.False,
				BuildBattleLeaveDiagnostic(
					controller,
					battleArea,
					placementBounds,
					releaseTablePosition,
					pressTablePosition,
					battlePose,
					"参战卡牌释放事实仍落在战斗区域内。"));
			Assert.That(
				placementBounds.Contains(controller.LastReleaseIntent.RequestedStackPosition),
				Is.True,
				BuildBattleLeaveDiagnostic(
					controller,
					battleArea,
					placementBounds,
					releaseTablePosition,
					pressTablePosition,
					battlePose,
					"参战卡牌释放后的请求堆位置不在牌桌可放置边界内。"));

			float leaveTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (!battle.IsEnded)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					leaveTimeoutAt,
					BuildBattleLeaveDiagnostic(
						controller,
						battleArea,
						placementBounds,
						releaseTablePosition,
						pressTablePosition,
						battlePose,
						"参战玩家卡牌被拖到战斗区域外后没有离开战斗。"));
				yield return null;
			}
			while (Object.FindObjectsByType<TabletopBattleAreaView>().Length != 0)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					leaveTimeoutAt,
					"玩家卡牌离战并结束战斗后，战斗区域视图没有释放。");
				yield return null;
			}

			Assert.That(tabletop.ActiveBattles, Is.Empty);
			Assert.That(
				controller.LastActionCandidates,
				Is.Empty,
				"参战卡牌拖出战斗区域时应由战斗释放语义接管，不应打开普通卡牌行动候选。");
			TabletopCardStack placedStack = controller.Cards.GetStackContaining(controller.BottomCardId);
			TabletopCardStack enemyStack = controller.Cards.GetStackContaining(controller.TargetCardId);
			TabletopCardStackGeometry geometry = controller.PlacementRules.Geometry;
			Rect placedFootprint = geometry.CalculateFootprint(
				placedStack.Position,
				placedStack.Cards.Count);
			Rect enemyFootprint = geometry.CalculateFootprint(
				enemyStack.Position,
				enemyStack.Cards.Count);
			Rect bounds = controller.PlacementRules.Area.Bounds;
			Assert.That(
				bounds.Contains(placedFootprint.min) && bounds.Contains(placedFootprint.max),
				Is.True,
				"离战后的逃离牌堆必须落在牌桌可放置边界内。");
			float overlapX = (placedFootprint.width + enemyFootprint.width) * 0.5f -
				Mathf.Abs(placedFootprint.center.x - enemyFootprint.center.x);
			float overlapY = (placedFootprint.height + enemyFootprint.height) * 0.5f -
				Mathf.Abs(placedFootprint.center.y - enemyFootprint.center.y);
			Assert.That(
				overlapX <= 0.001f || overlapY <= 0.001f,
				Is.True,
				"离战放置完成后，逃离牌堆和非逃离方牌堆的权威占地不能重叠。");
			yield return WaitUntilCardViewAt(
				playerView,
				placedStack.Position,
				"战斗离场后逃离方卡牌没有移动到权威牌堆位置。");
			yield return WaitUntilCardViewAt(
				enemyView,
				enemyStack.Position,
				"战斗离场后非逃离方卡牌没有回到权威牌堆位置。");
			Assert.That(
				Vector2.Distance(ToTablePosition(playerView), placedStack.Position),
				Is.LessThan(0.001f));
			Assert.That(
				Vector2.Distance(ToTablePosition(enemyView), enemyStack.Position),
				Is.LessThan(0.001f),
				"战斗结束后非逃离方也必须回到自己的权威牌堆位置。");
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_BattleAutomaticallyAttacksWhileWorldRemainsTurnBased()
		{
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			RuntimeTabletop tabletop = controller.ScenarioRun.Tabletop;
			Assert.That(
				controller.ScenarioRun.ProgressionMode,
				Is.EqualTo(ActionProgressionMode.TurnBased));
			CharacterCard attacker = (CharacterCard)tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				new Vector2(-1f, 2f));
			CharacterCard target = (CharacterCard)tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				new Vector2(1f, 2f));
			attacker.AbilitySystem.SetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.AttackSpeed,
				200f);
			target.AbilitySystem.SetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.AttackSpeed,
				50f);
			Battle battle = tabletop.StartBattle(
				new[] { attacker.Id },
				new[] { target.Id });
			float attackerHealthBefore = attacker.CurrentHealth;
			float healthBefore = target.AbilitySystem.GetAttrCurrentValue(
				XAttrSet.FightUnit,
				XAttribute.Health);
			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (target.AbilitySystem.GetAttrCurrentValue(
				       XAttrSet.FightUnit,
				       XAttribute.Health) >= healthBefore)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"回合制世界中的活动战斗没有自动推进，或自动攻击没有通过正式 EX-GAS 结算目标生命。");
				yield return null;
			}

			Assert.That(
				target.AbilitySystem.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Health),
				Is.LessThan(healthBefore));
			Assert.That(attacker.CurrentHealth, Is.EqualTo(attackerHealthBefore).Within(0.001f));
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
		public IEnumerator FoundationTabletop_AutomaticBattleRemovesDefeatedCardAndEnds()
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
			attacker.AbilitySystem.SetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.AttackSpeed,
				200f);
			target.AbilitySystem.SetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.AttackSpeed,
				0f);
			target.AbilitySystem.SetAttrBaseValue(
				XAttrSet.FightUnit,
				XAttribute.Health,
				5f);
			Battle battle = tabletop.StartBattle(
				new[] { attacker.Id },
				new[] { target.Id });

			float timeoutAt = Time.realtimeSinceStartup + 7f;
			while (!battle.IsEnded)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"自动攻击已经足以击败目标，但目标没有离开牌桌或战斗没有结束。");
				yield return null;
			}

			Assert.That(tabletop.Cards.TryGetCard(attacker.Id, out _), Is.True);
			Assert.That(tabletop.Cards.TryGetCard(target.Id, out _), Is.False);
			Assert.That(tabletop.ActiveBattles, Is.Empty);
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
			Camera camera = GetMainCamera();
			BoxCollider middleCollider = middleView.GetComponent<BoxCollider>();
			Vector2 draggedSegmentAnchorPosition = controller.Cards.GetCardTablePosition(
				controller.MiddleCardId,
				controller.PlacementRules.Geometry);
			Vector2 pressScreenPosition = FindScreenPointThatHitsCard(
				camera,
				middleCollider,
				controller.MiddleCardId);
			Vector2 pressTablePosition = ScreenToTablePosition(camera, middleView, pressScreenPosition);
			var releaseTablePosition = new Vector2(0f, -4f);
			Vector2 releaseScreenPosition = TableToScreenPoint(camera, middleView, releaseTablePosition);
			Vector2 requestedStackPosition = releaseTablePosition +
				(draggedSegmentAnchorPosition - pressTablePosition);

			Move(mouse.position, pressScreenPosition);
			yield return null;
			TabletopCardDragInput dragInput = Object.FindAnyObjectByType<TabletopCardDragInput>();
			Assert.That(dragInput, Is.Not.Null);
			TabletopCardView hitView = FindCardHitAt(camera, pressScreenPosition);
			Assert.That(
				hitView.CardId,
				Is.EqualTo(controller.MiddleCardId),
				"测试按下点必须命中中间牌外露区域，不能命中上层牌。");
			Press(mouse.leftButton);
			yield return null;
			Assert.That(
				dragInput.IsPointerSessionActive,
				Is.True,
				"从中间牌外露区域按下后，必须启动牌桌拖拽会话。");
			Move(mouse.position, releaseScreenPosition);
			yield return null;
			yield return null;
			Assert.That(
				dragInput.IsDragging,
				Is.True,
				"指针移动超过正式拖拽阈值后，必须进入拖拽状态。");
			Assert.That(
				Vector2.Distance(ToTablePosition(middleView), requestedStackPosition),
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
			yield return WaitUntilCardViewAt(
				middleView,
				expectedPosition,
				"视图必须按 StackCraft 0.1 秒移动补间投影到权威修订后的新堆位置。");
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
            Camera camera = GetMainCamera();
            BoxCollider middleCollider = middleView.GetComponent<BoxCollider>();
            Vector2 pressScreenPosition = FindScreenPointThatHitsCard(
                camera,
                middleCollider,
                controller.MiddleCardId);
			TabletopCardStackGeometry geometry = controller.PlacementRules.Geometry;
			Vector2 draggedSegmentAnchorPosition = controller.Cards.GetCardTablePosition(
				controller.MiddleCardId,
				geometry);
			Vector2 pressTablePosition = ScreenToTablePosition(camera, middleView, pressScreenPosition);
			Vector2 pointerToStackOffset = draggedSegmentAnchorPosition - pressTablePosition;
			Vector2 releaseTablePosition = FindBlankReleasePointThatOverlapsStack(
				camera,
				middleView,
				targetOriginalPosition,
				pointerToStackOffset,
				geometry,
				draggedCardCount: 2);
            Vector2 releaseScreenPosition = TableToScreenPoint(camera, middleView, releaseTablePosition);
            Assert.That(
				TryFindCardHitAt(camera, releaseScreenPosition, out _),
				Is.False,
                "重叠验收的指针落点必须按正式二维牌面命中规则落在空白桌面，不能进入行动查询分支。");

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

			yield return WaitUntilCardViewAt(
				middleView,
				placedStack.Position,
				"释放到空白桌面后，被拖拽牌堆视图必须按 StackCraft 0.1 秒补间移动到权威位置。");
			yield return WaitUntilCardViewAt(
				targetView,
				targetStack.Position,
				"释放后被空间解算推开的目标牌堆视图必须按 StackCraft 0.1 秒补间移动到权威位置。");
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
            TabletopView tabletopView = Object.FindAnyObjectByType<TabletopView>();
            Assert.That(tabletopView, Is.Not.Null);
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
            Camera camera = GetMainCamera();
            BoxCollider sourceCollider = sourceView.GetComponent<BoxCollider>();
			Vector2 pressScreenPosition = FindScreenPointThatHitsCard(
				camera,
				sourceCollider,
				controller.MiddleCardId);
			TabletopCardStackGeometry geometry = controller.PlacementRules.Geometry;
			Vector2 draggedSegmentAnchorPosition = controller.Cards.GetCardTablePosition(
				controller.MiddleCardId,
				geometry);
			Vector2 pressTablePosition = ScreenToTablePosition(camera, sourceView, pressScreenPosition);
            Vector2 targetScreenPosition = camera.WorldToScreenPoint(targetView.transform.position);

            TabletopCardView pressedView = FindCardHitAt(camera, pressScreenPosition);
            Assert.That(
                pressedView.CardId,
                Is.EqualTo(controller.MiddleCardId),
                "中间卡牌暴露区域必须按正式二维牌面命中规则先命中中间卡牌，而不是顶部或底部卡牌。");

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

			float clickThreshold = tabletopView.CardClickThreshold;
			Assert.That(
				clickThreshold,
				Is.EqualTo(0.02f).Within(0.0001f),
				"StackCraft 同态测试必须使用 clickThreshold = 0.02 的牌桌世界距离。");
			Vector2 belowDragThresholdTablePosition = pressTablePosition +
				Vector2.right * (clickThreshold * 0.5f);
			Vector2 belowDragThresholdPosition = TableToScreenPoint(
				camera,
				sourceView,
				belowDragThresholdTablePosition);
			Move(mouse.position, belowDragThresholdPosition);
			yield return null;
			Assert.That(dragInput.IsDragging, Is.False, "牌桌世界位移未达到 StackCraft 点击判定距离时不能开始拖拽。");
			Vector2 expectedBelowThresholdAnchor = belowDragThresholdTablePosition +
				(draggedSegmentAnchorPosition - pressTablePosition);
			Vector3 expectedBelowThresholdLocalPosition =
				TabletopCoordinateSpace.ToLocalPosition(
					expectedBelowThresholdAnchor,
					tabletopView.CardDragHeight);
			Assert.That(
				Vector3.Distance(sourceView.transform.localPosition, expectedBelowThresholdLocalPosition),
				Is.LessThan(0.001f),
				"StackCraft 按下后会立即把卡牌抬到 DragHeight 跟随指针；点击阈值只决定释放解释，不阻止视觉拿起。");
			Assert.That(sourceView.transform.position, Is.Not.EqualTo(sourceAuthoritativePosition));
			Assert.That(topView.transform.position, Is.Not.EqualTo(topAuthoritativePosition));

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
			TabletopCardStack sourceStack = controller.Cards.GetStackContaining(controller.MiddleCardId);
			yield return WaitUntilCardViewAt(
				sourceView,
				sourceStack.Position + geometry.StackStep * sourceStack.IndexOf(controller.MiddleCardId),
				"释放到行动目标后，来源中间牌必须按 StackCraft 0.1 秒补间回到权威牌堆姿态。");
			yield return WaitUntilCardViewAt(
				topView,
				sourceStack.Position + geometry.StackStep * sourceStack.IndexOf(controller.TopCardId),
				"释放到行动目标后，尾随顶牌必须按 StackCraft 0.1 秒补间回到权威牌堆姿态。");
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
			Button progressionModeButton = Object.FindObjectsByType<Button>(
					FindObjectsInactive.Exclude,
					FindObjectsSortMode.None)
				.Single(button => button.gameObject.name == "ProgressionMode");
			Assert.That(progressionModeButton.interactable, Is.True);
			yield return ClickButton(mouse, progressionModeButton);
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
            Camera camera = GetMainCamera();
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
		public IEnumerator FoundationTabletop_PlayerClicksCardPackAndDrawsEachSlotThroughFormalAction()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationCardPackMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationCardPackKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			controller.CreateCardPackTestCard();
			yield return null;

			int initialVillagerCount = CountCards(controller, FoundationTestSceneHarness.TestContentId);
			int initialWoodCount = CountCards(controller, FoundationTestSceneHarness.TestProductContentId);

			yield return ClickCard(mouse, controller.CardPackId);
			Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
			Assert.That(
				controller.LastActionCandidates[0].Action.ContentId,
				Is.EqualTo(new ContentId(FoundationTestSceneHarness.TestOpenCardPackActionContentId)));
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);

			Assert.That(
				controller.Cards.TryGetCard(controller.CardPackId, out TabletopCard pack),
				Is.True);
			Assert.That(pack.RemainingUses, Is.EqualTo(3));
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestContentId), Is.EqualTo(initialVillagerCount + 1));
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCardPackFirstRewardContentId), Is.Zero);
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCardPackSecondRewardContentId), Is.Zero);
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestProductContentId), Is.EqualTo(initialWoodCount));

			yield return ClickCard(mouse, controller.CardPackId);
			Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);

			Assert.That(controller.Cards.TryGetCard(controller.CardPackId, out pack), Is.True);
			Assert.That(pack.RemainingUses, Is.EqualTo(2));
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCardPackFirstRewardContentId), Is.EqualTo(1));
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCardPackSecondRewardContentId), Is.Zero);
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestProductContentId), Is.EqualTo(initialWoodCount));

			yield return ClickCard(mouse, controller.CardPackId);
			Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);

			Assert.That(controller.Cards.TryGetCard(controller.CardPackId, out pack), Is.True);
			Assert.That(pack.RemainingUses, Is.EqualTo(1));
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCardPackSecondRewardContentId), Is.EqualTo(1));
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestProductContentId), Is.EqualTo(initialWoodCount));

			yield return ClickCard(mouse, controller.CardPackId);
			Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);

			Assert.That(controller.Cards.TryGetCard(controller.CardPackId, out _), Is.False);
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestProductContentId), Is.EqualTo(initialWoodCount + 1));
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_PlayerPaysPackVendorInTwoTransactionsAndReceivesPack()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationPackVendorMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationPackVendorKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			controller.CreatePackVendorTestCards();
			yield return null;

			TabletopCardView vendorView = FindView(controller.PackVendorId);
			yield return WaitUntilPackVendorSurface(
				vendorView,
				"初始卡包",
				"价格：2",
				"已发现：\n0/4");

			AssertSinglePurchaseCandidate(
				controller,
				controller.FirstPackPaymentId,
				controller.PackVendorId,
				"卡包商贩付款规则候选本身必须成立。");

			yield return HoldDraggedCardOverCard(mouse, controller.MiddleCardId, controller.PackVendorId);
			Assert.That(
				vendorView.IsHighlighted,
				Is.False,
				"StackCraft 交易区高亮只表示当前拖入对象可以交易；普通非付款卡拖到商贩上不能高亮。");
			Release(mouse.leftButton);
			yield return null;
			yield return null;

			int releaseCountBeforeFirstPaymentDrag = controller.ReleaseIntentCount;
			yield return HoldDraggedCardOverCard(mouse, controller.FirstPackPaymentId, controller.PackVendorId);
			Assert.That(
				vendorView.IsHighlighted,
				Is.True,
				"金币拖到卡包商贩上时必须按 StackCraft CanTrade 语义高亮交易区。");
			Release(mouse.leftButton);
			yield return null;
			yield return null;
			Assert.That(
				controller.ReleaseIntentCount,
				Is.GreaterThan(releaseCountBeforeFirstPaymentDrag),
				BuildLastReleaseDiagnostic(controller, "第一次拖拽付款没有形成正式释放事实。"));
			Assert.That(
				controller.LastActionCandidates.Count,
				Is.EqualTo(1),
				BuildLastReleaseDiagnostic(controller, "第一次拖拽付款后没有得到购买候选。"));
			Assert.That(
				controller.LastActionCandidates[0].Action.ContentId,
				Is.EqualTo(new ContentId(FoundationTestSceneHarness.TestPurchaseCardPackActionContentId)));
			Assert.That(
				CountPlayingCardSmokeEffects(),
				Is.Zero,
				"付款前不应已经存在卡包商贩交易烟雾。");
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);
			yield return WaitUntilPlayingCardSmokeEffectAtLeast(
				1,
				"第一次成功付款后没有播放 StackCraft 交易烟雾。");
			yield return WaitUntilCardSmokeEffectsReleased("第一次付款烟雾没有按粒子时长结束。");

			Assert.That(
				controller.Cards.TryGetCard(controller.PackVendorId, out TabletopCard vendorCard),
				Is.True);
			Assert.That(((PackVendorCard)vendorCard).PaidAmount, Is.EqualTo(1));
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCardPackContentId), Is.Zero);
			yield return WaitUntilPackVendorSurface(
				vendorView,
				"初始卡包",
				"价格：1",
				"已发现：\n0/4");

			yield return ClickCard(mouse, controller.PackVendorId);
			TabletopCardInfoPanel infoPanel = Object.FindAnyObjectByType<TabletopCardInfoPanel>();
			Assert.That(infoPanel.DisplayedDescription, Does.Contain("剩余价格：1"));

			AssertSinglePurchaseCandidate(
				controller,
				controller.SecondPackPaymentId,
				controller.PackVendorId,
				"卡包商贩第二次付款规则候选本身必须成立。");
			int releaseCountBeforeSecondPaymentDrag = controller.ReleaseIntentCount;
			yield return HoldDraggedCardOverCard(mouse, controller.SecondPackPaymentId, controller.PackVendorId);
			Assert.That(
				vendorView.IsHighlighted,
				Is.True,
				"第二枚金币拖到卡包商贩上时必须继续高亮交易区。");
			Release(mouse.leftButton);
			yield return null;
			yield return null;
			Assert.That(
				controller.ReleaseIntentCount,
				Is.GreaterThan(releaseCountBeforeSecondPaymentDrag),
				BuildLastReleaseDiagnostic(controller, "第二次拖拽付款没有形成正式释放事实。"));
			Assert.That(
				controller.LastActionCandidates.Count,
				Is.EqualTo(1),
				BuildLastReleaseDiagnostic(controller, "第二次拖拽付款后没有得到购买候选。"));
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);
			yield return WaitUntilPlayingCardSmokeEffectAtLeast(
				1,
				"第二次成功付款后没有播放 StackCraft 交易烟雾。");
			yield return WaitUntilCardSmokeEffectsReleased("第二次付款烟雾没有按粒子时长结束。");

			Assert.That(((PackVendorCard)vendorCard).PaidAmount, Is.Zero);
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCardPackContentId), Is.EqualTo(1));
			TabletopCard purchasedPack = controller.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.Single(card => card.ContentId == new ContentId(FoundationTestSceneHarness.TestCardPackContentId));
			Assert.That(
				controller.ScenarioRun.Tabletop.ContentIndex.TryGet(
					vendorCard.ContentId,
					out PackVendorDefinition vendorDefinition),
				Is.True);
			Vector2 vendorPosition = controller.Cards.GetStackContaining(controller.PackVendorId).Position;
			Vector2 packPosition = controller.Cards.GetStackContaining(purchasedPack.Id).Position;
			Assert.That(
				Vector2.Distance(packPosition, vendorPosition + vendorDefinition.PackSpawnOffset),
				Is.LessThan(0.001f),
				"购买完成生成的卡包必须按 StackCraft 商贩下方偏移落位。");
			TabletopCardView purchasedPackView = FindView(purchasedPack.Id);
			yield return WaitUntilCardPackInstanceSurface(purchasedPackView, "初始卡包", "Starter");
			yield return WaitUntilPackVendorSurface(
				vendorView,
				"初始卡包",
				"价格：2",
				"已发现：\n0/4");
			yield return ClickCard(mouse, controller.PackVendorId);
			Assert.That(infoPanel.DisplayedDescription, Does.Contain("剩余价格：2"));
			Assert.That(infoPanel.DisplayedDescription, Does.Contain("收藏进度：0/4"));
		}

		[UnityTest]
		public IEnumerator FoundationTabletop_PlayerStoresWithdrawsAndPaysWithChestThroughExistingUi()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationChestMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationChestKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			controller.CreateChestTestCards();
			yield return null;

			yield return DragCardOntoCard(mouse, controller.FirstChestCurrencyId, controller.ChestId);
			Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
			Assert.That(
				controller.LastActionCandidates[0].Action.ContentId,
				Is.EqualTo(new ContentId(FoundationTestSceneHarness.TestDepositCurrencyIntoChestActionContentId)));
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);

			Assert.That(controller.Cards.TryGetCard(controller.ChestId, out TabletopCard chestCard), Is.True);
			ChestCard chest = (ChestCard)chestCard;
			Assert.That(chest.StoredCurrencyCount, Is.EqualTo(1));
			Assert.That(controller.Cards.TryGetCard(controller.FirstChestCurrencyId, out _), Is.False);
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCurrencyCardContentId), Is.EqualTo(1));

			yield return ClickCard(mouse, controller.ChestId);
			TabletopCardInfoPanel infoPanel = Object.FindAnyObjectByType<TabletopCardInfoPanel>();
			Assert.That(infoPanel.DisplayedDescription, Does.Contain("存币：1/2"));
			Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
			Assert.That(
				controller.LastActionCandidates[0].Action.ContentId,
				Is.EqualTo(new ContentId(FoundationTestSceneHarness.TestWithdrawCurrencyFromChestActionContentId)));
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);

			Assert.That(chest.StoredCurrencyCount, Is.Zero);
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCurrencyCardContentId), Is.EqualTo(2));

			TabletopCardId[] currencyCards = GetCardIds(
				controller,
				FoundationTestSceneHarness.TestCurrencyCardContentId);
			yield return DragCardOntoCard(mouse, currencyCards[0], controller.ChestId);
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);
			Assert.That(chest.StoredCurrencyCount, Is.EqualTo(1));

			currencyCards = GetCardIds(controller, FoundationTestSceneHarness.TestCurrencyCardContentId);
			yield return DragCardOntoCard(mouse, currencyCards[0], controller.ChestId);
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);
			Assert.That(chest.StoredCurrencyCount, Is.EqualTo(2));
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCurrencyCardContentId), Is.Zero);

			controller.CreatePackVendorTestCards();
			yield return null;
			yield return DragCardOntoCard(mouse, controller.ChestId, controller.PackVendorId);
			Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
			Assert.That(
				controller.LastActionCandidates[0].Action.ContentId,
				Is.EqualTo(new ContentId(FoundationTestSceneHarness.TestPurchaseCardPackActionContentId)));
			yield return SelectActionThroughPanel(
				controller,
				controller.ScenarioRun.Tabletop,
				controller.LastActionCandidates[0],
				mouse,
				playerInput);

			Assert.That(controller.Cards.TryGetCard(controller.ChestId, out TabletopCard stillPresent), Is.True);
			Assert.That(stillPresent, Is.SameAs(chest));
			Assert.That(chest.StoredCurrencyCount, Is.Zero);
			Assert.That(CountCards(controller, FoundationTestSceneHarness.TestCardPackContentId), Is.EqualTo(1));
			Assert.That(
				controller.Cards.TryGetCard(controller.PackVendorId, out TabletopCard vendorCard),
				Is.True);
			Assert.That(((PackVendorCard)vendorCard).PaidAmount, Is.Zero);

			yield return ClickCard(mouse, controller.ChestId);
			Assert.That(infoPanel.DisplayedDescription, Does.Contain("存币：0/2"));
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
			Camera camera = GetMainCamera();
			BoxCollider sourceCollider = sourceView.GetComponent<BoxCollider>();
			Vector2 sourcePosition = FindScreenPointThatHitsCard(
				camera,
				sourceCollider,
				controller.MiddleCardId);
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
			Vector2 additionalPosition = FindScreenPointThatHitsCard(
				camera,
				additionalView.GetComponent<BoxCollider>(),
				controller.BottomCardId);
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
			Assert.Throws<InvalidOperationException>(() =>
				controller.ScenarioRun.CreateActionPlan(candidate));
			ActionPlan second = controller.ScenarioRun.CreateActionPlan(
				CreateEmptyActionPlanCandidate(candidate));

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

		private static ActionCandidate CreateEmptyActionPlanCandidate(ActionCandidate source)
		{
			List<ActionSlotBinding> bindings = new List<ActionSlotBinding>(source.Bindings.Count);
			int missingParticipantCount = 0;
			for (int i = 0; i < source.Bindings.Count; i++)
			{
				ActionSlotDefinition slot = source.Bindings[i].Slot;
				bindings.Add(new ActionSlotBinding(slot, System.Array.Empty<TabletopCardId>()));
				missingParticipantCount += slot.MinimumParticipants;
			}
			return new ActionCandidate(source.Action, bindings, missingParticipantCount);
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
            Assert.That(confirmButton.interactable, Is.True);
			Assert.That(turnPanel.DisplayedTimePace, Is.EqualTo(ScenarioTimePace.Normal));
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

			confirmButton.onClick.Invoke();
			yield return null;
			Assert.That(turnPanel.DisplayedTimePace, Is.EqualTo(ScenarioTimePace.Fast));

			confirmButton.onClick.Invoke();
			yield return null;
			Assert.That(turnPanel.DisplayedTimePace, Is.EqualTo(ScenarioTimePace.Paused));
			float pausedProgress = turnPanel.DisplayedDayProgress;
			yield return null;
			yield return null;
			Assert.That(
				turnPanel.DisplayedDayProgress,
				Is.EqualTo(pausedProgress).Within(0.001f));
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
            Camera camera = GetMainCamera();
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

		[UnityTest]
		public IEnumerator FoundationTabletop_PlayerCompletesDayCycleByFeedingAndSellingThroughExistingUi()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("FoundationDayCycleMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("FoundationDayCycleKeyboard");
			yield return LoadFoundationTabletop();

			FoundationTestSceneHarness controller =
				Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
			ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();
			yield return director.EndScenarioAsync().ToCoroutine();
			yield return director.StartScenarioAsync(
				new ContentId(FoundationTestSceneHarness.TestDayCycleScenarioContentId),
				12345u).ToCoroutine();

			float readyTimeoutAt = Time.realtimeSinceStartup + 10f;
			while (!controller.IsReady || controller.ScenarioRun?.ScenarioId.Value !=
				   FoundationTestSceneHarness.TestDayCycleScenarioContentId)
			{
				Assert.Less(Time.realtimeSinceStartup, readyTimeoutAt, "日终测试剧本切换后牌桌没有重新就绪。");
				yield return null;
			}

			ScenarioRun run = controller.ScenarioRun;
			RuntimeTabletop tabletop = run.Tabletop;
			run.DiscoverContent(new ContentId(FoundationTestSceneHarness.TestSellActionContentId));
			CharacterCard character = (CharacterCard)tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestContentId),
				new Vector2(-2.5f, 1.6f));
			CharacterAttributes.SetBaseValueAndRecalculate(
				character.AbilitySystem,
				CharacterAttributes.MaxHealth,
				100f);
			CharacterAttributes.SetBaseValueAndRecalculate(
				character.AbilitySystem,
				CharacterAttributes.Health,
				20f);
			TabletopCard food = tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestFoodContentId),
				new Vector2(-2f, 1.6f));
			TabletopCard firstSellable = tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestSellableCardContentId),
				new Vector2(-1.5f, 0f));
			TabletopCard secondSellable = tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestSellableCardContentId),
				new Vector2(-0.5f, 0f));
			tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestSellableCardContentId),
				new Vector2(0.5f, 0f));
			tabletop.MergeStackOnto(secondSellable.Id, firstSellable.Id);
			TabletopCard buyer = tabletop.CreateCard(
				new ContentId(FoundationTestSceneHarness.TestBuyerCardContentId),
				new Vector2(2f, 0f));
			yield return null;
			yield return null;

			ScenarioTurnPanel turnPanel = Object.FindAnyObjectByType<ScenarioTurnPanel>();
			ScenarioScreenEffectView screenEffect = Object.FindAnyObjectByType<ScenarioScreenEffectView>();
			Assert.That(screenEffect, Is.Not.Null);
			Assert.That(screenEffect.DisplayedVignetteIntensity, Is.EqualTo(0f).Within(0.001f));
			ScenarioTabletopStats initialStats = run.GetTabletopStats();
			Assert.That(initialStats.TotalFoodNutrition, Is.EqualTo(1));
			Assert.That(initialStats.NutritionNeed, Is.EqualTo(1));
			Assert.That(initialStats.Currency, Is.Zero);
			Assert.That(initialStats.CardsOwned, Is.EqualTo(5));
			Assert.That(initialStats.CardLimit, Is.EqualTo(3));
			Assert.That(turnPanel.DisplayedTotalFoodNutrition, Is.EqualTo(1));
			Assert.That(turnPanel.DisplayedNutritionNeed, Is.EqualTo(1));
			Assert.That(turnPanel.DisplayedCurrency, Is.Zero);
			Assert.That(turnPanel.DisplayedCardsOwned, Is.EqualTo(5));
			Assert.That(turnPanel.DisplayedCardLimit, Is.EqualTo(3));
			string[] initialHudTexts = turnPanel.GetComponentsInChildren<TMP_Text>(true)
				.Select(text => text.text)
				.ToArray();
			CollectionAssert.Contains(initialHudTexts, "1/1");
			CollectionAssert.Contains(initialHudTexts, "0");
			CollectionAssert.Contains(initialHudTexts, "5/3");
			Assert.That(
				initialHudTexts.Any(text =>
					text.Contains("食物 ") ||
					text.Contains("货币 ") ||
					text.Contains("卡牌 ")),
				Is.False);

			yield return ClickConfirmTurnButton(mouse);
			Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingFeedingConfirmation));
			yield return WaitUntilScreenEffectAtLeast(
				() => screenEffect.DisplayedVignetteIntensity,
				0.3f,
				"进入日终处理阶段后没有驱动 StackCraft 对应的暗角屏幕反馈。");
			yield return ClickConfirmTurnButton(mouse);

			Assert.That(run.Tabletop.Cards.TryGetCard(character.Id, out _), Is.True);
			float healingTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (character.CurrentHealth <= 20f)
			{
				Assert.Less(Time.realtimeSinceStartup, healingTimeoutAt, "进食后 EX-GAS 没有结算生命恢复。");
				yield return null;
			}
			Assert.That(character.CurrentHealth, Is.EqualTo(70f).Within(0.001f));
			Assert.That(run.Tabletop.Cards.TryGetCard(food.Id, out _), Is.False);
			Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingExcessCardResolution));
			Assert.That(run.ExcessCardCount, Is.EqualTo(1));

			float viewTimeoutAt = Time.realtimeSinceStartup + 5f;
			TabletopCardView sellableView = null;
			TabletopCardView buyerView = null;
			while (sellableView == null || buyerView == null)
			{
				Assert.Less(Time.realtimeSinceStartup, viewTimeoutAt, "日终售卡所需的卡牌视图没有生成。");
				TabletopCardView[] views = Object.FindObjectsByType<TabletopCardView>();
				sellableView = views.SingleOrDefault(view => view.CardId == firstSellable.Id);
				buyerView = views.SingleOrDefault(view => view.CardId == buyer.Id);
				yield return null;
			}

			Camera camera = GetMainCamera();
			BoxCollider sellableCollider = sellableView.GetComponent<BoxCollider>();
			Vector3 exposedSellablePoint = sellableCollider.bounds.center +
				Vector3.down * sellableCollider.bounds.extents.y * 0.82f;
			yield return Drag(
				mouse,
				camera.WorldToScreenPoint(exposedSellablePoint),
				camera.WorldToScreenPoint(buyerView.transform.position));
			Assert.That(controller.LastActionCandidates.Count, Is.EqualTo(1));
			ActionCandidate sellCandidate = controller.LastActionCandidates.Single();
			Assert.That(
				sellCandidate.Action.ContentId.Value,
				Is.EqualTo(FoundationTestSceneHarness.TestSellActionContentId));
			CollectionAssert.AreEqual(
				new[] { firstSellable.Id, secondSellable.Id },
				sellCandidate.Bindings[0].CardIds);
			yield return SelectActionThroughPanel(
				controller,
				tabletop,
				sellCandidate,
				mouse,
				playerInput);

			Assert.That(tabletop.Cards.TryGetCard(firstSellable.Id, out _), Is.False);
			Assert.That(tabletop.Cards.TryGetCard(secondSellable.Id, out _), Is.False);
			Assert.That(
				tabletop.Cards.Stacks.SelectMany(stack => stack.Cards).Count(card =>
					card.ContentId.Value == FoundationTestSceneHarness.TestCurrencyCardContentId),
				Is.EqualTo(2));
			Assert.That(
				tabletop.Cards.Stacks.SelectMany(stack => stack.Cards).Count(card =>
					card.ContentId.Value == FoundationTestSceneHarness.TestEncounterCardContentId),
				Is.EqualTo(1));
			Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingNewDayConfirmation));
			Assert.That(run.DayEncounterResult.HasValue, Is.True);
			Assert.That(turnPanel.DisplayedTotalFoodNutrition, Is.Zero);
			Assert.That(turnPanel.DisplayedNutritionNeed, Is.EqualTo(1));
			Assert.That(turnPanel.DisplayedCurrency, Is.EqualTo(2));
			Assert.That(turnPanel.DisplayedCardsOwned, Is.EqualTo(3));
			Assert.That(turnPanel.DisplayedCardLimit, Is.EqualTo(3));
			string[] dayEndHudTexts = turnPanel.GetComponentsInChildren<TMP_Text>(true)
				.Select(text => text.text)
				.ToArray();
			CollectionAssert.Contains(dayEndHudTexts, "0/1");
			CollectionAssert.Contains(dayEndHudTexts, "2");
			CollectionAssert.Contains(dayEndHudTexts, "3/3");
			Assert.That(
				dayEndHudTexts.Any(text =>
					text.Contains("食物 ") ||
					text.Contains("货币 ") ||
					text.Contains("卡牌 ")),
				Is.False);
			Assert.That(
				turnPanel.GetComponentsInChildren<TMP_Text>(true).Any(text =>
					text.text.Contains("夜里传来了陌生脚步声") &&
					text.text.Contains("遭遇：夜间来客 x1")),
				Is.True);

			yield return ClickConfirmTurnButton(mouse);
			Assert.That(run.CurrentDay, Is.EqualTo(2));
			Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.Inactive));
			yield return WaitUntilScreenEffectAtMost(
				() => screenEffect.DisplayedVignetteIntensity,
				0.05f,
				"日终处理完成进入新一天后暗角屏幕反馈没有恢复。");
		}

        [UnityTearDown]
        public IEnumerator DestroyGameManagerAfterTest()
        {
            Time.timeScale = 1f;
			UIKit.ClearDialogQueue();
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

        private static Camera GetMainCamera()
        {
            Camera camera = GameManager.MainCamera;
            Assert.That(
                camera,
                Is.Not.Null,
                "地基测试场景必须通过正式场景组合根注册主相机，测试不能退回 Unity 标签查找。");
            return camera;
        }

        private static TabletopCardView FindView(TabletopCardId cardId)
        {
            return Object.FindObjectsByType<TabletopCardView>()
                .Single(view => view.CardId == cardId);
        }

		private static TabletopCardView FindCardHitAt(Camera camera, Vector2 screenPosition)
		{
			Assert.That(
				TryFindCardHitAt(camera, screenPosition, out TabletopCardView view),
				Is.True,
				"测试按下点必须射中至少一张牌桌卡牌。");
			return view;
		}

		private static bool TryFindCardHitAt(
			Camera camera,
			Vector2 screenPosition,
			out TabletopCardView view)
		{
			if (camera == null)
			{
				view = null;
				return false;
			}

			Ray ray = camera.ScreenPointToRay(screenPosition);
			RaycastHit[] hits = Physics.RaycastAll(
				ray,
				float.PositiveInfinity,
				Physics.DefaultRaycastLayers,
				QueryTriggerInteraction.Ignore);
			TabletopCardView bestView = null;
			float bestDistance = float.PositiveInfinity;
			int bestSortingOrder = int.MinValue;
			for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
			{
				RaycastHit hit = hits[hitIndex];
				TabletopCardView candidate = hit.collider.GetComponentInParent<TabletopCardView>();
				if (candidate == null || !candidate.CardId.IsValid)
				{
					continue;
				}
				if (bestView == null ||
					hit.distance < bestDistance - 0.0001f ||
					(Mathf.Abs(hit.distance - bestDistance) <= 0.0001f &&
					 candidate.SortingOrder > bestSortingOrder))
				{
					bestView = candidate;
					bestDistance = hit.distance;
					bestSortingOrder = candidate.SortingOrder;
				}
			}

			view = bestView;
			return view != null;
		}

		private static Vector2 FindScreenPointThatHitsCard(
			Camera camera,
			BoxCollider collider,
			TabletopCardId expectedCardId)
		{
			const int sampleSteps = 12;
			int blockedUiCandidateCount = 0;
			string firstBlockedUiDiagnostic = string.Empty;
			for (int zIndex = 0; zIndex <= sampleSteps; zIndex++)
			{
				float normalizedZ = Mathf.Lerp(0.98f, -0.98f, zIndex / (float)sampleSteps);
				for (int xIndex = 0; xIndex <= sampleSteps; xIndex++)
				{
					float normalizedX = Mathf.Lerp(-0.98f, 0.98f, xIndex / (float)sampleSteps);
					Vector3 localPoint = collider.center + new Vector3(
						collider.size.x * 0.5f * normalizedX,
						0f,
						collider.size.z * 0.5f * normalizedZ);
					Vector2 screenPoint = camera.WorldToScreenPoint(
						collider.transform.TransformPoint(localPoint));
					if (TryFindCardHitAt(camera, screenPoint, out TabletopCardView hitView) &&
						hitView.CardId == expectedCardId)
					{
						if (!UIPointerUtility.IsPositionOverUI(screenPoint))
						{
							return screenPoint;
						}
						blockedUiCandidateCount++;
						if (firstBlockedUiDiagnostic.Length == 0)
						{
							firstBlockedUiDiagnostic = DescribeUiRaycastAt(screenPoint);
						}
					}
				}
			}

			string viewDiagnostics = string.Join(
				"; ",
				Object.FindObjectsByType<TabletopCardView>()
					.Select(view =>
					{
						BoxCollider viewCollider = view.GetComponent<BoxCollider>();
						Vector2 tablePosition = TabletopCoordinateSpace.ToTablePosition(view.transform.localPosition);
						TabletopCardStack stack = view.TabletopCard.Stack;
						Vector2 stackPosition = stack?.Position ?? default;
						int stackIndex = stack?.IndexOf(view.CardId) ?? -1;
						string colliderSize = viewCollider == null
							? "(无 Collider)"
							: viewCollider.size.ToString("F3");
						return $"{view.CardId}:pos={tablePosition:F3},stack={stackPosition:F3},index={stackIndex},sort={view.SortingOrder},size={colliderSize}";
					}));
			Assert.Fail(
				$"当前相机与 Collider 下找不到可直接命中卡牌 {expectedCardId}、且不被 UI 挡住的外露点，无法验证牌桌输入。" +
				$" 已命中但被 UI 挡住的候选点数={blockedUiCandidateCount}。" +
				$" 首个阻挡UI={firstBlockedUiDiagnostic}。" +
				" 当前视图：" + viewDiagnostics);
			return default;
		}

		private static bool IsInsideViewFootprint(TabletopCardView view, Vector2 tablePosition)
		{
			BoxCollider collider = view.GetComponent<BoxCollider>();
			Assert.That(collider, Is.Not.Null, "牌桌卡牌视图必须保留 BoxCollider 作为牌面尺寸来源。");
			Vector2 center = TabletopCoordinateSpace.ToTablePosition(view.transform.localPosition);
			Vector2 halfSize = new Vector2(collider.size.x, collider.size.z) * 0.5f;
			return tablePosition.x >= center.x - halfSize.x &&
				tablePosition.x <= center.x + halfSize.x &&
				tablePosition.y >= center.y - halfSize.y &&
				tablePosition.y <= center.y + halfSize.y;
		}

		private static Vector2 ToTablePosition(TabletopCardView view)
		{
			return TabletopCoordinateSpace.ToTablePosition(view.transform.localPosition);
		}

		private static IEnumerator WaitUntilCardViewAt(
			TabletopCardView view,
			Vector2 expectedTablePosition,
			string timeoutMessage)
		{
			float timeoutAt = Time.realtimeSinceStartup + 1f;
			while (Vector2.Distance(ToTablePosition(view), expectedTablePosition) > 0.001f)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					$"{timeoutMessage} 当前={ToTablePosition(view):F3}，期望={expectedTablePosition:F3}");
				yield return null;
			}
		}

		private static IEnumerator WaitUntilPackVendorSurface(
			TabletopCardView view,
			string expectedTitle,
			string expectedPrice,
			string expectedTracker)
		{
			float timeoutAt = Time.realtimeSinceStartup + 1f;
			while (view.DisplayedTitleText != expectedTitle ||
				view.DisplayedPriceText != expectedPrice ||
				view.DisplayedNutritionText != expectedTracker)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"卡包商贩表面没有按 StackCraft 文本语义完成投影。" +
					$" 当前=({view.DisplayedTitleText}, {view.DisplayedPriceText}, {view.DisplayedNutritionText})，" +
					$"期望=({expectedTitle}, {expectedPrice}, {expectedTracker})");
				yield return null;
			}
		}

		private static IEnumerator WaitUntilCardPackInstanceSurface(
			TabletopCardView view,
			string expectedTitle,
			string expectedArtworkName)
		{
			Vector2 expectedSize = new Vector2(0.9f, 1.3000002f);
			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (view.DisplayedTitleText != expectedTitle ||
				view.DisplayedPriceText.Length != 0 ||
				view.DisplayedNutritionText.Length != 0 ||
				view.DisplayedSurfaceMaterial == null ||
				view.DisplayedSurfaceMaterial.name != "卡牌表面_卡包" ||
				view.DisplayedArtwork == null ||
				view.DisplayedArtwork.name != expectedArtworkName ||
				Vector2.Distance(view.AppliedCardSize, expectedSize) > 0.001f)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"购买生成的卡包没有按 StackCraft PackInstance 独立表面完成投影。" +
					$" 当前标题={view.DisplayedTitleText}，价格={view.DisplayedPriceText}，" +
					$"信息={view.DisplayedNutritionText}，材质={view.DisplayedSurfaceMaterial?.name ?? "(空)"}，" +
					$"卡图={view.DisplayedArtwork?.name ?? "(空)"}，尺寸={view.AppliedCardSize:F3}。");
				yield return null;
			}

			BoxCollider collider = view.GetComponent<BoxCollider>();
			Assert.That(collider, Is.Not.Null, "卡包视图必须保留 BoxCollider 作为 PackInstance 尺寸回读入口。");
			Assert.That(collider.size.x, Is.EqualTo(expectedSize.x).Within(0.001f));
			Assert.That(collider.size.z, Is.EqualTo(expectedSize.y).Within(0.001f));
			Assert.That(collider.size.y, Is.EqualTo(0f).Within(0.001f));
		}

		private static void AssertSinglePurchaseCandidate(
			FoundationTestSceneHarness controller,
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId,
			string message)
		{
			IReadOnlyList<ActionCandidate> candidates =
				controller.QueryTestActionCandidates(sourceCardId, targetCardId);
			Assert.That(
				candidates.Count,
				Is.EqualTo(1),
				message + " " + controller.BuildActionCandidateDiagnostic(sourceCardId, targetCardId));
			Assert.That(
				candidates[0].Action.ContentId,
				Is.EqualTo(new ContentId(FoundationTestSceneHarness.TestPurchaseCardPackActionContentId)),
				message);
		}

		private static string BuildLastReleaseDiagnostic(
			FoundationTestSceneHarness controller,
			string message)
		{
			TabletopCardPointerReleaseIntent intent = controller.LastReleaseIntent;
			return message +
				$" 释放次数={controller.ReleaseIntentCount}，" +
				$"来源={intent.CardId}，拖拽={intent.IsDrag}，目标={intent.TargetCardId}，" +
				$"按下={intent.PressPointerPosition}，释放={intent.ReleasePointerPosition}，" +
				$"请求堆位置={intent.RequestedStackPosition}。";
		}

		private static string BuildBattleLeaveDiagnostic(
			FoundationTestSceneHarness controller,
			Rect battleArea,
			Rect placementBounds,
			Vector2 intendedReleasePosition,
			Vector2 pressTablePosition,
			Vector2 battlePose,
			string message)
		{
			TabletopCardPointerReleaseIntent intent = controller.LastReleaseIntent;
			Vector2 expectedRequestedPosition = intendedReleasePosition + (battlePose - pressTablePosition);
			string placementPreview;
			try
			{
				placementPreview = intent.CardId.IsValid
					? controller.Cards.CanPlaceStack(
						intent.CardId,
						intent.RequestedStackPosition,
						controller.PlacementRules).ToString()
					: "无有效卡牌";
			}
			catch (System.Exception exception)
			{
				placementPreview = exception.GetType().Name + ": " + exception.Message;
			}

			return message +
				$" 释放次数={controller.ReleaseIntentCount}，" +
				$"来源={intent.CardId}，拖拽={intent.IsDrag}，目标={intent.TargetCardId}，" +
				$"按下={intent.PressPointerPosition}，释放={intent.ReleasePointerPosition}，" +
				$"请求堆位置={intent.RequestedStackPosition}，" +
				$"期望释放={intendedReleasePosition}，期望请求堆位置={expectedRequestedPosition}，" +
				$"战斗区={battleArea}，释放在战斗区内={battleArea.Contains(intent.ReleasePointerPosition)}，" +
				$"牌桌边界={placementBounds}，请求在边界内={placementBounds.Contains(intent.RequestedStackPosition)}，" +
				$"放置预演={placementPreview}。";
		}

		private static Vector2 FindBlankReleasePointThatOverlapsStack(
			Camera camera,
			TabletopCardView coordinateAnchor,
			Vector2 targetStackPosition,
			Vector2 pointerToStackOffset,
			TabletopCardStackGeometry geometry,
			int draggedCardCount)
		{
			Rect targetFootprint = geometry.CalculateFootprint(targetStackPosition, cardCount: 1);
			const int sampleSteps = 24;
			for (int xIndex = 0; xIndex <= sampleSteps; xIndex++)
			{
				float xOffset = Mathf.Lerp(
					-geometry.FootprintSize.x,
					geometry.FootprintSize.x,
					xIndex / (float)sampleSteps);
				for (int yIndex = 0; yIndex <= sampleSteps; yIndex++)
				{
					float yOffset = Mathf.Lerp(
						-geometry.FootprintSize.y,
						geometry.FootprintSize.y,
						yIndex / (float)sampleSteps);
					Vector2 releaseTablePosition = targetStackPosition + new Vector2(xOffset, yOffset);
					Vector2 releaseScreenPosition = TableToScreenPoint(
						camera,
						coordinateAnchor,
						releaseTablePosition);
					if (TryFindCardHitAt(camera, releaseScreenPosition, out _))
					{
						continue;
					}

					Vector2 requestedStackPosition = releaseTablePosition + pointerToStackOffset;
					Rect draggedFootprint = geometry.CalculateFootprint(requestedStackPosition, draggedCardCount);
					if (FootprintsOverlap(draggedFootprint, targetFootprint))
					{
						return releaseTablePosition;
					}
				}
			}

			Assert.Fail("找不到“指针在空白桌面、拖拽整堆足迹会重叠目标堆”的释放点，无法验证空白释放后的整堆分离。");
			return default;
		}

		private static bool FootprintsOverlap(Rect left, Rect right)
		{
			float overlapX = (left.width + right.width) * 0.5f -
				Mathf.Abs(left.center.x - right.center.x);
			float overlapY = (left.height + right.height) * 0.5f -
				Mathf.Abs(left.center.y - right.center.y);
			return overlapX > 0.001f && overlapY > 0.001f;
		}

		private static bool TryProjectScreenToTable(
			Camera camera,
			Transform tableTransform,
			Vector2 screenPosition,
			out Vector2 tablePosition)
		{
			Ray ray = camera.ScreenPointToRay(screenPosition);
			if (!TabletopCoordinateSpace.CreateTablePlane(tableTransform).Raycast(ray, out float distance))
			{
				tablePosition = default;
				return false;
			}

			tablePosition = TabletopCoordinateSpace.ToTablePosition(
				tableTransform.InverseTransformPoint(ray.GetPoint(distance)));
			return true;
		}

		private static Vector2 ScreenToTablePosition(
			Camera camera,
			TabletopCardView view,
			Vector2 screenPosition)
		{
			Assert.That(
				TryProjectScreenToTable(camera, view.transform.parent, screenPosition, out Vector2 tablePosition),
				Is.True,
				"测试屏幕点必须能投影到牌桌平面。");
			return tablePosition;
		}

		private static Vector2 TableToScreenPoint(
			Camera camera,
			TabletopCardView view,
			Vector2 tablePosition)
		{
			Vector3 localPosition = TabletopCoordinateSpace.ToLocalPosition(tablePosition);
			Vector3 worldPosition = view.transform.parent.TransformPoint(localPosition);
			return camera.WorldToScreenPoint(worldPosition);
		}

        private static bool AreAllViewsReady()
        {
            TabletopCardView[] views = Object.FindObjectsByType<TabletopCardView>();
            return views.Length >= 4 &&
                views.All(view => view.DisplaysArtwork);
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

		private static IEnumerator WaitUntilScreenEffectAtLeast(
			System.Func<float> readValue,
			float expectedMinimum,
			string timeoutMessage)
		{
			float timeoutAt = Time.realtimeSinceStartup + 2f;
			while (readValue() < expectedMinimum)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, timeoutMessage);
				yield return null;
			}
		}

		private static IEnumerator WaitUntilScreenEffectAtMost(
			System.Func<float> readValue,
			float expectedMaximum,
			string timeoutMessage)
		{
			float timeoutAt = Time.realtimeSinceStartup + 2f;
			while (readValue() > expectedMaximum)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, timeoutMessage);
				yield return null;
			}
		}

		private static IEnumerator WaitUntilPlayingCardSmokeEffectAtLeast(
			int expectedMinimum,
			string timeoutMessage)
		{
			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (CountPlayingCardSmokeEffects() < expectedMinimum)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, timeoutMessage);
				yield return null;
			}
		}

		private static IEnumerator WaitUntilCardSmokeEffectsReleased(string timeoutMessage)
		{
			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (CountPlayingCardSmokeEffects() > 0)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, timeoutMessage);
				yield return null;
			}
		}

		private static int CountPlayingCardSmokeEffects()
		{
			return Object.FindObjectsByType<TabletopCardSmokeEffectView>(
					FindObjectsInactive.Exclude,
					FindObjectsSortMode.None)
				.Count(view => view.IsPlaying);
		}

		private static string DescribeUiRaycastAt(Vector2 screenPosition)
		{
			EventSystem eventSystem = GameManager.EventSystem;
			if (eventSystem == null)
			{
				return "(无正式 EventSystem)";
			}

			PointerEventData eventData = new PointerEventData(eventSystem)
			{
				position = screenPosition
			};
			List<RaycastResult> results = new List<RaycastResult>();
			eventSystem.RaycastAll(eventData, results);
			if (results.Count == 0)
			{
				return "(无 UI 射线命中)";
			}

			return string.Join(
				"; ",
				results.Select(result =>
				{
					GameObject hitObject = result.gameObject;
					string path = hitObject == null ? "(空对象)" : BuildTransformPath(hitObject.transform);
					Graphic graphic = hitObject == null ? null : hitObject.GetComponent<Graphic>();
					CanvasGroup[] groups = hitObject == null
						? System.Array.Empty<CanvasGroup>()
						: hitObject.GetComponentsInParent<CanvasGroup>(includeInactive: false);
					string groupText = groups.Length == 0
						? "无CanvasGroup"
						: string.Join(
							">",
							groups.Select(group =>
								$"{group.name}:blocks={group.blocksRaycasts},interactable={group.interactable}"));
					return $"{path},raycastTarget={graphic?.raycastTarget.ToString() ?? "(非Graphic)"},{groupText}";
				}));
		}

		private static string BuildTransformPath(Transform transform)
		{
			if (transform == null)
			{
				return "(空 Transform)";
			}

			List<string> names = new List<string>();
			while (transform != null)
			{
				names.Add(transform.name);
				transform = transform.parent;
			}
			names.Reverse();
			return string.Join("/", names);
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

		private IEnumerator ClickCard(Mouse mouse, TabletopCardId cardId)
		{
			TabletopCardView view = null;
			float viewTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (view == null)
			{
				view = Object.FindObjectsByType<TabletopCardView>()
					.SingleOrDefault(candidate => candidate.CardId == cardId);
				Assert.Less(Time.realtimeSinceStartup, viewTimeoutAt, "卡包已创建，但牌桌视图没有完成投影。");
				yield return null;
			}
			Physics.SyncTransforms();
			Camera camera = GetMainCamera();
			Vector2 screenPosition = FindScreenPointThatHitsCard(
				camera,
				view.GetComponent<BoxCollider>(),
				cardId);
			Assert.That(
				UIPointerUtility.IsPositionOverUI(screenPosition),
				Is.False,
				"测试点击点被正式 UI 射线挡住，牌桌输入不会选择卡牌。" +
				" UI命中=" + DescribeUiRaycastAt(screenPosition));
			Assert.That(
				GameManager.InputSystem.IsGameplayActionBlocked(EGameplayInputAction.Click),
				Is.False,
				"测试点击卡牌时 Gameplay 点击输入仍被动作图释放门禁阻挡。");
			Move(mouse.position, screenPosition);
			yield return null;
			Press(mouse.leftButton);
			yield return null;
			Release(mouse.leftButton);
			yield return null;
			yield return null;
			TabletopView tabletopView = Object.FindAnyObjectByType<TabletopView>();
			Assert.That(tabletopView, Is.Not.Null);
			float selectionTimeoutAt = Time.realtimeSinceStartup + 1f;
			while (tabletopView.ReadableCardId != cardId)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					selectionTimeoutAt,
					$"点击卡牌 {cardId} 后没有成为详情面板可读对象。" +
					$" 当前悬浮={tabletopView.HoveredCardId}，选中={tabletopView.SelectedCardId}，可读={tabletopView.ReadableCardId}。");
				yield return null;
			}
		}

		private static IEnumerator CaptureVisualEvidence(string fileName)
		{
			string screenshotDirectory = GetFoundationE2EScreenshotDirectory();
			Directory.CreateDirectory(screenshotDirectory);
			string screenshotPath = Path.Combine(screenshotDirectory, fileName);
			if (File.Exists(screenshotPath))
			{
				File.Delete(screenshotPath);
			}

			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(screenshotPath);
			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					$"端到端截图没有写入：{screenshotPath}");
				yield return null;
			}
			yield return null;
		}

		private static string GetFoundationE2EScreenshotDirectory()
		{
			return Path.GetFullPath(
				Path.Combine(Application.dataPath, "..", "Assets", "Screenshots", "FoundationE2E"));
		}

		private static void CreateVisualEvidenceContactSheet(
			IReadOnlyList<string> fileNames,
			string outputFileName)
		{
			string screenshotDirectory = GetFoundationE2EScreenshotDirectory();
			var textures = new List<Texture2D>(fileNames.Count);
			try
			{
				foreach (string fileName in fileNames)
				{
					string path = Path.Combine(screenshotDirectory, fileName);
					Assert.That(File.Exists(path), Is.True, $"端到端过程图缺失，无法生成拼图：{path}");

					var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
					Assert.That(
						ImageConversion.LoadImage(texture, File.ReadAllBytes(path)),
						Is.True,
						$"端到端过程图无法解码，无法生成拼图：{path}");
					textures.Add(texture);
				}

				int columns = Mathf.Min(3, textures.Count);
				int rows = Mathf.CeilToInt(textures.Count / (float)columns);
				int cellWidth = textures.Max(texture => texture.width);
				int cellHeight = textures.Max(texture => texture.height);
				var contactSheet = new Texture2D(
					cellWidth * columns,
					cellHeight * rows,
					TextureFormat.RGBA32,
					false);
				try
				{
					var background = new Color32[contactSheet.width * contactSheet.height];
					var backgroundColor = new Color32(18, 18, 18, 255);
					for (int i = 0; i < background.Length; i++)
					{
						background[i] = backgroundColor;
					}
					contactSheet.SetPixels32(background);

					for (int i = 0; i < textures.Count; i++)
					{
						Texture2D source = textures[i];
						int column = i % columns;
						int rowFromTop = i / columns;
						int x = column * cellWidth + (cellWidth - source.width) / 2;
						int y = (rows - 1 - rowFromTop) * cellHeight + (cellHeight - source.height) / 2;
						contactSheet.SetPixels32(x, y, source.width, source.height, source.GetPixels32());
					}
					contactSheet.Apply(false, false);

					string outputPath = Path.Combine(screenshotDirectory, outputFileName);
					if (File.Exists(outputPath))
					{
						File.Delete(outputPath);
					}
					File.WriteAllBytes(outputPath, ImageConversion.EncodeToPNG(contactSheet));
					Assert.That(File.Exists(outputPath), Is.True, $"端到端过程拼图没有写入：{outputPath}");
					Assert.That(new FileInfo(outputPath).Length, Is.GreaterThan(0), $"端到端过程拼图为空：{outputPath}");
				}
				finally
				{
					UnityEngine.Object.Destroy(contactSheet);
				}
			}
			finally
			{
				foreach (Texture2D texture in textures)
				{
					UnityEngine.Object.Destroy(texture);
				}
			}
		}

		private IEnumerator DragCardOntoCard(
			Mouse mouse,
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId)
		{
			yield return HoldDraggedCardOverCard(mouse, sourceCardId, targetCardId);
			Release(mouse.leftButton);
			yield return null;
			yield return null;
		}

		private IEnumerator HoldDraggedCardOverCard(
			Mouse mouse,
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId)
		{
			TabletopCardView source = null;
			TabletopCardView target = null;
			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (source == null || target == null)
			{
				TabletopCardView[] views = Object.FindObjectsByType<TabletopCardView>();
				source = views.SingleOrDefault(candidate => candidate.CardId == sourceCardId);
				target = views.SingleOrDefault(candidate => candidate.CardId == targetCardId);
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, "商贩或付款卡已经创建，但牌桌视图没有完成投影。");
				yield return null;
			}
			Physics.SyncTransforms();
			Camera camera = GetMainCamera();
			Vector2 start = FindScreenPointThatHitsCard(
				camera,
				source.GetComponent<BoxCollider>(),
				sourceCardId);
			Assert.That(
				UIPointerUtility.IsPositionOverUI(start),
				Is.False,
				"测试拖拽按下点被正式 UI 射线挡住，牌桌输入不会开始拖拽。" +
				" UI命中=" + DescribeUiRaycastAt(start));
			Assert.That(FindCardHitAt(camera, start).CardId, Is.EqualTo(sourceCardId));
			Assert.That(
				GameManager.InputSystem.IsGameplayInputLocked,
				Is.False,
				"测试拖拽开始前 Gameplay 输入仍被剧本表现或其它正式流程锁定。");
			Assert.That(
				GameManager.InputSystem.IsGameplayActionBlocked(EGameplayInputAction.Click),
				Is.False,
				"测试拖拽开始前 Gameplay 点击输入仍被动作图释放门禁阻挡。");
			Move(mouse.position, start);
			yield return null;
			Assert.That(
				Vector2.Distance(
					GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay),
					start),
				Is.LessThan(0.5f),
				"正式 Gameplay 指针输入没有读到测试拖拽按下点。");
			TabletopCardDragInput dragInput = Object.FindAnyObjectByType<TabletopCardDragInput>();
			Assert.That(dragInput, Is.Not.Null, "统一测试场景缺少正式牌桌拖拽输入组件。");
			Assert.That(dragInput.isActiveAndEnabled, Is.True, "正式牌桌拖拽输入组件没有处于启用状态。");
			Press(mouse.leftButton);
			float sessionTimeoutAt = Time.realtimeSinceStartup + 1f;
			while (!dragInput.IsPointerSessionActive)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					sessionTimeoutAt,
					BuildDragStartDiagnostic(mouse, camera, start, sourceCardId));
				yield return null;
			}
			Vector2 end = FindDropScreenPointThatHitsCard(
				camera,
				target,
				targetCardId);
			Assert.That(FindCardHitAt(camera, end).CardId, Is.EqualTo(targetCardId));
			Move(mouse.position, end);
			yield return null;
			yield return null;
			Assert.That(
				dragInput.IsDragging,
				Is.True,
				$"拖到目标卡牌 {targetCardId} 后仍未进入拖拽状态。");
		}

		private static string BuildDragStartDiagnostic(
			Mouse mouse,
			Camera camera,
			Vector2 start,
			TabletopCardId sourceCardId)
		{
			PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
			InputAction clickAction = playerInput?.actions
				?.FindActionMap(EActionMap.Gameplay.ToString(), throwIfNotFound: false)
				?.FindAction("Click", throwIfNotFound: false);
			string currentActionMap = playerInput?.currentActionMap?.name ?? "(无当前动作图)";
			string clickEnabled = clickAction?.enabled.ToString() ?? "(缺少 Click Action)";
			string clickPressed = clickAction?.IsPressed().ToString() ?? "(缺少 Click Action)";
			string mousePressed = mouse?.leftButton.isPressed.ToString() ?? "(缺少测试鼠标)";
			string gameCoreClickPressed = GameManager.InputSystem
				.IsGameplayActionPressed(EGameplayInputAction.Click)
				.ToString();
			string gameplayLocked = GameManager.InputSystem.IsGameplayInputLocked.ToString();
			string gameplayClickBlocked = GameManager.InputSystem
				.IsGameplayActionBlocked(EGameplayInputAction.Click)
				.ToString();
			string uiDiagnostic = UIPointerUtility.IsPositionOverUI(start)
				? DescribeUiRaycastAt(start)
				: "(未被 UI 阻挡)";
			string physicsHit = TryFindCardHitAt(camera, start, out TabletopCardView hitView)
				? hitView.CardId.ToString()
				: "(未命中卡牌)";
			Vector2 pointerPosition = GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);

			return $"按下卡牌 {sourceCardId} 后没有建立牌桌拖拽会话。" +
				$" 当前动作图={currentActionMap}，Click启用={clickEnabled}，Click按下={clickPressed}，" +
				$"测试鼠标左键按下={mousePressed}，正式点击按下={gameCoreClickPressed}，" +
				$"Gameplay锁定={gameplayLocked}，点击门禁={gameplayClickBlocked}，" +
				$"指针={pointerPosition}，目标按下点={start}，物理命中卡牌={physicsHit}，UI命中={uiDiagnostic}";
		}

		private static Vector2 FindDropScreenPointThatHitsCard(
			Camera camera,
			TabletopCardView target,
			TabletopCardId expectedCardId)
		{
			Vector2 centerScreenPoint = camera.WorldToScreenPoint(target.transform.position);
			if (TryFindCardHitAt(camera, centerScreenPoint, out TabletopCardView hitView) &&
				hitView.CardId == expectedCardId &&
				!UIPointerUtility.IsPositionOverUI(centerScreenPoint))
			{
				return centerScreenPoint;
			}

			return FindScreenPointThatHitsCard(
				camera,
				target.GetComponent<BoxCollider>(),
				expectedCardId);
		}

		private static int CountCards(FoundationTestSceneHarness controller, string contentId)
		{
			ContentId expected = new ContentId(contentId);
			return controller.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.Count(card => card.ContentId == expected);
		}

		private static IEnumerator WaitUntilViewsDisplayArtwork(
			string contentId,
			string timeoutMessage)
		{
			ContentId expected = new ContentId(contentId);
			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (true)
			{
				TabletopCardView[] matchingViews = Object
					.FindObjectsByType<TabletopCardView>(FindObjectsInactive.Exclude)
					.Where(view => view.ContentId == expected)
					.ToArray();
				if (matchingViews.Length > 0 &&
					matchingViews.All(view =>
						view.DisplaysArtwork &&
						view.DisplayedArtwork.name != "卡牌占位图"))
				{
					yield break;
				}

				Assert.Less(Time.realtimeSinceStartup, timeoutAt, timeoutMessage);
				yield return null;
			}
		}

		private static TabletopCardId[] GetCardIds(FoundationTestSceneHarness controller, string contentId)
		{
			ContentId expected = new ContentId(contentId);
			return controller.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.Where(card => card.ContentId == expected)
				.Select(card => card.Id)
				.ToArray();
		}

		private static TabletopHitResultView FindHitResult(
			System.Func<TabletopHitResultView, bool> predicate)
		{
			TabletopHitResultView[] matches = Object
				.FindObjectsByType<TabletopHitResultView>(
					FindObjectsInactive.Exclude,
					FindObjectsSortMode.None)
				.Where(predicate)
				.ToArray();
			Assert.That(matches.Length, Is.EqualTo(1), "命中结果必须由独立浮动 HitResult 视图承载。");
			return matches[0];
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
			bool completedImmediately = false;
			void OnActionCompleted(ActionCompletedEvent completedEvent)
			{
				if (completedEvent.ActionId == candidate.Action.ContentId)
				{
					completedImmediately = true;
				}
			}
			EventKit.Type.Register<ActionCompletedEvent>(OnActionCompleted);

			try
			{
				Move(mouse.position, buttonScreenPosition);
				yield return null;
				Press(mouse.leftButton);
				yield return null;
				Release(mouse.leftButton);

				float actionTimeoutAt = Time.realtimeSinceStartup + 5f;
				while (tabletop.ActiveActions.Count == 0 && !completedImmediately)
				{
					Assert.Less(
						Time.realtimeSinceStartup,
						actionTimeoutAt,
						$"点击行动候选 {candidate.Action.ContentId} 后既没有活动行动，也没有即时完成事实。");
					yield return null;
				}
			}
			finally
			{
				EventKit.Type.UnRegister<ActionCompletedEvent>(OnActionCompleted);
			}

            yield return null;
			float closeTimeoutAt = Time.realtimeSinceStartup + 5f;
			while (panel != null && panel.gameObject.activeInHierarchy)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					closeTimeoutAt,
					"行动已经提交，但行动选择面板没有退出 UIKit 面板栈。");
				yield return null;
			}
            Assert.That(playerInput.currentActionMap.name, Is.EqualTo(EActionMap.Gameplay.ToString()));
            Assert.That(GameManager.GameStateSystem.currentState, Is.EqualTo(EGameState.Gameplay));
            Assert.That(inputModule.actionsAsset, Is.SameAs(playerInput.actions));
            Assert.That(playerInput.uiInputModule, Is.SameAs(inputModule));
            Assert.That(inputModule.enabled, Is.True,
                "返回玩法状态后，常驻回合 HUD 必须继续接收同一份 UI 鼠标输入。");
        }

	}
}
