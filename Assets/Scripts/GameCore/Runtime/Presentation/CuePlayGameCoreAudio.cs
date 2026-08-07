using System.Collections.Generic;
using GAS.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// EX-GAS Cue 到 GameCore 音频闭包的唯一桥。
    /// 技能时间轴只配置 Cue；实际播放仍通过 AudioClipResolver 和 AudioSystem。
    /// </summary>
    public sealed class CuePlayGameCoreAudio : GameplayCueBase<XParamGameCoreAudio>
    {
        public override void OnActivate(float time)
        {
            base.OnActivate(time);

            AudioClipResolver audioClipResolver = ResolveAudioClipResolver();
            if (audioClipResolver == null)
            {
                return;
            }

            YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(audioClipResolver));
        }

        private AudioClipResolver ResolveAudioClipResolver()
        {
            if (Parameter == null || string.IsNullOrWhiteSpace(Parameter.AudioResolverGuid))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("EX-GAS Cue 已触发 GameCore 音频事件，但 CuePlayGameCoreAudio 未配置 AudioClipResolver GUID。");
#endif
                return null;
            }

            if (!GameManager.Exists())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("EX-GAS Cue 已触发 GameCore 音频事件，但当前没有运行中的 GameManager，无法解析 AudioClipResolver。");
#endif
                return null;
            }

            AudioClipResolver audioClipResolver =
                GameManager.Database.GUIDToDatabaseEntry<AudioClipResolver>(Parameter.AudioResolverGuid);
            if (audioClipResolver == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"EX-GAS Cue 已触发 GameCore 音频事件，但找不到 AudioClipResolver GUID：{Parameter.AudioResolverGuid}。");
#endif
            }

            return audioClipResolver;
        }
    }

    public sealed class XParamGameCoreAudio : XParam
    {
        [ShowInInspector]
        [LabelText("AudioClipResolver GUID")]
        [BeanField(nameof(SetAudioResolverGuid), Comment = "GameCore音频资源GUID", Order = 1)]
        public string AudioResolverGuid { get; private set; } = string.Empty;

        public void SetAudioResolverGuid(string audioResolverGuid)
        {
            AudioResolverGuid = string.IsNullOrWhiteSpace(audioResolverGuid)
                || audioResolverGuid == XParamDefault.DefaultString
                    ? string.Empty
                    : audioResolverGuid;
        }

#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData == null || paramData.Count == 0)
            {
                AudioResolverGuid = string.Empty;
                return;
            }

            SetAudioResolverGuid(paramData[0]?.ToString());
        }

        public List<object> EncodeExcelData()
        {
            return new List<object>
            {
                string.IsNullOrWhiteSpace(AudioResolverGuid) ? XParamDefault.DefaultString : AudioResolverGuid
            };
        }
#endif
    }
}
