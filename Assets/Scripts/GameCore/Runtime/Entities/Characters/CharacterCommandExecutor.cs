using GAS.Runtime;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterBase))]
    public sealed class CharacterCommandExecutor : MonoBehaviour
    {
        [SerializeField] private CharacterBase m_character = null;

        private CharacterButtonActivation m_buttonActivation = null;
        private CharacterMovement m_movement = null;

        public PlayerOrderResult Submit(PlayerOrderRequest orderRequest)
        {
            PlayerCommandResult commandResult = Execute(orderRequest.CommandRequest);
            return commandResult.Succeeded
                ? PlayerOrderResult.Success(orderRequest, 1, commandResult)
                : PlayerOrderResult.Failed(orderRequest, 1, commandResult);
        }

        public PlayerCommandResult Execute(PlayerCommandRequest request)
        {
            if (!m_character)
            {
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.InvalidControlledCharacter);
            }

            if (!isActiveAndEnabled)
            {
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.NotRunning);
            }

            if (request.CommandContext.HasActor && request.Actor != m_character)
            {
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.ActorMismatch);
            }

            return request.Kind switch
            {
                EPlayerCommandKind.Interact => ExecuteInteractCommand(request),
                EPlayerCommandKind.OpenGameMenu => ExecuteOpenGameMenuCommand(request),
                EPlayerCommandKind.Move => ExecuteMoveCommand(request),
                EPlayerCommandKind.StopMove => ExecuteStopMoveCommand(request),
                EPlayerCommandKind.ClickMove => ExecuteClickMoveCommand(request),
                EPlayerCommandKind.ToggleMovementControlMode => ExecuteToggleMovementControlModeCommand(request),
                EPlayerCommandKind.FireAbility => ExecuteFireAbilityCommand(request),
                EPlayerCommandKind.StopFireAbility => ExecuteStopFireAbilityCommand(request),
                _ => PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.InvalidCommand)
            };
        }

        private void Awake()
        {
            EnsureCharacterReference();
            ResolveButtonActivation();
            ResolveMovement();
        }

        private void Reset()
        {
            EnsureCharacterReference();
        }

        private void OnValidate()
        {
            EnsureCharacterReference();
        }

        private PlayerCommandResult ExecuteInteractCommand(PlayerCommandRequest request)
        {
            CharacterButtonActivation buttonActivation = ResolveButtonActivation();
            if (buttonActivation == null || !buttonActivation.CanInteractNow())
            {
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.InteractionLocked);
            }

            return buttonActivation.TryInteract(request.InteractionTarget)
                ? PlayerCommandResult.Success(request)
                : PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.BlockedByState);
        }

        private PlayerCommandResult ExecuteOpenGameMenuCommand(PlayerCommandRequest request)
        {
            if (!m_character.Can(EActionFlags.Interact))
            {
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.BlockedByState);
            }

            GameRuntimeEvents.RequestMenu(EMenu.Pause);
            return PlayerCommandResult.Success(request);
        }

        private PlayerCommandResult ExecuteMoveCommand(PlayerCommandRequest request)
        {
            CharacterMovement movement = ResolveMovement();
            return movement != null && movement.HandleDirectionalMove(request.Direction)
                ? PlayerCommandResult.Success(request)
                : PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.BlockedByState);
        }

        private PlayerCommandResult ExecuteStopMoveCommand(PlayerCommandRequest request)
        {
            CharacterMovement movement = ResolveMovement();
            return movement != null && movement.StopMovement()
                ? PlayerCommandResult.Success(request)
                : PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.BlockedByState);
        }

        private PlayerCommandResult ExecuteClickMoveCommand(PlayerCommandRequest request)
        {
            if (!request.WorldPosition.HasValue)
            {
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.InvalidTarget);
            }

            CharacterMovement movement = ResolveMovement();
            return movement != null && movement.HandleClickMove(request.WorldPosition.Value)
                ? PlayerCommandResult.Success(request)
                : PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.BlockedByState);
        }

        private PlayerCommandResult ExecuteToggleMovementControlModeCommand(PlayerCommandRequest request)
        {
            CharacterMovement movement = ResolveMovement();
            return movement != null && movement.ToggleMovementControlMode()
                ? PlayerCommandResult.Success(request)
                : PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.BlockedByState);
        }

        private PlayerCommandResult ExecuteFireAbilityCommand(PlayerCommandRequest request)
        {
            if (ResolveButtonActivation()?.HasInteractedThisFrame() == true)
            {
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.BlockedByState);
            }

            CharacterAbilityFireResult fireResult = m_character.FireEquippedAbilityAtIndex(
                request.AbilityIndex,
                request.CommandContext,
                CreateAbilityActivationContext(request));

            if (!fireResult.HasAbilitySource)
            {
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.MissingAbility);
            }

            if (fireResult.Result != EAbilityFireCheckResult.Valid)
            {
                GameRuntimeEvents.NotifyPlayerAbilityFireFailed(fireResult.FormalGasAbilityCode, fireResult.Result);
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.AbilityRejected);
            }

            return PlayerCommandResult.Success(request);
        }

        private AbilityActivationContext CreateAbilityActivationContext(PlayerCommandRequest request)
        {
            AbilitySystemCell mainTarget = null;
            if (request.TargetCharacter != null &&
                request.TargetCharacter.TryGetFormalAbilitySystem(out AbilitySystemComponent targetAbilitySystem))
            {
                mainTarget = targetAbilitySystem.Cell;
            }

            Vector2 aimDirection = ResolveAbilityAimDirection(request);
            Vector3 aimOrigin = m_character.transform.position;
            return aimDirection.sqrMagnitude > 0.0001f
                ? new AbilityActivationContext(aimOrigin, aimDirection, mainTarget)
                : new AbilityActivationContext(aimOrigin, mainTarget);
        }

        private Vector2 ResolveAbilityAimDirection(PlayerCommandRequest request)
        {
            if (request.Direction.sqrMagnitude > 0.0001f)
            {
                return request.Direction.normalized;
            }

            Vector2 characterPosition = m_character.transform.position;
            if (request.TargetCharacter != null)
            {
                Vector2 targetDirection =
                    (Vector2)request.TargetCharacter.transform.position - characterPosition;
                if (targetDirection.sqrMagnitude > 0.0001f)
                {
                    return targetDirection.normalized;
                }
            }

            if (request.WorldPosition.HasValue)
            {
                Vector2 worldDirection = request.WorldPosition.Value - characterPosition;
                if (worldDirection.sqrMagnitude > 0.0001f)
                {
                    return worldDirection.normalized;
                }
            }

            Vector2 currentDirection = m_character.GetTargetDirection();
            return currentDirection.sqrMagnitude > 0.0001f
                ? currentDirection.normalized
                : Vector2.zero;
        }

        private PlayerCommandResult ExecuteStopFireAbilityCommand(PlayerCommandRequest request)
        {
            return m_character.StopFireEquippedAbilityAtIndex(request.AbilityIndex)
                ? PlayerCommandResult.Success(request)
                : PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.MissingAbility);
        }

        private void EnsureCharacterReference()
        {
            if (m_character == null)
            {
                TryGetComponent(out m_character);
            }
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
