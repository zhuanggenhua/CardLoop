using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FantasyWord.GameCore.Tests
{
    public sealed class InventorySystemTransactionEditModeTests
    {
        private readonly List<Object> m_createdObjects = new();
        private GameObject m_inventoryObject;
        private InventorySystem m_inventorySystem;

        [SetUp]
        public void SetUp()
        {
            m_inventoryObject = new GameObject("库存交易测试系统");
            m_inventorySystem = m_inventoryObject.AddComponent<InventorySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_inventoryObject)
            {
                Object.DestroyImmediate(m_inventoryObject);
            }

            for (int i = 0; i < m_createdObjects.Count; i++)
            {
                if (m_createdObjects[i])
                {
                    Object.DestroyImmediate(m_createdObjects[i]);
                }
            }

            m_createdObjects.Clear();
        }

        [Test]
        public void ExecuteShopPurchase_WithInsufficientFunds_DoesNotWriteInventory()
        {
            Item item = CreateItem("测试商品", 50);
            Shop shop = CreateShop(item);
            m_inventorySystem.AddMoney(20);

            InventoryOperationResult result = m_inventorySystem.ExecuteShopPurchase(
                InventoryOwnerHandle.DefaultParty,
                shop,
                item);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(EInventoryOperationFailureReason.InsufficientFunds, result.FailureReason);
            Assert.AreEqual(20, m_inventorySystem.money);
            Assert.AreEqual(0, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, item));
        }

        [Test]
        public void ExecuteShopSale_WithoutSourceItem_DoesNotAddMoney()
        {
            Item item = CreateItem("测试可卖物", 50);
            Shop shop = CreateShop(item);

            InventoryOperationResult result = m_inventorySystem.ExecuteShopSale(
                InventoryOwnerHandle.DefaultParty,
                shop,
                item);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(EInventoryOperationFailureReason.InsufficientQuantity, result.FailureReason);
            Assert.AreEqual(0, m_inventorySystem.money);
        }

        [Test]
        public void ExecuteCraftRecipe_WithNegativeCost_ThrowsAndKeepsInventory()
        {
            Item ingredient = CreateItem("测试材料", 5);
            Item output = CreateItem("测试产物", 30);
            Recipe recipe = CreateRecipe(output, 1, new Dictionary<Item, int> { [ingredient] = 2 });
            m_inventorySystem.AddMoney(20);
            m_inventorySystem.AddToBag(InventoryOwnerHandle.DefaultParty, ingredient, 2, EItemTransferType.Crafting);

            Assert.Throws<System.InvalidOperationException>(
                () => m_inventorySystem.ExecuteCraftRecipe(
                    InventoryOwnerHandle.DefaultParty,
                    recipe,
                    -1));
            Assert.AreEqual(20, m_inventorySystem.money);
            Assert.AreEqual(2, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, ingredient));
            Assert.AreEqual(0, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, output));
        }

        [Test]
        public void ExecuteCraftRecipe_WithMissingIngredient_DoesNotWritePartialState()
        {
            Item ingredient = CreateItem("测试材料", 5);
            Item output = CreateItem("测试产物", 30);
            Recipe recipe = CreateRecipe(output, 1, new Dictionary<Item, int> { [ingredient] = 2 });
            m_inventorySystem.AddMoney(100);
            m_inventorySystem.AddToBag(InventoryOwnerHandle.DefaultParty, ingredient, 1, EItemTransferType.Crafting);

            InventoryOperationResult result = m_inventorySystem.ExecuteCraftRecipe(
                InventoryOwnerHandle.DefaultParty,
                recipe,
                15);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(EInventoryOperationFailureReason.InsufficientIngredients, result.FailureReason);
            Assert.AreEqual(100, m_inventorySystem.money);
            Assert.AreEqual(1, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, ingredient));
            Assert.AreEqual(0, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, output));
        }

        [Test]
        public void ExecuteCraftRecipe_WithValidInputs_CommitsAllWrites()
        {
            Item ingredient = CreateItem("测试材料", 5);
            Item output = CreateItem("测试产物", 30);
            Recipe recipe = CreateRecipe(output, 1, new Dictionary<Item, int> { [ingredient] = 2 });
            m_inventorySystem.AddMoney(100);
            m_inventorySystem.AddToBag(InventoryOwnerHandle.DefaultParty, ingredient, 2, EItemTransferType.Crafting);

            InventoryOperationResult result = m_inventorySystem.ExecuteCraftRecipe(
                InventoryOwnerHandle.DefaultParty,
                recipe,
                15);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(EInventoryOperationFailureReason.None, result.FailureReason);
            Assert.AreEqual(85, m_inventorySystem.money);
            Assert.AreEqual(0, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, ingredient));
            Assert.AreEqual(1, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, output));
        }

        [Test]
        public void ExecuteChestLootInitialization_WithInvalidEntry_DoesNotWritePreviousEntries()
        {
            Item validItem = CreateItem("测试宝箱物品", 10);
            ChestLoot loot = CreateChestLoot(
                new[]
                {
                    new ChestLootEntry { item = validItem, quantity = 1 },
                    new ChestLootEntry { item = null, quantity = 1 }
                },
                25);

            Assert.Throws<System.InvalidOperationException>(
                () => m_inventorySystem.ExecuteChestLootInitialization(
                    InventoryOwnerHandle.DefaultParty,
                    loot));
            Assert.AreEqual(0, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, validItem));
            Assert.AreEqual(0, m_inventorySystem.money);
        }

        [Test]
        public void ExecuteChestLootInitialization_WithValidLoot_CommitsItemsAndMoney()
        {
            Item item = CreateItem("测试宝箱物品", 10);
            ChestLoot loot = CreateChestLoot(
                new[] { new ChestLootEntry { item = item, quantity = 2 } },
                25);

            m_inventorySystem.ExecuteChestLootInitialization(InventoryOwnerHandle.DefaultParty, loot);

            Assert.AreEqual(2, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, item));
            Assert.AreEqual(25, m_inventorySystem.money);
        }

        [Test]
        public void ExecuteLootReward_WithInvalidEntry_DoesNotWritePreviousEntriesOrMoney()
        {
            Item validItem = CreateItem("测试奖励物品", 10);
            Loot[] grantedLoot =
            {
                new Loot { item = validItem, quantity = 1 },
                new Loot { item = null, quantity = 1 }
            };

            Assert.Throws<System.InvalidOperationException>(
                () => m_inventorySystem.ExecuteLootReward(
                    InventoryOwnerHandle.DefaultParty,
                    grantedLoot,
                    25,
                    EItemTransferType.CharacterDrop));
            Assert.AreEqual(0, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, validItem));
            Assert.AreEqual(0, m_inventorySystem.money);
        }

        [Test]
        public void ExecuteLootReward_WithValidLoot_CommitsItemsAndMoney()
        {
            Item item = CreateItem("测试奖励物品", 10);
            Loot[] grantedLoot =
            {
                new Loot { item = item, quantity = 2 }
            };

            m_inventorySystem.ExecuteLootReward(
                InventoryOwnerHandle.DefaultParty,
                grantedLoot,
                25,
                EItemTransferType.CharacterDrop);

            Assert.AreEqual(2, m_inventorySystem.GetItemCount(InventoryOwnerHandle.DefaultParty, item));
            Assert.AreEqual(25, m_inventorySystem.money);
        }

        private Item CreateItem(string itemName, int price)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            item.name = itemName;
            SetPrivateField(item, "m_price", price);
            m_createdObjects.Add(item);
            return item;
        }

        private Shop CreateShop(Item item)
        {
            Shop shop = ScriptableObject.CreateInstance<Shop>();
            shop.name = "测试商店";
            SetPrivateField(shop, "m_items", new[] { item });
            m_createdObjects.Add(shop);
            return shop;
        }

        private Recipe CreateRecipe(Item output, int quantity, Dictionary<Item, int> ingredients)
        {
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "测试配方";
            SetPrivateField(recipe, "m_item", output);
            SetPrivateField(recipe, "m_quantity", quantity);
            SetPrivateField(recipe, "m_ingredients", CreateRecipeDictionary("m_ingredients", ingredients));
            SetPrivateField(
                recipe,
                "m_additionalOutput",
                CreateRecipeDictionary("m_additionalOutput", new Dictionary<Item, int>()));
            m_createdObjects.Add(recipe);
            return recipe;
        }

        private static ChestLoot CreateChestLoot(ChestLootEntry[] entries, int money)
        {
            object boxedLoot = new ChestLoot { money = money };
            FieldInfo entriesField = typeof(ChestLoot).GetField(
                "m_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(entriesField, $"找不到字段：{nameof(ChestLoot)}.m_entries");
            entriesField.SetValue(boxedLoot, entries);
            return (ChestLoot)boxedLoot;
        }

        private static object CreateRecipeDictionary(string fieldName, Dictionary<Item, int> source)
        {
            FieldInfo field = typeof(Recipe).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段：{nameof(Recipe)}.{fieldName}");

            object result = System.Activator.CreateInstance(field.FieldType);
            MethodInfo addMethod = field.FieldType.GetMethod(
                "Add",
                new[] { typeof(Item), typeof(int) });
            Assert.IsNotNull(addMethod, $"{field.FieldType.Name} 缺少 Add(Item, int) 方法。");

            foreach ((Item item, int quantity) in source)
            {
                addMethod.Invoke(result, new object[] { item, quantity });
            }

            return result;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段：{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
