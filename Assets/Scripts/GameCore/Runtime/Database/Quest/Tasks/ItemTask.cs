using System;
using UnityEngine;
using UnityEngine.Serialization;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 收集物品任务的进度存档块；数量会在恢复后从目标背包范围重新扫描。
    /// </summary>
    [Serializable]
    public class ItemTaskProgressDataBlock : QuestTaskProgressDataBlock
    {
        public override IQuestTaskProgress CreateInstance() => new ItemTaskProgress(this);
    }

    /// <summary>
    /// 跟踪指定背包范围中的物品数量，并在数量变化或控制角色变化时刷新任务进度。
    /// </summary>
    public class ItemTaskProgress : QuestTaskProgress<ItemTaskProgressDataBlock>
    {
        /// <summary>
        /// 当前范围内已有的目标物品数量。
        /// </summary>
        public int currentQuantity { get; private set; } = 0;

        private ItemTask m_itemTask => (ItemTask)m_task;

        public ItemTaskProgress(ItemTask task) : base(task) { }

        public ItemTaskProgress(ItemTaskProgressDataBlock block) : base(block) { }

        protected override void OnProgressTrackingStarted()
        {
            EventKit.Type.Register<InventoryItemAddedEvent>(OnItemAdded);
            EventKit.Type.Register<InventoryItemRemovedEvent>(OnItemRemoved);

            if (m_itemTask.inventoryScope == EInventoryQueryScope.CurrentControlledCharacter &&
                GameManager.Exists() &&
                GameManager.HasSystem<PlayerSystem>())
            {
                GameManager.PlayerSystem.AddCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        protected override void OnProgressTrackingStopped()
        {
            EventKit.Type.UnRegister<InventoryItemAddedEvent>(OnItemAdded);
            EventKit.Type.UnRegister<InventoryItemRemovedEvent>(OnItemRemoved);

            if (m_itemTask.inventoryScope == EInventoryQueryScope.CurrentControlledCharacter &&
                GameManager.Exists() &&
                GameManager.HasSystem<PlayerSystem>())
            {
                GameManager.PlayerSystem.RemoveCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        public override bool IsCompleted()
        {
            return currentQuantity >= m_itemTask.amountToCollect;
        }

        protected override void Update()
        {
            int quantityInInventory = m_itemTask.GetCurrentQuantity();

            if (quantityInInventory != currentQuantity)
            {
                currentQuantity = quantityInInventory;
                UpdateProgression();
            }
        }

        private void OnItemAdded(InventoryItemAddedEvent inventoryItemAddedEvent)
        {
            if (GameManager.InventorySystem.IsOwnerInScope(m_itemTask.inventoryScope, inventoryItemAddedEvent.Owner))
            {
                Update();
            }
        }

        private void OnItemRemoved(InventoryItemRemovedEvent inventoryItemRemovedEvent)
        {
            if (GameManager.InventorySystem.IsOwnerInScope(m_itemTask.inventoryScope, inventoryItemRemovedEvent.Owner))
            {
                Update();
            }
        }

        private void OnCurrentControlledCharacterChanged(CharacterBase character)
        {
            Update();
        }
    }

    /// <summary>
    /// 要求玩家或队伍拥有指定数量物品的任务资产。
    /// </summary>
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Quests_Tasks + nameof(ItemTask))]
    public class ItemTask : QuestTask
    {
        [FormerlySerializedAs("item")]
        [InspectorName("目标物品")]
        [Tooltip("任务要求收集或持有的物品。")]
        [SerializeField]
        private Item m_item = null;

        [FormerlySerializedAs("amountToCollect")]
        [InspectorName("目标数量")]
        [Tooltip("达到该数量后任务完成。")]
        [SerializeField]
        private int m_amountToCollect = 1;

        [InspectorName("背包范围")]
        [Tooltip("决定从队伍、当前控制角色或其他背包范围统计目标物品数量。")]
        [SerializeField]
        private EInventoryQueryScope m_inventoryScope = EInventoryQueryScope.Party;

        public Item item => m_item;
        public int amountToCollect => m_amountToCollect;
        public EInventoryQueryScope inventoryScope => m_inventoryScope;

        public ItemTask()
        {
            m_title = "Acquire {0} ({1}/{2})";
        }

        public override IQuestTaskProgress CreateTaskProgress() => new ItemTaskProgress(this);

        /// <summary>
        /// 从配置的背包范围读取当前目标物品数量，作为任务完成判断的唯一来源。
        /// </summary>
        public int GetCurrentQuantity()
        {
            return GameManager.InventorySystem.GetItemCount(
                GameManager.InventorySystem.GetOwner(m_inventoryScope),
                m_item);
        }

        public override string GetCompletedTitle()
        {
            return StringFormatter.Format(m_title, item.displayName, amountToCollect, amountToCollect);
        }

        public override string GetInProgressTitle(IQuestTaskProgress progress)
        {
            return StringFormatter.Format(m_title, item.displayName, ((ItemTaskProgress)progress).currentQuantity, amountToCollect);
        }
    }
}
