using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YokiFrame;

namespace GameCore.Tests
{
    public sealed class PersistenceSystemRegistrationEditModeTests
    {
        private readonly List<UnityEngine.Object> m_createdObjects = new();
        private PersistenceSystem m_persistenceSystem;

        [SetUp]
        public void SetUp()
        {
            CreateGameManagerWithPersistenceSystem();
            m_persistenceSystem.OnSystemStart();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_persistenceSystem != null)
            {
                m_persistenceSystem.OnSystemStop();
            }

            SetStaticField(typeof(GameManager), "_instance", null);

            for (int i = m_createdObjects.Count - 1; i >= 0; i--)
            {
                if (m_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_createdObjects[i]);
                }
            }

            m_createdObjects.Clear();
        }

        [Test]
        public void CreateDataBlock_UsesRegisteredPreInstancedPersistables()
        {
            PersistenceProbe probe = CreatePreInstancedProbe("registered-pre-instanced", true);
            probe.gameObject.SetActive(false);

            PersistenceDataBlock block = m_persistenceSystem.CreateDataBlock();

            Assert.AreEqual(1, block.objects.Length, "已登记的预摆持久化对象应进入保存块。");
            Assert.AreEqual(1, probe.SaveCount);
            Assert.AreEqual(
                EPersistableObjectState.Inactive,
                block.objects[0].state,
                "保存应保留登记对象当前的禁用状态。");
        }

        [Test]
        public void CreateDataBlock_AfterDisablePersistence_SkipsRegisteredPersistable()
        {
            PersistenceProbe probe = CreatePreInstancedProbe("disabled-pre-instanced", true);

            probe.DisablePersistence();
            PersistenceDataBlock block = m_persistenceSystem.CreateDataBlock();

            Assert.AreEqual(0, block.objects.Length, "关闭持久化后不应继续进入自动保存集合。");
            Assert.AreEqual(0, probe.SaveCount);
        }

        [Test]
        public void SceneLoadedEvent_LoadsRegisteredPreInstancedPersistables()
        {
            PersistenceProbe probe = CreatePreInstancedProbe("load-registered-pre-instanced", true);
            PersistableDataBlock savedBlock = new()
            {
                info = new PreInstancedPersistentDataHandler
                {
                    identifier = probe.GetPersistentIdentifier()
                },
                state = EPersistableObjectState.Active
            };

            m_persistenceSystem.LoadDataBlock(new PersistenceDataBlock
            {
                objects = new[] { savedBlock }
            });
            EventKit.Type.Send(new SceneLoadedEvent());

            Assert.AreEqual(1, probe.LoadCount, "地图加载应把保存块应用到已登记的预摆对象。");
            Assert.AreSame(savedBlock, probe.LastLoadedBlock);
        }

        [Test]
        public void RegisterCustomInstancedPersistable_RemovesPreviousIdentifierMapping()
        {
            const string oldIdentifier = "pre-instanced-player-id";
            const string newIdentifier = "player";
            PersistenceProbe probe = CreatePreInstancedProbe(oldIdentifier, true);

            Assert.IsTrue(
                m_persistenceSystem.TryResolvePersistable(oldIdentifier, out PersistenceProbe oldResolvedBefore),
                "预摆对象登记后应能通过原始 ID 解析。");
            Assert.AreSame(probe, oldResolvedBefore);

            m_persistenceSystem.RegisterCustomInstancedPersistable(probe, newIdentifier);

            Assert.IsFalse(
                m_persistenceSystem.TryResolvePersistable(oldIdentifier, out PersistenceProbe _),
                "同一对象改登为自定义实例后，旧预摆 ID 不应继续解析到玩家。");
            Assert.IsTrue(
                m_persistenceSystem.TryResolvePersistable(newIdentifier, out PersistenceProbe newResolved),
                "同一对象改登为自定义实例后，应能通过新 ID 解析。");
            Assert.AreSame(probe, newResolved);
        }

        private void CreateGameManagerWithPersistenceSystem()
        {
            GameObject persistenceObject = new("持久化登记测试系统");
            m_createdObjects.Add(persistenceObject);
            m_persistenceSystem = persistenceObject.AddComponent<PersistenceSystem>();

            GameObject mapObject = new("持久化登记测试地图系统");
            m_createdObjects.Add(mapObject);
            MapSystem mapSystem = mapObject.AddComponent<MapSystem>();

            GameObject gameManagerObject = new("持久化登记测试 GameManager");
            m_createdObjects.Add(gameManagerObject);
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();

            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            m_createdObjects.Add(config);

            Dictionary<Type, AGameSystem> systems = new()
            {
                [typeof(MapSystem)] = mapSystem,
                [typeof(PersistenceSystem)] = m_persistenceSystem
            };

            SetInstanceField(gameManager, "m_config", config);
            SetInstanceField(gameManager, "m_systems", systems);
            SetStaticField(typeof(GameManager), "_instance", gameManager);
        }

        private PersistenceProbe CreatePreInstancedProbe(string identifier, bool autoPersist)
        {
            GameObject probeObject = new(identifier);
            m_createdObjects.Add(probeObject);

            PersistenceProbe probe = probeObject.AddComponent<PersistenceProbe>();
            SetInstanceField(probe, "m_autoPersistWhenPreInstanced", autoPersist);
            SetInstanceField(
                probe,
                "m_persistenceInfo",
                new PreInstancedPersistentDataHandler
                {
                    identifier = identifier
                });
            InvokeLifecycle(probe, "Awake");
            return probe;
        }

        private static void InvokeLifecycle(Component component, string methodName)
        {
            MethodInfo method = FindInstanceMethod(component.GetType(), methodName);
            Assert.IsNotNull(
                method,
                $"找不到生命周期方法 {component.GetType().Name}.{methodName}");
            method.Invoke(component, null);
        }

        private static void SetInstanceField(object target, string fieldName, object value)
        {
            FieldInfo field = FindInstanceField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static void SetStaticField(Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到静态字段 {type.Name}.{fieldName}");
            field.SetValue(null, value);
        }

        private static FieldInfo FindInstanceField(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindInstanceMethod(Type type, string methodName)
        {
            while (type != null)
            {
                MethodInfo method = type.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }
    }

    public sealed class PersistenceProbe : Persistable
    {
        public int SaveCount { get; private set; }

        public int LoadCount { get; private set; }

        public PersistableDataBlock LastLoadedBlock { get; private set; }

        protected override void OnSave(PersistableDataBlock block)
        {
            SaveCount++;
        }

        protected override void OnLoad(PersistableDataBlock block)
        {
            LoadCount++;
            LastLoadedBlock = block;
        }
    }
}
