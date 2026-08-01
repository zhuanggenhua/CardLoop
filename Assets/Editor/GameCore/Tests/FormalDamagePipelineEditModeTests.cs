using System;
using System.Reflection;
using GAS.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FantasyWord.GameCore.Tests
{
    public sealed class FormalDamagePipelineEditModeTests
    {
        private readonly System.Collections.Generic.List<UnityEngine.Object> m_createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            GasEditModeTestHelper.ResetWorld();
            CreateGameManagerWithMinimalConfig();
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
        public void Damage_UpdatesFormalAscCurrentHealth()
        {
            CharacterActor attacker = CreateCharacter("attacker", CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", CreateStats(health: 50, physicalDefense: 3));

            int previousHealth = defender.GetCurrentHealth();
            int previousMaxHealth = defender.GetMaxHealth();
            Assert.AreEqual(50, previousHealth);
            Assert.AreEqual(50, previousMaxHealth);

            FormalDamageEffectPayload payload = new(
                new DamageDescriptor
                {
                    damageType = EDamageType.Physical,
                    flatDamages = 7,
                    scalingFactor = 0.0f,
                    criticalBehavior = EResolutionBehavior.Never,
                    missBehavior = EResolutionBehavior.Never,
                    ignoreDefense = true,
                    silent = true
                },
                EEffectVisualFlags.None,
                default,
                EEffectImpactDataType.Velocity,
                Vector2.zero);
            bool applied = FormalGameplayEffectDamageHelper.TryApplyDamage(attacker, defender, payload);

            Assert.IsTrue(applied);
            GasEditModeTestHelper.AdvanceWorldUntil(() => defender.GetCurrentHealth() == previousHealth - 7);
            Assert.AreEqual(previousHealth - 7, defender.GetCurrentHealth());
            Assert.AreEqual(previousMaxHealth, defender.GetMaxHealth());

            Assert.IsTrue(defender.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderAsc));
            int currentFormalHealth = Mathf.RoundToInt(defenderAsc.GetAttrCurrentValue(
                FormalGameplayAttributeSet.SetCode,
                FormalGameplayAttributeSet.Health));
            int baseFormalHealth = Mathf.RoundToInt(defenderAsc.GetAttrBaseValue(
                FormalGameplayAttributeSet.SetCode,
                FormalGameplayAttributeSet.Health));
            Assert.AreEqual(previousHealth - 7, currentFormalHealth);
            Assert.AreEqual(previousMaxHealth, baseFormalHealth);
        }

        [Test]
        public void ConsumeMana_UpdatesFormalAscCurrentManaWithoutChangingMaxMana()
        {
            CharacterActor caster = CreateCharacter("caster", CreateStats(health: 30, mana: 12));

            int previousMana = caster.GetCurrentMana();
            int previousMaxMana = caster.GetMaxMana();
            Assert.AreEqual(12, previousMana);
            Assert.AreEqual(12, previousMaxMana);

            caster.ConsumeMana(5);

            Assert.AreEqual(7, caster.GetCurrentMana());
            Assert.AreEqual(previousMaxMana, caster.GetMaxMana());

            Assert.IsTrue(caster.TryGetFormalAbilitySystem(out AbilitySystemComponent casterAsc));
            int currentFormalMana = Mathf.RoundToInt(casterAsc.GetAttrCurrentValue(
                FormalGameplayAttributeSet.SetCode,
                FormalGameplayAttributeSet.Mana));
            int baseFormalMana = Mathf.RoundToInt(casterAsc.GetAttrBaseValue(
                FormalGameplayAttributeSet.SetCode,
                FormalGameplayAttributeSet.Mana));
            Assert.AreEqual(7, currentFormalMana);
            Assert.AreEqual(previousMaxMana, baseFormalMana);
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
                Activator.CreateInstance(GetRequiredFieldType(typeof(GameManager), "m_systems")));
            SetStaticField(typeof(GameManager), "_instance", gameManager);
        }

        private CharacterActor CreateCharacter(string name, Stats baseStats)
        {
            GameObject characterObject = new(name);
            m_createdObjects.Add(characterObject);

            Rigidbody2D rigidbody2D = characterObject.AddComponent<Rigidbody2D>();
            CharacterActor character = characterObject.AddComponent<CharacterActor>();
            AbilitySystemComponent abilitySystemComponent = characterObject.GetComponent<AbilitySystemComponent>();
            CharacterAbilitySet abilitySet = characterObject.GetComponent<CharacterAbilitySet>();

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
            Assert.IsNotNull(method, $"找不到生命周期方法 {component.GetType().Name}.{methodName}");
            method.Invoke(component, null);
        }

        private static void SetInstanceField(object target, string fieldName, object value)
        {
            Assert.IsNotNull(target, $"目标对象为空，无法写入字段 {fieldName}");
            FieldInfo field = FindInstanceField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
        private static void SetStaticField(Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到静态字段 {type.Name}.{fieldName}");
            field.SetValue(null, value);
        }

        private static void InvokeStaticMethod(Type type, string methodName)
        {
            Assert.IsNotNull(type, $"找不到类型 {methodName} 的宿主。");
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"找不到静态方法 {type.Name}.{methodName}");
            method.Invoke(null, null);
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
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
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
                MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
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
