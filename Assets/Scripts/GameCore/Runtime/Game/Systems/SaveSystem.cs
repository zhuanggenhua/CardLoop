using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 通用世界存档系统。它负责组装和恢复地图、游戏标记、玩家和持久化对象；
    /// YokiFrame SaveKit 只作为文件槽位、版本和元数据承载层，不能变成世界状态真相源。
    /// </summary>
    public class SaveSystem : AGameSystem, IDataBlockHandler<SaveDataBlock>
    {
        public void LoadDefaultSaveFile(SaveFile saveFile)
        {
            LoadDataBlock(saveFile.CreateContentSnapshot());
        }

        public static void EraseSaveData(string saveFileName)
        {
            SaveFileStorageRuntime.EraseSaveData(saveFileName);
        }

        public static SaveDataBlock ExtractSaveDataFromFile(string saveFileName)
        {
            return SaveFileStorageRuntime.ExtractSaveDataFromFile(saveFileName);
        }

        public void LoadFromFile(string saveFileName)
        {
            SaveDataBlock saveData = ExtractSaveDataFromFile(saveFileName);

            if (saveData != null)
            {
                LoadDataBlock(
                    saveData,
                    () => Debug.Log($"Save <b>{saveFileName}</b> loaded successfully!"));
            }
            else
            {
                Debug.LogError($"Save <b>{saveFileName}</b> failed to load!");
            }
        }

        public void SaveToFile(string saveFileName)
        {
            try
            {
                SaveDataBlock saveFile = CreateDataBlock();
                if (StoreSaveDataToFile(saveFileName, saveFile))
                {
                    int slotId = GetSlotId(saveFileName);
                    Debug.Log($"Saved <b>{saveFileName}</b> to SaveKit slot {slotId}.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save {saveFileName} through SaveKit: {e.Message}");
            }
        }

        internal static bool StoreSaveDataToFile(string saveFileName, SaveDataBlock block)
        {
            return SaveFileStorageRuntime.StoreSaveDataToFile(saveFileName, block);
        }

        internal static int GetSlotId(string saveFileName)
        {
            return SaveFileStorageRuntime.GetSlotId(saveFileName);
        }

        internal static void ConfigureSaveKit(string saveDirectory = null)
        {
            SaveFileStorageRuntime.ConfigureSaveKit(saveDirectory);
        }

        internal static void ResetSaveKitConfigurationForTests()
        {
            SaveFileStorageRuntime.ResetSaveKitConfigurationForTests();
        }

        public string GenerateSavefileHeader()
        {
            return $"GameCore Save {DateTime.Now:yyyy-MM-dd HH:mm}";
        }

        public SaveDataBlock CreateDataBlock()
        {
            return new SaveDataBlock
            {
                header = GenerateSavefileHeader(),
                map = GameManager.MapSystem.CreateDataBlock(),
                gameFlags = GameManager.GameFlagSystem.CreateDataBlock(),
                player = GameManager.PlayerSystem.CreateDataBlock(),
                persistence = GameManager.PersistenceSystem.CreateDataBlock(),
            };
        }

        public void LoadDataBlock(SaveDataBlock block)
        {
            LoadDataBlock(block, null);
        }

        private void LoadDataBlock(SaveDataBlock block, Action onCompletion)
        {
            GameManager.GameFlagSystem.LoadDataBlock(block.gameFlags);
            GameManager.PlayerSystem.LoadDataBlock(block.player);
            GameManager.PersistenceSystem.LoadDataBlock(block.persistence);
            GameManager.MapSystem.LoadDataBlock(block.map, () =>
            {
                YokiFrame.EventKit.Type.Send(new SaveFileLoadedEvent());
                onCompletion?.Invoke();
            });
        }
    }
}
