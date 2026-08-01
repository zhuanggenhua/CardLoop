using System;
using System.Reflection;
using GAS.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyWord.GameCore.Tests
{
    public sealed class TerrainSurfaceDamageSystemEditModeTests
    {
        private readonly System.Collections.Generic.List<UnityEngine.Object> m_createdObjects =
            new();
        private GameObject m_gridObject;
        private Tilemap m_tilemap;
        private TerrainNavigationMap m_navigationMap;
        private TerrainSurfaceDamageSystem m_damageSystem;

        [SetUp]
        public void SetUp()
        {
            GasEditModeTestHelper.ResetWorld();
            CreateGameManagerWithMinimalConfig();
            CreateTerrainMap();
            CreateDamageSystem();
        }

        [TearDown]
        public void TearDown()
        {
            SetStaticField(typeof(GameManager), "_instance", null);

            for (int i = m_createdObjects.Count - 1; i >= 0; i--)
            {
                if (m_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_createdObjects[i]);
                }
            }

            m_createdObjects.Clear();
            GasEditModeTestHelper.ShutdownWorld();
        }

        [Test]
        public void BurningContactDamage_UsesFormalDamagePipeline()
        {
            Vector3Int burningCell = new(0, 0, 0);
            AuthorGrassCell(burningCell);
            Vector3 cellCenter = m_tilemap.GetCellCenterWorld(burningCell);
            Assert.IsTrue(m_navigationMap.SetRuntimeSurfaceState(
                cellCenter,
                ETerrainRuntimeSurfaceState.Burning));
            CharacterActor target = CreateCharacter(
                "burning-contact-target",
                cellCenter,
                CreateStats(health: 10));
            int previousHealth = target.GetCurrentHealth();

            bool applied = InvokeTryApplyBurningContactDamage(target);

            Assert.IsTrue(applied, "站在 Burning 地表上的角色应通过正式伤害链受到地表伤害。");
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => target.GetCurrentHealth() == previousHealth - 1,
                maxTicks: 8);
            Assert.AreEqual(previousHealth - 1, target.GetCurrentHealth());
        }

        [Test]
        public void BurningContactDamage_IgnoresNonBurningCell()
        {
            Vector3Int safeCell = new(0, 0, 0);
            AuthorGrassCell(safeCell);
            CharacterActor target = CreateCharacter(
                "safe-contact-target",
                m_tilemap.GetCellCenterWorld(safeCell),
                CreateStats(health: 10));
            int previousHealth = target.GetCurrentHealth();

            bool applied = InvokeTryApplyBurningContactDamage(target);

            Assert.IsFalse(applied, "没有 Burning 状态的地表不应造成地表燃烧伤害。");
            Assert.AreEqual(previousHealth, target.GetCurrentHealth());
        }

        private void CreateGameManagerWithMinimalConfig()
        {
            GameObject gameManagerObject = new("EditModeGameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            DatabaseRegistry database = ScriptableObject.CreateInstance<DatabaseRegistry>();

            m_createdObjects.Add(config);
            m_createdObjects.Add(database);
            m_createdObjects.Add(gameManagerObject);

            SetInstanceField(config, "m_databaseRegistry", database);
            SetInstanceField(config, "m_canCriticalHit", false);
            SetInstanceField(config, "m_canMissHit", false);
            SetInstanceField(gameManager, "m_config", config);
            SetInstanceField(
                gameManager,
                "m_systems",
                Activator.CreateInstance(GetRequiredFieldType(
                    typeof(GameManager),
                    "m_systems")));
            SetStaticField(typeof(GameManager), "_instance", gameManager);
        }

        private void CreateTerrainMap()
        {
            m_gridObject = new GameObject("地表伤害测试 Grid", typeof(Grid));
            m_createdObjects.Add(m_gridObject);

            GameObject tilemapObject = new(
                "地表伤害测试规则",
                typeof(Tilemap),
                typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(m_gridObject.transform);
            m_createdObjects.Add(tilemapObject);

            m_tilemap = tilemapObject.GetComponent<Tilemap>();
            m_navigationMap = m_gridObject.AddComponent<TerrainNavigationMap>();
            SetInstanceField(m_navigationMap, "m_ruleTilemap", m_tilemap);
        }

        private void CreateDamageSystem()
        {
            GameObject systemObject = new("地表伤害测试系统");
            m_createdObjects.Add(systemObject);

            m_damageSystem = systemObject.AddComponent<TerrainSurfaceDamageSystem>();
            SetInstanceField(m_damageSystem, "m_navigationMap", m_navigationMap);
            SetInstanceField(m_damageSystem, "m_burningDamagePerTick", 1);
            SetInstanceField(m_damageSystem, "m_ignoreDefense", true);
            SetInstanceField(m_damageSystem, "m_silentDamage", true);
        }

        private void AuthorGrassCell(Vector3Int cell)
        {
            TerrainNavigationTile grassTile = ScriptableObject.CreateInstance<TerrainNavigationTile>();
            m_createdObjects.Add(grassTile);
            SetInstanceField(grassTile, "m_walkable", true);
            SetInstanceField(grassTile, "m_surfaceKind", ETerrainSurfaceKind.Grass);
            SetInstanceField(grassTile, "m_traversalCost", 1.0f);

            m_tilemap.SetTile(cell, grassTile);
            m_navigationMap.RefreshNavigationData();
        }

        private CharacterActor CreateCharacter(
            string name,
            Vector3 position,
            Stats baseStats)
        {
            GameObject characterObject = new(name);
            characterObject.transform.position = position;
            m_createdObjects.Add(characterObject);

            Rigidbody2D rigidbody2D = characterObject.AddComponent<Rigidbody2D>();
            rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            CharacterActor character = characterObject.AddComponent<CharacterActor>();
            AbilitySystemComponent abilitySystemComponent =
                characterObject.GetComponent<AbilitySystemComponent>();
            CharacterAbilitySet abilitySet =
                characterObject.GetComponent<CharacterAbilitySet>();

            CharacterSheet sheet = ScriptableObject.CreateInstance<CharacterSheet>();
            m_createdObjects.Add(sheet);
            SetInstanceField(sheet, "m_baseStats", baseStats.Clone());
            SetInstanceField(character, "m_sheet", sheet);
            SetInstanceField(character, "m_rigidbody", rigidbody2D);

            InvokeLifecycle(abilitySystemComponent, "Awake");
            SetInstanceField(abilitySet, "m_character", character);
            InvokeLifecycle(abilitySet, "Awake");
            InvokeLifecycle(character, "Awake");
            InvokeLifecycle(abilitySystemComponent, "OnEnable");
            InvokeLifecycle(abilitySet, "OnEnable");
            InvokeLifecycle(character, "OnEnable");

            return character;
        }

        private bool InvokeTryApplyBurningContactDamage(CharacterBase target)
        {
            MethodInfo method = typeof(TerrainSurfaceDamageSystem).GetMethod(
                "TryApplyBurningContactDamage",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(CharacterBase) },
                null);
            Assert.IsNotNull(
                method,
                "找不到 TerrainSurfaceDamageSystem.TryApplyBurningContactDamage(CharacterBase)。");
            return (bool)method.Invoke(m_damageSystem, new object[] { target });
        }

        private static Stats CreateStats(
            int health = 0,
            int mana = 0,
            int physicalAttack = 0,
            int magicalAttack = 0,
            int physicalDefense = 0,
            int magicalDefense = 0,
            int agility = 0,
            int luck = 0,
            int attackSpeed = 0)
        {
            Stats stats = new();
            stats[EStat.Health] = health;
            stats[EStat.Mana] = mana;
            stats[EStat.PhysicalAttack] = physicalAttack;
            stats[EStat.MagicalAttack] = magicalAttack;
            stats[EStat.PhysicalDefense] = physicalDefense;
            stats[EStat.MagicalDefense] = magicalDefense;
            stats[EStat.Agility] = agility;
            stats[EStat.Luck] = luck;
            stats[EStat.AttackSpeed] = attackSpeed;
            return stats;
        }

        private static void InvokeLifecycle(Component component, string methodName)
        {
            MethodInfo method = FindInstanceMethod(component.GetType(), methodName);
            Assert.IsNotNull(
                method,
                $"找不到生命周期方法 {component.GetType().Name}.{methodName}");
            method.Invoke(component, null);
        }

        private static void SetInstanceField(
            object target,
            string fieldName,
            object value)
        {
            Assert.IsNotNull(target, $"目标对象为空，无法写入字段 {fieldName}");
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

        private static Type GetRequiredFieldType(Type type, string fieldName)
        {
            FieldInfo field = FindInstanceField(type, fieldName);
            Assert.IsNotNull(field, $"找不到字段 {type.Name}.{fieldName}");
            return field.FieldType;
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
}
