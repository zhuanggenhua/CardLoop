using System;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace FantasyWord.GameCore.Tests
{
    internal static class GasEditModeTestHelper
    {
        private const float EditModeTickDeltaTime = 1.0f / 30.0f;

        public static void ResetWorld()
        {
            ShutdownWorld();

            Type bootstrapType = typeof(GameManager).Assembly.GetType(
                "FantasyWord.GameCore.FormalAbilityRuntimeBootstrap",
                throwOnError: false);
            Assert.IsNotNull(bootstrapType, "找不到 FantasyWord.GameCore.FormalAbilityRuntimeBootstrap。");

            MethodInfo ensureInitializedMethod = bootstrapType.GetMethod(
                "EnsureInitialized",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(ensureInitializedMethod, "找不到 FormalAbilityRuntimeBootstrap.EnsureInitialized。");
            ensureInitializedMethod.Invoke(null, null);
        }

        public static void ShutdownWorld()
        {
            Type gasManagerType = ResolveType("GAS.Runtime.GASManager");
            Assert.IsNotNull(gasManagerType, "找不到 GAS.Runtime.GASManager。");

            MethodInfo stopMethod = gasManagerType.GetMethod(
                "Stop",
                BindingFlags.Static | BindingFlags.Public);
            stopMethod?.Invoke(null, null);

            MethodInfo clearAscBindingMethod = gasManagerType.GetMethod(
                "ClearAscBinding",
                BindingFlags.Static | BindingFlags.Public);
            clearAscBindingMethod?.Invoke(null, null);

            PropertyInfo exWorldProperty = gasManagerType.GetProperty(
                "ExWorld",
                BindingFlags.Static | BindingFlags.Public);
            object world = exWorldProperty?.GetValue(null);
            if (world != null && IsWorldCreated(world))
            {
                MethodInfo disposeMethod = world.GetType().GetMethod(
                    "Dispose",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);
                Assert.IsNotNull(disposeMethod, "找不到 World.Dispose()。");
                disposeMethod.Invoke(world, null);
            }

            ResetGasManagerState(gasManagerType);
            ResetFormalAbilityRuntimeBootstrapState();
        }

        public static void AdvanceWorld(int ticks = 1)
        {
            Assert.GreaterOrEqual(ticks, 0, "GAS world 推进次数不能为负数。");

            for (int i = 0; i < ticks; i++)
            {
                AdvanceActiveCharacterAbilities(EditModeTickDeltaTime);
                object world = GetExGasWorld();
                Assert.IsNotNull(world, "EX-GAS world 尚未初始化。");
                Assert.IsTrue(IsWorldCreated(world), "EX-GAS world 未创建。");
                AdvanceActiveGasTimelinesForEditMode();
                InvokeLogicGroupUpdate(world);
                InvokeDisplayGroupUpdate(world);
            }
        }

        public static void AdvanceWorldUntil(Func<bool> predicate, int maxTicks = 4, Func<string> diagnostic = null)
        {
            Assert.IsNotNull(predicate, "GAS world 轮询条件不能为空。");
            if (predicate())
            {
                return;
            }

            for (int i = 0; i < maxTicks; i++)
            {
                AdvanceWorld();
                if (predicate())
                {
                    return;
                }
            }

            string diagnosticText = diagnostic != null ? $" 诊断：{diagnostic()}" : string.Empty;
            Assert.Fail($"推进 EX-GAS world {maxTicks} 次后，目标条件仍未满足。{diagnosticText}");
        }

        private static object GetExGasWorld()
        {
            Type gasManagerType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("GAS.Runtime.GASManager", false))
                .FirstOrDefault(type => type != null);
            Assert.IsNotNull(gasManagerType, "找不到 GAS.Runtime.GASManager。");

            PropertyInfo exWorldProperty = gasManagerType.GetProperty(
                "ExWorld",
                BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(exWorldProperty, "找不到 GASManager.ExWorld。");
            return exWorldProperty.GetValue(null);
        }

        private static bool IsWorldCreated(object world)
        {
            if (world == null)
            {
                return false;
            }

            PropertyInfo isCreatedProperty = world.GetType().GetProperty(
                "IsCreated",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(isCreatedProperty, "找不到 World.IsCreated。");
            return isCreatedProperty.GetValue(world) is bool isCreated && isCreated;
        }

        private static void ResetGasManagerState(Type gasManagerType)
        {
            SetStaticBackingField(gasManagerType, "ExWorld", null);
            SetStaticBackingField(gasManagerType, "EntityManager", default(Unity.Entities.EntityManager));
            SetStaticBackingField(gasManagerType, "IsRunning", false);
            SetStaticBackingField(gasManagerType, "IsInitialized", false);
            SetStaticBackingField(gasManagerType, "EntityGlobalTimer", Unity.Entities.Entity.Null);
        }

        private static void ResetFormalAbilityRuntimeBootstrapState()
        {
            Type bootstrapType = typeof(GameManager).Assembly.GetType(
                "FantasyWord.GameCore.FormalAbilityRuntimeBootstrap",
                throwOnError: false);
            Assert.IsNotNull(bootstrapType, "找不到 FantasyWord.GameCore.FormalAbilityRuntimeBootstrap。");

            FieldInfo initializedField = bootstrapType.GetField(
                "s_initialized",
                BindingFlags.Static | BindingFlags.NonPublic);
            initializedField?.SetValue(null, false);

            FieldInfo extensionsRegisteredField = bootstrapType.GetField(
                "s_gameCoreGasExtensionsRegistered",
                BindingFlags.Static | BindingFlags.NonPublic);
            extensionsRegisteredField?.SetValue(null, false);
        }

        private static void SetStaticBackingField(Type type, string propertyName, object value)
        {
            FieldInfo field = type.GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到 {type.FullName}.{propertyName} 的 backing field。");
            field.SetValue(null, value);
        }

        private static void InvokeLogicGroupUpdate(object world)
        {
            Type logicGroupType = ResolveType("GAS.Runtime.SGLogic");
            Assert.IsNotNull(logicGroupType, "找不到 GAS.Runtime.SGLogic。");

            MethodInfo getExistingSystemManagedMethod = world.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method =>
                    method.Name == "GetExistingSystemManaged" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 0);
            Assert.IsNotNull(getExistingSystemManagedMethod, "找不到 World.GetExistingSystemManaged<T>()。");

            object logicGroup = getExistingSystemManagedMethod
                .MakeGenericMethod(logicGroupType)
                .Invoke(world, null);
            Assert.IsNotNull(logicGroup, "EX-GAS world 缺少 SGLogic。");

            MethodInfo updateMethod = logicGroup.GetType().GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.IsNotNull(updateMethod, "找不到 SGLogic.Update()。");
            updateMethod.Invoke(logicGroup, null);
        }

        private static void InvokeDisplayGroupUpdate(object world)
        {
            Type displayGroupType = ResolveType("GAS.Runtime.SysGrpDisplay");
            Assert.IsNotNull(displayGroupType, "找不到 GAS.Runtime.SysGrpDisplay。");

            MethodInfo getExistingSystemManagedMethod = world.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method =>
                    method.Name == "GetExistingSystemManaged" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 0);
            Assert.IsNotNull(getExistingSystemManagedMethod, "找不到 World.GetExistingSystemManaged<T>()。");

            object displayGroup = getExistingSystemManagedMethod
                .MakeGenericMethod(displayGroupType)
                .Invoke(world, null);
            Assert.IsNotNull(displayGroup, "EX-GAS world 缺少 SysGrpDisplay。");

            MethodInfo updateMethod = displayGroup.GetType().GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.IsNotNull(updateMethod, "找不到 SysGrpDisplay.Update()。");
            updateMethod.Invoke(displayGroup, null);
        }

        private static void AdvanceActiveCharacterAbilities(float deltaTime)
        {
            CharacterBase[] characters = UnityEngine.Object.FindObjectsByType<CharacterBase>(UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                CharacterBase character = characters[i];
                if (character == null || !character.isActiveAndEnabled)
                {
                    continue;
                }

                MethodInfo advanceRuntimeMethod = ResolveCharacterAdvanceRuntimeMethod(character.GetType());
                if (advanceRuntimeMethod != null)
                {
                    advanceRuntimeMethod.Invoke(character, new object[] { deltaTime });
                    continue;
                }

                MethodInfo updateMethod = ResolveCharacterUpdateMethod(character.GetType());
                Assert.IsNotNull(updateMethod, $"找不到角色运行时更新入口 {character.GetType().Name}.Update。");
                updateMethod.Invoke(character, null);
            }
        }

        private static void AdvanceActiveGasTimelinesForEditMode()
        {
            if (Application.isPlaying || !GASManager.IsInitialized)
            {
                return;
            }

            global::Unity.Entities.EntityManager entityManager = GASManager.EntityManager;
            using global::Unity.Entities.EntityQuery query = entityManager.CreateEntityQuery(
                global::Unity.Entities.ComponentType.ReadOnly<CAbilityActive>(),
                global::Unity.Entities.ComponentType.ReadOnly<MCAbilityLogic>());
            using NativeArray<global::Unity.Entities.Entity> abilityEntities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < abilityEntities.Length; i++)
            {
                MCAbilityLogic abilityLogic = entityManager.GetComponentData<MCAbilityLogic>(abilityEntities[i]);
                if (abilityLogic?.Logic == null ||
                    abilityLogic.Logic.GetType().FullName != "GAS.Runtime.ALTimeline")
                {
                    continue;
                }

                AdvanceTimelinePlayerOneFrame(abilityLogic.Logic);
            }
        }

        private static void AdvanceTimelinePlayerOneFrame(AbilityLogicBase abilityLogic)
        {
            FieldInfo playerField = abilityLogic.GetType().GetField(
                "_player",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object player = playerField?.GetValue(abilityLogic);
            Assert.IsNotNull(player, "ALTimeline 缺少 _player，无法在 EditMode 手动推进时间轴。");

            Type playerType = player.GetType();
            PropertyInfo isPlayingProperty = playerType.GetProperty(
                "IsPlaying",
                BindingFlags.Instance | BindingFlags.Public);
            if (isPlayingProperty?.GetValue(player) is not bool isPlaying || !isPlaying)
            {
                return;
            }

            FieldInfo currentFrameField = playerType.GetField(
                "_currentFrame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo lifeTimeProperty = playerType.GetProperty(
                "LifeTime",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo tickFrameMethod = playerType.GetMethod(
                "TickFrame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(currentFrameField, "ALTimelinePlayer 缺少 _currentFrame。");
            Assert.IsNotNull(lifeTimeProperty, "ALTimelinePlayer 缺少 LifeTime。");
            Assert.IsNotNull(tickFrameMethod, "ALTimelinePlayer 缺少 TickFrame。");

            int currentFrame = currentFrameField.GetValue(player) is int frame ? frame : -1;
            int lifeTime = lifeTimeProperty.GetValue(player) is int value ? value : 0;
            if (currentFrame >= lifeTime)
            {
                return;
            }

            int nextFrame = currentFrame + 1;
            currentFrameField.SetValue(player, nextFrame);
            tickFrameMethod.Invoke(player, new object[] { nextFrame });

            if (nextFrame < lifeTime)
            {
                return;
            }

            MethodInfo onPlayEndMethod = playerType.GetMethod(
                "OnPlayEnd",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onPlayEndMethod, "ALTimelinePlayer 缺少 OnPlayEnd。");
            currentFrameField.SetValue(player, nextFrame + 1);
            onPlayEndMethod.Invoke(player, null);
        }

        private static MethodInfo ResolveCharacterAdvanceRuntimeMethod(Type characterType)
        {
            while (characterType != null)
            {
                MethodInfo method = characterType.GetMethod(
                    "AdvanceCharacterRuntime",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new[] { typeof(float) },
                    null);
                if (method != null)
                {
                    return method;
                }

                characterType = characterType.BaseType;
            }

            return null;
        }

        private static MethodInfo ResolveCharacterUpdateMethod(Type characterType)
        {
            while (characterType != null)
            {
                MethodInfo method = characterType.GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);
                if (method != null)
                {
                    return method;
                }

                characterType = characterType.BaseType;
            }

            return null;
        }

        private static Type ResolveType(string fullName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
    }
}
