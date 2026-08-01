using System;
using System.Collections;
using System.Collections.Generic;
using Ami.BroAudio;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public partial class AudioChannel
    {
        /// <summary>
        /// `AudioChannel` 的播放执行模块。
        /// 这里只承接 BroAudio 跟踪、独占切歌和暂停/恢复/停止编排，不替代 `AudioChannel` 自己的正式公开入口。
        /// </summary>
        private sealed class PlaybackRuntime
        {
            private readonly AudioChannel m_owner;
            private readonly List<IAudioPlayer> m_activeBroAudioPlayers = new();
            private readonly HashSet<IAudioPlayer> m_suppressedBroAudioCompletionPlayers = new();

            private Coroutine m_transitionCoroutine = null;
            private Coroutine m_completionCoroutine = null;

            public PlaybackRuntime(AudioChannel owner)
            {
                m_owner = owner;
            }

            public void SetVolumeScale(float scale)
            {
                for (int i = m_activeBroAudioPlayers.Count - 1; i >= 0; i--)
                {
                    IAudioPlayer player = m_activeBroAudioPlayers[i];
                    if (player == null)
                    {
                        m_activeBroAudioPlayers.RemoveAt(i);
                        continue;
                    }

                    player.SetVolume(scale);
                }
            }

            public void Stop()
            {
                StopTransitionCoroutines();
                StopExclusiveSource();
                m_owner.fallbackPoolRuntime.StopActivePlayers();
                StopBroAudioPlayers();
            }

            public void Pause()
            {
                if (m_owner.m_audioSource != null)
                {
                    m_owner.m_audioSource.Pause();
                }

                m_owner.fallbackPoolRuntime.PauseActivePlayers();

                for (int i = m_activeBroAudioPlayers.Count - 1; i >= 0; i--)
                {
                    IAudioPlayer player = m_activeBroAudioPlayers[i];
                    if (player == null)
                    {
                        m_activeBroAudioPlayers.RemoveAt(i);
                        continue;
                    }

                    player.Pause();
                }
            }

            public void Resume()
            {
                if (m_owner.m_audioSource != null)
                {
                    m_owner.m_audioSource.UnPause();
                }

                m_owner.fallbackPoolRuntime.ResumeActivePlayers();

                for (int i = m_activeBroAudioPlayers.Count - 1; i >= 0; i--)
                {
                    IAudioPlayer player = m_activeBroAudioPlayers[i];
                    if (player == null)
                    {
                        m_activeBroAudioPlayers.RemoveAt(i);
                        continue;
                    }

                    player.UnPause();
                }
            }

            public void PlayBroAudio(
                SoundID soundId,
                Vector3? position,
                Transform followTarget,
                Action onCompleted)
            {
                PreservePauseStateWhileStoppingIfExclusive();

                IAudioPlayer player;
                if (followTarget != null)
                {
                    player = sBroAudioPlayAttached(soundId, followTarget);
                }
                else if (position.HasValue)
                {
                    player = sBroAudioPlayAt(soundId, position.Value);
                }
                else
                {
                    player = sBroAudioPlay(soundId);
                }

                if (player == null)
                {
                    return;
                }

                player.SetVolume(m_owner.m_volumeScale);
                m_activeBroAudioPlayers.Add(player);
                player.OnEnd(_ =>
                {
                    m_activeBroAudioPlayers.Remove(player);
                    bool suppressed = m_suppressedBroAudioCompletionPlayers.Remove(player);
                    if (!suppressed)
                    {
                        onCompleted?.Invoke();
                    }
                });

                if (m_owner.m_isPaused)
                {
                    player.Pause();
                }
            }

            public void PlayExclusiveClip(AudioClip audioClip, Action onCompleted)
            {
                m_owner.fallbackPoolRuntime.StopActivePlayers();
                StopBroAudioPlayers();
                StopCompletionCoroutine();

                if (m_transitionCoroutine != null)
                {
                    m_owner.StopCoroutine(m_transitionCoroutine);
                }

                m_transitionCoroutine = m_owner.StartCoroutine(FadeOutAndIn(audioClip, onCompleted));
            }

            public void PlayFallbackClip(
                AudioClip audioClip,
                Vector3? position,
                Transform followTarget,
                Action onCompleted)
            {
                PreservePauseStateWhileStoppingIfExclusive();
                m_owner.fallbackPoolRuntime.TryPlay(
                    audioClip,
                    m_owner.m_volumeScale,
                    position,
                    followTarget,
                    onCompleted,
                    m_owner.m_isPaused);
            }

            private void PreservePauseStateWhileStoppingIfExclusive()
            {
                if (m_owner.m_audioChannelMode != EAudioChannelMode.Exclusive)
                {
                    return;
                }

                bool shouldRemainPaused = m_owner.m_isPaused;
                Stop();
                m_owner.m_isPaused = shouldRemainPaused;
            }

            private void StopBroAudioPlayers()
            {
                for (int i = m_activeBroAudioPlayers.Count - 1; i >= 0; i--)
                {
                    IAudioPlayer player = m_activeBroAudioPlayers[i];
                    if (player == null)
                    {
                        continue;
                    }

                    m_suppressedBroAudioCompletionPlayers.Add(player);
                    player.Stop();
                }

                m_activeBroAudioPlayers.Clear();
            }

            private void StopTransitionCoroutines()
            {
                if (m_transitionCoroutine != null)
                {
                    m_owner.StopCoroutine(m_transitionCoroutine);
                    m_transitionCoroutine = null;
                }

                StopCompletionCoroutine();
            }

            private void StopCompletionCoroutine()
            {
                if (m_completionCoroutine != null)
                {
                    m_owner.StopCoroutine(m_completionCoroutine);
                    m_completionCoroutine = null;
                }
            }

            private void StopExclusiveSource()
            {
                if (m_owner.m_audioSource == null)
                {
                    return;
                }

                m_owner.m_audioSource.Stop();
                m_owner.m_audioSource.clip = null;
            }

            private IEnumerator FadeOutAndIn(AudioClip newClip, Action onCompleted)
            {
                if (m_owner.m_audioSource == null)
                {
                    m_transitionCoroutine = null;
                    yield break;
                }

                // 独占通道切新音频前先触发数据加载，减少首次播放时同步解码卡住主线程的概率。
                newClip.LoadAudioData();

                while (m_owner.m_audioSource.volume > 0f)
                {
                    if (!m_owner.m_isPaused)
                    {
                        m_owner.m_audioSource.volume -= m_owner.m_volumeScale * Time.unscaledDeltaTime / Mathf.Max(0.0001f, m_owner.m_fadeOutDuration);
                    }

                    yield return null;
                }

                m_owner.m_audioSource.Stop();
                m_owner.m_audioSource.clip = newClip;
                m_owner.m_audioSource.Play();

                while (m_owner.m_audioSource.volume < m_owner.m_volumeScale)
                {
                    if (!m_owner.m_isPaused)
                    {
                        m_owner.m_audioSource.volume += m_owner.m_volumeScale * Time.unscaledDeltaTime / Mathf.Max(0.0001f, m_owner.m_fadeInDuration);
                    }

                    yield return null;
                }

                m_owner.m_audioSource.volume = m_owner.m_volumeScale;
                m_transitionCoroutine = null;

                StopCompletionCoroutine();
                m_completionCoroutine = m_owner.StartCoroutine(TrackExclusiveCompletion(newClip.length, onCompleted));
            }

            private IEnumerator TrackExclusiveCompletion(float duration, Action onCompleted)
            {
                float remainingDuration = duration;
                while (remainingDuration > 0f && m_owner.m_audioSource != null && m_owner.m_audioSource.clip != null)
                {
                    if (!m_owner.m_isPaused)
                    {
                        remainingDuration -= Time.unscaledDeltaTime;
                    }

                    yield return null;
                }

                m_completionCoroutine = null;
                onCompleted?.Invoke();
            }
        }
    }
}
