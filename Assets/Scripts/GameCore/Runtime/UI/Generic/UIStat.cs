using TMPro;
using UnityEngine;

namespace GameCore
{
    public class UIStat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] protected TextMeshProUGUI m_value = null;

        [Header("Settings")]
        [SerializeField] protected EStat m_stat;

        public EStat stat => m_stat;
        protected FormalAttributeDefinition definition => FormalAttributeCatalog.Get(m_stat);

        public void UpdateUI(CharacterBase target)
        {
            UpdateValue(target != null ? target.GetStatValue(definition) : 0);
        }

        protected void UpdateValue(int value)
        {
            m_value.text = value.ToString();
        }
    }
}

