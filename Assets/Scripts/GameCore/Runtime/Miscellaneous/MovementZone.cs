using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 区域速度倍率触发器。
    /// 只吸收 TopDown MovementZone 的“进入区域时施加动作层速度倍率、离开时恢复”合同，
    /// 不引入 ButtonActivated 状态机、提示 UI 或第二套角色能力生命周期。
    /// </summary>
    public sealed class MovementZone : MonoBehaviour
    {
        [Header("Requirements")]
        [Tooltip("可触发该区域的层。")]
        [SerializeField] private LayerMask m_targetLayerMask = ~0;

        [Tooltip("开启后，只有当前控制角色会受该区域影响；若尚未切控制对象，则回退到玩家主角色。")]
        [SerializeField] private bool m_requirePlayerType = true;

        [Header("Movement Zone")]
        [Tooltip("进入该区域后施加到动作执行层的速度倍率。")]
        [SerializeField] private float m_movementSpeedMultiplier = 0.5f;

        private readonly Dictionary<Movable, int> m_collidingMovables = new();

        private void OnDisable()
        {
            ClearAppliedMovables();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryEnter(other != null ? other.gameObject : null);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            TryExit(other != null ? other.gameObject : null);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryEnter(other != null ? other.gameObject : null);
        }

        private void OnTriggerExit(Collider other)
        {
            TryExit(other != null ? other.gameObject : null);
        }

        private void TryEnter(GameObject target)
        {
            if (!TryResolveMovable(target, out Movable movable))
            {
                return;
            }

            if (m_collidingMovables.TryGetValue(movable, out int overlapCount))
            {
                m_collidingMovables[movable] = overlapCount + 1;
                return;
            }

            movable.SetContextSpeedMultiplier(m_movementSpeedMultiplier);
            m_collidingMovables.Add(movable, 1);
        }

        private void TryExit(GameObject target)
        {
            if (!TryResolveMovable(target, out Movable movable))
            {
                return;
            }

            if (!m_collidingMovables.TryGetValue(movable, out int overlapCount))
            {
                return;
            }

            if (overlapCount > 1)
            {
                m_collidingMovables[movable] = overlapCount - 1;
                return;
            }

            movable.ResetContextSpeedMultiplier();
            m_collidingMovables.Remove(movable);
        }

        private bool TryResolveMovable(GameObject target, out Movable movable)
        {
            movable = null;

            if (target == null || (m_targetLayerMask.value & (1 << target.layer)) == 0)
            {
                return false;
            }

            movable = target.GetComponentInParent<Movable>();
            if (movable == null)
            {
                return false;
            }

            if (m_requirePlayerType)
            {
                if (!TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter) ||
                    movable != currentControlledCharacter)
                {
                    return false;
                }
            }

            return true;
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

        private void ClearAppliedMovables()
        {
            foreach (var entry in m_collidingMovables)
            {
                Movable movable = entry.Key;

                if (movable == null)
                {
                    continue;
                }

                movable.ResetContextSpeedMultiplier();
            }

            m_collidingMovables.Clear();
        }
    }
}
