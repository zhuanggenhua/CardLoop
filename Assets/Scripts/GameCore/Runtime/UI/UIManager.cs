using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 正式 UI 运行时协调器。
    /// 这里统一承接对话期间的交互门禁，以及项目菜单语义到 UIKit 原生入口的唯一正式入口。
    /// </summary>
    public sealed partial class UIManager : MonoBehaviour
    {
        [SerializeField] private CanvasGroup[] m_lockedCanvasGroupsOnDialogue = null;

        private readonly InputActionReleaseGate m_dialogueUiReleaseGate = new();
        private GameObject m_cachedSelectedObjectOnDialogue = null;
        private bool m_restoreUiAfterDialogue = false;

        private void Start()
        {
            StartDialogueRuntime();
            StartMenuRuntime();
        }

        private void OnDestroy()
        {
            StopMenuRuntime();
            StopDialogueRuntime();
        }

        private void SetCanvasGroupsInteractionState(bool enabled)
        {
            foreach (CanvasGroup group in m_lockedCanvasGroupsOnDialogue)
            {
                group.interactable = enabled;
            }
        }

        private void OnDialogueStarted(DialogueTree dialogue)
        {
            m_restoreUiAfterDialogue = false;
            m_cachedSelectedObjectOnDialogue = GameManager.EventSystem.currentSelectedGameObject;
            GameManager.EventSystem.SetSelectedGameObject(null);
            GameManager.InputSystem.PrepareUIReleaseGate(
                m_dialogueUiReleaseGate,
                EUIInputAction.Submit,
                EUIInputAction.Cancel,
                EUIInputAction.Click);
            SetCanvasGroupsInteractionState(false);
        }

        private void OnDialogueEnded(DialogueTree dialogue)
        {
            if (m_dialogueUiReleaseGate.HasBlockedActions)
            {
                m_restoreUiAfterDialogue = true;
                return;
            }

            RestoreUiAfterDialogue();
        }

        private void OnDialogueUiActionReleased(InputAction.CallbackContext context)
        {
            m_dialogueUiReleaseGate.NotifyReleased(context.action);

            if (m_restoreUiAfterDialogue && !m_dialogueUiReleaseGate.HasBlockedActions)
            {
                RestoreUiAfterDialogue();
            }
        }

        private void RestoreUiAfterDialogue()
        {
            m_restoreUiAfterDialogue = false;
            SetCanvasGroupsInteractionState(true);

            GameObject selectedObject = GetRestorableSelectedObject(m_cachedSelectedObjectOnDialogue);
            m_cachedSelectedObjectOnDialogue = null;
            GameManager.EventSystem.SetSelectedGameObject(selectedObject);
        }

        private static GameObject GetRestorableSelectedObject(GameObject selectedObject)
        {
            if (selectedObject == null || !selectedObject.activeInHierarchy)
            {
                return null;
            }

            Selectable selectable = selectedObject.GetComponent<Selectable>();
            if (selectable == null || !selectable.IsInteractable())
            {
                return null;
            }

            return selectable.gameObject;
        }

        private void StartDialogueRuntime()
        {
            GameManager.DialogueSystem.AddStartedListener(OnDialogueStarted);
            GameManager.DialogueSystem.AddEndedListener(OnDialogueEnded);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Submit, EInputActionPhase.Canceled, OnDialogueUiActionReleased);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Canceled, OnDialogueUiActionReleased);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Click, EInputActionPhase.Canceled, OnDialogueUiActionReleased);
        }

        private void StopDialogueRuntime()
        {
            GameManager.DialogueSystem.RemoveStartedListener(OnDialogueStarted);
            GameManager.DialogueSystem.RemoveEndedListener(OnDialogueEnded);
            GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Submit, EInputActionPhase.Canceled, OnDialogueUiActionReleased);
            GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Canceled, OnDialogueUiActionReleased);
            GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Click, EInputActionPhase.Canceled, OnDialogueUiActionReleased);
        }
    }
}
