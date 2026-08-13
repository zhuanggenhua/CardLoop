using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameCore
{
    /// <summary>
    /// 游戏主菜单中的单个条目，负责在选中时更新焦点表现并在点击时请求对应菜单。
    /// </summary>
    public class UIGameMenuEntry : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        /// <summary>
        /// 游戏主菜单条目可触发的动作。
        /// </summary>
        public enum EGameMenuAction
        {
            None,
            OpenSaveMenu,
            OpenSettings
        }

        [Header("设置")]
        [LabelText("菜单动作")]
        [Tooltip("点击该条目时执行的菜单动作。")]
        [SerializeField] private EGameMenuAction m_action = EGameMenuAction.None;

        [Header("引用")]
        [LabelText("按钮")]
        [Tooltip("接收点击和焦点的按钮。")]
        [SerializeField] private Button m_button = null;

        [LabelText("文本")]
        [Tooltip("条目选中时显示的文本提示。")]
        [SerializeField] private TextMeshProUGUI m_text = null;

        private UIGameMenu m_menu = null;

        private void Awake()
        {
            m_menu = GetComponentInParent<UIGameMenu>();
            Debug.Assert(m_menu != null, $"{nameof(UIGameMenuEntry)} requires a parent {nameof(UIGameMenu)}.");
            m_button.onClick.AddListener(OnButtonClicked);
            m_text.enabled = false;
        }

        private void OnDestroy()
        {
            if (m_button)
            {
                m_button.onClick.RemoveListener(OnButtonClicked);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            m_text.enabled = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            m_text.enabled = true;
            m_menu.HandleGameMenuEntrySelected(this);
        }

        internal GameObject GetFocusTarget() => m_button != null ? m_button.gameObject : gameObject;

        private void OnButtonClicked()
        {
            switch (m_action)
            {
                case EGameMenuAction.OpenSaveMenu:
                    _ = GameManager.UISystem.OpenMenuAsync(EMenu.Save);
                    break;

                case EGameMenuAction.OpenSettings:
                    _ = GameManager.UISystem.OpenMenuAsync(EMenu.Settings);
                    break;
            }
        }
    }
}


