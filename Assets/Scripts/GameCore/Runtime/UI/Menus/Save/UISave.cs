using UnityEngine;

namespace GameCore
{
    public class UISave : UIKitMenuPanelBase, ISaveFileEventReceiver
    {
        [Header("References")]
        [SerializeField] private UISaveFile[] m_saveFiles = null;

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            Debug.Assert(openData.ArgumentCount == 0, "SaveMenu panel invoked with incorrect arguments");
            UpdateUI();
        }

        private void UpdateUI(bool skipItemSlots = false)
        {
            foreach (UISaveFile saveFile in m_saveFiles)
            {
                saveFile.UpdateUI();
            }
        }

        public void HandleSaveFileClicked(SaveFileActionDesc desc)
        {
            GameManager.SaveSystem.SaveToFile(desc.slotId);
            UpdateUI();
        }
    }
}

