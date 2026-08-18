using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace GameCore
{
    /// <summary>
    /// UI 焦点与提交音效目标；只发送表现音频请求，不执行按钮业务。
    /// </summary>
    public class UINavigationTarget : MonoBehaviour, ISelectHandler, ISubmitHandler, IPointerClickHandler
    {
        [LabelText("导航选中音效")]
        [Tooltip("手柄或键盘导航选中该 UI 时播放；为空则使用 GameConfig 默认音效。")]
        [SerializeField] private AudioClipResolver m_navigationSelectSoundOverride = null;

        [LabelText("指针选中音效")]
        [Tooltip("鼠标或触摸让该 UI 获得焦点时播放；为空则使用 GameConfig 默认音效。")]
        [SerializeField] private AudioClipResolver m_pointerSelectSoundOverride = null;

        [LabelText("提交音效")]
        [Tooltip("点击或提交该 UI 时播放；为空则使用 GameConfig 默认音效。")]
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
            PlaySubmitSoundIfInteractable(eventData.selectedObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PlaySubmitSoundIfInteractable(gameObject);
        }

        private void PlaySubmitSoundIfInteractable(GameObject target)
        {
            if (target == null || !submitSound)
            {
                return;
            }

            Selectable selectable = target.GetComponent<Selectable>();
            if (selectable != null && selectable.interactable)
            {
                YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(submitSound));
            }
        }
    }
}
