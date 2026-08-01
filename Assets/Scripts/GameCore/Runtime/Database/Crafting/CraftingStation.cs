using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Crafting + nameof(CraftingStation))]
    public class CraftingStation : DatabaseEntry
    {
        [SerializeField] private Recipe[] m_recipes = null;
        [SerializeField] private float m_priceMultiplier = 1.0f;
        [SerializeField] private int m_flatPrice = 0;

        public int recipeCount => m_recipes?.Length ?? 0;
        public Recipe GetRecipeAt(int index) => index >= 0 && index < recipeCount ? m_recipes[index] : null;
        public Recipe[] GetRecipes() => m_recipes != null ? (Recipe[])m_recipes.Clone() : System.Array.Empty<Recipe>();

        public int GetCraftingCost(Recipe recipe)
        {
            return recipe.CalculateCraftCost(m_flatPrice, m_priceMultiplier);
        }

        public bool CanCraft(Recipe recipe, out bool hasMoney, out bool hasIngredients)
        {
            return CanCraft(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance(), recipe, out hasMoney, out hasIngredients);
        }

        public bool CanCraft(CharacterBase owner, Recipe recipe, out bool hasMoney, out bool hasIngredients)
        {
            EnsureValidRecipe(recipe, nameof(CanCraft));
            return recipe.CanCraft(owner, out hasMoney, out hasIngredients, m_flatPrice, m_priceMultiplier);
        }

        public void Craft(Recipe recipe)
        {
            Craft(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance(), recipe);
        }

        public void Craft(CharacterBase owner, Recipe recipe)
        {
            InventoryOperationResult result = TryCraft(owner, recipe);
            if (!result.Succeeded)
            {
                throw new System.InvalidOperationException(
                    $"[{nameof(CraftingStation)}] 无法制作配方 {recipe.name}，失败原因={result.FailureReason}。");
            }
        }

        public InventoryOperationResult TryCraft(CharacterBase owner, Recipe recipe)
        {
            EnsureValidRecipe(recipe, nameof(TryCraft));
            int craftCost = recipe.CalculateCraftCost(m_flatPrice, m_priceMultiplier);
            InventoryOwnerHandle ownerHandle = GameManager.InventorySystem.GetOwner(owner);
            return GameManager.InventorySystem.ExecuteCraftRecipe(ownerHandle, recipe, craftCost);
        }

        private static void EnsureValidRecipe(Recipe recipe, string operationName)
        {
            if (!recipe)
            {
                throw new System.InvalidOperationException(
                    $"[{nameof(CraftingStation)}] {operationName} 需要有效配方，不能把空配方当成制作结果。");
            }

            recipe.EnsureCraftConfiguration();
        }
    }
}
