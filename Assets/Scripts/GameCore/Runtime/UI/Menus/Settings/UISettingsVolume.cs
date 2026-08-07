using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameCore
{
    public class UISettingsVolume : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] protected TextMeshProUGUI m_value = null;
        [SerializeField] protected Button m_decreaseButton;
        [SerializeField] protected Button m_increaseButton;

        public void UpdateUI(int volume, string suffix = "")
        {
            m_value.text = $"{volume}{suffix}";
        }

        // 只回答默认焦点对象，不把内部 Button 直接外借给外层菜单。
        public GameObject GetDefaultFocusTarget() => m_decreaseButton != null ? m_decreaseButton.gameObject : gameObject;
    }
}

