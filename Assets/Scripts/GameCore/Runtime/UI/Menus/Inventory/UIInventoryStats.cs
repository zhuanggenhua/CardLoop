using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UIInventoryStats : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIStat[] m_stats = null;

        private CharacterBase m_target = null;

        private void OnEnable()
        {
            UpdateUI(m_target);
        }

        private void Start()
        {
            UpdateUI(m_target);
        }

        public void UpdateUI(CharacterBase target)
        {
            m_target = target;

            foreach (UIStat stat in m_stats)
            {
                stat.UpdateUI(m_target);
            }
        }
    }
}

