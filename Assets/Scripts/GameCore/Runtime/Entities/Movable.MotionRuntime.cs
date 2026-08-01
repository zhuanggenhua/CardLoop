using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public abstract partial class Movable
    {
        /// <summary>
        /// `Movable` 的内部动作执行模块。
        /// 这里只收口碰撞探测、MoveOrder、输入平滑和推力执行，不替代 `Movable` 自己的生命周期、朝向语义或受击规则。
        /// </summary>
        private sealed class MotionRuntime
        {
            private readonly Movable m_owner;
            private readonly List<RaycastHit2D> m_castCollisions = new();
            private readonly HashSet<GameObject> m_castCollisionSet = new();
            private readonly Stack<float> m_contextSpeedMultipliers = new();

            private Vector2 m_smoothedMovementInput = Vector2.zero;
            private float m_accelerationAmount = 0.0f;
            private Vector2 m_lastSuccessfulMovement = Vector2.zero;
            private float m_steeringSpeedScale = 1.0f;
            private Vector2 m_steeringCorrection = Vector2.zero;
            private bool m_movementIntentUpdatesFacing = true;
            private MoveOrder? m_moveOrder = null;
            private PushOrder? m_pushOrder = null;

            public MotionRuntime(Movable owner)
            {
                m_owner = owner;
            }

            public bool IsValidSpawnPoint(Vector3 position)
            {
                return IsValidPosition(position);
            }

            public Vector3 NearestValidDestination(Vector3 destination)
            {
                Vector2 targetPosition = destination;
                if (IsValidPosition(targetPosition))
                {
                    return destination;
                }

                float maxDistance = GameManager.Config != null ? GameManager.Config.maxTeleportDistanceWhenStuckInWall : 0.0f;
                Vector2 resolvedPosition = TryFindValidPosition(targetPosition, maxDistance, 16) ?? targetPosition;
                return new Vector3(resolvedPosition.x, resolvedPosition.y, destination.z);
            }

            public void TeleportTo(Vector3 position)
            {
                ResetMovement();
                Vector3 resolvedPosition = NearestValidDestination(position);

                if (m_owner.m_rigidbody != null)
                {
                    m_owner.m_rigidbody.position = resolvedPosition;
                }

                m_owner.transform.position = resolvedPosition;
                m_owner.m_teleported.Invoke();
            }

            public float GetContextSpeedMultiplier()
            {
                return m_contextSpeedMultipliers.Count > 0 ? m_contextSpeedMultipliers.Peek() : 1.0f;
            }

            public void SetContextSpeedMultiplier(float multiplier)
            {
                m_contextSpeedMultipliers.Push(Mathf.Max(0.0f, multiplier));
            }

            public void ResetContextSpeedMultiplier()
            {
                if (m_contextSpeedMultipliers.Count > 0)
                {
                    m_contextSpeedMultipliers.Pop();
                }
            }

            public void ClearContextSpeedMultipliers()
            {
                m_contextSpeedMultipliers.Clear();
            }

            public void ResetMovement()
            {
                m_owner.m_movementDirection = Vector2.zero;
                m_movementIntentUpdatesFacing = true;
                m_smoothedMovementInput = Vector2.zero;
                m_accelerationAmount = 0.0f;

                if (m_moveOrder.HasValue)
                {
                    m_moveOrder.Value.task?.TrySetResult(false);
                    m_moveOrder = null;
                }

                if (m_owner.m_rigidbody != null)
                {
                    m_owner.m_rigidbody.linearVelocity = Vector2.zero;
                }

                m_owner.UpdateMovementAnimation(Vector2.zero);
            }

            public bool HasMoveOrder()
            {
                return m_moveOrder.HasValue;
            }

            public bool HasActiveMovementIntent()
            {
                float idleThresholdSquared = m_owner.m_idleThreshold * m_owner.m_idleThreshold;
                return m_owner.m_movementDirection.sqrMagnitude > idleThresholdSquared ||
                    m_moveOrder.HasValue ||
                    m_pushOrder.HasValue ||
                    m_lastSuccessfulMovement.sqrMagnitude > 0.0001f;
            }

            public void HandleWallCollision()
            {
                if (!IsInWall())
                {
                    return;
                }

                float maxTeleportDistance = GameManager.Config.maxTeleportDistanceWhenStuckInWall;
                Vector2? nearestValidPosition = FindNearestValidPosition(maxTeleportDistance);
                if (nearestValidPosition.HasValue)
                {
                    m_owner.m_rigidbody.position = nearestValidPosition.Value;
                    Debug.Log($"Movable '{m_owner.gameObject.name}' moved from inside a collider to a valid position within {maxTeleportDistance} units.");
                    return;
                }

                Debug.LogWarning($"Movable '{m_owner.gameObject.name}' is stuck and could not find a valid position within {maxTeleportDistance} units.");
                m_owner.OnStuckInAWall();
            }

            public void SetMovementDirection(Vector2 direction, bool updateFacingFromIntent)
            {
                // 吸收 uMMORPG PlayerNavMeshMovement.MoveWASD() 的优先级规则：
                // 一旦开始直接方向驱动，就立刻取消现有导航/MoveOrder，避免继续沿旧路径滑行。
                if (direction != Vector2.zero && m_moveOrder.HasValue)
                {
                    CompleteMoveOrder(false);
                }

                m_owner.m_movementDirection = direction;
                m_movementIntentUpdatesFacing = updateFacingFromIntent;
                if (updateFacingFromIntent)
                {
                    UpdateFacingFromMovementIntent(direction);
                }
            }

            public void SetSteeringMotion(float speedScale, Vector2 correctionDisplacement)
            {
                m_steeringSpeedScale = Mathf.Clamp01(speedScale);
                m_steeringCorrection = correctionDisplacement;
            }

            public void HandleMovement()
            {
                try
                {
                    if (m_moveOrder.HasValue)
                    {
                        ExecuteMoveOrder();
                    }
                    else
                    {
                        MoveInDirection(
                            m_owner.m_movementDirection,
                            m_owner.CalculateMoveSpeed(),
                            false,
                            true,
                            m_movementIntentUpdatesFacing);
                    }
                }
                finally
                {
                    m_steeringSpeedScale = 1.0f;
                    m_steeringCorrection = Vector2.zero;
                }
            }

            public TaskCompletionSource<bool> MoveTo(Vector2 destination, float? speedOverride = null)
            {
                return MoveTo(destination, Constants.AcceptableDistanceFromTarget, speedOverride);
            }

            public TaskCompletionSource<bool> MoveTo(Vector2 destination, float stoppingDistance, float? speedOverride = null)
            {
                return StartMoveOrder(
                    new[] { destination },
                    stoppingDistance,
                    speedOverride);
            }

            public TaskCompletionSource<bool> MoveAlongPath(
                IReadOnlyList<Vector2> waypoints,
                float stoppingDistance,
                float? speedOverride = null)
            {
                if (waypoints == null || waypoints.Count == 0)
                {
                    TaskCompletionSource<bool> failedTask = new();
                    failedTask.SetResult(false);
                    return failedTask;
                }

                Vector2[] route = new Vector2[waypoints.Count];
                for (int i = 0; i < waypoints.Count; i++)
                {
                    route[i] = waypoints[i];
                }

                return StartMoveOrder(route, stoppingDistance, speedOverride);
            }

            public bool IsMovingUp()
            {
                return m_owner.m_movementDirection.y > 0.0f || (IsPushed() && m_pushOrder.Value.direction.y > 0.0f);
            }

            public bool IsMovingDown()
            {
                return m_owner.m_movementDirection.y < 0.0f || (IsPushed() && m_pushOrder.Value.direction.y < 0.0f);
            }

            public bool IsMovingLeft()
            {
                return m_owner.m_movementDirection.x < 0.0f || (IsPushed() && m_pushOrder.Value.direction.x < 0.0f);
            }

            public bool IsMovingRight()
            {
                return m_owner.m_movementDirection.x > 0.0f || (IsPushed() && m_pushOrder.Value.direction.x > 0.0f);
            }

            public bool IsMoving()
            {
                return m_lastSuccessfulMovement.magnitude > 0.0f;
            }

            public void HandlePush()
            {
                if (m_pushOrder.HasValue)
                {
                    ExecutePushOrder();
                }
            }

            public bool IsPushed()
            {
                return m_pushOrder.HasValue;
            }

            public void InterruptPush()
            {
                m_pushOrder = null;
            }

            public void Push(Vector2 direction, float intensity, float resistance)
            {
                if (!m_owner.IsPushable())
                {
                    return;
                }

                m_owner.m_rigidbody.linearVelocity = Vector2.zero;
                m_pushOrder = new PushOrder
                {
                    direction = direction,
                    intensity = intensity * m_owner.m_pushIntensityScale,
                    resistance = resistance * m_owner.m_pushResistanceScale,
                    collisionSet = new HashSet<GameObject>()
                };
            }

            private bool IsInWall(Vector2? direction = null, float speed = 0.0f, float deltaTime = 0.0f)
            {
                int collisions = m_owner.m_rigidbody.Cast(
                    direction ?? Vector2.zero,
                    GameManager.Config.collisionContactFilter,
                    m_castCollisions,
                    speed * deltaTime + Constants.CollisionOffset
                );

                foreach (RaycastHit2D hit in m_castCollisions)
                {
                    m_castCollisionSet.Add(hit.collider.gameObject);
                }

                return collisions > 0;
            }

            private bool IsMovementValid(Vector2 direction, float speed, float deltaTime)
            {
                return !IsInWall(direction, speed, deltaTime);
            }

            private Vector2? FindNearestValidPosition(float maxDistance, int attempts = 16)
            {
                Debug.Assert(IsInWall(), "Movable entity not in a wall, FindNearestValidPosition() shouldn't be called.");

                if (maxDistance == 0.0f)
                {
                    return null;
                }

                return TryFindValidPosition(m_owner.m_rigidbody.position, maxDistance, attempts);
            }

            private Vector2? TryFindValidPosition(Vector2 currentPosition, float maxDistance, int attempts)
            {
                Vector2[] directions =
                {
                    Vector2.up,
                    Vector2.right,
                    Vector2.down,
                    Vector2.left,
                    new Vector2(1, 1).normalized,
                    new Vector2(1, -1).normalized,
                    new Vector2(-1, -1).normalized,
                    new Vector2(-1, 1).normalized
                };

                for (int i = 1; i <= attempts; i++)
                {
                    float distance = i / (float)attempts * maxDistance;

                    foreach (Vector2 direction in directions)
                    {
                        Vector2 testPosition = currentPosition + direction * distance;
                        if (IsValidPosition(testPosition))
                        {
                            return testPosition;
                        }
                    }
                }

                return null;
            }

            private bool IsValidPosition(Vector2 testPosition)
            {
                Vector2 originalPosition = m_owner.m_rigidbody.position;
                m_owner.m_rigidbody.position = testPosition;
                bool isValid = !IsInWall();
                m_owner.m_rigidbody.position = originalPosition;
                return isValid;
            }

            private void ExecuteMoveOrder()
            {
                float stoppingDistance = math.max(Constants.AcceptableDistanceFromTarget, m_moveOrder.Value.stoppingDistance);
                Vector2 currentPosition = m_owner.m_rigidbody.position;
                Vector2 targetDelta = m_moveOrder.Value.targetPosition - currentPosition;

                if (targetDelta.magnitude <= stoppingDistance)
                {
                    CompleteCurrentWaypoint();
                    return;
                }

                Vector2 direction = targetDelta.normalized;
                float speed = m_moveOrder.Value.speedOverride ?? m_owner.CalculateMoveSpeed();
                if (!MoveInDirection(direction, speed, false, false, true))
                {
                    CompleteMoveOrder(false);
                    return;
                }

                if (Vector2.Distance(m_owner.m_rigidbody.position, m_moveOrder.Value.targetPosition) <= stoppingDistance)
                {
                    CompleteCurrentWaypoint();
                }
            }

            private TaskCompletionSource<bool> StartMoveOrder(
                Vector2[] waypoints,
                float stoppingDistance,
                float? speedOverride)
            {
                if (m_moveOrder.HasValue)
                {
                    m_moveOrder.Value.task?.TrySetCanceled();
                }

                TaskCompletionSource<bool> task = new();
                m_moveOrder = new MoveOrder
                {
                    waypoints = waypoints,
                    waypointIndex = 0,
                    targetPosition = waypoints[0],
                    stoppingDistance = math.max(0.0f, stoppingDistance),
                    speedOverride = speedOverride,
                    task = task
                };

                return task;
            }

            private void CompleteCurrentWaypoint()
            {
                MoveOrder order = m_moveOrder.Value;
                int nextWaypointIndex = order.waypointIndex + 1;
                if (order.waypoints != null && nextWaypointIndex < order.waypoints.Length)
                {
                    order.waypointIndex = nextWaypointIndex;
                    order.targetPosition = order.waypoints[nextWaypointIndex];
                    m_moveOrder = order;
                    return;
                }

                CompleteMoveOrder(true);
            }

            private void CompleteMoveOrder(bool success)
            {
                if (!m_moveOrder.HasValue)
                {
                    return;
                }

                TaskCompletionSource<bool> task = m_moveOrder.Value.task;
                m_moveOrder = null;
                task?.TrySetResult(success);
                m_owner.UpdateMovementAnimation(Vector2.zero);
            }

            private bool MoveInDirection(
                Vector2 direction,
                float moveSpeed,
                bool force = false,
                bool applyInputHandling = false,
                bool updateFacingFromIntent = false)
            {
                BeginMove();

                bool canApplyMovement = force || m_owner.CanMove();
                if (canApplyMovement && updateFacingFromIntent)
                {
                    UpdateFacingFromMovementIntent(direction);
                }

                Vector2 resolvedDirection = applyInputHandling ? ResolveMovementInput(direction) : direction.normalized;

                if (canApplyMovement && moveSpeed > 0.0f && (resolvedDirection.sqrMagnitude > 0.0f || m_steeringCorrection.sqrMagnitude > 0.0f))
                {
                    float resolvedSpeed = moveSpeed * m_owner.CalculateMovementSpeedMultiplier() * m_steeringSpeedScale;
                    Vector2 targetVelocity = resolvedDirection * resolvedSpeed;
                    if (Time.fixedDeltaTime > 0.0f && m_steeringCorrection.sqrMagnitude > 0.0f)
                    {
                        targetVelocity += m_steeringCorrection / Time.fixedDeltaTime;
                    }

                    resolvedDirection = targetVelocity.normalized;
                    resolvedSpeed = targetVelocity.magnitude;

                    if (!TryMove(resolvedDirection, resolvedSpeed))
                    {
                        if (!TryMove(new Vector2(resolvedDirection.x, 0.0f), resolvedSpeed))
                        {
                            TryMove(new Vector2(0.0f, resolvedDirection.y), resolvedSpeed);
                        }
                    }
                }

                EndMove();
                m_owner.UpdateMovementAnimation(m_lastSuccessfulMovement);

                // MoveOrder 只应在完全走不动时失败；先撞到完整方向、再沿单轴滑动成功仍然是有效移动。
                return !canApplyMovement ||
                    moveSpeed <= 0.0f ||
                    resolvedDirection.magnitude <= 0.0f ||
                    m_lastSuccessfulMovement.sqrMagnitude > 0.0f;
            }

            private Vector2 ResolveMovementInput(Vector2 rawInput)
            {
                Vector2 directionalInput = ApplyMovementInputMode(rawInput);
                directionalInput = ResolveTerrainMovementInput(directionalInput);
                Vector2 normalizedInput = directionalInput.normalized;
                Vector2 targetInput = m_owner.m_useAnalogMovementInput
                    ? Vector2.ClampMagnitude(directionalInput, 1.0f)
                    : normalizedInput;

                if (targetInput.magnitude <= m_owner.m_idleThreshold)
                {
                    targetInput = Vector2.zero;
                }

                if (m_owner.m_acceleration <= 0.0f || m_owner.m_deceleration <= 0.0f)
                {
                    m_smoothedMovementInput = targetInput;
                    m_accelerationAmount = targetInput == Vector2.zero ? 0.0f : 1.0f;
                    return m_smoothedMovementInput;
                }

                if (targetInput == Vector2.zero)
                {
                    m_accelerationAmount = Mathf.Lerp(m_accelerationAmount, 0.0f, m_owner.m_deceleration * Time.fixedDeltaTime);
                    m_smoothedMovementInput = Vector2.Lerp(
                        m_smoothedMovementInput,
                        m_smoothedMovementInput * m_accelerationAmount,
                        m_owner.m_deceleration * Time.fixedDeltaTime);
                }
                else
                {
                    m_accelerationAmount = Mathf.Lerp(m_accelerationAmount, 1.0f, m_owner.m_acceleration * Time.fixedDeltaTime);
                    m_smoothedMovementInput = Vector2.ClampMagnitude(targetInput, m_accelerationAmount);
                }

                if (m_smoothedMovementInput.magnitude <= m_owner.m_idleThreshold)
                {
                    m_smoothedMovementInput = Vector2.zero;
                }

                return m_smoothedMovementInput;
            }

            private void UpdateFacingFromMovementIntent(Vector2 direction)
            {
                float idleThresholdSquared = m_owner.m_idleThreshold * m_owner.m_idleThreshold;
                if (m_owner.m_lookAtDirectionUpdateStrategy != ELookAtDirectionUpdateStrategy.MovementBased ||
                    direction.sqrMagnitude <= idleThresholdSquared)
                {
                    return;
                }

                m_owner.SetLookAtDirection(direction.normalized);
            }

            private Vector2 ResolveTerrainMovementInput(Vector2 input)
            {
                if (input.sqrMagnitude <= 0.000001f ||
                    !GameManager.Exists() ||
                    !GameManager.TryGetSystem(out MapSystem mapSystem) ||
                    !mapSystem.TryGetActiveTerrainNavigationMap(out TerrainNavigationMap navigationMap))
                {
                    return input;
                }

                if (!navigationMap.TryResolveRampMovementDirection(
                        m_owner.m_rigidbody.position,
                        input,
                        out Vector2 resolvedDirection))
                {
                    return input;
                }

                return resolvedDirection * input.magnitude;
            }

            private Vector2 ApplyMovementInputMode(Vector2 input)
            {
                switch (m_owner.m_movementInputMode)
                {
                    case EMovementInputMode.Strict2DirectionsHorizontal:
                        input.y = 0.0f;
                        break;
                    case EMovementInputMode.Strict2DirectionsVertical:
                        input.x = 0.0f;
                        break;
                    case EMovementInputMode.Strict4Directions:
                        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                        {
                            input.y = 0.0f;
                        }
                        else
                        {
                            input.x = 0.0f;
                        }

                        break;
                    case EMovementInputMode.Strict8Directions:
                        input.x = Mathf.Round(input.x);
                        input.y = Mathf.Round(input.y);
                        break;
                }

                return input;
            }

            private void BeginMove()
            {
                m_lastSuccessfulMovement = Vector2.zero;
                m_castCollisionSet.Clear();
            }

            private void EndMove(bool sendCollisionNotifications = true)
            {
                if (!sendCollisionNotifications || m_castCollisionSet.Count <= 0)
                {
                    return;
                }

                foreach (GameObject gameObject in m_castCollisionSet)
                {
                    CollisionDispatcher.RegisterCollision(m_owner, gameObject);
                }
            }

            private bool TryMove(Vector2 direction, float speed)
            {
                if (direction.magnitude <= 0.0f)
                {
                    return false;
                }

                if (!IsMovementValid(direction, speed, Time.fixedDeltaTime))
                {
                    return false;
                }

                m_lastSuccessfulMovement = direction * speed;
                m_owner.m_rigidbody.MovePosition(m_owner.m_rigidbody.position + direction * speed * Time.fixedDeltaTime);
                return true;
            }

            private void ExecutePushOrder()
            {
                BeginMove();

                PushOrder pushOrder = m_pushOrder.Value;
                if (pushOrder.intensity > 0.2f)
                {
                    bool movementSuccess = TryMove(pushOrder.direction, pushOrder.intensity);
                    pushOrder.intensity = math.lerp(pushOrder.intensity, 0.0f, Time.fixedDeltaTime * pushOrder.resistance);
                    m_pushOrder = pushOrder;

                    if (!movementSuccess)
                    {
                        foreach (GameObject gameObject in m_castCollisionSet)
                        {
                            if (m_pushOrder.Value.AddCollision(gameObject))
                            {
                                CollisionDispatcher.RegisterCollision(m_owner, gameObject);
                            }
                        }
                    }
                }
                else
                {
                    m_pushOrder = null;
                }

                EndMove(false);
            }

            private struct MoveOrder
            {
                public Vector2[] waypoints;
                public int waypointIndex;
                public Vector2 targetPosition;
                public float stoppingDistance;
                public float? speedOverride;
                public TaskCompletionSource<bool> task;
            }

            private struct PushOrder
            {
                public Vector2 direction;
                public float intensity;
                public float resistance;
                public HashSet<GameObject> collisionSet;

                public bool AddCollision(GameObject gameObject)
                {
                    return collisionSet.Add(gameObject);
                }
            }
        }
    }
}
