using TMPro;
using UnityEngine;
using UnityEngine.UI;

using System.Collections.Generic;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 商店菜单的正式 UIKit 面板。
    /// 当前只承接菜单面板语义本身，不改变商店、背包或交易规则真相。
    /// </summary>
    public class UIShop : UIKitMenuPanelBase, IInventoryBagItemClickHandler
    {
        [Header("Audio")]
        [SerializeField] private AudioClipResolver m_buySellAudio;

        [Header("References")]
        [SerializeField] private UIInventoryBag m_inventoryBag = null;
        [SerializeField] private GameObject m_shopEntryPrefab = null;
        [SerializeField] private GameObject m_itemSlotsRoot = null;
        [SerializeField] private int m_shopEntryPoolSize = 24;
        [SerializeField] private TextMeshProUGUI m_money = null;

        private UIShopEntry[] m_slots = System.Array.Empty<UIShopEntry>();
        private Shop m_shop = null;
        private GameCommandContext m_commandContext = GameCommandContext.Unknown();
        private readonly List<GameObject> m_activeShopEntries = new();

        protected override void OnPanelInit()
        {
            ConfigureShopEntryPool();
            m_inventoryBag.Init();
        }

        protected override void OnPanelHidden()
        {
            ReturnShopEntries();
        }

        private void OnDestroy()
        {
            ReturnShopEntries();
        }

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            if (!TryResolveShop(openData, out Shop shop, out GameCommandContext commandContext))
            {
                return;
            }

            m_shop = shop;
            m_commandContext = commandContext;
            UpdateUI();
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            foreach (UIShopEntry slot in m_slots)
            {
                if (slot != null)
                {
                    return slot.GetFocusTarget();
                }
            }

            UINavigationCursorTarget bagNavigationTarget = m_inventoryBag.FindNavigationTarget();

            if (bagNavigationTarget && bagNavigationTarget.gameObject.activeInHierarchy)
            {
                return bagNavigationTarget.gameObject;
            }

            return null;
        }

        private static bool TryResolveShop(UIKitMenuOpenData openData, out Shop shop, out GameCommandContext commandContext)
        {
            commandContext = GameCommandContext.Unknown();
            if (openData != null &&
                (openData.ArgumentCount == 1 || openData.ArgumentCount == 2) &&
                openData.TryGetArgument(0, out shop))
            {
                if (openData.ArgumentCount == 2 &&
                    !openData.TryGetArgument(1, out commandContext))
                {
                    Debug.LogError($"[{nameof(UIShop)}] 商店面板上下文参数无效，第二个参数必须是 {nameof(GameCommandContext)}。");
                    shop = null;
                    commandContext = GameCommandContext.Unknown();
                    return false;
                }

                return true;
            }

            Debug.LogError($"[{nameof(UIShop)}] 商店面板打开参数无效，当前正式菜单运行时必须传入唯一 {nameof(Shop)} 实例。");
            shop = null;
            return false;
        }

        private void UpdateUI(bool skipItemSlots = false)
        {
            CharacterBase inventoryOwner = ResolveInventoryOwner();
            m_inventoryBag.UpdateSlots(inventoryOwner);

            if (!skipItemSlots)
            {
                ClearSlots();
                FillSlots();
                RewireNavigation();
            }
        }

        private void Update()
        {
            UpdatePlayerMoneyDisplay();
        }

        private void UpdatePlayerMoneyDisplay()
        {
            int selectedItemPrice = 0;
            ETransactionType transactionType = ETransactionType.Buy;

            GameObject selection = GameManager.EventSystem.currentSelectedGameObject;
            if (selection != null)
            {
                UIShopEntry shopItem = selection.GetComponent<UIShopEntry>();

                if (shopItem && shopItem.GetItem())
                {
                    transactionType = ETransactionType.Buy;
                    selectedItemPrice = m_shop.GetPrice(shopItem.GetItem(), transactionType);
                }
                else
                {
                    UIInventoryBagSlot bagItem = selection.GetComponent<UIInventoryBagSlot>();

                    if (bagItem && bagItem.GetItem())
                    {
                        transactionType = ETransactionType.Sell;
                        selectedItemPrice = m_shop.GetPrice(bagItem.GetItem(), transactionType);
                    }
                }

                if (selectedItemPrice == 0)
                {
                    m_money.text = GameManager.InventorySystem.money.ToString();
                }
                else
                {
                    m_money.text = string.Format("{0}\n({1}{2})",
                        GameManager.InventorySystem.money,
                        transactionType == ETransactionType.Buy ? "-" : "+",
                        selectedItemPrice);
                }
            }
        }

        private void FillSlots()
        {
            int itemCount = m_shop.itemCount;
            m_slots = new UIShopEntry[itemCount];

            for (int i = 0; i < itemCount; ++i)
            {
                Item item = m_shop.GetItemAt(i);

                GameObject itemSlot = GameObjectPoolService.Rent(m_shopEntryPrefab, m_itemSlotsRoot.transform);
                if (itemSlot == null)
                {
                    Debug.LogWarning("没有可用的商店条目实例，请检查商店条目对象池容量。", this);
                    continue;
                }

                if (!itemSlot.TryGetComponent(out UIShopEntry inventoryBagSlot))
                {
                    Debug.LogError($"商店条目预制体缺少 {nameof(UIShopEntry)} 组件。", itemSlot);
                    GameObjectPoolService.Return(itemSlot);
                    continue;
                }

                inventoryBagSlot.Initialize(item);
                m_slots[i] = inventoryBagSlot;
                m_activeShopEntries.Add(itemSlot);
            }
        }

        private void ClearSlots() => ReturnShopEntries();

        public void HandleShopSlotClicked(Item item)
        {
            RunPanelTaskAndReport(HandleShopSlotClickedAsync(item), nameof(HandleShopSlotClicked));
        }

        private async System.Threading.Tasks.Task HandleShopSlotClickedAsync(Item item)
        {
            CharacterBase inventoryOwner = ResolveInventoryOwner();
            InventoryOwnerHandle ownerHandle = GameManager.InventorySystem.GetOwner(inventoryOwner);
            InventoryOperationResult result = GameManager.InventorySystem.ExecuteShopPurchase(ownerHandle, m_shop, item);

            if (result.Succeeded)
            {
                GameRuntimeEvents.RequestAudioPlayback(m_buySellAudio);
                m_inventoryBag.SetCategory(item.category); // Navigate to the category of the purchased item for better UX
                UpdateUI(true);
            }
            else
            {
                await GameManager.DialogueSystem.PlayNow(MenuFeedbackPrompts.ShopCannotBuy, item.displayName);
            }
        }

        public void HandleBagItemClicked(Item item)
        {
            RunPanelTaskAndReport(HandleBagItemClickedAsync(item), nameof(HandleBagItemClicked));
        }

        private async System.Threading.Tasks.Task HandleBagItemClickedAsync(Item item)
        {
            CharacterBase inventoryOwner = ResolveInventoryOwner();
            InventoryOwnerHandle ownerHandle = GameManager.InventorySystem.GetOwner(inventoryOwner);
            InventoryOperationResult result = GameManager.InventorySystem.ExecuteShopSale(ownerHandle, m_shop, item);
            if (result.Succeeded)
            {
                GameRuntimeEvents.RequestAudioPlayback(m_buySellAudio);
                UpdateUI();
            }
            else
            {
                await GameManager.DialogueSystem.PlayNow(MenuFeedbackPrompts.ShopCannotSell, item.displayName);
                UpdateUI();
            }
        }

        private void RewireNavigation()
        {
            Selectable firstBagSlotSelectable = m_inventoryBag.GetFirstSlotSelectable();

            for (int i = 0; i < m_slots.Length; ++i)
            {
                UIShopEntry current = m_slots[i];
                if (current == null)
                {
                    continue;
                }

                UIShopEntry previous = i > 0 ? m_slots[i - 1] : null;
                UIShopEntry next = i < m_slots.Length - 1 ? m_slots[i + 1] : null;
                current.ConfigureNavigation(previous, next, firstBagSlotSelectable);
            }
        }

        private void ConfigureShopEntryPool()
        {
            if (m_shopEntryPrefab == null)
            {
                return;
            }

            GameObjectPoolService.SetMaxCapacity(m_shopEntryPrefab, m_shopEntryPoolSize);
            GameObjectPoolService.Prewarm(m_shopEntryPrefab, m_shopEntryPoolSize);
        }

        private void ReturnShopEntries()
        {
            foreach (GameObject entry in m_activeShopEntries)
            {
                if (entry)
                {
                    GameObjectPoolService.Return(entry);
                }
            }

            m_activeShopEntries.Clear();
            m_slots = System.Array.Empty<UIShopEntry>();
        }

        private CharacterBase ResolveInventoryOwner()
        {
            return m_commandContext.ResolveActorOrCurrentControlledCharacter();
        }
    }
}

