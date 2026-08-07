using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.SerializeReferenceExtensions;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 按生命周期、碰撞、交互或条件变化触发命令的场景组件。
    /// </summary>
    public class CommandTrigger : MonoBehaviour, IMovableCollisionReceiver, IInteractionReceiver
    {
        /// <summary>
        /// CommandTrigger 支持监听的触发时机。
        /// </summary>
        public enum EActivationEvent
        {
            OnStart,
            OnEnable,
            OnDisable,
            OnFixedUpdate,
            OnUpdate,
            OnPlayerEnterTrigger,
            OnPlayerExitTrigger,
            OnPlayerCollision,
            OnPlayerInteract,
            OnConditionStateChanged
        }

        [Header("条件")]
        [InspectorName("触发时机")]
        [Tooltip("组件在何种事件发生时尝试执行命令。")]
        [SerializeField] private EActivationEvent m_activationEvent;

        [InspectorName("执行条件")]
        [Tooltip("命令执行前需要满足的条件。为空时视为满足。")]
        [SerializeReference, SubclassSelector] private ICondition m_condition;

        [Header("动作")]
        [InspectorName("执行命令")]
        [Tooltip("触发时执行的命令，支持上下文命令接收当前玩家或脚本上下文。")]
        [SerializeReference, SubclassSelector] private ICommand m_toExecute;

        [Header("设置")]
        [InspectorName("延迟帧数")]
        [Tooltip("触发后延迟多少帧再执行命令；0 表示立即执行。")]
        [SerializeField] private int m_frameDelay = 0;

        private void OnEnable()
        {
            AttemptExecution(EActivationEvent.OnEnable);

            if (m_activationEvent == EActivationEvent.OnConditionStateChanged)
            {
                m_condition.StartListening(OnConditionStateChanged);
            }
        }

        private void OnDisable()
        {
            AttemptExecution(EActivationEvent.OnDisable);

            if (m_activationEvent == EActivationEvent.OnConditionStateChanged)
            {
                m_condition.StopListening();
            }
        }

        private void OnConditionStateChanged() => AttemptExecution(EActivationEvent.OnConditionStateChanged);

        private void AttemptExecution(EActivationEvent currentEvent)
        {
            AttemptExecution(currentEvent, actor: null, actorRequired: false);
        }

        private void AttemptExecutionForActor(EActivationEvent currentEvent, CharacterBase actor)
        {
            AttemptExecution(currentEvent, actor, actorRequired: true);
        }

        private void AttemptExecution(EActivationEvent currentEvent, CharacterBase actor, bool actorRequired)
        {
            // CommandTrigger 只对当前正式玩家上下文执行，编辑器预览或系统未启动时直接跳过。
            if (GameManager.Exists() && GameManager.TryGetSystem(out PlayerSystem playerSystem))
            {
                CharacterBase currentControlledCharacter = playerSystem.GetCurrentControlledCharacterOrPlayerInstance();
                if (!currentControlledCharacter)
                {
                    return;
                }

                if (actorRequired && actor != currentControlledCharacter)
                {
                    return;
                }

                if (currentEvent == m_activationEvent && (m_condition?.Evaluate() ?? true))
                {
                    GameCommandContext commandContext = actorRequired
                        ? GameCommandContext.LocalPlayer(actor)
                        : GameCommandContext.Script(currentControlledCharacter, nameof(CommandTrigger));

                    if (m_frameDelay <= 0)
                    {
                        Execute(commandContext);
                    }
                    else
                    {
                        ExecuteAfterFrameDelayAsync(
                            m_frameDelay,
                            commandContext,
                            destroyCancellationToken).Forget(LogAsyncException);
                    }
                }
            }
        }

        private void AttemptTriggerExecution(EActivationEvent currentEvent, Collider2D collider = null)
        {
            CharacterBase actor = collider != null
                ? collider.GetComponentInParent<CharacterBase>()
                : null;
            if (actor == null)
            {
                return;
            }

            AttemptExecutionForActor(currentEvent, actor);
        }

        private void Execute(GameCommandContext context)
        {
            m_toExecute.ExecuteFireAndReport(context, nameof(CommandTrigger), this);
        }

        private async UniTask ExecuteAfterFrameDelayAsync(
            int frames,
            GameCommandContext context,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < frames; ++i)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested || this == null)
            {
                return;
            }

            Execute(context);
        }

        private void LogAsyncException(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }

            Debug.LogException(exception, this);
        }

        private void Start() => AttemptExecution(EActivationEvent.OnStart);
        private void FixedUpdate() => AttemptExecution(EActivationEvent.OnFixedUpdate);
        private void Update() => AttemptExecution(EActivationEvent.OnUpdate);
        private void OnTriggerEnter2D(Collider2D collider) => AttemptTriggerExecution(EActivationEvent.OnPlayerEnterTrigger, collider);
        private void OnTriggerExit2D(Collider2D collider) => AttemptTriggerExecution(EActivationEvent.OnPlayerExitTrigger, collider);
        public void OnMovableCollision(Movable movable) => AttemptExecutionForActor(EActivationEvent.OnPlayerCollision, movable as CharacterBase);
        public void OnInteract(CharacterBase sender) => AttemptExecutionForActor(EActivationEvent.OnPlayerInteract, sender);
    }
}
