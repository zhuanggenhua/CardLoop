using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace GameCore
{
    /// <summary>
    /// 编辑器场景查询工具，统一从 Build Settings 和 AssetDatabase 生成场景快照。
    /// </summary>
    public static class SceneUtil
    {
        /// <summary>
        /// 正式场景地址和编辑器场景菜单只来自项目场景根，避免插件 Demo、参考模板和恢复场景混入正式入口。
        /// </summary>
        private static readonly string[] SceneSearchRoots =
        {
            "Assets/Scenes",
            "Assets/Settings/Scenes"
        };

        /// <summary>
        /// 单个场景资产的编辑器快照，记录路径和是否进入 Build Settings。
        /// </summary>
        public readonly struct SceneEntry
        {
            public SceneEntry(string path, bool isInBuildSettings)
            {
                Path = path;
                IsInBuildSettings = isInBuildSettings;
            }

            public string Path { get; }

            public bool IsInBuildSettings { get; }

            public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

            public string RelativePathWithoutExtension
            {
                get
                {
                    string relativePath = Path.Replace('\\', '/');
                    if (relativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = relativePath.Substring("Assets/".Length);
                    }

                    const string sceneExtension = ".unity";
                    if (relativePath.EndsWith(sceneExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = relativePath.Substring(0, relativePath.Length - sceneExtension.Length);
                    }

                    return relativePath.Replace('\\', '/');
                }
            }

            public string MenuPath => RelativePathWithoutExtension.Replace('\\', '/');
        }

        public static string[] CreateBuildSettingsSceneNameSnapshot()
        {
            List<string> sceneNames = new();

            foreach (var scene in EditorBuildSettings.scenes)
            {
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.path);
                sceneNames.Add(sceneName);
            }

            return sceneNames.ToArray();
        }

        /// <summary>
        /// 当前 YooAsset 场景作者入口使用的地址快照。
        /// 正式场景收集规则采用 AddressByFileName，因此这里返回场景文件名而不是 Build Settings 名单。
        /// </summary>
        public static string[] CreateSceneAddressSnapshot()
        {
            return CreateAssetDatabaseScenePathSnapshot()
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        public static string[] CreateAssetDatabaseScenePathSnapshot()
        {
            string[] guids = AssetDatabase.FindAssets("t:scene", SceneSearchRoots);
            return guids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(IsProjectScenePath)
                .Where(path => !IsUnityTestRunnerScene(path))
                .ToArray();
        }

        public static bool IsProjectScenePath(string scenePath)
        {
            string normalizedPath = scenePath?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalizedPath) ||
                !normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return SceneSearchRoots.Any(root =>
            {
                string normalizedRoot = root.TrimEnd('/').Replace('\\', '/');
                return normalizedPath.StartsWith(
                    normalizedRoot + "/",
                    StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool IsUnityTestRunnerScene(string scenePath)
        {
            string normalizedPath = scenePath?.Replace('\\', '/');
            return normalizedPath != null &&
                normalizedPath.StartsWith("Assets/InitTestScene", StringComparison.OrdinalIgnoreCase) &&
                normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        public static SceneEntry[] CreateSceneEntrySnapshot()
        {
            HashSet<string> buildScenePaths = EditorBuildSettings.scenes
                .Select(scene => scene.path.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return CreateAssetDatabaseScenePathSnapshot()
                .Select(path => path.Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new SceneEntry(path, buildScenePaths.Contains(path)))
                .OrderBy(entry => entry.MenuPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static SceneEntry[] CreateBuildSettingsSceneEntrySnapshot()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path.Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new SceneEntry(path, isInBuildSettings: true))
                .OrderBy(entry => entry.MenuPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
