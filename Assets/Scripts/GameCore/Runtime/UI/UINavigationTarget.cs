using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameCore
{
    public class UINavigationTarget : MonoBehaviour, ISelectHandler, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] private AudioClipResolver m_navigationSelectSoundOverride = null;
        [SerializeField] private AudioClipResolver m_pointerSelectSoundOverride = null;
        [SerializeField] private AudioClipResolver m_submitSoundOverride = null;

        private AudioClipResolver navigationSelectSound => m_navigationSelectSoundOverride ?? GameManager.Config.navigationSelectSound;
        private AudioClipResolver pointerSelectSound => m_pointerSelectSoundOverride ?? GameManager.Config.pointerSelectSound;
        private AudioClipResolver submitSound => m_submitSoundOverride ?? GameManager.Config.submitSound;

        private void OnSelectWithPointer()
        {
            if (pointerSelectSound)
            {
                YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(pointerSelectSound));
            }
        }

        private void OnSelectWithNavigation()
        {
            if (navigationSelectSound)
            {
                YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(navigationSelectSound));
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (eventData is AxisEventData)
            {
                OnSelectWithNavigation();
            }
            else
            {
                OnSelectWithPointer();
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            GameObject selected = eventData.selectedObject;

            if (selected != null)
            {
                Selectable selectable = selected.GetComponent<Selectable>();

                if (selectable != null && selectable.interactable && submitSound)
                {
                    YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(submitSound));
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData) => OnSubmit(eventData);
    }
}
