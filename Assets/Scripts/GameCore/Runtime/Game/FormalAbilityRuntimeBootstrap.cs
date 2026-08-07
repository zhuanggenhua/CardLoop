using GAS.Runtime;
using GAS.General;
using UnityEngine;

namespace GameCore
{
    internal static class FormalAbilityRuntimeBootstrap
    {
        private static bool s_initialized;
        private static bool s_gameCoreGasExtensionsRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubsystemState()
        {
            if (GASManager.ExWorld is { IsCreated: true })
            {
                return;
            }

            s_initialized = false;
            s_gameCoreGasExtensionsRegistered = false;
        }

        public static void EnsureInitialized()
        {
            EnsureResourceLoaderRegistered();

            if (s_initialized && GASManager.ExWorld != null && GASManager.ExWorld.IsCreated)
            {
                EnsureGameCoreGasExtensionsRegistered();
                GASManager.Run();
                return;
            }

            GASManager.Initialize();
            s_gameCoreGasExtensionsRegistered = false;
            EnsureGameCoreGasExtensionsRegistered();
            GASManager.Run();
            EnsureGeneratedGasCachesInitialized();

            CharacterAbilitySet.EnsureFormalAbilityRuleLogicRegistered();
            CharacterAbilitySet.EnsureFormalRuleSupportTypesRegistered();

            s_initialized = true;
        }

        public static void Shutdown()
        {
            GASManager.Shutdown();
            s_initialized = false;
            s_gameCoreGasExtensionsRegistered = false;
        }

        private static void EnsureResourceLoaderRegistered()
        {
            GASResourceLoader.Register(
                FormalGasAbilityResourceLoader.LoadSync,
                FormalGasAbilityResourceLoader.LoadAsync,
                FormalGasAbilityResourceLoader.Release);
        }

        private static void EnsureGameCoreGasExtensionsRegistered()
        {
            if (s_gameCoreGasExtensionsRegistered ||
                GASManager.ExWorld == null ||
                !GASManager.ExWorld.IsCreated)
            {
                return;
            }

            var instantEffectGroup = GASManager.ExWorld.GetExistingSystemManaged<SGInstantEffect>();
            if (instantEffectGroup == null)
            {
                return;
            }

            var damageSystem = GASManager.ExWorld.GetOrCreateSystemManaged<SExecuteFormalDamageEffectsManaged>();
            instantEffectGroup.AddSystemToUpdateList(damageSystem);
            instantEffectGroup.SortSystems();
            s_gameCoreGasExtensionsRegistered = true;
        }

        private static void EnsureGeneratedGasCachesInitialized()
        {
            if (ReflectionHelper.TypeExists("GAS.Runtime.XLauncher"))
            {
                ReflectionHelper.InvokeStaticMethod("GAS.Runtime.XLauncher", "InitCache");
            }

            if (ReflectionHelper.TypeExists("GAS.Runtime.XTag"))
            {
                ReflectionHelper.InvokeStaticMethod("GAS.Runtime.XTag", "InitTagList");
            }

#if UNITY_EDITOR
            if (ReflectionHelper.TypeExists("GAS.Runtime.XLuban"))
            {
                ReflectionHelper.InvokeStaticMethod("GAS.Runtime.XLuban", "LoadTablesForEditor");
            }
#endif
        }
    }
}
