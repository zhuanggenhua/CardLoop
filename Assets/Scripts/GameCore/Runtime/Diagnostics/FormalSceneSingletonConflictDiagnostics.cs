using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GameCore
{
    /// <summary>
    /// 诊断正式场景里本应唯一存在的输入节点和音频节点冲突。
    /// 这里只做一次性取证，不自动创建、删除或修正任何运行时对象。
    /// </summary>
    internal static class FormalSceneSingletonConflictDiagnostics
    {
        private static readonly HashSet<string> FormalScenePaths = new()
        {
            // 运行时侧不能直接读 EditorBuildSettings；
            // 当前正式启动入口只有 SampleScene，因此这里先只对白名单正式场景做冲突诊断。
            "Assets/Scenes/SampleScene.unity"
        };

        private static readonly HashSet<string> ReportedIssueKeys = new();

        public static void ReportFormalSceneSingletonConflicts(string trigger)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !FormalScenePaths.Contains(activeScene.path))
            {
                return;
            }

            ReportSingletonCount<EventSystem>("EventSystem", trigger, activeScene);
            ReportSingletonCount<AudioListener>("AudioListener", trigger, activeScene);
        }

        private static void ReportSingletonCount<T>(string singletonName, string trigger, Scene activeScene) where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            if (instances.Length == 1)
            {
                return;
            }

            string issueKey = $"{activeScene.path}|{singletonName}|{instances.Length}";
            if (!ReportedIssueKeys.Add(issueKey))
            {
                return;
            }

            var message = new StringBuilder()
                .Append($"[{nameof(FormalSceneSingletonConflictDiagnostics)}] 正式场景 {activeScene.path} ")
                .Append($"在 {trigger} 后检测到 {singletonName} 数量异常：")
                .Append(instances.Length)
                .AppendLine("。")
                .Append("期望数量：1。")
                .AppendLine(" 这说明当前运行时仍有隐式第二真相。");

            if (instances.Length == 0)
            {
                message.Append("当前没有找到任何激活中的 ").Append(singletonName).Append("。");
                Debug.LogError(message.ToString());
                return;
            }

            message.AppendLine().AppendLine("实际对象：");

            for (int i = 0; i < instances.Length; i++)
            {
                T instance = instances[i];
                message.Append(i + 1)
                    .Append(". scene=")
                    .Append(GetSceneLabel(instance.gameObject.scene))
                    .Append(" path=")
                    .Append(GetHierarchyPath(instance.transform))
                    .Append(" activeInHierarchy=")
                    .Append(instance.gameObject.activeInHierarchy)
                    .Append(" enabled=")
                    .Append(GetEnabledLabel(instance))
                    .Append(" instanceId=")
                    .Append(GetObjectId(instance))
                    .AppendLine();
            }

            Debug.LogError(message.ToString(), instances[0]);
        }

        private static string GetEnabledLabel(Component component)
        {
            return component is Behaviour behaviour ? behaviour.enabled.ToString() : "N/A";
        }

        private static int GetObjectId(Object target)
        {
#if UNITY_6000_0_OR_NEWER
            return target != null ? target.GetEntityId().GetHashCode() : 0;
#else
            return target != null ? target.GetInstanceID() : 0;
#endif
        }

        private static string GetSceneLabel(Scene scene)
        {
            if (!scene.IsValid())
            {
                return "<InvalidScene>";
            }

            if (!string.IsNullOrEmpty(scene.path))
            {
                return scene.path;
            }

            return string.IsNullOrEmpty(scene.name) ? "<DontDestroyOnLoad>" : scene.name;
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "<null>";
            }

            var segments = new List<string>();
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
