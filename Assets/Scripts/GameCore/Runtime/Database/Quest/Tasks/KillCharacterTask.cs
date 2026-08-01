using System;
using UnityEngine;
using UnityEngine.Serialization;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 击杀任务的进度存档块，保存已经击杀的目标数量。
    /// </summary>
    [Serializable]
    public class KillCharacterTaskProgressDataBlock : QuestTaskProgressDataBlock
    {
        /// <summary>
        /// 已计入任务进度的击杀数量。
        /// </summary>
        public int charactersKilled;

        public override IQuestTaskProgress CreateInstance() => new KillCharacterTaskProgress(this);
    }

    /// <summary>
    /// 监听角色死亡事件并统计目标 CharacterSheet 击杀数的任务进度。
    /// </summary>
    public class KillCharacterTaskProgress : QuestTaskProgress<KillCharacterTaskProgressDataBlock>
    {
        /// <summary>
        /// 当前已击杀的目标数量。
        /// </summary>
        public int charactersKilled { get; private set; } = 0;

        private KillCharacterTask m_killCharacterTask => (KillCharacterTask)m_task;

        public KillCharacterTaskProgress(KillCharacterTask task) : base(task) { }

        public KillCharacterTaskProgress(KillCharacterTaskProgressDataBlock block) : base(block) { }

        protected override void OnProgressTrackingStarted()
        {
            EventKit.Type.Register<CharacterKilledEvent>(OnCharacterKilled);
        }

        protected override void OnProgressTrackingStopped()
        {
            EventKit.Type.UnRegister<CharacterKilledEvent>(OnCharacterKilled);
        }

        public override bool IsCompleted()
        {
            return charactersKilled >= m_killCharacterTask.charactersToKill;
        }

        private void OnCharacterKilled(CharacterSheet character)
        {
            if (character == m_killCharacterTask.character)
            {
                ++charactersKilled;
                UpdateProgression();
            }
        }

        private void OnCharacterKilled(CharacterKilledEvent evt)
        {
            OnCharacterKilled(evt.Character);
        }

        public override KillCharacterTaskProgressDataBlock CreateDataBlock()
        {
            KillCharacterTaskProgressDataBlock block = base.CreateDataBlock();
            block.charactersKilled = charactersKilled;
            return block;
        }

        public override void LoadDataBlock(KillCharacterTaskProgressDataBlock block)
        {
            base.LoadDataBlock(block);
            charactersKilled = block.charactersKilled;
        }
    }

    /// <summary>
    /// 要求击杀指定角色模板若干次的任务资产。
    /// </summary>
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Quests_Tasks + nameof(KillCharacterTask))]
    public class KillCharacterTask : QuestTask
    {
        [FormerlySerializedAs("monster")]
        [FormerlySerializedAs("m_monster")]
        [InspectorName("目标角色")]
        [Tooltip("被击杀后会计入任务进度的角色配置。")]
        [SerializeField]
        private CharacterSheet m_character = null;

        [FormerlySerializedAs("monstersToKill")]
        [FormerlySerializedAs("m_monstersToKill")]
        [InspectorName("击杀数量")]
        [Tooltip("累计击杀目标角色达到该数量后任务完成。")]
        [SerializeField]
        private int m_charactersToKill = 1;

        public CharacterSheet character => m_character;
        public int charactersToKill => m_charactersToKill;

        public KillCharacterTask()
        {
            m_title = "Kill {0} ({1}/{2})";
        }

        public override IQuestTaskProgress CreateTaskProgress() => new KillCharacterTaskProgress(this);

        public override string GetCompletedTitle()
        {
            return StringFormatter.Format(m_title, character.displayName, charactersToKill, charactersToKill);
        }

        public override string GetInProgressTitle(IQuestTaskProgress progress)
        {
            return StringFormatter.Format(m_title, character.displayName, ((KillCharacterTaskProgress)progress).charactersKilled, charactersToKill);
        }
    }
}
