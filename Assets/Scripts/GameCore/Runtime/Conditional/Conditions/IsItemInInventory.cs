using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using YokiFrame;

namespace FantasyWord.GameCore
{
    [Serializable]
    [MovedFrom(false, null, "FantasyWord.GameCore.Conditions")]
    public class IsItemInInventory : ABaseCondition
    {
        [SerializeField] private Item m_item = null;
        [SerializeField] private EInventoryQueryScope m_inventoryScope = EInventoryQueryScope.Party;

        public override bool Evaluate()
        {
            if (!TryGetInventorySystem(out InventorySystem inventorySystem) ||
                !TryGetInventoryOwner(inventorySystem, out InventoryOwnerHandle owner))
            {
                return false;
            }

            return inventorySystem.HasItemInBag(owner, m_item);
        }

        protected override void OnStartListening()
        {
            EventKit.Type.Register<InventoryItemAddedEvent>(OnItemAdded);
            EventKit.Type.Register<InventoryItemRemovedEvent>(OnItemRemoved);

            if (m_inventoryScope == EInventoryQueryScope.CurrentControlledCharacter &&
                TryGetPlayerSystem(out PlayerSystem playerSystem))
            {
                playerSystem.AddCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        protected override void OnStopListening()
        {
            EventKit.Type.UnRegister<InventoryItemAddedEvent>(OnItemAdded);
            EventKit.Type.UnRegister<InventoryItemRemovedEvent>(OnItemRemoved);

            if (m_inventoryScope == EInventoryQueryScope.CurrentControlledCharacter &&
                TryGetPlayerSystem(out PlayerSystem playerSystem))
            {
                playerSystem.RemoveCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        private void OnItemAdded(InventoryItemAddedEvent inventoryItemAddedEvent)
        {
            if (TryGetInventorySystem(out InventorySystem inventorySystem) &&
                IsOwnerInScope(inventorySystem, inventoryItemAddedEvent.Owner))
            {
                NotifyStateChange();
            }
        }

        private void OnItemRemoved(InventoryItemRemovedEvent inventoryItemRemovedEvent)
        {
            if (TryGetInventorySystem(out InventorySystem inventorySystem) &&
                IsOwnerInScope(inventorySystem, inventoryItemRemovedEvent.Owner))
            {
                NotifyStateChange();
            }
        }

        private void OnCurrentControlledCharacterChanged(CharacterBase character) => NotifyStateChange();

        private bool IsOwnerInScope(InventorySystem inventorySystem, InventoryOwnerHandle owner)
        {
            switch (m_inventoryScope)
            {
                case EInventoryQueryScope.CurrentControlledCharacter:
                    return TryGetInventoryOwner(inventorySystem, out InventoryOwnerHandle currentOwner) &&
                        owner.Equals(currentOwner);
                case EInventoryQueryScope.Party:
                default:
                    return owner.Equals(InventoryOwnerHandle.DefaultParty);
            }
        }

        private bool TryGetInventoryOwner(InventorySystem inventorySystem, out InventoryOwnerHandle owner)
        {
            owner = InventoryOwnerHandle.DefaultParty;
            switch (m_inventoryScope)
            {
                case EInventoryQueryScope.CurrentControlledCharacter:
                    if (!TryGetPlayerSystem(out PlayerSystem playerSystem))
                    {
                        return false;
                    }

                    CharacterBase currentControlledCharacter = playerSystem.GetCurrentControlledCharacterOrPlayerInstance();
                    if (currentControlledCharacter == null)
                    {
                        return false;
                    }

                    owner = inventorySystem.GetOwner(currentControlledCharacter);
                    return owner.IsValid;
                case EInventoryQueryScope.Party:
                default:
                    owner = InventoryOwnerHandle.DefaultParty;
                    return true;
            }
        }

        private static bool TryGetInventorySystem(out InventorySystem inventorySystem)
        {
            return GameManager.TryGetSystem(out inventorySystem);
        }

        private static bool TryGetPlayerSystem(out PlayerSystem playerSystem)
        {
            return GameManager.TryGetSystem(out playerSystem);
        }
    }
}
