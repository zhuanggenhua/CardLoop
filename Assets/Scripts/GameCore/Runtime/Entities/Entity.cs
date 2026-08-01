using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using MackySoft.SerializeReferenceExtensions;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 实体的基础存档块，保存 Transform 状态。
    /// </summary>
    [Serializable]
    public class EntityDataBlock : PersistableDataBlock
    {
        /// <summary>
        /// 世界坐标位置。
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// 世界旋转。
        /// </summary>
        public Quaternion rotation;

        /// <summary>
        /// 本地缩放。
        /// </summary>
        public Vector3 scale;
    }

    /// <summary>
    /// 场景中可持久化、可交互的基础实体，统一保存 Transform 并承载交互反馈。
    /// </summary>
    public class Entity : Persistable, IInteractionTarget
    {
        [Header("实体设置")]
        [InspectorName("悬浮图标")]
        [Tooltip("实体上方展示交互、提示等短状态图标的 UI 组件。")]
        [SerializeField] private UIFloatingIcon m_floatingIcon = null;

        [InspectorName("交互逻辑")]
        [Tooltip("玩家与实体交互时执行的项目侧交互实现。为空时会播放拒绝反馈。")]
        [SerializeReference, SubclassSelector] private IInteraction m_interaction = null;

        [Header("反馈")]
        [InspectorName("交互反馈")]
        [SerializeField]
        [Tooltip("实体交互成功或拒绝时的表现反馈。交互规则仍由 IInteraction/ICommand 负责。")]
        private GameplayFeedbackSet m_feedbacks = new();

        /// <summary>
        /// 对话系统显示的说话者名称；普通实体默认没有名称。
        /// </summary>
        public virtual string GetSpeakerName() => string.Empty;

        /// <summary>
        /// 设置实体悬浮图标，可选在指定时间后由图标组件自行恢复。
        /// </summary>
        public void SetFloatingIcon(EFloatingIcon icon, float? duration = null) => m_floatingIcon?.SetIcon(icon, duration);

        /// <summary>
        /// 以脚本上下文播放该实体发起的对话序列。
        /// </summary>
        public virtual async Task Say(DialogueSequence sequence, UnityAction<DialogueMessageFeed> onDialogueEnded = null, params string[] args)
        {
            await Say(sequence, GameCommandContext.Script(), onDialogueEnded, args);
        }

        /// <summary>
        /// 以指定角色为来源播放对话，确保对话节点命令可以识别真实发起者。
        /// </summary>
        public virtual async Task Say(DialogueSequence sequence, CharacterBase source, UnityAction<DialogueMessageFeed> onDialogueEnded = null, params string[] args)
        {
            await Say(sequence, ResolveDialogueCommandContext(source), onDialogueEnded, args);
        }

        private async Task Say(DialogueSequence sequence, GameCommandContext commandContext, UnityAction<DialogueMessageFeed> onDialogueEnded = null, params string[] args)
        {
            string speaker = GetSpeakerName();

            DialogueTree dialogueTree = sequence.ToDialogueTree(speaker, commandContext, args);

            if (onDialogueEnded != null)
            {
                dialogueTree.AddEndedListener(onDialogueEnded);
            }

            await GameManager.DialogueSystem.PlayNow(dialogueTree);
        }

        private static GameCommandContext ResolveDialogueCommandContext(CharacterBase source)
        {
            return GameCommandContext.ResolveForActor(source);
        }

        public virtual void OnInteract(CharacterBase sender)
        {
            _ = ExecuteInteractionAndReport(sender);
        }

        private async Task ExecuteInteractionAndReport(CharacterBase sender)
        {
            try
            {
                await ExecuteInteraction(sender);
            }
            catch (Exception exception)
            {
                Debug.LogException(
                    new InvalidOperationException($"[{nameof(Entity)}] 交互执行失败。", exception),
                    this);
            }
        }

        private async Task ExecuteInteraction(CharacterBase sender)
        {
            if (m_interaction == null)
            {
                m_feedbacks.PlayInteractionDenied(transform.position);
                GameRuntimeEvents.NotifyInteractionPresentation(new InteractionPresentationContext(transform.position, sender, this, false));
                return;
            }

            bool executed = await m_interaction.TryExecute(sender, this);
            if (executed)
            {
                m_feedbacks.PlayInteractionActivation(transform.position);
                GameRuntimeEvents.NotifyInteractionPresentation(new InteractionPresentationContext(transform.position, sender, this, true));
            }
            else
            {
                m_feedbacks.PlayInteractionDenied(transform.position);
                GameRuntimeEvents.NotifyInteractionPresentation(new InteractionPresentationContext(transform.position, sender, this, false));
            }
        }

        protected override Type GetDataBlockType() => typeof(EntityDataBlock);

        protected override void OnSave(PersistableDataBlock block)
        {
            base.OnSave(block);
            block.As<EntityDataBlock>().position = transform.position;
            block.As<EntityDataBlock>().rotation = transform.rotation;
            block.As<EntityDataBlock>().scale = transform.localScale;
        }

        protected override void OnLoad(PersistableDataBlock block)
        {
            base.OnLoad(block);
            transform.position = block.As<EntityDataBlock>().position;
            transform.rotation = block.As<EntityDataBlock>().rotation;
            transform.localScale = block.As<EntityDataBlock>().scale;
        }
    }
}

