using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameCore
{
    /// <summary>
    /// 术语文本显示模式。
    /// </summary>
    public enum ETermDisplayMode
    {
        FullName,
        ShortName
    }

    /// <summary>
    /// 从 GameConfig 读取术语定义并刷新可选图标和文本的 UI 组件。
    /// </summary>
    public class UITerm : MonoBehaviour
    {
        [Header("设置")]
        [InspectorName("术语 ID")]
        [Tooltip("GameConfig 中的术语标识。")]
        [SerializeField] private string m_termID = null;

        [InspectorName("文本格式")]
        [Tooltip("用于包装术语名称的格式字符串，{0} 会替换成术语全称或简称。")]
        [SerializeField] private string m_textFormat = "{0}";

        [InspectorName("显示模式")]
        [Tooltip("选择显示术语全称还是简称。")]
        [SerializeField] private ETermDisplayMode m_displayMode = ETermDisplayMode.FullName;

        [Header("引用")]
        [InspectorName("可选图标")]
        [Tooltip("存在时会显示术语图标。")]
        [SerializeField] private Image m_optionalIcon = null;

        [InspectorName("可选文本")]
        [Tooltip("存在时会显示格式化后的术语名称。")]
        [SerializeField] private TextMeshProUGUI m_optionalLabel = null;

        public void Start()
        {
            UpdateUI();
        }

        public void UpdateUI()
        {
            TermDefinition definition = GameManager.Config.GetTermDefinition(m_termID);

            if (m_optionalIcon)
            {
                m_optionalIcon.sprite = definition.icon;
            }

            if (m_optionalLabel)
            {
                m_optionalLabel.text = string.Format(m_textFormat, m_displayMode == ETermDisplayMode.FullName ? definition.fullName : definition.shortName);
            }
        }
    }
}

