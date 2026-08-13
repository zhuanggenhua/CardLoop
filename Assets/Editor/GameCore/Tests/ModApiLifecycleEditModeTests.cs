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
            public UniTask<bool> LoadAllModsAsync(List<ModInfo> modInfos)
            {
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

            public UniTask<bool> LoadAllModsAsync(List<ModInfo> modInfos)
            {
                modInfos.Add(new ModInfo
                {
                    apiVersion = ModAPI.DefaultAPIVersion,
                    modName = m_modName,
                    version = "1.0.0"
                });
                return UniTask.FromResult(m_result);
            }
        }

        private sealed class PendingModLoader : IModLoader
        {
            private readonly UniTaskCompletionSource m_completion = new();

            public async UniTask<bool> LoadAllModsAsync(List<ModInfo> modInfos)
            {
                await m_completion.Task;
                return true;
            }

            public void Complete()
            {
                m_completion.TrySetResult();
            }
        }
    }
}
