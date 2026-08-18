using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameCore
{
    /// <summary>
    /// 进程级显示设置系统，负责分辨率、全屏、垂直同步、帧率上限和阴影预设。
    /// 它接管 StackCraft 模板的 GraphicsManager 玩家效果，但不保留模板单例或 PlayerPrefs 键。
    /// </summary>
    public sealed class DisplaySettingsSystem : AGameSystem
    {
        private enum ShadowPreset
        {
            Off,
            Low,
            Medium,
            High,
            Ultra
        }

        private const int DefaultTargetWidth = 1920;
        private const int DefaultTargetHeight = 1080;
        private const FullScreenMode DefaultFullscreenMode = FullScreenMode.FullScreenWindow;
        private const int DefaultVSync = 1;
        private const int DefaultFrameRateCap = -1;
        private const ShadowPreset DefaultShadowPreset = ShadowPreset.High;

        private const string PlayerPrefsPrefix = "GameCore_DisplaySettings_";
        private const string ScreenWidthKey = PlayerPrefsPrefix + "ScreenWidth";
        private const string ScreenHeightKey = PlayerPrefsPrefix + "ScreenHeight";
        private const string FullscreenModeKey = PlayerPrefsPrefix + "FullscreenMode";
        private const string VSyncKey = PlayerPrefsPrefix + "VSyncMode";
        private const string FrameRateCapKey = PlayerPrefsPrefix + "FrameRateCap";
        private const string ShadowKey = PlayerPrefsPrefix + "ShadowPreset";

        private static readonly FullScreenMode[] FullscreenModes =
        {
            FullScreenMode.FullScreenWindow,
            FullScreenMode.Windowed
        };

        private static readonly int[] FrameRateCaps =
        {
            -1,
            30,
            60,
            120,
            144,
            240
        };

        [LabelText("同步未缩放时间到 Shader")]
        [Tooltip("开启后每帧写入全局 _UnscaledTime，承接模板卡牌高亮等暂停时仍需动画的 Shader 语义。")]
        [SerializeField]
        private bool m_syncUnscaledTimeToShader = true;

        private readonly List<Resolution> m_resolutions = new();
        private Resolution m_currentResolution;
        private FullScreenMode m_currentMode = DefaultFullscreenMode;
        private int m_currentVSync = DefaultVSync;
        private int m_currentFrameRateCap = DefaultFrameRateCap;
        private ShadowPreset m_currentShadowPreset = DefaultShadowPreset;

        public override void OnSystemStart()
        {
            RefreshResolutionOptions();
            ApplySavedSettings();
        }

        private void Update()
        {
            if (m_syncUnscaledTimeToShader)
            {
                Shader.SetGlobalFloat("_UnscaledTime", Time.unscaledTime);
            }
        }

        /// <summary>切换到下一个可用屏幕分辨率，并立即保存。</summary>
        public Resolution CycleScreenResolution()
        {
            RequireResolutionOptions();
            int index = m_resolutions.FindIndex(
                resolution => resolution.width == m_currentResolution.width &&
                    resolution.height == m_currentResolution.height);
            if (index < 0)
            {
                index = 0;
            }

            int next = (index + 1) % m_resolutions.Count;
            m_currentResolution = m_resolutions[next];
            ApplyScreenResolution();
            SaveSettings();
            return m_currentResolution;
        }

        /// <summary>切换全屏 / 窗口模式，并立即保存。</summary>
        public void CycleFullscreenMode()
        {
            int index = Array.IndexOf(FullscreenModes, m_currentMode);
            if (index < 0)
            {
                index = 0;
            }

            m_currentMode = FullscreenModes[(index + 1) % FullscreenModes.Length];
            ApplyScreenResolution();
            SaveSettings();
        }

        /// <summary>按模板顺序切换垂直同步：关、开、半帧。</summary>
        public void CycleVSync()
        {
            m_currentVSync = (m_currentVSync + 1) % 3;
            ApplyPerformanceSettings();
            SaveSettings();
        }

        /// <summary>按模板顺序切换帧率上限。</summary>
        public void CycleFrameRateCap()
        {
            int index = Array.IndexOf(FrameRateCaps, m_currentFrameRateCap);
            if (index < 0)
            {
                index = 0;
            }

            m_currentFrameRateCap = FrameRateCaps[(index + 1) % FrameRateCaps.Length];
            ApplyPerformanceSettings();
            SaveSettings();
        }

        /// <summary>按模板顺序切换阴影质量预设。</summary>
        public void CycleShadowPreset()
        {
            int count = Enum.GetValues(typeof(ShadowPreset)).Length;
            m_currentShadowPreset = (ShadowPreset)(((int)m_currentShadowPreset + 1) % count);
            ApplyShadowPreset(m_currentShadowPreset);
            SaveSettings();
        }

        /// <summary>只清除显示设置系统拥有的偏好键，并恢复默认显示设置。</summary>
        public void ResetSettingsToDefaults()
        {
            PlayerPrefs.DeleteKey(ScreenWidthKey);
            PlayerPrefs.DeleteKey(ScreenHeightKey);
            PlayerPrefs.DeleteKey(FullscreenModeKey);
            PlayerPrefs.DeleteKey(VSyncKey);
            PlayerPrefs.DeleteKey(FrameRateCapKey);
            PlayerPrefs.DeleteKey(ShadowKey);
            ApplyDefaultSettings();
            SaveSettings();
        }

        public string GetResolutionLabel()
        {
            return $"分辨率 {m_currentResolution.width}x{m_currentResolution.height}";
        }

        public string GetFullscreenLabel()
        {
            return m_currentMode == FullScreenMode.Windowed ? "全屏：窗口" : "全屏：开启";
        }

        public string GetVSyncLabel()
        {
            return m_currentVSync switch
            {
                0 => "垂直同步：关闭",
                1 => "垂直同步：开启",
                2 => "垂直同步：半帧",
                _ => "垂直同步：未知"
            };
        }

        public string GetFrameRateCapLabel()
        {
            return m_currentFrameRateCap < 0 ? "帧率：无限制" : $"帧率：{m_currentFrameRateCap}";
        }

        public string GetShadowPresetLabel()
        {
            return m_currentShadowPreset switch
            {
                ShadowPreset.Off => "阴影：关闭",
                ShadowPreset.Low => "阴影：低",
                ShadowPreset.Medium => "阴影：中",
                ShadowPreset.High => "阴影：高",
                ShadowPreset.Ultra => "阴影：极高",
                _ => "阴影：未知"
            };
        }

        private void ApplySavedSettings()
        {
            m_currentMode = ReadFullscreenMode();
            m_currentResolution = ReadResolution();
            m_currentVSync = Mathf.Clamp(PlayerPrefs.GetInt(VSyncKey, DefaultVSync), 0, 2);
            m_currentFrameRateCap = ReadFrameRateCap();
            m_currentShadowPreset = ReadShadowPreset();

            ApplyScreenResolution();
            ApplyPerformanceSettings();
            ApplyShadowPreset(m_currentShadowPreset);
        }

        private void ApplyDefaultSettings()
        {
            RefreshResolutionOptions();
            m_currentMode = DefaultFullscreenMode;
            m_currentResolution = FindClosestResolution(DefaultTargetWidth, DefaultTargetHeight);
            m_currentVSync = DefaultVSync;
            m_currentFrameRateCap = DefaultFrameRateCap;
            m_currentShadowPreset = DefaultShadowPreset;

            ApplyScreenResolution();
            ApplyPerformanceSettings();
            ApplyShadowPreset(m_currentShadowPreset);
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetInt(ScreenWidthKey, m_currentResolution.width);
            PlayerPrefs.SetInt(ScreenHeightKey, m_currentResolution.height);
            PlayerPrefs.SetInt(FullscreenModeKey, (int)m_currentMode);
            PlayerPrefs.SetInt(VSyncKey, m_currentVSync);
            PlayerPrefs.SetInt(FrameRateCapKey, m_currentFrameRateCap);
            PlayerPrefs.SetInt(ShadowKey, (int)m_currentShadowPreset);
            PlayerPrefs.Save();
        }

        private FullScreenMode ReadFullscreenMode()
        {
            FullScreenMode mode = (FullScreenMode)PlayerPrefs.GetInt(
                FullscreenModeKey,
                (int)DefaultFullscreenMode);
            return Array.IndexOf(FullscreenModes, mode) >= 0 ? mode : DefaultFullscreenMode;
        }

        private Resolution ReadResolution()
        {
            if (PlayerPrefs.HasKey(ScreenWidthKey) && PlayerPrefs.HasKey(ScreenHeightKey))
            {
                int width = PlayerPrefs.GetInt(ScreenWidthKey);
                int height = PlayerPrefs.GetInt(ScreenHeightKey);
                int index = m_resolutions.FindIndex(
                    resolution => resolution.width == width && resolution.height == height);
                if (index >= 0)
                {
                    return m_resolutions[index];
                }
            }

            return FindClosestResolution(DefaultTargetWidth, DefaultTargetHeight);
        }

        private int ReadFrameRateCap()
        {
            int frameRateCap = PlayerPrefs.GetInt(FrameRateCapKey, DefaultFrameRateCap);
            return Array.IndexOf(FrameRateCaps, frameRateCap) >= 0
                ? frameRateCap
                : DefaultFrameRateCap;
        }

        private ShadowPreset ReadShadowPreset()
        {
            ShadowPreset preset = (ShadowPreset)PlayerPrefs.GetInt(
                ShadowKey,
                (int)DefaultShadowPreset);
            return Enum.IsDefined(typeof(ShadowPreset), preset) ? preset : DefaultShadowPreset;
        }

        private void RefreshResolutionOptions()
        {
            m_resolutions.Clear();
            Resolution[] hardwareResolutions = Screen.resolutions;
            for (int i = 0; i < hardwareResolutions.Length; i++)
            {
                Resolution resolution = hardwareResolutions[i];
                if (m_resolutions.Exists(existing =>
                        existing.width == resolution.width &&
                        existing.height == resolution.height))
                {
                    continue;
                }

                m_resolutions.Add(resolution);
            }

            m_resolutions.Sort((left, right) =>
            {
                int widthComparison = left.width.CompareTo(right.width);
                return widthComparison != 0 ? widthComparison : left.height.CompareTo(right.height);
            });

            if (m_resolutions.Count == 0)
            {
                Resolution fallback = default;
                fallback.width = Mathf.Max(1, Screen.currentResolution.width);
                fallback.height = Mathf.Max(1, Screen.currentResolution.height);
                m_resolutions.Add(fallback);
            }
        }

        private Resolution FindClosestResolution(int targetWidth, int targetHeight)
        {
            RequireResolutionOptions();
            Resolution best = m_resolutions[0];
            long bestScore = long.MaxValue;
            for (int i = 0; i < m_resolutions.Count; i++)
            {
                Resolution resolution = m_resolutions[i];
                long dx = resolution.width - targetWidth;
                long dy = resolution.height - targetHeight;
                long score = dx * dx + dy * dy;
                if (score < bestScore)
                {
                    best = resolution;
                    bestScore = score;
                }
            }

            return best;
        }

        private void RequireResolutionOptions()
        {
            if (m_resolutions.Count == 0)
            {
                throw new InvalidOperationException("显示设置系统没有可用分辨率选项。");
            }
        }

        private void ApplyScreenResolution()
        {
            RequireResolutionOptions();
            Screen.fullScreenMode = m_currentMode;
            Screen.SetResolution(m_currentResolution.width, m_currentResolution.height, m_currentMode);
        }

        private void ApplyPerformanceSettings()
        {
            QualitySettings.vSyncCount = Mathf.Clamp(m_currentVSync, 0, 2);
            Application.targetFrameRate = m_currentFrameRateCap;
        }

        private static void ApplyShadowPreset(ShadowPreset preset)
        {
            ApplyBuiltInShadowPreset(preset);
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline ??
                GraphicsSettings.defaultRenderPipeline;
            if (asset != null)
            {
                ApplyRenderPipelineShadowPreset(asset, preset);
            }
        }

        private static void ApplyBuiltInShadowPreset(ShadowPreset preset)
        {
            switch (preset)
            {
                case ShadowPreset.Off:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    break;
                case ShadowPreset.Low:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    break;
                case ShadowPreset.Medium:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    break;
                case ShadowPreset.High:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    break;
                case ShadowPreset.Ultra:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知阴影预设。");
            }
        }

        private static void ApplyRenderPipelineShadowPreset(RenderPipelineAsset asset, ShadowPreset preset)
        {
            int resolution = 2048;
            float distance = 30f;
            int cascades = 3;

            switch (preset)
            {
                case ShadowPreset.Off:
                    distance = 0f;
                    break;
                case ShadowPreset.Low:
                    resolution = 512;
                    distance = 10f;
                    cascades = 1;
                    break;
                case ShadowPreset.Medium:
                    resolution = 1024;
                    distance = 20f;
                    cascades = 2;
                    break;
                case ShadowPreset.High:
                    resolution = 2048;
                    distance = 30f;
                    cascades = 3;
                    break;
                case ShadowPreset.Ultra:
                    resolution = 4096;
                    distance = 40f;
                    cascades = 4;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知阴影预设。");
            }

            SetPropertyIfPresent(asset, "mainLightShadowmapResolution", resolution);
            SetPropertyIfPresent(asset, "shadowDistance", distance);
            SetPropertyIfPresent(asset, "shadowCascadeCount", cascades);
        }

        private static void SetPropertyIfPresent<TValue>(
            RenderPipelineAsset asset,
            string propertyName,
            TValue value)
        {
            PropertyInfo property = asset.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(asset, value);
            }
        }
    }
}
