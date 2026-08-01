using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 物品使用结果，包含是否成功和成功后展示给玩家的消息。
    /// </summary>
    public struct ItemUsageResult
    {
        public bool success;
        public string message;
    }

    /// <summary>
    /// 物品效果基类，统一处理使用成功后的音效、提示文本和可选消耗。
    /// </summary>
    public abstract class AItemEffect : IItemEffect
    {
        [InspectorName("使用后消耗")]
        [Tooltip("开启后，物品效果成功执行时会从使用者背包移除 1 个该物品。")]
        [SerializeField] private bool m_consumeAfterUse = false;

        [InspectorName("使用音效")]
        [Tooltip("物品使用成功时播放的音频解析器。失败时不会播放。")]
        [SerializeField] private AudioClipResolver m_useAudio = null;

        /// <summary>
        /// 派生类实现实际效果；只有返回 success=true 时才会播放提示、音效和消耗物品。
        /// </summary>
        protected abstract ItemUsageResult OnUse(Item item, CharacterBase sourceOwner, CharacterBase target, EItemLocation location);

        /// <summary>
        /// 尝试使用物品，并在成功后执行统一的表现和背包副作用。
        /// </summary>
        public async Task<bool> TryUse(Item item, CharacterBase sourceOwner, CharacterBase target, EItemLocation location)
        {
            InventoryOwnerHandle consumptionOwner = default;
            if (m_consumeAfterUse && !TryResolveConsumptionOwner(item, sourceOwner, out consumptionOwner))
            {
                await GameManager.DialogueSystem.PlayNow(
                    MenuFeedbackPrompts.InventoryUseMissingItem,
                    item ? item.displayName : "this item");
                return true;
            }

            ItemUsageResult result = OnUse(item, sourceOwner, target, location);

            if (result.success)
            {
                if (m_consumeAfterUse)
                {
                    if (!GameManager.InventorySystem.RemoveFromBag(consumptionOwner, item, 1, EItemTransferType.Use))
                    {
                        throw new System.InvalidOperationException(
                            $"[{nameof(AItemEffect)}] 物品 {item.name} 效果已成功，但无法从来源背包 {consumptionOwner} 扣除消耗。");
                    }
                }

                GameRuntimeEvents.RequestAudioPlayback(m_useAudio);

                await GameManager.DialogueSystem.PlayNow(
                    string.IsNullOrEmpty(result.message) ?
                    $"You used {item.name}." :
                    result.message
                );

                return true;
            }

            return false;
        }

        private static bool TryResolveConsumptionOwner(
            Item item,
            CharacterBase sourceOwner,
            out InventoryOwnerHandle ownerHandle)
        {
            ownerHandle = sourceOwner
                ? GameManager.InventorySystem.GetOwner(sourceOwner)
                : InventoryOwnerHandle.DefaultParty;

            return ownerHandle.IsValid &&
                item &&
                GameManager.InventorySystem.HasItemInBag(ownerHandle, item, 1);
        }
    }
}

