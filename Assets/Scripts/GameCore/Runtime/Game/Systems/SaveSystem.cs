using System;
using System.Collections.Generic;
using UnityEngine;
using YokiFrame;

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

        public static bool DeleteSaveData(int slotId)
        {
            return SaveFileStorageRuntime.DeleteSaveData(slotId);
        }

        public static IReadOnlyList<SaveMeta> GetAllSaveMetadata()
        {
            return SaveFileStorageRuntime.GetAllSaveMetadata();
        }

        public static int DeleteAllSaveData()
        {
            return SaveFileStorageRuntime.DeleteAllSaveData();
        }

        public static SaveData ExtractSaveContainerFromFile(int slotId)
        {
            return SaveFileStorageRuntime.ExtractSaveContainerFromFile(slotId);
        }

        public static SaveData CreateSaveContainer()
        {
            return SaveFileStorageRuntime.CreateSaveContainer();
        }

        public static SaveMeta GetSaveMetadata(int slotId)
        {
            return SaveFileStorageRuntime.GetSaveMetadata(slotId);
        }

        public void LoadFromFile(int slotId)
        {
            SaveData container = ExtractSaveContainerFromFile(slotId);
            SaveDataBlock saveData = container?.GetModule<SaveDataBlock>();

            if (saveData != null)
            {
                LoadDataBlock(
                    saveData,
                    () => Debug.Log($"Save slot <b>{slotId}</b> loaded successfully!"));
            }
            else
            {
                Debug.LogError($"Save slot <b>{slotId}</b> does not contain the GameCore world module.");
            }
        }

        public void SaveToFile(int slotId)
        {
            SaveDataBlock saveFile = CreateDataBlock();
            SaveData container = CreateSaveContainer();
            container.RegisterModule(saveFile);
            if (StoreSaveDataToFile(slotId, container, saveFile.header))
            {
                Debug.Log($"Saved GameCore world module to SaveKit slot {slotId}.");
            }
        }

        public static bool StoreSaveDataToFile(
            int slotId,
            SaveData container,
            string displayName)
        {
            return SaveFileStorageRuntime.StoreSaveContainer(slotId, container, displayName);
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
