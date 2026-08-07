using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.StackCraft
{
    public class GameplayPrefsUI : MonoBehaviour
    {
        [SerializeField, Tooltip("The TextMeshProUGUI component displaying the current value of the Day Duration slider.")]
        private TextMeshProUGUI durationLabel;

        [SerializeField, Tooltip("The Slider component used to set the duration (in seconds) of a game day.")]
        private Slider durationSlider;

        [SerializeField, Tooltip("The TextMeshProUGUI component displaying the current state and description of the Friendly Mode toggle.")]
        private TextMeshProUGUI isFriendlyLabel;

        [SerializeField, Tooltip("The Toggle component used to enable or disable 'Friendly Mode' (enemy presence).")]
        private Toggle isFriendlyToggle;

        [SerializeField, Tooltip("The button used to close the UI panel without starting a new game.")]
        private TextButton cancelButton;

        [SerializeField, Tooltip("The button used to confirm the settings and start a new game.")]
        private TextButton confirmButton;

        private void Awake()
        {
            durationSlider.onValueChanged.AddListener(value =>
            {
                int duration = (int)value;
                durationLabel.text = $"Day Duration: {duration} sec";
            });

            isFriendlyToggle.onValueChanged.AddListener(isOn =>
            {
                string state = isOn ? "ON" : "OFF";
                string message = isOn ? "(No enemies will appear)" : "(Enemies may appear)";
                isFriendlyLabel.text = $"Friendly Mode: {state}\n<size=23>{message}";

                AudioManager.Instance?.PlaySFX(AudioId.Click);
            });

            cancelButton.SetOnClick(Close);
            confirmButton.SetOnClick(StartNewGame);
        }

        public void Open() => gameObject.SetActive(true);
        private void Close() => gameObject.SetActive(false);

        private void StartNewGame()
        {
            int dayDuration = (int)durationSlider.value;
            bool isFriendlyMode = isFriendlyToggle.isOn;
            var prefs = new GameplayPrefs(dayDuration, isFriendlyMode);
            GameDirector.Instance.NewGame(prefs);
            Close();
        }
    }
}
