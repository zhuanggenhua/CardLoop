using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameCore
{
    public class UISettingsVolume : MonoBehaviour
    {
        [Header("设置控件引用")]
        [LabelText("数值文字")]
        [Tooltip("显示当前音量数值的文本。")]
        [SerializeField] protected TextMeshProUGUI m_value = null;

        [LabelText("减少按钮")]
        [Tooltip("点击后降低该音量项。")]
        [SerializeField] protected Button m_decreaseButton;

        [LabelText("增加按钮")]
        [Tooltip("点击后提高该音量项。")]
        [SerializeField] protected Button m_increaseButton;

        public void UpdateUI(int volume, string suffix = "")
        {
            m_value.text = $"{volume}{suffix}";
        }

        // 只回答默认焦点对象，不把内部 Button 直接外借给外层菜单。
        public GameObject GetDefaultFocusTarget() => m_decreaseButton != null ? m_decreaseButton.gameObject : gameObject;
    }
}

