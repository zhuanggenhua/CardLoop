using System;
using System.Reflection;
using GAS.Runtime;
using GameCore;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Tabletop;
using RuntimeTabletop = Gameplay.Tabletop.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using YokiFrame;
using UEntity = Unity.Entities.Entity;

namespace Gameplay.Tests
{
	/// <summary>验证战斗只接收拥有唯一 ASC 的角色卡，并直接拥有本场战斗方。</summary>
	public sealed class BattleEditModeTests
	{
		private readonly System.Collections.Generic.List<UnityEngine.Object> m_createdObjects = new();

		[SetUp]
		public void SetUp()
		{
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			CreateGameManagerWithCombatConfig(canCriticalHit: true, canMissHit: true);
		}

		[TearDown]
		public void TearDown()
		{
			InvokeFormalGasBootstrap("Shutdown");
			SetStaticField(typeof(GameManager), "_instance", null);
			for (int i = m_createdObjects.Count - 1; i >= 0; i--)
			{
				if (m_createdObjects[i] != null)
				{
					UnityEngine.Object.DestroyImmediate(m_createdObjects[i]);
				}
			}
			m_createdObjects.Clear();
		}

		[Test]
		public void CreateCard_UsesCharacterDefinitionAsTheOnlyAbilitySystemSource()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.character");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));

				TabletopCard card = tabletop.CreateCard(definition.ContentId, Vector2.zero);

				Assert.That(card, Is.TypeOf<CharacterCard>());
				Assert.That(((CharacterCard)card).AbilitySystem.HasTag(XTag.Faction_Player), Is.True);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void CharacterSnapshot_RestoresLevelAndBaseAttributesFromCurrentAscPreset()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.character.snapshot");
			RuntimeTabletop source = null;
			RuntimeTabletop restored = null;
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { definition });
				source = CreateBattleTabletop(contentIndex);
				CharacterCard character = (CharacterCard)source.CreateCard(definition.ContentId, Vector2.zero);
				float savedHealth = character.AbilitySystem.GetAttrBaseValue(XAttrSet.FightUnit, XAttribute.Health) - 1f;
				character.AbilitySystem.SetAttrBaseValue(XAttrSet.FightUnit, XAttribute.Health, savedHealth);
				character.AbilitySystem.SetLevel(3);

				string json = JsonUtility.ToJson(source.Cards.CreateSnapshot());
				TabletopCardStateSnapshot snapshot = JsonUtility.FromJson<TabletopCardStateSnapshot>(json);
				restored = new RuntimeTabletop(
					contentIndex,
					snapshot,
					TabletopTestPlacement.Rules,
					_ => false,
					(_, __) => { },
					_ => { },
					cardIdSequence: new TabletopCardIdSequence(source.Cards.CardIdSequence.NextValue));

