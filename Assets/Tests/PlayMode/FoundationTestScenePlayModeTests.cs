using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using InputSystemApi = UnityEngine.InputSystem.InputSystem;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证地基场景通过真实 YooAsset、正式输入 owner 和物理命中跑通牌桌拖拽表现链路。
    /// </summary>
    public sealed class FoundationTestScenePlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator FoundationTabletop_InstantiatesViewsAndLoadsArtworkThroughYooAsset()
        {
            yield return LoadFoundationTabletop();

            FoundationTestSceneHarness controller =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            TabletopCardView[] views = Object.FindObjectsByType<TabletopCardView>();

            Assert.That(controller.CardState.CardCount, Is.EqualTo(4));
            Assert.That(controller.CardState.StackCount, Is.EqualTo(2));
            Assert.That(views, Has.Length.EqualTo(4));
            Assert.That(views.All(view => view.GetComponent<BoxCollider>() != null), Is.True);
            Assert.That(
                views.All(view => view.GetComponent<SpriteRenderer>()?.sprite?.name == "Square"),
                Is.True,
                "卡面必须由内容作者源地址经过 ResourceSystem/YooAsset 写入视图。");
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
                controller.DiscoveryState.IsDiscovered(
                    new ContentId(FoundationTestSceneHarness.TestActionContentId)),
                Is.True,
                "统一测试场景里的测试行动必须先进入局内发现状态，再参与候选解析。");
            TabletopCardActionCandidate candidate = controller.LastActionCandidates[0];
            Assert.That(candidate.Action.ContentId.Value, Is.EqualTo("test.foundation.action"));
            Assert.That(candidate.IsReady, Is.True);
            Assert.That(candidate.MissingParticipantCount, Is.Zero);
            Assert.That(candidate.Bindings.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { controller.MiddleCardId, controller.TargetCardId },
                candidate.Bindings[0].CardIds);
            Assert.That(controller.CardState.StackCount, Is.EqualTo(2), "释放意图不能提前合并正式堆栈。");
            Assert.That(
                controller.CardState.GetStackContaining(controller.MiddleCardId),
                Is.SameAs(controller.CardState.GetStackContaining(controller.BottomCardId)));
            Assert.That(
                controller.CardState.GetStackContaining(controller.MiddleCardId),
                Is.Not.SameAs(controller.CardState.GetStackContaining(controller.TargetCardId)));
            Assert.That(Vector3.Distance(sourceView.transform.position, sourceAuthoritativePosition), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(topView.transform.position, topAuthoritativePosition), Is.LessThan(0.0001f));
            Assert.That(targetView.IsHighlighted, Is.False);

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            ScenarioTurnSystem scenarioTurnSystem = GameManager.GetSystem<ScenarioTurnSystem>();
            TabletopCardActionSystem actionSystem = GameManager.GetSystem<TabletopCardActionSystem>();
            TabletopCardActionJob job = controller.StartSelectedAction(candidate.Action.ContentId);
            Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Running));
            Assert.That(job.ActionId, Is.EqualTo(candidate.Action.ContentId));
            Assert.That(job.Bindings, Is.Not.SameAs(candidate.Bindings));
            Assert.That(job.Bindings.Count, Is.EqualTo(candidate.Bindings.Count));
            Assert.That(job.Bindings[0].Slot, Is.SameAs(candidate.Action.ParticipationSlots[0]));
            CollectionAssert.AreEqual(candidate.Bindings[0].CardIds, job.Bindings[0].CardIds);
            Assert.That(actionSystem.ActiveJobs, Has.Count.EqualTo(1));
            Assert.That(
                actionSystem.ProgressionMode,
                Is.EqualTo(TabletopCardActionProgressionMode.TurnBased));

            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(job.ProgressedTurns, Is.Zero, "默认回合制不能因为现实时间流逝而推进普通行动。");

            Assert.That(scenarioTurnSystem.ConfirmedTurnIndex, Is.Zero);
            Assert.That(scenarioDirector.ConfirmTurn(), Is.EqualTo(1));
            Assert.That(job.ProgressedTurns, Is.EqualTo(1f));
            Assert.That(job.Progress, Is.EqualTo(0.5f));

            ContentRegistrySystem contentSystem = GameManager.GetSystem<ContentRegistrySystem>();
            Assert.That(
                contentSystem.Index.TryGet(
                    new ContentId(FoundationTestSceneHarness.TestTurnTimingContentId),
                    out TurnTimingDefinition turnTiming),
                Is.True);
            actionSystem.UseRealTimeProgression(turnTiming);
            Assert.That(
                actionSystem.ProgressionMode,
                Is.EqualTo(TabletopCardActionProgressionMode.RealTime));

            Time.timeScale = 0f;
            float globallyPausedTurns = job.ProgressedTurns;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(job.ProgressedTurns, Is.EqualTo(globallyPausedTurns).Within(0.0001f));
            Time.timeScale = 1f;

            actionSystem.PauseAction(job);
            float pausedTurns = job.ProgressedTurns;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Paused));
            Assert.That(job.ProgressedTurns, Is.EqualTo(pausedTurns).Within(0.0001f));

            Time.timeScale = 2f;
            actionSystem.ResumeAction(job);
            float completionTimeoutAt = Time.realtimeSinceStartup + 2f;
            while (job.State != TabletopCardActionJobState.Completed)
            {
                Assert.Less(Time.realtimeSinceStartup, completionTimeoutAt, "恢复后的实时行动作业没有完成。");
                yield return null;
            }
            Time.timeScale = 1f;

            Assert.That(job.Progress, Is.EqualTo(1f));
            Assert.That(job.ProgressedTurns, Is.EqualTo(job.TurnCost).Within(0.0001f));
            Assert.That(actionSystem.ActiveJobs, Is.Empty);
            yield return null;

            int expectedProductCount = job.ResultBranchKey switch
            {
                "one-product" => 1,
                "two-products" => 2,
                _ => throw new AssertionException($"测试行动返回了未知随机结果分支：{job.ResultBranchKey}")
            };
            Assert.That(controller.CardState.CardCount, Is.EqualTo(2 + expectedProductCount));
            Assert.That(controller.CardState.StackCount, Is.EqualTo(1 + expectedProductCount));
            Assert.That(controller.CardState.TryGetCard(controller.MiddleCardId, out _), Is.False);
            Assert.That(controller.CardState.TryGetCard(controller.TargetCardId, out _), Is.False);

            TabletopCardStack[] productStacks = controller.CardState.Stacks
                .Where(stack => stack.Cards.Count == 1 &&
                                stack.Cards[0].ContentId.Value ==
                                FoundationTestSceneHarness.TestProductContentId)
                .ToArray();
            Assert.That(productStacks, Has.Length.EqualTo(expectedProductCount));
            Vector2 sourceStackPosition = controller.CardState
                .GetStackContaining(controller.BottomCardId)
                .Position;
            Assert.That(productStacks.All(stack => stack.Position == sourceStackPosition), Is.True);
            Assert.That(
                Object.FindObjectsByType<TabletopCardView>(),
                Has.Length.EqualTo(2 + expectedProductCount));
            QuestSystem questSystem = GameManager.GetSystem<QuestSystem>();
            Assert.That(
                questSystem.GetStatus(
                    new ContentId(FoundationTestSceneHarness.TestQuestContentId)),
                Is.EqualTo(QuestStatus.Completed),
                "成功结算测试行动后，活动任务子项必须消费同一个行动完成事实并完成。");
        }

        [UnityTest]
        public IEnumerator FoundationTabletop_RemovedParticipantCancelsBeforeProgressAndResult()
        {
            yield return LoadFoundationTabletop();

            FoundationTestSceneHarness controller =
                Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            IReadOnlyList<TabletopCardActionCandidate> candidates =
                controller.QueryTestActionCandidates(controller.MiddleCardId, controller.TargetCardId);
            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates[0].IsReady, Is.True);

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            TabletopCardActionSystem actionSystem = GameManager.GetSystem<TabletopCardActionSystem>();
            TabletopCardActionJob job = controller.StartSelectedAction(candidates[0].Action.ContentId);
            controller.CardState.RemoveCard(controller.TargetCardId);

            Assert.That(scenarioDirector.ConfirmTurn(), Is.EqualTo(1));

            Assert.That(job.State, Is.EqualTo(TabletopCardActionJobState.Cancelled));
            Assert.That(
                job.CancellationReason,
                Is.EqualTo(TabletopCardActionCancellationReason.ParticipantInvalidated));
            Assert.That(job.ProgressedTurns, Is.Zero);
            Assert.That(actionSystem.ActiveJobs, Is.Empty);
            Assert.That(controller.CardState.CardCount, Is.EqualTo(3));
            Assert.That(controller.CardState.TryGetCard(controller.MiddleCardId, out _), Is.True);
            Assert.That(
                controller.CardState.Stacks.Any(stack =>
                    stack.Cards.Any(card =>
                        card.ContentId.Value == FoundationTestSceneHarness.TestProductContentId)),
                Is.False);
            QuestSystem questSystem = GameManager.GetSystem<QuestSystem>();
            Assert.That(
                questSystem.GetStatus(
                    new ContentId(FoundationTestSceneHarness.TestQuestContentId)),
                Is.EqualTo(QuestStatus.Active),
                "参与者失效取消的行动不能发布成功完成事实或推进任务子项。");

            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<TabletopCardView>(), Has.Length.EqualTo(3));
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
            yield return SceneManager.LoadSceneAsync("FoundationTest", LoadSceneMode.Single);

            float timeoutAt = Time.realtimeSinceStartup + 20f;
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "GameManager 启动超时。");
                yield return null;
            }

            Assert.That(GameManager.StartupState, Is.EqualTo(GameManagerStartupState.Ready),
                GameManager.StartupException?.ToString());
            Assert.That(
                GameManager.HasSystem<QuestSystem>(),
                Is.True,
                "统一地基场景必须装配正式任务系统，后续任务条件不能另建场景级入口。");
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
            QuestSystem questSystem = GameManager.GetSystem<QuestSystem>();
            ScenarioTurnSystem scenarioTurnSystem = GameManager.GetSystem<ScenarioTurnSystem>();
            Assert.That(scenarioDirector.HasActiveScenario, Is.True);
            Assert.That(
                scenarioDirector.ActiveScenarioId.Value,
                Is.EqualTo(FoundationTestSceneHarness.TestScenarioContentId));
            Assert.That(
                questSystem.GetStatus(
                    new ContentId(FoundationTestSceneHarness.TestQuestContentId)),
                Is.EqualTo(QuestStatus.Active));
            Assert.That(scenarioTurnSystem.ConfirmedTurnIndex, Is.Zero);
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

    }
}
