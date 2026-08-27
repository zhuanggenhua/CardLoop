using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace CryingSnow.StackCraft
{
    public static class SaveSystem
    {
        /// <summary>
        /// Saves data to a file as JSON.
        /// </summary>
        public static void SaveData<T>(T data, string fileName)
        {
            string filePath = Path.Combine(Application.persistentDataPath, fileName + ".json");

            // Convert the data object to a JSON string
            // Formatting.Indented makes the file readable (good for debugging). 
            // Change to Formatting.None for a smaller file size in release.
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);

            // Write the string to the file
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Loads data from a JSON file.
        /// </summary>
        public static T LoadData<T>(string fileName)
        {
            string filePath = Path.Combine(Application.persistentDataPath, fileName + ".json");

            if (File.Exists(filePath))
            {
                // Read the JSON string from the file
                string json = File.ReadAllText(filePath);

                // Convert the JSON string back into the object of type T
                T data = JsonConvert.DeserializeObject<T>(json);

                return data;
            }
            else
            {
                // Optional warning
                // Debug.LogWarning($"Save file not found at: {filePath}");
                return default(T);
            }
        }

        /// <summary>
        /// Loads all JSON files in the directory and returns a Dictionary of valid data.
        /// Key = File Name (without extension), Value = The Data Object.
        /// </summary>
        public static Dictionary<string, T> LoadAllValidData<T>()
        {
            Dictionary<string, T> validDataDict = new Dictionary<string, T>();
            string directoryPath = Application.persistentDataPath;

            // 1. Ensure the directory exists
            if (!Directory.Exists(directoryPath))
            {
                return validDataDict;
            }

            // 2. Get all .json files in the directory
            string[] filePaths = Directory.GetFiles(directoryPath, "*.json");

            foreach (string filePath in filePaths)
            {
                try
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    // 参考模板和 CardLoop 共用持久化目录，模板存档只接受自己的 SaveSlot 文件。
                    if (typeof(T) == typeof(GameData) && !fileName.StartsWith("SaveSlot", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // 3. Attempt to read and deserialize
                    string json = File.ReadAllText(filePath);
                    T data = JsonConvert.DeserializeObject<T>(json);

                    if (data != null)
                    {
                        // 旧模板存档 UI 依赖当前场景名，缺失时跳过，避免把其它 JSON 当成模板存档。
                        if (data is GameData gameData && string.IsNullOrWhiteSpace(gameData.CurrentScene))
                        {
                            Debug.LogWarning($"Skipped invalid StackCraft save file at: {filePath}. Missing CurrentScene.");
                            continue;
                        }

                        // Add to dictionary
                        validDataDict.Add(fileName, data);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Skipped invalid save file at: {filePath}. Error: {ex.Message}");
                }
            }

            return validDataDict;
        }

        /// <summary>
        /// Helper to delete a save file.
        /// </summary>
        public static void DeleteSave(string fileName)
        {
            string filePath = Path.Combine(Application.persistentDataPath, fileName + ".json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
