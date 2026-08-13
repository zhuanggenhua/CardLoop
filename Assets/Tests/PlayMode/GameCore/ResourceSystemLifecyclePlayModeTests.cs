using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using YokiFrame;
using YooAsset;

namespace GameCore.Tests
{
    /// <summary>
    /// 保护项目资源入口的唯一启动权和可预判失败语义。
    /// </summary>
    public sealed class ResourceSystemLifecyclePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return ResetResourceRuntime();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ResetResourceRuntime();
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenAlreadyInitialized_ThrowsInsteadOfPretendingSuccess()
        {
            yield return ResourceSystem.InitializeAsync().ToCoroutine();

            Assert.That(ResourceSystem.Initialized, Is.True);
            Exception failure = null;
            yield return ResourceSystem.InitializeAsync().ToCoroutine(exception => failure = exception);

            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains("重复初始化", failure.Message);
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenYokiFrameAlreadyOwnsResources_ThrowsWithoutTakingOwnership()
        {
            yield return YooInit.InitAsync().ToCoroutine();

            Assert.That(YooInit.Initialized, Is.True);
            Assert.That(YooAssets.IsInitialized, Is.True);

            Exception failure = null;
            yield return ResourceSystem.InitializeAsync().ToCoroutine(exception => failure = exception);

            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains("其它入口", failure.Message);
            Assert.That(ResourceSystem.Initialized, Is.False);
            Assert.That(YooInit.Initialized, Is.True);
            Assert.That(YooAssets.IsInitialized, Is.True);
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenConfigurationHasNoPackages_FailsBeforeCreatingGlobalResourceState()
        {
            var config = new YooInitConfig
            {
                PackageNames = new List<string>()
            };
            Exception failure = null;

            yield return ResourceSystem.InitializeAsync(config).ToCoroutine(exception => failure = exception);

            Assert.That(failure, Is.TypeOf<ArgumentException>());
            StringAssert.Contains("资源包列表不能为空", failure.Message);
            Assert.That(ResourceSystem.Initialized, Is.False);
            Assert.That(YooInit.Initialized, Is.False);
            Assert.That(YooAssets.IsInitialized, Is.False);
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenCustomModeHasNoHandler_FailsBeforeCreatingGlobalResourceState()
        {
            CustomInitModeHandler originalHandler = YooInit.CustomHandler;
            YooInit.CustomHandler = null;
            var config = new YooInitConfig
            {
                EditorPlayMode = EPlayMode.CustomPlayMode,
                PackageNames = new List<string> { "ResourceSystemCustomMode" }
            };
            Exception failure = null;

            yield return ResourceSystem.InitializeAsync(config).ToCoroutine(exception => failure = exception);

            YooInit.CustomHandler = originalHandler;

            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains("CustomPlayMode 缺少", failure.Message);
            Assert.That(ResourceSystem.Initialized, Is.False);
            Assert.That(YooInit.Initialized, Is.False);
            Assert.That(YooAssets.IsInitialized, Is.False);
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenResourceSystemOwnsResources_ReleasesTheOwnedRuntime()
        {
            yield return ResourceSystem.InitializeAsync().ToCoroutine();

            ResourceSystem.Shutdown();

            Assert.That(ResourceSystem.Initialized, Is.False);
            Assert.That(YooInit.Initialized, Is.False);
            Assert.That(YooAssets.IsInitialized, Is.False);
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenYokiFrameOwnsResources_ThrowsWithoutReleasingExternalState()
        {
            yield return YooInit.InitAsync().ToCoroutine();
            Exception failure = null;

            try
            {
                ResourceSystem.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains("其它入口", failure.Message);
            Assert.That(ResourceSystem.Initialized, Is.False);
            Assert.That(YooInit.Initialized, Is.True);
            Assert.That(YooAssets.IsInitialized, Is.True);
        }

        [UnityTest]
        public IEnumerator Shutdown_WhileInitializationIsPending_CancelsInitializationInsteadOfLeavingItRunning()
        {
            UniTask initialization = ResourceSystem.InitializeAsync();
            Assert.That(initialization.Status, Is.EqualTo(UniTaskStatus.Pending));

            Exception shutdownFailure = null;
            try
            {
                ResourceSystem.Shutdown();
            }
            catch (Exception exception)
            {
                shutdownFailure = exception;
            }

            Exception initializationFailure = null;
            yield return initialization.ToCoroutine(exception => initializationFailure = exception);

            if (ResourceSystem.Initialized)
            {
                ResourceSystem.Shutdown();
            }

            Assert.That(shutdownFailure, Is.Null);
            Assert.That(initializationFailure, Is.TypeOf<OperationCanceledException>());
            Assert.That(ResourceSystem.Initialized, Is.False);
            Assert.That(YooInit.Initialized, Is.False);
            Assert.That(YooAssets.IsInitialized, Is.False);
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhileCancelledInitializationIsRollingBack_RejectsReentry()
        {
            UniTask initialization = ResourceSystem.InitializeAsync();
            Assert.That(initialization.Status, Is.EqualTo(UniTaskStatus.Pending));

            ResourceSystem.Shutdown();

            Exception reentryFailure = null;
            yield return ResourceSystem.InitializeAsync().ToCoroutine(exception => reentryFailure = exception);

            Assert.That(reentryFailure, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains("仍持有未关闭", reentryFailure.Message);

            Exception initializationFailure = null;
            yield return initialization.ToCoroutine(exception => initializationFailure = exception);

            Assert.That(initializationFailure, Is.TypeOf<OperationCanceledException>());
            Assert.That(ResourceSystem.Initialized, Is.False);
            Assert.That(YooInit.Initialized, Is.False);
            Assert.That(YooAssets.IsInitialized, Is.False);
        }

        private static IEnumerator ResetResourceRuntime()
        {
            if (ResourceSystem.Initialized)
            {
                ResourceSystem.Shutdown();
            }

            if (YooInit.Initialized)
            {
                YooInit.Dispose();
            }

            if (YooAssets.IsInitialized)
            {
                YooAssets.Destroy();
            }

            yield return null;
        }
    }
}
