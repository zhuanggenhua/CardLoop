using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace GameCore
{
    public class UIStat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] protected TextMeshProUGUI m_value = null;

        [Header("属性")]
        [LabelText("显示属性")]
        [Tooltip("选择要显示的 EX-GAS FightUnit 属性；显示角色 ASC 中的基础值。")]
        [CharacterAttributeCode]
        [SerializeField] protected int m_attributeCode;

        public int attributeCode => m_attributeCode;

        public void UpdateUI(CharacterBase target)
        {
            UpdateValue(target != null ? target.GetAttributeBaseValue(m_attributeCode) : 0.0f);
        }

        protected void UpdateValue(float value)
        {
            m_value.text = value.ToString("0.##");
        }
    }
}

