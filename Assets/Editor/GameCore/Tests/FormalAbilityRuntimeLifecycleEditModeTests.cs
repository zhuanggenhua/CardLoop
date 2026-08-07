using System.Reflection;
using GAS.Runtime;
using NUnit.Framework;

namespace GameCore.Tests
{
    public sealed class FormalAbilityRuntimeLifecycleEditModeTests
    {
        private static readonly BindingFlags StaticMethodFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [TearDown]
        public void TearDown()
        {
            GasEditModeTestHelper.ShutdownWorld();
        }

        [Test]
        public void Shutdown_ReleasesGasWorldAndAllowsCleanReinitialization()
        {
            GasEditModeTestHelper.ResetWorld();

            InvokeBootstrap("Shutdown");

            Assert.That(GASManager.IsInitialized, Is.False);
            Assert.That(GASManager.IsRunning, Is.False);
            Assert.That(GASManager.ExWorld, Is.Null);
            Assert.That(GASManager.EntityGlobalTimer, Is.EqualTo(Unity.Entities.Entity.Null));

            InvokeBootstrap("EnsureInitialized");

            Assert.That(GASManager.IsInitialized, Is.True);
            Assert.That(GASManager.IsRunning, Is.True);
            Assert.That(GASManager.ExWorld, Is.Not.Null);
            Assert.That(GASManager.ExWorld.IsCreated, Is.True);
            Assert.That(TagHelper.HasTag(XTag.Ability_Gun_Shoot, XTag.Ability_Gun), Is.True);
        }

        private static void InvokeBootstrap(string methodName)
        {
            System.Type bootstrapType = typeof(GameManager).Assembly.GetType(
                "GameCore.FormalAbilityRuntimeBootstrap",
                throwOnError: true);
            MethodInfo method = bootstrapType.GetMethod(methodName, StaticMethodFlags);
            Assert.That(method, Is.Not.Null, $"找不到 FormalAbilityRuntimeBootstrap.{methodName}。");
            method.Invoke(null, null);
        }
    }
}
