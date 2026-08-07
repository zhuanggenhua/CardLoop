using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameCore
{
    public interface IModLoader
    {
        UniTask<bool> LoadAllModsAsync(List<ModInfo> modInfos);
    }

    /// <summary>
    /// Mod 目录加载器。它会扫描目录、读取配置、执行启停/删除状态，
    /// 并把每个启用内容初始化为独立 YooAsset 资源包。
    /// </summary>
    public sealed class ModLoader : IModLoader
    {
        private readonly ModConfig m_modConfigData;
        private readonly IModValidator m_validator;

        public ModLoader(ModConfig modConfigData, IModValidator validator)
        {
            m_modConfigData = modConfigData;
            m_validator = validator;
        }

        public async UniTask<bool> LoadAllModsAsync(List<ModInfo> modInfos)
        {
            string modPath = m_modConfigData.LoadingPath;
            if (!Directory.Exists(modPath))
            {
                Directory.CreateDirectory(modPath);
                return true;
            }

            await ModAPI.UnZipAllAsync(modPath, true);

            string[] directories = Directory.GetDirectories(modPath, "*", SearchOption.AllDirectories);
            if (directories.Length == 0)
            {
                return true;
            }

            foreach (string directory in directories)
            {
                string[] files = Directory.GetFiles(directory, "*.cfg", SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                {
                    continue;
                }

                ModInfo modInfo = await ModAPI.LoadModInfo(files[0]);
                if (modInfo == null)
                {
                    continue;
                }

                ModStatus state = m_modConfigData.EnsureModState(modInfo).status;
                if (state == ModStatus.Delete)
                {
                    ModAPI.DeleteModFromDisk(modInfo, m_modConfigData.LoadingPath);
                    m_modConfigData.ConsumeDeletedModState(modInfo);
                    continue;
                }

                modInfos.Add(modInfo);
                if (state == ModStatus.Disabled)
                {
                    continue;
                }

                if (!m_validator.ValidateMod(modInfo))
                {
                    Debug.LogWarning($"[ModLoader] Skip incompatible mod {modInfo.FullName}.");
                    continue;
                }

                EnsurePackageName(modInfo);
                await ResourceSystem.LoadModPackageAsync(modInfo.packageName, directory, modInfo.loadOrder);
            }

            return true;
        }

        public async UniTask<ModInfo> LoadModAsync(ModConfig configData, string path)
        {
            string[] configs = Directory.GetFiles(path, "*.cfg", SearchOption.TopDirectoryOnly);
            if (configs.Length == 0)
            {
                return null;
            }

            ModInfo modInfo = await ModAPI.LoadModInfo(configs[0]);
            if (modInfo == null)
            {
                return null;
            }

            ModStatus state = configData.EnsureModState(modInfo).status;
            if (state == ModStatus.Delete)
            {
                ModAPI.DeleteModFromDisk(modInfo, configData.LoadingPath);
                configData.ConsumeDeletedModState(modInfo);
                return null;
            }

            if (state == ModStatus.Disabled || !m_validator.ValidateMod(modInfo))
            {
                return modInfo;
            }

            EnsurePackageName(modInfo);
            await ResourceSystem.LoadModPackageAsync(modInfo.packageName, path, modInfo.loadOrder);
            return modInfo;
        }

        private static void EnsurePackageName(ModInfo modInfo)
        {
            if (string.IsNullOrWhiteSpace(modInfo.packageName))
            {
                throw new InvalidDataException(
                    $"Mod {modInfo.FullName} 缺少 YooAsset 资源包名称 packageName，无法加载内容包。");
            }
        }
    }
}
