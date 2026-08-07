using System;
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

        public static void EraseSaveData(string saveFileName)
        {
            ConfigureSaveKit();
            SaveKit.Delete(GetSlotId(saveFileName));
        }

        public static SaveDataBlock ExtractSaveDataFromFile(string saveFileName)
        {
            try
            {
                ConfigureSaveKit();
                SaveData saveData = SaveKit.Load(GetSlotId(saveFileName));
                return saveData?.GetModule<SaveDataBlock>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 把世界存档块写入 SaveKit。这里故意只注册一个 SaveDataBlock 模块，
        /// 避免 SaveKit 的模块容器反过来拆散当前世界状态的单一聚合语义。
        /// </summary>
        public static bool StoreSaveDataToFile(string saveFileName, SaveDataBlock block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            ConfigureSaveKit();

            SaveData saveData = SaveKit.CreateSaveData();
            saveData.RegisterModule(block);

            string displayName = string.IsNullOrWhiteSpace(block.header) ? saveFileName : block.header;
            return SaveKit.Save(GetSlotId(saveFileName), saveData, displayName);
        }

        /// <summary>
        /// 将旧菜单保存名稳定映射到 SaveKit 槽位。已存在的 SAVEFILE_A/B/C 直接对应 0/1/2；
        /// 其他名称只作为兜底，用确定性哈希分配到剩余槽位，避免跨运行随机变化。
        /// </summary>
        public static int GetSlotId(string saveFileName)
        {
            if (string.IsNullOrWhiteSpace(saveFileName))
            {
                throw new ArgumentException("Save file name cannot be empty.", nameof(saveFileName));
            }

            string normalized = Path.GetFileNameWithoutExtension(saveFileName.Trim()).ToUpperInvariant();
            int underscoreIndex = normalized.LastIndexOf('_');
            string suffix = underscoreIndex >= 0 ? normalized[(underscoreIndex + 1)..] : normalized;

            if (suffix.Length == 1 && suffix[0] >= 'A' && suffix[0] <= 'Z')
            {
                return suffix[0] - 'A';
            }

            if (int.TryParse(suffix, out int numericSlot) && numericSlot >= 0 && numericSlot < SaveKitMaxSlots)
            {
                return numericSlot;
            }

            uint hash = 2166136261;
            for (int i = 0; i < normalized.Length; ++i)
            {
                hash ^= normalized[i];
                hash *= 16777619;
            }

            return (int)(hash % SaveKitMaxSlots);
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
