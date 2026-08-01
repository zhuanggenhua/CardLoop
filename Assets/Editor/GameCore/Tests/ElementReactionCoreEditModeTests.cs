using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FantasyWord.GameCore.Tests
{
    public sealed class ElementReactionCoreEditModeTests
    {
        private readonly List<Object> m_createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < m_createdObjects.Count; i++)
            {
                Object.DestroyImmediate(m_createdObjects[i]);
            }

            m_createdObjects.Clear();
        }

        [Test]
        public void CollectMatches_SortsByPriorityThenStableId()
        {
            ElementReactionDefinition lowPriority = CreateReaction(5);
            ElementReactionDefinition firstStableId = CreateReaction(10);
            ElementReactionDefinition secondStableId = CreateReaction(10);
            ElementApplication application = CreateFireApplication();
            ElementReactionContext context = new(
                EElementReactionTrigger.OnElementApplied,
                application,
                ETerrainElementStateKind.None,
                ETerrainSurfaceKind.Grass,
                ETerrainSurfaceKind.Grass,
                ETerrainRuntimeSurfaceState.None);
            List<ElementReactionCandidate> candidates = new()
            {
                new("rule-b", secondStableId),
                new("rule-low", lowPriority),
                new("rule-a", firstStableId)
            };
            List<ElementReactionCandidate> matches = new();

            ElementReactionResolver.CollectMatches(candidates, context, matches);

            Assert.AreEqual(3, matches.Count);
            Assert.AreEqual("rule-a", matches[0].StableId);
            Assert.AreEqual("rule-b", matches[1].StableId);
            Assert.AreEqual("rule-low", matches[2].StableId);
        }

        [Test]
        public void RegisterReactionDefinition_RejectsDuplicateStableId()
        {
            GameObject systemObject = new("元素反应系统");
            m_createdObjects.Add(systemObject);
            ElementReactionSystem system =
                systemObject.AddComponent<ElementReactionSystem>();
            ElementReactionDefinition reaction = CreateReaction(10);
            MethodInfo registerMethod = typeof(ElementReactionSystem).GetMethod(
                "TryRegisterReactionDefinition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(registerMethod);

            bool firstRegistration = (bool)registerMethod.Invoke(
                system,
                new object[] { "duplicate-rule", reaction });
            LogAssert.Expect(
                LogType.Error,
                "元素反应稳定 ID 'duplicate-rule' 重复。");
            bool secondRegistration = (bool)registerMethod.Invoke(
                system,
                new object[] { "duplicate-rule", reaction });

            Assert.IsTrue(firstRegistration);
            Assert.IsFalse(secondRegistration);
        }

        [Test]
        public void ReactionDefinition_RequiresConfiguredSurfaceAndRuntimeState()
        {
            ElementReactionDefinition reaction = CreateReaction(0);
            SetPrivateField(reaction, "m_requireEffectiveSurface", true);
            SetPrivateField(reaction, "m_effectiveSurface", ETerrainSurfaceKind.Grass);
            SetPrivateField(
                reaction,
                "m_requiredStates",
                ETerrainRuntimeSurfaceState.Oiled);

            ElementApplication application = CreateFireApplication();
            ElementReactionContext matchingContext = new(
                EElementReactionTrigger.OnElementApplied,
                application,
                ETerrainElementStateKind.None,
                ETerrainSurfaceKind.Grass,
                ETerrainSurfaceKind.Grass,
                ETerrainRuntimeSurfaceState.Oiled);
            ElementReactionContext wrongSurfaceContext = new(
                EElementReactionTrigger.OnElementApplied,
                application,
                ETerrainElementStateKind.None,
                ETerrainSurfaceKind.Grass,
                ETerrainSurfaceKind.Dirt,
                ETerrainRuntimeSurfaceState.Oiled);
            ElementReactionContext missingStateContext = new(
                EElementReactionTrigger.OnElementApplied,
                application,
                ETerrainElementStateKind.None,
                ETerrainSurfaceKind.Grass,
                ETerrainSurfaceKind.Grass,
                ETerrainRuntimeSurfaceState.None);

            Assert.IsTrue(reaction.Matches(matchingContext));
            Assert.IsFalse(reaction.Matches(wrongSurfaceContext));
            Assert.IsFalse(reaction.Matches(missingStateContext));
        }

        [Test]
        public void RuntimeState_RefreshesDurationAndKeepsStrongerIntensity()
        {
            TerrainCellRuntimeState runtimeState = new();
            TerrainElementStateSource firstSource = new(null, 101);
            TerrainElementStateSource secondSource = new(null, 202);

            Assert.IsTrue(runtimeState.ApplyOrMergeState(
                ETerrainElementStateKind.Burning,
                0.8f,
                3.0f,
                firstSource,
                "fire-grass",
                ETerrainStateMergePolicy.RefreshDuration));
            Assert.IsTrue(runtimeState.ApplyOrMergeState(
                ETerrainElementStateKind.Burning,
                0.4f,
                5.0f,
                secondSource,
                "fire-grass",
                ETerrainStateMergePolicy.RefreshDuration));

            Assert.AreEqual(ETerrainRuntimeSurfaceState.Burning, runtimeState.RuntimeStateFlags);
            Assert.IsTrue(runtimeState.TryGetState(
                ETerrainElementStateKind.Burning,
                out TerrainElementStateInstance burning));
            Assert.AreEqual(0.8f, burning.Intensity);
            Assert.AreEqual(5.0f, burning.RemainingDuration);
            Assert.AreEqual(202, burning.SourceAbilityCode);
            Assert.AreEqual(1, runtimeState.ActiveStates.Count);
        }

        [Test]
        public void RuntimeState_TracksExpirationWithoutRemovingStateEarly()
        {
            TerrainCellRuntimeState runtimeState = new();
            TerrainElementStateSource source = new(null, 101);
            runtimeState.ApplyOrMergeState(
                ETerrainElementStateKind.Burning,
                0.5f,
                1.0f,
                source,
                "fire-grass",
                ETerrainStateMergePolicy.RefreshDuration);
            List<ETerrainElementStateKind> expiredStates = new();

            runtimeState.AdvanceDurations(1.0f, expiredStates);

            Assert.AreEqual(1, expiredStates.Count);
            Assert.AreEqual(ETerrainElementStateKind.Burning, expiredStates[0]);
            Assert.IsTrue(runtimeState.TryGetState(
                ETerrainElementStateKind.Burning,
                out TerrainElementStateInstance burning));
            Assert.AreEqual(0.0f, burning.RemainingDuration);
            Assert.AreEqual(
                ETerrainRuntimeSurfaceState.Burning,
                runtimeState.RuntimeStateFlags);
        }

        private ElementReactionDefinition CreateReaction(int priority)
        {
            ElementReactionDefinition reaction =
                ScriptableObject.CreateInstance<ElementReactionDefinition>();
            m_createdObjects.Add(reaction);
            SetPrivateField(reaction, "m_trigger", EElementReactionTrigger.OnElementApplied);
            SetPrivateField(reaction, "m_elementKind", EWorldElementKind.Fire);
            SetPrivateField(reaction, "m_priority", priority);
            SetPrivateField(
                reaction,
                "m_operations",
                new[] { new ElementReactionOperation() });
            return reaction;
        }

        private static ElementApplication CreateFireApplication()
        {
            return new ElementApplication(
                EWorldElementKind.Fire,
                1.0f,
                0.25f,
                ElementArea.Cone(3.0f, 30.0f),
                Vector2.zero,
                Vector2.right,
                sourceAbilityCode: 101);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段：{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段：{target.GetType().Name}.{fieldName}");
            return (T)field.GetValue(target);
        }
    }
}
