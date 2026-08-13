using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YokiFrame;
using YooAsset;
using Object = UnityEngine.Object;

namespace GameCore.Tests
{
    /// <summary>
    /// 验证进程组合入口只关闭自己成功接管的基础设施。
    /// </summary>
    public sealed class GameManagerInfrastructureLifecyclePlayModeTests
    {
        private GameObject m_gameManagerObject;
        private GameConfig m_gameConfig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return DestroyGameManager();
            ResetResourceRuntime();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyGameManager();

            if (m_gameConfig != null)
            {
                Object.Destroy(m_gameConfig);
            }

            ResetResourceRuntime();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Start_WhenResourcesAreOwnedExternally_FailsWithoutTryingToCloseThem()
        {
            yield return YooInit.InitAsync().ToCoroutine();

            Assert.That(YooInit.Initialized, Is.True);
            Assert.That(YooAssets.IsInitialized, Is.True);

            m_gameConfig = ScriptableObject.CreateInstance<GameConfig>();
            m_gameManagerObject = new GameObject("GameManager Infrastructure Lifecycle Test");
            GameManager gameManager = m_gameManagerObject.AddComponent<GameManager>();
            SetInstanceField(gameManager, "m_config", m_gameConfig);

            LogAssert.Expect(
                LogType.Exception,
                new Regex("YokiFrame\\.YooInit 已由其它入口初始化。"));

            float timeoutAt = Time.realtimeSinceStartup + 5f;
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "GameManager 启动失败用例超时。");
                yield return null;
            }

            Assert.That(GameManager.StartupState, Is.EqualTo(GameManagerStartupState.Failed));
            Assert.That(GameManager.StartupException, Is.TypeOf<InvalidOperationException>());
            Assert.That(YooInit.Initialized, Is.True);
            Assert.That(YooAssets.IsInitialized, Is.True);
        }

        private IEnumerator DestroyGameManager()
        {
            if (GameManager.Exists())
            {
                Object.Destroy(GameManager.Instance.gameObject);
                yield return null;
            }

            if (m_gameManagerObject != null)
            {
                Object.Destroy(m_gameManagerObject);
                yield return null;
            }

            m_gameManagerObject = null;
        }

        private static void ResetResourceRuntime()
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
        }

        private static void SetInstanceField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"找不到测试所需字段 {fieldName}。");
            field.SetValue(target, value);
        }
    }
}
