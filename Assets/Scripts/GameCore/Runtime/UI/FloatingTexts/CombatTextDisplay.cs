using UnityEngine;

using YokiFrame;
namespace GameCore
{
    [RequireComponent(typeof(FloatingTextPool))]
    public class CombatTextDisplay : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private bool m_showDamages = true;
        [SerializeField] private bool m_showHeals = true;
        [SerializeField] private bool m_showNullHeals = true;

        [Header("Mana Settings")]
        [SerializeField] private bool m_showConsumedMana = false;
        [SerializeField] private bool m_showRecoveredMana = true;
        [SerializeField] private bool m_showNullManaConsumption = false;
        [SerializeField] private bool m_showNullManaRecovery = true;

        [Header("Text Colors")]
        [SerializeField] private Color m_damageColor = Color.white;
        [SerializeField] private Color m_silentDamageColor = Color.red;
        [SerializeField] private Color m_missDamageColor = Color.white;
        [SerializeField] private Color m_criticalDamageColor = Color.yellow;
        [SerializeField] private Color m_healColor = Color.green;
        [SerializeField] private Color m_manaConsumedColor = Color.cyan;
        [SerializeField] private Color m_manaRecoveredColor = Color.cyan;

        [Header("Text Content")]
        [SerializeField] private string m_missText = "Miss";

        [Header("Animation Parameters")]
        [SerializeField] private string m_textAnimationParameter = "bounce";

        private FloatingTextPool m_floatingTextPool = null;

        private void Awake()
        {
            m_floatingTextPool = GetComponent<FloatingTextPool>();
        }

        /// <summary>
        /// 浮字现在统一消费正式表现上下文。
        /// 纯表现层只关心“该怎么显示”，不再回读任何旧通知中心。
        /// </summary>
        private void OnEnable()
        {
            EventKit.Type.Register<DamageTakenPresentationEvent>(OnDamageTakenPresentation);
            EventKit.Type.Register<HealthRecoveredPresentationEvent>(OnHealthRecoveredPresentation);
            EventKit.Type.Register<ManaConsumedPresentationEvent>(OnManaConsumedPresentation);
            EventKit.Type.Register<ManaRecoveredPresentationEvent>(OnManaRecoveredPresentation);
        }

        private void OnDisable()
        {
            EventKit.Type.UnRegister<DamageTakenPresentationEvent>(OnDamageTakenPresentation);
            EventKit.Type.UnRegister<HealthRecoveredPresentationEvent>(OnHealthRecoveredPresentation);
            EventKit.Type.UnRegister<ManaConsumedPresentationEvent>(OnManaConsumedPresentation);
            EventKit.Type.UnRegister<ManaRecoveredPresentationEvent>(OnManaRecoveredPresentation);
        }

        private void OnDamageTakenPresentation(DamageTakenPresentationEvent presentationEvent)
        {
            DamageTakenFeedbackContext context = presentationEvent.Context;

            if (m_showDamages && !context.visualFlags.HasFlag(EEffectVisualFlags.NoFloatingText))
            {
                string text = context.damageInput.IsMissed ? m_missText : context.damageInput.damage.ToString();

                Color color =
                    context.damageInput.IsMissed ? m_missDamageColor :
                    context.damageInput.IsCriticalHit ? m_criticalDamageColor :
                    (context.damageInput.IsSilentAppliedHit ? m_silentDamageColor : m_damageColor);

                m_floatingTextPool.ShowText(text, context.target.transform.position, color, m_textAnimationParameter);
            }
        }

        private void OnHealthRecoveredPresentation(HealthRecoveredPresentationEvent presentationEvent)
        {
            CharacterValuePresentationContext context = presentationEvent.Context;

            if (m_showHeals && !context.visualFlags.HasFlag(EEffectVisualFlags.NoFloatingText) && (m_showNullHeals || context.value > 0))
            {
                m_floatingTextPool.ShowText(context.value.ToString(), context.position, m_healColor, m_textAnimationParameter);
            }
        }

        private void OnManaConsumedPresentation(ManaConsumedPresentationEvent presentationEvent)
        {
            CharacterValuePresentationContext context = presentationEvent.Context;

            if (m_showConsumedMana && !context.visualFlags.HasFlag(EEffectVisualFlags.NoFloatingText) && (m_showNullManaConsumption || context.value > 0))
            {
                m_floatingTextPool.ShowText(context.value.ToString(), context.position, m_manaConsumedColor, m_textAnimationParameter);
            }
        }

        private void OnManaRecoveredPresentation(ManaRecoveredPresentationEvent presentationEvent)
        {
            CharacterValuePresentationContext context = presentationEvent.Context;

            if (m_showRecoveredMana && !context.visualFlags.HasFlag(EEffectVisualFlags.NoFloatingText) && (m_showNullManaRecovery || context.value > 0))
            {
                m_floatingTextPool.ShowText(context.value.ToString(), context.position, m_manaRecoveredColor, m_textAnimationParameter);
            }
        }

    }
}

