#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 正式场景相机与直进黑屏配置审计入口。
    /// 用于 AIBridge 不可用时的 Unity 导入级验证，不创建运行时测试控制器。
    /// </summary>
    public static class FormalSceneCameraAudit
    {
        private const string ResultPath = "Temp/UnityBridge/results/formal-scene-camera-audit.json";

        public static void RunForCommandLine()
        {
            AuditResult result = Inspect();
            WriteResult(result);

            if (!result.Success)
            {
                EditorApplication.Exit(1);
            }
        }

        public static AuditResult Inspect()
        {
            List<SceneCameraReport> reports = new();
            foreach (SceneUtil.SceneEntry sceneEntry in SceneUtil.CreateBuildSettingsSceneEntrySnapshot())
            {
                reports.Add(InspectScene(sceneEntry.Path));
            }

            bool success = reports.All(report => report.Success);
            return new AuditResult
            {
                Success = success,
                SceneReports = reports.ToArray(),
            };
        }

        private static SceneCameraReport InspectScene(string scenePath)
        {
            SceneCameraReport report = new()
            {
                ScenePath = scenePath,
                SceneExists = File.Exists(scenePath),
            };

            if (!report.SceneExists)
            {
                report.Failures = new[] { "场景文件不存在。" };
                return report;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            Camera[] rootMainCameras = roots
                .Where(root => root.CompareTag("MainCamera"))
                .Select(root => root.GetComponent<Camera>())
                .Where(camera => camera != null)
                .ToArray()!;

            Camera[] allEnabledMainCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(camera => camera.isActiveAndEnabled && camera.CompareTag("MainCamera"))
                .ToArray();

            TransitionSystem? transitionSystem = UnityEngine.Object.FindObjectsByType<TransitionSystem>(FindObjectsSortMode.None)
                .FirstOrDefault();

            bool startsWithBlackScreen = false;
            if (transitionSystem != null)
            {
                var serializedObject = new SerializedObject(transitionSystem);
                startsWithBlackScreen = serializedObject.FindProperty("m_startWithBlackScreen")?.boolValue ?? false;
            }

            List<string> failures = new();
            if (rootMainCameras.Length != 1)
            {
                failures.Add($"根级 MainCamera 数量应为 1，实际为 {rootMainCameras.Length}。");
            }

            if (allEnabledMainCameras.Length != 1)
            {
                failures.Add($"启用的 MainCamera 数量应为 1，实际为 {allEnabledMainCameras.Length}。");
            }

            Camera? mainCamera = allEnabledMainCameras.FirstOrDefault();
            if (mainCamera == null)
            {
                failures.Add("运行相机不存在。");
            }
            else
            {
                if (!mainCamera.orthographic)
                {
                    failures.Add("主相机不是正交相机。");
                }

                if (Vector3.Distance(mainCamera.transform.position, new Vector3(0f, 0f, -10f)) > 0.001f)
                {
                    failures.Add($"主相机位置不是固定原点视角：{mainCamera.transform.position}。");
                }
            }

            if (startsWithBlackScreen)
            {
                failures.Add("TransitionSystem 仍配置为启动黑屏。");
            }

            report.RootCount = roots.Length;
            report.RootMainCameraCount = rootMainCameras.Length;
            report.EnabledMainCameraCount = allEnabledMainCameras.Length;
            report.HasTransitionSystem = transitionSystem != null;
            report.StartsWithBlackScreen = startsWithBlackScreen;
            report.MainCameraPosition = mainCamera != null ? mainCamera.transform.position.ToString("F2") : string.Empty;
            report.Success = failures.Count == 0;
            report.Failures = failures.ToArray();
            return report;
        }

        private static void WriteResult(AuditResult result)
        {
            string fullPath = Path.GetFullPath(ResultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, JsonUtility.ToJson(result, true));
        }

        /// <summary>
        /// 正式场景相机审计汇总结果。
        /// </summary>
        [Serializable]
        public sealed class AuditResult
        {
            public bool Success;
            public SceneCameraReport[] SceneReports = Array.Empty<SceneCameraReport>();
        }

        /// <summary>
        /// 单个场景的相机和启动黑屏配置审计结果。
        /// </summary>
        [Serializable]
        public sealed class SceneCameraReport
        {
            public string ScenePath = string.Empty;
            public bool SceneExists;
            public bool Success;
            public int RootCount;
            public int RootMainCameraCount;
            public int EnabledMainCameraCount;
            public bool HasTransitionSystem;
            public bool StartsWithBlackScreen;
            public string MainCameraPosition = string.Empty;
            public string[] Failures = Array.Empty<string>();
        }
    }
}
