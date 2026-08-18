using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameCore
{
    public interface IModLoader
    {
        UniTask<bool> LoadAllModsAsync(List<ModInfo> modInfos, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Mod 目录加载器。它先收齐清单并验证依赖闭包，再按依赖顺序加载独立 YooAsset 包。
    /// </summary>
    public sealed class ModLoader : IModLoader
    {
        private readonly ModConfig m_modConfigData;
        private readonly IModValidator m_validator;

        public ModLoader(ModConfig modConfigData, IModValidator validator)
        {
            m_modConfigData = modConfigData ?? throw new ArgumentNullException(nameof(modConfigData));
            m_validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public async UniTask<bool> LoadAllModsAsync(
            List<ModInfo> modInfos,
            CancellationToken cancellationToken)
        {
            if (modInfos == null)
            {
                throw new ArgumentNullException(nameof(modInfos));
            }
            cancellationToken.ThrowIfCancellationRequested();
            string modPath = m_modConfigData.LoadingPath;
            if (!Directory.Exists(modPath))
            {
                Directory.CreateDirectory(modPath);
                return true;
            }

            await ExtractArchivesAsync(modPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            string[] rootManifests = Directory.GetFiles(modPath, "*.cfg", SearchOption.TopDirectoryOnly);
            if (rootManifests.Length > 0)
            {
                Array.Sort(rootManifests, StringComparer.OrdinalIgnoreCase);
                throw new InvalidDataException(
                    $"Mod 根目录不能直接放置清单；每个 Mod 必须使用独立子目录：{string.Join("、", rootManifests)}。");
            }

            string[] directories = Directory.GetDirectories(modPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

            var discoveredMods = new List<ModInfo>();
            var retainedMods = new List<ModInfo>();
            var pendingDeletions = new List<ModInfo>();
            foreach (string directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string[] files = Directory.GetFiles(directory, "*.cfg", SearchOption.TopDirectoryOnly);
                if (files.Length > 1)
                {
                    throw new InvalidDataException(
                        $"目录 {directory} 包含多个 Mod 清单，必须只保留一个：{string.Join("、", files)}。");
                }
                if (files.Length == 0)
                {
                    continue;
                }

                ModInfo modInfo = ModAPI.LoadModInfo(files[0]);
                cancellationToken.ThrowIfCancellationRequested();

                ModStatus state = m_modConfigData.EnsureModState(modInfo).status;
                if (state == ModStatus.Delete)
                {
                    pendingDeletions.Add(modInfo);
                }
                else
                {
                    retainedMods.Add(modInfo);
                    if (state == ModStatus.Enabled && !m_validator.ValidateMod(modInfo))
                    {
                        throw new InvalidDataException(
                            $"Mod {modInfo.DisplayName ?? "<未命名>"} 的 API 版本 {modInfo.apiVersion ?? "<空>"} 与当前版本不兼容。");
                    }
                }

                discoveredMods.Add(modInfo);
            }

            var installedModIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < discoveredMods.Count; i++)
            {
                installedModIds.Add(discoveredMods[i].modId);
            }
            m_modConfigData.ConsumeDeletedStatesMissingFrom(installedModIds);

            IReadOnlyList<ModInfo> loadOrder = ModDependencyResolver.Resolve(discoveredMods, m_modConfigData);
            DeletePendingMods(pendingDeletions);
            var loadedPackageNames = new List<string>(loadOrder.Count);
            try
            {
                for (int i = 0; i < loadOrder.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ModInfo modInfo = loadOrder[i];
                    await ResourceSystem.LoadModPackageAsync(
                        modInfo.packageName,
                        modInfo.FilePath,
                        cancellationToken);
                    loadedPackageNames.Add(modInfo.packageName);
                }
            }
            catch
            {
                for (int i = loadedPackageNames.Count - 1; i >= 0; i--)
                {
                    await ResourceSystem.UnloadModPackageAsync(loadedPackageNames[i]);
                }
                throw;
            }
            modInfos.AddRange(retainedMods);
            return true;
        }

        /// <summary>全部删除路径通过预检后才开始改动磁盘；每个状态只在对应目录删除成功后消费。</summary>
        internal void DeletePendingMods(IReadOnlyList<ModInfo> pendingDeletions)
        {
            if (pendingDeletions == null)
            {
                throw new ArgumentNullException(nameof(pendingDeletions));
            }

            for (int i = 0; i < pendingDeletions.Count; i++)
            {
                ModAPI.RequireModDirectoryForDeletion(
                    pendingDeletions[i],
                    m_modConfigData.LoadingPath);
            }

            for (int i = 0; i < pendingDeletions.Count; i++)
            {
                ModInfo modInfo = pendingDeletions[i];
                ModAPI.DeleteModFromDisk(modInfo, m_modConfigData.LoadingPath);
                if (!m_modConfigData.ConsumeDeletedModState(modInfo))
                {
                    throw new InvalidOperationException(
                        $"Mod {modInfo.DisplayName ?? "<未命名>"} 的目录已经删除，但配置中没有对应的待删除状态。");
                }
            }
        }

        private static async UniTask ExtractArchivesAsync(
            string modPath,
            CancellationToken cancellationToken)
        {
            string[] archives = Directory.GetFiles(modPath, "*.zip", SearchOption.TopDirectoryOnly);
            Array.Sort(archives, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < archives.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string archivePath = archives[i];
                await UniTask.RunOnThreadPool(
                    () => ExtractArchive(archivePath),
                    cancellationToken: cancellationToken);
            }
        }

        internal static void ExtractArchive(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                throw new InvalidDataException($"Mod 压缩包没有有效父目录：{archivePath ?? "<null>"}。");
            }

            string fullArchivePath = Path.GetFullPath(archivePath);
            string parentDirectory = Path.GetDirectoryName(fullArchivePath);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                throw new InvalidDataException($"Mod 压缩包没有有效父目录：{fullArchivePath}。");
            }

            string archiveName = Path.GetFileNameWithoutExtension(fullArchivePath);
            string outputDirectory = Path.Combine(parentDirectory, archiveName);
            if (Directory.Exists(outputDirectory))
            {
                throw new InvalidDataException(
                    $"Mod 压缩包 {fullArchivePath} 的目标目录已经存在：{outputDirectory}。请先明确删除旧目录或更换压缩包名称。");
            }

            if (!ZipArchiveExtractor.UnzipFile(fullArchivePath, outputDirectory))
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
                throw new InvalidDataException($"Mod 压缩包解压失败：{fullArchivePath}。");
            }

            File.Delete(fullArchivePath);
        }
    }
}
