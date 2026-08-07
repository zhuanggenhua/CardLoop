using System;
using System.Collections;
using UnityEngine;

namespace GameCore
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioChannelFallbackPlayer : MonoBehaviour
    {
        private AudioSource m_audioSource;
        private Coroutine m_playbackCoroutine;
        private Transform m_followTarget;
        private Action<AudioChannelFallbackPlayer> m_onCompleted;
        private float m_remainingDuration;
        private bool m_isPaused;

        public void Initialize(AudioSource template, float volumeScale)
        {
            m_audioSource ??= GetComponent<AudioSource>();
            CopySettings(template, m_audioSource);
            m_audioSource.volume = volumeScale;
            gameObject.SetActive(false);
        }

        public void Play(
            AudioClip clip,
            float volumeScale,
            Vector3? position,
            Transform followTarget,
            Action<AudioChannelFallbackPlayer> onCompleted)
        {
            StopPlayback();

            m_followTarget = followTarget;
            m_onCompleted = onCompleted;
            m_isPaused = false;
            m_remainingDuration = clip != null ? clip.length : 0f;

            if (followTarget != null)
            {
                transform.position = followTarget.position;
            }
            else if (position.HasValue)
            {
                transform.position = position.Value;
            }

            m_audioSource.clip = clip;
            m_audioSource.loop = false;
            m_audioSource.volume = volumeScale;
            gameObject.SetActive(true);

            if (clip == null)
            {
                CompletePlayback();
                return;
            }

            m_audioSource.Play();
            m_playbackCoroutine = StartCoroutine(TrackPlayback());
        }

        public void SetVolumeScale(float volumeScale)
        {
            if (m_audioSource == null)
            {
                return;
            }

            m_audioSource.volume = volumeScale;
        }

        public void PausePlayback()
        {
            if (!gameObject.activeSelf || m_isPaused)
            {
                return;
            }

            m_isPaused = true;
            m_audioSource.Pause();
        }

        public void ResumePlayback()
        {
            if (!gameObject.activeSelf || !m_isPaused)
            {
                return;
            }

            m_isPaused = false;
            m_audioSource.UnPause();
        }

        public void StopPlayback()
        {
            StopPlaybackInternal(deactivate: true);
        }

        private void OnDisable()
        {
            StopPlaybackInternal(deactivate: false);
        }

        private void OnDestroy()
        {
            StopPlaybackInternal(deactivate: false);
        }

        private void StopPlaybackInternal(bool deactivate)
        {
            if (m_playbackCoroutine != null)
            {
                StopCoroutine(m_playbackCoroutine);
                m_playbackCoroutine = null;
            }

            if (m_audioSource != null)
            {
                m_audioSource.Stop();
                m_audioSource.clip = null;
            }

            m_followTarget = null;
            m_onCompleted = null;
            m_remainingDuration = 0f;
            m_isPaused = false;

            if (deactivate && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private IEnumerator TrackPlayback()
        {
            while (m_remainingDuration > 0f)
            {
                if (m_followTarget != null)
                {
                    transform.position = m_followTarget.position;
                }

                if (!m_isPaused)
                {
                    m_remainingDuration -= Time.unscaledDeltaTime;
                }

                yield return null;
            }

            CompletePlayback();
        }

        private void CompletePlayback()
        {
            Action<AudioChannelFallbackPlayer> callback = m_onCompleted;
            StopPlayback();
            callback?.Invoke(this);
        }

        private static void CopySettings(AudioSource template, AudioSource target)
        {
            if (template == null || target == null)
            {
                return;
            }

            target.outputAudioMixerGroup = template.outputAudioMixerGroup;
            target.mute = template.mute;
            target.bypassEffects = template.bypassEffects;
            target.bypassListenerEffects = template.bypassListenerEffects;
            target.bypassReverbZones = template.bypassReverbZones;
            target.playOnAwake = false;
            target.loop = false;
            target.priority = template.priority;
            target.pitch = template.pitch;
            target.panStereo = template.panStereo;
            target.spatialBlend = template.spatialBlend;
            target.reverbZoneMix = template.reverbZoneMix;
            target.dopplerLevel = template.dopplerLevel;
            target.spread = template.spread;
            target.rolloffMode = template.rolloffMode;
            target.minDistance = template.minDistance;
            target.maxDistance = template.maxDistance;
            target.ignoreListenerPause = template.ignoreListenerPause;
            target.ignoreListenerVolume = template.ignoreListenerVolume;
            target.velocityUpdateMode = template.velocityUpdateMode;
        }
    }
}
