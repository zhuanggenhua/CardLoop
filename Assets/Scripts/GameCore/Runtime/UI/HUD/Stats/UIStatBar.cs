using Sirenix.OdinInspector;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace GameCore
{
    public class UIStatBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider m_slider = null;
        [SerializeField] private TextMeshProUGUI m_sliderText = null;
        [Tooltip("留空时跟随当前控制角色；只有明确指定时，才固定显示某个角色的数值。")]
        [SerializeField] private CharacterBase m_target = null;

        [Header("属性")]
        [LabelText("当前值属性")]
        [Tooltip("选择作为进度条当前值的 EX-GAS FightUnit 属性，例如 Health。")]
        [CharacterAttributeCode]
        [SerializeField] private int m_currentAttributeCode;

        [LabelText("上限属性")]
        [Tooltip("选择作为进度条上限的 EX-GAS FightUnit 属性，例如 MaxHealth。")]
        [CharacterAttributeCode]
        [SerializeField] private int m_maximumAttributeCode;

        [Header("Visual Settings")]
        [SerializeField] private bool m_shakeOnDecrease = false;
        [SerializeField] private float m_shakeAmplitude = 5.0f;
        [SerializeField] private float2 m_shakeFrequency = new(30.0f, 25.0f);
        [SerializeField] private float m_shakeDuration = 0.2f;

        private ShakeHandler? m_shakeHandler = null;
        private bool m_followCurrentControlledCharacter = false;
        private bool m_hasDisplayedBoundValue = false;
        private CharacterBase m_configuredTarget = null;
        private bool m_currentControlledCharacterListening = false;

        private void Awake()
        {
            m_followCurrentControlledCharacter = m_target == null;
            if (!m_followCurrentControlledCharacter)
            {
                m_configuredTarget = m_target;
                m_target = null;
            }
        }

        private void OnEnable()
        {
            BindInitialTargetIfReady();
        }

        private void Start()
        {
            BindInitialTargetIfReady();
        }

        private void OnDisable()
        {
            StopShake();
            StopCurrentControlledCharacterListening();
            UnbindTarget();
        }

        private void OnDestroy()
        {
            StopShake();
            StopCurrentControlledCharacterListening();
            UnbindTarget();
        }

        private void BindInitialTargetIfReady()
        {
            if (m_followCurrentControlledCharacter)
            {
                StartCurrentControlledCharacterListeningIfReady();
            }
            else
            {
                BindTarget(m_configuredTarget);
            }
        }

        private void StartCurrentControlledCharacterListeningIfReady()
        {
            if (m_currentControlledCharacterListening)
            {
                return;
            }

            if (!GameManager.Exists() || !GameManager.HasSystem<PlayerSystem>())
            {
                return;
            }

            m_currentControlledCharacterListening = true;
            GameManager.PlayerSystem.AddCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            OnCurrentControlledCharacterChanged(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance());
        }

        private void StopCurrentControlledCharacterListening()
        {
            if (!m_currentControlledCharacterListening)
            {
                return;
            }

            m_currentControlledCharacterListening = false;
            if (GameManager.Exists() && GameManager.HasSystem<PlayerSystem>())
            {
                GameManager.PlayerSystem.RemoveCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        private void OnAttributeValueChanged(CharacterAttributeValueChange change)
        {
            UpdateUI();
        }

        private void OnCurrentControlledCharacterChanged(CharacterBase character)
        {
            BindTarget(character);
        }

        private void UpdateUI()
        {
            if (m_target == null)
            {
                m_slider.minValue = 0;
                m_slider.maxValue = 0;
                m_slider.value = 0;
                m_sliderText.text = string.Empty;
                m_hasDisplayedBoundValue = false;
                return;
            }

            float current = m_target.GetAttributeCurrentValue(m_currentAttributeCode);
            float maximum = m_target.GetAttributeBaseValue(m_maximumAttributeCode);

            float previousSliderValue = m_slider.value;

            m_slider.minValue = 0;
            m_slider.maxValue = maximum;
            m_slider.value = current;

            if (m_hasDisplayedBoundValue && m_slider.value < previousSliderValue && m_shakeOnDecrease)
            {
                Shake();
            }

            m_sliderText.text = StringFormatter.Format(
                "{0}/{1}",
                current.ToString("0.##"),
                maximum.ToString("0.##"));
            m_hasDisplayedBoundValue = true;
        }

        private void Shake()
        {
            if (m_shakeHandler.HasValue)
            {
                TransformShaker.InterruptShakeIfInProgress(m_shakeHandler.Value);
                m_shakeHandler = null;
            }

            m_shakeHandler = TransformShaker.Shake(
                owner: this,
                target: m_slider.transform,
                amplitude: m_shakeAmplitude,
                frequency: m_shakeFrequency,
                duration: m_shakeDuration
            );
        }

        private void StopShake()
        {
            if (!m_shakeHandler.HasValue)
            {
                return;
            }

            TransformShaker.InterruptShakeIfInProgress(m_shakeHandler.Value);
            m_shakeHandler = null;
        }
        private void BindTarget(CharacterBase character)
        {
            if (ReferenceEquals(m_target, character))
            {
                return;
            }

            UnbindTarget();
            m_target = character;
            m_hasDisplayedBoundValue = false;

            if (m_target == null)
            {
                UpdateUI();
                return;
            }

            m_target.AddAttributeBaseValueChangedListener(OnAttributeValueChanged);
            m_target.AddAttributeCurrentValueChangedListener(OnAttributeValueChanged);
            UpdateUI();
        }

        private void UnbindTarget()
        {
            if (m_target != null)
            {
                m_target.RemoveAttributeBaseValueChangedListener(OnAttributeValueChanged);
                m_target.RemoveAttributeCurrentValueChangedListener(OnAttributeValueChanged);
            }

            m_target = null;
        }
    }
}



