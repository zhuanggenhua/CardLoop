#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 控制组 / RTS 订单链 / GAS formal 恢复的最小正式 PlayMode smoke。
    /// 只走现有 PlayerSystem、PlayerOrderRequest 和 CharacterBase 存读档闭包，不创建并行控制器。
    /// </summary>
    public static class CompositeRuntimeSmokeValidator
    {
        private const string ResultRelativePath = "Temp/UnityBridge/results/composite-runtime-smoke.json";
        private const string SmokeTransformationId = "composite-runtime-smoke-transformation";
        private const int SmokeBaselineAbilityCode = XAbility.ABILITY_Attack;
        private const int SmokeReplacementAbilityCode = XAbility.ABILITY_TransformReplaceSmoke;
        private const string SmokeContainerName = "Composite runtime smoke container";
        private const string SmokeItemName = "Composite runtime smoke item";
        private const int MinimumObservationFrames = 1;
        private const int MaximumObservationFrames = 10;
        private const float TargetTolerance = 0.35f;
        private const float SeparationTolerance = 0.2f;
        private static ValidationResult? s_result;
        private static bool s_running;
        private static int s_dispatchFrame;
        private static CharacterActor? s_originalPlayer;
        private static CharacterActor? s_primaryActor;
        private static CharacterActor? s_companion;
        private static Persistable? s_container;
        private static Item? s_probeItem;
        private static int s_baselineAbilityCode;
        private static int s_replacementAbilityCode;
        private static Vector2 s_expectedPlayerTarget;
        private static Vector2 s_expectedCompanionTarget;
        private static bool s_originalPlayerWasControllable;
        private static bool s_baselineAbilityAddedBySmoke;
        private static bool s_transformationApplied;

        public static string ResultPath => Path.GetFullPath(ResultRelativePath);

        public static StartResult Start()
        {
            if (!Application.isPlaying)
            {
                WriteResult(Fail("Composite runtime smoke 只能在 PlayMode 下启动。"));
                return new StartResult { ResultPath = ResultPath };
            }

            s_result = new ValidationResult
            {
                ScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                ScreenSize = $"{Screen.width}x{Screen.height}",
            };

            s_running = true;
            s_dispatchFrame = 0;
            s_originalPlayer = null;
            s_primaryActor = null;
            s_companion = null;
            s_container = null;
            s_probeItem = null;
            s_baselineAbilityCode = 0;
            s_replacementAbilityCode = 0;
            s_expectedPlayerTarget = Vector2.zero;
            s_expectedCompanionTarget = Vector2.zero;
            s_originalPlayerWasControllable = false;
            s_baselineAbilityAddedBySmoke = false;
            s_transformationApplied = false;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return new StartResult { ResultPath = ResultPath };
        }

        private static void Tick()
        {
            if (!s_running || s_result == null)
            {
                StopTicking();
                return;
            }

            if (!Application.isPlaying)
            {
                WriteAndStop(Fail("验证过程中 PlayMode 已退出。"));
                return;
            }

            try
            {
                if (!s_result.OrderDispatched)
                {
                    SetupControlGroupAndDispatchOrder(s_result);
                    s_dispatchFrame = Time.frameCount;
                    return;
                }

                int elapsedFrames = Time.frameCount - s_dispatchFrame;
                if (elapsedFrames < MinimumObservationFrames)
                {
                    return;
                }

                if (ShouldContinueObserving(elapsedFrames))
                {
                    return;
                }

                CaptureMovementEvidence(s_result);
                CaptureInventoryAndAbilityEvidence(s_result);
                CaptureControlGroupCollapseEvidence(s_result);
                FinalizeResult(s_result);
                WriteAndStop(s_result);
            }
            catch (Exception exception)
            {
                ValidationResult failedResult = s_result ?? new ValidationResult();
                failedResult.Completed = true;
                failedResult.Success = false;
                failedResult.Message = exception.ToString();
                failedResult.Failures = new[] { exception.ToString() };
                WriteAndStop(failedResult);
            }
        }

        private static void SetupControlGroupAndDispatchOrder(ValidationResult result)
        {
            result.GameManagerExists = GameManager.Exists();
            result.HasPlayerSystem = result.GameManagerExists && GameManager.HasSystem<PlayerSystem>();
            result.HasPersistenceSystem = result.GameManagerExists && GameManager.HasSystem<PersistenceSystem>();

            if (!result.HasPlayerSystem)
            {
                throw new InvalidOperationException("PlayerSystem 不存在，无法执行 composite runtime smoke。");
            }

            PlayerSystem playerSystem = GameManager.PlayerSystem;
            CharacterActor player = playerSystem.GetCurrentControlledCharacterOrPlayerInstance() as CharacterActor
                ?? playerSystem.GetPrimaryPlayerCharacter();
            if (player == null)
            {
                throw new InvalidOperationException("当前场景没有可用 CharacterActor。");
            }

            s_originalPlayer = player;
            s_originalPlayerWasControllable = player.CanBePlayerControlled();
            result.PlayerName = player.name;
            result.OriginalPlayerControllable = s_originalPlayerWasControllable;

            CharacterPlayerControl playerControl = player.GetComponent<CharacterPlayerControl>();
            if (playerControl == null)
            {
                throw new InvalidOperationException("当前玩家实例没有 CharacterPlayerControl。");
            }

            CharacterActor primaryActor = player;
            if (!s_originalPlayerWasControllable)
            {
                primaryActor = CreateCompanionClone(player, "控制组主控");
                result.PrimaryActorIsClone = true;
                result.PrimaryActorRegisteredPersistable = TryRegisterCustomPersistable(primaryActor);
                playerControl = primaryActor.GetComponent<CharacterPlayerControl>();
                if (playerControl == null)
                {
                    throw new InvalidOperationException("控制组主控克隆体没有 CharacterPlayerControl。");
                }
            }
            else
            {
                result.PrimaryActorRegisteredPersistable = true;
            }

            s_primaryActor = primaryActor;
            result.RuntimePrimaryName = primaryActor.name;
            result.PlayerBefore = Format(primaryActor.transform.position);

            primaryActor.StartController();
            playerSystem.SetCurrentControlledCharacter(primaryActor);
            playerControl.SetMovementControlMode(EPlayerMovementControlMode.ClickToMove);

            CharacterActor companion = CreateCompanionClone(primaryActor, "控制组陪练");
            if (companion == null)
            {
                throw new InvalidOperationException("无法创建控制组陪练角色。");
            }

            s_companion = companion;
            result.CompanionCreated = true;
            result.CompanionName = companion.name;
            result.CompanionRegisteredPersistable = TryRegisterCustomPersistable(companion);
            result.CompanionBefore = Format(companion.transform.position);

            CharacterPlayerControl companionControl = companion.GetComponent<CharacterPlayerControl>();
            if (companionControl == null)
            {
                throw new InvalidOperationException("陪练角色没有 CharacterPlayerControl。");
            }

            companionControl.SetMovementControlMode(EPlayerMovementControlMode.ClickToMove);
            companion.StartController();
            companion.ResetMovement();

            result.ControlGroupAddSucceeded = playerSystem.TryAddCurrentControlGroupMember(companion);
            PlayerControlGroupSnapshot snapshotAfterAdd = default;
            bool snapshotAvailableAfterAdd = TryGetCurrentControlGroupSnapshot(playerSystem, out snapshotAfterAdd);
            result.ControlGroupMemberCountAfterAdd = snapshotAvailableAfterAdd ? snapshotAfterAdd.MemberCount : 0;
            result.ControlGroupSnapshotAvailableAfterAdd = snapshotAvailableAfterAdd;
            result.ControlGroupPrimaryAfterAdd = snapshotAfterAdd.PrimaryMember != null ? snapshotAfterAdd.PrimaryMember.name : string.Empty;
            result.ControlGroupMembersAfterAdd = snapshotAfterAdd.Members?.Select(member => member != null ? member.name : "null").ToArray() ?? Array.Empty<string>();

            result.ControlGroupPrimarySwitchToCompanion = playerSystem.TrySetCurrentControlGroupPrimaryMember(companion);
            TryGetCurrentControlGroupSnapshot(playerSystem, out PlayerControlGroupSnapshot snapshotAfterSwitch);
            result.ControlGroupPrimaryAfterSwitch = snapshotAfterSwitch.PrimaryMember != null
                ? snapshotAfterSwitch.PrimaryMember.name
                : string.Empty;
            CaptureCurrentControlledInventoryScopeEvidence(result, primaryActor, companion);
            result.ControlGroupPrimaryRestoredToPlayer = playerSystem.TrySetCurrentControlGroupPrimaryMember(primaryActor);
            TryGetCurrentControlGroupSnapshot(playerSystem, out PlayerControlGroupSnapshot snapshotAfterRestore);
            result.ControlGroupPrimaryAfterRestore = snapshotAfterRestore.PrimaryMember != null
                ? snapshotAfterRestore.PrimaryMember.name
                : string.Empty;
            CaptureRestoredControlledInventoryScopeEvidence(result, primaryActor);

            if (!TryGetCurrentControlGroupSnapshot(playerSystem, out PlayerControlGroupSnapshot runtimeSnapshot) ||
                runtimeSnapshot.MemberCount < 2)
            {
                throw new InvalidOperationException("控制组快照未建立，无法继续运行 RTS 订单 smoke。");
            }

            result.ControlGroupRuntimeSnapshotAvailable = true;
            result.ControlGroupRuntimeSnapshotCount = runtimeSnapshot.MemberCount;

            PlayerCommandRequest commandRequest = new(
                GameCommandContext.LocalPlayer(primaryActor),
                EPlayerCommandKind.ClickMove,
                worldPosition: ResolveOrderAnchor(primaryActor, companion));
            PlayerOrderRequest orderRequest = PlayerOrderRequest.FromCommandRequest(commandRequest);

            result.OrderTargetScope = orderRequest.TargetScope.ToString();
            result.OrderQueueMode = orderRequest.QueueMode.ToString();
            result.OrderSpatialPolicy = orderRequest.SpatialContract.Policy.ToString();
            result.OrderSpacing = orderRequest.SpatialContract.Spacing;
            result.OrderAnchor = Format(commandRequest.WorldPosition ?? Vector2.zero);

            s_expectedPlayerTarget = ResolveClickMoveDestination(
                primaryActor,
                commandRequest.WorldPosition ?? Vector2.zero);
            s_expectedCompanionTarget = ResolveClickMoveDestination(
                companion,
                ResolveDistributedRingPosition(
                    commandRequest.WorldPosition ?? Vector2.zero,
                    orderRequest.SpatialContract.Spacing,
                    memberIndex: 1,
                    memberCount: runtimeSnapshot.MemberCount));

            result.ExpectedPrimaryTarget = Format(s_expectedPlayerTarget);
            result.ExpectedCompanionTarget = Format(s_expectedCompanionTarget);

            PlayerOrderResult orderResult = playerSystem.SubmitPlayerOrder(orderRequest);
            result.OrderDispatched = true;
            result.OrderSucceeded = orderResult.Succeeded;
            result.OrderWasQueued = orderResult.WasQueued;
            result.OrderDispatchedMemberCount = orderResult.DispatchedMemberCount;
            result.OrderLastFailureReason = orderResult.LastCommandResult.FailureReason.ToString();
            result.PlayerHasMoveOrderAfterDispatch = primaryActor.HasMoveOrder();
            result.CompanionHasMoveOrderAfterDispatch = companion.HasMoveOrder();
            result.PlayerMoveOrderTargetAvailable = TryGetMoveOrderDestination(primaryActor, out Vector2 primaryMoveOrderTarget);
            result.CompanionMoveOrderTargetAvailable = TryGetMoveOrderDestination(companion, out Vector2 companionMoveOrderTarget);
            result.PlayerMoveOrderTargetAfterDispatch = Format(primaryMoveOrderTarget);
            result.CompanionMoveOrderTargetAfterDispatch = Format(companionMoveOrderTarget);
            result.PlayerMoveOrderTargetDistanceToExpected = Vector2.Distance(primaryMoveOrderTarget, s_expectedPlayerTarget);
            result.CompanionMoveOrderTargetDistanceToExpected = Vector2.Distance(companionMoveOrderTarget, s_expectedCompanionTarget);
        }

        private static bool ShouldContinueObserving(int elapsedFrames)
        {
            if (elapsedFrames >= MaximumObservationFrames)
            {
                return false;
            }

            bool playerStillMoving = s_primaryActor != null && s_primaryActor.HasMoveOrder();
            bool companionStillMoving = s_companion != null && s_companion.HasMoveOrder();
            return playerStillMoving || companionStillMoving;
        }

        /// <summary>
        /// 位置变化只作为运行时观测证据保留，不作为控制组 / 订单链正式合同的硬性门槛。
        /// 当前 smoke 的核心是真正确认订单是否通过正式入口分发并写入成员 MoveOrder。
        /// </summary>
        private static void CaptureMovementEvidence(ValidationResult result)
        {
            if (s_primaryActor == null || s_companion == null)
            {
                throw new InvalidOperationException("控制组成员不存在，无法采集移动证据。");
            }

            Vector2 playerPosition = s_primaryActor.transform.position;
            Vector2 companionPosition = s_companion.transform.position;
            Vector2 playerBefore = ParseVector2(result.PlayerBefore);
            Vector2 companionBefore = ParseVector2(result.CompanionBefore);

            result.PlayerAfter = Format(playerPosition);
            result.CompanionAfter = Format(companionPosition);
            result.PlayerMovedDistance = Vector2.Distance(playerBefore, playerPosition);
            result.CompanionMovedDistance = Vector2.Distance(companionBefore, companionPosition);
            result.PlayerDistanceToExpectedTarget = Vector2.Distance(playerPosition, s_expectedPlayerTarget);
            result.CompanionDistanceToExpectedTarget = Vector2.Distance(companionPosition, s_expectedCompanionTarget);
            result.CompanionDistanceToAnchor = Vector2.Distance(companionPosition, ParseVector2(result.OrderAnchor));
            result.FinalMemberSeparation = Vector2.Distance(playerPosition, companionPosition);
            result.PlayerHasMoveOrderAfterObserve = s_primaryActor.HasMoveOrder();
            result.CompanionHasMoveOrderAfterObserve = s_companion.HasMoveOrder();
        }

        private static void CaptureControlGroupCollapseEvidence(ValidationResult result)
        {
            if (s_primaryActor == null || s_companion == null || !GameManager.Exists() || !GameManager.HasSystem<PlayerSystem>())
            {
                return;
            }

            PlayerSystem playerSystem = GameManager.PlayerSystem;
            result.ControlGroupRemoveSucceeded = playerSystem.TryRemoveCurrentControlGroupMember(s_companion);
            result.ControlGroupSnapshotAvailableAfterCollapse = TryGetCurrentControlGroupSnapshot(playerSystem, out PlayerControlGroupSnapshot collapseSnapshot);
            result.ControlGroupMemberCountAfterCollapse = result.ControlGroupSnapshotAvailableAfterCollapse ? collapseSnapshot.MemberCount : 0;
            result.CurrentControlledCharacterRestoredToPrimary =
                playerSystem.TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter) &&
                currentControlledCharacter == s_primaryActor;
            result.CurrentInputTargetRestoredToCharacterPlayerControl =
                playerSystem.TryGetCurrentInputTarget(out IPlayerInputTarget inputTarget) &&
                inputTarget is CharacterPlayerControl;
        }

        private static bool TryGetCurrentControlGroupSnapshot(
            PlayerSystem playerSystem,
            out PlayerControlGroupSnapshot snapshot)
        {
            snapshot = default;
            if (playerSystem == null ||
                !playerSystem.TryGetCurrentInputTarget(out IPlayerInputTarget inputTarget) ||
                inputTarget is not PlayerControlGroup controlGroup)
            {
                return false;
            }

            snapshot = controlGroup.CreateSnapshot();
            return snapshot.IsValid;
        }

        private static void CaptureInventoryAndAbilityEvidence(ValidationResult result)
        {
            if (s_primaryActor == null || s_companion == null)
            {
                throw new InvalidOperationException("控制组成员不存在，无法执行背包与能力 smoke。");
            }

            RunInventoryOwnershipSmoke(result, s_primaryActor, s_companion);
            RunAbilityCompositionSmoke(result, s_primaryActor);
        }

        private static void FinalizeResult(ValidationResult result)
        {
            List<string> failures = new();

            Require(result.GameManagerExists, "GameManager 未启动。", failures);
            Require(result.HasPlayerSystem, "PlayerSystem 未注册。", failures);
            Require(result.HasPersistenceSystem, "PersistenceSystem 未注册。", failures);
            Require(result.CompanionCreated, "未能创建控制组陪练角色。", failures);
            Require(result.PrimaryActorRegisteredPersistable, "控制组主控体没有登记到正式持久化注册表。", failures);
            Require(result.CompanionRegisteredPersistable, "陪练角色没有登记到正式持久化注册表。", failures);

            Require(result.ControlGroupAddSucceeded, "陪练角色没有通过 PlayerSystem 加入当前控制组。", failures);
            Require(result.ControlGroupSnapshotAvailableAfterAdd, "控制组对外快照没有建立。", failures);
            Require(result.ControlGroupMemberCountAfterAdd == 2, $"控制组成员数应为 2，实际为 {result.ControlGroupMemberCountAfterAdd}。", failures);
            Require(result.ControlGroupRuntimeSnapshotAvailable, "控制组运行时快照没有建立。", failures);
            Require(result.ControlGroupRuntimeSnapshotCount == 2, $"控制组运行时快照成员数应为 2，实际为 {result.ControlGroupRuntimeSnapshotCount}。", failures);
            Require(result.ControlGroupPrimarySwitchToCompanion, "控制组主控切换到陪练角色失败。", failures);
            Require(result.ControlGroupPrimaryAfterSwitch == result.CompanionName, "控制组主控切换后不是陪练角色。", failures);
            Require(result.ControlGroupPrimaryRestoredToPlayer, "控制组主控恢复到玩家失败。", failures);
            Require(result.ControlGroupPrimaryAfterRestore == result.RuntimePrimaryName, "控制组主控恢复后不是运行时主控体。", failures);
            Require(result.InventoryScopeResolvedToCompanionAfterSwitch, "当前受控角色切换后，背包上下文没有切到陪练角色。", failures);
            Require(result.InventoryScopeResolvedToPrimaryAfterRestore, "当前受控角色恢复后，背包上下文没有切回主控角色。", failures);
            Require(result.InventoryPrimaryAndCompanionOwnersAreDistinct, "两个角色的背包 owner 没有分开。", failures);
            Require(result.InventoryPrimaryCountBeforeTransfers == 1, $"主控角色初始背包数量应为 1，实际为 {result.InventoryPrimaryCountBeforeTransfers}。", failures);
            Require(result.InventoryCompanionCountBeforeTransfers == 2, $"陪练角色初始背包数量应为 2，实际为 {result.InventoryCompanionCountBeforeTransfers}。", failures);
            Require(result.InventoryContainerTransferSucceeded, "容器转移没有通过正式 InventorySystem 闭包。", failures);
            Require(result.InventoryContainerRoundTripRestoredCounts, "容器转移回滚后数量没有恢复。", failures);
            Require(result.InventoryCorpseTransferSucceeded, "尸体转移没有通过正式 InventorySystem 闭包。", failures);
            Require(result.InventoryCorpseRoundTripRestoredCounts, "尸体转移回滚后数量没有恢复。", failures);
            Require(result.InventoryCompanionUnchangedByPrimaryTransfers, "主控角色的库存流转影响到了陪练角色。", failures);
            Require(result.FormalGasAbilityBaselineResolved, "没有找到可用于变形/感染验证的正式 EX-GAS 能力。", failures);
            Require(result.FormalGasAbilityReplacementGrantSucceeded, "变形式 EX-GAS 能力授予没有成功。", failures);
            Require(result.AbilitySuppressionSucceeded, "变形式 EX-GAS 能力压制没有成功。", failures);
            Require(result.FormalGasAbilityReplacementPresentDuringTransformation, "变形期没有拿到临时授予的 EX-GAS 能力。", failures);
            Require(result.FormalGasAbilityBaselinePresentDuringTransformation, "变形期基础 EX-GAS 能力不再属于角色。", failures);
            Require(result.FormalGasAbilityBaselineSuppressedDuringTransformation, "基础 EX-GAS 能力没有在变形期被压制。", failures);
            Require(result.AbilityTransformationRemovedGrant, "变形结束后，临时授予能力没有执行撤回。", failures);
            Require(result.AbilityTransformationRemovedSuppression, "变形结束后，基础能力压制没有执行撤回。", failures);
            Require(result.AbilityTransformationRestoredBaseline, "变形结束后，基础 EX-GAS 能力没有恢复。", failures);
            Require(result.AbilityTransformationRemovedReplacement, "变形结束后，临时授予能力仍然残留。", failures);
            Require(result.FormalGasAbilityBaselineRemovedBySmokeCleanup, "能力 smoke 结束后，临时基础 EX-GAS 能力没有清理。", failures);

            Require(string.Equals(result.OrderTargetScope, EPlayerOrderTargetScope.ControlledGroup.ToString(), StringComparison.Ordinal), "点击移动订单没有落到控制组目标范围。", failures);
            Require(string.Equals(result.OrderSpatialPolicy, EPlayerOrderSpatialPolicy.DistributedRing.ToString(), StringComparison.Ordinal), "点击移动订单没有启用 DistributedRing 落点合同。", failures);
            Require(result.OrderDispatched, "正式订单没有进入分发链。", failures);
            Require(result.OrderSucceeded, $"正式订单提交失败：{result.OrderLastFailureReason}。", failures);
            Require(result.OrderDispatchedMemberCount == 2, $"订单应分发给 2 个成员，实际为 {result.OrderDispatchedMemberCount}。", failures);
            Require(result.PlayerHasMoveOrderAfterDispatch, "玩家角色在订单下发后没有进入正式 MoveOrder。", failures);
            Require(result.CompanionHasMoveOrderAfterDispatch, "陪练角色在订单下发后没有进入正式 MoveOrder。", failures);
            Require(result.PlayerMoveOrderTargetAvailable, "玩家角色没有写入正式 MoveOrder 目标点。", failures);
            Require(result.CompanionMoveOrderTargetAvailable, "陪练角色没有写入正式 MoveOrder 目标点。", failures);
            Require(result.PlayerMoveOrderTargetDistanceToExpected <= TargetTolerance, $"玩家角色 MoveOrder 目标点没有落在主控锚点，误差 {result.PlayerMoveOrderTargetDistanceToExpected:0.###}。", failures);
            Require(result.CompanionMoveOrderTargetDistanceToExpected <= TargetTolerance, $"陪练角色 MoveOrder 目标点没有落在 DistributedRing 分布点，误差 {result.CompanionMoveOrderTargetDistanceToExpected:0.###}。", failures);

            Require(result.ControlGroupRemoveSucceeded, "控制组收缩回单角色失败。", failures);
            Require(result.ControlGroupMemberCountAfterCollapse == 0, $"控制组收缩后成员数应为 0，实际为 {result.ControlGroupMemberCountAfterCollapse}。", failures);
            Require(!result.ControlGroupSnapshotAvailableAfterCollapse, "控制组收缩后仍然能读到控制组快照。", failures);
            Require(result.CurrentControlledCharacterRestoredToPrimary, "控制组收缩后当前受控角色没有回到运行时主控体。", failures);
            Require(result.CurrentInputTargetRestoredToCharacterPlayerControl, "控制组收缩后当前输入目标没有回到 CharacterPlayerControl。", failures);

            result.Success = failures.Count == 0;
            result.Failures = failures.ToArray();
            result.Message = result.Success
                ? "控制组 / RTS 订单链 smoke 通过。"
                : string.Join(" | ", failures);
            result.Completed = true;
        }

        private static CharacterActor CreateCompanionClone(CharacterActor player, string cloneName)
        {
            Vector3 spawnPosition = ResolveCompanionSpawnPosition(player);
            GameObject sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(player.gameObject) ?? player.gameObject;
            GameObject companionObject = UnityEngine.Object.Instantiate(sourceObject, spawnPosition, player.transform.rotation);
            companionObject.name = cloneName;
            ApplyDontSaveHierarchy(companionObject);
            if (companionObject.CompareTag("Player"))
            {
                companionObject.tag = "Untagged";
            }

            CharacterActor companion = companionObject.GetComponent<CharacterActor>();
            if (companion == null)
            {
                UnityEngine.Object.DestroyImmediate(companionObject);
                throw new InvalidOperationException("控制组陪练对象不是 CharacterActor。");
            }

            companion.TeleportTo(spawnPosition);
            companion.ResetMovement();
            return companion;
        }

        private static Vector3 ResolveCompanionSpawnPosition(CharacterActor player)
        {
            Vector3 origin = player.transform.position;
            Vector3[] candidates =
            {
                origin + new Vector3(0.9f, 0.0f, 0.0f),
                origin + new Vector3(-0.9f, 0.0f, 0.0f),
                origin + new Vector3(0.0f, 0.9f, 0.0f),
                origin + new Vector3(0.0f, -0.9f, 0.0f),
            };

            foreach (Vector3 candidate in candidates)
            {
                Vector3 resolved = player.NearestValidDestination(candidate);
                if (Vector2.Distance(origin, resolved) >= 0.5f)
                {
                    return resolved;
                }
            }

            return origin + new Vector3(0.9f, 0.0f, 0.0f);
        }

        private static Vector2 ResolveOrderAnchor(CharacterActor player, CharacterActor companion)
        {
            Vector2 playerOrigin = player.transform.position;
            Vector2[] offsets =
            {
                new(2.0f, 0.0f),
                new(1.5f, 0.75f),
                new(-2.0f, 0.0f),
                new(0.0f, 2.0f),
            };

            foreach (Vector2 offset in offsets)
            {
                Vector2 anchor = player.NearestValidDestination(playerOrigin + offset);
                Vector2 distributedTarget = companion.NearestValidDestination(
                    ResolveDistributedRingPosition(anchor, 0.65f, memberIndex: 1, memberCount: 2));

                if (Vector2.Distance(playerOrigin, anchor) >= 0.8f &&
                    Vector2.Distance(anchor, distributedTarget) >= SeparationTolerance)
                {
                    return anchor;
                }
            }

            return player.NearestValidDestination(playerOrigin + offsets[0]);
        }

        private static Vector2 ResolveClickMoveDestination(CharacterActor character, Vector2 requestedTarget)
        {
            if (GameManager.Exists() &&
                GameManager.TryGetSystem(out MapSystem mapSystem))
            {
                MapInfo mapInfo = ResolveActiveMapInfo(mapSystem);
                if (mapInfo != null &&
                    mapInfo.TryGetTerrainNavigationMap(out TerrainNavigationMap terrainNavigationMap) &&
                    terrainNavigationMap.TryBuildWorldPath(
                        character.transform.position,
                        requestedTarget,
                        out Vector2[] worldPath) &&
                    worldPath.Length > 0)
                {
                    return worldPath[^1];
                }
            }

            return character.NearestValidDestination(requestedTarget);
        }

        private static MapInfo ResolveActiveMapInfo(MapSystem mapSystem)
        {
            MethodInfo? resolveMethod = typeof(MapSystem).GetMethod(
                "ResolveActiveMapInfo",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return resolveMethod?.Invoke(mapSystem, null) as MapInfo;
        }

        private static Vector2 ResolveDistributedRingPosition(
            Vector2 anchor,
            float spacing,
            int memberIndex,
            int memberCount)
        {
            if (memberCount <= 1 || memberIndex <= 0)
            {
                return anchor;
            }

            int remainingIndex = memberIndex - 1;
            int ring = 1;
            int slotsInRing = 6;
            while (remainingIndex >= slotsInRing)
            {
                remainingIndex -= slotsInRing;
                ring++;
                slotsInRing = Mathf.Max(6, ring * 6);
            }

            float angle = (Mathf.PI * 2f * remainingIndex) / slotsInRing;
            float radius = spacing * ring;
            Vector2 offset = new(Mathf.Cos(angle), Mathf.Sin(angle));
            return anchor + offset * radius;
        }

        private static void CaptureCurrentControlledInventoryScopeEvidence(ValidationResult result, CharacterActor primaryActor, CharacterActor companion)
        {
            if (!GameManager.Exists() || !GameManager.HasSystem<InventorySystem>())
            {
                return;
            }

            InventorySystem inventorySystem = GameManager.InventorySystem;
            InventoryOwnerHandle primaryOwner = inventorySystem.GetOwner(primaryActor);
            InventoryOwnerHandle companionOwner = inventorySystem.GetOwner(companion);
            InventoryOwnerHandle currentOwner = inventorySystem.GetOwner(EInventoryQueryScope.CurrentControlledCharacter);

            result.InventoryPrimaryAndCompanionOwnersAreDistinct = !primaryOwner.Equals(companionOwner);
            result.InventoryScopeResolvedToCompanionAfterSwitch = currentOwner.Equals(companionOwner);
        }

        private static void CaptureRestoredControlledInventoryScopeEvidence(ValidationResult result, CharacterActor primaryActor)
        {
            if (!GameManager.Exists() || !GameManager.HasSystem<InventorySystem>())
            {
                return;
            }

            InventorySystem inventorySystem = GameManager.InventorySystem;
            InventoryOwnerHandle primaryOwner = inventorySystem.GetOwner(primaryActor);
            InventoryOwnerHandle currentOwner = inventorySystem.GetOwner(EInventoryQueryScope.CurrentControlledCharacter);
            result.InventoryScopeResolvedToPrimaryAfterRestore = currentOwner.Equals(primaryOwner);
        }

        private static void RunInventoryOwnershipSmoke(ValidationResult result, CharacterActor primaryActor, CharacterActor companion)
        {
            InventorySystem inventorySystem = GameManager.InventorySystem;
            InventoryOwnerHandle primaryOwner = inventorySystem.GetOwner(primaryActor);
            InventoryOwnerHandle companionOwner = inventorySystem.GetOwner(companion);
            InventoryOwnerHandle containerOwner = InventoryOwnerHandle.ForPersistable(
                EInventoryOwnerKind.Container,
                GetOrCreateSmokeContainer());
            InventoryOwnerHandle corpseOwner = inventorySystem.GetCorpseOwner(primaryActor);

            if (s_probeItem == null)
            {
                s_probeItem = ScriptableObject.CreateInstance<Item>();
                s_probeItem.name = SmokeItemName;
                s_probeItem.hideFlags = HideFlags.DontSave;
            }

            inventorySystem.AddToBag(primaryOwner, s_probeItem, 1, EItemTransferType.Command);
            inventorySystem.AddToBag(companionOwner, s_probeItem, 2, EItemTransferType.Command);

            result.InventoryPrimaryCountBeforeTransfers = inventorySystem.GetItemCount(primaryOwner, s_probeItem);
            result.InventoryCompanionCountBeforeTransfers = inventorySystem.GetItemCount(companionOwner, s_probeItem);

            result.InventoryContainerTransferSucceeded = inventorySystem.TransferItem(
                primaryOwner,
                containerOwner,
                s_probeItem,
                1,
                EItemTransferType.Chest);
            result.InventoryPrimaryCountAfterContainerTransfer = inventorySystem.GetItemCount(primaryOwner, s_probeItem);
            result.InventoryContainerCountAfterContainerTransfer = inventorySystem.GetItemCount(containerOwner, s_probeItem);
            result.InventoryCompanionCountAfterContainerTransfer = inventorySystem.GetItemCount(companionOwner, s_probeItem);

            result.InventoryContainerRoundTripRestoredCounts =
                result.InventoryContainerTransferSucceeded &&
                inventorySystem.TransferItem(containerOwner, primaryOwner, s_probeItem, 1, EItemTransferType.Chest) &&
                inventorySystem.GetItemCount(primaryOwner, s_probeItem) == 1 &&
                inventorySystem.GetItemCount(containerOwner, s_probeItem) == 0;

            result.InventoryCorpseTransferSucceeded = inventorySystem.TransferCharacterInventoryToCorpse(primaryActor);
            result.InventoryPrimaryCountAfterCorpseTransfer = inventorySystem.GetItemCount(primaryOwner, s_probeItem);
            result.InventoryCorpseCountAfterCorpseTransfer = inventorySystem.GetItemCount(corpseOwner, s_probeItem);
            result.InventoryCompanionCountAfterCorpseTransfer = inventorySystem.GetItemCount(companionOwner, s_probeItem);

            result.InventoryCorpseRoundTripRestoredCounts =
                result.InventoryCorpseTransferSucceeded &&
                inventorySystem.TransferCorpseInventoryToCharacter(primaryActor) &&
                inventorySystem.GetItemCount(primaryOwner, s_probeItem) == 1 &&
                inventorySystem.GetItemCount(corpseOwner, s_probeItem) == 0;

            result.InventoryCompanionUnchangedByPrimaryTransfers =
                inventorySystem.GetItemCount(companionOwner, s_probeItem) == 2 &&
                result.InventoryCompanionCountAfterContainerTransfer == 2 &&
                result.InventoryCompanionCountAfterCorpseTransfer == 2;

            inventorySystem.RemoveFromBag(primaryOwner, s_probeItem, inventorySystem.GetItemCount(primaryOwner, s_probeItem));
            inventorySystem.RemoveFromBag(companionOwner, s_probeItem, inventorySystem.GetItemCount(companionOwner, s_probeItem));
        }

        private static void RunAbilityCompositionSmoke(ValidationResult result, CharacterActor primaryActor)
        {
            s_baselineAbilityCode = SmokeBaselineAbilityCode;
            s_replacementAbilityCode = SmokeReplacementAbilityCode;
            result.FormalGasAbilityBaselineResolved = s_baselineAbilityCode > 0;
            result.FormalGasAbilityReplacementResolved = s_replacementAbilityCode > 0;
            if (s_baselineAbilityCode <= 0 || s_replacementAbilityCode <= 0)
            {
                return;
            }

            result.FormalGasAbilityBaselineWasOwnedBeforeSmoke = primaryActor.HasFormalGasAbility(s_baselineAbilityCode);
            if (!result.FormalGasAbilityBaselineWasOwnedBeforeSmoke)
            {
                result.FormalGasAbilityBaselineAddedBySmoke = primaryActor.AddSourcedBonusFormalGasAbility(
                    s_baselineAbilityCode,
                    new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, SmokeTransformationId));
                s_baselineAbilityAddedBySmoke = result.FormalGasAbilityBaselineAddedBySmoke;
                if (!result.FormalGasAbilityBaselineAddedBySmoke)
                {
                    return;
                }
            }

            result.FormalGasAbilityReplacementGrantSucceeded = primaryActor.AddSourcedBonusFormalGasAbility(
                s_replacementAbilityCode,
                new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, SmokeTransformationId));
            result.AbilitySuppressionSucceeded = primaryActor.AddSourcedFormalGasAbilitySuppression(
                s_baselineAbilityCode,
                new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, SmokeTransformationId));
            s_transformationApplied = result.FormalGasAbilityReplacementGrantSucceeded && result.AbilitySuppressionSucceeded;
            if (!s_transformationApplied)
            {
                return;
            }

            result.FormalGasAbilityReplacementPresentDuringTransformation = primaryActor.HasFormalGasAbility(s_replacementAbilityCode);
            result.FormalGasAbilityBaselinePresentDuringTransformation = primaryActor.HasFormalGasAbility(s_baselineAbilityCode);
            result.FormalGasAbilityBaselineSuppressedDuringTransformation = primaryActor.IsFormalGasAbilitySuppressed(s_baselineAbilityCode);
            result.FormalGasAbilityReplacementSuppressedDuringTransformation = primaryActor.IsFormalGasAbilitySuppressed(s_replacementAbilityCode);

            result.AbilityTransformationRemovedGrant = primaryActor.RemoveSourcedBonusFormalGasAbility(
                s_replacementAbilityCode,
                new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, SmokeTransformationId));
            result.AbilityTransformationRemovedSuppression = primaryActor.RemoveSourcedFormalGasAbilitySuppression(
                s_baselineAbilityCode,
                new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, SmokeTransformationId));
            result.AbilityTransformationRemovedReplacement = !primaryActor.HasFormalGasAbility(s_replacementAbilityCode) ||
                s_replacementAbilityCode == s_baselineAbilityCode;
            result.AbilityTransformationRestoredBaseline =
                primaryActor.HasFormalGasAbility(s_baselineAbilityCode) &&
                !primaryActor.IsFormalGasAbilitySuppressed(s_baselineAbilityCode);

            if (s_baselineAbilityAddedBySmoke)
            {
                result.FormalGasAbilityBaselineRemovedBySmokeCleanup =
                    primaryActor.RemoveSourcedBonusFormalGasAbility(
                        s_baselineAbilityCode,
                        new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, SmokeTransformationId)) &&
                    !primaryActor.HasFormalGasAbility(s_baselineAbilityCode);
            }
            else
            {
                result.FormalGasAbilityBaselineRemovedBySmokeCleanup = true;
            }

            s_transformationApplied = false;
        }

        private static Persistable GetOrCreateSmokeContainer()
        {
            if (s_container != null)
            {
                return s_container;
            }

            GameObject containerObject = new(SmokeContainerName)
            {
                hideFlags = HideFlags.DontSave
            };
            ApplyDontSaveHierarchy(containerObject);
            s_container = containerObject.AddComponent<Persistable>();
            s_container.hideFlags = HideFlags.DontSave;
            return s_container;
        }

        private static void ApplyDontSaveHierarchy(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.DontSave;
                foreach (Component component in child.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        component.hideFlags = HideFlags.DontSave;
                    }
                }
            }
        }

        private static bool TryRegisterCustomPersistable(Persistable persistable)
        {
            if (persistable == null || !GameManager.Exists() || !GameManager.HasSystem<PersistenceSystem>())
            {
                return false;
            }

            MethodInfo? registerMethod = typeof(PersistenceSystem).GetMethod(
                "RegisterCustomInstancedPersistable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (registerMethod == null)
            {
                return false;
            }

            registerMethod.Invoke(GameManager.PersistenceSystem, new object?[] { persistable, null });
            return true;
        }

        private static bool TryGetMoveOrderDestination(Movable movable, out Vector2 targetPosition)
        {
            targetPosition = Vector2.zero;
            if (movable == null)
            {
                return false;
            }

            FieldInfo? motionRuntimeField = typeof(Movable).GetField("m_motionRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
            object? motionRuntime = motionRuntimeField?.GetValue(movable);
            if (motionRuntime == null)
            {
                return false;
            }

            FieldInfo? moveOrderField = motionRuntime.GetType().GetField("m_moveOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            object? boxedMoveOrder = moveOrderField?.GetValue(motionRuntime);
            if (boxedMoveOrder == null)
            {
                return false;
            }

            FieldInfo? waypointsField = boxedMoveOrder.GetType().GetField("waypoints", BindingFlags.Instance | BindingFlags.Public);
            if (waypointsField?.GetValue(boxedMoveOrder) is Vector2[] { Length: > 0 } waypoints)
            {
                targetPosition = waypoints[^1];
                return true;
            }

            FieldInfo? targetPositionField = boxedMoveOrder.GetType().GetField("targetPosition", BindingFlags.Instance | BindingFlags.Public);
            if (targetPositionField == null)
            {
                return false;
            }

            object? value = targetPositionField.GetValue(boxedMoveOrder);
            if (value is not Vector2 resolvedTargetPosition)
            {
                return false;
            }

            targetPosition = resolvedTargetPosition;
            return true;
        }

        private static ValidationResult Fail(string message)
        {
            return new ValidationResult
            {
                Completed = true,
                Success = false,
                Message = message,
                Failures = new[] { message },
            };
        }

        private static void WriteAndStop(ValidationResult result)
        {
            try
            {
                CleanupRuntimeArtifacts();
            }
            finally
            {
                WriteResult(result);
                StopTicking();
            }
        }

        private static void CleanupRuntimeArtifacts()
        {
            if (s_primaryActor != null)
            {
                if (s_transformationApplied)
                {
                    if (s_replacementAbilityCode > 0)
                    {
                        s_primaryActor.RemoveSourcedBonusFormalGasAbility(
                            s_replacementAbilityCode,
                            new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, SmokeTransformationId));
                    }

                    if (s_baselineAbilityCode > 0)
                    {
                        s_primaryActor.RemoveSourcedFormalGasAbilitySuppression(
                            s_baselineAbilityCode,
                            new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, SmokeTransformationId));
                    }
                }

                if (s_baselineAbilityAddedBySmoke && s_baselineAbilityCode > 0)
                {
                    s_primaryActor.RemoveSourcedBonusFormalGasAbility(
                        s_baselineAbilityCode,
                        new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.Transformation, SmokeTransformationId));
                }
            }

            if (GameManager.Exists() && GameManager.HasSystem<InventorySystem>() && s_probeItem != null)
            {
                InventorySystem inventorySystem = GameManager.InventorySystem;
                if (s_primaryActor != null)
                {
                    InventoryOwnerHandle primaryOwner = inventorySystem.GetOwner(s_primaryActor);
                    int primaryCount = inventorySystem.GetItemCount(primaryOwner, s_probeItem);
                    if (primaryCount > 0)
                    {
                        inventorySystem.RemoveFromBag(primaryOwner, s_probeItem, primaryCount);
                    }

                    InventoryOwnerHandle corpseOwner = inventorySystem.GetCorpseOwner(s_primaryActor);
                    int corpseCount = inventorySystem.GetItemCount(corpseOwner, s_probeItem);
                    if (corpseCount > 0)
                    {
                        inventorySystem.RemoveFromBag(corpseOwner, s_probeItem, corpseCount);
                    }
                }

                if (s_companion != null)
                {
                    InventoryOwnerHandle companionOwner = inventorySystem.GetOwner(s_companion);
                    int companionCount = inventorySystem.GetItemCount(companionOwner, s_probeItem);
                    if (companionCount > 0)
                    {
                        inventorySystem.RemoveFromBag(companionOwner, s_probeItem, companionCount);
                    }
                }

                if (s_container != null)
                {
                    InventoryOwnerHandle containerOwner = InventoryOwnerHandle.ForPersistable(
                        EInventoryOwnerKind.Container,
                        s_container);
                    int containerCount = inventorySystem.GetItemCount(containerOwner, s_probeItem);
                    if (containerCount > 0)
                    {
                        inventorySystem.RemoveFromBag(containerOwner, s_probeItem, containerCount);
                    }
                }
            }

            if (GameManager.Exists() && GameManager.HasSystem<PlayerSystem>())
            {
                PlayerSystem playerSystem = GameManager.PlayerSystem;
                if (s_companion != null)
                {
                    playerSystem.TryRemoveCurrentControlGroupMember(s_companion);
                }

                if (s_originalPlayerWasControllable && s_originalPlayer != null)
                {
                    playerSystem.SetCurrentControlledCharacter(s_originalPlayer);
                }
                else
                {
                    playerSystem.SetCurrentInputTarget(null);
                }
            }

            if (s_companion != null)
            {
                UnityEngine.Object.DestroyImmediate(s_companion.gameObject);
                s_companion = null;
            }

            if (s_primaryActor != null && s_primaryActor != s_originalPlayer)
            {
                UnityEngine.Object.DestroyImmediate(s_primaryActor.gameObject);
            }

            if (s_container != null)
            {
                UnityEngine.Object.DestroyImmediate(s_container.gameObject);
                s_container = null;
            }

            if (s_probeItem != null)
            {
                UnityEngine.Object.DestroyImmediate(s_probeItem);
                s_probeItem = null;
            }

            s_primaryActor = null;
            s_originalPlayer = null;
            s_baselineAbilityAddedBySmoke = false;
            s_transformationApplied = false;
        }

        private static void WriteResult(ValidationResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath)!);
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
        }

        private static void StopTicking()
        {
            s_running = false;
            EditorApplication.update -= Tick;
        }

        private static void Require(bool condition, string failure, List<string> failures)
        {
            if (!condition)
            {
                failures.Add(failure);
            }
        }

        private static string Format(Vector2 value) => $"({value.x:0.###}, {value.y:0.###})";
        private static string Format(Vector3 value) => $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";

        private static Vector2 ParseVector2(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Vector2.zero;
            }

            string[] parts = value.Trim('(', ')').Split(',');
            if (parts.Length < 2)
            {
                return Vector2.zero;
            }

            return new Vector2(
                float.TryParse(parts[0].Trim(), out float x) ? x : 0.0f,
                float.TryParse(parts[1].Trim(), out float y) ? y : 0.0f);
        }

        /// <summary>
        /// 启动命令返回给外部桥接层的最小结果，只暴露最终 JSON 证据路径。
        /// </summary>
        [Serializable]
        public sealed class StartResult
        {
            public string ResultPath = string.Empty;
        }

        /// <summary>
        /// Composite runtime smoke 的完整证据对象，覆盖控制组、订单、背包、能力替换和清理结果。
        /// </summary>
        [Serializable]
        public sealed class ValidationResult
        {
            public bool Completed;
            public bool Success;
            public string Message = string.Empty;
            public string ScenePath = string.Empty;
            public string ScreenSize = string.Empty;
            public bool GameManagerExists;
            public bool HasPlayerSystem;
            public bool HasPersistenceSystem;

            public string PlayerName = string.Empty;
            public bool OriginalPlayerControllable;
            public bool PrimaryActorIsClone;
            public bool PrimaryActorRegisteredPersistable;
            public string RuntimePrimaryName = string.Empty;
            public string CompanionName = string.Empty;
            public bool CompanionCreated;
            public bool CompanionRegisteredPersistable;
            public bool InventoryScopeResolvedToCompanionAfterSwitch;
            public bool InventoryScopeResolvedToPrimaryAfterRestore;
            public bool InventoryPrimaryAndCompanionOwnersAreDistinct;
            public int InventoryPrimaryCountBeforeTransfers;
            public int InventoryCompanionCountBeforeTransfers;
            public bool InventoryContainerTransferSucceeded;
            public int InventoryPrimaryCountAfterContainerTransfer;
            public int InventoryContainerCountAfterContainerTransfer;
            public int InventoryCompanionCountAfterContainerTransfer;
            public bool InventoryContainerRoundTripRestoredCounts;
            public bool InventoryCorpseTransferSucceeded;
            public int InventoryPrimaryCountAfterCorpseTransfer;
            public int InventoryCorpseCountAfterCorpseTransfer;
            public int InventoryCompanionCountAfterCorpseTransfer;
            public bool InventoryCorpseRoundTripRestoredCounts;
            public bool InventoryCompanionUnchangedByPrimaryTransfers;

            public bool FormalGasAbilityBaselineResolved;
            public bool FormalGasAbilityReplacementResolved;
            public bool FormalGasAbilityBaselineWasOwnedBeforeSmoke;
            public bool FormalGasAbilityBaselineAddedBySmoke;
            public bool FormalGasAbilityReplacementGrantSucceeded;
            public bool AbilitySuppressionSucceeded;
            public bool FormalGasAbilityReplacementPresentDuringTransformation;
            public bool FormalGasAbilityBaselinePresentDuringTransformation;
            public bool FormalGasAbilityBaselineSuppressedDuringTransformation;
            public bool FormalGasAbilityReplacementSuppressedDuringTransformation;
            public bool AbilityTransformationRemovedGrant;
            public bool AbilityTransformationRemovedSuppression;
            public bool AbilityTransformationRestoredBaseline;
            public bool AbilityTransformationRemovedReplacement;
            public bool FormalGasAbilityBaselineRemovedBySmokeCleanup;

            public bool ControlGroupAddSucceeded;
            public int ControlGroupMemberCountAfterAdd;
            public bool ControlGroupSnapshotAvailableAfterAdd;
            public string ControlGroupPrimaryAfterAdd = string.Empty;
            public string[] ControlGroupMembersAfterAdd = Array.Empty<string>();
            public bool ControlGroupPrimarySwitchToCompanion;
            public string ControlGroupPrimaryAfterSwitch = string.Empty;
            public bool ControlGroupPrimaryRestoredToPlayer;
            public string ControlGroupPrimaryAfterRestore = string.Empty;
            public bool ControlGroupRuntimeSnapshotAvailable;
            public int ControlGroupRuntimeSnapshotCount;

            public bool OrderDispatched;
            public bool OrderSucceeded;
            public bool OrderWasQueued;
            public int OrderDispatchedMemberCount;
            public string OrderLastFailureReason = string.Empty;
            public string OrderTargetScope = string.Empty;
            public string OrderQueueMode = string.Empty;
            public string OrderSpatialPolicy = string.Empty;
            public float OrderSpacing;
            public string OrderAnchor = string.Empty;
            public string ExpectedPrimaryTarget = string.Empty;
            public string ExpectedCompanionTarget = string.Empty;
            public bool PlayerHasMoveOrderAfterDispatch;
            public bool CompanionHasMoveOrderAfterDispatch;
            public bool PlayerHasMoveOrderAfterObserve;
            public bool CompanionHasMoveOrderAfterObserve;
            public bool PlayerMoveOrderTargetAvailable;
            public bool CompanionMoveOrderTargetAvailable;
            public string PlayerMoveOrderTargetAfterDispatch = string.Empty;
            public string CompanionMoveOrderTargetAfterDispatch = string.Empty;
            public float PlayerMoveOrderTargetDistanceToExpected;
            public float CompanionMoveOrderTargetDistanceToExpected;

            public string PlayerBefore = string.Empty;
            public string PlayerAfter = string.Empty;
            public string CompanionBefore = string.Empty;
            public string CompanionAfter = string.Empty;
            public float PlayerMovedDistance;
            public float CompanionMovedDistance;
            public float PlayerDistanceToExpectedTarget;
            public float CompanionDistanceToExpectedTarget;
            public float CompanionDistanceToAnchor;
            public float FinalMemberSeparation;

            public bool ControlGroupRemoveSucceeded;
            public int ControlGroupMemberCountAfterCollapse;
            public bool ControlGroupSnapshotAvailableAfterCollapse;
            public bool CurrentControlledCharacterRestoredToPrimary;
            public bool CurrentInputTargetRestoredToCharacterPlayerControl;

            public string[] Failures = Array.Empty<string>();
        }
    }
}
