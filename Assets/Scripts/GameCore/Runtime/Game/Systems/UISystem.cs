using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UISystem : AGameSystem
    {
        [Header("References")]
        [SerializeField] private GameObject m_uiPrefab;

        private GameObject m_uiInstance = null;

        public override void OnSystemStart()
        {
            ShowUI();
        }

        // Called after gameplay has been initialized properly.
        // We do this to make sure the UI, when it's created, is created after the gameplay has been initialized.
        // As the UI might depend on some gameplay data.
        public override void OnSaveFileLoaded()
        {
            ShowUI();
        }

        public void ShowUI()
        {
            if (m_uiInstance == null)
            {
                m_uiInstance = Instantiate(m_uiPrefab, transform);
            }
            else
            {
                m_uiInstance.SetActive(true);
            }

            // 正式场景不允许继续靠运行时重复节点而“看起来能跑”。
            FormalSceneSingletonConflictDiagnostics.ReportFormalSceneSingletonConflicts($"{nameof(UISystem)}.{nameof(ShowUI)}");
        }

        public void HideUI()
        {
            if (m_uiInstance != null)
            {
                m_uiInstance.SetActive(false);
            }
        }
    }
}

