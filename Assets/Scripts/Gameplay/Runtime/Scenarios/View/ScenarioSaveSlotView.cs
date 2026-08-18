using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Gameplay.Scenarios
{
    /// <summary>
    /// 单个存档槽位的纯视图；它不读取文件，也不拥有保存、读取或删除职责。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioSaveSlotView : MonoBehaviour
    {
        [Header("槽位组件")]
        [SerializeField, Tooltip("显示槽位编号、单局摘要与最后保存时间。")]
        private TMP_Text m_summaryLabel;

        [SerializeField, Tooltip("执行当前面板模式对应的保存或读取操作。")]
        private Button m_primaryButton;

        [SerializeField, Tooltip("显示当前槽位的主要操作名称。")]
        private TMP_Text m_primaryLabel;

        [SerializeField, Tooltip("删除当前槽位。")]
        private Button m_deleteButton;

        private int m_slotId;
        private Action<int> m_onPrimary;
        private Action<int> m_onDelete;

        public int SlotId => m_slotId;

        public void Bind(
            SaveMeta metadata,
            ScenarioSavePanelMode mode,
            Action<int> onPrimary,
            Action<int> onDelete)
        {
            if (m_summaryLabel == null || m_primaryButton == null ||
                m_primaryLabel == null || m_deleteButton == null)
            {
                throw new InvalidOperationException("剧本存档槽位预制体缺少必要 UI 引用。");
            }

            m_slotId = metadata.SlotId;
            m_onPrimary = onPrimary ?? throw new ArgumentNullException(nameof(onPrimary));
            m_onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
            string displayName = string.IsNullOrWhiteSpace(metadata.DisplayName)
                ? "未命名单局"
                : metadata.DisplayName;
            m_summaryLabel.text =
                $"槽位 {metadata.SlotId + 1:D2}\n{displayName}\n{metadata.GetLastSavedDateTime():yyyy-MM-dd HH:mm}";
            m_primaryLabel.text = mode == ScenarioSavePanelMode.Save ? "覆盖" : "读取";
            m_primaryButton.onClick.RemoveAllListeners();
            m_deleteButton.onClick.RemoveAllListeners();
            m_primaryButton.onClick.AddListener(InvokePrimary);
            m_deleteButton.onClick.AddListener(InvokeDelete);
        }

        private void OnDestroy()
        {
            m_primaryButton?.onClick.RemoveAllListeners();
            m_deleteButton?.onClick.RemoveAllListeners();
        }

        private void InvokePrimary()
        {
            m_onPrimary?.Invoke(m_slotId);
        }

        private void InvokeDelete()
        {
            m_onDelete?.Invoke(m_slotId);
        }
    }
}
