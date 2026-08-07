using UnityEngine;

namespace GameCore
{
    public class UIGameMenu : UIKitMenuPanelBase
    {
        [Header("References")]
        [SerializeField] private UIGameMenuEntry[] m_menus;
        [SerializeField] private GameObject[] m_disableWhileOpened = null;

        [Header("Audio")]
        [SerializeField] private AudioClipResolver m_pauseSound;
        [SerializeField] private AudioClipResolver m_resumeSound;

        private UIGameMenuEntry m_selected = null;

        protected override void OnPushedToMenuStack()
        {
            if (m_pauseSound)
            {
                YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(m_pauseSound));
            }
        }

        protected override void OnPoppedFromMenuStack()
        {
            if (m_resumeSound)
            {
                YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(m_resumeSound));
            }
        }

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            foreach (GameObject gameObject in m_disableWhileOpened)
            {
                gameObject.SetActive(false);
            }
        }

        protected override void OnPanelHidden()
        {
            foreach (GameObject gameObject in m_disableWhileOpened)
            {
                gameObject.SetActive(true);
            }
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            if (m_selected)
            {
                return m_selected.GetFocusTarget();
            }

            return null;
        }

        public void HandleGameMenuEntrySelected(UIGameMenuEntry selected)
        {
            m_selected = selected;
        }
    }
}

