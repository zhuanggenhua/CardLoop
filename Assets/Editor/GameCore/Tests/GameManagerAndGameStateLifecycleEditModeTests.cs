using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using YokiFrame;
using Object = UnityEngine.Object;

namespace GameCore.Tests
{
    public sealed class GameManagerAndGameStateLifecycleEditModeTests
    {
        private static readonly List<string> LifecycleLog = new();
        private readonly List<Object> m_createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            SetStaticField(typeof(GameManager), "_instance", null);
            SetStaticField(typeof(GameManager), "_mainCamera", null);
            SetStaticField(typeof(GameManager), "_mainCameraRegistrationSource", null);
            LifecycleLog.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SetStaticField(typeof(GameManager), "_instance", null);
            SetStaticField(typeof(GameManager), "_mainCamera", null);
            SetStaticField(typeof(GameManager), "_mainCameraRegistrationSource", null);

            for (int i = m_createdObjects.Count - 1; i >= 0; i--)
            {
                if (m_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(m_createdObjects[i]);
                }
            }

            m_createdObjects.Clear();
            LifecycleLog.Clear();
        }

        [Test]
        public void SystemDiscovery_RegistersOnlySystemsConfiguredUnderGameManager()
        {
            GameObject gameManagerObject = CreateObject("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();

            GameObject childSystemObject = CreateObject("ChildSystem");
            childSystemObject.transform.SetParent(gameManagerObject.transform);
            ProbeSystem childSystem = childSystemObject.AddComponent<ProbeSystem>();

            GameObject externalSystemObject = CreateObject("ExternalSystem");
            ProbeSystem externalSystem = externalSystemObject.AddComponent<ProbeSystem>();

            SetStaticField(typeof(GameManager), "_instance", gameManager);
            InvokeInstanceMethod(gameManager, "FindSystems");

            Assert.IsTrue(GameManager.TryGetSystem(out ProbeSystem registeredSystem));
            Assert.AreSame(childSystem, registeredSystem);
            Assert.AreNotSame(externalSystem, registeredSystem);
        }

        [Test]
        public void MainCamera_UsesExplicitSceneRegistration()
        {
            GameObject cameraObject = CreateObject("未打标签的正式玩法相机");
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject registrationSource = CreateObject("正式玩法相机注册入口");

            GameManager.RegisterMainCamera(camera, registrationSource);

            Assert.That(GameManager.MainCamera, Is.SameAs(camera));

            GameManager.UnregisterMainCamera(camera, registrationSource);

            Assert.That(GameManager.MainCamera, Is.Null);
        }

        [Test]
        public void GameStateSystem_RepeatedDirectStartThrowsAndRestartAfterStopRemainsValid()
        {
            GameManager gameManager = CreateGameManagerWithInputSystem();
            GameStateSystem gameStateSystem = CreateObject("GameStateSystem").AddComponent<GameStateSystem>();
            SetInstanceField(gameManager, "m_systems", new Dictionary<System.Type, AGameSystem>
            {
                [typeof(InputSystem)] = gameManager.GetComponentInChildren<InputSystem>(),
                [typeof(GameStateSystem)] = gameStateSystem,
            });
            SetStaticField(typeof(GameManager), "_instance", gameManager);

            gameStateSystem.OnSystemStart();
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => gameStateSystem.OnSystemStart());
            StringAssert.Contains("重复启动", exception.Message);

            gameStateSystem.RemoveLayer(EGameState.Gameplay);

            Assert.AreEqual(EGameState.None, gameStateSystem.currentState);

            gameStateSystem.OnSystemStop();

            Assert.AreEqual(EGameState.None, gameStateSystem.currentState);
            Assert.AreEqual(1.0f, Time.timeScale);

            gameStateSystem.OnSystemStart();
            Assert.AreEqual(EGameState.Gameplay, gameStateSystem.currentState);
            gameStateSystem.OnSystemStop();
        }

        [Test]
        public void SystemLifecycle_UsesDeclaredDependenciesAndReleasesInReverseOrder()
        {
            GameObject gameManagerObject = CreateObject("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();

            CreateChildSystem<DependentLifecycleProbeSystem>(gameManagerObject, "Dependent");
            CreateChildSystem<DependencyLifecycleProbeSystem>(gameManagerObject, "Dependency");

            InvokeInstanceMethod(gameManager, "FindSystems");
            InvokeInstanceMethod(gameManager, "InitializeSystems");
            InvokeInstanceMethod(gameManager, "StartSystems");
            InvokeInstanceMethod(gameManager, "ShutdownSystems");

            CollectionAssert.AreEqual(
                new[]
                {
                    "Dependency.Init",
                    "Dependent.Init",
                    "Dependency.Start",
                    "Dependent.Start",
                    "Dependent.Stop",
                    "Dependency.Stop",
                    "Dependent.Shutdown",
                    "Dependency.Shutdown",
                },
                LifecycleLog);
        }

        [Test]
        public void SystemLifecycle_RepeatedDirectStartThrowsInsteadOfBeingIgnored()
        {
            GameObject gameManagerObject = CreateObject("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            CreateChildSystem<DependencyLifecycleProbeSystem>(gameManagerObject, "Dependency");

            InvokeInstanceMethod(gameManager, "FindSystems");
            InvokeInstanceMethod(gameManager, "InitializeSystems");
            TargetInvocationException initializationException = Assert.Throws<TargetInvocationException>(
                () => InvokeInstanceMethod(gameManager, "InitializeSystems"));
            Assert.That(initializationException.InnerException, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains("重复初始化", initializationException.InnerException.Message);

            InvokeInstanceMethod(gameManager, "StartSystems");

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => InvokeInstanceMethod(gameManager, "StartSystems"));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains("重复启动", exception.InnerException.Message);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Dependency.Init",
                    "Dependency.Start",
                },
                LifecycleLog);

            InvokeInstanceMethod(gameManager, "ShutdownSystems");
        }

