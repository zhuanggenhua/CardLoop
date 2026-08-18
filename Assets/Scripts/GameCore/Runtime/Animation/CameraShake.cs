using Unity.Mathematics;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// 镜头震动表现入口。
    /// 消费 GameCore 正式表现事件，不直接监听伤害规则或战斗管理器来反推业务语义。
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Header("镜头震动设置")]
        [Tooltip("普通命中时的震动强度。")]
        [SerializeField] private float m_amplitude = 0.05f;

        [Tooltip("震动在本地 X/Y 方向上的频率。")]
        [SerializeField] private float2 m_frequency = new(60.0f, 50.0f);

        [Tooltip("单次震动持续秒数。")]
        [SerializeField] private float m_duration = 0.2f;

        [Tooltip("暴击时在普通震动强度上的倍率。")]
        [SerializeField] private float m_criticalHitAmplitudeModifier = 2.0f;

        private ShakeHandler? m_shakeHandler = null;

        private void OnEnable()
        {
            EventKit.Type.Register<DamageTakenPresentationEvent>(OnDamageTakenPresentation);
            EventKit.Type.Register<AbilitySystemDamageResolvedPresentationEvent>(
                OnAbilitySystemDamageResolvedPresentation);
        }

        private void OnDisable()
        {
            EventKit.Type.UnRegister<DamageTakenPresentationEvent>(OnDamageTakenPresentation);
            EventKit.Type.UnRegister<AbilitySystemDamageResolvedPresentationEvent>(
                OnAbilitySystemDamageResolvedPresentation);
            StopActiveShake();
        }

        private bool TryGetCameraShakeSources(out ECameraShakeSources cameraShakeSources)
        {
            cameraShakeSources = ECameraShakeSources.None;
            if (!GameManager.Exists() || GameManager.Config == null)
            {
                return false;
            }

            cameraShakeSources = GameManager.Config.cameraShakeSources;
            return cameraShakeSources != ECameraShakeSources.None;
        }

        private static bool TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter)
        {
            currentControlledCharacter = null;
            if (!GameManager.Exists() || !GameManager.TryGetSystem(out PlayerSystem playerSystem))
            {
                return false;
            }

            currentControlledCharacter = playerSystem.GetCurrentControlledCharacterOrPlayerInstance();
            return currentControlledCharacter != null;
        }

        private bool IsValidShakeSource(
            DamageTakenFeedbackContext context,
            ECameraShakeSources cameraShakeSources)
        {
            if (!TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter))
            {
                return false;
            }

            return !context.damageInput.silent && (
                (
                    cameraShakeSources.HasFlag(ECameraShakeSources.PlayerReceiveDamage) &&
                    context.target == currentControlledCharacter
                )
                ||
                (
                    cameraShakeSources.HasFlag(ECameraShakeSources.AnyCharacterReceiveDamageFromPlayer) &&
                    context.sourceCharacter == currentControlledCharacter
                ));
        }

        private void StopActiveShake()
        {
            if (!m_shakeHandler.HasValue)
            {
                return;
            }

            TransformShaker.InterruptShakeIfInProgress(m_shakeHandler.Value);
            m_shakeHandler = null;
        }

        private void StartShake(bool isCriticalHit)
        {
            if (m_shakeHandler.HasValue)
            {
                StopActiveShake();
            }

            float amplitude = isCriticalHit ? m_amplitude * m_criticalHitAmplitudeModifier : m_amplitude;
            m_shakeHandler = TransformShaker.Shake(this, transform, amplitude, m_frequency, m_duration);
        }

        private void OnDamageTakenPresentation(DamageTakenPresentationEvent presentationEvent)
        {
            DamageTakenFeedbackContext context = presentationEvent.Context;

            if (TryGetCameraShakeSources(out ECameraShakeSources cameraShakeSources) &&
                IsValidShakeSource(context, cameraShakeSources) &&
                !context.visualFlags.HasFlag(EEffectVisualFlags.NoCameraShake))
            {
                if (!context.damageInput.IsMissed)
                {
                    StartShake(context.damageInput.IsCriticalHit);
                }
            }
        }

        private void OnAbilitySystemDamageResolvedPresentation(
            AbilitySystemDamageResolvedPresentationEvent presentationEvent)
        {
            if (!TryGetCameraShakeSources(out ECameraShakeSources cameraShakeSources) ||
                !cameraShakeSources.HasFlag(ECameraShakeSources.AbilitySystemDamageResolved) ||
                presentationEvent.IsMissed ||
                presentationEvent.IsSilent ||
                presentationEvent.VisualFlags.HasFlag(EEffectVisualFlags.NoCameraShake))
            {
                return;
            }

            StartShake(presentationEvent.IsCriticalHit);
        }
    }
}



