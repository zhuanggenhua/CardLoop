using System;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace GameCore
{
    public class UISettings : UIKitMenuPanelBase
    {
        [Header("设置面板组件")]
        [LabelText("主音量控件")]
        [Tooltip("显示和调整 GameCore 主音量的控件引用。")]
        [SerializeField] private UISettingsMasterVolume m_masterVolume = null;

        [LabelText("分通道音量控件")]
        [Tooltip("显示和调整各音频通道音量的控件引用；测试面板可为空数组。")]
        [SerializeField] private UISettingsChannelVolume[] m_channelVolumes = null;

        [LabelText("关闭按钮")]
        [Tooltip("关闭设置窗口并返回上一层界面。")]
        [SerializeField]
        private Button m_closeButton = null;

        [Header("显示设置")]
        [LabelText("分辨率按钮")]
        [Tooltip("点击后按当前设备支持列表切换分辨率；实际设置由 DisplaySettingsSystem 执行。")]
        [SerializeField] private Button m_resolutionButton;

        [LabelText("分辨率文字")]
        [Tooltip("显示当前分辨率设置。")]
        [SerializeField] private TMP_Text m_resolutionLabel;

        [LabelText("全屏按钮")]
        [Tooltip("点击后在全屏窗口和窗口模式之间切换。")]
        [SerializeField] private Button m_fullscreenButton;

        [LabelText("全屏文字")]
        [Tooltip("显示当前全屏模式。")]
        [SerializeField] private TMP_Text m_fullscreenLabel;

        [LabelText("垂直同步按钮")]
        [Tooltip("点击后按关闭、开启、半帧顺序切换垂直同步。")]
        [SerializeField] private Button m_vSyncButton;

        [LabelText("垂直同步文字")]
        [Tooltip("显示当前垂直同步设置。")]
        [SerializeField] private TMP_Text m_vSyncLabel;

        [LabelText("帧率按钮")]
        [Tooltip("点击后按模板帧率上限列表切换。")]
        [SerializeField] private Button m_frameRateButton;

        [LabelText("帧率文字")]
        [Tooltip("显示当前帧率上限。")]
        [SerializeField] private TMP_Text m_frameRateLabel;

        [LabelText("阴影按钮")]
        [Tooltip("点击后按关闭、低、中、高、极高顺序切换阴影质量。")]
        [SerializeField] private Button m_shadowButton;

        [LabelText("阴影文字")]
        [Tooltip("显示当前阴影质量预设。")]
        [SerializeField] private TMP_Text m_shadowLabel;

        [LabelText("重置设置按钮")]
        [Tooltip("点击后打开确认框，只重置本面板拥有的音频和显示设置。")]
        [SerializeField] private Button m_resetSettingsButton;

        [Header("音量显示")]
        [LabelText("最大显示音量")]
        [Tooltip("把 0-1 的音量值映射到作者可读的显示上限。")]
        [SerializeField] private float m_maxVolume = 10.0f;

        [LabelText("音量后缀")]
        [Tooltip("追加到音量数字后的显示文本。")]
        [SerializeField] private string m_volumeSuffix = " / 10";

        [LabelText("音量步进")]
        [Tooltip("点击增减按钮时每次改变的 0-1 音量步长。")]
        [SerializeField] private float m_volumeStep = 0.1f;

        protected override void OnPanelInit()
        {
			if (m_masterVolume == null || m_channelVolumes == null || m_closeButton == null)
			{
				throw new InvalidOperationException("游戏设置面板预制体缺少必要 UI 引用。");
			}
            if (HasAnyDisplaySettingsControl() && !HasAllDisplaySettingsControls())
            {
                throw new InvalidOperationException("游戏设置面板的显示设置控件没有成组配置完整。");
            }
            if (HasAllDisplaySettingsControls() && !GameManager.HasSystem<DisplaySettingsSystem>())
            {
                throw new InvalidOperationException("游戏设置面板需要 DisplaySettingsSystem，但运行根没有配置该系统。");
            }

            m_masterVolume.RegisterCallbacks(OnMasterVolumeDecreased, OnMasterVolumeIncreased);
			m_closeButton.onClick.AddListener(CloseFromMenuStackOrSelf);
            RegisterDisplaySettingsCallbacks();

            foreach (UISettingsChannelVolume channelVolume in m_channelVolumes)
            {
                channelVolume.RegisterCallbacks(OnChannelVolumeDecreased, OnChannelVolumeIncreased);
            }
        }

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            UpdateUI();
        }

        private void OnDestroy()
        {
            m_masterVolume.UnregisterCallbacks();
			m_closeButton?.onClick.RemoveListener(CloseFromMenuStackOrSelf);
            UnregisterDisplaySettingsCallbacks();

            foreach (UISettingsChannelVolume channelVolume in m_channelVolumes)
            {
                channelVolume.UnregisterCallbacks();
            }
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            return m_masterVolume.GetDefaultFocusTarget();
        }

        private bool HasAnyDisplaySettingsControl()
        {
            return m_resolutionButton != null ||
                m_resolutionLabel != null ||
                m_fullscreenButton != null ||
                m_fullscreenLabel != null ||
                m_vSyncButton != null ||
                m_vSyncLabel != null ||
                m_frameRateButton != null ||
                m_frameRateLabel != null ||
                m_shadowButton != null ||
                m_shadowLabel != null ||
                m_resetSettingsButton != null;
        }

        private bool HasAllDisplaySettingsControls()
        {
            return m_resolutionButton != null &&
                m_resolutionLabel != null &&
                m_fullscreenButton != null &&
                m_fullscreenLabel != null &&
                m_vSyncButton != null &&
                m_vSyncLabel != null &&
                m_frameRateButton != null &&
                m_frameRateLabel != null &&
                m_shadowButton != null &&
                m_shadowLabel != null &&
                m_resetSettingsButton != null;
        }

        private void RegisterDisplaySettingsCallbacks()
        {
            if (!HasAllDisplaySettingsControls())
            {
                return;
            }

            m_resolutionButton.onClick.AddListener(CycleResolution);
            m_fullscreenButton.onClick.AddListener(CycleFullscreen);
            m_vSyncButton.onClick.AddListener(CycleVSync);
            m_frameRateButton.onClick.AddListener(CycleFrameRateCap);
            m_shadowButton.onClick.AddListener(CycleShadowPreset);
            m_resetSettingsButton.onClick.AddListener(ConfirmResetAllSettings);
        }

        private void UnregisterDisplaySettingsCallbacks()
        {
            m_resolutionButton?.onClick.RemoveListener(CycleResolution);
            m_fullscreenButton?.onClick.RemoveListener(CycleFullscreen);
            m_vSyncButton?.onClick.RemoveListener(CycleVSync);
            m_frameRateButton?.onClick.RemoveListener(CycleFrameRateCap);
            m_shadowButton?.onClick.RemoveListener(CycleShadowPreset);
            m_resetSettingsButton?.onClick.RemoveListener(ConfirmResetAllSettings);
        }

        private void CycleResolution()
        {
            GameManager.DisplaySettingsSystem.CycleScreenResolution();
            UpdateUI();
        }

        private void CycleFullscreen()
        {
            GameManager.DisplaySettingsSystem.CycleFullscreenMode();
            UpdateUI();
        }

        private void CycleVSync()
        {
            GameManager.DisplaySettingsSystem.CycleVSync();
            UpdateUI();
        }

        private void CycleFrameRateCap()
        {
            GameManager.DisplaySettingsSystem.CycleFrameRateCap();
            UpdateUI();
        }

        private void CycleShadowPreset()
        {
            GameManager.DisplaySettingsSystem.CycleShadowPreset();
            UpdateUI();
        }

        private void ConfirmResetAllSettings()
        {
            DialogConfig config = DialogConfig.Confirm(
                "确定重置显示与音频设置吗？这不会删除存档、Mod 配置或其它系统偏好。",
                "重置设置");
            config.OKText = "重置";
            UIKit.ShowDialog<ConfirmationDialogPanel>(
                config,
                result =>
                {
                    if (!result.IsConfirmed)
                    {
                        return;
                    }

                    GameManager.DisplaySettingsSystem.ResetSettingsToDefaults();
                    GameManager.AudioSystem.ResetSettingsToDefaults();
                    UpdateUI();
                });
        }

        private float ComputeVolumeChange(float volume, float stepScale)
        {
            float step = m_volumeStep * stepScale;
            return math.saturate(math.round((volume + step) * (1.0f / step)) * step);
        }

        private float ComputeVolumeIncrement(float volume) => ComputeVolumeChange(volume, +1.0f);

        private float ComputeVolumeDecrement(float volume) => ComputeVolumeChange(volume, -1.0f);

        private void OnMasterVolumeIncreased()
        {
            GameManager.AudioSystem.SetMasterVolume(
                ComputeVolumeIncrement(
                    GameManager.AudioSystem.GetMasterVolume()
                )
            );

            UpdateUI();
        }

        private void OnMasterVolumeDecreased()
        {
            GameManager.AudioSystem.SetMasterVolume(
                ComputeVolumeDecrement(
                    GameManager.AudioSystem.GetMasterVolume()
                )
            );

            UpdateUI();
        }

        private void OnChannelVolumeIncreased(EAudioChannel channel)
        {
            AudioSystem audioSystem = GameManager.AudioSystem;
            float targetVolumeScale = ComputeVolumeIncrement(audioSystem.GetChannelVolumeScale(channel));
            audioSystem.SetChannelVolumeScale(channel, targetVolumeScale);
            UpdateUI();
        }

        private void OnChannelVolumeDecreased(EAudioChannel channel)
        {
            AudioSystem audioSystem = GameManager.AudioSystem;
            float targetVolumeScale = ComputeVolumeDecrement(audioSystem.GetChannelVolumeScale(channel));
            audioSystem.SetChannelVolumeScale(channel, targetVolumeScale);
            UpdateUI();
        }

        private void UpdateUI()
        {
            m_masterVolume.UpdateUI((int)math.round(GameManager.AudioSystem.GetMasterVolume() * m_maxVolume), m_volumeSuffix);

            foreach (UISettingsChannelVolume channelVolume in m_channelVolumes)
            {
                float volumeScale = GameManager.AudioSystem.GetChannelVolumeScale(channelVolume.audioChannel) * m_maxVolume;
                channelVolume.UpdateUI((int)math.round(volumeScale), m_volumeSuffix);
            }

            if (HasAllDisplaySettingsControls())
            {
                DisplaySettingsSystem displaySettings = GameManager.DisplaySettingsSystem;
                m_resolutionLabel.text = displaySettings.GetResolutionLabel();
                m_fullscreenLabel.text = displaySettings.GetFullscreenLabel();
                m_vSyncLabel.text = displaySettings.GetVSyncLabel();
                m_frameRateLabel.text = displaySettings.GetFrameRateCapLabel();
                m_shadowLabel.text = displaySettings.GetShadowPresetLabel();
            }
        }
    }
}


