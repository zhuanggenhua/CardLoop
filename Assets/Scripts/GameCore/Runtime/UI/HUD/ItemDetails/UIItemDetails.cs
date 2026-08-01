using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    using YokiFrame;

    public class UIItemDetails : MonoBehaviour
    {
        // Inspector Settings
        [Header("References")]
        [SerializeField] private GameObject m_itemDetailsBox = null;
        [SerializeField] private Image m_itemIcon = null;
        [SerializeField] private TextMeshProUGUI m_itemName = null;
        [SerializeField] private TextMeshProUGUI m_itemDescription = null;

        private void Awake()
        {
            m_itemDetailsBox.SetActive(false);
        }

        /// <summary>
        /// 物品详情框只在可见期间监听打开/关闭请求，避免场景切换或 UI 复用后保留脏监听。
        /// </summary>
        private void OnEnable()
        {
            EventKit.Type.Register<ItemDetailsOpenedEvent>(OnDetailsOpened);
            EventKit.Type.Register<ItemDetailsClosedEvent>(OnDetailsClosed);
        }

        private void OnDisable()
        {
            EventKit.Type.UnRegister<ItemDetailsOpenedEvent>(OnDetailsOpened);
            EventKit.Type.UnRegister<ItemDetailsClosedEvent>(OnDetailsClosed);
            OnDetailsClosed();
        }

        private void OnDetailsOpened(ItemDetailsOpenedEvent itemDetailsOpenedEvent)
        {
            Item item = itemDetailsOpenedEvent.Item;
            if (item)
            {
                m_itemDetailsBox.SetActive(true);
                m_itemIcon.sprite = item.icon;
                m_itemName.text = item.displayName;
                m_itemDescription.text = item.description;

                if (item is Equipment)
                {
                    Equipment equipment = (Equipment)item;

                    foreach (FormalAttributeDefinition attribute in FormalAttributeCatalog.Definitions)
                    {
                        int value = equipment.GetBonusStatValue(attribute);

                        if (value != 0)
                        {
                            m_itemDescription.text += $" <u>{(value > 0 ? '+' : string.Empty)}{value}\u00A0{GameManager.Config.GetTermDefinition(attribute.Stat).shortName}</u>";
                        }
                    }
                }
            }
            else
            {
                OnDetailsClosed();
            }
        }

        private void OnDetailsClosed(ItemDetailsClosedEvent _)
        {
            OnDetailsClosed();
        }

        private void OnDetailsClosed()
        {
            m_itemDetailsBox.SetActive(false);
        }
    }
}

