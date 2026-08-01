using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using azixMcAze.SerializableDictionary;

namespace FantasyWord.GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Crafting + nameof(Recipe))]
    public class Recipe : DatabaseEntry, INameable
    {
        [Header("Settings")]
        [SerializeField] private int m_craftingFee;

        [Header("Overrides")]
        [SerializeField] private string m_nameOverride = string.Empty;
        [SerializeField] private Sprite m_iconOverride = null;

        [Header("Output")]
        [SerializeField] private Item m_item = null;
        [SerializeField][Min(1)] private int m_quantity = 1;
        [SerializeField] private SerializableDictionary<Item, int> m_additionalOutput = null;

        [Header("Input")]
        [SerializeField] private SerializableDictionary<Item, int> m_ingredients = null;

        public Item item => m_item;
        public Sprite icon => m_iconOverride ? m_iconOverride : (m_item ? m_item.icon : null);
        public string displayName => GetDisplayName();
        public int quantity => m_quantity;
        public KeyValuePair<Item, int>[] GetIngredients() => m_ingredients != null ? m_ingredients.ToArray() : System.Array.Empty<KeyValuePair<Item, int>>();
        public KeyValuePair<Item, int>[] GetAdditionalOutput() => m_additionalOutput != null ? m_additionalOutput.ToArray() : System.Array.Empty<KeyValuePair<Item, int>>();
        public int ingredientCount => m_ingredients?.Count ?? 0;

        public int CalculateCraftCost(int flatPrice, float craftingPriceMultiplier)
        {
            return (int)(m_craftingFee * craftingPriceMultiplier) + flatPrice;
        }

        public int CalculateCraftCapacity()
        {
            return CalculateCraftCapacity(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance());
        }

        public int CalculateCraftCapacity(CharacterBase owner)
        {
            EnsureCraftConfiguration();

            int maxCapacity = int.MaxValue;
            InventoryOwnerHandle ownerHandle = GameManager.InventorySystem.GetOwner(owner);

            foreach (var ingredient in GetIngredients())
            {
                maxCapacity = math.min(maxCapacity, GameManager.InventorySystem.GetItemCount(ownerHandle, ingredient.Key) / ingredient.Value);
            }

            return maxCapacity;
        }

        public virtual bool CanCraft(out bool hasMoney, out bool hasIngredients, int flatPrice = 0, float craftPriceMultiplier = 1.0f)
        {
            return CanCraft(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance(), out hasMoney, out hasIngredients, flatPrice, craftPriceMultiplier);
        }

        public virtual bool CanCraft(CharacterBase owner, out bool hasMoney, out bool hasIngredients, int flatPrice = 0, float craftPriceMultiplier = 1.0f)
        {
            EnsureCraftConfiguration();

            int craftCost = CalculateCraftCost(flatPrice, craftPriceMultiplier);

            hasMoney = GameManager.InventorySystem.HasSufficientFunds(craftCost);

            hasIngredients = true;

            foreach (var ingredient in GetIngredients())
            {
                if (!HasIngredient(owner, ingredient))
                {
                    hasIngredients = false;
                }
            }

            return hasIngredients && hasMoney;
        }

        private string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(m_nameOverride))
            {
                return m_nameOverride;
            }
            else if (m_item)
            {
                string displayName = m_item.displayName;

                if (m_quantity > 1)
                {
                    displayName = $"({m_quantity}) {displayName}";
                }

                return displayName;
            }
            else
            {
                return name;
            }
        }

        private bool HasIngredient(KeyValuePair<Item, int> ingredient)
        {
            return HasIngredient(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance(), ingredient);
        }

        private bool HasIngredient(CharacterBase owner, KeyValuePair<Item, int> ingredient)
        {
            InventoryOwnerHandle ownerHandle = GameManager.InventorySystem.GetOwner(owner);
            return GameManager.InventorySystem.HasItemInBag(ownerHandle, ingredient.Key, ingredient.Value);
        }

        public void EnsureCraftConfiguration()
        {
            if (!m_item)
            {
                throw new System.InvalidOperationException(
                    $"[{nameof(Recipe)}] 配方 {name} 缺少产物，不能把空物品当成制作成功结果。");
            }

            if (m_quantity <= 0)
            {
                throw new System.InvalidOperationException(
                    $"[{nameof(Recipe)}] 配方 {name} 的产物数量无效，当前数量={m_quantity}。");
            }

            foreach (var ingredient in GetIngredients())
            {
                if (!IsValidIngredient(ingredient))
                {
                    throw new System.InvalidOperationException(
                        $"[{nameof(Recipe)}] 配方 {name} 存在无效材料，材料必须非空且数量为正。");
                }
            }

            foreach (var output in GetAdditionalOutput())
            {
                if (!IsValidOutput(output))
                {
                    throw new System.InvalidOperationException(
                        $"[{nameof(Recipe)}] 配方 {name} 存在无效额外产物，产物必须非空且数量为正。");
                }
            }
        }

        public bool IsValidIngredient(KeyValuePair<Item, int> ingredient)
        {
            return ingredient.Key != null && ingredient.Value > 0;
        }

        private static bool IsValidOutput(KeyValuePair<Item, int> output)
        {
            return output.Key != null && output.Value > 0;
        }
    }
}

