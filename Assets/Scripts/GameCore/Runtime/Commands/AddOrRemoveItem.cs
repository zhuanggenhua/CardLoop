using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 向上下文角色背包添加或移除指定物品。
    /// </summary>
    [Serializable]
    public class AddOrRemoveItem : IContextualCommand
    {
        [InspectorName("动作")]
        [Tooltip("决定添加还是移除物品。")]
        [SerializeField] private EAction m_action = EAction.Add;

        [InspectorName("物品")]
        [Tooltip("要添加或移除的物品资产。")]
        [SerializeField] private Item m_item = null;

        [InspectorName("数量")]
        [Tooltip("要添加或移除的数量。")]
        [SerializeField][Min(1)] private int m_quantity = 1;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            Debug.Assert(m_quantity != 0, "Invalid quantity! Expected != 0");
            CharacterBase inventoryOwner =
                context.ResolveRequiredActorOrCurrentControlledCharacter(nameof(AddOrRemoveItem));

            InventoryOwnerHandle ownerHandle = GameManager.InventorySystem.GetOwner(inventoryOwner);
            switch (m_action)
            {
                case EAction.Add:
                    GameManager.InventorySystem.AddToBag(ownerHandle, m_item, m_quantity, EItemTransferType.Command);
                    break;

                case EAction.Remove:
                    if (!GameManager.InventorySystem.RemoveFromBag(ownerHandle, m_item, m_quantity, EItemTransferType.Command))
                    {
                        throw new InvalidOperationException(
                            $"[{nameof(AddOrRemoveItem)}] 无法从 {ownerHandle} 移除 {m_item.name} x{m_quantity}，不能把命令结果当作成功。");
                    }
                    break;
            }

            return Task.CompletedTask;
        }
    }
}

