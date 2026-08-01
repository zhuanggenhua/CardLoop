using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FantasyWord.GameCore.Tests
{
    public sealed class ModRuntimeSecurityEditModeTests
    {
        [Test]
        public void ZipArchiveExtractor_RejectsSiblingPrefixTraversal()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "GameCoreModSecurityTests", Guid.NewGuid().ToString("N"));
            string modRoot = Path.Combine(tempRoot, "Mods");
            string zipPath = Path.Combine(tempRoot, "payload.zip");
            string escapedPath = Path.Combine(tempRoot, "Modsevil", "payload.txt");

            Directory.CreateDirectory(modRoot);
            Directory.CreateDirectory(tempRoot);
            CreateZipWithEntry(zipPath, "../Modsevil/payload.txt", "outside");

            try
            {
                LogAssert.Expect(LogType.Error, "[ZipArchiveExtractor] Unsafe zip entry path: ../Modsevil/payload.txt");
                bool extracted = ZipArchiveExtractor.UnzipFile(zipPath, modRoot);

                Assert.IsFalse(extracted, "zip 条目不能写到 Mod 根目录的同前缀兄弟目录。");
                Assert.IsFalse(File.Exists(escapedPath), "非法 zip 条目不应在 Mod 根目录外落盘。");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void DeleteModFromDisk_RejectsSiblingPrefixDirectory()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "GameCoreModSecurityTests", Guid.NewGuid().ToString("N"));
            string modRoot = Path.Combine(tempRoot, "Mods");
            string siblingDirectory = Path.Combine(tempRoot, "Modsevil");
            string marker = Path.Combine(siblingDirectory, "marker.txt");

            Directory.CreateDirectory(modRoot);
            Directory.CreateDirectory(siblingDirectory);
            File.WriteAllText(marker, "outside");

            try
            {
                ModInfo modInfo = new()
                {
                    apiVersion = ModAPI.DefaultAPIVersion,
                    modName = "Outside",
                    version = "1.0.0",
                    FilePath = siblingDirectory
                };

                LogAssert.Expect(LogType.Error, new Regex(@"\[ModAPI\] Refuse to delete mod outside loading root: .*Modsevil"));
                ModAPI.DeleteModFromDisk(modInfo, modRoot);

                Assert.IsTrue(Directory.Exists(siblingDirectory), "删除 Mod 时不能删除 Mod 根目录外的同前缀兄弟目录。");
                Assert.IsTrue(File.Exists(marker), "拒绝删除后目录内容应保持不变。");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void CreateZipWithEntry(string zipPath, string entryName, string content)
        {
            using FileStream stream = File.Create(zipPath);
            using ZipArchive archive = new(stream, ZipArchiveMode.Create);
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }
    }
}
