using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIMainMenu : MonoBehaviour, ISaveFileEventReceiver
    {
        [Header("Settings")]
        [SerializeField] private Button m_defaultSelectedButton = null;
        [SerializeField] private UISettings m_settingsMenu = null;

        [Header("References")]
        [SerializeField] private UISaveFile[] m_saveFiles = null;
        [SerializeField] private Button[] m_eraseButtons = null;

        private bool m_cancelListening = false;

        private void Start()
        {
            m_settingsMenu.Init();
            StartCancelListeningIfReady();
        }

        public void OnEnable()
        {
            UpdateUI();
            SelectDefaultButton();
            StartCancelListeningIfReady();
        }

        private void OnDisable()
        {
            StopCancelListening();
        }

        private void OnDestroy()
        {
            StopCancelListening();
        }

        private void UpdateUI()
        {
            for (int i = 0; i < m_saveFiles.Length; ++i)
            {
                UISaveFile saveFile = m_saveFiles[i];
                Button eraseButton = i < m_eraseButtons.Length ? m_eraseButtons[i] : null;
                saveFile.UpdateUI();
                eraseButton.interactable = saveFile.CanEraseSaveData();
            }
        }

        public void ShowSettingsMenu()
        {
            m_settingsMenu.Show();
        }

        private void OnCancel(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            m_settingsMenu.Hide();
        }

        public void StartNewGameFromDefaultSaveFile(SaveFile saveFile)
        {
            LoadScenes(() =>
            {
                GameManager.SaveSystem.LoadDefaultSaveFile(saveFile);
            });
        }

        private void LoadScenes(Action onScenesLoaded)
        {
            SceneManager.LoadSceneAsync(Constants.M2DEngineSceneName).completed += (operation) =>
            {
                onScenesLoaded();
            };
        }

        private void SelectDefaultButton()
        {
            m_defaultSelectedButton.Select();
        }

        public void EraseSaveFile(UISaveFile saveFile)
        {
            saveFile.EraseSaveData();
            UpdateUI();
            SelectDefaultButton();
        }

        public void HandleSaveFileClicked(SaveFileActionDesc desc)
        {
            LoadScenes(() =>
            {
                GameManager.SaveSystem.LoadFromFile(desc.filename);
            });
        }

        private void StartCancelListeningIfReady()
        {
            if (m_cancelListening ||
                !GameManager.Exists() ||
                !GameManager.HasSystem<InputSystem>())
            {
                return;
            }

            m_cancelListening = true;
            GameManager.InputSystem.AddUIActionListener(
                EUIInputAction.Cancel,
                EInputActionPhase.Performed,
                OnCancel);
        }

        private void StopCancelListening()
        {
            if (!m_cancelListening)
            {
                return;
            }

            m_cancelListening = false;
            if (!GameManager.Exists() ||
                !GameManager.HasSystem<InputSystem>())
            {
                return;
            }

            GameManager.InputSystem.RemoveUIActionListener(
                EUIInputAction.Cancel,
                EInputActionPhase.Performed,
                OnCancel);
        }
    }
}

