using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using azixMcAze.SerializableDictionary;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 背包所有者类型。
    /// 用于区分队伍、角色、容器、尸体、商店和制作台等不同库存归属。
    /// </summary>
    public enum EInventoryOwnerKind
    {
        Party,
        Character,
        Container,
        GroundPile,
        Corpse,
        Shop,
        CraftingStation
    }

    /// <summary>
    /// 背包查询范围。
    /// 命令和 UI 用它在默认队伍库存与当前受控角色库存之间选择。
    /// </summary>
    public enum EInventoryQueryScope
    {
        Party,
        CurrentControlledCharacter
    }

    /// <summary>
    /// 一个背包所有者的稳定句柄。
    /// Kind 说明库存类别，Id 绑定具体角色、容器或默认队伍，作为 InventorySystem 的字典键。
    /// </summary>
    [Serializable]
    public readonly struct InventoryOwnerHandle : IEquatable<InventoryOwnerHandle>
    {
        public static readonly InventoryOwnerHandle DefaultParty = new(EInventoryOwnerKind.Party, "default");

        public InventoryOwnerHandle(EInventoryOwnerKind kind, string id)
        {
            Kind = kind;
            Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        public EInventoryOwnerKind Kind { get; }
        public string Id { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Id);

        public static InventoryOwnerHandle ForCharacter(CharacterBase character)
        {
            if (!character)
            {
                Debug.LogError($"[{nameof(InventoryOwnerHandle)}] 角色背包 owner 需要有效角色，不能回退到默认队伍背包。");
                return new InventoryOwnerHandle(EInventoryOwnerKind.Character, string.Empty);
            }

            string persistentIdentifier = character.GetPersistentIdentifier();
            if (string.IsNullOrWhiteSpace(persistentIdentifier))
            {
                Debug.LogError($"[{nameof(InventoryOwnerHandle)}] 角色 {character.name} 缺少稳定持久化标识，不能作为可保存背包 owner。", character);
                return new InventoryOwnerHandle(EInventoryOwnerKind.Character, string.Empty);
            }

            return new InventoryOwnerHandle(EInventoryOwnerKind.Character, persistentIdentifier);
        }

        public static InventoryOwnerHandle ForPersistable(EInventoryOwnerKind kind, Persistable persistable)
        {
            if (!persistable)
            {
                Debug.LogError($"[{nameof(InventoryOwnerHandle)}] {kind} 背包 owner 需要有效持久化对象，不能生成 default 假标识。");
                return new InventoryOwnerHandle(kind, string.Empty);
            }

            string persistentIdentifier = persistable.GetPersistentIdentifier();
            if (string.IsNullOrWhiteSpace(persistentIdentifier))
            {
                Debug.LogError($"[{nameof(InventoryOwnerHandle)}] 持久化对象 {persistable.name} 缺少稳定持久化标识，不能作为可保存背包 owner。", persistable);
                return new InventoryOwnerHandle(kind, string.Empty);
            }

            return new InventoryOwnerHandle(kind, persistentIdentifier);
        }

        public bool Equals(InventoryOwnerHandle other) => Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InventoryOwnerHandle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, Id);
        public override string ToString() => $"{Kind}:{Id}";
    }

    /// <summary>
    /// 单个背包所有者的存档数据。
    /// Item 引用使用数据库引用，避免保存运行时对象实例。
    /// </summary>
    [Serializable]
    public class InventoryOwnerDataBlock
    {
        public EInventoryOwnerKind ownerKind;
        public string ownerId;
        public int money;
        public SerializableDictionary<DatabaseEntryReference<Item>, int> items;
    }

    /// <summary>
    /// InventorySystem 的整体存档数据块。
    /// </summary>
    [Serializable]
    public class InventoryDataBlock : DataBlock
    {
        public InventoryOwnerDataBlock[] inventories;
    }

    /// <summary>
    /// 游戏背包系统。
    /// 它是金钱、物品数量、装备转移和尸体库存迁移的运行时真相源。
    /// </summary>
    public class InventorySystem : AGameSystem, IDataBlockHandler<InventoryDataBlock>
    {
        /// <summary>
        /// 单个所有者在内存中的背包状态。
        /// 这里保存运行时 Item 资产引用；存档时再转换为数据库引用。
        /// </summary>
        private sealed class InventoryRuntime
        {
            public int money;
            public readonly Dictionary<Item, int> items = new();
        }

        private readonly Dictionary<InventoryOwnerHandle, InventoryRuntime> m_inventories = new();

        public int money => GetMoney(InventoryOwnerHandle.DefaultParty);

        public InventoryOwnerHandle GetOwner(CharacterBase character)
        {
            if (character == null)
            {
                return InventoryOwnerHandle.DefaultParty;
            }

            if (character.TryGetComponent(out CharacterInventory inventory) && inventory != null)
            {
                return inventory.ResolveMainInventoryOwner();
            }

            throw new InvalidOperationException(
                $"[{nameof(InventorySystem)}] Character inventory owner requires {nameof(CharacterInventory)} on [{character.name}].");
        }

        public InventoryOwnerHandle GetCorpseOwner(CharacterBase character) =>
            InventoryOwnerHandle.ForPersistable(EInventoryOwnerKind.Corpse, character);

        public InventoryOwnerHandle GetOwner(EInventoryQueryScope queryScope)
        {
            switch (queryScope)
            {
                case EInventoryQueryScope.CurrentControlledCharacter:
                    return GetOwner(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance());
                case EInventoryQueryScope.Party:
                default:
                    return InventoryOwnerHandle.DefaultParty;
            }
        }

        public KeyValuePair<Item, int>[] GetBagEntries(InventoryOwnerHandle owner)
        {
            return GetInventory(owner).items.ToArray();
        }

        public int GetItemCount(InventoryOwnerHandle owner, Item item)
        {
            if (GetInventory(owner).items.TryGetValue(item, out int count))
            {
                return count;
            }

            return 0;
        }

        public void AddMoney(int value)
        {
            AddMoney(InventoryOwnerHandle.DefaultParty, value);
        }

        private void AddMoney(InventoryOwnerHandle owner, int value)
        {
            if (value > 0)
            {
                GetInventory(owner).money += value;
                GameRuntimeEvents.NotifyInventoryMoneyAdded(owner, value);
            }
        }

        public void RemoveMoney(int value)
        {
            RemoveMoney(InventoryOwnerHandle.DefaultParty, value);
        }

        private void RemoveMoney(InventoryOwnerHandle owner, int value)
        {
            if (value > 0)
            {
                InventoryRuntime inventory = GetInventory(owner);
                inventory.money = math.max(inventory.money - value, 0);
                GameRuntimeEvents.NotifyInventoryMoneyRemoved(owner, value);
            }
        }

        public bool HasSufficientFunds(int value) => value <= money;
        private bool HasSufficientFunds(InventoryOwnerHandle owner, int value) => value <= GetMoney(owner);

        /// <summary>
        /// 执行库存系统内部付款步骤。
        /// 成功表示确认时资金仍足够且已扣款；失败表示没有写入任何金钱状态。
        /// </summary>
        private InventoryOperationResult ExecuteMoneyPayment(int amount)
        {
            EnsureValidMoneyPayment(amount, nameof(ExecuteMoneyPayment));

            if (!HasSufficientFunds(amount))
            {
                return InventoryOperationResult.Failed(EInventoryOperationFailureReason.InsufficientFunds);
            }

            RemoveMoney(amount);
            return InventoryOperationResult.Success();
        }

        private int GetMoney(InventoryOwnerHandle owner)
        {
            return GetInventory(owner).money;
        }

        public bool IsOwnerInScope(EInventoryQueryScope queryScope, InventoryOwnerHandle owner)
        {
            switch (queryScope)
            {
                case EInventoryQueryScope.CurrentControlledCharacter:
                    return owner.Equals(GetOwner(EInventoryQueryScope.CurrentControlledCharacter));
                case EInventoryQueryScope.Party:
                default:
                    return owner.Equals(InventoryOwnerHandle.DefaultParty);
            }
        }

        public bool HasItemInBag(InventoryOwnerHandle owner, Item item, int quantity = 1)
        {
            return GetInventory(owner).items.TryGetValue(item, out int count) && count >= quantity;
        }

        /// <summary>
        /// 执行商店买入交易。
        /// 先验证目标背包和资金，再一次性完成扣钱与入包，避免 UI 调用方拼出半完成交易。
        /// </summary>
        public InventoryOperationResult ExecuteShopPurchase(
            InventoryOwnerHandle destinationOwner,
            Shop shop,
            Item item)
        {
            EnsureValidInventoryOwner(destinationOwner, nameof(ExecuteShopPurchase));
            EnsureValidShopTransaction(shop, item, nameof(ExecuteShopPurchase));

            int itemPrice = shop.GetPrice(item, ETransactionType.Buy);
            InventoryOperationResult paymentResult = ExecuteMoneyPayment(itemPrice);
            if (!paymentResult.Succeeded)
            {
                return paymentResult;
            }

            AddToBag(destinationOwner, item, 1, EItemTransferType.Trading);
            return InventoryOperationResult.Success();
        }

        /// <summary>
        /// 执行商店卖出交易。
        /// 只有来源背包真实移除物品后才增加金钱，避免“物品没删但钱增加”的结果漂移。
        /// </summary>
        public InventoryOperationResult ExecuteShopSale(
            InventoryOwnerHandle sourceOwner,
            Shop shop,
            Item item)
        {
            EnsureValidInventoryOwner(sourceOwner, nameof(ExecuteShopSale));
            EnsureValidShopTransaction(shop, item, nameof(ExecuteShopSale));

            if (!item.sellable)
            {
                return InventoryOperationResult.Failed(EInventoryOperationFailureReason.ItemNotSellable);
            }

            if (!HasItemInBag(sourceOwner, item, 1))
            {
                return InventoryOperationResult.Failed(EInventoryOperationFailureReason.InsufficientQuantity);
            }

            int sellingPrice = shop.GetPrice(item, ETransactionType.Sell);
            if (!RemoveFromBag(sourceOwner, item, 1, EItemTransferType.Trading))
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] 商店卖出前已确认 {sourceOwner} 拥有 {item.name}，但正式移除失败。");
            }

            AddMoney(sellingPrice);
            return InventoryOperationResult.Success();
        }

        /// <summary>
        /// 执行制作交易。
        /// 所有资金、材料和产物配置先验证，再统一扣款、扣料、写入产物。
        /// </summary>
        public InventoryOperationResult ExecuteCraftRecipe(
            InventoryOwnerHandle owner,
            Recipe recipe,
            int craftCost)
        {
            EnsureValidInventoryOwner(owner, nameof(ExecuteCraftRecipe));
            EnsureValidRecipe(recipe, nameof(ExecuteCraftRecipe));
            EnsureValidMoneyPayment(craftCost, nameof(ExecuteCraftRecipe));

            if (!HasSufficientFunds(craftCost))
            {
                return InventoryOperationResult.Failed(EInventoryOperationFailureReason.InsufficientFunds);
            }

            foreach (KeyValuePair<Item, int> requirement in recipe.GetIngredients())
            {
                if (!HasItemInBag(owner, requirement.Key, requirement.Value))
                {
                    return InventoryOperationResult.Failed(EInventoryOperationFailureReason.InsufficientIngredients);
                }
            }

            InventoryOperationResult paymentResult = ExecuteMoneyPayment(craftCost);
            if (!paymentResult.Succeeded)
            {
                return paymentResult;
            }

            foreach (KeyValuePair<Item, int> requirement in recipe.GetIngredients())
            {
                if (!RemoveFromBag(owner, requirement.Key, requirement.Value, EItemTransferType.Crafting))
                {
                    throw new InvalidOperationException(
                        $"[{nameof(InventorySystem)}] 制作前已确认材料充足，但从 {owner} 扣除 {requirement.Key.name} x{requirement.Value} 失败。");
                }
            }

            AddToBag(owner, recipe.item, recipe.quantity, EItemTransferType.Crafting);

            foreach (KeyValuePair<Item, int> entry in recipe.GetAdditionalOutput())
            {
                AddToBag(owner, entry.Key, entry.Value, EItemTransferType.Crafting);
            }

            return InventoryOperationResult.Success();
        }

        private static void EnsureValidMoneyPayment(int amount, string operationName)
        {
            if (amount < 0)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] {operationName} 需要非负付款金额，当前金额={amount}。");
            }
        }

        /// <summary>
        /// 执行宝箱首次开启的库存初始化。
        /// 先验证全部掉落配置，再把物品写入容器背包并把金钱写入队伍钱包。
        /// </summary>
        public void ExecuteChestLootInitialization(InventoryOwnerHandle containerOwner, ChestLoot loot)
        {
            EnsureValidInventoryOwner(containerOwner, nameof(ExecuteChestLootInitialization));
            ChestLootEntry[] entries = loot.GetEntries();
            EnsureValidChestLoot(entries, loot.money);

            foreach (ChestLootEntry entry in entries)
            {
                AddToBag(containerOwner, entry.item, entry.quantity, EItemTransferType.Chest);
            }

            if (loot.money > 0)
            {
                AddMoney(loot.money);
            }
        }

        /// <summary>
        /// 执行击杀奖励中的库存写入。
        /// 先验证本次实际掉落和金钱奖励，再统一写入接收者背包和队伍钱包。
        /// </summary>
        public void ExecuteLootReward(
            InventoryOwnerHandle destinationOwner,
            IReadOnlyList<Loot> grantedLoot,
            int moneyReward,
            EItemTransferType transferType)
        {
            EnsureValidInventoryOwner(destinationOwner, nameof(ExecuteLootReward));
            EnsureValidLootReward(grantedLoot, moneyReward);

            foreach (Loot loot in grantedLoot)
            {
                AddToBag(destinationOwner, loot.item, loot.quantity, transferType);
            }

            if (moneyReward > 0)
            {
                AddMoney(moneyReward);
            }
        }

        private static void EnsureValidShopTransaction(Shop shop, Item item, string operationName)
        {
            if (!shop)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] {operationName} 需要有效商店配置。");
            }

            if (!item)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] {operationName} 需要有效交易物品，不能把空物品当成交易成功。");
            }
        }

        private static void EnsureValidRecipe(Recipe recipe, string operationName)
        {
            if (!recipe)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] {operationName} 需要有效配方，不能把空配方当成制作结果。");
            }

            recipe.EnsureCraftConfiguration();
        }

        private static void EnsureValidLootReward(IReadOnlyList<Loot> grantedLoot, int moneyReward)
        {
            if (grantedLoot == null)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] 击杀奖励掉落列表不能为 null。");
            }

            if (moneyReward < 0)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] 击杀奖励金钱不能为负数，当前数量={moneyReward}。");
            }

            foreach (Loot loot in grantedLoot)
            {
                if (!loot.item || loot.quantity <= 0)
                {
                    throw new InvalidOperationException(
                        $"[{nameof(InventorySystem)}] 击杀奖励掉落配置无效，物品必须存在且数量必须大于 0。");
                }
            }
        }

        private static void EnsureValidChestLoot(ChestLootEntry[] entries, int money)
        {
            if (money < 0)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] 宝箱金钱掉落不能为负数，当前数量={money}。");
            }

            foreach (ChestLootEntry entry in entries)
            {
                if (!entry.item || entry.quantity <= 0)
                {
                    throw new InvalidOperationException(
                        $"[{nameof(InventorySystem)}] 宝箱掉落配置无效，物品必须存在且数量必须大于 0。");
                }
            }
        }

        public Equipment GetEquipment(CharacterEquipment equipmentTarget, EEquipmentType type)
        {
            if (equipmentTarget == null)
            {
                return null;
            }

            if (equipmentTarget.TryGetEquipment(type, out Equipment equipment))
            {
                return equipment;
            }

            return null;
        }

        public EEquipmentOperationResult TryEquip(InventoryOwnerHandle sourceOwner, CharacterEquipment equipmentTarget, Equipment equipment)
        {
            Debug.Assert(equipment, "Cannot equip a null equipment");

            if (equipmentTarget == null || equipmentTarget.Character == null)
            {
                return EEquipmentOperationResult.InvalidTarget;
            }

            if (!equipmentTarget.Character.Can(EActionFlags.ChangeEquipment))
            {
                return EEquipmentOperationResult.ActionLocked;
            }

            if (!HasItemInBag(sourceOwner, equipment, 1))
            {
                return EEquipmentOperationResult.MissingItem;
            }

            EEquipmentOperationResult result = equipmentTarget.TryEquip(equipment, out Equipment previousEquipment);

            if (result == EEquipmentOperationResult.Valid)
            {
                RemoveFromBag(sourceOwner, equipment, 1, EItemTransferType.Equipment);

                if (previousEquipment)
                {
                    AddToBag(sourceOwner, previousEquipment, 1, EItemTransferType.Equipment);
                }
            }

            return result;
        }

        public EEquipmentOperationResult TryUnequip(InventoryOwnerHandle destinationOwner, CharacterEquipment equipmentTarget, EEquipmentType type)
        {
            EnsureValidInventoryOwner(destinationOwner, nameof(TryUnequip));

            if (equipmentTarget == null || equipmentTarget.Character == null)
            {
                return EEquipmentOperationResult.InvalidTarget;
            }

            if (!equipmentTarget.Character.Can(EActionFlags.ChangeEquipment))
            {
                return EEquipmentOperationResult.ActionLocked;
            }

            EEquipmentOperationResult result = equipmentTarget.TryUnequip(type, out Equipment previousEquipment);

            if (result == EEquipmentOperationResult.Valid && previousEquipment != null)
            {
                AddToBag(destinationOwner, previousEquipment, 1, EItemTransferType.Equipment);
            }

            return result;
        }

        private static void EnsureValidInventoryOwner(InventoryOwnerHandle owner, string operationName)
        {
            if (!owner.IsValid)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] {operationName} 需要有效背包 owner，不能先改变装备状态再丢失回包目标。");
            }
        }

        public void AddToBag(Item item, int quantity = 1, EItemTransferType source = EItemTransferType.Unknown)
        {
            AddToBag(InventoryOwnerHandle.DefaultParty, item, quantity, source);
        }

        public void AddToBag(InventoryOwnerHandle owner, Item item, int quantity = 1, EItemTransferType source = EItemTransferType.Unknown)
        {
            EnsureValidInventoryWrite(item, quantity, nameof(AddToBag));

            Dictionary<Item, int> items = GetInventory(owner).items;
            if (!items.ContainsKey(item))
            {
                items.Add(item, quantity);
            }
            else
            {
                items[item] += quantity;
            }

            GameRuntimeEvents.NotifyInventoryItemAdded(owner, item, quantity, source);
        }

        public bool RemoveFromBag(Item item, int quantity = 1, EItemTransferType transferType = EItemTransferType.Unknown)
        {
            return RemoveFromBag(InventoryOwnerHandle.DefaultParty, item, quantity, transferType);
        }

        public bool RemoveFromBag(InventoryOwnerHandle owner, Item item, int quantity = 1, EItemTransferType transferType = EItemTransferType.Unknown)
        {
            EnsureValidInventoryWrite(item, quantity, nameof(RemoveFromBag));

            Dictionary<Item, int> items = GetInventory(owner).items;

            if (!items.TryGetValue(item, out int currentQuantity))
            {
                return false;
            }

            int removedQuantity = math.min(quantity, currentQuantity);

            if (removedQuantity >= currentQuantity)
            {
                items.Remove(item);
            }
            else
            {
                items[item] -= removedQuantity;
            }

            GameRuntimeEvents.NotifyInventoryItemRemoved(owner, item, removedQuantity, transferType);

            return true;
        }

        private static void EnsureValidInventoryWrite(Item item, int quantity, string operationName)
        {
            if (!item)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] {operationName} 需要有效物品配置，不能把空物品当成成功写入。");
            }

            if (quantity <= 0)
            {
                throw new InvalidOperationException(
                    $"[{nameof(InventorySystem)}] {operationName} 需要正数物品数量，当前数量={quantity}。");
            }
        }

        public bool TransferItem(
            InventoryOwnerHandle sourceOwner,
            InventoryOwnerHandle destinationOwner,
            Item item,
            int quantity = 1,
            EItemTransferType transferType = EItemTransferType.Unknown)
        {
            InventoryTransferResult result = ExecuteTransfer(new InventoryTransferRequest(
                null,
                sourceOwner,
                destinationOwner,
                item,
                quantity,
                transferType));
            return result.Succeeded;
        }

        public InventoryTransferResult ExecuteTransfer(InventoryTransferRequest request)
        {
            EInventoryTransferFailureReason failureReason = ValidateTransferRequest(request);
            if (failureReason != EInventoryTransferFailureReason.None)
            {
                return InventoryTransferResult.Failed(request, failureReason);
            }

            if (request.SourceOwner.Equals(request.DestinationOwner))
            {
                return InventoryTransferResult.Success(request, 0);
            }

            RemoveFromBag(request.SourceOwner, request.Item, request.Quantity, request.TransferType);
            AddToBag(request.DestinationOwner, request.Item, request.Quantity, request.TransferType);
            return InventoryTransferResult.Success(request, request.Quantity);
        }

        private EInventoryTransferFailureReason ValidateTransferRequest(InventoryTransferRequest request)
        {
            if (!request.Item)
            {
                return EInventoryTransferFailureReason.InvalidItem;
            }

            if (request.Quantity <= 0)
            {
                return EInventoryTransferFailureReason.InvalidQuantity;
            }

            if (!request.SourceOwner.IsValid)
            {
                return EInventoryTransferFailureReason.InvalidSourceOwner;
            }

            if (!request.DestinationOwner.IsValid)
            {
                return EInventoryTransferFailureReason.InvalidDestinationOwner;
            }

            if (!CanActorParticipateInTransfer(request))
            {
                return EInventoryTransferFailureReason.ActorNotParticipant;
            }

            if (!CanActorManageInventory(request))
            {
                return EInventoryTransferFailureReason.ActorActionLocked;
            }

            if (!HasItemInBag(request.SourceOwner, request.Item, request.Quantity))
            {
                return EInventoryTransferFailureReason.InsufficientQuantity;
            }

            return EInventoryTransferFailureReason.None;
        }

        private bool CanActorParticipateInTransfer(InventoryTransferRequest request)
        {
            if (!request.Actor)
            {
                return true;
            }

            InventoryOwnerHandle actorOwner = GetOwner(request.Actor);
            return actorOwner.Equals(request.SourceOwner) || actorOwner.Equals(request.DestinationOwner);
        }

        private static bool CanActorManageInventory(InventoryTransferRequest request)
        {
            return !request.Actor || request.Actor.Can(EActionFlags.ManageInventory);
        }

        private bool TransferAllItems(
            InventoryOwnerHandle sourceOwner,
            InventoryOwnerHandle destinationOwner,
            EItemTransferType transferType = EItemTransferType.Unknown)
        {
            bool transferredAny = false;

            foreach (KeyValuePair<Item, int> entry in GetBagEntries(sourceOwner))
            {
                transferredAny |= TransferItem(sourceOwner, destinationOwner, entry.Key, entry.Value, transferType);
            }

            return transferredAny;
        }

        public bool TransferCharacterInventoryToCorpse(CharacterBase character)
        {
            if (!character)
            {
                return false;
            }

            return TransferAllItems(
                GetOwner(character),
                GetCorpseOwner(character),
                EItemTransferType.Corpse);
        }

        public bool TransferCharacterEquipmentToCorpse(CharacterBase character)
        {
            if (character == null ||
                !character.TryGetComponent(out CharacterEquipment equipmentComponent) ||
                equipmentComponent == null)
            {
                return false;
            }

            bool transferredAny = false;
            InventoryOwnerHandle corpseOwner = GetCorpseOwner(character);
            EnsureValidInventoryOwner(corpseOwner, nameof(TransferCharacterEquipmentToCorpse));
            foreach (Equipment equipment in equipmentComponent.ForceUnequipAllEquipmentForLifecycle())
            {
                AddToBag(corpseOwner, equipment, 1, EItemTransferType.Corpse);
                transferredAny = true;
            }

            return transferredAny;
        }

        public bool TransferCorpseInventoryToCharacter(CharacterBase character)
        {
            if (!character)
            {
                return false;
            }

            return TransferAllItems(
                GetCorpseOwner(character),
                GetOwner(character),
                EItemTransferType.Corpse);
        }

        public void LoadDataBlock(InventoryDataBlock block)
        {
            m_inventories.Clear();

            if (block == null)
            {
                return;
            }

            LoadOwnerDataBlocks(block.inventories);
        }

        public InventoryDataBlock CreateDataBlock()
        {
            return new InventoryDataBlock
            {
                inventories = CreateOwnerDataBlocks()
            };
        }

        private InventoryRuntime GetInventory(InventoryOwnerHandle owner)
        {
            if (!owner.IsValid)
            {
                throw new InvalidOperationException($"[{nameof(InventorySystem)}] 不能为无效背包 owner 创建或读取库存。");
            }

            if (!m_inventories.TryGetValue(owner, out InventoryRuntime inventory))
            {
                inventory = new InventoryRuntime();
                m_inventories.Add(owner, inventory);
            }

            return inventory;
        }

        private void LoadOwnerDataBlocks(InventoryOwnerDataBlock[] ownerDataBlocks)
        {
            if (ownerDataBlocks == null)
            {
                return;
            }

            foreach (InventoryOwnerDataBlock ownerDataBlock in ownerDataBlocks)
            {
                if (ownerDataBlock == null)
                {
                    continue;
                }

                InventoryOwnerHandle owner = new(ownerDataBlock.ownerKind, ownerDataBlock.ownerId);
                if (!owner.IsValid)
                {
                    Debug.LogError($"[{nameof(InventorySystem)}] 存档中存在无效背包 owner，已跳过。Kind={ownerDataBlock.ownerKind}");
                    continue;
                }

                InventoryRuntime inventory = GetInventory(owner);
                inventory.money = ownerDataBlock.money;
                inventory.items.Clear();

                if (ownerDataBlock.items == null)
                {
                    continue;
                }

                foreach ((DatabaseEntryReference<Item> itemReference, int quantity) in ownerDataBlock.items)
                {
                    Item item = GameManager.Database.LoadFromReference(itemReference);
                    if (!item)
                    {
                        Debug.LogError($"[{nameof(InventorySystem)}] 存档背包 {owner} 中存在无法解析的物品 GUID：{itemReference?.guid}");
                        continue;
                    }

                    inventory.items[item] = quantity;
                }
            }
        }

        private InventoryOwnerDataBlock[] CreateOwnerDataBlocks()
        {
            List<InventoryOwnerDataBlock> blocks = new();
            foreach ((InventoryOwnerHandle owner, InventoryRuntime inventory) in m_inventories)
            {
                if (!owner.IsValid)
                {
                    throw new InvalidOperationException(
                        $"[{nameof(InventorySystem)}] 内存中存在无效背包 owner，不能把当前运行时库存保存成部分存档。Kind={owner.Kind}");
                }

                blocks.Add(new InventoryOwnerDataBlock
                {
                    ownerKind = owner.Kind,
                    ownerId = owner.Id,
                    money = inventory.money,
                    items = CreateItemDataBlock(owner, inventory)
                });
            }

            return blocks.ToArray();
        }

        private static SerializableDictionary<DatabaseEntryReference<Item>, int> CreateItemDataBlock(
            InventoryOwnerHandle owner,
            InventoryRuntime inventory)
        {
            SerializableDictionary<DatabaseEntryReference<Item>, int> items = new();
            foreach ((Item item, int quantity) in inventory.items)
            {
                if (!item || quantity <= 0)
                {
                    throw new InvalidOperationException(
                        $"[{nameof(InventorySystem)}] 背包 {owner} 中存在无效物品或数量，不能把当前运行时库存保存成部分存档。");
                }

                if (!GameManager.Database.TryCreateReference(item, out DatabaseEntryReference<Item> itemReference))
                {
                    Debug.LogError($"[{nameof(InventorySystem)}] 背包 {owner} 中的物品 {item.name} 未登记，已跳过。", item);
                    continue;
                }

                items.Add(itemReference, quantity);
            }

            return items;
        }
    }
}
