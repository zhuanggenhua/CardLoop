using System;
using System.Reflection;
using GAS.Runtime;
using GameCore;
using Gameplay.Content;
using Gameplay.Tabletop;
using NUnit.Framework;
using UnityEngine;
using TabletopBoard = Gameplay.Tabletop.Tabletop;

namespace Gameplay.Tests
{
	/// <summary>验证战斗只接收拥有唯一 ASC 的角色卡，并直接拥有本场战斗方。</summary>
	public sealed class BattleEditModeTests
	{
		[SetUp]
		public void SetUp()
		{
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
		}

		[TearDown]
		public void TearDown()
		{
			InvokeFormalGasBootstrap("Shutdown");
		}

		[Test]
		public void CreateCard_UsesCharacterDefinitionAsTheOnlyAbilitySystemSource()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.character");
			try
			{
				TabletopBoard tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));

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
		public void StartBattle_OwnsExplicitSidesAndRejectsOrdinaryCards()
		{
			CharacterCardDefinition characterDefinition = CreateCharacterCard("test.battle.character-side");
			CardDefinition ordinaryDefinition = CreateOrdinaryCard("test.battle.ordinary");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(
					new ContentAsset[] { characterDefinition, ordinaryDefinition });
				TabletopBoard tabletop = CreateBattleTabletop(contentIndex);
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
				TabletopBoard tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
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
				TabletopBoard tabletop = CreateBattleTabletop(
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
		public void BattleAbilityRandom_IsDeterministicAndIndependentBetweenBattles()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.random-sequence");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { definition });
				TabletopBoard firstTabletop = CreateBattleTabletop(contentIndex, seed: 4711u);
				Battle firstBattle = CreateTwoCharacterBattle(firstTabletop, definition.ContentId);
				Battle unrelatedBattle = CreateTwoCharacterBattle(firstTabletop, definition.ContentId);
				uint firstSeed = firstBattle.TakeAbilityActivationSeed();
				uint secondSeed = firstBattle.TakeAbilityActivationSeed();
				_ = unrelatedBattle.TakeAbilityActivationSeed();

				TabletopBoard replayTabletop = CreateBattleTabletop(contentIndex, seed: 4711u);
				Battle replayBattle = CreateTwoCharacterBattle(replayTabletop, definition.ContentId);
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
				TabletopBoard tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
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
		public void CreateSnapshot_RejectsAnActiveBattleInsteadOfSavingPartialCombatState()
		{
			CharacterCardDefinition definition = CreateCharacterCard("test.battle.snapshot-rejected");
			try
			{
				TabletopBoard tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
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
				TabletopBoard tabletop = CreateBattleTabletop(ContentIndex.Build(new ContentAsset[] { definition }));
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

		private static CharacterCardDefinition CreateCharacterCard(string contentId)
		{
			CharacterCardDefinition definition = ScriptableObject.CreateInstance<CharacterCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"},\"m_abilitySystemPresetId\":1001}",
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

		private static TabletopBoard CreateBattleTabletop(
			ContentIndex contentIndex,
			uint seed = 12345u,
			bool initializeRandom = true)
		{
			TabletopBoard tabletop = new TabletopBoard(
				contentIndex,
				TabletopTestPlacement.Rules,
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

		private static Battle CreateTwoCharacterBattle(TabletopBoard tabletop, ContentId contentId)
		{
			CharacterCard ally = (CharacterCard)tabletop.CreateCard(contentId, Vector2.zero);
			CharacterCard enemy = (CharacterCard)tabletop.CreateCard(contentId, Vector2.one);
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

	}
}
