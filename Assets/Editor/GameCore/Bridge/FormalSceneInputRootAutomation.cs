#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GameCore
{
    /// <summary>
    /// 正式场景显式输入根节点的 AutomationOnly 入口。
    /// 这里只提供检查与确定性修复，不自动执行，也不替用户保存正式场景。
    /// </summary>
    public static class FormalSceneInputRootAutomation
    {
        private const string InputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string DefaultRepairMethodName = nameof(EnsureOpenFormalSceneInputRoot);
        private const string DirtyFormalSceneRepairMethodName = nameof(EnsureOpenFormalSceneInputRootAllowDirtyFormalScene);

        [Serializable]
        public sealed class FormalSceneInputRootInspectionResult
        {
            public bool Success;
            public string Message = string.Empty;
            public string ActiveScenePath = string.Empty;
            public string ActiveSceneName = string.Empty;
            public bool IsFormalScene;
            public bool SceneIsDirty;
            public bool InputActionsAssetFound;
            public int EventSystemCount;
            public int InputSystemUIInputModuleCount;
            public int StandaloneInputModuleCount;
            public bool HasExplicitInputRoot;
            public bool InputActionsAssigned;
            public bool NeedsRepair;
            public bool RepairBlockedByDirtyScene;
            public string RecommendedRepairMethod = string.Empty;
            public string[] EventSystemHierarchyPaths = Array.Empty<string>();
            public string[] InputModuleHierarchyPaths = Array.Empty<string>();
            public string[] StandaloneModuleHierarchyPaths = Array.Empty<string>();
        }

        public static FormalSceneInputRootInspectionResult InspectOpenFormalScene()
        {
            return InspectScene(SceneManager.GetActiveScene());
        }

        public static FormalSceneInputRootInspectionResult EnsureOpenFormalSceneInputRoot()
        {
            return EnsureOpenFormalSceneInputRootCore(allowDirtyFormalScene: false);
        }

        public static FormalSceneInputRootInspectionResult EnsureOpenFormalSceneInputRootAllowDirtyFormalScene()
        {
            return EnsureOpenFormalSceneInputRootCore(allowDirtyFormalScene: true);
        }

        private static FormalSceneInputRootInspectionResult EnsureOpenFormalSceneInputRootCore(bool allowDirtyFormalScene)
        {
            Scene scene = SceneManager.GetActiveScene();
            FormalSceneInputRootInspectionResult inspection = InspectScene(scene);
            if (!inspection.Success || !inspection.IsFormalScene)
            {
                return inspection;
            }

            if (inspection.SceneIsDirty && !allowDirtyFormalScene)
            {
                inspection.Success = false;
                inspection.NeedsRepair = true;
                inspection.RepairBlockedByDirtyScene = true;
                inspection.RecommendedRepairMethod = DirtyFormalSceneRepairMethodName;
                inspection.Message = $"当前正式场景存在未保存改动。默认修复入口已拒绝执行；只有在用户明确授权处理脏正式场景时，才允许调用 {DirtyFormalSceneRepairMethodName}().";
                return inspection;
            }

            if (!inspection.InputActionsAssetFound)
            {
                inspection.Success = false;
                inspection.Message = $"缺少正式输入动作资源：{InputActionsAssetPath}。";
                return inspection;
            }

            List<EventSystem> eventSystems = GetSceneComponents<EventSystem>(scene);
            List<InputSystemUIInputModule> inputModules = GetSceneComponents<InputSystemUIInputModule>(scene);
            List<StandaloneInputModule> standaloneModules = GetSceneComponents<StandaloneInputModule>(scene);

            if (eventSystems.Count > 1 || inputModules.Count > 1 || standaloneModules.Count > 1)
            {
                inspection.Success = false;
                inspection.Message = "当前正式场景存在多个输入根节点组件，修复入口拒绝自动改写，请先人工确认唯一真相。";
                inspection.NeedsRepair = true;
                return inspection;
            }

            InputActionAsset? actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            if (actionsAsset == null)
            {
                inspection.Success = false;
                inspection.Message = $"无法加载正式输入动作资源：{InputActionsAssetPath}。";
                return inspection;
            }

            GameObject rootObject;
            EventSystem eventSystem;
            if (eventSystems.Count == 0)
            {
                rootObject = new GameObject("EventSystem");
                SceneManager.MoveGameObjectToScene(rootObject, scene);
                eventSystem = rootObject.AddComponent<EventSystem>();
            }
            else
            {
                eventSystem = eventSystems[0];
                rootObject = eventSystem.gameObject;
            }

            // 正式场景只允许一套新输入系统模块；若唯一旧模块挂在同一根节点上，就在修复时直接替换。
            if (standaloneModules.Count == 1 && standaloneModules[0] != null && standaloneModules[0].gameObject == rootObject)
            {
                UnityEngine.Object.DestroyImmediate(standaloneModules[0], true);
            }

            InputSystemUIInputModule inputModule;
            if (inputModules.Count == 0)
            {
                inputModule = rootObject.AddComponent<InputSystemUIInputModule>();
            }
            else if (inputModules[0].gameObject == rootObject)
            {
                inputModule = inputModules[0];
            }
            else
            {
                inspection.Success = false;
                inspection.Message = "唯一 InputSystemUIInputModule 不在正式 EventSystem 根节点上，修复入口拒绝自动搬迁。";
                inspection.NeedsRepair = true;
                return inspection;
            }

            List<string> missingActionNames = AssignFormalInputActions(inputModule, actionsAsset);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.SetDirty(rootObject);
            EditorUtility.SetDirty(eventSystem);
            EditorUtility.SetDirty(inputModule);

            FormalSceneInputRootInspectionResult result = InspectScene(scene);
            result.Success = result.HasExplicitInputRoot && missingActionNames.Count == 0;
            result.NeedsRepair = !result.HasExplicitInputRoot;
            result.RepairBlockedByDirtyScene = false;
            UpdateRecommendedRepairMethod(result);
            result.Message = result.Success
                ? "正式场景显式输入根节点已补齐，场景已标记为未保存。"
                : $"正式场景输入根节点已尝试修复，但仍缺少动作引用：{string.Join("、", missingActionNames)}。";
            return result;
        }

        private static FormalSceneInputRootInspectionResult InspectScene(Scene scene)
        {
            FormalSceneInputRootInspectionResult result = new()
            {
                ActiveScenePath = scene.path ?? string.Empty,
                ActiveSceneName = scene.name ?? string.Empty
            };

            if (!scene.IsValid() || !scene.isLoaded)
            {
                result.Success = false;
                result.Message = "当前没有可检查的已加载场景。";
                return result;
            }

            string scenePath = scene.path ?? string.Empty;
            result.IsFormalScene = scenePath.Length > 0 && IsBuildSettingsFormalScene(scenePath);
            result.SceneIsDirty = scene.isDirty;
            result.InputActionsAssetFound = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath) != null;

            List<EventSystem> eventSystems = GetSceneComponents<EventSystem>(scene);
            List<InputSystemUIInputModule> inputModules = GetSceneComponents<InputSystemUIInputModule>(scene);
            List<StandaloneInputModule> standaloneModules = GetSceneComponents<StandaloneInputModule>(scene);

            result.EventSystemCount = eventSystems.Count;
            result.InputSystemUIInputModuleCount = inputModules.Count;
            result.StandaloneInputModuleCount = standaloneModules.Count;
            result.EventSystemHierarchyPaths = eventSystems.Select(component => GetHierarchyPath(component.transform)).ToArray();
            result.InputModuleHierarchyPaths = inputModules.Select(component => GetHierarchyPath(component.transform)).ToArray();
            result.StandaloneModuleHierarchyPaths = standaloneModules.Select(component => GetHierarchyPath(component.transform)).ToArray();

            InputSystemUIInputModule? inputModule = inputModules.Count == 1 ? inputModules[0] : null;
            EventSystem? eventSystem = eventSystems.Count == 1 ? eventSystems[0] : null;
            result.InputActionsAssigned = inputModule != null && HasAllFormalInputActions(inputModule);
            result.HasExplicitInputRoot =
                eventSystem != null &&
                inputModule != null &&
                standaloneModules.Count == 0 &&
                eventSystem.gameObject == inputModule.gameObject &&
                result.InputActionsAssigned;

            result.Success = true;
            result.NeedsRepair = result.IsFormalScene && !result.HasExplicitInputRoot;
            result.RepairBlockedByDirtyScene = false;
            UpdateRecommendedRepairMethod(result);
            result.Message = !result.IsFormalScene
                ? "当前打开的不是正式场景；只做检查，不执行修复。"
                : result.HasExplicitInputRoot
                    ? "正式场景已具备显式 EventSystem + InputSystemUIInputModule。"
                    : "正式场景缺少显式输入根节点，仍需修复。";
            return result;
        }

        private static void UpdateRecommendedRepairMethod(FormalSceneInputRootInspectionResult result)
        {
            if (!result.IsFormalScene || !result.NeedsRepair || result.HasExplicitInputRoot)
            {
                result.RecommendedRepairMethod = string.Empty;
                return;
            }

            result.RecommendedRepairMethod = result.SceneIsDirty
                ? DirtyFormalSceneRepairMethodName
                : DefaultRepairMethodName;
        }

        private static bool IsBuildSettingsFormalScene(string scenePath)
        {
            foreach (EditorBuildSettingsScene buildSettingsScene in EditorBuildSettings.scenes)
            {
                if (!buildSettingsScene.enabled)
                {
                    continue;
                }

                if (string.Equals(buildSettingsScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> AssignFormalInputActions(InputSystemUIInputModule module, InputActionAsset actionsAsset)
        {
            module.actionsAsset = actionsAsset;
            module.point = FindInputActionReference(actionsAsset, module.point?.action?.name, "Point", "MousePosition", "Mouse Position");
            module.leftClick = FindInputActionReference(actionsAsset, module.leftClick?.action?.name, "Click", "LeftClick", "Left Click");
            module.middleClick = FindInputActionReference(actionsAsset, module.middleClick?.action?.name, "MiddleClick", "Middle Click");
            module.rightClick = FindInputActionReference(actionsAsset, module.rightClick?.action?.name, "RightClick", "Right Click", "ContextClick", "Context Click", "ContextMenu", "Context Menu");
            module.scrollWheel = FindInputActionReference(actionsAsset, module.scrollWheel?.action?.name, "ScrollWheel", "Scroll Wheel", "Scroll", "Wheel");
            module.move = FindInputActionReference(actionsAsset, module.move?.action?.name, "Navigate", "Move");
            module.submit = FindInputActionReference(actionsAsset, module.submit?.action?.name, "Submit");
            module.cancel = FindInputActionReference(actionsAsset, module.cancel?.action?.name, "Cancel", "Esc", "Escape");
            module.trackedDevicePosition = FindInputActionReference(actionsAsset, module.trackedDevicePosition?.action?.name, "TrackedDevicePosition", "Position");
            module.trackedDeviceOrientation = FindInputActionReference(actionsAsset, module.trackedDeviceOrientation?.action?.name, "TrackedDeviceOrientation", "Orientation");
            module.deselectOnBackgroundClick = false;
            module.pointerBehavior = UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;
            module.cursorLockBehavior = InputSystemUIInputModule.CursorLockBehavior.OutsideScreen;
            module.scrollDeltaPerTick = 6f;

            List<string> missingActionNames = new();
            if (module.point == null) missingActionNames.Add("Point");
            if (module.leftClick == null) missingActionNames.Add("LeftClick");
            if (module.middleClick == null) missingActionNames.Add("MiddleClick");
            if (module.rightClick == null) missingActionNames.Add("RightClick");
            if (module.scrollWheel == null) missingActionNames.Add("ScrollWheel");
            if (module.move == null) missingActionNames.Add("Move");
            if (module.submit == null) missingActionNames.Add("Submit");
            if (module.cancel == null) missingActionNames.Add("Cancel");
            if (module.trackedDevicePosition == null) missingActionNames.Add("TrackedDevicePosition");
            if (module.trackedDeviceOrientation == null) missingActionNames.Add("TrackedDeviceOrientation");
            return missingActionNames;
        }

        private static bool HasAllFormalInputActions(InputSystemUIInputModule module)
        {
            return module.actionsAsset != null &&
                module.point != null &&
                module.leftClick != null &&
                module.middleClick != null &&
                module.rightClick != null &&
                module.scrollWheel != null &&
                module.move != null &&
                module.submit != null &&
                module.cancel != null &&
                module.trackedDevicePosition != null &&
                module.trackedDeviceOrientation != null;
        }

        private static InputActionReference? FindInputActionReference(InputActionAsset actionsAsset, params string?[] candidateNames)
        {
            foreach (InputActionReference reference in GetAllAssetReferences(actionsAsset))
            {
                if (reference.action == null)
                {
                    continue;
                }

                foreach (string? candidateName in candidateNames)
                {
                    if (!string.IsNullOrWhiteSpace(candidateName) &&
                        string.Equals(reference.action.name, candidateName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return reference;
                    }
                }
            }

            return null;
        }

        private static InputActionReference[] GetAllAssetReferences(InputActionAsset actionsAsset)
        {
            string assetPath = AssetDatabase.GetAssetPath(actionsAsset);
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<InputActionReference>()
                .OrderBy(reference => reference.name, StringComparer.InvariantCultureIgnoreCase)
                .ToArray();
        }

        private static List<T> GetSceneComponents<T>(Scene scene) where T : Component
        {
            List<T> components = new();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                components.AddRange(rootObject.GetComponentsInChildren<T>(true));
            }

            return components;
        }

        private static string GetHierarchyPath(Transform target)
        {
            List<string> segments = new();
            Transform current = target;
            while (current != null)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
