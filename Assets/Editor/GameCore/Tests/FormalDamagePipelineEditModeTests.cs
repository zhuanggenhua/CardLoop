using System;
using System.Reflection;
using GAS.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GameCore.Tests
{
    public sealed class FormalDamagePipelineEditModeTests
    {
        private const int ChargedDamageGameplayEffectId = 2004;

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
        public void AttributeQueries_ReadFormalGasValuesByAttributeCode()
        {
            CharacterActor character = CreateCharacter(
                "attribute-query-character",
                CreateAttributeOverrides(health: 50, mana: 12, attack: 23));

            Assert.AreEqual(23.0f, character.GetAttributeBaseValue(CharacterAttributes.Attack));
            Assert.AreEqual(23.0f, character.GetAttributeCurrentValue(CharacterAttributes.Attack));
            Assert.AreEqual(50.0f, character.GetAttributeBaseValue(CharacterAttributes.MaxHealth));
            Assert.AreEqual(50.0f, character.GetAttributeCurrentValue(CharacterAttributes.Health));

            character.ConsumeMana(5);

            Assert.AreEqual(12.0f, character.GetAttributeBaseValue(CharacterAttributes.MaxMana));
            Assert.AreEqual(7.0f, character.GetAttributeCurrentValue(CharacterAttributes.Mana));
            Assert.Throws<InvalidOperationException>(() => character.GetAttributeBaseValue(9999));
        }

        [Test]
        public void AttributeChangeEvents_PublishCurrentValueByGasAttributeCode()
        {
            CharacterActor caster = CreateCharacter("caster", CreateAttributeOverrides(health: 30, mana: 12));
            CharacterAttributeValueChange? observedChange = null;
            caster.AddAttributeCurrentValueChangedListener(change => observedChange = change);

            caster.ConsumeMana(5);

            Assert.IsTrue(observedChange.HasValue);
            Assert.AreEqual(CharacterAttributes.Mana, observedChange.Value.AttributeCode);
            Assert.AreEqual(12.0f, observedChange.Value.PreviousValue);
            Assert.AreEqual(7.0f, observedChange.Value.CurrentValue);
        }

        [Test]
        public void ConfiguredDamageEffect_UpdatesFormalAscCurrentHealth()
        {
            CharacterActor attacker = CreateCharacter("attacker", CreateAttributeOverrides(health: 30, attack: 10));
            CharacterActor defender = CreateCharacter("defender", CreateAttributeOverrides(health: 50, defense: 3));

            int previousHealth = defender.GetCurrentHealth();
            int previousMaxHealth = defender.GetMaxHealth();
            Assert.AreEqual(50, previousHealth);
            Assert.AreEqual(50, previousMaxHealth);

            ApplyConfiguredGameplayEffect(attacker, defender, ChargedDamageGameplayEffectId);
            GasEditModeTestHelper.AdvanceWorldUntil(() => defender.GetCurrentHealth() < previousHealth);
            int damagedHealth = defender.GetCurrentHealth();

            Assert.Less(damagedHealth, previousHealth);
            Assert.AreEqual(previousMaxHealth, defender.GetMaxHealth());

            Assert.IsTrue(defender.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderAsc));
            int currentFormalHealth = Mathf.RoundToInt(defenderAsc.GetAttrCurrentValue(
                CharacterAttributes.SetCode,
                CharacterAttributes.Health));
            int baseFormalHealth = Mathf.RoundToInt(defenderAsc.GetAttrBaseValue(
                CharacterAttributes.SetCode,
                CharacterAttributes.MaxHealth));
            Assert.AreEqual(damagedHealth, currentFormalHealth);
            Assert.AreEqual(previousMaxHealth, baseFormalHealth);
        }

        [Test]
        public void ConfiguredDamageEffect_RemainsAppliedAfterFormalAttributeRecalculation()
        {
            CharacterActor attacker = CreateCharacter("attacker", CreateAttributeOverrides(health: 30, attack: 10));
            CharacterActor defender = CreateCharacter("defender", CreateAttributeOverrides(health: 50, defense: 3));

            ApplyConfiguredGameplayEffect(attacker, defender, ChargedDamageGameplayEffectId);
            GasEditModeTestHelper.AdvanceWorldUntil(() => defender.GetCurrentHealth() < 50);
            int damagedHealth = defender.GetCurrentHealth();
            Assert.IsTrue(defender.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderAsc));

            AttributeHelper.RecalculateCurrentValue(
                defenderAsc.Cell.Entity,
                CharacterAttributes.SetCode,
                CharacterAttributes.Health);

            Assert.AreEqual(damagedHealth, defender.GetCurrentHealth());
            Assert.AreEqual(50, defender.GetMaxHealth());
        }

        [Test]
        public void CharacterAttributeSnapshot_RestoresGasBaseValuesAndCurrentResources()
        {
            CharacterActor character = CreateCharacter(
                "snapshot-character",
                CreateAttributeOverrides(health: 50, mana: 12, attack: 23));
            character.ConsumeMana(5);
            CharacterAttributeSnapshot snapshot = character.CaptureAttributeSnapshot();

            character.ConsumeMana(3);
            character.SetAttributeBaseValueAndRecalculate(CharacterAttributes.Attack, 5.0f);
            Assert.AreEqual(4, character.GetCurrentMana());
            Assert.AreEqual(5.0f, character.GetAttributeBaseValue(CharacterAttributes.Attack));

            character.RestoreAttributeSnapshot(snapshot);

            Assert.AreEqual(7, character.GetCurrentMana());
            Assert.AreEqual(7.0f, character.GetAttributeBaseValue(CharacterAttributes.Mana));
            Assert.AreEqual(23.0f, character.GetAttributeBaseValue(CharacterAttributes.Attack));
            Assert.AreEqual(23.0f, character.GetAttributeCurrentValue(CharacterAttributes.Attack));
        }

        [Test]
        public void ConsumeMana_UpdatesFormalAscCurrentManaWithoutChangingMaxMana()
        {
            CharacterActor caster = CreateCharacter("caster", CreateAttributeOverrides(health: 30, mana: 12));

            int previousMana = caster.GetCurrentMana();
            int previousMaxMana = caster.GetMaxMana();
            Assert.AreEqual(12, previousMana);
            Assert.AreEqual(12, previousMaxMana);

            caster.ConsumeMana(5);

            Assert.AreEqual(7, caster.GetCurrentMana());
            Assert.AreEqual(previousMaxMana, caster.GetMaxMana());

            Assert.IsTrue(caster.TryGetFormalAbilitySystem(out AbilitySystemComponent casterAsc));
            int currentFormalMana = Mathf.RoundToInt(casterAsc.GetAttrCurrentValue(
                CharacterAttributes.SetCode,
                CharacterAttributes.Mana));
            int baseFormalMana = Mathf.RoundToInt(casterAsc.GetAttrBaseValue(
                CharacterAttributes.SetCode,
                CharacterAttributes.MaxMana));
            Assert.AreEqual(7, currentFormalMana);
            Assert.AreEqual(previousMaxMana, baseFormalMana);
        }

        [Test]
        public void CharacterStartup_UsesGasDefaultsForUnprojectedAttributes()
        {
            CharacterActor character = CreateCharacter(
                "character",
                CreateAttributeOverrides(health: 50));

            Assert.IsTrue(character.TryGetFormalAbilitySystem(out AbilitySystemComponent characterAsc));
            Assert.AreEqual(100.0f, characterAsc.GetAttrBaseValue(
                CharacterAttributes.SetCode,
                CharacterAttributes.Stamina));
            Assert.AreEqual(100.0f, characterAsc.GetAttrBaseValue(
                CharacterAttributes.SetCode,
                CharacterAttributes.MaxStamina));
            Assert.AreEqual(100.0f, characterAsc.GetAttrBaseValue(
                CharacterAttributes.SetCode,
                CharacterAttributes.Accuracy));
            Assert.AreEqual(200.0f, characterAsc.GetAttrBaseValue(
                CharacterAttributes.SetCode,
                CharacterAttributes.CriticalMultiplier));
        }

        [Test]
        public void DamageSolver_UsesTemplateDefenseAndCriticalThresholds()
        {
            Assert.AreEqual(11, DamageSolver.CalculateDamageIn(14, 3.0f));
            Assert.AreEqual(1, DamageSolver.CalculateDamageIn(2, 10.0f));
            Assert.IsTrue(DamageSolver.EvaluateCritical(5.0f, 5.0f));
            Assert.IsFalse(DamageSolver.EvaluateCritical(5.0f, 5.01f));
        }

        [Test]
        public void DamageSolver_AppliesConfiguredGasTagMatchupAfterDefenseBeforeCritical()
        {
            SetInstanceField(GameManager.Config, "m_canCriticalHit", true);
            AbilitySystemCell attacker = CreateAbilitySystemCell(1001);
            AbilitySystemCell defender = CreateAbilitySystemCell(1004);
            try
            {
                SetBaseAndRecalculate(attacker, CharacterAttributes.Attack, 20.0f);
                SetBaseAndRecalculate(attacker, CharacterAttributes.CriticalChance, 100.0f);
                SetBaseAndRecalculate(attacker, CharacterAttributes.CriticalMultiplier, 200.0f);
                SetBaseAndRecalculate(defender, CharacterAttributes.Defense, 4.0f);
                DamageDescriptor descriptor = new(
                    EDamageType.Physical,
                    flatDamages: 4,
                    scalingFactor: 1.0f,
                    ignoreDefense: false,
                    matchupRules: new[]
                    {
                        new DamageMatchupRule(
                            XTag.Combat_Melee,
                            XTag.Combat_Ranged,
                            1.5f,
                            DamageMatchupResult.Advantage)
                    });

                DamageOutputDescriptor output = DamageSolver.SolveDamageOutput(
                    attacker,
                    descriptor,
                    new DamageResolutionRolls(criticalRollPercent: 0.0f, hitRollPercent: 0.0f));
                DamageInputDescriptor input = DamageSolver.SolveDamageInput(defender, output);

                Assert.AreEqual(60, input.damage);
                Assert.AreEqual(DamageMatchupResult.Advantage, input.matchupResult);
                Assert.IsTrue(input.IsCriticalHit);
                Assert.IsFalse(input.IsMissed);
            }
            finally
            {
                attacker.Dispose();
                defender.Dispose();
            }
        }

        [Test]
        public void DamageSolver_UsesTemplateAccuracyMinusDodgeHitChance()
        {
            Assert.IsFalse(DamageSolver.EvaluateMiss(95.0f, 5.0f, 90.0f));
            Assert.IsTrue(DamageSolver.EvaluateMiss(95.0f, 5.0f, 90.01f));
            Assert.IsFalse(DamageSolver.EvaluateMiss(0.0f, 100.0f, 5.0f));
            Assert.IsTrue(DamageSolver.EvaluateMiss(0.0f, 100.0f, 5.01f));
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

        private CharacterActor CreateCharacter(
            string name,
            CharacterAttributeOverride[] attributeOverrides)
        {
            GameObject characterObject = new(name);
            m_createdObjects.Add(characterObject);

            Rigidbody2D rigidbody2D = characterObject.AddComponent<Rigidbody2D>();
            CharacterActor character = characterObject.AddComponent<CharacterActor>();
            AbilitySystemComponent abilitySystemComponent = characterObject.GetComponent<AbilitySystemComponent>();
            CharacterAbilitySet abilitySet = characterObject.GetComponent<CharacterAbilitySet>();

            CharacterSheet sheet = ScriptableObject.CreateInstance<CharacterSheet>();
            m_createdObjects.Add(sheet);
            SetInstanceField(sheet, "m_attributeOverrides", attributeOverrides);
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

        private static void ApplyConfiguredGameplayEffect(
            CharacterBase source,
            CharacterBase target,
            int gameplayEffectId)
        {
            GameplayEffectConfig effectConfig = GameplayEffectHelper.GetConfigByID(gameplayEffectId);
            Assert.IsNotNull(effectConfig, $"找不到 EX-GAS GameplayEffect {gameplayEffectId}。");
            Assert.IsTrue(source.TryGetFormalAbilitySystem(out AbilitySystemComponent sourceAsc));
            Assert.IsNotNull(sourceAsc.Cell);
            Assert.IsTrue(target.TryGetFormalAbilitySystem(out AbilitySystemComponent targetAsc));
            Assert.IsNotNull(targetAsc.Cell);

            Unity.Entities.Entity gameplayEffect = effectConfig.CreateGameplayEffectEntity();
            GameplayEffectHelper.ApplyGameplayEffectTo(
                gameplayEffect,
                targetAsc.Cell.Entity,
                sourceAsc.Cell.Entity);
        }

        private static AbilitySystemCell CreateAbilitySystemCell(int presetId)
        {
            AbilitySystemCellConfig config = XLuban.GetAscConfig(presetId);
            AbilitySystemCell abilitySystem = new();
            abilitySystem.Init(
                config.BaseTags ?? Array.Empty<int>(),
                config.AttrSets ?? Array.Empty<AttrSetConfig>(),
                config.BaseAbilities ?? Array.Empty<AbilityConfig>(),
                config.Level);
            return abilitySystem;
        }

        private static void SetBaseAndRecalculate(
            AbilitySystemCell abilitySystem,
            int attributeCode,
            float value)
        {
            abilitySystem.SetAttrBaseValue(CharacterAttributes.SetCode, attributeCode, value);
            AttributeHelper.RecalculateCurrentValue(
                abilitySystem.Entity,
                CharacterAttributes.SetCode,
                attributeCode);
        }

        private static CharacterAttributeOverride[] CreateAttributeOverrides(
            float health = 0.0f,
            float mana = 0.0f,
            float attack = 0.0f,
            float defense = 0.0f)
        {
            System.Collections.Generic.List<CharacterAttributeOverride> overrides = new();
            if (health > 0.0f)
            {
                overrides.Add(new CharacterAttributeOverride(CharacterAttributes.MaxHealth, health));
            }

            if (mana > 0.0f)
            {
                overrides.Add(new CharacterAttributeOverride(CharacterAttributes.MaxMana, mana));
            }

            if (attack > 0.0f)
            {
                overrides.Add(new CharacterAttributeOverride(CharacterAttributes.Attack, attack));
            }

            if (defense > 0.0f)
            {
                overrides.Add(new CharacterAttributeOverride(CharacterAttributes.Defense, defense));
            }

            return overrides.ToArray();
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
