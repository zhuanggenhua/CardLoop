using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace GameCore.Tests
{
    /// <summary>
    /// 保护进程级 Mod 初始化的唯一入口，避免重复调用伪装成成功。
    /// </summary>
    public sealed class ModApiLifecycleEditModeTests
    {
        private string m_testRoot;

        [SetUp]
        public void SetUp()
        {
            ModAPI.Shutdown();
            m_testRoot = Path.Combine(
                Application.temporaryCachePath,
                $"CardLoop-ModApiLifecycle-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            ModAPI.Shutdown();
            if (Directory.Exists(m_testRoot))
            {
                Directory.Delete(m_testRoot, recursive: true);
            }
        }

        [Test]
        public void ModConfig_WhenExistingFileIsInvalid_ThrowsWithoutReplacingIt()
        {
            string configPath = Path.Combine(m_testRoot, "broken-config.json");
            const string invalidContent = "{ not valid json";
            File.WriteAllText(configPath, invalidContent);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ModConfig.LoadOrCreate(configPath));

			StringAssert.Contains(Path.GetFullPath(configPath), exception.Message);
            Assert.That(File.ReadAllText(configPath), Is.EqualTo(invalidContent));
        }

        [Test]
        public void ModConfig_WhenExistingFileContainsNull_ThrowsInsteadOfCreatingDefaults()
        {
            string configPath = Path.Combine(m_testRoot, "null-config.json");
            File.WriteAllText(configPath, "null");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ModConfig.LoadOrCreate(configPath));

			StringAssert.Contains(Path.GetFullPath(configPath), exception.Message);
            Assert.That(File.ReadAllText(configPath), Is.EqualTo("null"));
        }

        [Test]
        public void ModConfig_WhenStateIdentityIsDuplicated_ThrowsInsteadOfKeepingTwoTruths()
        {
            string configPath = Path.Combine(m_testRoot, "duplicate-state-config.json");
            File.WriteAllText(
                configPath,
                "{\"ApiVersion\":\"0.1.0\",\"States\":[" +
                "{\"modId\":\"author.example\",\"status\":0}," +
                "{\"modId\":\"author.example\",\"status\":1}]}");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ModConfig.LoadOrCreate(configPath));

            StringAssert.Contains("重复", exception.Message);
            StringAssert.Contains("author.example", exception.InnerException?.Message ?? exception.Message);
        }

        [Test]
        public void Initialize_WhenProvidedConfigHasNoLoadingPath_ThrowsBeforeCallingLoader()
        {
            var config = new ModConfig { LoadingPath = null };
            var loader = new CountingModLoader();

            Assert.Throws<InvalidDataException>(
                () => ModAPI.Initialize(config, loader).GetAwaiter().GetResult());

            Assert.That(loader.CallCount, Is.Zero);
            Assert.That(ModAPI.Initialized, Is.False);
        }

        [Test]
        public void ModConfig_SaveAtomically_ReplacesExistingFileWithoutLeavingTemporaryFile()
        {
            string configPath = Path.Combine(m_testRoot, "atomic-config.json");
            ModConfig config = ModConfig.LoadOrCreate(configPath);
            config.ApiVersion = "1.2.3";
            config.Save();

            config.ApiVersion = "2.0.0";
            config.Save();

            ModConfig restored = ModConfig.LoadOrCreate(configPath);
            Assert.That(restored.ApiVersion, Is.EqualTo("2.0.0"));
            Assert.That(File.Exists(configPath + ".tmp"), Is.False);
        }

        [Test]
        public void Initialize_WhenConfiguredModIsTemporarilyMissing_PreservesItsState()
        {
            string configPath = Path.Combine(m_testRoot, "missing-mod-config.json");
            ModConfig config = ModConfig.LoadOrCreate(configPath);
            var missingMod = new ModInfo { modId = "author.missing" };
            config.SetModEnabled(missingMod, false);
            config.Save();

            ModAPI.Initialize(config, new EmptyModLoader()).GetAwaiter().GetResult();

            ModConfig restored = ModConfig.LoadOrCreate(configPath);
            Assert.That(restored.GetModState(missingMod), Is.EqualTo(ModStatus.Disabled));
        }

        [Test]
        public void Initialize_WhenAlreadyInitialized_ThrowsInsteadOfPretendingSuccess()
        {
            ModConfig config = ModConfig.LoadOrCreate(Path.Combine(m_testRoot, "config.json"));
            config.LoadingPath = Path.Combine(m_testRoot, "Mods");
            var loader = new EmptyModLoader();

            ModAPI.Initialize(config, loader).GetAwaiter().GetResult();

            Assert.That(ModAPI.Initialized, Is.True);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ModAPI.Initialize(config, loader).GetAwaiter().GetResult());
            StringAssert.Contains("重复", exception.Message);
        }

        [Test]
        public void DeleteMod_MarksNextStartupDeletionWithoutHidingCurrentRuntimeMod()
        {
            string configPath = Path.Combine(m_testRoot, "delete-config.json");
            ModConfig config = ModConfig.LoadOrCreate(configPath);
            config.LoadingPath = Path.Combine(m_testRoot, "Mods");
            ModAPI.Initialize(config, new ResultModLoader(true, "Loaded")).GetAwaiter().GetResult();
            ModInfo loaded = ModAPI.CreateInfoSnapshot()[0];

            ModAPI.DeleteMod(loaded);

            Assert.That(ModAPI.GetModState(loaded), Is.EqualTo(ModStatus.Delete));
            Assert.That(ModAPI.CreateInfoSnapshot(), Does.Contain(loaded));
        }

        [Test]
        public void Initialize_WhenLoaderReportsFailure_ThrowsAndCanRetryWithFreshSnapshot()
        {
            ModConfig config = ModConfig.LoadOrCreate(Path.Combine(m_testRoot, "config.json"));
            config.LoadingPath = Path.Combine(m_testRoot, "Mods");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ModAPI.Initialize(config, new ResultModLoader(false, "Stale")).GetAwaiter().GetResult());

            StringAssert.Contains("加载失败", exception.Message);
            Assert.That(ModAPI.Initialized, Is.False);

            ModAPI.Initialize(config, new ResultModLoader(true, "Fresh")).GetAwaiter().GetResult();

            ModInfo[] snapshot = ModAPI.CreateInfoSnapshot();
            Assert.That(snapshot, Has.Length.EqualTo(1));
            Assert.That(snapshot[0].modName, Is.EqualTo("Fresh"));
        }

        [Test]
        public void Initialize_WhenAnotherInitializationIsPending_RejectsReentryAndShutdownCancelsLateCommit()
        {
            ModConfig config = ModConfig.LoadOrCreate(Path.Combine(m_testRoot, "config.json"));
            config.LoadingPath = Path.Combine(m_testRoot, "Mods");
            var pendingLoader = new PendingModLoader();

            UniTask initialization = ModAPI.Initialize(config, pendingLoader);

            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => ModAPI.Initialize(config, new EmptyModLoader()).GetAwaiter().GetResult());
                StringAssert.Contains("正在初始化", exception.Message);
            }
            finally
            {
                ModAPI.Shutdown();
                pendingLoader.Complete();
            }

            Assert.Throws<OperationCanceledException>(() => initialization.GetAwaiter().GetResult());
            Assert.That(ModAPI.Initialized, Is.False);
        }

        [Test]
        public void Initialize_WhenExternalCancellationIsRequested_RejectsLateCommit()
        {
            ModConfig config = ModConfig.LoadOrCreate(Path.Combine(m_testRoot, "config.json"));
            config.LoadingPath = Path.Combine(m_testRoot, "Mods");
            var pendingLoader = new PendingModLoader();
            using var cancellation = new CancellationTokenSource();

            UniTask initialization = ModAPI.Initialize(config, pendingLoader, cancellation.Token);
            cancellation.Cancel();
            pendingLoader.Complete();

            Assert.Throws<OperationCanceledException>(() => initialization.GetAwaiter().GetResult());
            Assert.That(ModAPI.Initialized, Is.False);
        }

        private sealed class EmptyModLoader : IModLoader
        {
            public UniTask<bool> LoadAllModsAsync(
                List<ModInfo> modInfos,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return UniTask.FromResult(true);
            }
        }

        private sealed class CountingModLoader : IModLoader
        {
            public int CallCount { get; private set; }

            public UniTask<bool> LoadAllModsAsync(
                List<ModInfo> modInfos,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return UniTask.FromResult(true);
            }
        }

        private sealed class ResultModLoader : IModLoader
        {
            private readonly bool m_result;
            private readonly string m_modName;

            public ResultModLoader(bool result, string modName)
            {
                m_result = result;
                m_modName = modName;
            }

            public UniTask<bool> LoadAllModsAsync(
                List<ModInfo> modInfos,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                modInfos.Add(new ModInfo
                {
                    modId = $"tests.{m_modName.ToLowerInvariant()}",
                    apiVersion = ModAPI.DefaultAPIVersion,
                    modName = m_modName,
                    version = "1.0.0",
                    packageName = $"Tests{m_modName}"
                });
                return UniTask.FromResult(m_result);
            }
        }

        private sealed class PendingModLoader : IModLoader
        {
            private readonly UniTaskCompletionSource m_completion = new();

            public async UniTask<bool> LoadAllModsAsync(
                List<ModInfo> modInfos,
                CancellationToken cancellationToken)
            {
                await m_completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }

            public void Complete()
            {
                m_completion.TrySetResult();
            }
        }
    }
}
