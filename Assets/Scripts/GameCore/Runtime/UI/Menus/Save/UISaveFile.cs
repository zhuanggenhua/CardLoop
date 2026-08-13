using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// 存档槽按钮事件接收者，由父级存档菜单处理实际保存或读取动作。
    /// </summary>
    public interface ISaveFileEventReceiver
    {
        void HandleSaveFileClicked(SaveFileActionDesc desc);
    }

    /// <summary>
    /// 存档槽按钮的动作类型。
    /// </summary>
    public enum SaveFileActionType
    {
        Save,
        Load
    }

    /// <summary>
    /// 存档按钮点击后传给父级菜单的正式动作描述。
    /// 这里只表达“对哪个槽位做什么”，不承担存档系统真相。
    /// </summary>
    public struct SaveFileActionDesc
    {
        public SaveFileActionType action;
        public int slotId;
    }

    /// <summary>
    /// 单个存档槽 UI，负责展示槽位摘要并把点击动作交给父级菜单。
    /// </summary>
    public class UISaveFile : MonoBehaviour
    {
        [Header("设置")]
        [LabelText("动作")]
        [Tooltip("点击该槽位时执行保存还是读取。")]
        [SerializeField] private SaveFileActionType m_action = SaveFileActionType.Load;

        [LabelText("槽位编号")]
        [Tooltip("SaveKit 槽位编号，从 0 开始。槽位不通过文件名或字符串哈希推导。")]
        [Min(0)]
        [SerializeField] private int m_slotId;

        [Header("引用")]
        [LabelText("详情文本")]
        [Tooltip("显示存档摘要或 Empty 的文本控件。")]
        [SerializeField] private TextMeshProUGUI m_details = null;

        [LabelText("按钮")]
        [Tooltip("触发存档槽动作的按钮。")]
        [SerializeField] private Button m_button = null;

        private bool m_isEmpty;
        private ISaveFileEventReceiver m_receiver = null;

        private void Awake()
        {
            m_receiver = GetComponentInParent<ISaveFileEventReceiver>();
            Debug.Assert(m_receiver != null, $"{nameof(UISaveFile)} requires a parent implementing {nameof(ISaveFileEventReceiver)}.");
            m_button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (m_button)
            {
                m_button.onClick.RemoveListener(OnClick);
            }
        }
        /// <summary>
        /// 读取存档文件摘要并刷新槽位显示。
        /// </summary>
        public void UpdateUI()
        {
            SaveMeta metadata = SaveSystem.GetSaveMetadata(m_slotId);

            if (metadata.Version > 0)
            {
                m_details.text = string.IsNullOrWhiteSpace(metadata.DisplayName)
                    ? $"Slot {metadata.SlotId + 1:D3}"
                    : metadata.DisplayName;
                m_isEmpty = false;
                m_button.interactable = true;
            }
            else
            {
                m_details.text = "Empty";
                m_isEmpty = true;

                if (m_action == SaveFileActionType.Load)
                {
                    m_button.interactable = false;
                }
            }
        }

        /// <summary>
        /// 当前槽位是否存在可删除的存档数据。
        /// </summary>
        public bool CanEraseSaveData() => !m_isEmpty;

        /// <summary>
        /// 删除该槽位对应的存档文件。
        /// </summary>
        public void EraseSaveData()
        {
            if (SaveSystem.DeleteSaveData(m_slotId))
            {
                UpdateUI();
            }
        }

        /// <summary>
        /// 把点击动作描述交给父级存档菜单处理。
        /// </summary>
        public void OnClick()
        {
            m_receiver.HandleSaveFileClicked(new SaveFileActionDesc
            {
                action = m_action,
                slotId = m_slotId

            });
        }
    }
}


