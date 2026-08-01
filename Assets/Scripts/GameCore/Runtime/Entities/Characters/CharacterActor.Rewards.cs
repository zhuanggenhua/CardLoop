using System.Collections.Generic;

namespace FantasyWord.GameCore
{
    public partial class CharacterActor
    {
        public override void Kill()
        {
            if (IsMarkedAsDestroyed())
            {
                return;
            }

            CharacterBase rewardReceiver = ResolveRewardReceiver();
            base.Kill();

            if (m_isSummoned)
            {
                return;
            }

            GameRuntimeEvents.NotifyCharacterKilled(m_sheet);
            if (rewardReceiver != null)
            {
                GrantKillRewards(rewardReceiver, GameManager.InventorySystem);
            }

            m_sheet.ExecuteOnDeath(
                rewardReceiver != null
                    ? ResolveRewardCommandContext(rewardReceiver)
                    : GameCommandContext.Script(this, nameof(CharacterActor)));
        }

        private CharacterBase ResolveRewardReceiver()
        {
            if (TryGetLastEffectiveDamageSource(out CharacterBase damageSource))
            {
                return damageSource;
            }

            return GameManager.PlayerSystem.GetPrimaryPlayerCharacter();
        }

        private void GrantKillRewards(CharacterBase receiver, InventorySystem inventorySystem)
        {
            if (receiver == null)
            {
                return;
            }

            InventoryOwnerHandle receiverOwner = inventorySystem.GetOwner(receiver);
            List<Loot> grantedLoot = ResolveGrantedLoot(receiver);
            int experienceReward = m_sheet.GetExperienceRewardAtLevel(m_level);
            int moneyReward = m_sheet.GetMoneyRewardAtLevel(m_level);

            inventorySystem.ExecuteLootReward(
                receiverOwner,
                grantedLoot,
                moneyReward,
                EItemTransferType.CharacterDrop);

            if (receiver is CharacterActor actor && experienceReward > 0)
            {
                actor.AddExperience(experienceReward);
            }

            if (grantedLoot.Count > 0 || moneyReward > 0)
            {
                PlayRewardPresentation(receiver, grantedLoot.Count > 0, moneyReward);
            }

        }

        private List<Loot> ResolveGrantedLoot(CharacterBase receiver)
        {
            List<Loot> grantedLoot = new();

            foreach (Loot loot in m_sheet.GetPotentialLoot())
            {
                if (!CanGrantLoot(loot, receiver))
                {
                    continue;
                }

                grantedLoot.Add(loot);
            }

            return grantedLoot;
        }

        private bool CanGrantLoot(Loot loot, CharacterBase receiver)
        {
            return receiver.level >= loot.minimumReceiverLevel
                && m_level >= loot.minimumDefeatedCharacterLevel
                && loot.IsAvailable()
                && loot.ResolveDrop();
        }

        private void PlayRewardPresentation(CharacterBase receiver, bool rewardGranted, int moneyReward)
        {
            UnityEngine.Vector3 rewardPosition = transform.position;
            m_sheet.feedbacks.PlayLoot(rewardPosition);
            GameRuntimeEvents.NotifyLootPresentation(new LootPresentationContext(
                rewardPosition,
                this,
                receiver,
                rewardGranted,
                moneyReward));
        }

        private static GameCommandContext ResolveRewardCommandContext(CharacterBase receiver)
        {
            return GameCommandContext.ResolveForActor(receiver);
        }
    }
}
