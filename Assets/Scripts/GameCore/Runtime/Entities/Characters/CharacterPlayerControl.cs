using UnityEngine;

namespace FantasyWord.GameCore
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterBase))]
    [RequireComponent(typeof(CharacterCommandExecutor))]
    public sealed class CharacterPlayerControl : MonoBehaviour, IPlayerInputTarget
    {
        [Header("Control Composition")]
        [SerializeField] private CharacterBase m_character = null;
        [SerializeField] private CharacterCommandExecutor m_commandExecutor = null;
        [SerializeField] private bool m_acceptsPlayerInput = true;

        private CharacterButtonActivation m_buttonActivation = null;
        private CharacterMovement m_movement = null;
        private bool m_wasLocallyControlled;

        public CharacterBase Character => m_character;
        public bool AcceptsPlayerInput => m_acceptsPlayerInput;

        public void SetAcceptsPlayerInput(bool acceptsPlayerInput)
        {
            m_acceptsPlayerInput = acceptsPlayerInput;
            if (!m_acceptsPlayerInput)
            {
                ResetLocalControlState();
            }
        }

        public bool TryGetControlledCharacter(out CharacterBase character)
        {
            character = m_character;
            return character != null;
        }

        public CharacterBase[] CreateControlledCharacterSnapshot()
        {
            return m_character ? new[] { m_character } : System.Array.Empty<CharacterBase>();
        }

        public PlayerOrderResult SubmitPlayerOrder(PlayerOrderRequest orderRequest)
        {
            if (!m_acceptsPlayerInput || !m_character || !m_character.CanBePlayerControlled())
            {
                PlayerCommandResult failed = PlayerCommandResult.Failed(
                    orderRequest.CommandRequest,
                    EPlayerCommandFailureReason.ControlLocked);
                return PlayerOrderResult.Failed(orderRequest, 1, failed);
            }

            return ResolveCommandExecutor().Submit(orderRequest);
        }

        public EPlayerMovementControlMode GetMovementControlMode()
        {
            CharacterMovement movement = ResolveMovement();
            return movement != null ? movement.MovementControlMode : EPlayerMovementControlMode.Directional;
        }

        public void SetMovementControlMode(EPlayerMovementControlMode mode)
        {
            ResolveMovement()?.SetMovementControlMode(mode);
        }

        public bool TryGetCurrentInteractionTargetPosition(out Vector3 position)
        {
            CharacterButtonActivation buttonActivation = ResolveButtonActivation();
            if (buttonActivation != null)
            {
                return buttonActivation.TryGetCurrentTargetPosition(out position);
            }

            position = default;
            return false;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Update()
        {
            if (!m_character || !m_acceptsPlayerInput)
            {
                return;
            }

            if (!GameManager.PlayerSystem.TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter) ||
                currentControlledCharacter != m_character ||
                !m_character.CanBePlayerControlled())
            {
                ResetLocalControlStateIfPreviouslyControlled();
                return;
            }

            m_wasLocallyControlled = true;
            ResolveButtonActivation()?.RefreshCurrentTarget();
            CharacterMovement movement = ResolveMovement();
            if (movement == null)
            {
                m_character.ResetTargetDirection();
                return;
            }

            if (!m_character.CanUpdateTargetDirection())
            {
                return;
            }

            if (!movement.TryUpdateIdlePointerTargetDirection())
            {
                m_character.ResetTargetDirection();
            }
        }

        private void OnDisable()
        {
            ResetLocalControlState();
        }

        private void Reset()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            EnsureReferences();
        }

        private void ResetLocalControlState()
        {
            if (m_character)
            {
                m_character.ResetTargetDirection();
                m_character.ResetMovement();
            }

            ResolveButtonActivation()?.ResetState();
            m_wasLocallyControlled = false;
        }

        private void ResetLocalControlStateIfPreviouslyControlled()
        {
            if (!m_wasLocallyControlled)
            {
                return;
            }

            ResetLocalControlState();
        }

        private void EnsureReferences()
        {
            if (m_character == null)
            {
                TryGetComponent(out m_character);
            }

            if (m_commandExecutor == null)
            {
                TryGetComponent(out m_commandExecutor);
            }

            ResolveButtonActivation();
            ResolveMovement();
        }

        private CharacterCommandExecutor ResolveCommandExecutor()
        {
            if (m_commandExecutor == null)
            {
                TryGetComponent(out m_commandExecutor);
            }

            return m_commandExecutor;
        }

        private CharacterButtonActivation ResolveButtonActivation()
        {
            if (m_buttonActivation == null && m_character != null)
            {
                m_character.TryGetComponent(out m_buttonActivation);
            }

            return m_buttonActivation;
        }

        private CharacterMovement ResolveMovement()
        {
            if (m_movement == null && m_character != null)
            {
                m_character.TryGetComponent(out m_movement);
            }

            return m_movement;
        }
    }
}
