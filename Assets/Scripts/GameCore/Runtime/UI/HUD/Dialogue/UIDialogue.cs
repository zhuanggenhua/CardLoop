using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIDialogue : MonoBehaviour, IDialogueHudEventReceiver
    {
        // Inspector Settings
        [SerializeField] private Graphic m_interactionBlocker = null;
        [SerializeField] private UIDialogueMessageBox m_messageBox = null;
        [SerializeField] private UIDialogueChoiceBox m_choiceBox = null;

        private DialogueNode m_currentNode = null;
        private readonly InputActionReleaseGate m_skipInputReleaseGate = new();
        private bool m_dialogueRuntimeListening = false;
        private bool m_dialogueLayerApplied = false;

        private void OnEnable()
        {
            StartDialogueRuntimeIfReady();
        }

        private void Start()
        {
            StartDialogueRuntimeIfReady();
        }

        private void OnDisable()
        {
            StopDialogueRuntime();
            CloseDialogue();
        }

        private void OnDestroy()
        {
            StopDialogueRuntime();
            CloseDialogue();
        }

        private void CloseDialogue()
        {
            m_currentNode = null;
            m_interactionBlocker.gameObject.SetActive(false);
            m_messageBox.Hide();
            m_choiceBox.Hide();
            RemoveDialogueLayerIfApplied();
        }

        private void OnDialogueStarted(DialogueTree dialogue)
        {
            GameManager.InputSystem.PrepareUIReleaseGate(
                m_skipInputReleaseGate,
                EUIInputAction.Submit,
                EUIInputAction.Cancel,
                EUIInputAction.Click);
            m_interactionBlocker.gameObject.SetActive(true);
            m_messageBox.Show();
            AddDialogueLayerIfNeeded();
        }

        private void OnDialogueEnded(DialogueTree dialogue) => CloseDialogue();

        private void OnDialogueNodeChanged(DialogueNode node)
        {
            m_currentNode = node;

            if (node != null)
            {
                m_messageBox.SetText(node.speaker, node.text, node.optionCount == 1);

                if (m_currentNode.optionCount < 2)
                {
                    m_choiceBox.Hide();
                }
            }
            else
            {
                m_choiceBox.Hide();
            }
        }

        public void HandleMessageBoxTextAnimationFinished()
        {
            UpdateChoiceBox();
        }

        private void UpdateChoiceBox()
        {
            if (m_currentNode != null && m_currentNode.optionCount > 1)
            {
                m_choiceBox.Show(m_currentNode.GetOptions());
            }
        }

        private void OnSkipInputReleased(InputAction.CallbackContext context)
        {
            m_skipInputReleaseGate.NotifyReleased(context.action);
        }

        public void OnSkip(InputAction.CallbackContext context)
        {
            if (!GameManager.DialogueSystem.IsPlaying() || m_skipInputReleaseGate.IsBlocked(context.action))
            {
                return;
            }

            if (!m_messageBox.IsTextAnimationFinished())
            {
                m_messageBox.SkipTextAnimation();
                m_skipInputReleaseGate.ArmIfPressed(context.action);
            }
            else if (GameManager.DialogueSystem.TrySkipping())
            {
                m_skipInputReleaseGate.ArmIfPressed(context.action);
            }
        }

        public void HandleDialogueOptionClicked(int option)
        {
            GameManager.DialogueSystem.Next(option);
        }

        private void StartDialogueRuntimeIfReady()
        {
            if (m_dialogueRuntimeListening)
            {
                return;
            }

            if (!GameManager.Exists() ||
                !GameManager.HasSystem<DialogueSystem>() ||
                !GameManager.HasSystem<InputSystem>() ||
                !GameManager.HasSystem<GameStateSystem>())
            {
                return;
            }

            m_dialogueRuntimeListening = true;
            GameManager.DialogueSystem.AddStartedListener(OnDialogueStarted);
            GameManager.DialogueSystem.AddEndedListener(OnDialogueEnded);
            GameManager.DialogueSystem.AddNodeChangedListener(OnDialogueNodeChanged);

            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Submit, EInputActionPhase.Started, OnSkip);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Submit, EInputActionPhase.Canceled, OnSkipInputReleased);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Started, OnSkip);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Canceled, OnSkipInputReleased);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Click, EInputActionPhase.Started, OnSkip);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Click, EInputActionPhase.Canceled, OnSkipInputReleased);

            m_choiceBox.Hide();
            SyncCurrentDialogueIfPlaying();
        }

        private void StopDialogueRuntime()
        {
            if (!m_dialogueRuntimeListening)
            {
                return;
            }

            m_dialogueRuntimeListening = false;
            m_skipInputReleaseGate.Clear();

            if (GameManager.Exists() && GameManager.HasSystem<DialogueSystem>())
            {
                GameManager.DialogueSystem.RemoveStartedListener(OnDialogueStarted);
                GameManager.DialogueSystem.RemoveEndedListener(OnDialogueEnded);
                GameManager.DialogueSystem.RemoveNodeChangedListener(OnDialogueNodeChanged);
            }

            if (GameManager.Exists() && GameManager.HasSystem<InputSystem>())
            {
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Submit, EInputActionPhase.Started, OnSkip);
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Submit, EInputActionPhase.Canceled, OnSkipInputReleased);
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Started, OnSkip);
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Canceled, OnSkipInputReleased);
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Click, EInputActionPhase.Started, OnSkip);
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Click, EInputActionPhase.Canceled, OnSkipInputReleased);
            }
        }

        private void SyncCurrentDialogueIfPlaying()
        {
            if (!GameManager.DialogueSystem.TryGetCurrentState(out DialogueTree dialogue, out DialogueNode node))
            {
                CloseDialogue();
                return;
            }

            OnDialogueStarted(dialogue);
            OnDialogueNodeChanged(node);
        }

        private void AddDialogueLayerIfNeeded()
        {
            if (m_dialogueLayerApplied)
            {
                return;
            }

            if (!GameManager.Exists() || !GameManager.HasSystem<GameStateSystem>())
            {
                return;
            }

            if (GameManager.GameStateSystem.currentState != EGameState.Dialogue)
            {
                GameManager.GameStateSystem.AddLayer(EGameState.Dialogue);
            }

            m_dialogueLayerApplied = true;
        }

        private void RemoveDialogueLayerIfApplied()
        {
            if (!m_dialogueLayerApplied)
            {
                return;
            }

            m_dialogueLayerApplied = false;
            if (GameManager.Exists() &&
                GameManager.HasSystem<GameStateSystem>() &&
                GameManager.GameStateSystem.currentState == EGameState.Dialogue)
            {
                GameManager.GameStateSystem.RemoveLayer(EGameState.Dialogue);
            }
        }
    }
}
