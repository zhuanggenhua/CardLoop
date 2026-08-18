using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace GameCore
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
                ModConfig config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path));
                if (config == null)
                {
                    throw new InvalidDataException($"Mod 配置文件 {path} 没有包含有效配置。");
                }
                config.Validate();
                config.ConfigPath = path;
                return config;
            }
			catch (Exception e)
			{
				throw new InvalidDataException(
					$"无法读取 Mod 配置文件 {path}；原文件已保留。具体原因：{e.Message}",
					e);
			}
        }

        public void Save()
        {
            Validate();
            string path = string.IsNullOrWhiteSpace(ConfigPath) ? DefaultConfigPath : ConfigPath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = path + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(this, Formatting.Indented));
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
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
                modId = RequireModId(modInfo),
                status = ModStatus.Enabled
            };
            States.Add(created);
            return created;
        }

        public void DeleteMod(ModInfo modInfo)
        {
            ModState modStateInfo = EnsureModState(modInfo);
            modStateInfo.status = ModStatus.Delete;
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

        /// <summary>安装目录已经不存在时，消费已达成目标的删除状态；其它缺失 Mod 状态继续保留。</summary>
        internal int ConsumeDeletedStatesMissingFrom(ISet<string> installedModIds)
        {
            if (installedModIds == null)
            {
                throw new ArgumentNullException(nameof(installedModIds));
            }

            int consumed = 0;
            for (int i = States.Count - 1; i >= 0; i--)
            {
                ModState state = States[i];
                if (state.status != ModStatus.Delete || installedModIds.Contains(state.modId))
                {
                    continue;
                }

                States.RemoveAt(i);
                consumed++;
            }

            return consumed;
        }

        public bool TryGetModState(ModInfo modInfo, out ModState modState)
        {
            string modId = RequireModId(modInfo);
            foreach (ModState stateInfo in States)
            {
                if (string.Equals(stateInfo.modId, modId, StringComparison.Ordinal))
                {
                    modState = stateInfo;
                    return true;
                }
            }

            modState = null;
            return false;
        }

        private static string RequireModId(ModInfo modInfo)
        {
            if (modInfo == null)
            {
                throw new ArgumentNullException(nameof(modInfo));
            }
            if (string.IsNullOrWhiteSpace(modInfo.modId))
            {
                throw new InvalidOperationException("Mod 清单缺少稳定身份 modId。");
            }
            return modInfo.modId;
        }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(LoadingPath))
            {
                throw new InvalidDataException("Mod 配置缺少加载目录。");
            }
            try
            {
                _ = Path.GetFullPath(LoadingPath);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException($"Mod 加载目录无效：{LoadingPath}。", exception);
            }
            if (!Version.TryParse(ApiVersion, out _))
            {
                throw new InvalidDataException($"Mod API 版本不是有效版本号：{ApiVersion ?? "<null>"}。");
            }
            if (States == null)
            {
                throw new InvalidDataException("Mod 配置缺少状态列表。");
            }

            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < States.Count; i++)
            {
                ModState state = States[i] ??
                    throw new InvalidDataException($"Mod 配置的第 {i + 1} 条状态为空。");
                if (string.IsNullOrWhiteSpace(state.modId))
                {
                    throw new InvalidDataException($"Mod 配置的第 {i + 1} 条状态缺少 Mod ID。");
                }
                if (!stateIds.Add(state.modId))
                {
                    throw new InvalidDataException($"Mod 配置重复记录状态：{state.modId}。");
                }
                if (!Enum.IsDefined(typeof(ModStatus), state.status))
                {
                    throw new InvalidDataException(
                        $"Mod {state.modId} 的状态值无效：{(int)state.status}。");
                }
            }
        }
    }
}
