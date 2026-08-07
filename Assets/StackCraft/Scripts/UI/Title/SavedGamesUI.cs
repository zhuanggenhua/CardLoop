using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class SavedGamesUI : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefab used to create a saved-game slot entry in the list.")]
        private SavedGameSlot slotPrefab;

        [SerializeField, Tooltip("Parent container that holds all generated saved-game slot entries.")]
        private RectTransform contentRect;

        [SerializeField, Tooltip("Button that deletes all saved games after user confirmation.")]
        private TextButton clearButton;

        [SerializeField, Tooltip("Button that closes the Saved Games UI panel.")]
        private TextButton closeButton;

        [SerializeField, Tooltip("Modal confirmation window used for delete actions.")]
        private ModalWindow modalWindow;

        private List<SavedGameSlot> slots = new();

        public void Open() => gameObject.SetActive(true);
        public void Close() => gameObject.SetActive(false);

        private void Start()
        {
            foreach (var savedGame in GameDirector.Instance.SavedGames.Values)
            {
                var slot = Instantiate(slotPrefab, contentRect);
                slot.Initialize(savedGame, modalWindow, this);
                slots.Add(slot);
            }

            clearButton.SetOnClick(() =>
                modalWindow.Show(
                    "Delete Games",
                    "Are you sure you want to delete all saved games?" +
                    "\nThis action is permanent and cannot be undone.",
                    ClearSavedGames
                )
            );

            closeButton.SetOnClick(Close);
        }

        private void ClearSavedGames()
        {
            slots.RemoveAll(slot => slot == null);
            slots.ForEach(slot => slot.DeleteSavedGame());
            slots.Clear();
        }
    }
}
