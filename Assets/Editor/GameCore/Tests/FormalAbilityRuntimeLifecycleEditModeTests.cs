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

        [Test]
        public void EnsureInitialized_WhenGasAlreadyStartedByExternalEntry_RejectsOwnershipAndShutdownDoesNotReleaseExternalWorld()
        {
            GasEditModeTestHelper.ShutdownWorld();
            GASManager.Initialize();
            XTag.InitTagList();

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => InvokeBootstrap("EnsureInitialized"));
            Assert.That(exception.InnerException, Is.TypeOf<System.InvalidOperationException>());
            StringAssert.Contains("其它入口", exception.InnerException.Message);

            TargetInvocationException shutdownException = Assert.Throws<TargetInvocationException>(
                () => InvokeBootstrap("Shutdown"));
            Assert.That(shutdownException.InnerException, Is.TypeOf<System.InvalidOperationException>());
            StringAssert.Contains("其它入口", shutdownException.InnerException.Message);
            Assert.That(GASManager.IsInitialized, Is.True);
            Assert.That(GASManager.ExWorld, Is.Not.Null);
            Assert.That(GASManager.ExWorld.IsCreated, Is.True);
            Assert.That(GASManager.EntityGlobalTimer, Is.Not.EqualTo(Unity.Entities.Entity.Null));

            GASManager.Shutdown();
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
