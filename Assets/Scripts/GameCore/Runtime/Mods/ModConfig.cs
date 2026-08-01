using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// Mod 加载配置，保留 Chris 的 LoadingPath/API/状态列表语义。
    /// 与 Chris 原版不同，这里不用额外 Config 框架，直接保存到玩家持久化目录。
    /// </summary>
    [Serializable]
    public class ModConfig
    {
        public string LoadingPath { get; set; } = ModAPI.LoadingPath;
        public string ApiVersion { get; set; } = ModAPI.DefaultAPIVersion;
        public List<ModState> States { get; set; } = new();

        [JsonIgnore] public string ConfigPath { get; private set; }

        public static string DefaultConfigPath =>
            Path.Combine(Application.persistentDataPath, "GameCoreModConfig.json");

        public static ModConfig LoadOrCreate(string configPath = null)
        {
            string path = string.IsNullOrWhiteSpace(configPath) ? DefaultConfigPath : Path.GetFullPath(configPath);
            if (!File.Exists(path))
            {
                ModConfig created = new();
                created.ConfigPath = path;
                return created;
            }

            try
            {
                ModConfig config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path)) ?? new ModConfig();
                config.ConfigPath = path;
                return config;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ModAPI] Failed to load mod config {path}: {e.Message}");
                ModConfig fallback = new();
                fallback.ConfigPath = path;
                return fallback;
            }
        }

        public void Save()
        {
            string path = string.IsNullOrWhiteSpace(ConfigPath) ? DefaultConfigPath : ConfigPath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public ModStatus GetModState(ModInfo modInfo)
        {
            if (TryGetModState(modInfo, out ModState modStateInfo))
            {
                return modStateInfo.status;
            }

            return ModStatus.Enabled;
        }

        public ModState EnsureModState(ModInfo modInfo)
        {
            if (TryGetModState(modInfo, out ModState modState))
            {
                return modState;
            }

            ModState created = new()
            {
                fullName = modInfo.FullName,
                status = ModStatus.Enabled
            };
            States.Add(created);
            return created;
        }

        public void DeleteMod(ModInfo modInfo, bool force = false)
        {
            if (force)
            {
                if (TryGetModState(modInfo, out ModState modStateInfo))
                {
                    States.Remove(modStateInfo);
                }
            }
            else
            {
                ModState modStateInfo = EnsureModState(modInfo);
                modStateInfo.status = ModStatus.Delete;
            }
        }

        public void SetModEnabled(ModInfo modInfo, bool isEnabled)
        {
            ModState modStateInfo = EnsureModState(modInfo);
            modStateInfo.status = isEnabled ? ModStatus.Enabled : ModStatus.Disabled;
        }

        public bool ConsumeDeletedModState(ModInfo modInfo)
        {
            if (!TryGetModState(modInfo, out ModState modStateInfo) || modStateInfo.status != ModStatus.Delete)
            {
                return false;
            }

            States.Remove(modStateInfo);
            return true;
        }

        public bool TryGetModState(ModInfo modInfo, out ModState modState)
        {
            foreach (ModState stateInfo in States)
            {
                if (stateInfo.fullName == modInfo.FullName)
                {
                    modState = stateInfo;
                    return true;
                }
            }

            modState = null;
            return false;
        }
    }
}
