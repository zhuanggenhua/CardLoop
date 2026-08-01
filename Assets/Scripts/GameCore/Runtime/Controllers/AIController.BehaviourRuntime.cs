using System;
using ContextSteering2D;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public partial class AIController
    {
        /// <summary>
        /// `AIController` 的内部业务行为模块。
        /// 这里只维护目标、攻击和战斗移动状态；转向求解、检测和局部避让统一交给 ContextSteering2D 世界模拟。
        /// </summary>
        private sealed class BehaviourRuntime
        {
            private const float MinimumNavigationWaypointTolerance = 0.3f;

            private readonly AIController m_owner;
            private CharacterSteeringRuntime2D m_steeringAdapter = null;
            private readonly CharacterSteeringPathCursor2D m_pathCursor = new();
            private readonly CombatWanderRuntime2D m_combatWander = new();
            private Vector2 m_steeringAverageOutput = Vector2.zero;
            private Vector2 m_targetPosition = Vector2.zero;
            private float m_navigationRepathTimer;
            private bool m_waitingForAttackFacing = false;

            public BehaviourRuntime(AIController owner)
            {
                m_owner = owner;
            }

            public void Initialize()
            {
                m_owner.m_initialPosition = m_owner.m_subject.transform.position;

                if (m_owner.m_subject is not CharacterBase character)
                {
                    throw new InvalidOperationException("AIController can only be attached to a CharacterBase.");
                }

                m_steeringAdapter = new CharacterSteeringRuntime2D(character, m_owner.m_steeringProfile);
                ValidateSteeringGroupMapping(m_owner.m_transitSteeringGroupId, "中间路线");
                ValidateSteeringGroupMapping(m_owner.m_targetPursuitSteeringGroupId, "移动目标追击");
                if (m_owner.ShouldUseCombatWander)
                {
                    ValidateSteeringGroupMapping(m_owner.m_combatWanderSteeringGroupId, "战斗游走");
                }
                if (m_owner.ShouldUseTargetOrbitSteeringAtSoughtDistance)
                {
                    ValidateSteeringGroupMapping(m_owner.TargetOrbitSteeringGroupIdValue, "近身环绕");
                }
            }

            public void Stop()
            {
                InvalidateNavigationPath();
                m_waitingForAttackFacing = false;
                m_combatWander.Reset();
                m_steeringAdapter?.Stop();
            }

            public void Dispose()
            {
                m_steeringAdapter?.Dispose();
                m_steeringAdapter = null;
            }

            public void TryHandleProvoked(CharacterBase source)
            {
                if (source && !m_owner.m_target && m_owner.m_retargetCooldownTimer == 0.0f && CombatSolver.IsJudiciousTarget(m_owner.m_subject, source))
                {
                    m_owner.m_target = source;
                }
            }

            public void Tick()
            {
                UpdateCooldowns();
                RefreshTarget();
                UpdateTargetPosition();
                ApplyMovement();
            }

            private bool CanSee(CharacterBase other)
            {
                Vector2 targetPosition = other.transform.position;
                Vector2 currentPosition = m_owner.transform.position;
                Vector2 directionToTarget = targetPosition - currentPosition;
                RaycastHit2D hit = Physics2D.Raycast(
                    currentPosition,
                    directionToTarget,
                    Vector2.Distance(currentPosition, targetPosition),
                    GameManager.Config.visibilityContactFilter.layerMask);
                return hit.collider == null;
            }

            private CharacterBase FindTarget()
            {
                foreach (Collider2D collider in m_steeringAdapter.DetectedColliders)
                {
                    CharacterBase potentialTarget = collider != null ? collider.GetComponentInParent<CharacterBase>() : null;
                    if (potentialTarget != null &&
                        CombatSolver.IsHostileTowards(m_owner.m_subject, potentialTarget) &&
                        CanSee(potentialTarget))
                    {
                        return potentialTarget;
                    }
                }

                return null;
            }

            private void UpdateCooldowns()
            {
                if (m_owner.m_retargetCooldownTimer > 0.0f)
                {
                    m_owner.m_retargetCooldownTimer = Math.Max(m_owner.m_retargetCooldownTimer - Time.fixedDeltaTime, 0.0f);
                }

                if (m_owner.m_attackCooldownTimer > 0.0f)
                {
                    m_owner.m_attackCooldownTimer = Math.Max(m_owner.m_attackCooldownTimer - Time.fixedDeltaTime, 0.0f);
                }

                if (m_navigationRepathTimer > 0.0f)
                {
                    m_navigationRepathTimer = Math.Max(m_navigationRepathTimer - Time.fixedDeltaTime, 0.0f);
                }
            }

            private void RefreshTarget()
            {
                // 当前正式真相仍是“控制器自己维护当前追敌目标和冷却”，不额外升成项目级事件或系统。
                if (m_owner.m_target && (m_owner.m_target.dead || !CombatSolver.IsJudiciousTarget(m_owner.m_subject, m_owner.m_target)))
                {
                    m_owner.m_target = null;
                    m_waitingForAttackFacing = false;
                }

                if (!m_owner.m_target)
                {
                    m_waitingForAttackFacing = false;
                    if (m_owner.m_retargetCooldownTimer == 0.0f)
                    {
                        m_owner.m_target = FindTarget();
                    }

                    return;
                }

                float distanceToTarget = Vector2.Distance(m_owner.m_target.transform.position, m_owner.transform.position);
                TryAttackTarget(distanceToTarget);
                CheckIfTargetOutOfRange(distanceToTarget);
            }

            private void TryAttackTarget(float distanceToTarget)
            {
                if (!m_owner.m_target ||
                    !CanSee(m_owner.m_target) ||
                    m_owner.m_attackCooldownTimer != 0.0f ||
                    distanceToTarget >= m_owner.m_attackTriggerRadius)
                {
                    m_waitingForAttackFacing = false;
                    return;
                }

                if (!m_owner.m_subject.TryGetFirstTriggerableFormalGasAbilityCode(out int formalGasAbilityCode))
                {
                    m_waitingForAttackFacing = false;
                    return;
                }

                Vector2 attackDirection = m_owner.m_target.transform.position - m_owner.transform.position;
                if (attackDirection.sqrMagnitude <= 0.0001f)
                {
                    m_waitingForAttackFacing = false;
                    return;
                }

                attackDirection.Normalize();
                m_owner.m_subject.SetTargetDirection(attackDirection);
                if (!PrepareAttackFacing(attackDirection))
                {
                    return;
                }

                m_owner.m_subject.StopFireFormalGasAbility(formalGasAbilityCode);
                EAbilityFireCheckResult fireResult = m_owner.m_subject.FireFormalGasAbility(
                    formalGasAbilityCode,
                    GameCommandContext.AI(m_owner.m_subject));
                if (fireResult == EAbilityFireCheckResult.Valid)
                {
                    m_owner.m_attackCooldownTimer = m_owner.m_attackCooldown;
                }
            }

            private bool PrepareAttackFacing(Vector2 attackDirection)
            {
                if (!m_owner.ShouldRequireTargetFacingBeforeAttack)
                {
                    m_waitingForAttackFacing = false;
                    m_owner.m_subject.SetLookAtDirection(attackDirection);
                    return true;
                }

                if (AIController.IsAttackFacingAligned(
                        m_owner.m_subject.GetLookAtDirection(),
                        attackDirection,
                        m_owner.AttackFacingCompletionAngleDegrees))
                {
                    m_waitingForAttackFacing = false;
                    return true;
                }

                // 对齐 duolafashi TurnTargetNode：未面向目标时只推进身体朝向，本次攻击保持等待。
                m_waitingForAttackFacing = true;
                m_owner.m_subject.SetLookAtDirection(attackDirection);
                return false;
            }

            private void CheckIfTargetOutOfRange(float distanceToTarget)
            {
                float distanceToInitialPosition = Vector2.Distance(m_owner.m_homePosition, m_owner.transform.position);
                bool isTooFarFromInitialPosition = distanceToInitialPosition > m_owner.m_resetFromInitialPositionRadius;
                bool isTooFarFromTarget = distanceToTarget > m_owner.m_resetFromTargetDistanceRadius;

                if (isTooFarFromInitialPosition || isTooFarFromTarget)
                {
                    StopChase(m_owner.m_targetOutOfRangeRetargetCooldown);
                }
            }

            private void StopChase(float retargetCooldown)
            {
                m_owner.m_retargetCooldownTimer = retargetCooldown;
                m_owner.m_target = null;
                m_waitingForAttackFacing = false;
                InvalidateNavigationPath();
            }

            private void UpdateTargetPosition()
            {
                if (m_owner.m_target)
                {
                    // 一旦失去视线，AI 仍先朝最后一次确认的位置追一段时间，而不是立刻原地丢失目标。
                    if (CanSee(m_owner.m_target))
                    {
                        m_targetPosition = (Vector2)m_owner.m_target.transform.position;
                        m_owner.m_timeSinceTargetLastSeen = 0.0f;
                    }
                    else
                    {
                        m_owner.m_timeSinceTargetLastSeen += Time.deltaTime;

                        if (m_owner.m_timeSinceTargetLastSeen > m_owner.m_timeBeforeResetAfterTargetSightLost)
                        {
                            StopChase(m_owner.m_cannotSeeTargetRetargetCooldown);
                        }
                    }
                }
                else
                {
                    m_targetPosition = m_owner.m_homePosition;
                }
            }

            private void ApplyMovement()
            {
                float soughtDistance =
                    m_owner.m_target ?
                    m_owner.m_soughtDistanceFromTarget :
                    m_owner.m_soughtDistanceFromMasterTarget;
                Vector2 currentPosition = m_owner.transform.position;
                float distanceToDestination = Vector2.Distance(currentPosition, m_targetPosition);
                float finalApproachDistance = soughtDistance +
                    ResolveNavigationWaypointTolerance();

                if (CombatWanderRuntime2D.ShouldUse(
                        m_owner.ShouldUseCombatWander,
                        m_owner.m_target,
                        distanceToDestination,
                        m_owner.m_combatWanderRange))
                {
                    SteeringWanderIntent2D wanderIntent = m_combatWander.Tick(
                        Time.fixedDeltaTime,
                        m_owner.m_attackTriggerRadius,
                        m_owner.m_combatWanderRange);
                    Vector2 targetVelocity = Vector2.zero;
                    if (m_owner.m_target.TryGetComponent(out Rigidbody2D wanderTargetBody))
                    {
                        targetVelocity = wanderTargetBody.linearVelocity;
                    }

                    m_steeringAdapter.Submit(
                        true,
                        m_targetPosition,
                        targetVelocity,
                        ResolveSubjectForward(),
                        m_owner.m_combatWanderSteeringGroupId,
                        m_owner.m_detectionRadius,
                        speedMultiplier: m_owner.m_combatWanderSpeedMultiplier,
                        wanderIntent: wanderIntent);
                    m_steeringAdapter.ApplyLatestResult();
                    m_steeringAverageOutput = m_steeringAdapter.LatestResult.SafeDirection;
                    ApplySubjectFacing(m_owner.CombatWanderFacingMode, m_steeringAverageOutput);
                    return;
                }

                if (distanceToDestination <= finalApproachDistance)
                {
                    bool useTargetOrbit =
                        m_owner.m_target &&
                        m_owner.ShouldUseTargetOrbitSteeringAtSoughtDistance;
                    string finalGroupId = useTargetOrbit
                        ? m_owner.TargetOrbitSteeringGroupIdValue
                        : (m_owner.m_target
                            ? m_owner.m_targetPursuitSteeringGroupId
                            : m_owner.m_steeringProfile.DefaultGroupIdValue);
                    Vector2 targetVelocity = Vector2.zero;
                    if (m_owner.m_target &&
                        m_owner.m_target.TryGetComponent(out Rigidbody2D targetBody))
                    {
                        targetVelocity = targetBody.linearVelocity;
                    }

                    m_steeringAdapter.Submit(
                        true,
                        m_targetPosition,
                        targetVelocity,
                        ResolveSubjectForward(),
                        finalGroupId,
                        m_owner.m_detectionRadius,
                        soughtDistance);
                    m_steeringAdapter.ApplyLatestResult();
                    m_steeringAverageOutput = m_steeringAdapter.LatestResult.SafeDirection;
                    ApplySubjectFacing(ResolveActiveFacingMode(), m_steeringAverageOutput);
                    return;
                }

                if (!TryResolveSteeringTarget(
                        currentPosition,
                        m_targetPosition,
                        out Vector2 steeringTarget,
                        out string behaviourGroupId,
                        out bool isFinalTarget))
                {
                    m_steeringAdapter.Submit(
                        false,
                        null,
                        Vector2.zero,
                        ResolveSubjectForward(),
                        semanticQueryRadius: m_owner.m_detectionRadius);
                    m_owner.m_subject.SetSteeringMotion(1.0f, Vector2.zero);
                    m_owner.m_subject.SetSteeringMovementDirection(Vector2.zero);
                    return;
                }

                Vector2 forward = m_owner.m_subject.IsMoving()
                    ? m_steeringAverageOutput
                    : (steeringTarget - currentPosition);
                if (forward.sqrMagnitude <= 0.0001f)
                {
                    forward = ResolveSubjectForward();
                }

                Vector2 resolvedTargetVelocity = Vector2.zero;
                if (isFinalTarget &&
                    m_owner.m_target &&
                    m_owner.m_target.TryGetComponent(out Rigidbody2D resolvedTargetBody))
                {
                    resolvedTargetVelocity = resolvedTargetBody.linearVelocity;
                }

                m_steeringAdapter.Submit(
                    true,
                    steeringTarget,
                    resolvedTargetVelocity,
                    forward,
                    behaviourGroupId,
                    m_owner.m_detectionRadius,
                    isFinalTarget ? soughtDistance : -1.0f);
                m_steeringAdapter.ApplyLatestResult();
                m_steeringAverageOutput = m_steeringAdapter.LatestResult.SafeDirection;
                ApplySubjectFacing(ResolveActiveFacingMode(), m_steeringAverageOutput);
            }

            private AICharacterFacingMode2D ResolveActiveFacingMode()
            {
                if (m_waitingForAttackFacing)
                {
                    return AICharacterFacingMode2D.FaceTarget;
                }

                return m_owner.m_target
                    ? m_owner.TargetPursuitFacingMode
                    : AICharacterFacingMode2D.FaceMovement;
            }

            private Vector2 ResolveSubjectForward()
            {
                Vector2 lookAtDirection = m_owner.m_subject.GetLookAtDirection();
                if (lookAtDirection != Vector2.zero)
                {
                    return lookAtDirection.normalized;
                }

                return m_owner.m_subject.transform.right;
            }

            private void ApplySubjectFacing(AICharacterFacingMode2D facingMode, Vector2 movementDirection)
            {
                if (!m_owner.m_subject.CanMove())
                {
                    return;
                }

                switch (facingMode)
                {
                    case AICharacterFacingMode2D.FaceTarget:
                        if (m_owner.m_target)
                        {
                            m_owner.m_subject.LookAtTarget(m_targetPosition);
                        }
                        break;
                    case AICharacterFacingMode2D.FaceMovement:
                        if (movementDirection != Vector2.zero)
                        {
                            m_owner.m_subject.SetLookAtDirection(movementDirection.normalized);
                        }
                        break;
                    case AICharacterFacingMode2D.KeepCurrent:
                    default:
                        break;
                }
            }

            private bool TryResolveSteeringTarget(
                Vector2 currentPosition,
                Vector2 finalDestination,
                out Vector2 steeringTarget,
                out string behaviourGroupId,
                out bool isFinalTarget)
            {
                steeringTarget = finalDestination;
                behaviourGroupId = m_owner.m_steeringProfile.DefaultGroupIdValue;
                isFinalTarget = true;

                if (!GameManager.Exists() ||
                    !GameManager.TryGetSystem(out MapSystem mapSystem) ||
                    !mapSystem.TryGetActiveTerrainNavigationMap(out TerrainNavigationMap navigationMap))
                {
                    InvalidateNavigationPath();
                    return true;
                }

                float targetMoveThreshold = Mathf.Max(m_owner.m_navigationTargetMoveThreshold, 0.05f);
                bool destinationMoved = m_pathCursor.HasDestinationMoved(
                    finalDestination,
                    targetMoveThreshold);
                if (!m_pathCursor.HasPath ||
                    destinationMoved ||
                    m_navigationRepathTimer <= 0.0f)
                {
                    m_navigationRepathTimer = Mathf.Max(m_owner.m_navigationRepathInterval, 0.1f);
                    if (!navigationMap.TryBuildWorldPathWithoutDebug(
                            currentPosition,
                            finalDestination,
                            out Vector2[] navigationPath))
                    {
                        m_pathCursor.Clear();
                        return false;
                    }

                    m_pathCursor.SetPath(navigationPath, finalDestination);
                }

                if (!m_pathCursor.TryGetTarget(
                        currentPosition,
                        ResolveNavigationWaypointTolerance(),
                        out steeringTarget,
                        out isFinalTarget))
                {
                    return false;
                }

                behaviourGroupId = isFinalTarget
                    ? (m_owner.m_target
                        ? m_owner.m_targetPursuitSteeringGroupId
                        : m_owner.m_steeringProfile.DefaultGroupIdValue)
                    : m_owner.m_transitSteeringGroupId;
                return true;
            }

            private float ResolveNavigationWaypointTolerance()
            {
                float profileAgentRadius = m_owner.m_steeringProfile != null
                    ? m_owner.m_steeringProfile.AgentRadius
                    : 0.0f;
                return Mathf.Max(
                    m_owner.m_navigationWaypointTolerance,
                    profileAgentRadius,
                    MinimumNavigationWaypointTolerance);
            }

            private void ValidateSteeringGroupMapping(string groupId, string usage)
            {
                if (string.IsNullOrWhiteSpace(groupId))
                {
                    throw new InvalidOperationException($"AIController 的{usage}未配置 ContextSteering 行为组 ID。");
                }

                try
                {
                    m_owner.m_steeringProfile.GetBehaviourGroup(groupId);
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidOperationException(
                        $"AIController 的{usage}配置为行为组 '{groupId}'，但 Profile '{m_owner.m_steeringProfile.name}' 中不存在该组。",
                        exception);
                }
            }

            private void InvalidateNavigationPath()
            {
                m_pathCursor.Clear();
                m_navigationRepathTimer = 0.0f;
            }
        }
    }
}
