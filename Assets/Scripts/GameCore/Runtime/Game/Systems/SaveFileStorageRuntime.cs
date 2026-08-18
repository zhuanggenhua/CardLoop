using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// SaveSystem 的 SaveKit 文件层实现。
    /// 这里只负责槽位、路径、版本、文件格式和稳定槽位映射，
    /// 不负责 RPG 世界存档块的聚合真相。
    /// </summary>
    internal static class SaveFileStorageRuntime
    {
        private const int SaveKitVersion = 1;
        private const int SaveKitMaxSlots = 32;
        private const string SaveKitDirectoryName = "GameCoreSaves";
        private const string SaveKitFilePrefix = "gamecore_";
        private const string SaveKitFileExtension = ".yoki";

        private static bool s_saveKitConfigured;
        private static string s_configuredSaveKitPath;

        public static bool DeleteSaveData(int slotId)
        {
            ConfigureSaveKit();
            if (!SaveKit.Exists(slotId))
            {
                return false;
            }
            return SaveKit.Delete(slotId);
        }

        public static IReadOnlyList<SaveMeta> GetAllSaveMetadata()
        {
            ConfigureSaveKit();
            List<SaveMeta> metadata = SaveKit.GetAllSlots();
            metadata.Sort((left, right) => left.SlotId.CompareTo(right.SlotId));
            return metadata;
        }

        public static int GetMaximumSaveSlots()
        {
            ConfigureSaveKit();
            return SaveKit.GetMaxSlots();
        }

        public static int DeleteAllSaveData()
        {
            IReadOnlyList<SaveMeta> metadata = GetAllSaveMetadata();
            int deletedCount = 0;
            for (int i = 0; i < metadata.Count; i++)
            {
                if (SaveKit.Delete(metadata[i].SlotId))
                {
                    deletedCount++;
                }
            }
            return deletedCount;
        }

        public static SaveData ExtractSaveContainerFromFile(int slotId)
        {
            ConfigureSaveKit();
            return SaveKit.Load(slotId);
        }

        /// <summary>
        /// 把各领域已经组装好的模块容器写入 SaveKit；文件层不识别任何业务模块。
        /// </summary>
        public static bool StoreSaveContainer(
            int slotId,
            SaveData container,
            string displayName)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            ConfigureSaveKit();
            return SaveKit.Save(slotId, container, displayName);
        }

        public static SaveMeta GetSaveMetadata(int slotId)
        {
            ConfigureSaveKit();
            return SaveKit.GetMeta(slotId);
        }

        public static SaveData CreateSaveContainer()
        {
            ConfigureSaveKit();
            return SaveKit.CreateSaveData();
        }

        /// <summary>
        /// 配置 GameCore 专用 SaveKit 文件格式。测试可传入临时目录；运行时首次使用后，
        /// 无参数调用不会覆盖测试或外部已显式设置的路径。
        /// </summary>
        public static void ConfigureSaveKit(string saveDirectory = null)
        {
            if (s_saveKitConfigured && string.IsNullOrWhiteSpace(saveDirectory))
            {
                return;
            }

            string targetPath = string.IsNullOrWhiteSpace(saveDirectory)
                ? Path.Combine(Application.persistentDataPath, SaveKitDirectoryName)
                : Path.GetFullPath(saveDirectory);

            if (s_saveKitConfigured && string.Equals(s_configuredSaveKitPath, targetPath, StringComparison.Ordinal))
            {
                return;
            }

            SaveKit.SetMaxSlots(SaveKitMaxSlots);
            SaveKit.SetCurrentVersion(SaveKitVersion);
            SaveKit.SetFileFormat(SaveKitFilePrefix, SaveKitFileExtension);
            SaveKit.SetSavePath(targetPath);

            s_configuredSaveKitPath = targetPath;
            s_saveKitConfigured = true;
        }

        public static void ResetSaveKitConfigurationForTests()
        {
            s_configuredSaveKitPath = null;
            s_saveKitConfigured = false;
        }
    }
}
