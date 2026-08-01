using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIDialogueOption : MonoBehaviour
    {
        // Inspector Settings
        [SerializeField] private TextMeshProUGUI m_text = null;
        [SerializeField] private int m_optionID = 0;

        // Component References
        private Button m_button = null;
        private IDialogueHudEventReceiver m_receiver = null;

        private void Awake()
        {
            m_button = GetComponent<Button>();
            m_receiver = GetComponentInParent<IDialogueHudEventReceiver>();
            Debug.Assert(m_receiver != null, $"{nameof(UIDialogueOption)} 需要父级实现 {nameof(IDialogueHudEventReceiver)}。");
            m_button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (m_button)
            {
                m_button.onClick.RemoveListener(OnClicked);
            }
        }
        private void OnClicked()
        {
            m_receiver?.HandleDialogueOptionClicked(m_optionID);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetText(string text)
        {
            m_text.text = text;
        }
    }
}


