using UnityEngine;

namespace GameCore
{
    public class AudioRegion : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] protected AudioClipResolver m_audioClipResolver = null;

        private AudioClipResolver m_previousAudio = null;

        public bool IsPlayer(Collider2D collision)
        {
            if (collision == null || !TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter))
            {
                return false;
            }

            if (currentControlledCharacter && currentControlledCharacter.gameObject)
            {
                return collision.gameObject == currentControlledCharacter.gameObject;
            }

            return false;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!IsPlayer(collision) ||
                !TryGetAudioClipResolver(out AudioClipResolver audioClipResolver) ||
                !TryGetAudioSystem(out AudioSystem audioSystem))
            {
                return;
            }

            m_previousAudio = audioSystem.GetLastPlayedAudioClipResolver(audioClipResolver.targetChannel);
            YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(audioClipResolver));
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!IsPlayer(collision))
            {
                return;
            }

            if (m_previousAudio)
            {
                YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(m_previousAudio));
            }

            m_previousAudio = null;
        }

        private static bool TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter)
        {
            currentControlledCharacter = null;
            if (!GameManager.Exists() || !GameManager.TryGetSystem(out PlayerSystem playerSystem))
            {
                return false;
            }

            currentControlledCharacter = playerSystem.GetCurrentControlledCharacterOrPlayerInstance();
            return currentControlledCharacter != null && currentControlledCharacter.gameObject != null;
        }

        private bool TryGetAudioSystem(out AudioSystem audioSystem)
        {
            audioSystem = null;
            if (GameManager.Exists() && GameManager.TryGetSystem(out audioSystem))
            {
                return true;
            }

            Debug.LogError($"[{nameof(AudioRegion)}] 区域音频需要 AudioSystem 才能记录并切换当前通道音频。", this);
            return false;
        }

        private bool TryGetAudioClipResolver(out AudioClipResolver audioClipResolver)
        {
            audioClipResolver = m_audioClipResolver;
            if (audioClipResolver)
            {
                return true;
            }

            Debug.LogError($"[{nameof(AudioRegion)}] 区域音频缺少 AudioClipResolver，无法进入或恢复区域音频。", this);
            return false;
        }
    }
}