        [Test]
        public void SystemDiscovery_RejectsMissingStartupDependency()
        {
            GameObject gameManagerObject = CreateObject("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            CreateChildSystem<MissingDependencyLifecycleProbeSystem>(gameManagerObject, "MissingDependency");

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => InvokeInstanceMethod(gameManager, "FindSystems"));

            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains(nameof(UnconfiguredLifecycleProbeSystem), exception.InnerException.Message);
        }

        [Test]
        public void SystemDiscovery_RejectsStartupDependencyCycle()
        {
            GameObject gameManagerObject = CreateObject("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            CreateChildSystem<CycleALifecycleProbeSystem>(gameManagerObject, "CycleA");
            CreateChildSystem<CycleBLifecycleProbeSystem>(gameManagerObject, "CycleB");

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => InvokeInstanceMethod(gameManager, "FindSystems"));

            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains("dependency cycle", exception.InnerException.Message);
        }

        [Test]
        public void SystemStartFailure_StopsOnlySystemsThatEnteredStartLifecycle()
        {
            GameObject gameManagerObject = CreateObject("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            CreateChildSystem<DependencyLifecycleProbeSystem>(gameManagerObject, "Dependency");
            ThrowingStartLifecycleProbeSystem throwingSystem =
                CreateChildSystem<ThrowingStartLifecycleProbeSystem>(gameManagerObject, "ThrowingStart");
            CreateChildSystem<NeverStartedLifecycleProbeSystem>(gameManagerObject, "NeverStarted");
            throwingSystem.ThrowOnStart = true;

            InvokeInstanceMethod(gameManager, "FindSystems");
            InvokeInstanceMethod(gameManager, "InitializeSystems");

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => InvokeInstanceMethod(gameManager, "StartSystems"));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());

            CollectionAssert.AreEqual(
                new[]
                {
                    "Dependency.Init",
                    "ThrowingStart.Init",
                    "NeverStarted.Init",
                    "Dependency.Start",
                    "ThrowingStart.Start",
                    "ThrowingStart.Stop",
                    "Dependency.Stop",
                },
                LifecycleLog);

