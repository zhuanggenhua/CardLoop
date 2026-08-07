using System;
using System.Threading.Tasks;
using MackySoft.SerializeReferenceExtensions;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 实体的基础存档块，保存 Transform 状态。
    /// </summary>
    [Serializable]
    public class EntityDataBlock : PersistableDataBlock
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    /// <summary>
    /// 场景中可持久化、可交互的基础实体，统一保存 Transform 并承载交互反馈。
    /// </summary>
    public class Entity : Persistable, IInteractionTarget
    {
        [InspectorName("交互逻辑")]
        [Tooltip("玩家与实体交互时执行的项目侧交互实现。为空时会播放拒绝反馈。")]
        [SerializeReference, SubclassSelector] private IInteraction m_interaction = null;

        [Header("反馈")]
        [InspectorName("交互反馈")]
        [SerializeField]
        [Tooltip("实体交互成功或拒绝时的表现反馈。交互规则仍由 IInteraction/ICommand 负责。")]
        private GameplayFeedbackSet m_feedbacks = new();

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
                YokiFrame.EventKit.Type.Send(new InteractionPresentationEvent(new InteractionPresentationContext(transform.position, sender, this, false)));
                return;
            }

            bool executed = await m_interaction.TryExecute(sender, this);
            if (executed)
            {
                m_feedbacks.PlayInteractionActivation(transform.position);
            }
            else
            {
                m_feedbacks.PlayInteractionDenied(transform.position);
            }

            YokiFrame.EventKit.Type.Send(new InteractionPresentationEvent(new InteractionPresentationContext(transform.position, sender, this, executed)));
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
