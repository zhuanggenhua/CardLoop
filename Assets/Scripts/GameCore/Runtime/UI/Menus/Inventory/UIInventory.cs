using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UIInventory : UIKitMenuPanelBase, IInventoryBagItemClickHandler
    {
        [SerializeField] private UIInventoryEquipment m_equipment = null;
        [SerializeField] private UIInventoryBag m_bag = null;
        [SerializeField] private UIInventoryStats m_stats = null;

        private InventoryMenuContext m_context = InventoryMenuContext.CurrentControlledCharacter();
        private bool m_currentControlledCharacterListening = false;

        protected override void OnPanelInit()
        {
            m_bag.Init();
        }

        private void OnDestroy()
        {
            StopCurrentControlledCharacterListening();
        }

        protected override void OnPanelOpened(UIKitMenuOpenData openData)
        {
            m_context = TryResolveInventoryContext(openData, out InventoryMenuContext context)
                ? context
                : InventoryMenuContext.CurrentControlledCharacter();
        }

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            BindCurrentControlledCharacterListenerForContext();
            UpdateUI();
        }

        protected override void OnPanelHidden()
        {
            StopCurrentControlledCharacterListening();
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            UINavigationCursorTarget bagNavigationTarget = m_bag.FindNavigationTarget();

            if (bagNavigationTarget && bagNavigationTarget.gameObject.activeInHierarchy)
            {
                return bagNavigationTarget.gameObject;
            }
            else
            {
                UINavigationCursorTarget equipmentNavigationTarget = m_equipment.FindNavigationTarget();

                if (equipmentNavigationTarget && equipmentNavigationTarget.isActiveAndEnabled)
                {
                    return equipmentNavigationTarget.gameObject;
                }
            }

            return null;
        }

        // 子节点现在通过显式父级方法回调这里，不再依赖 SendMessageUpwards 字符串传播。
        private void UpdateUI()
        {
            CharacterBase actor = m_context.ResolveActor();
            InventoryOwnerHandle displayOwner = m_context.ResolveDisplayOwner();

            m_bag.UpdateSlots(displayOwner);
            m_equipment.UpdateSlots(actor);
            m_stats.UpdateUI(actor);
        }

        private void OnItemClicked(Item item, EItemLocation location)
        {
            RunPanelTaskAndReport(OnItemClickedAsync(item, location), nameof(OnItemClicked));
        }

        private async System.Threading.Tasks.Task OnItemClickedAsync(Item item, EItemLocation location)
        {
            CharacterBase actor = m_context.ResolveActor();
            if (actor == null)
            {
                return;
            }

            if (m_context.Mode == EInventoryMenuMode.TransferToDestination && location == EItemLocation.Bag)
            {
                InventoryTransferResult result = GameManager.InventorySystem.ExecuteTransfer(
                    m_context.CreateTransferRequest(item, 1));

                if (result.Succeeded)
                {
                    UpdateUI();
                }
                else
                {
                    Debug.LogWarning($"Inventory transfer failed: {result.FailureReason}", this);
                    string prompt = result.FailureReason switch
                    {
                        EInventoryTransferFailureReason.ActorNotParticipant =>
                            MenuFeedbackPrompts.InventoryTransferActorNotParticipant,
                        EInventoryTransferFailureReason.ActorActionLocked =>
                            MenuFeedbackPrompts.InventoryTransferActionLocked,
                        _ => MenuFeedbackPrompts.InventoryTransferFailed
                    };
                    await GameManager.DialogueSystem.PlayNow(prompt, item.displayName);
                }

                return;
            }

            if (m_context.Mode != EInventoryMenuMode.UseOwnerItems)
            {
                return;
            }

            CharacterBase target = ResolveItemUseTarget(actor, item);
            await item.Use(actor, target, location);
            UpdateUI();
        }

        public void HandleBagItemClicked(Item item) => OnItemClicked(item, EItemLocation.Bag);
        public void HandleEquipmentItemClicked(Item item) => OnItemClicked(item, EItemLocation.Equipment);

        private void OnCurrentControlledCharacterChanged(CharacterBase character)
        {
            if (m_context.FollowsCurrentControlledCharacter && gameObject.activeInHierarchy)
            {
                UpdateUI();
            }
        }

        private void BindCurrentControlledCharacterListenerForContext()
        {
            if (m_context.FollowsCurrentControlledCharacter)
            {
                StartCurrentControlledCharacterListeningIfReady();
            }
            else
            {
                StopCurrentControlledCharacterListening();
            }
        }

        private void StartCurrentControlledCharacterListeningIfReady()
        {
            if (m_currentControlledCharacterListening)
            {
                return;
            }

            if (!GameManager.Exists() || !GameManager.HasSystem<PlayerSystem>())
            {
                return;
            }

            m_currentControlledCharacterListening = true;
            GameManager.PlayerSystem.AddCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
        }

        private void StopCurrentControlledCharacterListening()
        {
            if (!m_currentControlledCharacterListening)
            {
                return;
            }

            m_currentControlledCharacterListening = false;
            if (GameManager.Exists() && GameManager.HasSystem<PlayerSystem>())
            {
                GameManager.PlayerSystem.RemoveCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        private static bool TryResolveInventoryContext(UIKitMenuOpenData openData, out InventoryMenuContext context)
        {
            context = InventoryMenuContext.CurrentControlledCharacter();
            if (openData == null || openData.ArgumentCount != 1)
            {
                return false;
            }

            return openData.TryGetArgument(0, out context);
        }

        private static CharacterBase ResolveItemUseTarget(CharacterBase actor, Item item)
        {
            if (actor == null)
            {
                return null;
            }

            return actor;
        }
    }
}

