using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 场景拾取物通用生命周期。
    /// 参考 TopDown PickableItem，只吸收拾取条件、反馈和禁用流程；库存归属仍由 GameCore 系统裁决。
    /// </summary>
    public abstract class PickableItem : MonoBehaviour
    {
        [Header("Pickable Item")]
        [Tooltip("拾取成功时播放的表现反馈。")]
        [SerializeField] private GameplayFeedbackSet m_feedbacks = new();

        [Tooltip("拾取成功后是否禁用当前碰撞体。")]
        [SerializeField] private bool m_disableColliderOnPick = false;

        [Tooltip("拾取成功后是否禁用当前对象。")]
        [SerializeField] private bool m_disableObjectOnPick = true;

        [Min(0f)]
        [Tooltip("禁用当前对象前的延迟秒数。")]
        [SerializeField] private float m_disableDelay = 0f;

        [Tooltip("拾取成功后是否隐藏模型对象。")]
        [SerializeField] private bool m_disableModelOnPick = false;

        [Tooltip("拾取成功后要隐藏的模型对象。")]
        [SerializeField] private GameObject m_model = null;

        [Tooltip("拾取成功后是否额外禁用一个目标对象。")]
        [SerializeField] private bool m_disableTargetObjectOnPick = false;

        [Tooltip("拾取成功后要禁用的目标对象。")]
        [SerializeField] private GameObject m_targetObjectToDisable = null;

        [Min(0f)]
        [Tooltip("禁用目标对象前的延迟秒数。")]
        [SerializeField] private float m_targetObjectDisableDelay = 1f;

        [Header("Pick Conditions")]
        [Tooltip("开启后，只有带 CharacterBase 的对象才能拾取。")]
        [SerializeField] private bool m_requireCharacterComponent = true;

        [Tooltip("开启后，只有当前正式受控角色才能拾取；若当前没有受控角色，则回退到玩家存档主角色。")]
        [SerializeField] private bool m_requirePlayerType = true;

        private Collider m_collider = null;
        private Collider2D m_collider2D = null;
        private bool m_picked = false;

        protected virtual void Awake()
        {
            m_collider = GetComponent<Collider>();
            m_collider2D = GetComponent<Collider2D>();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPick(other.gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryPick(other.gameObject);
        }

        private void TryPick(GameObject picker)
        {
            if (m_picked || !TryResolvePicker(picker, out CharacterBase pickerCharacter))
            {
                return;
            }

            if (!TryPick(pickerCharacter))
            {
                return;
            }

            m_picked = true;
            m_feedbacks.PlayPickup(transform.position);
            GameRuntimeEvents.NotifyPickupPresentation(new PickupPresentationContext(transform.position, pickerCharacter, this));

            if (m_disableColliderOnPick)
            {
                if (m_collider != null)
                {
                    m_collider.enabled = false;
                }

                if (m_collider2D != null)
                {
                    m_collider2D.enabled = false;
                }
            }

            if (m_disableModelOnPick && m_model != null)
            {
                m_model.SetActive(false);
            }

            if (m_disableObjectOnPick)
            {
                if (m_disableDelay <= 0f)
                {
                    gameObject.SetActive(false);
                }
                else
                {
                    DisableSelfAfterDelayAsync(m_disableDelay, destroyCancellationToken).Forget(LogAsyncException);
                }
            }

            if (m_disableTargetObjectOnPick && m_targetObjectToDisable != null)
            {
                if (m_targetObjectDisableDelay <= 0f)
                {
                    m_targetObjectToDisable.SetActive(false);
                }
                else
                {
                    DisableTargetAfterDelayAsync(
                        m_targetObjectDisableDelay,
                        m_targetObjectToDisable,
                        destroyCancellationToken).Forget(LogAsyncException);
                }
            }
        }

        protected abstract bool TryPick(CharacterBase pickerCharacter);

        private bool TryResolvePicker(GameObject picker, out CharacterBase pickerCharacter)
        {
            pickerCharacter = picker != null ? picker.GetComponentInParent<CharacterBase>() : null;

            if (m_requireCharacterComponent && pickerCharacter == null)
            {
                return false;
            }

            if (m_requirePlayerType && pickerCharacter != GetExpectedPickerCharacter())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 当前拾取入口默认跟随正式受控角色。
        /// 拾取成功后，具体掉落类型再决定写入角色背包还是队伍钱包。
        /// </summary>
        private static CharacterBase GetExpectedPickerCharacter()
        {
            if (GameManager.Exists() && GameManager.TryGetSystem(out PlayerSystem playerSystem))
            {
                return playerSystem.GetCurrentControlledCharacterOrPlayerInstance();
            }

            return null;
        }

        private async UniTask DisableSelfAfterDelayAsync(float delay, CancellationToken cancellationToken)
        {
            await UniTask.WaitForSeconds(Mathf.Max(0f, delay), cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested || this == null)
            {
                return;
            }

            gameObject.SetActive(false);
        }

        private static async UniTask DisableTargetAfterDelayAsync(
            float delay,
            GameObject targetObject,
            CancellationToken cancellationToken)
        {
            await UniTask.WaitForSeconds(Mathf.Max(0f, delay), cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested || targetObject == null)
            {
                return;
            }

            targetObject.SetActive(false);
        }

        private void LogAsyncException(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }

            Debug.LogException(exception, this);
        }
    }
}
