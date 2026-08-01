using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public partial class AudioChannel
    {
        /// <summary>
        /// `AudioChannel` 的 Unity fallback 播放器池。
        /// 这里只管理创建、预热、租还和活动播放器状态，不承担通道级播放裁决。
        /// </summary>
        private sealed class FallbackPoolRuntime
        {
            private readonly AudioChannel m_owner;
            private Transform m_poolRoot = null;
            private readonly Queue<AudioChannelFallbackPlayer> m_inactivePlayers = new();
            private readonly List<AudioChannelFallbackPlayer> m_activePlayers = new();
            private readonly List<AudioChannelFallbackPlayer> m_ownedPlayers = new();

            public FallbackPoolRuntime(AudioChannel owner)
            {
                m_owner = owner;
            }

            public void Initialize()
            {
                EnsurePoolRoot();
                PrewarmPlayers();
            }

            public void Dispose()
            {
                StopActivePlayers();

                foreach (AudioChannelFallbackPlayer player in m_ownedPlayers)
                {
                    if (player != null)
                    {
                        m_owner.DestroyOwnedObject(player.gameObject);
                    }
                }

                m_inactivePlayers.Clear();
                m_activePlayers.Clear();
                m_ownedPlayers.Clear();

                if (m_poolRoot != null)
                {
                    m_owner.DestroyOwnedObject(m_poolRoot.gameObject);
                    m_poolRoot = null;
                }
            }

            public void SetVolumeScale(float scale)
            {
                for (int i = m_activePlayers.Count - 1; i >= 0; i--)
                {
                    AudioChannelFallbackPlayer player = m_activePlayers[i];
                    if (player == null)
                    {
                        m_activePlayers.RemoveAt(i);
                        continue;
                    }

                    player.SetVolumeScale(scale);
                }
            }

            public void PauseActivePlayers()
            {
                for (int i = m_activePlayers.Count - 1; i >= 0; i--)
                {
                    AudioChannelFallbackPlayer player = m_activePlayers[i];
                    if (player == null)
                    {
                        m_activePlayers.RemoveAt(i);
                        continue;
                    }

                    player.PausePlayback();
                }
            }

            public void ResumeActivePlayers()
            {
                for (int i = m_activePlayers.Count - 1; i >= 0; i--)
                {
                    AudioChannelFallbackPlayer player = m_activePlayers[i];
                    if (player == null)
                    {
                        m_activePlayers.RemoveAt(i);
                        continue;
                    }

                    player.ResumePlayback();
                }
            }

            public void StopActivePlayers()
            {
                for (int i = m_activePlayers.Count - 1; i >= 0; i--)
                {
                    RecyclePlayer(m_activePlayers[i], true);
                }

                m_activePlayers.Clear();
            }

            public bool TryPlay(
                AudioClip clip,
                float volumeScale,
                Vector3? position,
                Transform followTarget,
                Action onCompleted,
                bool startPaused)
            {
                AudioChannelFallbackPlayer player = RentPlayer();
                if (player == null)
                {
                    return false;
                }

                m_activePlayers.Add(player);
                player.Play(
                    clip,
                    volumeScale,
                    position,
                    followTarget,
                    completedPlayer =>
                    {
                        RecyclePlayer(completedPlayer, false);
                        onCompleted?.Invoke();
                    });

                if (startPaused)
                {
                    player.PausePlayback();
                }

                return true;
            }

            private AudioChannelFallbackPlayer RentPlayer()
            {
                while (m_inactivePlayers.Count > 0)
                {
                    AudioChannelFallbackPlayer pooledPlayer = m_inactivePlayers.Dequeue();
                    if (pooledPlayer != null)
                    {
                        pooledPlayer.gameObject.SetActive(true);
                        return pooledPlayer;
                    }
                }

                if (m_owner.m_multipleModeMaxPlayers >= 0 && m_ownedPlayers.Count >= m_owner.m_multipleModeMaxPlayers)
                {
                    return null;
                }

                return CreatePlayer();
            }

            private AudioChannelFallbackPlayer CreatePlayer()
            {
                EnsurePoolRoot();

                var fallbackObject = new GameObject($"[{m_owner.name}] AudioPlayer");
                fallbackObject.transform.SetParent(m_poolRoot, false);

                var fallbackPlayer = fallbackObject.AddComponent<AudioChannelFallbackPlayer>();
                fallbackPlayer.Initialize(m_owner.m_audioSource, m_owner.m_volumeScale);
                m_ownedPlayers.Add(fallbackPlayer);
                fallbackObject.SetActive(true);
                return fallbackPlayer;
            }

            private void RecyclePlayer(AudioChannelFallbackPlayer player, bool stopPlayback)
            {
                if (player == null)
                {
                    return;
                }

                if (stopPlayback)
                {
                    player.StopPlayback();
                }

                m_activePlayers.Remove(player);
                player.transform.SetParent(m_poolRoot, false);
                m_inactivePlayers.Enqueue(player);
            }

            private void EnsurePoolRoot()
            {
                if (m_poolRoot != null)
                {
                    return;
                }

                var poolRoot = new GameObject("[AudioChannel Pool]");
                poolRoot.transform.SetParent(m_owner.transform, false);
                m_poolRoot = poolRoot.transform;
            }

            private void PrewarmPlayers()
            {
                if (m_owner.m_multipleModePrewarmCount <= 0)
                {
                    return;
                }

                int targetCount = m_owner.m_multipleModePrewarmCount;
                if (m_owner.m_multipleModeMaxPlayers >= 0)
                {
                    targetCount = Mathf.Min(targetCount, m_owner.m_multipleModeMaxPlayers);
                }

                while (m_ownedPlayers.Count < targetCount)
                {
                    AudioChannelFallbackPlayer player = CreatePlayer();
                    if (player == null)
                    {
                        break;
                    }

                    RecyclePlayer(player, true);
                }
            }
        }
    }
}