            InvokeInstanceMethod(gameManager, "ShutdownSystems");
        }

        [Test]
        public void SystemInitFailure_ShutsDownOnlySystemsThatEnteredInitializationLifecycle()
        {
            GameObject gameManagerObject = CreateObject("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            CreateChildSystem<DependencyLifecycleProbeSystem>(gameManagerObject, "Dependency");
            ThrowingInitLifecycleProbeSystem throwingSystem =
                CreateChildSystem<ThrowingInitLifecycleProbeSystem>(gameManagerObject, "ThrowingInit");
            CreateChildSystem<NeverInitializedLifecycleProbeSystem>(gameManagerObject, "NeverInitialized");
            throwingSystem.ThrowOnInit = true;

            InvokeInstanceMethod(gameManager, "FindSystems");

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => InvokeInstanceMethod(gameManager, "InitializeSystems"));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());

            CollectionAssert.AreEqual(
                new[]
                {
                    "Dependency.Init",
                    "ThrowingInit.Init",
                    "ThrowingInit.Shutdown",
                    "Dependency.Shutdown",
                },
                LifecycleLog);
        }

        [Test]
        public async Task TransitionSystem_ImplementsSceneKitTransitionLifecycle()
        {
            TransitionSystem transitionSystem = CreateObject("TransitionSystem").AddComponent<TransitionSystem>();
            Assert.That(transitionSystem, Is.AssignableTo<ISceneTransitionUniTask>());

            await transitionSystem.FadeOutUniTaskAsync();
            Assert.That(transitionSystem.IsTransitioning, Is.True);
            Assert.That(transitionSystem.Progress, Is.EqualTo(0.5f));

            await transitionSystem.FadeInUniTaskAsync();
            Assert.That(transitionSystem.IsTransitioning, Is.False);
            Assert.That(transitionSystem.Progress, Is.EqualTo(1f));
        }

        private GameManager CreateGameManagerWithInputSystem()
        {
            GameObject gameManagerObject = CreateObject("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            GameObject inputObject = CreateObject("InputSystem");
            inputObject.transform.SetParent(gameManagerObject.transform);
            PlayerInput playerInput = inputObject.AddComponent<PlayerInput>();
            InputActionAsset actionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            actionAsset.AddActionMap("Gameplay");
            actionAsset.AddActionMap("UI");
            actionAsset.AddActionMap("None");
            playerInput.actions = actionAsset;
            playerInput.ActivateInput();
            SetInstanceField(playerInput, "m_Enabled", true);
            m_createdObjects.Add(actionAsset);

            InputSystem inputSystem = inputObject.AddComponent<InputSystem>();
            SetInstanceField(inputSystem, "m_playerInput", playerInput);
            SetStaticField(typeof(GameManager), "_instance", gameManager);
            return gameManager;
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new(name);
            m_createdObjects.Add(gameObject);
            return gameObject;
        }

        private T CreateChildSystem<T>(GameObject parent, string name) where T : AGameSystem
        {
            GameObject systemObject = CreateObject(name);
            systemObject.transform.SetParent(parent.transform);
            return systemObject.AddComponent<T>();
        }

        private static void InvokeInstanceMethod(object target, string methodName)
        {
            MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
            Assert.IsNotNull(method, $"找不到生命周期方法 {target.GetType().Name}.{methodName}");
            method.Invoke(target, null);
        }

        private static void SetInstanceField(object target, string fieldName, object value)
        {
            FieldInfo field = FindInstanceField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static void SetStaticField(System.Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到静态字段 {type.Name}.{fieldName}");
            field.SetValue(null, value);
        }

        private static FieldInfo FindInstanceField(System.Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindInstanceMethod(System.Type type, string methodName)
        {
            while (type != null)
            {
                MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }

        private sealed class ProbeSystem : AGameSystem
        {
        }

        private abstract class LifecycleProbeSystem : AGameSystem
        {
            public bool ThrowOnInit { get; set; }
            public bool ThrowOnStart { get; set; }

            protected abstract string LogName { get; }

            public override void OnSystemInit()
            {
                LifecycleLog.Add($"{LogName}.Init");
                if (ThrowOnInit)
                {
                    throw new InvalidOperationException($"{LogName} init failed.");
                }
            }

            public override void OnSystemStart()
            {
                LifecycleLog.Add($"{LogName}.Start");
                if (ThrowOnStart)
                {
                    throw new InvalidOperationException($"{LogName} start failed.");
                }
            }

            public override void OnSystemStop()
            {
                LifecycleLog.Add($"{LogName}.Stop");
            }

            public override void OnSystemShutdown()
            {
                LifecycleLog.Add($"{LogName}.Shutdown");
            }
        }

        private sealed class DependencyLifecycleProbeSystem : LifecycleProbeSystem
        {
            protected override string LogName => "Dependency";
        }

        private sealed class DependentLifecycleProbeSystem : LifecycleProbeSystem
        {
            private static readonly System.Type[] Dependencies = { typeof(DependencyLifecycleProbeSystem) };

            protected override string LogName => "Dependent";
            public override IReadOnlyCollection<System.Type> StartupDependencies => Dependencies;
        }

        private sealed class UnconfiguredLifecycleProbeSystem : LifecycleProbeSystem
        {
            protected override string LogName => "Unconfigured";
        }

        private sealed class MissingDependencyLifecycleProbeSystem : LifecycleProbeSystem
        {
            private static readonly System.Type[] Dependencies = { typeof(UnconfiguredLifecycleProbeSystem) };

            protected override string LogName => "MissingDependency";
            public override IReadOnlyCollection<System.Type> StartupDependencies => Dependencies;
        }

        private sealed class CycleALifecycleProbeSystem : LifecycleProbeSystem
        {
            private static readonly System.Type[] Dependencies = { typeof(CycleBLifecycleProbeSystem) };

            protected override string LogName => "CycleA";
            public override IReadOnlyCollection<System.Type> StartupDependencies => Dependencies;
        }

        private sealed class CycleBLifecycleProbeSystem : LifecycleProbeSystem
        {
            private static readonly System.Type[] Dependencies = { typeof(CycleALifecycleProbeSystem) };

            protected override string LogName => "CycleB";
            public override IReadOnlyCollection<System.Type> StartupDependencies => Dependencies;
        }

        private sealed class ThrowingStartLifecycleProbeSystem : LifecycleProbeSystem
        {
            protected override string LogName => "ThrowingStart";
        }

        private sealed class NeverStartedLifecycleProbeSystem : LifecycleProbeSystem
        {
            protected override string LogName => "NeverStarted";
        }

        private sealed class ThrowingInitLifecycleProbeSystem : LifecycleProbeSystem
        {
            protected override string LogName => "ThrowingInit";
        }

        private sealed class NeverInitializedLifecycleProbeSystem : LifecycleProbeSystem
        {
            protected override string LogName => "NeverInitialized";
        }
    }
}
