using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameCore.Tests
{
    public sealed class ModRuntimeSecurityEditModeTests
    {
        [Test]
        public void UnZipAll_WhenArchiveIsInvalid_ThrowsAndKeepsOriginalArchive()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "GameCoreModSecurityTests", Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(tempRoot, "broken.zip");
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(zipPath, "not a zip archive");

            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[ZipArchiveExtractor\].*"));
                InvalidDataException exception = Assert.Throws<InvalidDataException>(
                    () => ModLoader.ExtractArchive(zipPath));

                StringAssert.Contains(zipPath, exception.Message);
                Assert.That(File.Exists(zipPath), Is.True);
                Assert.That(Directory.Exists(Path.Combine(tempRoot, "broken")), Is.False);
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
        public void ExtractArchive_UsesDedicatedDirectoryAndRefusesExistingTarget()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "GameCoreModSecurityTests", Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(tempRoot, "example.zip");
            string outputDirectory = Path.Combine(tempRoot, "example");
            Directory.CreateDirectory(tempRoot);
            CreateZipWithEntry(zipPath, "manifest.cfg", "{}");

            try
            {
                ModLoader.ExtractArchive(zipPath);

                Assert.That(File.Exists(zipPath), Is.False);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "manifest.cfg")), Is.True);

                CreateZipWithEntry(zipPath, "replacement.cfg", "{}");
                InvalidDataException exception = Assert.Throws<InvalidDataException>(
                    () => ModLoader.ExtractArchive(zipPath));

                StringAssert.Contains("目标目录已经存在", exception.Message);
                Assert.That(File.Exists(zipPath), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "replacement.cfg")), Is.False);
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
        public void ZipArchiveExtractor_RejectsDuplicateDestinationPaths()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "GameCoreModSecurityTests", Guid.NewGuid().ToString("N"));
            string modRoot = Path.Combine(tempRoot, "Mod");
            string zipPath = Path.Combine(tempRoot, "duplicates.zip");
            Directory.CreateDirectory(tempRoot);
            using (FileStream stream = File.Create(zipPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                archive.CreateEntry("Data.cfg");
                archive.CreateEntry("data.cfg");
            }

            try
            {
                LogAssert.Expect(LogType.Error, "[ZipArchiveExtractor] Duplicate zip entry path: data.cfg");
                Assert.That(ZipArchiveExtractor.UnzipFile(zipPath, modRoot), Is.False);
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

                InvalidDataException exception = Assert.Throws<InvalidDataException>(
                    () => ModAPI.DeleteModFromDisk(modInfo, modRoot));

                StringAssert.Contains("Mod 根目录外", exception.Message);
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
