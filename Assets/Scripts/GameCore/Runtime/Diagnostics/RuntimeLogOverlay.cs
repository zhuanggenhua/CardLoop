using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    public static class RuntimeLogOverlay
    {
        public static bool IsEnabled => KitLoggerIMGUI.Instance != null;

        public static KitLoggerIMGUI Enable(
            int maxLogCount = 300,
            bool showWindow = true,
            KitLoggerIMGUI.LogTypeFilter filter = KitLoggerIMGUI.LogTypeFilter.All,
            KeyCode toggleKey = KeyCode.BackQuote,
            int toggleTouchCount = 3)
        {
            KitLoggerIMGUI overlay = KitLogger.EnableIMGUI(maxLogCount);
            overlay.ShowWindow = showWindow;
            overlay.Filter = filter;
            overlay.ToggleKey = toggleKey;
            overlay.ToggleTouchCount = toggleTouchCount;
            return overlay;
        }

        public static void Disable()
        {
            KitLogger.DisableIMGUI();
        }

        public static bool ShouldEnable(bool isEditor, bool isDebugBuild, bool enableInEditor, bool enableInDevelopmentBuild, bool enableInReleaseBuild)
        {
            if (isEditor)
            {
                return enableInEditor;
            }

            return isDebugBuild ? enableInDevelopmentBuild : enableInReleaseBuild;
        }
    }
}
