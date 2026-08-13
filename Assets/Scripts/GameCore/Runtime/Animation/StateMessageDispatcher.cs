using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 动画状态消息的传播模式；当前正式运行时只允许显式接收者合同。
    /// </summary>
    public enum EMessagePropagationMode
    {
        /// <summary>
        /// 保留原序列化值 3，确保已经登记的动画资源继续命中显式接收者模式。
        /// 旧的 Broadcast/Send/SendUpwards 字符串传播分支已经从正式运行时移除。
        /// </summary>
        RequireExplicitReceiver = 3
    }

    /// <summary>
    /// Animator 状态进入或退出时要派发的消息配置。
    /// </summary>
    [Serializable]
    public struct MessageData
    {
        [LabelText("消息名")]
        [Tooltip("必须命中 AnimationStateMessageNames 中登记的正式消息名。")]
        public string message;

        [LabelText("传播模式")]
        [Tooltip("当前只支持显式接收者模式，旧字符串传播模式不会在正式运行时兜底。")]
        public EMessagePropagationMode propagationMode;

        /// <summary>
        /// 只有配置了非空消息名才会尝试派发。
        /// </summary>
        public bool IsValid() => !string.IsNullOrWhiteSpace(message);
    }

    /// <summary>
    /// Animator 状态机消息派发器，把动画状态进入/退出事件转成明确的接收者接口调用。
    /// </summary>
    public class StateMessageDispatcher : StateMachineBehaviour
    {
        [LabelText("进入状态消息")]
        [Tooltip("Animator 进入该状态时派发的消息。为空则不派发。")]
        public MessageData animationStartMessage;

        [LabelText("退出状态消息")]
        [Tooltip("Animator 退出该状态时派发的消息。为空则不派发。")]
        public MessageData animationEndMessage;


        override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (animationStartMessage.IsValid())
            {
                PropagateMessage(animator, animationStartMessage);
            }
        }

        override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (animationEndMessage.IsValid())
            {
                PropagateMessage(animator, animationEndMessage);
            }
        }

        private void PropagateMessage(Component source, MessageData messageData)
        {
            if (messageData.propagationMode != EMessagePropagationMode.RequireExplicitReceiver)
            {
                Debug.Assert(false, $"Animation message '{messageData.message}' is still configured to use a removed string propagation mode on '{source.name}'.");
                return;
            }

            if (TryPropagateKnownMessage(source, messageData.message))
            {
                return;
            }

            Debug.Assert(false, $"Animation message '{messageData.message}' requires an explicit animation-state receiver contract, but no registered contract handled it.");
        }

        /// <summary>
        /// 当前仓库里已登记的状态机消息必须命中正式接口合同。
        /// 这些消息的资产配置已经全部进入台账；如果没找到接收者，就说明当前接线有误，
        /// 不再悄悄退回旧的字符串传播模式掩盖问题。
        /// </summary>
        private static bool TryPropagateKnownMessage(Component source, string message)
        {
            switch (message)
            {
                case AnimationStateMessageNames.InvincibleAnimationStart:
                    RequireDispatch(source, message, static (ICharacterAnimationStateReceiver receiver) => receiver.OnInvincibleAnimationStart());
                    return true;

                case AnimationStateMessageNames.InvincibleAnimationStop:
                    RequireDispatch(source, message, static (ICharacterAnimationStateReceiver receiver) => receiver.OnInvincibleAnimationStop());
                    return true;

                case AnimationStateMessageNames.DeathAnimationStart:
                    RequireDispatch(source, message, static (ICharacterAnimationStateReceiver receiver) => receiver.OnDeathAnimationStart());
                    return true;

                case AnimationStateMessageNames.DeathAnimationStop:
                    RequireDispatch(source, message, static (ICharacterAnimationStateReceiver receiver) => receiver.OnDeathAnimationStop());
                    return true;

                case AnimationStateMessageNames.FadeInCompleted:
                    RequireDispatch(source, message, static (ITransitionAnimationStateReceiver receiver) => receiver.OnFadeInCompleted());
                    return true;

                case AnimationStateMessageNames.FadeOutCompleted:
                    RequireDispatch(source, message, static (ITransitionAnimationStateReceiver receiver) => receiver.OnFadeOutCompleted());
                    return true;

                case AnimationStateMessageNames.FloatingTextAnimationEnd:
                    RequireDispatch(source, message, static (IFloatingTextAnimationStateReceiver receiver) => receiver.OnFloatingTextAnimationEnd());
                    return true;
            }

            return false;
        }

        private static void RequireDispatch<TReceiver>(Component source, string message, Action<TReceiver> dispatch)
            where TReceiver : class
        {
            TReceiver receiver = source.GetComponentInParent<TReceiver>();
            Debug.Assert(receiver != null, $"Animation message '{message}' requires a parent {typeof(TReceiver).Name} receiver on '{source.name}'.");
            if (receiver != null)
            {
                dispatch(receiver);
            }
        }
    }
}

