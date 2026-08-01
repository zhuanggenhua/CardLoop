using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIIngredientEntry : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image m_image = null;
        [SerializeField] private TextMeshProUGUI m_name = null;
        [SerializeField] private TextMeshProUGUI m_quantity = null;

        [Header("Settings")]
        [SerializeField] private Color m_requirementMetColor;
        [SerializeField] private Color m_requirementNotMetColor;

        public void Initialize(Item item, int quantity)
        {
            Initialize(item, quantity, GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance());
        }

        public void Initialize(Item item, int quantity, CharacterBase owner)
        {
            InventoryOwnerHandle ownerHandle = GameManager.InventorySystem.GetOwner(owner);
            int itemCountInBag = GameManager.InventorySystem.GetItemCount(ownerHandle, item);

            Color textColor = itemCountInBag >= quantity ? m_requirementMetColor : m_requirementNotMetColor;

            m_image.sprite = item.icon;

            m_name.text = item.displayName;
            m_quantity.text = $"{itemCountInBag}/{quantity}";

            m_name.color = textColor;
            m_quantity.color = textColor;
        }
    }
}

