using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class ItemEquipOrUnequip : IItemEffect
    {
        [SerializeField] private AudioClipResolver m_equipSound = null;
        [SerializeField] private AudioClipResolver m_unEquipSound = null;

        private void OnOperationSuccess(EOperationType operationType)
        {
            AudioClipResolver audioClip =
                operationType == EOperationType.Equip ?
                m_equipSound :
                m_unEquipSound;

            if (audioClip)
            {
                GameRuntimeEvents.RequestAudioPlayback(audioClip);
            }
        }

        private string GetReason(EEquipmentOperationResult operationResult)
        {
            Debug.Assert(operationResult != EEquipmentOperationResult.Valid, "This method should only be called when the operation result is not valid");

            switch (operationResult)
            {
                case EEquipmentOperationResult.NotEnoughHealth: return "this could kill you!";
                case EEquipmentOperationResult.NotEnoughMana: return "this could leave you with less than no <mana>!";
                case EEquipmentOperationResult.InvalidTarget: return "this character cannot equip items!";
                case EEquipmentOperationResult.MissingItem: return "the source inventory no longer has it!";
                case EEquipmentOperationResult.ActionLocked: return "you can't change equipment right now!";
            }

            return "trust me!";
        }

        private Task OnOperationFailure(EOperationType operationType, EEquipmentOperationResult operationResult)
        {
            string operation = operationType.ToString().ToLower();
            string reason = GetReason(operationResult);
            return GameManager.DialogueSystem.PlayNow($"You can't {operation} this item, {reason}");
        }

        public async Task<bool> TryUse(Item item, CharacterBase sourceOwner, CharacterBase target, EItemLocation location)
        {
            Debug.Assert(item is Equipment, "");

            Equipment equipment = (Equipment)item;
            InventorySystem inventorySystem = GameManager.InventorySystem;

            EOperationType operationType =
                location == EItemLocation.Bag ?
                EOperationType.Equip :
                EOperationType.Unequip;

            EEquipmentOperationResult operationResult;
            if (sourceOwner && !sourceOwner.Can(EActionFlags.ManageInventory))
            {
                operationResult = EEquipmentOperationResult.ActionLocked;
            }
            else if (target == null || !target.TryGetComponent(out CharacterEquipment equipmentTarget) || equipmentTarget == null)
            {
                operationResult = EEquipmentOperationResult.InvalidTarget;
            }
            else
            {
                InventoryOwnerHandle sourceOwnerHandle = sourceOwner
                    ? inventorySystem.GetOwner(sourceOwner)
                    : InventoryOwnerHandle.DefaultParty;
                operationResult = operationType == EOperationType.Equip
                    ? inventorySystem.TryEquip(sourceOwnerHandle, equipmentTarget, equipment)
                    : inventorySystem.TryUnequip(sourceOwnerHandle, equipmentTarget, equipment.type);
            }

            if (operationResult == EEquipmentOperationResult.Valid)
            {
                OnOperationSuccess(operationType);
            }
            else
            {
                await OnOperationFailure(operationType, operationResult);
            }

            return true;
        }
    }
}

