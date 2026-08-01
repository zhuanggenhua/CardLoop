using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 制作菜单的正式 UIKit 面板。
    /// 当前只承接菜单面板语义本身，不改变配方、材料或制作站规则真相。
    /// </summary>
    public class UICraft : UIKitMenuPanelBase, IInventoryBagItemClickHandler
    {
        [Header("Audio")]
        [SerializeField] private AudioClipResolver m_craftAudio;

        [Header("References")]
        [SerializeField] private UIInventoryBag m_inventoryBag = null;
        [SerializeField] private GameObject m_recipeEntryPrefab = null;
        [SerializeField] private GameObject m_recipeEntriesRoot = null;
        [SerializeField] private int m_recipeEntryPoolSize = 16;
        [SerializeField] private TextMeshProUGUI m_money = null;
        [SerializeField] private GameObject m_noRecipeSelectedIndicator = null;
        [SerializeField] private Transform m_ingredientEntriesRoot = null;
        [SerializeField] private GameObject m_ingredientEntryPrefab = null;
        [SerializeField] private int m_ingredientEntryPoolSize = 6;

        private UIRecipeEntry[] m_entries = System.Array.Empty<UIRecipeEntry>();
        private CraftingStation m_craftingStation = null;
        private GameCommandContext m_commandContext = GameCommandContext.Unknown();
        private readonly List<GameObject> m_activeRecipeEntries = new();
        private readonly List<GameObject> m_activeIngredientEntries = new();

        protected override void OnPanelInit()
        {
            ConfigureRecipeEntryPool();
            ConfigureIngredientEntryPool();
            m_inventoryBag.Init();
        }

        protected override void OnPanelHidden()
        {
            ReturnRecipeEntries();
            ReturnIngredientEntries();
        }

        private void OnDestroy()
        {
            ReturnRecipeEntries();
            ReturnIngredientEntries();
        }

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            if (!TryResolveCraftingStation(openData, out CraftingStation craftingStation, out GameCommandContext commandContext))
            {
                return;
            }

            m_craftingStation = craftingStation;
            m_commandContext = commandContext;
            UpdateUI();
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            foreach (UIRecipeEntry entry in m_entries)
            {
                if (entry != null)
                {
                    return entry.GetFocusTarget();
                }
            }

            UINavigationCursorTarget bagNavigationTarget = m_inventoryBag.FindNavigationTarget();

            if (bagNavigationTarget && bagNavigationTarget.gameObject.activeInHierarchy)
            {
                return bagNavigationTarget.gameObject;
            }

            return null;
        }

        private static bool TryResolveCraftingStation(UIKitMenuOpenData openData, out CraftingStation craftingStation, out GameCommandContext commandContext)
        {
            commandContext = GameCommandContext.Unknown();
            if (openData != null &&
                (openData.ArgumentCount == 1 || openData.ArgumentCount == 2) &&
                openData.TryGetArgument(0, out craftingStation))
            {
                if (openData.ArgumentCount == 2 &&
                    !openData.TryGetArgument(1, out commandContext))
                {
                    Debug.LogError($"[{nameof(UICraft)}] 制作面板上下文参数无效，第二个参数必须是 {nameof(GameCommandContext)}。");
                    craftingStation = null;
                    commandContext = GameCommandContext.Unknown();
                    return false;
                }

                return true;
            }

            Debug.LogError($"[{nameof(UICraft)}] 制作面板打开参数无效，当前正式菜单运行时必须传入唯一 {nameof(CraftingStation)} 实例。");
            craftingStation = null;
            return false;
        }

        private void UpdateUI(Recipe selectedRecipe = null, bool skipItemSlots = false)
        {
            CharacterBase inventoryOwner = ResolveInventoryOwner();
            m_inventoryBag.UpdateSlots(inventoryOwner);

            if (!skipItemSlots)
            {
                ClearEntries();
                FillEntries();
                RewireNavigation();
            }

            foreach (UIRecipeEntry entry in m_entries)
            {
                entry?.UpdateUI(inventoryOwner);
            }

            UpdatePlayerMoneyDisplay(selectedRecipe);
            UpdateRecipeDetails(selectedRecipe, inventoryOwner);
        }

        private void UpdateRecipeDetails(Recipe selectedRecipe, CharacterBase inventoryOwner)
        {
            ReturnIngredientEntries();

            m_noRecipeSelectedIndicator.SetActive(!selectedRecipe);

            if (selectedRecipe && m_ingredientEntryPrefab != null)
            {
                foreach (var requirement in selectedRecipe.GetIngredients())
                {
                    GameObject instance = GameObjectPoolService.Rent(m_ingredientEntryPrefab, GetIngredientEntriesRoot());
                    if (instance == null)
                    {
                        Debug.LogWarning("没有可用的制作材料条目实例，请检查材料条目对象池容量。", this);
                        continue;
                    }

                    if (!instance.TryGetComponent(out UIIngredientEntry ingredientEntry))
                    {
                        Debug.LogError("制作材料条目预制体缺少 UIIngredientEntry 组件。", instance);
                        GameObjectPoolService.Return(instance);
                        continue;
                    }

                    ingredientEntry.Initialize(requirement.Key, requirement.Value, inventoryOwner);
                    m_activeIngredientEntries.Add(instance);
                }
            }
        }

        private void UpdatePlayerMoneyDisplay(Recipe selectedRecipe)
        {
            int selectedRecipeCost = 0;

            if (selectedRecipe)
            {
                selectedRecipeCost = m_craftingStation.GetCraftingCost(selectedRecipe);
            }

            if (selectedRecipeCost == 0)
            {
                m_money.text = GameManager.InventorySystem.money.ToString();
            }
            else
            {
                m_money.text = string.Format("{0}\n({1}{2})",
                    GameManager.InventorySystem.money,
                    selectedRecipeCost > 0 ? "-" : "+",
                    math.abs(selectedRecipeCost));
            }
        }

        private void FillEntries()
        {
            int recipeCount = m_craftingStation.recipeCount;
            m_entries = new UIRecipeEntry[recipeCount];

            for (int i = 0; i < recipeCount; ++i)
            {
                Recipe recipe = m_craftingStation.GetRecipeAt(i);

                GameObject recipeEntryInstance = GameObjectPoolService.Rent(m_recipeEntryPrefab, m_recipeEntriesRoot.transform);
                if (recipeEntryInstance == null)
                {
                    Debug.LogWarning("没有可用的制作配方条目实例，请检查配方条目对象池容量。", this);
                    continue;
                }

                if (!recipeEntryInstance.TryGetComponent(out UIRecipeEntry recipeEntry))
                {
                    Debug.LogError("制作配方条目预制体缺少 UIRecipeEntry 组件。", recipeEntryInstance);
                    GameObjectPoolService.Return(recipeEntryInstance);
                    continue;
                }

                recipeEntry.Initialize(recipe, m_craftingStation);
                m_entries[i] = recipeEntry;
                m_activeRecipeEntries.Add(recipeEntryInstance);
            }
        }

        private void ClearEntries() => ReturnRecipeEntries();

        public void HandleRecipeEntrySelected(Recipe recipe) => UpdateUI(recipe, true);

        public void HandleRecipeEntryDeselected(Recipe recipe) => UpdateUI(recipe, true);

        public void HandleRecipeEntryClicked(Recipe recipe)
        {
            RunPanelTaskAndReport(HandleRecipeEntryClickedAsync(recipe), nameof(HandleRecipeEntryClicked));
        }

        private async System.Threading.Tasks.Task HandleRecipeEntryClickedAsync(Recipe recipe)
        {
            CharacterBase inventoryOwner = ResolveInventoryOwner();
            InventoryOperationResult result = m_craftingStation.TryCraft(inventoryOwner, recipe);
            if (result.Succeeded)
            {
                await GameManager.DialogueSystem.PlayNow(MenuFeedbackPrompts.CraftSucceeded, recipe.displayName);
                GameRuntimeEvents.RequestAudioPlayback(m_craftAudio);
                UpdateUI(recipe, true);
            }
            else if (result.FailureReason == EInventoryOperationFailureReason.InsufficientIngredients)
            {
                await GameManager.DialogueSystem.PlayNow(MenuFeedbackPrompts.CraftMissingIngredients, recipe.displayName);
            }
            else if (result.FailureReason == EInventoryOperationFailureReason.InsufficientFunds)
            {
                await GameManager.DialogueSystem.PlayNow(MenuFeedbackPrompts.CraftMissingMoney, recipe.displayName);
            }
        }

        public void HandleBagItemClicked(Item item)
        {
            RunPanelTaskAndReport(HandleBagItemClickedAsync(item), nameof(HandleBagItemClicked));
        }

        private async System.Threading.Tasks.Task HandleBagItemClickedAsync(Item item)
        {
            await GameManager.DialogueSystem.PlayNow(MenuFeedbackPrompts.CraftCannotUseItem, item.displayName);
        }

        private void RewireNavigation()
        {
            Selectable firstBagSlotSelectable = m_inventoryBag.GetFirstSlotSelectable();

            for (int i = 0; i < m_entries.Length; ++i)
            {
                UIRecipeEntry current = m_entries[i];
                if (current == null)
                {
                    continue;
                }

                UIRecipeEntry previous = i > 0 ? m_entries[i - 1] : null;
                UIRecipeEntry next = i < m_entries.Length - 1 ? m_entries[i + 1] : null;
                current.ConfigureNavigation(previous, next, firstBagSlotSelectable);
            }
        }

        private Transform GetIngredientEntriesRoot() => m_ingredientEntriesRoot ? m_ingredientEntriesRoot : transform;

        private void ConfigureRecipeEntryPool()
        {
            if (m_recipeEntryPrefab == null)
            {
                return;
            }

            GameObjectPoolService.SetMaxCapacity(m_recipeEntryPrefab, m_recipeEntryPoolSize);
            GameObjectPoolService.Prewarm(m_recipeEntryPrefab, m_recipeEntryPoolSize);
        }

        private void ConfigureIngredientEntryPool()
        {
            if (m_ingredientEntryPrefab == null)
            {
                return;
            }

            GameObjectPoolService.SetMaxCapacity(m_ingredientEntryPrefab, m_ingredientEntryPoolSize);
            GameObjectPoolService.Prewarm(m_ingredientEntryPrefab, m_ingredientEntryPoolSize);
        }

        private void ReturnRecipeEntries()
        {
            foreach (GameObject entry in m_activeRecipeEntries)
            {
                if (entry)
                {
                    GameObjectPoolService.Return(entry);
                }
            }

            m_activeRecipeEntries.Clear();
            m_entries = System.Array.Empty<UIRecipeEntry>();
        }

        private void ReturnIngredientEntries()
        {
            foreach (GameObject entry in m_activeIngredientEntries)
            {
                if (entry)
                {
                    GameObjectPoolService.Return(entry);
                }
            }

            m_activeIngredientEntries.Clear();
        }

        private CharacterBase ResolveInventoryOwner()
        {
            return m_commandContext.ResolveActorOrCurrentControlledCharacter();
        }
    }
}
