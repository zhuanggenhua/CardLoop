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
	/// <summary>
	/// 验证剧本阵型只派生战斗表现位置，并由牌桌战斗生命周期保护参战卡牌的普通移动入口。
	/// </summary>
	public sealed class BattleFormationEditModeTests
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
		public void StartBattle_RequiresFormationOwnedByCurrentScenario()
		{
			CardDefinition playerDefinition = CreateCard("test.battle.formation-required.player");
			CardDefinition enemyDefinition = CreateCard("test.battle.formation-required.enemy");
			try
			{
				TabletopBoard tabletop = new TabletopBoard(
					ContentIndex.Build(new ContentAsset[] { playerDefinition, enemyDefinition }),
					TabletopTestPlacement.Rules,
					_ => { });
				TabletopCard player = tabletop.CreateCard(playerDefinition.ContentId, Vector2.zero);
				TabletopCard enemy = tabletop.CreateCard(enemyDefinition.ContentId, Vector2.right);

				InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
					tabletop.StartBattle(new[] { player.Id }, new[] { enemy.Id }));

				StringAssert.Contains("剧本没有配置战斗阵型规则", exception.Message);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(playerDefinition);
				UnityEngine.Object.DestroyImmediate(enemyDefinition);
			}
		}

		[Test]
		public void BattleFormation_DerivesRowsWithoutMutatingAuthoritativeCardPositions()
		{
			CardDefinition playerDefinition = CreateCard("test.battle.formation.player");
			CardDefinition enemyDefinition = CreateCard("test.battle.formation.enemy");
			try
			{
				TabletopBoard tabletop = CreateBattleTabletop(playerDefinition, enemyDefinition);
				TabletopCard playerOne = tabletop.CreateCard(playerDefinition.ContentId, Vector2.zero);
				TabletopCard playerTwo = tabletop.CreateCard(playerDefinition.ContentId, Vector2.zero);
				TabletopCard playerThree = tabletop.CreateCard(playerDefinition.ContentId, Vector2.zero);
				TabletopCard enemy = tabletop.CreateCard(enemyDefinition.ContentId, Vector2.zero);

				tabletop.StartBattle(
					new[] { playerOne.Id, playerTwo.Id, playerThree.Id },
					new[] { enemy.Id });

				Assert.That(tabletop.TryGetBattlePose(playerOne.Id, 100, out TabletopCardPose playerOnePose), Is.True);
				Assert.That(tabletop.TryGetBattlePose(playerTwo.Id, 100, out TabletopCardPose playerTwoPose), Is.True);
				Assert.That(tabletop.TryGetBattlePose(playerThree.Id, 100, out TabletopCardPose playerThreePose), Is.True);
				Assert.That(tabletop.TryGetBattlePose(enemy.Id, 100, out TabletopCardPose enemyPose), Is.True);
				Assert.That(playerOnePose.LocalPosition, Is.EqualTo(new Vector3(-2.5f, 0f, 0f)));
				Assert.That(playerTwoPose.LocalPosition, Is.EqualTo(new Vector3(-1.5f, 0f, 0f)));
				Assert.That(playerThreePose.LocalPosition, Is.EqualTo(new Vector3(-2f, -1f, 0f)));
				Assert.That(enemyPose.LocalPosition, Is.EqualTo(new Vector3(2f, 0f, 0f)));
				Assert.That(playerOnePose.SortingOrder, Is.EqualTo(100));
				Assert.That(enemyPose.SortingOrder, Is.EqualTo(103));
				Assert.That(tabletop.Cards.GetStackContaining(playerOne.Id).Position, Is.EqualTo(Vector2.zero));
				Assert.That(tabletop.Cards.GetStackContaining(enemy.Id).Position, Is.EqualTo(Vector2.zero));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(playerDefinition);
				UnityEngine.Object.DestroyImmediate(enemyDefinition);
			}
		}

		[Test]
		public void ActiveBattleParticipant_RejectsOrdinaryStackMovesAndMerges()
		{
			CardDefinition playerDefinition = CreateCard("test.battle.movement.player");
			CardDefinition enemyDefinition = CreateCard("test.battle.movement.enemy");
			try
			{
				TabletopBoard tabletop = CreateBattleTabletop(playerDefinition, enemyDefinition);
				TabletopCard player = tabletop.CreateCard(playerDefinition.ContentId, Vector2.zero);
				TabletopCard enemy = tabletop.CreateCard(enemyDefinition.ContentId, Vector2.right);
				TabletopCard bystander = tabletop.CreateCard(playerDefinition.ContentId, Vector2.left);
				tabletop.StartBattle(new[] { player.Id }, new[] { enemy.Id });

				Assert.Throws<InvalidOperationException>(() => tabletop.DetachStackAt(player.Id));
				Assert.Throws<InvalidOperationException>(() => tabletop.MergeStackOnto(bystander.Id, player.Id));
				Assert.Throws<InvalidOperationException>(() => tabletop.TryPlaceStack(
					player.Id,
					Vector2.up,
					out _));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(playerDefinition);
				UnityEngine.Object.DestroyImmediate(enemyDefinition);
			}
		}

		private static TabletopBoard CreateBattleTabletop(
			CardDefinition playerDefinition,
			CardDefinition enemyDefinition)
		{
			TabletopBoard tabletop = new TabletopBoard(
				ContentIndex.Build(new ContentAsset[] { playerDefinition, enemyDefinition }),
				TabletopTestPlacement.Rules,
				_ => { },
				new BattleFormationRules(
					new BattleSideFormationRules(
						new Vector2(-2f, 0f),
						Vector2.right,
						Vector2.down,
						2),
					new BattleSideFormationRules(
						new Vector2(2f, 0f),
						Vector2.left,
						Vector2.up,
						2)));
			tabletop.InitializeAuthoritativeRandom(12345u);
			return tabletop;
		}

		private static CharacterCardDefinition CreateCard(string contentId)
		{
			CharacterCardDefinition definition = ScriptableObject.CreateInstance<CharacterCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"},\"m_abilitySystemPresetId\":1001}",
				definition);
			return definition;
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
