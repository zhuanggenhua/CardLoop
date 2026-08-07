using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GamePlay.EditorTools
{
    public static class StackCraftReferenceInputCompatibility
    {
        private const string InputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string StackCraftSceneRoot = "Assets/StackCraft/Scenes";
        private const string StackCraftUiRootPrefabPath = "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";

        [MenuItem("GamePlay/StackCraft/Fix Reference Input Compatibility")]
        public static void FixAll()
        {
            var actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            if (actionsAsset == null)
                throw new InvalidOperationException($"Missing input actions asset: {InputActionsAssetPath}");

            int changedAssets = 0;
            if (FixPrefab(StackCraftUiRootPrefabPath, actionsAsset))
                changedAssets++;

            string[] scenePaths = Directory.Exists(StackCraftSceneRoot)
                ? Directory.GetFiles(StackCraftSceneRoot, "*.unity", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : Array.Empty<string>();

            string activeScenePath = SceneManager.GetActiveScene().path;
            foreach (string scenePath in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (FixScene(scene, actionsAsset))
                {
                    EditorSceneManager.SaveScene(scene);
                    changedAssets++;
                }
            }

            if (!string.IsNullOrWhiteSpace(activeScenePath) && File.Exists(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"StackCraft reference input compatibility checked. Changed assets: {changedAssets}");
        }

        private static bool FixPrefab(string prefabPath, InputActionAsset actionsAsset)
        {
            if (!File.Exists(prefabPath))
                return false;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                bool changed = FixEventSystems(root.GetComponentsInChildren<EventSystem>(true), actionsAsset);
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool FixScene(Scene scene, InputActionAsset actionsAsset)
        {
            var eventSystems = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                .ToArray();

            bool changed = FixEventSystems(eventSystems, actionsAsset);
            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);

            return changed;
        }

        private static bool FixEventSystems(IEnumerable<EventSystem> eventSystems, InputActionAsset actionsAsset)
        {
            bool changed = false;
            foreach (EventSystem eventSystem in eventSystems)
            {
                GameObject root = eventSystem.gameObject;

                foreach (StandaloneInputModule standaloneModule in root.GetComponents<StandaloneInputModule>())
                {
                    UnityEngine.Object.DestroyImmediate(standaloneModule, true);
                    changed = true;
                }

                var inputModule = root.GetComponent<InputSystemUIInputModule>();
                if (inputModule == null)
                {
                    inputModule = root.AddComponent<InputSystemUIInputModule>();
                    changed = true;
                }

                changed |= AssignInputActions(inputModule, actionsAsset);
                EditorUtility.SetDirty(root);
                EditorUtility.SetDirty(eventSystem);
                EditorUtility.SetDirty(inputModule);
            }

            return changed;
        }

        private static bool AssignInputActions(InputSystemUIInputModule module, InputActionAsset actionsAsset)
        {
            bool changed = false;
            changed |= SetIfDifferent(module.actionsAsset, actionsAsset, value => module.actionsAsset = value);
            changed |= SetIfDifferent(module.point, FindInputActionReference(actionsAsset, "Point"), value => module.point = value);
            changed |= SetIfDifferent(module.leftClick, FindInputActionReference(actionsAsset, "Click", "LeftClick", "Left Click"), value => module.leftClick = value);
            changed |= SetIfDifferent(module.middleClick, FindInputActionReference(actionsAsset, "MiddleClick", "Middle Click"), value => module.middleClick = value);
            changed |= SetIfDifferent(module.rightClick, FindInputActionReference(actionsAsset, "RightClick", "Right Click", "ContextClick", "Context Click"), value => module.rightClick = value);
            changed |= SetIfDifferent(module.scrollWheel, FindInputActionReference(actionsAsset, "ScrollWheel", "Scroll Wheel", "Scroll"), value => module.scrollWheel = value);
            changed |= SetIfDifferent(module.move, FindInputActionReference(actionsAsset, "Navigate", "Move"), value => module.move = value);
            changed |= SetIfDifferent(module.submit, FindInputActionReference(actionsAsset, "Submit"), value => module.submit = value);
            changed |= SetIfDifferent(module.cancel, FindInputActionReference(actionsAsset, "Cancel", "Esc", "Escape"), value => module.cancel = value);
            changed |= SetIfDifferent(module.trackedDevicePosition, FindInputActionReference(actionsAsset, "TrackedDevicePosition", "Position"), value => module.trackedDevicePosition = value);
            changed |= SetIfDifferent(module.trackedDeviceOrientation, FindInputActionReference(actionsAsset, "TrackedDeviceOrientation", "Orientation"), value => module.trackedDeviceOrientation = value);

            if (module.pointerBehavior != UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack)
            {
                module.pointerBehavior = UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;
                changed = true;
            }

            if (module.cursorLockBehavior != InputSystemUIInputModule.CursorLockBehavior.OutsideScreen)
            {
                module.cursorLockBehavior = InputSystemUIInputModule.CursorLockBehavior.OutsideScreen;
                changed = true;
            }

            return changed;
        }

        private static bool SetIfDifferent<T>(T currentValue, T newValue, Action<T> setter) where T : UnityEngine.Object
        {
            if (currentValue == newValue)
                return false;

            setter(newValue);
            return true;
        }

        private static InputActionReference FindInputActionReference(InputActionAsset actionsAsset, params string[] candidateNames)
        {
            string assetPath = AssetDatabase.GetAssetPath(actionsAsset);
            var references = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<InputActionReference>()
                .Where(reference => reference.action != null);

            foreach (InputActionReference reference in references)
            {
                foreach (string candidateName in candidateNames)
                {
                    if (string.Equals(reference.action.name, candidateName, StringComparison.OrdinalIgnoreCase))
                        return reference;
                }
            }

            return null;
        }
    }
}
