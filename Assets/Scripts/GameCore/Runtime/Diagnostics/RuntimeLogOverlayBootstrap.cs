using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    [DisallowMultipleComponent]
    public class RuntimeLogOverlayBootstrap : MonoBehaviour
    {
        [Header("Enable Rules")]
        [SerializeField] private bool m_enableInEditor = false;
        [SerializeField] private bool m_enableInDevelopmentBuild = true;
        [SerializeField] private bool m_enableInReleaseBuild = false;

        [Header("Overlay Settings")]
        [SerializeField] private int m_maxLogCount = 300;
        [SerializeField] private bool m_showWindowOnStart = false;
        [SerializeField] private KeyCode m_toggleKey = KeyCode.BackQuote;
        [SerializeField] private int m_toggleTouchCount = 3;
        [SerializeField] private KitLoggerIMGUI.LogTypeFilter m_filter = KitLoggerIMGUI.LogTypeFilter.All;

        private void Awake()
        {
            if (!RuntimeLogOverlay.ShouldEnable(
                    Application.isEditor,
                    Debug.isDebugBuild,
                    m_enableInEditor,
                    m_enableInDevelopmentBuild,
                    m_enableInReleaseBuild))
            {
                return;
            }

            RuntimeLogOverlay.Enable(
                m_maxLogCount,
                m_showWindowOnStart,
                m_filter,
                m_toggleKey,
                m_toggleTouchCount);
        }
    }
}
