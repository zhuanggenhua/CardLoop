using System;
using System.Linq;
using System.Reflection;
using GAS.General;
using GAS.Runtime;
using UnityEngine;

namespace GameCore
{
    internal static class FormalAbilityRuntimeBootstrap
    {
        private static bool s_initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubsystemState()
        {
            if (GASManager.ExWorld is { IsCreated: true })
            {
                return;
            }

            s_initialized = false;
        }

        public static void EnsureInitialized()
        {
            if (s_initialized)
            {
                EnsureGasWorldIsUsable();
                GASManager.Run();
                return;
            }

            EnsureReadyForInitialization();
            bool gasInitializationEntered = false;
            try
            {
                EnsureResourceLoaderRegistered();
                EnsureGeneratedConfigTablesInitialized();
                gasInitializationEntered = true;
                GASManager.Initialize();
                EnsureGasWorldIsUsable();
                EnsureGameCoreGasExtensionsRegistered();
                GASManager.Run();
                EnsureGeneratedGasCachesInitialized();

                CharacterAbilitySet.EnsureFormalAbilityRuleLogicRegistered();
                CharacterAbilitySet.EnsureFormalRuleSupportTypesRegistered();

                s_initialized = true;
            }
            catch (Exception initializationException)
            {
                try
                {
                    RollbackFailedInitialization(gasInitializationEntered);
                }
                catch (Exception shutdownException)
                {
                    throw new AggregateException(
                        "EX-GAS 项目组合初始化失败，且项目侧回滚也失败。",
                        initializationException,
                        shutdownException);
                }

                throw;
            }
        }

        public static void Shutdown()
        {
            if (!s_initialized)
            {
                if (HasGasRuntimeState())
                {
                    throw new InvalidOperationException(
                        "EX-GAS 已由其它入口启动，项目组合入口不能关闭外部 GAS 运行时。"
                        + "请只保留 GameManager 的正式 GAS 启动入口。");
                }

                return;
            }

            EnsureGasWorldIsUsable();
            GASManager.Shutdown();
            s_initialized = false;
        }

        private static void EnsureResourceLoaderRegistered()
        {
            GASResourceLoader.Register(
                FormalGasAbilityResourceLoader.LoadSync,
                FormalGasAbilityResourceLoader.LoadAsync,
                FormalGasAbilityResourceLoader.Release);
        }

        /// <summary>
        /// 生成表程序集依赖 GameCore 的 GAS 扩展，GameCore 不能反向建立程序集引用。
        /// 这里是唯一允许的组合调用点：资源系统已完成初始化后，要求生成程序集装载配置表。
        /// </summary>
        private static void EnsureGeneratedConfigTablesInitialized()
        {
            const string integrationType = "GAS.Runtime.GasGeneratedConfigIntegration";
            const string initializeMethod = "InitializeConfigTables";

            object result = InvokeRequiredStaticMethod(integrationType, initializeMethod);
            if (result is not true)
            {
                throw new InvalidOperationException(
                    "EX-GAS 生成配置表初始化失败。"
                    + "请检查 gas-config YooAsset 收集组和配置表内容。");
            }
        }

        private static void EnsureGameCoreGasExtensionsRegistered()
        {
            if (GASManager.ExWorld == null || !GASManager.ExWorld.IsCreated)
            {
                throw new InvalidOperationException(
                    "EX-GAS World 尚未创建，不能注册项目侧 GameplayEffect 扩展。");
            }

            var instantEffectGroup = GASManager.ExWorld.GetExistingSystemManaged<SGInstantEffect>();
            if (instantEffectGroup == null)
            {
                throw new InvalidOperationException(
                    "EX-GAS World 缺少即时 GameplayEffect 系统组，不能注册项目侧伤害结算扩展。");
            }

            var damageSystem = GASManager.ExWorld.GetOrCreateSystemManaged<SExecuteGameplayEffectDamageManaged>();
            instantEffectGroup.AddSystemToUpdateList(damageSystem);
            instantEffectGroup.SortSystems();
        }

        private static void EnsureGeneratedGasCachesInitialized()
        {
            InvokeRequiredStaticMethod("GAS.Runtime.XLauncher", "InitCache");
            InvokeRequiredStaticMethod("GAS.Runtime.XTag", "InitTagList");

#if UNITY_EDITOR
            if (TryFindStaticType("GAS.Runtime.XLuban") != null)
            {
                InvokeRequiredStaticMethod("GAS.Runtime.XLuban", "LoadTablesForEditor");
            }
#endif
        }

        private static void EnsureReadyForInitialization()
        {
            if (HasGasRuntimeState())
            {
                throw new InvalidOperationException(
                    "EX-GAS 已由其它入口启动，项目组合入口不能接管外部 GAS 运行时。"
                    + "请只保留 GameManager 的正式 GAS 启动入口。");
            }
        }

        private static void EnsureGasWorldIsUsable()
        {
            if (!GASManager.IsInitialized || GASManager.ExWorld is not { IsCreated: true })
            {
                throw new InvalidOperationException(
                    "项目 GAS 生命周期状态与 EX-GAS World 不一致。"
                    + "请定位绕过 FormalAbilityRuntimeBootstrap 的启动或关闭调用。");
            }
        }

        private static bool HasGasRuntimeState()
        {
            return GASManager.IsInitialized ||
                   GASManager.IsRunning ||
                   GASManager.ExWorld != null ||
                   GASManager.EntityGlobalTimer != Unity.Entities.Entity.Null;
        }

        private static void RollbackFailedInitialization(bool gasInitializationEntered)
        {
            s_initialized = false;
            if (!gasInitializationEntered || !HasGasRuntimeState())
            {
                return;
            }

            GASManager.Shutdown();
        }

        private static object InvokeRequiredStaticMethod(string fullTypeName, string methodName)
        {
            Type type = TryFindStaticType(fullTypeName) ?? throw new InvalidOperationException(
                $"缺少 EX-GAS 生成入口 {fullTypeName}。请检查 GameCore.GasIntegration 与生成程序集是否已编译。");
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException(
                $"缺少 EX-GAS 生成方法 {fullTypeName}.{methodName}。请重新生成 GAS 配置代码。");

            try
            {
                return method.Invoke(null, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw new InvalidOperationException(
                    $"调用 EX-GAS 生成入口 {fullTypeName}.{methodName} 失败。",
                    exception.InnerException);
            }
        }

        private static Type TryFindStaticType(string fullTypeName)
        {
            return Type.GetType(fullTypeName) ?? AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullTypeName, throwOnError: false))
                .FirstOrDefault(type => type != null);
        }
    }
}