				Assert.That(restored.Cards.TryGetCard(character.Id, out TabletopCard restoredCard), Is.True);
				Assert.That(restoredCard, Is.TypeOf<CharacterCard>());
				CharacterCard restoredCharacter = (CharacterCard)restoredCard;
				Assert.That(restoredCharacter.AbilitySystem.GetLevel(), Is.EqualTo(3));
				Assert.That(
					restoredCharacter.AbilitySystem.GetAttrBaseValue(XAttrSet.FightUnit, XAttribute.Health),
					Is.EqualTo(savedHealth));
				Assert.That(restoredCharacter.AbilitySystem.HasTag(XTag.Faction_Player), Is.True);
				AbilityConfig[] presetAbilities = definition.CreateAbilitySystemConfig().BaseAbilities;
				Assert.That(presetAbilities, Is.Not.Empty);
				ConfAbilityBaseInfo presetAbility = Array.Find(
					presetAbilities[0].ComponentConfigs,
					component => component is ConfAbilityBaseInfo) as ConfAbilityBaseInfo;
				Assert.That(presetAbility, Is.Not.Null);
				Assert.That(restoredCharacter.AbilitySystem.GetAbilitySpec(presetAbility.Code), Is.Not.Null);
			}
			finally
			{
				restored?.End();
				source?.End();
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void StartBattle_OwnsExplicitSidesAndRejectsOrdinaryCards()
		{
			CharacterCardDefinition characterDefinition = CreateCharacterCard("test.battle.character-side");
			CardDefinition ordinaryDefinition = CreateOrdinaryCard("test.battle.ordinary");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { characterDefinition, ordinaryDefinition });
				RuntimeTabletop tabletop = CreateBattleTabletop(contentIndex);
				TabletopCard allyOne = tabletop.CreateCard(characterDefinition.ContentId, Vector2.zero);
				TabletopCard allyTwo = tabletop.CreateCard(characterDefinition.ContentId, Vector2.up);
				TabletopCard enemy = tabletop.CreateCard(characterDefinition.ContentId, Vector2.one);
				TabletopCard ordinary = tabletop.CreateCard(ordinaryDefinition.ContentId, Vector2.left);

				Assert.Throws<InvalidOperationException>(() => tabletop.StartBattle(
					new[] { allyOne.Id },
					new[] { ordinary.Id }));

				Battle battle = tabletop.StartBattle(
					new[] { allyOne.Id, allyTwo.Id },
					new[] { enemy.Id });

				Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(1));
				Assert.That(battle.Sides, Has.Count.EqualTo(2));
				CollectionAssert.AreEqual(new[] { allyOne.Id, allyTwo.Id }, battle.Sides[0].CardIds);
				CollectionAssert.AreEqual(new[] { enemy.Id }, battle.Sides[1].CardIds);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(characterDefinition);
				UnityEngine.Object.DestroyImmediate(ordinaryDefinition);
			}
		}

		[Test]
		public void StartBattle_RejectsInvalidSidesMissingCardsAndDuplicateActiveMembership()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.membership");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
				TabletopCard ally = tabletop.CreateCard(definition.ContentId, Vector2.zero);
				TabletopCard enemy = tabletop.CreateCard(definition.ContentId, Vector2.one);

				Assert.Throws<InvalidOperationException>(() => tabletop.StartBattle(new[] { ally.Id }));
				Assert.Throws<InvalidOperationException>(() => tabletop.StartBattle(
					new[] { ally.Id },
					Array.Empty<TabletopCardId>()));
				Assert.Throws<InvalidOperationException>(() => tabletop.StartBattle(
					new[] { ally.Id },
					new[] { new TabletopCardId(999) }));

				Battle battle = tabletop.StartBattle(new[] { ally.Id }, new[] { enemy.Id });
				Assert.Throws<InvalidOperationException>(() => tabletop.StartBattle(
					new[] { ally.Id },
					new[] { enemy.Id }));
				Assert.Throws<InvalidOperationException>(() => tabletop.RemoveCard(ally.Id));
				Assert.That(battle.HasParticipant(ally.Id), Is.True);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void StartBattle_RequiresTheTabletopAuthoritativeRandomStream()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.random-required");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(
					ContentIndex.Build(new ContentAsset[] { definition }),
					initializeRandom: false);
				CharacterCard ally = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.zero);
				CharacterCard enemy = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.one);

				Assert.Throws<InvalidOperationException>(() => tabletop.StartBattle(
					new[] { ally.Id },
					new[] { enemy.Id }));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void CharacterCannotParticipateInAnActionAndBattleAtTheSameTime()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.exclusive-participation");
			ActionDefinition actionDefinition = CreateAction(
				"test.action.exclusive-participation",
				definition.ContentId.Value);
			RuntimeTabletop tabletop = null;
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { definition, actionDefinition });
				tabletop = CreateBattleTabletop(contentIndex);
				TabletopCard worker = tabletop.CreateCard(definition.ContentId, Vector2.zero);
				TabletopCard partner = tabletop.CreateCard(definition.ContentId, Vector2.right);
				TabletopCard enemy = tabletop.CreateCard(definition.ContentId, Vector2.left);
				ActionCandidate candidate = CreateReadyActionCandidate(
				 tabletop,
					contentIndex,
					actionDefinition,
					worker.Id,
					partner.Id);

				ActionInstance action = tabletop.StartAction(ActionRequest.FromCandidate(candidate));
				InvalidOperationException battleException = Assert.Throws<InvalidOperationException>(() =>
					tabletop.StartBattle(new[] { worker.Id }, new[] { enemy.Id }));
				StringAssert.Contains("活动行动", battleException.Message);
				Assert.That(action.State, Is.EqualTo(ActionInstanceState.Running));

				tabletop.CancelAction(action);
				Battle battle = tabletop.StartBattle(new[] { worker.Id }, new[] { enemy.Id });
				InvalidOperationException actionException = Assert.Throws<InvalidOperationException>(() =>
					tabletop.StartAction(ActionRequest.FromCandidate(candidate)));
				StringAssert.Contains("活动战斗", actionException.Message);
				Assert.That(battle.HasParticipant(worker.Id), Is.True);
			}
			finally
			{
				tabletop?.End();
				UnityEngine.Object.DestroyImmediate(definition);
				UnityEngine.Object.DestroyImmediate(actionDefinition);
			}
		}

		[Test]
		public void BattleAbilityRandom_IsDeterministicAndIndependentBetweenBattles()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.random-sequence");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { definition });
				RuntimeTabletop firstTabletop = CreateBattleTabletop(contentIndex, seed: 4711u);
				Battle firstBattle = CreateTwoCharacterBattle(firstTabletop, definition.ContentId, new Vector2(-10f, 0f));
				Battle unrelatedBattle = CreateTwoCharacterBattle(firstTabletop, definition.ContentId, new Vector2(10f, 0f));
				uint firstSeed = firstBattle.TakeAbilityActivationSeed();
				uint secondSeed = firstBattle.TakeAbilityActivationSeed();
				_ = unrelatedBattle.TakeAbilityActivationSeed();

				RuntimeTabletop replayTabletop = CreateBattleTabletop(contentIndex, seed: 4711u);
				Battle replayBattle = CreateTwoCharacterBattle(replayTabletop, definition.ContentId, new Vector2(-10f, 0f));
				Assert.That(replayBattle.TakeAbilityActivationSeed(), Is.EqualTo(firstSeed));
				Assert.That(replayBattle.TakeAbilityActivationSeed(), Is.EqualTo(secondSeed));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void LeaveBattle_EndsBattleWhenOnlyOneSideHasParticipants()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.leave");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
				TabletopCard ally = tabletop.CreateCard(definition.ContentId, Vector2.zero);
				TabletopCard enemy = tabletop.CreateCard(definition.ContentId, Vector2.one);
				Battle battle = tabletop.StartBattle(new[] { ally.Id }, new[] { enemy.Id });

				tabletop.LeaveBattle(battle, enemy.Id);

				Assert.That(battle.IsEnded, Is.True);
				Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(0));
				Assert.DoesNotThrow(() => tabletop.RemoveCard(ally.Id));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void DropBattleParticipant_InsideAreaStaysAndOutsideAreaLeavesThenPlacesStack()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.drop-leave");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
				TabletopCard ally = tabletop.CreateCard(definition.ContentId, Vector2.zero);
				TabletopCard enemy = tabletop.CreateCard(definition.ContentId, Vector2.one);
				Battle battle = tabletop.StartBattle(new[] { ally.Id }, new[] { enemy.Id });
				Rect battleArea = tabletop.GetBattleArea(battle);
				Vector2 stackPositionBefore = tabletop.Cards.GetStackContaining(ally.Id).Position;

				bool handledInside = tabletop.TryDropBattleParticipant(
					ally.Id,
					battleArea.center,
					new Vector2(25f, 25f),
					out bool leftInside,
					out TabletopCardStack placedInside);

				Assert.That(handledInside, Is.True);
				Assert.That(leftInside, Is.False);
				Assert.That(placedInside, Is.Null);
				Assert.That(battle.HasParticipant(ally.Id), Is.True);
				Assert.That(tabletop.Cards.GetStackContaining(ally.Id).Position, Is.EqualTo(stackPositionBefore));

				Vector2 fleePosition = new Vector2(battleArea.xMax + 10f, battleArea.yMax + 10f);
				bool handledOutside = tabletop.TryDropBattleParticipant(
					ally.Id,
					fleePosition,
					fleePosition,
					out bool leftOutside,
					out TabletopCardStack placedOutside);

				Assert.That(handledOutside, Is.True);
				Assert.That(leftOutside, Is.True);
				Assert.That(placedOutside, Is.Not.Null);
				Assert.That(placedOutside.Cards[0].Id, Is.EqualTo(ally.Id));
				Assert.That(placedOutside.Position, Is.EqualTo(fleePosition));
				Assert.That(battle.IsEnded, Is.True);
				Assert.That(tabletop.ActiveBattles, Is.Empty);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void JoinBattle_AddsACharacterToTheSelectedExistingSide()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.reinforcement");
			CardDefinition ordinaryDefinition = CreateOrdinaryCard("test.battle.reinforcement.ordinary");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(
					ContentIndex.Build(new ContentAsset[] { definition, ordinaryDefinition }));
				TabletopCard ally = tabletop.CreateCard(definition.ContentId, Vector2.zero);
				TabletopCard reinforcement = tabletop.CreateCard(definition.ContentId, Vector2.up);
				TabletopCard enemy = tabletop.CreateCard(definition.ContentId, Vector2.one);
				TabletopCard ordinary = tabletop.CreateCard(ordinaryDefinition.ContentId, Vector2.left);
				Battle battle = tabletop.StartBattle(new[] { ally.Id }, new[] { enemy.Id });

				Assert.Throws<ArgumentOutOfRangeException>(() =>
					tabletop.JoinBattle(battle, sideIndex: 2, reinforcement.Id));
				Assert.Throws<InvalidOperationException>(() =>
					tabletop.JoinBattle(battle, sideIndex: 0, ordinary.Id));
				tabletop.JoinBattle(battle, sideIndex: 0, reinforcement.Id);

				CollectionAssert.AreEqual(
					new[] { ally.Id, reinforcement.Id },
					battle.Sides[0].CardIds);
				Assert.That(battle.HasParticipant(reinforcement.Id), Is.True);
				Assert.Throws<InvalidOperationException>(() =>
					tabletop.JoinBattle(battle, sideIndex: 1, reinforcement.Id));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
				UnityEngine.Object.DestroyImmediate(ordinaryDefinition);
			}
		}

		[Test]
		public void StartBattle_AutomaticallyMergesOverlappingBattleAreasBySideIndex()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.auto-merge-start");
			RuntimeTabletop tabletop = null;
			try
			{
				tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
				Battle destination = CreateTwoCharacterBattle(tabletop, definition.ContentId, Vector2.zero);
				Battle mergedResult = CreateTwoCharacterBattle(
				 tabletop,
					definition.ContentId,
					new Vector2(0.1f, 0f));

				Assert.That(mergedResult, Is.SameAs(destination));
				Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(1));
				Assert.That(destination.Sides[0].ParticipantCount, Is.EqualTo(2));
				Assert.That(destination.Sides[1].ParticipantCount, Is.EqualTo(2));
			}
			finally
			{
				if (tabletop != null && !tabletop.IsEnded)
				{
					tabletop.End();
				}
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void JoinBattle_AutomaticallyMergesWhenTheExpandedAreaOverlapsAnotherBattle()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.auto-merge-join");
			RuntimeTabletop tabletop = null;
			try
			{
				tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
				Battle destination = CreateTwoCharacterBattle(tabletop, definition.ContentId, Vector2.zero);
				Battle source = CreateTwoCharacterBattle(
				 tabletop,
					definition.ContentId,
					new Vector2(0.33f, 0f));
				CharacterCard reinforcement = (CharacterCard)tabletop.CreateCard(
					definition.ContentId,
					Vector2.zero);
				Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(2));

				tabletop.JoinBattle(destination, sideIndex: 0, reinforcement.Id);

				Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(1));
				Assert.That(tabletop.ActiveBattles[0], Is.SameAs(destination));
				Assert.That(source.IsEnded, Is.True);
				Assert.That(destination.Sides[0].ParticipantCount, Is.EqualTo(3));
				Assert.That(destination.Sides[1].ParticipantCount, Is.EqualTo(2));
			}
			finally
			{
				if (tabletop != null && !tabletop.IsEnded)
				{
					tabletop.End();
				}
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void MergeBattles_TransfersMappedSidesAndPreservesDestinationRandomStream()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.merge");
			RuntimeTabletop tabletop = null;
			RuntimeTabletop replay = null;
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { definition });
				tabletop = CreateBattleTabletop(contentIndex, seed: 7321u);
				CharacterCard firstAlly = (CharacterCard)tabletop.CreateCard(
					definition.ContentId,
					new Vector2(-10f, 0f));
				CharacterCard firstEnemy = (CharacterCard)tabletop.CreateCard(
					definition.ContentId,
					new Vector2(-10f, 0f));
				CharacterCard secondAlly = (CharacterCard)tabletop.CreateCard(
					definition.ContentId,
					new Vector2(10f, 0f));
				CharacterCard secondEnemy = (CharacterCard)tabletop.CreateCard(
					definition.ContentId,
					new Vector2(10f, 0f));
				Battle destination = tabletop.StartBattle(
					new[] { firstAlly.Id },
					new[] { firstEnemy.Id });
				Battle source = tabletop.StartBattle(
					new[] { secondAlly.Id },
					new[] { secondEnemy.Id });
				uint firstSeed = destination.TakeAbilityActivationSeed();

				tabletop.MergeBattles(destination, source, new[] { 0, 1 });

				Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(1));
				Assert.That(tabletop.ActiveBattles[0], Is.SameAs(destination));
				Assert.That(source.IsEnded, Is.True);
				CollectionAssert.AreEqual(
					new[] { firstAlly.Id, secondAlly.Id },
					destination.Sides[0].CardIds);
				CollectionAssert.AreEqual(
					new[] { firstEnemy.Id, secondEnemy.Id },
					destination.Sides[1].CardIds);

				replay = CreateBattleTabletop(contentIndex, seed: 7321u);
				Battle replayDestination = CreateTwoCharacterBattle(
					replay,
					definition.ContentId,
					new Vector2(-10f, 0f));
				_ = CreateTwoCharacterBattle(replay, definition.ContentId, new Vector2(10f, 0f));
				Assert.That(replayDestination.TakeAbilityActivationSeed(), Is.EqualTo(firstSeed));
				Assert.That(
					destination.TakeAbilityActivationSeed(),
					Is.EqualTo(replayDestination.TakeAbilityActivationSeed()));
			}
			finally
			{
				if (tabletop != null && !tabletop.IsEnded)
				{
					tabletop.End();
				}
				if (replay != null && !replay.IsEnded)
				{
					replay.End();
				}
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void MergeBattles_RejectsInvalidSideMappingWithoutChangingEitherBattle()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.merge-invalid");
			RuntimeTabletop tabletop = null;
			try
			{
				tabletop = CreateBattleTabletop(
					ContentIndex.Build(new ContentAsset[] { definition }));
				Battle destination = CreateTwoCharacterBattle(
				 tabletop,
					definition.ContentId,
					new Vector2(-10f, 0f));
				Battle source = CreateTwoCharacterBattle(
				 tabletop,
					definition.ContentId,
					new Vector2(10f, 0f));
				TabletopCardId destinationAlly = destination.Sides[0].CardIds[0];
				TabletopCardId sourceAlly = source.Sides[0].CardIds[0];

				Assert.Throws<ArgumentException>(() =>
					tabletop.MergeBattles(destination, source, new[] { 0 }));
				Assert.Throws<ArgumentOutOfRangeException>(() =>
					tabletop.MergeBattles(destination, source, new[] { 0, 2 }));

				Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(2));
				Assert.That(destination.IsEnded, Is.False);
				Assert.That(source.IsEnded, Is.False);
				CollectionAssert.AreEqual(new[] { destinationAlly }, destination.Sides[0].CardIds);
				CollectionAssert.AreEqual(new[] { sourceAlly }, source.Sides[0].CardIds);
			}
			finally
			{
				if (tabletop != null && !tabletop.IsEnded)
				{
					tabletop.End();
				}
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void AdvanceRealTime_HostileEnemyStartsBattleAgainstNearbyPlayer()
		{
			CharacterCardDefinition playerDefinition = CreateCharacterCard(
				"test.hostile-ai.player",
				abilitySystemPresetId: 1001);
			CharacterCardDefinition enemyDefinition = CreateCharacterCard(
				"test.hostile-ai.enemy",
				abilitySystemPresetId: 1004,
				automaticMovementIntervalSeconds: 1f,
				automaticMovementRadius: 1f,
				automaticMovementMaxAttempts: 1,
				automaticAggroRadius: 5f,
				automaticAttackRadius: 1.5f);
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(
					ContentIndex.Build(new ContentAsset[] { playerDefinition, enemyDefinition }),
					seed: 12345u);
				CharacterCard player = (CharacterCard)tabletop.CreateCard(
					playerDefinition.ContentId,
					Vector2.zero);
				CharacterCard enemy = (CharacterCard)tabletop.CreateCard(
					enemyDefinition.ContentId,
					new Vector2(1f, 0f));

				tabletop.AdvanceRealTime(1f);

				Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(1));
				Battle battle = tabletop.ActiveBattles[0];
				CollectionAssert.AreEqual(new[] { enemy.Id }, battle.Sides[0].CardIds);
				CollectionAssert.AreEqual(new[] { player.Id }, battle.Sides[1].CardIds);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(playerDefinition);
				UnityEngine.Object.DestroyImmediate(enemyDefinition);
			}
		}

		[Test]
		public void AdvanceRealTime_HostileEnemyJoinsExistingPlayerBattle()
		{
			CharacterCardDefinition playerDefinition = CreateCharacterCard(
				"test.hostile-ai.join-player",
				abilitySystemPresetId: 1001);
			CharacterCardDefinition enemyDefinition = CreateCharacterCard(
				"test.hostile-ai.join-enemy",
				abilitySystemPresetId: 1004,
				automaticMovementIntervalSeconds: 1f,
				automaticMovementRadius: 1f,
				automaticMovementMaxAttempts: 1,
				automaticAggroRadius: 5f,
				automaticAttackRadius: 1.5f);
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(
					ContentIndex.Build(new ContentAsset[] { playerDefinition, enemyDefinition }),
					seed: 54321u);
				CharacterCard player = (CharacterCard)tabletop.CreateCard(
					playerDefinition.ContentId,
					Vector2.zero);
				CharacterCard existingEnemy = (CharacterCard)tabletop.CreateCard(
					enemyDefinition.ContentId,
					new Vector2(0.5f, 0f));
				CharacterCard reinforcement = (CharacterCard)tabletop.CreateCard(
					enemyDefinition.ContentId,
					new Vector2(0.75f, 0f));
				Battle battle = tabletop.StartBattle(
					new[] { existingEnemy.Id },
					new[] { player.Id });

				tabletop.AdvanceRealTime(1f);

				Assert.That(tabletop.ActiveBattles, Has.Count.EqualTo(1));
				Assert.That(tabletop.ActiveBattles[0], Is.SameAs(battle));
				CollectionAssert.AreEqual(
					new[] { existingEnemy.Id, reinforcement.Id },
					battle.Sides[0].CardIds);
				CollectionAssert.AreEqual(new[] { player.Id }, battle.Sides[1].CardIds);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(playerDefinition);
				UnityEngine.Object.DestroyImmediate(enemyDefinition);
			}
		}

		[Test]
		public void CreateSnapshot_RejectsAnActiveBattleInsteadOfSavingPartialCombatState()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.snapshot-rejected");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
				CharacterCard ally = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.zero);
				CharacterCard enemy = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.one);
				tabletop.StartBattle(new[] { ally.Id }, new[] { enemy.Id });

				InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => tabletop.CreateSnapshot());

				StringAssert.Contains("不保存战斗中状态", exception.Message);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void RequestBattleAbilityActivation_RejectsCardsOutsideTheBattleAndMissingAbilities()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.ability-contract");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
				CharacterCard source = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.zero);
				CharacterCard target = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.one);
				CharacterCard outsider = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.left);
				Battle battle = tabletop.StartBattle(new[] { source.Id }, new[] { target.Id });

				Assert.Throws<InvalidOperationException>(() => tabletop.RequestBattleAbilityActivation(
					battle,
					source.Id,
					outsider.Id,
					XAbility.ABILITY_Attack));
				Assert.Throws<InvalidOperationException>(() => tabletop.RequestBattleAbilityActivation(
					battle,
					source.Id,
					target.Id,
					XAbility.ABILITY_Attack));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void AdvanceRealTime_AutomaticBattleAbilityUsesTemplateDefenseBeforeCriticalDamage()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.gns-critical");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(
					ContentIndex.Build(new ContentAsset[] { definition }),
					seed: 12345u);
				CharacterCard attacker = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.zero);
				CharacterCard defender = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.one);
				SetBaseAndRecalculate(attacker, XAttribute.Attack, 10f);
				SetBaseAndRecalculate(attacker, XAttribute.Accuracy, 100f);
				SetBaseAndRecalculate(attacker, XAttribute.CriticalChance, 100f);
				SetBaseAndRecalculate(attacker, XAttribute.CriticalMultiplier, 200f);
				SetBaseAndRecalculate(defender, XAttribute.Defense, 3f);
				SetBaseAndRecalculate(defender, XAttribute.Dodge, 0f);
				Battle battle = tabletop.StartBattle(new[] { attacker.Id }, new[] { defender.Id });
				AbilitySystemDamageResolvedPresentationEvent? resolvedEvent = null;
				void OnDamageResolved(AbilitySystemDamageResolvedPresentationEvent damageEvent)
				{
					if (ReferenceEquals(damageEvent.TargetAbilitySystem, defender.AbilitySystem))
					{
						resolvedEvent = damageEvent;
					}
				}

				EventKit.Type.Register<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				try
				{
					tabletop.AdvanceRealTime(1f);
					AdvanceGasWorldUntil(() => Mathf.RoundToInt(defender.CurrentHealth) == 78);
				}
				finally
				{
					EventKit.Type.UnRegister<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				}

				Assert.That(battle.HasParticipant(attacker.Id), Is.True);
				Assert.That(Mathf.RoundToInt(defender.CurrentHealth), Is.EqualTo(78));
				Assert.That(resolvedEvent.HasValue, Is.True);
				Assert.That(resolvedEvent.Value.AppliedDamage, Is.EqualTo(22));
				Assert.That(resolvedEvent.Value.IsCriticalHit, Is.True);
				Assert.That(resolvedEvent.Value.IsMissed, Is.False);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void AdvanceRealTime_RangedAutomaticAttackWaitsForProjectileBeforeApplyingDamage()
		{
			SetInstanceField(GameManager.Config, "m_canCriticalHit", false);
			SetInstanceField(GameManager.Config, "m_canMissHit", false);
			CharacterCardDefinition rangedDefinition = CreateCharacterCard(
				"test.battle.projectile.ranged",
				1004);
			CharacterCardDefinition meleeDefinition = CreateCharacterCard(
				"test.battle.projectile.melee",
				1001);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { rangedDefinition, meleeDefinition });
				RuntimeTabletop tabletop = CreateBattleTabletop(contentIndex, seed: 24680u);
				CharacterCard attacker = (CharacterCard)tabletop.CreateCard(
					rangedDefinition.ContentId,
					Vector2.zero);
				CharacterCard defender = (CharacterCard)tabletop.CreateCard(
					meleeDefinition.ContentId,
					Vector2.one);
				SetBaseAndRecalculate(attacker, XAttribute.Attack, 20f);
				SetBaseAndRecalculate(attacker, XAttribute.Accuracy, 100f);
				SetBaseAndRecalculate(defender, XAttribute.Defense, 4f);
				SetBaseAndRecalculate(defender, XAttribute.Dodge, 0f);
				Battle battle = tabletop.StartBattle(new[] { attacker.Id }, new[] { defender.Id });
				AbilitySystemDamageResolvedPresentationEvent? resolvedEvent = null;
				void OnDamageResolved(AbilitySystemDamageResolvedPresentationEvent damageEvent)
				{
					if (ReferenceEquals(damageEvent.TargetAbilitySystem, defender.AbilitySystem))
					{
						resolvedEvent = damageEvent;
					}
				}

				EventKit.Type.Register<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				try
				{
					tabletop.AdvanceRealTime(1f);

					Assert.That(
						battle.TryGetPendingAttackPresentation(out BattleAttackPresentation presentation),
						Is.True);
					Assert.That(presentation.SourceCardId, Is.EqualTo(attacker.Id));
					Assert.That(presentation.TargetCardId, Is.EqualTo(defender.Id));
					Assert.That(presentation.CombatTypeTagCode, Is.EqualTo(XTag.Combat_Ranged));
					Assert.That(presentation.DurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
					Assert.That(Mathf.RoundToInt(defender.CurrentHealth), Is.EqualTo(100));
					Assert.That(resolvedEvent.HasValue, Is.False);

					tabletop.AdvanceRealTime(0.49f);

					Assert.That(
						battle.TryGetPendingAttackPresentation(out _),
						Is.True);
					Assert.That(Mathf.RoundToInt(defender.CurrentHealth), Is.EqualTo(100));
					Assert.That(resolvedEvent.HasValue, Is.False);

					tabletop.AdvanceRealTime(0.02f);

					Assert.That(
						battle.TryGetPendingAttackPresentation(out _),
						Is.False);
					Assert.That(Mathf.RoundToInt(defender.CurrentHealth), Is.EqualTo(100));
					Assert.That(resolvedEvent.HasValue, Is.False);
					AdvanceGasWorldUntil(() => resolvedEvent.HasValue);
				}
				finally
				{
					EventKit.Type.UnRegister<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				}

				Assert.That(Mathf.RoundToInt(defender.CurrentHealth), Is.EqualTo(85));
				Assert.That(resolvedEvent.HasValue, Is.True);
				Assert.That(resolvedEvent.Value.AppliedDamage, Is.EqualTo(15));
				Assert.That(resolvedEvent.Value.MatchupResult, Is.EqualTo(DamageMatchupResult.Disadvantage));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(rangedDefinition);
				UnityEngine.Object.DestroyImmediate(meleeDefinition);
			}
		}

		[Test]
		public void AdvanceRealTime_MeleeAutomaticAttackExposesAttackStartedPresentationWithoutProjectile()
		{
			SetInstanceField(GameManager.Config, "m_canCriticalHit", false);
			SetInstanceField(GameManager.Config, "m_canMissHit", false);
			CharacterCardDefinition definition = CreateCharacterCard(
				"test.battle.presentation.melee",
				1001);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { definition });
				RuntimeTabletop tabletop = CreateBattleTabletop(contentIndex, seed: 36912u);
				CharacterCard attacker = (CharacterCard)tabletop.CreateCard(
					definition.ContentId,
					Vector2.zero);
				CharacterCard defender = (CharacterCard)tabletop.CreateCard(
					definition.ContentId,
					Vector2.one);
				SetBaseAndRecalculate(attacker, XAttribute.Attack, 20f);
				SetBaseAndRecalculate(attacker, XAttribute.Accuracy, 100f);
				SetBaseAndRecalculate(defender, XAttribute.Defense, 4f);
				SetBaseAndRecalculate(defender, XAttribute.Dodge, 0f);
				Battle battle = tabletop.StartBattle(new[] { attacker.Id }, new[] { defender.Id });

				tabletop.AdvanceRealTime(1f);

				Assert.That(
					battle.TryConsumeAttackStartedPresentation(out BattleAttackPresentation presentation),
					Is.True);
				Assert.That(presentation.SourceCardId, Is.EqualTo(attacker.Id));
				Assert.That(presentation.TargetCardId, Is.EqualTo(defender.Id));
				Assert.That(presentation.CombatTypeTagCode, Is.EqualTo(XTag.Combat_Melee));
				Assert.That(presentation.DurationSeconds, Is.EqualTo(0f));
				Assert.That(presentation.RemainingSeconds, Is.EqualTo(0f));
				Assert.That(
					battle.TryConsumeAttackStartedPresentation(out _),
					Is.False);
				Assert.That(
					battle.TryGetPendingAttackPresentation(out _),
					Is.False);
				Assert.That(
					battle.TryGetExecutingAttackPresentation(out BattleAttackPresentation executing),
					Is.True);
				Assert.That(executing.CombatTypeTagCode, Is.EqualTo(XTag.Combat_Melee));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void RequestBattleAbilityActivation_AppliesConfiguredRpsAdvantageFromGasTags()
		{
			SetInstanceField(GameManager.Config, "m_canCriticalHit", false);
			SetInstanceField(GameManager.Config, "m_canMissHit", false);
			CharacterCardDefinition meleeDefinition = CreateCharacterCard("test.battle.rps.melee", 1001);
			CharacterCardDefinition rangedDefinition = CreateCharacterCard("test.battle.rps.ranged", 1004);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { meleeDefinition, rangedDefinition });
				RuntimeTabletop tabletop = CreateBattleTabletop(contentIndex, seed: 2468u);
				CharacterCard attacker = (CharacterCard)tabletop.CreateCard(meleeDefinition.ContentId, Vector2.zero);
				CharacterCard defender = (CharacterCard)tabletop.CreateCard(rangedDefinition.ContentId, Vector2.one);
				SetBaseAndRecalculate(attacker, XAttribute.Attack, 20f);
				SetBaseAndRecalculate(defender, XAttribute.Defense, 4f);
				Battle battle = tabletop.StartBattle(new[] { attacker.Id }, new[] { defender.Id });
				AbilitySystemDamageResolvedPresentationEvent? resolvedEvent = null;
				void OnDamageResolved(AbilitySystemDamageResolvedPresentationEvent damageEvent)
				{
					if (ReferenceEquals(damageEvent.TargetAbilitySystem, defender.AbilitySystem))
					{
						resolvedEvent = damageEvent;
					}
				}

				EventKit.Type.Register<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				AbilityActivationResult result;
				try
				{
					result = tabletop.RequestBattleAbilityActivation(
						battle,
						attacker.Id,
						defender.Id,
						attacker.AutomaticBattleAbilityCode);
					AdvanceGasWorldUntil(() => Mathf.RoundToInt(defender.CurrentHealth) == 70);
				}
				finally
				{
					EventKit.Type.UnRegister<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				}

				Assert.That(result, Is.EqualTo(AbilityActivationResult.Success));
				Assert.That(Mathf.RoundToInt(defender.CurrentHealth), Is.EqualTo(70));
				Assert.That(resolvedEvent.HasValue, Is.True);
				Assert.That(resolvedEvent.Value.AppliedDamage, Is.EqualTo(30));
				Assert.That(resolvedEvent.Value.MatchupResult, Is.EqualTo(DamageMatchupResult.Advantage));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(meleeDefinition);
				UnityEngine.Object.DestroyImmediate(rangedDefinition);
			}
		}

		[Test]
		public void RequestBattleAbilityActivation_AppliesConfiguredRpsDisadvantageFromGasTags()
		{
			SetInstanceField(GameManager.Config, "m_canCriticalHit", false);
			SetInstanceField(GameManager.Config, "m_canMissHit", false);
			CharacterCardDefinition meleeDefinition = CreateCharacterCard("test.battle.rps.target-melee", 1001);
			CharacterCardDefinition rangedDefinition = CreateCharacterCard("test.battle.rps.source-ranged", 1004);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { meleeDefinition, rangedDefinition });
				RuntimeTabletop tabletop = CreateBattleTabletop(contentIndex, seed: 1357u);
				CharacterCard attacker = (CharacterCard)tabletop.CreateCard(rangedDefinition.ContentId, Vector2.zero);
				CharacterCard defender = (CharacterCard)tabletop.CreateCard(meleeDefinition.ContentId, Vector2.one);
				SetBaseAndRecalculate(attacker, XAttribute.Attack, 20f);
				SetBaseAndRecalculate(defender, XAttribute.Defense, 4f);
				Battle battle = tabletop.StartBattle(new[] { attacker.Id }, new[] { defender.Id });
				AbilitySystemDamageResolvedPresentationEvent? resolvedEvent = null;
				void OnDamageResolved(AbilitySystemDamageResolvedPresentationEvent damageEvent)
				{
					if (ReferenceEquals(damageEvent.TargetAbilitySystem, defender.AbilitySystem))
					{
						resolvedEvent = damageEvent;
					}
				}

				EventKit.Type.Register<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				AbilityActivationResult result;
				try
				{
					result = tabletop.RequestBattleAbilityActivation(
						battle,
						attacker.Id,
						defender.Id,
						attacker.AutomaticBattleAbilityCode);
					AdvanceGasWorldUntil(() => Mathf.RoundToInt(defender.CurrentHealth) == 85);
				}
				finally
				{
					EventKit.Type.UnRegister<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				}

				Assert.That(result, Is.EqualTo(AbilityActivationResult.Success));
				Assert.That(Mathf.RoundToInt(defender.CurrentHealth), Is.EqualTo(85));
				Assert.That(resolvedEvent.HasValue, Is.True);
				Assert.That(resolvedEvent.Value.AppliedDamage, Is.EqualTo(15));
				Assert.That(resolvedEvent.Value.MatchupResult, Is.EqualTo(DamageMatchupResult.Disadvantage));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(meleeDefinition);
				UnityEngine.Object.DestroyImmediate(rangedDefinition);
			}
		}

		[Test]
		public void RequestBattleAbilityActivation_UsesGnsAccuracyAndDodgeForMiss()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.gns-miss");
			try
			{
				RuntimeTabletop tabletop = CreateBattleTabletop(
					ContentIndex.Build(new ContentAsset[] { definition }),
					seed: 9876u);
				CharacterCard attacker = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.zero);
				CharacterCard defender = (CharacterCard)tabletop.CreateCard(definition.ContentId, Vector2.one);
				SetBaseAndRecalculate(attacker, XAttribute.Attack, 10f);
				SetBaseAndRecalculate(attacker, XAttribute.Accuracy, 0f);
				SetBaseAndRecalculate(attacker, XAttribute.CriticalChance, 100f);
				SetBaseAndRecalculate(attacker, XAttribute.CriticalMultiplier, 200f);
				SetBaseAndRecalculate(defender, XAttribute.Defense, 0f);
				SetBaseAndRecalculate(defender, XAttribute.Dodge, 100f);
				Battle battle = tabletop.StartBattle(new[] { attacker.Id }, new[] { defender.Id });
				AbilitySystemDamageResolvedPresentationEvent? resolvedEvent = null;
				void OnDamageResolved(AbilitySystemDamageResolvedPresentationEvent damageEvent)
				{
					if (ReferenceEquals(damageEvent.TargetAbilitySystem, defender.AbilitySystem))
					{
						resolvedEvent = damageEvent;
					}
				}

				EventKit.Type.Register<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				AbilityActivationResult result;
				try
				{
					result = tabletop.RequestBattleAbilityActivation(
						battle,
						attacker.Id,
						defender.Id,
						XAbility.ABILITY_TabletopBasicAttack);
					AdvanceGasWorld(6);
				}
				finally
				{
					EventKit.Type.UnRegister<AbilitySystemDamageResolvedPresentationEvent>(OnDamageResolved);
				}

				Assert.That(result, Is.EqualTo(AbilityActivationResult.Success));
				Assert.That(Mathf.RoundToInt(defender.CurrentHealth), Is.EqualTo(100));
				Assert.That(resolvedEvent.HasValue, Is.True);
				Assert.That(resolvedEvent.Value.AppliedDamage, Is.EqualTo(0));
				Assert.That(resolvedEvent.Value.IsMissed, Is.True);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(definition);
			}
		}

		private static CharacterCardDefinition CreateCharacterCard(
			string contentId,
			int abilitySystemPresetId = 1001,
			int automaticBattleAbilityCode = 20005,
			float automaticMovementIntervalSeconds = 0f,
			float automaticMovementRadius = 0f,
			int automaticMovementMaxAttempts = 0,
			float automaticAggroRadius = 0f,
			float automaticAttackRadius = 0f)
		{
			CharacterCardDefinition definition = ScriptableObject.CreateInstance<CharacterCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}," +
				"\"m_abilitySystemPresetId\":" + abilitySystemPresetId +
				",\"m_automaticBattleAbilityCode\":" + automaticBattleAbilityCode +
				",\"m_automaticMovementIntervalSeconds\":" + automaticMovementIntervalSeconds +
				",\"m_automaticMovementRadius\":" + automaticMovementRadius +
				",\"m_automaticMovementMaxAttempts\":" + automaticMovementMaxAttempts +
				",\"m_automaticAggroRadius\":" + automaticAggroRadius +
				",\"m_automaticAttackRadius\":" + automaticAttackRadius + "}",
				definition);
			return definition;
		}

		private static CardDefinition CreateOrdinaryCard(string contentId)
		{
			CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			return definition;
		}

		private static ActionDefinition CreateAction(string contentId, string participantContentId)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}," +
				"\"m_turnCost\":2," +
				"\"m_participationSlots\":[{\"m_key\":\"participants\"," +
				"\"m_minimumParticipants\":2,\"m_maximumParticipants\":2," +
				"\"m_allowedContentIds\":[{\"m_value\":\"" + participantContentId + "\"}]}]}",
				definition);
			return definition;
		}

		private static ActionCandidate CreateReadyActionCandidate(
			RuntimeTabletop tabletop,
			ContentIndex contentIndex,
			ActionDefinition action,
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId)
		{
			TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(
				sourceCardId,
				Vector2.zero,
				Vector2.right,
				Vector2.zero,
				isDrag: true,
				targetCardId);
			ActionCandidate[] candidates = ActionCandidateResolver.FindCandidates(
				intent,
				tabletop.Cards,
				contentIndex,
				new[] { action });
			Assert.That(candidates, Has.Length.EqualTo(1));
			Assert.That(candidates[0].IsReady, Is.True);
			return candidates[0];
		}

		private static RuntimeTabletop CreateBattleTabletop(
			ContentIndex contentIndex,
			uint seed = 12345u,
			bool initializeRandom = true)
		{
			RuntimeTabletop tabletop = new RuntimeTabletop(
				contentIndex,
				TabletopTestPlacement.Rules,
				_ => false,
				(_, __) => { },
				_ => { },
				new BattleFormationRules(
					new BattleSideFormationRules(new Vector2(-1f, 0f), Vector2.right, Vector2.down, 2),
					new BattleSideFormationRules(new Vector2(1f, 0f), Vector2.left, Vector2.up, 2)));
			if (initializeRandom)
			{
				tabletop.InitializeAuthoritativeRandom(seed);
			}
			return tabletop;
		}

		private void CreateGameManagerWithCombatConfig(bool canCriticalHit, bool canMissHit)
		{
			GameObject gameManagerObject = new("GameplayEditModeGameManager");
			GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
			GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
			DatabaseRegistry database = ScriptableObject.CreateInstance<DatabaseRegistry>();

			m_createdObjects.Add(config);
			m_createdObjects.Add(database);
			m_createdObjects.Add(gameManagerObject);

			SetInstanceField(config, "m_databaseRegistry", database);
			SetInstanceField(config, "m_canCriticalHit", canCriticalHit);
			SetInstanceField(config, "m_canMissHit", canMissHit);
			SetInstanceField(gameManager, "m_config", config);
			SetInstanceField(
				gameManager,
				"m_systems",
				Activator.CreateInstance(GetRequiredFieldType(typeof(GameManager), "m_systems")));
			SetStaticField(typeof(GameManager), "_instance", gameManager);
		}

		private static void SetBaseAndRecalculate(CharacterCard character, int attributeCode, float value)
		{
			character.AbilitySystem.SetAttrBaseValue(XAttrSet.FightUnit, attributeCode, value);
			AttributeHelper.RecalculateCurrentValue(
				character.AbilitySystem.Entity,
				XAttrSet.FightUnit,
				attributeCode);
		}

		private static void AdvanceGasWorldUntil(Func<bool> predicate, int maxTicks = 8)
		{
			Assert.IsNotNull(predicate, "GAS world 轮询条件不能为空。");
			if (predicate())
			{
				return;
			}
			for (int i = 0; i < maxTicks; i++)
			{
				AdvanceGasWorld();
				if (predicate())
				{
					return;
				}
			}
			Assert.Fail($"推进 EX-GAS world {maxTicks} 次后，目标条件仍未满足。");
		}

		private static void AdvanceGasWorld(int ticks = 1)
		{
			Assert.GreaterOrEqual(ticks, 0, "GAS world 推进次数不能为负数。");
			for (int i = 0; i < ticks; i++)
			{
				Assert.IsNotNull(GASManager.ExWorld, "EX-GAS world 尚未初始化。");
				Assert.IsTrue(GASManager.ExWorld.IsCreated, "EX-GAS world 未创建。");
				AdvanceActiveGasTimelinesForEditMode();
				GASManager.ExWorld.GetExistingSystemManaged<SGLogic>().Update();
				GASManager.ExWorld.GetExistingSystemManaged<SysGrpDisplay>().Update();
			}
		}

		private static void AdvanceActiveGasTimelinesForEditMode()
		{
			if (Application.isPlaying || !GASManager.IsInitialized)
			{
				return;
			}

			EntityManager entityManager = GASManager.EntityManager;
			using EntityQuery query = entityManager.CreateEntityQuery(
				ComponentType.ReadOnly<CAbilityActive>(),
				ComponentType.ReadOnly<MCAbilityLogic>());
			using NativeArray<UEntity> abilityEntities = query.ToEntityArray(Allocator.Temp);

			for (int i = 0; i < abilityEntities.Length; i++)
			{
				MCAbilityLogic abilityLogic = entityManager.GetComponentData<MCAbilityLogic>(abilityEntities[i]);
				if (abilityLogic?.Logic == null ||
					abilityLogic.Logic.GetType().FullName != "GAS.Runtime.ALTimeline")
				{
					continue;
				}
				AdvanceTimelinePlayerOneFrame(abilityLogic.Logic);
			}
		}

		private static void AdvanceTimelinePlayerOneFrame(AbilityLogicBase abilityLogic)
		{
			FieldInfo playerField = abilityLogic.GetType().GetField(
				"_player",
				BindingFlags.Instance | BindingFlags.NonPublic);
			object player = playerField?.GetValue(abilityLogic);
			Assert.IsNotNull(player, "ALTimeline 缺少 _player，无法在 EditMode 手动推进时间轴。");

			Type playerType = player.GetType();
			PropertyInfo isPlayingProperty = playerType.GetProperty(
				"IsPlaying",
				BindingFlags.Instance | BindingFlags.Public);
			if (isPlayingProperty?.GetValue(player) is not bool isPlaying || !isPlaying)
			{
				return;
			}

			FieldInfo currentFrameField = playerType.GetField(
				"_currentFrame",
				BindingFlags.Instance | BindingFlags.NonPublic);
			PropertyInfo lifeTimeProperty = playerType.GetProperty(
				"LifeTime",
				BindingFlags.Instance | BindingFlags.NonPublic);
			MethodInfo tickFrameMethod = playerType.GetMethod(
				"TickFrame",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(currentFrameField, "ALTimelinePlayer 缺少 _currentFrame。");
			Assert.IsNotNull(lifeTimeProperty, "ALTimelinePlayer 缺少 LifeTime。");
			Assert.IsNotNull(tickFrameMethod, "ALTimelinePlayer 缺少 TickFrame。");

			int currentFrame = currentFrameField.GetValue(player) is int frame ? frame : -1;
			int lifeTime = lifeTimeProperty.GetValue(player) is int value ? value : 0;
			if (currentFrame >= lifeTime)
			{
				return;
			}

			int nextFrame = currentFrame + 1;
			currentFrameField.SetValue(player, nextFrame);
			tickFrameMethod.Invoke(player, new object[] { nextFrame });

			if (nextFrame < lifeTime)
			{
				return;
			}

			MethodInfo onPlayEndMethod = playerType.GetMethod(
				"OnPlayEnd",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(onPlayEndMethod, "ALTimelinePlayer 缺少 OnPlayEnd。");
			currentFrameField.SetValue(player, nextFrame + 1);
			onPlayEndMethod.Invoke(player, null);
		}

		private static Battle CreateTwoCharacterBattle(
			RuntimeTabletop tabletop,
			ContentId contentId,
			Vector2 center)
		{
			CharacterCard ally = (CharacterCard)tabletop.CreateCard(contentId, center);
			CharacterCard enemy = (CharacterCard)tabletop.CreateCard(contentId, center);
			return tabletop.StartBattle(new[] { ally.Id }, new[] { enemy.Id });
		}

		private static void InvokeFormalGasBootstrap(string methodName)
		{
			Type bootstrapType = typeof(GameManager).Assembly.GetType(
				"GameCore.FormalAbilityRuntimeBootstrap",
				throwOnError: true);
			MethodInfo method = bootstrapType.GetMethod(
				methodName,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				throw new InvalidOperationException($"找不到 FormalAbilityRuntimeBootstrap.{methodName}。");
			}
			method.Invoke(null, null);
		}

		private static void SetInstanceField(object target, string fieldName, object value)
		{
			Assert.IsNotNull(target, $"目标对象为空，无法写入字段 {fieldName}");
			FieldInfo field = FindInstanceField(target.GetType(), fieldName);
			Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
			field.SetValue(target, value);
		}

		private static void SetStaticField(Type type, string fieldName, object value)
		{
			FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(field, $"找不到静态字段 {type.Name}.{fieldName}");
			field.SetValue(null, value);
		}

		private static Type GetRequiredFieldType(Type type, string fieldName)
		{
			FieldInfo field = FindInstanceField(type, fieldName);
			Assert.IsNotNull(field, $"找不到字段 {type.Name}.{fieldName}");
			return field.FieldType;
		}

		private static FieldInfo FindInstanceField(Type type, string fieldName)
		{
			while (type != null)
			{
				FieldInfo field = type.GetField(
					fieldName,
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (field != null)
				{
					return field;
				}
				type = type.BaseType;
			}
			return null;
		}

	}
}
