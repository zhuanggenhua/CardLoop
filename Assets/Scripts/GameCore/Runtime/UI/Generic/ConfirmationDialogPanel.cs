using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// 项目通用的 UIKit 确认对话框皮肤；对话框排队、模态和结果生命周期仍由 UIKit 管理。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConfirmationDialogPanel : UIDialogPanel
    {
        [Header("对话框组件")]
        [SerializeField, Tooltip("显示本次确认操作的标题。")]
        private TMP_Text m_titleLabel;

        [SerializeField, Tooltip("显示本次确认操作的说明和不可逆后果。")]
        private TMP_Text m_messageLabel;

        [SerializeField, Tooltip("确认当前操作。")]
        private Button m_confirmButton;

        [SerializeField, Tooltip("关闭对话框且不执行操作。")]
        private Button m_cancelButton;

        [SerializeField, Tooltip("确认按钮的可见文字。")]
        private TMP_Text m_confirmLabel;

        [SerializeField, Tooltip("取消按钮的可见文字。")]
        private TMP_Text m_cancelLabel;

        protected override void OnInit(IUIData data = null)
        {
            if (m_titleLabel == null || m_messageLabel == null ||
                m_confirmButton == null || m_cancelButton == null ||
                m_confirmLabel == null || m_cancelLabel == null)
            {
                throw new InvalidOperationException("通用确认对话框预制体缺少必要 UI 引用。");
            }
        }

        protected override void SetupDialog(DialogConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            m_titleLabel.text = string.IsNullOrWhiteSpace(config.Title) ? "请确认" : config.Title;
            m_messageLabel.text = config.Message ?? string.Empty;
            ConfigureResultButton(
                m_confirmButton,
                m_confirmLabel,
                DialogButtonType.OK,
                config.OKText,
                "确定",
                OnOKClicked);
            ConfigureResultButton(
                m_cancelButton,
                m_cancelLabel,
                DialogButtonType.Cancel,
                config.CancelText,
                "取消",
                OnCancelClicked);
        }

        protected override void ClearUIComponents()
        {
            m_confirmButton?.onClick.RemoveAllListeners();
            m_cancelButton?.onClick.RemoveAllListeners();
        }

        private void ConfigureResultButton(
            Button button,
            TMP_Text label,
            DialogButtonType type,
            string configuredText,
            string defaultText,
            Action onClick)
        {
            bool visible = (mConfig.Buttons & type) != 0;
            button.gameObject.SetActive(visible);
            button.onClick.RemoveAllListeners();
            if (!visible)
            {
                return;
            }

            label.text = string.IsNullOrWhiteSpace(configuredText) ? defaultText : configuredText;
            button.onClick.AddListener(() => onClick());
        }
    }
}
