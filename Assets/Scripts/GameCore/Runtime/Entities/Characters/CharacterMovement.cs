using UnityEngine;

namespace GameCore
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterBase))]
    public sealed class CharacterMovement : MonoBehaviour
    {
        [Header("Movement Control")]
        [SerializeField] private CharacterBase m_character = null;
        [SerializeField] private Transform m_directionPivot = null;
        [SerializeField] private bool m_castAbilitiesInPointerDirection = false;
        [SerializeField] private EPlayerMovementControlMode m_movementControlMode = EPlayerMovementControlMode.Directional;
        [SerializeField] private float m_clickMoveStoppingDistance = 0.05f;

        public EPlayerMovementControlMode MovementControlMode => m_movementControlMode;

        private Transform directionPivot => m_directionPivot != null ? m_directionPivot : m_character.transform;

        public bool TryUpdateIdlePointerTargetDirection()
        {
            if (!m_character ||
                !m_castAbilitiesInPointerDirection ||
                m_character.HasActiveMovementIntent() ||
                !GameManager.InputSystem.IsPointerActive(EActionMap.Gameplay))
            {
                return false;
            }

            Camera camera = GameManager.MainCamera;
            if (camera == null)
            {
                return false;
            }

            Vector2 pointerPosition = GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
            Vector2 pointerWorldPosition = camera.ScreenToWorldPoint(pointerPosition);
            Vector2 characterToPointerDirection = pointerWorldPosition - (Vector2)directionPivot.position;
            if (characterToPointerDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            m_character.SetTargetDirection(characterToPointerDirection.normalized);
            return true;
        }

        public bool HandleDirectionalMove(Vector2 direction)
        {
            if (!m_character || m_movementControlMode != EPlayerMovementControlMode.Directional)
            {
                return false;
            }

            m_character.SetMovementDirection(direction);
            return true;
        }

        public bool StopMovement()
        {
            if (!m_character)
            {
                return false;
            }

            m_character.ResetMovement();
            return true;
        }

        public bool HandleClickMove(Vector2 worldPosition)
        {
            if (!m_character ||
                m_movementControlMode != EPlayerMovementControlMode.ClickToMove ||
                !m_character.Can(EActionFlags.Move))
            {
                return false;
            }

            if (TryGetTerrainNavigationMap(out TerrainNavigationMap terrainNavigationMap))
            {
                if (!terrainNavigationMap.TryBuildWorldPath(
                        m_character.transform.position,
                        worldPosition,
                        out Vector2[] worldPath))
                {
                    m_character.ResetMovement();
                    return false;
                }

                m_character.SetMovementDirection(Vector2.zero);
                m_character.MoveAlongPath(worldPath, m_clickMoveStoppingDistance);
                return true;
            }

            Vector3 resolvedDestination = m_character.NearestValidDestination(worldPosition);
            Vector2 destination = resolvedDestination;

            m_character.SetMovementDirection(Vector2.zero);
            m_character.MoveTo(destination, m_clickMoveStoppingDistance);
            return true;
        }

        private static bool TryGetTerrainNavigationMap(out TerrainNavigationMap terrainNavigationMap)
        {
            terrainNavigationMap = null;
            if (!GameManager.Exists() ||
                !GameManager.TryGetSystem(out MapSystem mapSystem))
            {
                return false;
            }

            return mapSystem.TryGetActiveTerrainNavigationMap(out terrainNavigationMap);
        }

        public bool ToggleMovementControlMode()
        {
            EPlayerMovementControlMode nextMode = m_movementControlMode == EPlayerMovementControlMode.Directional
                ? EPlayerMovementControlMode.ClickToMove
                : EPlayerMovementControlMode.Directional;

            return SetMovementControlMode(nextMode);
        }

        public bool SetMovementControlMode(EPlayerMovementControlMode mode)
        {
            if (!m_character || m_movementControlMode == mode)
            {
                return false;
            }

            m_movementControlMode = mode;
            m_character.ResetMovement();
            return true;
        }

        private void Awake()
        {
            EnsureCharacterReference();
        }

        private void Reset()
        {
            EnsureCharacterReference();
        }

        private void OnValidate()
        {
            EnsureCharacterReference();
        }

        private void EnsureCharacterReference()
        {
            if (m_character == null)
            {
                TryGetComponent(out m_character);
            }
        }
    }
}
