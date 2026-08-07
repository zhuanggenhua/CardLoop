using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace GameCore.Tests
{
    public sealed class InputSystemActionAssetEditModeTests
    {
        private const string InputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";

        [Test]
        public void FormalInputAsset_ContainsRuntimeAndUiOwnerContracts()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            Assert.That(asset, Is.Not.Null, $"缺少正式输入作者源：{InputActionsAssetPath}");

            InputActionMap gameplay = asset.FindActionMap("Gameplay", throwIfNotFound: true);
            AssertActions(
                gameplay,
                "Move",
                "Interact",
                "FireAbility1",
                "FireAbility2",
                "FireAbility3",
                "FireAbility4",
                "FireAbility5",
                "OpenGameMenu",
                "Point",
                "Click",
                "ToggleMovementControlMode");

            InputActionMap ui = asset.FindActionMap("UI", throwIfNotFound: true);
            AssertActions(
                ui,
                "Navigate",
                "Submit",
                "Cancel",
                "Point",
                "Click",
                "RightClick",
                "MiddleClick",
                "ScrollWheel",
                "TrackedDevicePosition",
                "TrackedDeviceOrientation");

            Assert.That(asset.FindActionMap("None", throwIfNotFound: false), Is.Not.Null);
            Assert.That(asset.FindActionMap("Player", throwIfNotFound: false), Is.Null,
                "旧模板 Player 动作图不能继续与正式 Gameplay 动作图并存。");
        }

        private static void AssertActions(InputActionMap actionMap, params string[] actionNames)
        {
            for (int i = 0; i < actionNames.Length; i++)
            {
                Assert.That(
                    actionMap.FindAction(actionNames[i], throwIfNotFound: false),
                    Is.Not.Null,
                    $"动作图 {actionMap.name} 缺少正式动作 {actionNames[i]}。");
            }
        }
    }
}
