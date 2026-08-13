using System;
using System.Reflection;
using GAS.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GameCore.Tests
{
    public sealed class CharacterAttributeAuthoringEditModeTests
    {
        private CharacterSheet m_sheet;

        [SetUp]
        public void SetUp()
        {
            GasEditModeTestHelper.ResetWorld();
            m_sheet = ScriptableObject.CreateInstance<CharacterSheet>();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_sheet != null)
            {
                UnityEngine.Object.DestroyImmediate(m_sheet);
            }

            GasEditModeTestHelper.ShutdownWorld();
        }

        [Test]
        public void CharacterOverrides_KeepGasDefaultsAndClampRules()
        {
            CharacterAttributeOverride[] overrides =
            {
                new(CharacterAttributes.MaxHealth, 75.0f),
                new(CharacterAttributes.Attack, 23.0f)
            };
            SetInstanceField(m_sheet, "m_attributeOverrides", overrides);

            AttrSetConfig config = m_sheet.CreateAttributeSetConfig();

            AttributeBaseSetting health = FindSetting(config, CharacterAttributes.Health);
            AttributeBaseSetting maxHealth = FindSetting(config, CharacterAttributes.MaxHealth);
            AttributeBaseSetting attack = FindSetting(config, CharacterAttributes.Attack);
            AttributeBaseSetting dodge = FindSetting(config, CharacterAttributes.Dodge);

            Assert.AreEqual(75.0f, health.InitValue, "只覆盖生命上限时，角色应以满生命开始。");
            Assert.AreEqual(75.0f, maxHealth.InitValue);
            Assert.AreEqual(23.0f, attack.InitValue);
            Assert.IsTrue(attack.IsClampMin);
            Assert.AreEqual(0.0f, attack.Min);
            Assert.IsFalse(attack.IsClampMax);
            Assert.AreEqual(0.0f, dodge.InitValue, "未覆盖属性必须沿用 EX-GAS 表默认值。");
            Assert.IsTrue(dodge.IsClampMin);
            Assert.IsTrue(dodge.IsClampMax);
            Assert.AreEqual(0.0f, dodge.Min);
            Assert.AreEqual(100.0f, dodge.Max);
        }

        [Test]
        public void CharacterOverrides_RejectDuplicateAttributeCodes()
        {
            SetInstanceField(
                m_sheet,
                "m_attributeOverrides",
                new[]
                {
                    new CharacterAttributeOverride(CharacterAttributes.Attack, 12.0f),
                    new CharacterAttributeOverride(CharacterAttributes.Attack, 18.0f)
                });

            Assert.Throws<InvalidOperationException>(() => m_sheet.CreateAttributeSetConfig());
        }

        [Test]
        public void CharacterOverrides_RejectUnknownAttributeCodes()
        {
            SetInstanceField(
                m_sheet,
                "m_attributeOverrides",
                new[] { new CharacterAttributeOverride(9999, 12.0f) });

            Assert.Throws<InvalidOperationException>(() => m_sheet.CreateAttributeSetConfig());
        }

        [Test]
        public void CharacterOverrides_RejectValuesOutsideGasClampRules()
        {
            SetInstanceField(
                m_sheet,
                "m_attributeOverrides",
                new[] { new CharacterAttributeOverride(CharacterAttributes.Dodge, 101.0f) });

            Assert.Throws<InvalidOperationException>(() => m_sheet.CreateAttributeSetConfig());
        }

        [Test]
        public void CharacterAttributeReadBeforeAscInitialization_Throws()
        {
            GameObject characterObject = new("uninitialized-character");
            characterObject.SetActive(false);
            try
            {
                CharacterActor character = characterObject.AddComponent<CharacterActor>();

                Assert.Throws<InvalidOperationException>(() => character.GetMaxHealth());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(characterObject);
            }
        }

        private static AttributeBaseSetting FindSetting(AttrSetConfig config, int attributeCode)
        {
            foreach (AttributeBaseSetting setting in config.Settings)
            {
                if (setting.Code == attributeCode)
                {
                    return setting;
                }
            }

            throw new AssertionException($"属性集 {config.Code} 中不存在属性 {attributeCode}。");
        }

        private static void SetInstanceField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
