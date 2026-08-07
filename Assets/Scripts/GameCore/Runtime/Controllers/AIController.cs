using System;
using ContextSteering2D;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// AI 状态层的身体朝向模式。它只决定角色身体朝向，不替代移动求解、技能目标方向或武器瞄准。
    /// </summary>
    internal enum AICharacterFacingMode2D
    {
        [InspectorName("保持当前朝向")]
        KeepCurrent,

        [InspectorName("面向目标")]
        FaceTarget,

        [InspectorName("面向移动方向")]
        FaceMovement
    }

    /// <summary>
    /// AI 控制器的存档状态，保留归位点、当前目标和关键冷却计时器。
    /// </summary>
    [Serializable]
    public class AIControllerDataBlock : ControllerDataBlock
    {
        /// <summary>
        /// AI 启动时的初始位置，用于目标丢失后回到活动范围。
        /// </summary>
        public Vector3 initialPosition;

        /// <summary>
        /// 当前战斗目标的持久化引用。
        /// </summary>
        public PersistableReference<CharacterBase> target;

        /// <summary>
        /// 重新寻找目标前的剩余冷却时间。
        /// </summary>
        public float retargetCooldownTimer;

        /// <summary>
        /// 攻击再次触发前的剩余冷却时间。
        /// </summary>
        public float attackCooldownTimer;

        /// <summary>
        /// 距离最后一次看见目标已经经过的时间。
        /// </summary>
        public float timeSinceTargetLastSeen;
    }

    /// <summary>
    /// 角色 AI 控制器，使用 ContextSteering2D 追踪目标、回到活动范围并触发攻击。
    /// </summary>
    [Serializable]
    public partial class AIController : AController<CharacterBase>
    {
        private const string DefaultTargetOrbitSteeringGroupId = "orbit";

        [Header("引用")]
        [InspectorName("主控实体")]
        [Tooltip("可选的跟随或守护对象；配置后 AI 的归位点优先取该实体位置。")]
        [SerializeField] private Entity m_master = null;

        [Header("追踪设置")]
        [InspectorName("发现半径")]
        [Tooltip("AI 搜索可攻击目标的最大半径。")]
        [SerializeField, Min(1.0f)] private float m_detectionRadius = 5.0f;

        [InspectorName("离初始点重置半径")]
        [Tooltip("AI 距离初始点超过该半径后，会倾向重置或返回活动范围。")]
        [SerializeField, Min(1.0f)] private float m_resetFromInitialPositionRadius = 10.0f;

        [InspectorName("离目标重置半径")]
        [Tooltip("AI 与目标距离超过该半径后，会放弃或重新寻找目标。")]
        [SerializeField, Min(1.0f)] private float m_resetFromTargetDistanceRadius = 10.0f;

        [InspectorName("越界重新选敌冷却")]
        [Tooltip("目标离开范围后，等待该秒数再尝试重新寻找目标。")]
        [SerializeField, Min(0.5f)] private float m_targetOutOfRangeRetargetCooldown = 3.0f;

        [InspectorName("距离主控目标期望距离")]
        [Tooltip("存在主控实体时，AI 尝试保持在主控目标附近的距离。")]
        [SerializeField, Min(0.1f)] private float m_soughtDistanceFromMasterTarget = 1.0f;

        [InspectorName("距离目标期望距离")]
        [Tooltip("追击目标时希望保持的距离，用于近战或环绕行为。")]
        [SerializeField, Min(0.1f)] private float m_soughtDistanceFromTarget = 1.0f;

        [Header("转向设置")]
        [InspectorName("转向配置")]
        [Tooltip("ContextSteering2D 使用的方向评分配置。")]
        [SerializeField] private ContextSteeringProfile2D m_steeringProfile = null;

        [InspectorName("通行转向组")]
        [Tooltip("非战斗移动时使用的转向组 ID。")]
        [SerializeField] private string m_transitSteeringGroupId = "transit";

        [InspectorName("追击转向组")]
        [Tooltip("追击目标时使用的转向组 ID。")]
        [SerializeField] private string m_targetPursuitSteeringGroupId = "predictive-target";

        [InspectorName("追击身体朝向")]
        [Tooltip("追击目标时身体朝向的状态层裁决：可保持当前、面向目标，或面向移动方向。")]
        [SerializeField] private AICharacterFacingMode2D m_targetPursuitFacingMode = AICharacterFacingMode2D.FaceTarget;

        [InspectorName("启用战斗游走")]
        [Tooltip("开启后，敌人进入游走范围便按参考行为随机左右游走；关闭时只使用原追击流程。")]
        [SerializeField] private bool m_useCombatWander = false;

        [InspectorName("战斗游走范围")]
        [Tooltip("目标进入该范围时，允许使用战斗游走。")]
        [SerializeField, Min(0.1f)] private float m_combatWanderRange = 3.0f;

        [InspectorName("战斗游走速度倍率")]
        [Tooltip("对应参考配置中的游走速度倍率。")]
        [SerializeField, Min(0.0f)] private float m_combatWanderSpeedMultiplier = 1.0f;

        [InspectorName("战斗游走转向组")]
        [Tooltip("战斗游走使用的行为组 ID。")]
        [SerializeField] private string m_combatWanderSteeringGroupId = "combat-wander";

        [InspectorName("战斗游走身体朝向")]
        [Tooltip("战斗游走开启后身体朝向的状态层裁决；默认保持当前，避免游走行为强制锁脸。")]
        [SerializeField] private AICharacterFacingMode2D m_combatWanderFacingMode = AICharacterFacingMode2D.KeepCurrent;

        [InspectorName("启用近身环绕")]
        [Tooltip("目标进入保持距离后切换到近身环绕组。关闭时继续使用追击组，并由 Arrive 在攻击距离附近停住。")]
        [SerializeField] private bool m_useTargetOrbitSteeringAtSoughtDistance = true;

        [InspectorName("近身环绕转向组")]
        [Tooltip("目标进入保持距离后使用的行为组 ID；默认 orbit，不应包含 Arrive。")]
        [SerializeField] private string m_targetOrbitSteeringGroupId = DefaultTargetOrbitSteeringGroupId;

        [InspectorName("重新寻路间隔")]
        [Tooltip("导航目标刷新路径的最小间隔秒数。")]
        [SerializeField, Min(0.1f)] private float m_navigationRepathInterval = 0.5f;

        [InspectorName("目标移动重算阈值")]
        [Tooltip("目标移动超过该距离后触发重新规划路径。")]
        [SerializeField, Min(0.05f)] private float m_navigationTargetMoveThreshold = 0.5f;

        [InspectorName("路径点容差")]
        [Tooltip("AI 距离当前路径点小于该值时视为已到达。")]
        [SerializeField, Min(0.05f)] private float m_navigationWaypointTolerance = 0.2f;

        [InspectorName("丢失目标后重置时间")]
        [Tooltip("看不见目标持续超过该时间后，AI 会进入重置或重新选敌流程。")]
        [SerializeField, Min(0.1f)] private float m_timeBeforeResetAfterTargetSightLost = 3.0f;

        [InspectorName("不可见目标重新选敌冷却")]
        [Tooltip("目标不可见后再次尝试选敌的冷却时间。")]
        [SerializeField, Min(0.1f)] private float m_cannotSeeTargetRetargetCooldown = 1.0f;

        [Header("攻击设置")]
        [InspectorName("攻击触发半径")]
        [Tooltip("目标进入该距离后 AI 可尝试触发攻击。")]
        [SerializeField] public float m_attackTriggerRadius = 1.0f;

        [InspectorName("攻击冷却")]
        [Tooltip("两次攻击尝试之间的最小间隔秒数。")]
        [SerializeField] public float m_attackCooldown = 1.0f;

        [InspectorName("攻击前要求对准目标")]
        [Tooltip("开启后，AI 进入攻击触发半径时先面向目标，身体朝向完成后下一次判断才会开火。")]
        [SerializeField] private bool m_requireTargetFacingBeforeAttack = true;

        [InspectorName("攻击对准完成角度")]
        [Tooltip("参考 duolafashi turnToTargetDetal 的 5 度完成判定；只用于攻击前对准门禁。")]
        [SerializeField, Range(0.0f, 45.0f)] private float m_attackFacingCompletionAngleDegrees = 5.0f;

        private Transform transform => m_subject.transform;

        private Vector2 m_homePosition =>
            m_master ?
            (Vector2)m_master.transform.position :
            m_initialPosition;

        private bool ShouldUseTargetOrbitSteeringAtSoughtDistance =>
            m_useTargetOrbitSteeringAtSoughtDistance;

        private bool ShouldUseCombatWander => m_useCombatWander;

        private AICharacterFacingMode2D TargetPursuitFacingMode => m_targetPursuitFacingMode;

        private AICharacterFacingMode2D CombatWanderFacingMode => m_combatWanderFacingMode;

        private bool ShouldRequireTargetFacingBeforeAttack => m_requireTargetFacingBeforeAttack;

        private float AttackFacingCompletionAngleDegrees => Mathf.Clamp(m_attackFacingCompletionAngleDegrees, 0.0f, 45.0f);

        private string TargetOrbitSteeringGroupIdValue =>
            string.IsNullOrWhiteSpace(m_targetOrbitSteeringGroupId)
                ? DefaultTargetOrbitSteeringGroupId
                : m_targetOrbitSteeringGroupId;

        private CharacterBase m_target = null;
        private float m_retargetCooldownTimer = 0.0f;
        private float m_attackCooldownTimer = 0.0f;
        private Vector2 m_initialPosition;
        private float m_timeSinceTargetLastSeen = 0.0f;

        private BehaviourRuntime m_behaviourRuntime = null;
        private BehaviourRuntime behaviourRuntime => m_behaviourRuntime ??= new BehaviourRuntime(this);

        /// <summary>
        /// 判断当前身体朝向是否已经满足攻击前对准；角度完成值来自参考的转向完成语义。
        /// </summary>
        internal static bool IsAttackFacingAligned(
            Vector2 currentFacing,
            Vector2 attackDirection,
            float completionAngleDegrees)
        {
            if (currentFacing.sqrMagnitude <= 0.0001f ||
                attackDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float completedAngle = Mathf.Clamp(completionAngleDegrees, 0.0f, 45.0f);
            return Vector2.Angle(currentFacing, attackDirection) < completedAngle;
        }

        protected override void OnInitialize()
        {
            behaviourRuntime.Initialize();
        }

        protected override void OnStart()
        {
            m_subject.AddProvokedListener(OnProvoked);
        }

        protected override void OnStop()
        {
            m_subject.RemoveProvokedListener(OnProvoked);
            behaviourRuntime.Stop();
        }

        protected override void OnTerminate() => m_behaviourRuntime?.Dispose();

        /// <summary>
        /// 设置 AI 的主控实体，并可临时覆盖跟随主控目标的期望距离。
        /// </summary>
        public void SetMaster(Entity master, float? soughtDistanceFromMaster = null)
        {
            m_soughtDistanceFromMasterTarget = soughtDistanceFromMaster ?? m_soughtDistanceFromMasterTarget;
            m_master = master;
        }

        /// <summary>
        /// 尝试把指定角色设为战斗目标；目标必须对当前主体是合理敌对目标。
        /// </summary>
        public bool TrySetCombatTarget(CharacterBase target)
        {
            if (!target || m_subject == null || !CombatSolver.IsJudiciousTarget(m_subject, target))
            {
                return false;
            }

            m_target = target;
            m_retargetCooldownTimer = 0.0f;
            m_timeSinceTargetLastSeen = 0.0f;
            return true;
        }

        private void OnProvoked(CharacterBase source)
        {
            behaviourRuntime.TryHandleProvoked(source);
        }

        protected override void OnFixedUpdate()
        {
            behaviourRuntime.Tick();
        }

        protected override Type GetDataBlockType() => typeof(AIControllerDataBlock);

        protected override void OnLoad(IControllerDataBlock block)
        {
            base.OnLoad(block);
            var aiControllerDataBlock = block.As<AIControllerDataBlock>();
            m_initialPosition = aiControllerDataBlock.initialPosition;
            m_target = aiControllerDataBlock.target.ResolveOrNull();
            m_retargetCooldownTimer = aiControllerDataBlock.retargetCooldownTimer;
            m_attackCooldownTimer = aiControllerDataBlock.attackCooldownTimer;
            m_timeSinceTargetLastSeen = aiControllerDataBlock.timeSinceTargetLastSeen;
        }

        protected override void OnSave(IControllerDataBlock block)
        {
            base.OnSave(block);
            var aiControllerDataBlock = block.As<AIControllerDataBlock>();
            aiControllerDataBlock.initialPosition = m_initialPosition;
            aiControllerDataBlock.target = m_target;
            aiControllerDataBlock.retargetCooldownTimer = m_retargetCooldownTimer;
            aiControllerDataBlock.attackCooldownTimer = m_attackCooldownTimer;
            aiControllerDataBlock.timeSinceTargetLastSeen = m_timeSinceTargetLastSeen;
        }
    }
}
