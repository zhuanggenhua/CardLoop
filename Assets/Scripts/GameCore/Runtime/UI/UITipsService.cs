using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    public class UITipsService : MonoBehaviour
    {
        [SerializeField] private GameObject m_tipsItemPrefab = null;
        [SerializeField] private Transform m_tipsRoot = null;
        [SerializeField] private int m_poolSize = 4;

        private void Awake()
        {
            if (m_tipsRoot == null)
            {
                m_tipsRoot = transform;
            }

            if (m_tipsItemPrefab == null)
            {
                return;
            }

            GameObjectPoolService.SetMaxCapacity(m_tipsItemPrefab, m_poolSize);
            GameObjectPoolService.Prewarm(m_tipsItemPrefab, m_poolSize);
        }

        public UITipsItem AddTips(string tips)
        {
            if (m_tipsItemPrefab == null)
            {
                Debug.LogWarning("UITipsService missing tips item prefab.");
                return null;
            }

            GameObject instance = GameObjectPoolService.Rent(m_tipsItemPrefab, m_tipsRoot);
            UITipsItem item = instance != null ? instance.GetComponent<UITipsItem>() : null;
            if (item == null)
            {
                Debug.LogError("Tips item prefab must have a UITipsItem component.");
                GameObjectPoolService.Return(instance);
                return null;
            }

            item.Show(tips);
            return item;
        }
    }
}
