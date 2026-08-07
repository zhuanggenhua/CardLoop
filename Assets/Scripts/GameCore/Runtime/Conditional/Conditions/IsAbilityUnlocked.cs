using System;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    [Serializable]
    public class IsAbilityUnlocked : ABaseCondition
    {
        [SerializeField] private int m_formalGasAbilityCode = 0;

        public override bool Evaluate()
        {
            if (!TryGetCurrentControlledCharacter(out CharacterBase currentCharacter))
            {
                return false;
            }

            if (m_formalGasAbilityCode <= 0)
            {
                return false;
            }

            return currentCharacter.HasFormalGasAbility(m_formalGasAbilityCode);
        }

        protected override void OnStartListening()
        {
            EventKit.Type.Register<CharacterAbilityAddedEvent>(OnAbilityAdded);
            EventKit.Type.Register<CharacterAbilityRemovedEvent>(OnAbilityRemoved);
            if (TryGetPlayerSystem(out PlayerSystem playerSystem))
            {
                playerSystem.AddCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        protected override void OnStopListening()
        {
            EventKit.Type.UnRegister<CharacterAbilityAddedEvent>(OnAbilityAdded);
            EventKit.Type.UnRegister<CharacterAbilityRemovedEvent>(OnAbilityRemoved);
            if (TryGetPlayerSystem(out PlayerSystem playerSystem))
            {
                playerSystem.RemoveCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        private void OnAbilityAdded(CharacterAbilityAddedEvent abilityAddedEvent)
        {
            if (ShouldRefreshFor(
                    abilityAddedEvent.Character,
                    abilityAddedEvent.FormalGasAbilityCode))
            {
                NotifyStateChange();
            }
        }

        private void OnAbilityRemoved(CharacterAbilityRemovedEvent abilityRemovedEvent)
        {
            if (ShouldRefreshFor(
                    abilityRemovedEvent.Character,
                    abilityRemovedEvent.FormalGasAbilityCode))
            {
                NotifyStateChange();
            }
        }

        private void OnCurrentControlledCharacterChanged(CharacterBase character) => NotifyStateChange();

        private bool ShouldRefreshFor(CharacterBase character, int formalGasAbilityCode)
        {
            if (m_formalGasAbilityCode <= 0)
            {
                return false;
            }

            return character != null &&
                formalGasAbilityCode == m_formalGasAbilityCode &&
                TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter) &&
                character == currentControlledCharacter;
        }

        private static bool TryGetCurrentControlledCharacter(out CharacterBase currentControlledCharacter)
        {
            currentControlledCharacter = null;
            if (!TryGetPlayerSystem(out PlayerSystem playerSystem))
            {
                return false;
            }

            currentControlledCharacter = playerSystem.GetCurrentControlledCharacterOrPlayerInstance();
            return currentControlledCharacter != null;
        }

        private static bool TryGetPlayerSystem(out PlayerSystem playerSystem)
        {
            return GameManager.TryGetSystem(out playerSystem);
        }
    }
}
