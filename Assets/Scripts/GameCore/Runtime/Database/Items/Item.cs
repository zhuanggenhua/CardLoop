using System.Threading.Tasks;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 物品当前所在位置。
    /// 使用效果可根据背包或装备位置决定不同处理方式。
    /// </summary>
    public enum EItemLocation
    {
        Bag,
        Equipment
    }

    /// <summary>
    /// 物品分类。
    /// 当前用于 UI、售卖规则和使用入口分流，不直接表示装备槽位。
    /// </summary>
    public enum EItemCategory
    {
        Consumable,
        Resource,
        Gear,
        Key
    }

    /// <summary>
    /// 物品转移来源或去向。
    /// 事件日志和 UI 用它区分装备、制作、交易、掉落、尸体迁移等不同语义。
    /// </summary>
    public enum EItemTransferType
    {
        Equipment,
        Crafting,
        Trading,
        Use,
        Chest,
        CharacterDrop,
        Command,
        Unknown,
        Corpse
    }

    /// <summary>
    /// 基础物品数据库条目。
    /// 它定义图标、显示文案、价格和使用效果；物品数量和归属由 InventorySystem 管。
    /// </summary>
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Items + nameof(Item))]
    public class Item : DatabaseEntry, INameable
    {
        [Header("通用")]
        [InspectorName("分类")]
        [Tooltip("决定物品在 UI 和售卖规则中的基础分类。")]
        [SerializeField] private EItemCategory m_category = 0;
        [InspectorName("图标")]
        [Tooltip("菜单和事件日志中显示的物品图标。")]
        [SerializeField] private Sprite m_icon = null;
        [InspectorName("显示名称")]
        [Tooltip("玩家可见名称。为空时使用数据库条目名称兜底。")]
        [SerializeField] private string m_displayName = string.Empty;
        [InspectorName("描述")]
        [Tooltip("玩家可见描述，支持 StringFormatter 占位符。")]
        [SerializeField] private string m_description = string.Empty;
        [InspectorName("价格")]
        [Tooltip("大于 0 且不是关键物品时允许售卖。")]
        [SerializeField] private int m_price = 50;

        [Header("使用")]
        [InspectorName("使用效果")]
        [Tooltip("从背包或装备位置使用物品时执行的效果。为空时会提示没有效果。")]
        [SerializeReference, SubclassSelector] protected IItemEffect m_onUse = null;

        public EItemCategory category => m_category;
        public Sprite icon => m_icon;
        public string displayName => DisplayNameUtils.GetNameOrDefault(this, m_displayName);
        public string description => StringFormatter.Format(m_description);
        public int price => m_price;
        public bool sellable => m_price > 0 && m_category != EItemCategory.Key;

        public Task Use(CharacterBase target, EItemLocation location)
        {
            return Use(target, target, location);
        }

        public async virtual Task Use(CharacterBase sourceOwner, CharacterBase target, EItemLocation location)
        {
            if (sourceOwner && !sourceOwner.Can(EActionFlags.ManageInventory))
            {
                await GameManager.DialogueSystem.PlayNow(MenuFeedbackPrompts.InventoryUseActionLocked, displayName);
                return;
            }

            if (!await (m_onUse?.TryUse(this, sourceOwner, target, location) ?? Task.FromResult(false)))
            {
                await GameManager.DialogueSystem.PlayNow("This item has no effect");
            }
        }
    }
}

