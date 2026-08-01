using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 对话任务的进度存档块，只需要保存是否已经完成对话。
    /// </summary>
    [Serializable]
    public class TalkToCharacterTaskProgressDataBlock : QuestTaskProgressDataBlock
    {
        /// <summary>
        /// 任务是否已经由对话流程标记完成。
        /// </summary>
        public bool completed;

        public override IQuestTaskProgress CreateInstance() => new TalkToCharacterTaskProgress(this);
    }

    /// <summary>
    /// 由对话系统显式标记完成的任务进度，本身不订阅全局事件。
    /// </summary>
    public class TalkToCharacterTaskProgress : QuestTaskProgress<TalkToCharacterTaskProgressDataBlock>
    {
        private bool m_completed = false;

        public TalkToCharacterTask talkToCharacterTask => (TalkToCharacterTask)m_task;

        public TalkToCharacterTaskProgress(TalkToCharacterTask task) : base(task) { }

        public TalkToCharacterTaskProgress(TalkToCharacterTaskProgressDataBlock block) : base(block) { }

        /// <summary>
        /// 对话流程确认目标对话完成后调用，随后触发任务完成检查。
        /// </summary>
        public void MarkAsCompleted()
        {
            m_completed = true;
            UpdateProgression();
        }

        public override bool IsCompleted() => m_completed;

        protected override void OnProgressTrackingStarted() { }
        protected override void OnProgressTrackingStopped() { }

        public override TalkToCharacterTaskProgressDataBlock CreateDataBlock()
        {
            TalkToCharacterTaskProgressDataBlock block = base.CreateDataBlock();
            block.completed = m_completed;
            return block;
        }

        public override void LoadDataBlock(TalkToCharacterTaskProgressDataBlock block)
        {
            base.LoadDataBlock(block);
            m_completed = block.completed;
        }
    }

    /// <summary>
    /// 要求和指定角色完成指定对话的任务资产。
    /// </summary>
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Quests_Tasks + nameof(TalkToCharacterTask))]
    public class TalkToCharacterTask : QuestTask
    {
        [FormerlySerializedAs("target")]
        [InspectorName("目标角色")]
        [Tooltip("任务要求玩家对话的角色。")]
        [SerializeField] private CharacterSheet m_target = null;

        [FormerlySerializedAs("dialogue")]
        [InspectorName("目标对话")]
        [Tooltip("完成该对话后，任务进度会被对话系统标记为完成。")]
        [SerializeField] private DialogueSequence m_dialogue = null;

        public CharacterSheet target => m_target;
        public DialogueSequence dialogue => m_dialogue;

        public TalkToCharacterTask()
        {
            m_title = "Talk to {0}";
        }

        public override IQuestTaskProgress CreateTaskProgress() => new TalkToCharacterTaskProgress(this);

        public override string GetCompletedTitle()
        {
            return StringFormatter.Format(m_title, target.displayName);
        }

        public override string GetInProgressTitle(IQuestTaskProgress progress) => GetCompletedTitle();
    }
}
