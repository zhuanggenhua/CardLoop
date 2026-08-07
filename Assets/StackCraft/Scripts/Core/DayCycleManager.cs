using System.Collections;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class DayCycleManager : MonoBehaviour
    {
        public static DayCycleManager Instance { get; private set; }

        public bool IsEndingCycle { get; private set; }

        private readonly object dayCycleRequester = "DayCycleRequester";
        private readonly object dayCycleInputLock = "DayCycleInputLock";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayEnded += HandleDayEnded;
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayEnded -= HandleDayEnded;

            if (CardManager.Instance != null)
                CardManager.Instance.OnStatsChanged -= OnStatsChangedDuringSelling;
        }

        private void HandleDayEnded(int day)
        {
            InputManager.Instance.AddLock(dayCycleInputLock);
            IsEndingCycle = true;
            StartCoroutine(NotificationPhase(day));
        }

        // --- PHASE 1: NOTIFICATION ---
        private IEnumerator NotificationPhase(int day)
        {
            InfoPanel.Instance?.RequestInfoDisplay(
                dayCycleRequester,
                InfoPriority.Modal,
                ($"End of Day {day}", "Your people are hungry!"),
                "Feed People",
                () =>
                {
                    StartCoroutine(FeedingPhase());
                }
            );
            yield break;
        }

        // --- PHASE 2: FEEDING ---
        private IEnumerator FeedingPhase()
        {
            InfoPanel.Instance?.RequestInfoDisplay(
                dayCycleRequester,
                InfoPriority.Modal,
                ("Feeding People", "Distributing food...")
            );

            // Wait for the card animations and logic to finish
            yield return CardManager.Instance.FeedCharacters();

            // Check if anyone survived
            var stats = CardManager.Instance.GetStatsSnapshot();
            if (stats.TotalCharacters <= 0)
            {
                HandleGameOver();
            }
            else
            {
                // Transition to Selling Phase
                StartSellingPhase();
            }
        }

        // --- PHASE 3: SELLING ---
        private void StartSellingPhase()
        {
            // 1. The player MUST be able to interact to sell cards.
            InputManager.Instance.RemoveLock(dayCycleInputLock);

            // 2. Subscribe to the event. We will now react ONLY when cards change.
            CardManager.Instance.OnStatsChanged += OnStatsChangedDuringSelling;

            // 3. Manually trigger the check once immediately.
            //    This handles the case where we might already have 0 excess cards.
            CheckSellingCondition(CardManager.Instance.GetStatsSnapshot());
        }

        private void OnStatsChangedDuringSelling(StatsSnapshot stats)
        {
            CheckSellingCondition(stats);
        }

        private void CheckSellingCondition(StatsSnapshot stats)
        {
            int excess = stats.ExcessCards;

            if (excess <= 0)
            {
                // CONDITION MET: We are done selling.

                // 1. Unsubscribe immediately so this doesn't fire again during gameplay.
                CardManager.Instance.OnStatsChanged -= OnStatsChangedDuringSelling;

                // 2. Proceed to next phase.
                StartCoroutine(EncounterPhase());
            }
            else
            {
                // CONDITION NOT MET: Update UI.
                InfoPanel.Instance?.RequestInfoDisplay(
                    dayCycleRequester,
                    InfoPriority.Modal,
                    ("Sell Excess Cards", $"You must sell {excess} excess {(excess > 1 ? "cards" : "card")} to continue.")
                );
            }
        }

        // --- PHASE 4: ENCOUNTER ---
        private IEnumerator EncounterPhase()
        {
            // 1. Check if we have an encounter for the current day.
            int currentDay = TimeManager.Instance.CurrentDay;

            var encounter = EncounterManager.Instance.GetBestEncounter(currentDay);

            if (encounter != null)
            {
                InputManager.Instance.AddLock(dayCycleInputLock);

                yield return EncounterManager.Instance.ExecuteEncounter(encounter);

                InputManager.Instance.RemoveLock(dayCycleInputLock);
            }

            PrepareForNewDay();
        }

        // --- PHASE 5: NEW DAY ---
        private void PrepareForNewDay()
        {
            InputManager.Instance.AddLock(dayCycleInputLock);

            int nextDay = TimeManager.Instance.CurrentDay + 1;

            InfoPanel.Instance?.RequestInfoDisplay(
                dayCycleRequester,
                InfoPriority.Modal,
                ($"Start of Day {nextDay}", "Everything is ready."),
                "Start Day",
                () =>
                {
                    IsEndingCycle = false;
                    InfoPanel.Instance?.ClearInfoRequest(dayCycleRequester);
                    InputManager.Instance.RemoveLock(dayCycleInputLock);
                    TimeManager.Instance.StartNewDay();
                    GameDirector.Instance.SaveGame();
                }
            );
        }

        private void HandleGameOver()
        {
            InputManager.Instance.AddLock(dayCycleInputLock);

            InfoPanel.Instance?.RequestInfoDisplay(
                dayCycleRequester,
                InfoPriority.Modal,
                ("Game Over", "You have no people left."),
                "Return to Title",
                () => GameDirector.Instance.GameOver()
            );
        }
    }
}
