using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// Zip 解压工具，职责对应 Chris 的 Mod 包解压能力。
    /// 项目当前不引入 SharpZipLib，因此用 .NET ZipArchive 实现 Mod 包解压。
    /// </summary>
    public static class ZipArchiveExtractor
    {
        public static bool UnzipFile(string filePathName, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(filePathName) || string.IsNullOrWhiteSpace(outputPath))
            {
                return false;
            }

            try
            {
                using FileStream stream = File.OpenRead(filePathName);
                return UnzipFile(stream, outputPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ZipArchiveExtractor] {e}");
                return false;
            }
        }

        public static bool UnzipFile(byte[] fileBytes, string outputPath)
        {
            if (fileBytes == null || string.IsNullOrWhiteSpace(outputPath))
            {
                return false;
            }

            using MemoryStream stream = new(fileBytes);
            return UnzipFile(stream, outputPath);
        }

        public static bool UnzipFile(Stream inputStream, string outputPath)
        {
            if (inputStream == null || string.IsNullOrWhiteSpace(outputPath))
            {
                return false;
            }

            string root = Path.GetFullPath(outputPath);

            try
            {
                using ZipArchive archive = new(inputStream, ZipArchiveMode.Read);
                var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!IsPathInsideDirectory(root, destinationPath))
                    {
                        Debug.LogError($"[ZipArchiveExtractor] Unsafe zip entry path: {entry.FullName}");
                        return false;
                    }
                    if (!destinations.Add(destinationPath))
                    {
                        Debug.LogError($"[ZipArchiveExtractor] Duplicate zip entry path: {entry.FullName}");
                        return false;
                    }
                }

                Directory.CreateDirectory(root);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    string directory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    entry.ExtractToFile(destinationPath, true);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ZipArchiveExtractor] {e}");
                return false;
            }
        }

        private static bool IsPathInsideDirectory(string rootPath, string candidatePath)
        {
            string normalizedRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(rootPath));
            string normalizedCandidate = Path.GetFullPath(candidatePath);
            return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
