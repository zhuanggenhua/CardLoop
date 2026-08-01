using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using MackySoft.SerializeReferenceExtensions;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 基础四方向枚举。
    /// 主要用于旧配置和简单朝向判断，正式移动仍使用 Vector2。
    /// </summary>
    public enum EDirection
    {
        Left,
        Right,
        Up,
        Down,
        None,
        Default = Right
    }

    /// <summary>
    /// 角色朝向更新策略。
    /// MovementBased 跟随移动意图，TargetBased 跟随技能或交互目标方向。
    /// </summary>
    public enum ELookAtDirectionUpdateStrategy
    {
        MovementBased,
        TargetBased
    }

    /// <summary>
    /// 移动输入方向限制模式。
    /// 吸收 TopDownEngine 的方向模式语义，但不接入它的输入管理器。
    /// </summary>
    public enum EMovementInputMode
    {
        Free,
        Strict2DirectionsHorizontal,
        Strict2DirectionsVertical,
        Strict4Directions,
        Strict8Directions
    }

    /// <summary>
    /// 可移动实体的存档数据块。
    /// 只保存朝向和控制器状态；物理速度和临时移动命令不进入存档。
    /// </summary>
    [Serializable]
    public class MovableDataBlock : EntityDataBlock
    {
        public Vector2 lookAtDirection;
        [SerializeReference, SubclassSelector] public IControllerDataBlock controllerData;
    }

    /// <summary>
    /// 可移动实体基类。
    /// 它统一控制器生命周期、Rigidbody2D 移动、朝向、推力和动画同步，不承担角色属性或战斗结算。
    /// </summary>
    public abstract partial class Movable : Entity, ICharacterAnimationStateReceiver
    {
        [Header("引用")]
        [InspectorName("刚体")]
        [Tooltip("正式 2D 移动和碰撞检测使用的 Rigidbody2D。不能为空。")]
        [SerializeField] private Rigidbody2D m_rigidbody = null;

        [Header("移动设置")]
        [InspectorName("基础移动速度")]
        [Tooltip("动作执行层的基础移动速度；角色属性倍率会在 CharacterBase 中叠加。")]
        [SerializeField] private float m_moveSpeed = 4.0f;
        [InspectorName("输入方向模式")]
        [Tooltip("移动输入方向限制。吸收 TopDown CharacterMovement 的方向模式，但不接入它的 InputManager。")]
        [SerializeField] private EMovementInputMode m_movementInputMode = EMovementInputMode.Free;
        [InspectorName("使用模拟输入强度")]
        [Tooltip("启用后保留摇杆输入强度；关闭后只使用归一化方向，键盘和摇杆速度一致。")]
        [SerializeField] private bool m_useAnalogMovementInput = false;
        [InspectorName("加速度")]
        [Tooltip("输入从静止过渡到目标速度的响应速度。0 表示立即达到目标速度。")]
        [SerializeField] private float m_acceleration = 10.0f;
        [InspectorName("减速度")]
        [Tooltip("输入松开后回到静止的响应速度。0 表示立即停止。")]
        [SerializeField] private float m_deceleration = 10.0f;
        [InspectorName("静止阈值")]
        [Tooltip("低于该强度时视为静止，避免摇杆漂移或浮点误差让角色抖动。")]
        [SerializeField] private float m_idleThreshold = 0.05f;
        [InspectorName("移动速度倍率")]
        [Tooltip("动作执行层的常规速度倍率；RPG 属性、装备和状态修正仍在 CharacterBase 中处理。")]
        [SerializeField] private float m_movementSpeedMultiplier = 1.0f;
        [InspectorName("移动速度倍率上限")]
        [Tooltip("限制动作执行层速度倍率上限，避免区域、Buff 和临时效果叠乘后失控。")]
        [SerializeField] private float m_movementSpeedMaxMultiplier = float.MaxValue;
        [InspectorName("禁止普通移动")]
        [Tooltip("禁用玩家输入和 AI 驱动的普通移动；强制位移和推力仍按各自规则处理。")]
        [SerializeField] private bool m_movementForbidden = false;
        [InspectorName("推力强度倍率")]
        [Tooltip("受击或技能推力的强度倍率。0 表示不可被推动。")]
        [SerializeField] private float m_pushIntensityScale = 1.0f;
        [InspectorName("推力阻力倍率")]
        [Tooltip("推力衰减速度倍率，越高越快停下。")]
        [SerializeField] private float m_pushResistanceScale = 1.0f;
        [InspectorName("禁用所有移动")]
        [Tooltip("完全关闭移动、卡墙修正和推力处理，通常只用于特殊静态对象。")]
        [SerializeField] private bool m_disableAllMovements = false;
        [InspectorName("死亡期间可移动")]
        [Tooltip("允许死亡状态继续执行普通移动。默认关闭。")]
        [SerializeField] private bool m_canMoveDuringDeath = false;
        [InspectorName("朝向更新策略")]
        [Tooltip("决定朝向跟随移动意图还是技能/交互目标方向。")]
        [SerializeField] protected ELookAtDirectionUpdateStrategy m_lookAtDirectionUpdateStrategy = ELookAtDirectionUpdateStrategy.MovementBased;

        [Header("控制器设置")]
        [InspectorName("默认控制器")]
        [Tooltip("玩家、AI 或脚本控制器。运行时可临时切换 active controller，但默认控制器仍是生命周期入口。")]
        [SerializeReference, SubclassSelector] protected IController m_controller = null;

        [Header("动画设置")]
        [InspectorName("动画策略")]
        [Tooltip("把移动、朝向、受击和死亡状态转成具体动画表现。")]
        [SerializeReference, SubclassSelector] protected IAnimationStrategy m_animationStrategy = null;

        protected bool m_destroyOnDeath = true;
        protected Vector2 m_lookAtDirection = Vector2.zero;
        private UnityEvent<Vector2> m_lookAtDirectionChangedEvent = new();
        private UnityEvent<Vector2> m_targetDirectionChangedEvent = new();
        private Vector2? m_targetDirectionOverride = null;
        private Vector2 m_movementDirection;
        private UnityEvent m_teleported = new();
        private MotionRuntime m_motionRuntime = null;
        private IController m_activeControllerOverride = null;
        private MotionRuntime motionRuntime => m_motionRuntime ??= new MotionRuntime(this);
        private IController activeController => m_activeControllerOverride ?? m_controller;

        protected override void Awake()
        {
            base.Awake();

            Debug.Assert(m_rigidbody, ErrorMessages.InspectorMissingComponentReference<Rigidbody2D>());

            m_animationStrategy?.AddDeathAnimationEndedListener(OnDeathAnimationEnd);

            _ = motionRuntime;
            m_animationStrategy?.Initialize();
            InitializeControllers();
        }

        public void AddTeleportedListener(UnityAction listener)
        {
            m_teleported.AddListener(listener);
        }

        public void RemoveTeleportedListener(UnityAction listener)
        {
            m_teleported.RemoveListener(listener);
        }

        public void AddTargetDirectionChangedListener(UnityAction<Vector2> listener)
        {
            m_targetDirectionChangedEvent.AddListener(listener);
        }

        public void RemoveTargetDirectionChangedListener(UnityAction<Vector2> listener)
        {
            m_targetDirectionChangedEvent.RemoveListener(listener);
        }

        /// <summary>
        /// 监听实体实际面朝方向变化；用于身体和换装表现，不等同于技能或交互目标方向。
        /// </summary>
        public void AddLookAtDirectionChangedListener(UnityAction<Vector2> listener)
        {
            m_lookAtDirectionChangedEvent.AddListener(listener);
        }

        /// <summary>
        /// 取消监听实体实际面朝方向变化。
        /// </summary>
        public void RemoveLookAtDirectionChangedListener(UnityAction<Vector2> listener)
        {
            m_lookAtDirectionChangedEvent.RemoveListener(listener);
        }

        public bool TryGetController<TController>(out TController controller) where TController : class
        {
            if (m_controller is TController typedController)
            {
                controller = typedController;
                return true;
            }

            controller = null;
            return false;
        }

        public bool IsControllerActive<TController>() where TController : class
        {
            return activeController is TController;
        }

        protected bool TryActivateController<TController>() where TController : class, IController
        {
            if (!TryGetController(out TController controller))
            {
                return false;
            }

            SetActiveControllerOverride(controller);
            return true;
        }

        protected bool ClearControllerOverride<TController>() where TController : class, IController
        {
            if (m_activeControllerOverride is not TController)
            {
                return false;
            }

            SetActiveControllerOverride(null);
            return true;
        }

        public void StartController()
        {
            StartActiveController();
        }

        public void StopController()
        {
            StopActiveController();
        }

        public virtual void Revive()
        {
            StartActiveController();
            MarkAsNotDestroyed();
        }

        public void OnInvincibleAnimationStart()
        {
            m_animationStrategy?.OnInvincibleAnimationStart();
        }

        public void OnInvincibleAnimationStop()
        {
            m_animationStrategy?.OnInvincibleAnimationStop();
        }

        public void OnDeathAnimationStart()
        {
            m_animationStrategy?.OnDeathAnimationStart();
        }

        public void OnDeathAnimationStop()
        {
            m_animationStrategy?.OnDeathAnimationStop();
        }

        protected virtual AudioClipResolver GetDeathAudio() => null;

        public virtual void Kill()
        {
            StopActiveController();
            MarkAsDestroyed();
            PlayDeathAudio();
            ResetMovement();

            if (!TryPlayDeathAnimation())
            {
                OnDeath();
            }
        }

        protected virtual bool TryPlayDeathAnimation()
        {
            return m_animationStrategy?.PlayDeathAnimation() ?? false;
        }

        /// <summary>
        /// 检查一个出生点/传送点对当前 2D 碰撞闭包是否有效。
        /// 吸收 uMMORPG `Movement.IsValidSpawnPoint(...)` 的职责，但继续沿用当前 Rigidbody2D + ContactFilter2D 真相源。
        /// </summary>
        public bool IsValidSpawnPoint(Vector3 position)
        {
            return motionRuntime.IsValidSpawnPoint(position);
        }

        /// <summary>
        /// 返回距离目标点最近、且对当前移动闭包有效的位置。
        /// 吸收 uMMORPG `Movement.NearestValidDestination(...)` 的职责，用于传送、出生点和后续点击移动目标修正。
        /// </summary>
        public Vector3 NearestValidDestination(Vector3 destination)
        {
            return motionRuntime.NearestValidDestination(destination);
        }

        public void TeleportTo(Vector3 position)
        {
            motionRuntime.TeleportTo(position);
        }

        private void PlayDeathAudio()
        {
            AudioClipResolver deathAudio = GetDeathAudio();

            if (deathAudio)
            {
                GameRuntimeEvents.RequestAudioPlayback(deathAudio);
            }
        }

        protected virtual void OnDeath()
        {
            if (m_destroyOnDeath)
            {
                Destroy(ResolveDeathCommandContext());
            }
        }

        protected virtual GameCommandContext ResolveDeathCommandContext()
        {
            return GameCommandContext.Script();
        }

        protected override void OnDestroy()
        {
            TerminateControllers();
            base.OnDestroy();
        }

        protected virtual void OnDeathAnimationEnd() => OnDeath();

        protected virtual void OnStuckInAWall() { }

        protected virtual void OnEnable() => StartActiveController();

        /// <summary>
        /// 默认只负责停掉控制器。
        /// 角色等子类若还持有临时运行时状态，应在 override 中先完成自己的收尾，再回到这里停控制器。
        /// </summary>
        protected virtual void OnDisable() => StopActiveController();
        private void OnDrawGizmos() => activeController?.DrawGizmos();
        protected virtual void Update() => activeController?.Update();
        protected virtual float CalculateMoveSpeed() => m_moveSpeed;
        protected virtual float CalculateMovementSpeedMultiplier()
        {
            float cappedMultiplier = Mathf.Min(m_movementSpeedMultiplier, m_movementSpeedMaxMultiplier);
            return cappedMultiplier * motionRuntime.GetContextSpeedMultiplier();
        }

        public float GetResolvedMoveSpeed()
        {
            return Mathf.Max(CalculateMoveSpeed() * CalculateMovementSpeedMultiplier(), 0.0f);
        }

        protected float GetContextMovementSpeedMultiplier() => motionRuntime.GetContextSpeedMultiplier();

        /// <summary>
        /// 设置动作执行层的常规速度倍率。RPG 属性、装备和状态效果的倍率仍由 <see cref="CharacterBase"/> 维护。
        /// </summary>
        public void SetMovementSpeedMultiplier(float multiplier)
        {
            m_movementSpeedMultiplier = Mathf.Max(0.0f, multiplier);
        }

        /// <summary>
        /// 限制动作执行层速度倍率上限，用于防止多个临时移动区域或 Buff 叠乘后超过设计范围。
        /// </summary>
        public void SetMovementSpeedMaxMultiplier(float maxMultiplier)
        {
            m_movementSpeedMaxMultiplier = Mathf.Max(0.0f, maxMultiplier);
        }

        /// <summary>
        /// 压入一个临时上下文速度倍率，例如地形区域、机关、脚下效果；后压入的倍率优先生效。
        /// </summary>
        public void SetContextSpeedMultiplier(float multiplier)
        {
            motionRuntime.SetContextSpeedMultiplier(multiplier);
        }

        /// <summary>
        /// 移除最近一次上下文速度倍率，恢复到上一层上下文；没有上下文时保持默认倍率。
        /// </summary>
        public void ResetContextSpeedMultiplier()
        {
            motionRuntime.ResetContextSpeedMultiplier();
        }

        /// <summary>
        /// 清空所有临时上下文速度倍率，通常用于场景切换、复活或外部区域强制收尾。
        /// </summary>
        public void ClearContextSpeedMultipliers()
        {
            motionRuntime.ClearContextSpeedMultipliers();
        }

        /// <summary>
        /// 清空当前普通移动和导航移动状态。
        /// 吸收 uMMORPG `Movement.Reset()` 的“立刻停住并清掉当前移动命令”语义，
        /// 但继续沿用当前 Rigidbody2D + MoveOrder 真相源，不引入 NavMesh 或第二套控制器。
        /// </summary>
        public void ResetMovement()
        {
            motionRuntime.ResetMovement();
        }

        /// <summary>
        /// 禁止或恢复普通移动输入。强制位移和推力有独立生命周期，不通过这个开关隐藏。
        /// </summary>
        public void SetMovementForbidden(bool movementForbidden)
        {
            m_movementForbidden = movementForbidden;

            if (movementForbidden)
            {
                ResetMovement();
            }
        }

        /// <summary>
        /// 普通移动输入是否被动作层禁止；强制位移和推力不通过这个状态表达。
        /// </summary>
        public bool IsMovementForbidden() => m_movementForbidden;

        /// <summary>
        /// 当前是否存在 MoveTo 指令。能力权限可用它阻断需要玩家完全自由控制的能力。
        /// </summary>
        public bool HasMoveOrder() => motionRuntime.HasMoveOrder();
        public bool HasActiveMovementIntent() => motionRuntime.HasActiveMovementIntent();

        /// <summary>
        /// 把本次真实移动结果交给动画表现；角色子类可转交给自己的正式动作驱动。
        /// </summary>
        protected virtual void UpdateMovementAnimation(Vector2 movement)
        {
            m_animationStrategy?.SetMovement(movement);
        }

        protected virtual void FixedUpdate()
        {
            activeController?.FixedUpdate();

            // 部分场景对象需要挂 Movable 复用控制器/动画合同，但不应参与物理移动或卡墙修正。
            if (!m_disableAllMovements)
            {
                motionRuntime.HandleWallCollision();
                motionRuntime.HandleMovement();
                motionRuntime.HandlePush();
            }
        }

        public void SetMovementDirection(Vector2 direction)
        {
            motionRuntime.SetMovementDirection(direction, true);
        }

        /// <summary>
        /// 写入移动求解器产出的安全移动方向；身体朝向必须由玩家输入或 AI 状态层显式决定。
        /// </summary>
        internal void SetSteeringMovementDirection(Vector2 direction)
        {
            motionRuntime.SetMovementDirection(direction, false);
        }

        public void SetSteeringMotion(float speedScale, Vector2 correctionDisplacement)
        {
            motionRuntime.SetSteeringMotion(speedScale, correctionDisplacement);
        }

        public virtual bool CanUpdateTargetDirection() => !IsMarkedAsDestroyed();
        public virtual bool CanMove() => !m_disableAllMovements && !m_movementForbidden && !IsPushed() && (!IsMarkedAsDestroyed() || m_canMoveDuringDeath);

        public TaskCompletionSource<bool> MoveTo(Vector2 destination, float? speedOverride = null)
        {
            return motionRuntime.MoveTo(destination, speedOverride);
        }

        /// <summary>
        /// 吸收 uMMORPG `Navigate(destination, stoppingDistance)` 的停止半径合同：
        /// 当前仍是直线 MoveOrder，不引入路径搜索，但正式允许“靠近到指定距离即算到达”。
        /// </summary>
        public TaskCompletionSource<bool> MoveTo(Vector2 destination, float stoppingDistance, float? speedOverride = null)
        {
            return motionRuntime.MoveTo(destination, stoppingDistance, speedOverride);
        }

        /// <summary>
        /// 按顺序连续执行一组世界坐标路径点。
        /// 路径计算不属于 Movable；该入口只复用现有 Rigidbody2D 碰撞和速度规则执行路线。
        /// </summary>
        public TaskCompletionSource<bool> MoveAlongPath(
            IReadOnlyList<Vector2> waypoints,
            float stoppingDistance,
            float? speedOverride = null)
        {
            return motionRuntime.MoveAlongPath(waypoints, stoppingDistance, speedOverride);
        }

        public bool IsMovingUp() => motionRuntime.IsMovingUp();
        public bool IsMovingDown() => motionRuntime.IsMovingDown();
        public bool IsMovingLeft() => motionRuntime.IsMovingLeft();
        public bool IsMovingRight() => motionRuntime.IsMovingRight();
        public bool IsMoving() => motionRuntime.IsMoving();

        public void LookAtTarget(Transform target)
        {
            float3 direction = target.position - transform.position;
            SetLookAtDirection(direction.xy);
        }

        public void LookAtTarget(Vector2 targetPosition)
        {
            Vector2 direction = targetPosition - (Vector2)transform.position;
            SetLookAtDirection(direction);
        }

        public void SetLookAtDirection(Vector2 direction)
        {
            if (direction.magnitude > 0.0f && direction != m_lookAtDirection)
            {
                // If no target direction override is set, update the target direction to match the look-at direction.
                // This ensures the character's target direction aligns with its facing direction when no specific target direction is provided (i.e. when using a controller).
                if (!m_targetDirectionOverride.HasValue)
                {
                    m_targetDirectionChangedEvent.Invoke(direction);
                    m_animationStrategy.SetTargetDirection(direction);
                }

                m_lookAtDirection = direction;
                m_lookAtDirectionChangedEvent.Invoke(direction);
                m_animationStrategy?.SetLookAtDirection(direction);
            }
        }

        public void SetTargetDirection(Vector2 direction)
        {
            if (CanUpdateTargetDirection())
            {
                m_targetDirectionOverride = direction;
                m_targetDirectionChangedEvent.Invoke(direction);
                m_animationStrategy?.SetTargetDirection(direction);

                if (m_lookAtDirectionUpdateStrategy == ELookAtDirectionUpdateStrategy.TargetBased)
                {
                    SetLookAtDirection(direction);
                }
            }
        }

        public void ResetTargetDirection()
        {
            if (!m_targetDirectionOverride.HasValue)
            {
                return;
            }

            m_targetDirectionOverride = null;
            if (m_lookAtDirection.sqrMagnitude > 0.0001f)
            {
                m_targetDirectionChangedEvent.Invoke(m_lookAtDirection);
                m_animationStrategy?.SetTargetDirection(m_lookAtDirection);
            }
        }

        public Vector2 GetTargetDirection()
        {
            return m_targetDirectionOverride ?? m_lookAtDirection;
        }

        /// <summary>
        /// 返回实体当前实际面朝方向；移动朝向表现应读取它，而不是读取技能或交互目标方向。
        /// </summary>
        public Vector2 GetLookAtDirection()
        {
            return m_lookAtDirection;
        }

        public bool TryGetGas2DFacingDirection(out Vector2 direction)
        {
            direction = m_lookAtDirection;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            direction.Normalize();
            return true;
        }

        public bool IsPushable() => m_pushIntensityScale > 0.0f;
        public bool IsPushed() => motionRuntime.IsPushed();
        public void InterruptPush() => motionRuntime.InterruptPush();

        protected bool TryPush(DamageInputDescriptor damageInput, Vector2 velocity) => TryPush(damageInput, velocity, default);

        protected bool TryPush(DamageInputDescriptor damageInput, Vector2 velocity, DamageImpactSettings impactSettings)
        {
            bool isSelfTargeted = damageInput.TryGetSourceCharacter(out CharacterBase sourceCharacter) && sourceCharacter == this;

            bool disablePush =
                impactSettings.pushMode == EDamagePushMode.Disabled ||
                isSelfTargeted ||
                (damageInput.silent && !GameManager.Config.allowPushOnSilentHit) ||
                (damageInput.IsRegularHit && !GameManager.Config.allowPushOnRegularHit) ||
                (damageInput.IsCriticalHit && !GameManager.Config.allowPushOnCriticalHit) ||
                (damageInput.IsMissed && !GameManager.Config.allowPushOnMissedHit);


            if (!disablePush)
            {
                if (impactSettings.pushMode == EDamagePushMode.Override)
                {
                    Push(velocity.normalized, impactSettings.sanitizedPushIntensity, impactSettings.sanitizedPushResistance);
                }
                else
                {
                    Push(velocity.normalized);
                }

                return true;
            }

            return false;
        }

        public void Push(Vector2 direction, float intensity = 5.0f, float resistance = 10.0f)
        {
            motionRuntime.Push(direction, intensity, resistance);
        }

        protected override Type GetDataBlockType() => typeof(MovableDataBlock);

        protected override void OnSave(PersistableDataBlock block)
        {
            base.OnSave(block);
            MovableDataBlock movableBlock = block.As<MovableDataBlock>();
            movableBlock.lookAtDirection = m_lookAtDirection;
            movableBlock.controllerData = m_controller?.CreateDataBlock();
        }

        protected override void OnLoad(PersistableDataBlock block)
        {
            base.OnLoad(block);
            var movableBlock = block.As<MovableDataBlock>();
            SetLookAtDirection(movableBlock.lookAtDirection);
            m_controller?.LoadDataBlock(movableBlock.controllerData);
        }

        private void InitializeControllers()
        {
            m_controller?.Initialize(this);
        }

        private void TerminateControllers()
        {
            m_controller?.Terminate();
        }

        private void StartActiveController()
        {
            activeController?.Start();
        }

        private void StopActiveController()
        {
            activeController?.Stop();
        }

        private void SetActiveControllerOverride(IController controller)
        {
            IController previousController = activeController;
            IController nextController = controller ?? m_controller;

            if (ReferenceEquals(previousController, nextController))
            {
                m_activeControllerOverride = controller;
                return;
            }

            if (isActiveAndEnabled)
            {
                previousController?.Stop();
            }

            m_activeControllerOverride = controller;

            if (isActiveAndEnabled && !IsMarkedAsDestroyed())
            {
                nextController?.Start();
            }
        }

    }
}

