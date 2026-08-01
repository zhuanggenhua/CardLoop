using System;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// Strongly typed Build Settings scene entry for SceneKit generated code.
    /// </summary>
    public readonly struct SceneKey
    {
        public SceneKey(string sceneName, int buildIndex, bool enabledInBuildSettings, string assetPath)
        {
            SceneName = sceneName ?? string.Empty;
            BuildIndex = buildIndex;
            EnabledInBuildSettings = enabledInBuildSettings;
            AssetPath = assetPath ?? string.Empty;
        }

        public string SceneName { get; }
        public int BuildIndex { get; }
        public bool EnabledInBuildSettings { get; }
        public string AssetPath { get; }
        public bool HasBuildIndex => BuildIndex >= 0;

        public Scene Load(SceneLoadMode mode = SceneLoadMode.Single)
        {
            return HasBuildIndex
                ? SceneKit.LoadScene(BuildIndex, mode)
                : SceneKit.LoadScene(SceneName, mode);
        }

        public SceneHandler LoadAsync(
            SceneLoadMode mode = SceneLoadMode.Single,
            Action<SceneHandler> onComplete = null,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f,
            ISceneData data = null)
        {
            return HasBuildIndex
                ? SceneKit.LoadSceneAsync(BuildIndex, mode, onComplete, onProgress, suspendAtProgress, data)
                : SceneKit.LoadSceneAsync(SceneName, mode, onComplete, onProgress, suspendAtProgress, data);
        }

        public override string ToString() => SceneName;
    }
}
