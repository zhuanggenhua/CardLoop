using System;
using System.Threading.Tasks;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    public class UISystem : AGameSystem
    {
        [Header("References")]
        [SerializeField] private GameObject m_uiPrefab;

        private GameObject m_uiInstance = null;
        private UIManager m_uiManager = null;

        public override void OnSystemStart()
        {
            ShowUI();
            EventKit.Type.Register<SaveFileLoadedEvent>(OnSaveFileLoaded);
        }

        public override void OnSystemStop()
        {
            EventKit.Type.UnRegister<SaveFileLoadedEvent>(OnSaveFileLoaded);
        }

        // Called after gameplay has been initialized properly.
        // We do this to make sure the UI, when it's created, is created after the gameplay has been initialized.
        // As the UI might depend on some gameplay data.
        private void OnSaveFileLoaded(SaveFileLoadedEvent _)
        {
            ShowUI();
        }

        public void ShowUI()
        {
            if (m_uiInstance == null)
            {
                m_uiInstance = Instantiate(m_uiPrefab, transform);
                m_uiManager = m_uiInstance.GetComponentInChildren<UIManager>(includeInactive: true);
                if (m_uiManager == null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(UISystem)} UI prefab must contain one {nameof(UIManager)}.");
                }
            }
            else
            {
                m_uiInstance.SetActive(true);
            }

        }

        public Task<bool> OpenMenuAsync(EMenu menu)
        {
            ShowUI();
            return GetRequiredUIManager().OpenMenuAsync(menu);
        }

        public void CloseAllMenus()
        {
            GetRequiredUIManager().CloseAllMenus();
        }

        public void HideUI()
        {
            if (m_uiInstance != null)
            {
                m_uiInstance.SetActive(false);
            }
        }

        private UIManager GetRequiredUIManager()
        {
            if (m_uiManager != null)
            {
                return m_uiManager;
            }

            throw new InvalidOperationException(
                $"{nameof(UISystem)} has not created its {nameof(UIManager)} runtime yet.");
        }
    }
}

