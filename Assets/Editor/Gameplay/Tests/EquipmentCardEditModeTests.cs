using System;
using System.Collections.Generic;
using System.Linq;
using GAS.Runtime;
using GameCore;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	public sealed class EquipmentCardEditModeTests
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
		public void ScenarioRun_EquipAppliesGameplayEffectAndRemovesEquipmentFromTabletop()
		{
			using EquipmentScenarioContext context = CreateScenario();
			CharacterCard character = (CharacterCard)context.Run.Tabletop.CreateCard(
				context.Character.ContentId,
				new Vector2(1f, 0f));
			TabletopCard equipment = context.Run.Tabletop.CreateCard(
				context.FirstEquipment.ContentId,
				new Vector2(-1f, 0f));

			ActionCandidate candidate = context.FindCandidates(equipment, character).Single();
			context.Run.StartAction(ActionRequest.FromCandidate(candidate));
			TickGasWorld();

			Assert.That(context.Run.Tabletop.Cards.TryGetCard(equipment.Id, out _), Is.False);
			Assert.That(character.TryGetEquippedCard(context.WeaponSlot.ContentId, out EquippedCardState equipped), Is.True);
			Assert.That(character.EquippedCardCount, Is.EqualTo(1));
			Assert.That(character.EquippedCards.Single(), Is.SameAs(equipped));
			Assert.That(equipped.CardSnapshot.CardId, Is.EqualTo(equipment.Id));
			Assert.That(character.AbilitySystem.HasTag(XTag.State_Buff_SpeedUp), Is.True);
		}

		[Test]
		public void ScenarioRun_ReplacesSameSlotEquipmentAndReturnsOldCardToTabletop()
		{
			using EquipmentScenarioContext context = CreateScenario();
			CharacterCard character = (CharacterCard)context.Run.Tabletop.CreateCard(
				context.Character.ContentId,
				new Vector2(1f, 0f));
			TabletopCard first = context.Run.Tabletop.CreateCard(
				context.FirstEquipment.ContentId,
				new Vector2(-1f, 0f));
			TabletopCard second = context.Run.Tabletop.CreateCard(
				context.SecondEquipment.ContentId,
				new Vector2(-2f, 0f));

			context.Run.StartAction(ActionRequest.FromCandidate(context.FindCandidates(first, character).Single()));
			context.Run.StartAction(ActionRequest.FromCandidate(context.FindCandidates(second, character).Single()));

			Assert.That(context.Run.Tabletop.Cards.TryGetCard(first.Id, out TabletopCard returned), Is.True);
			Assert.That(returned.ContentId, Is.EqualTo(context.FirstEquipment.ContentId));
			Assert.That(context.Run.Tabletop.Cards.TryGetCard(second.Id, out _), Is.False);
			Assert.That(character.TryGetEquippedCard(context.WeaponSlot.ContentId, out EquippedCardState equipped), Is.True);
			Assert.That(equipped.CardSnapshot.CardId, Is.EqualTo(second.Id));
		}

		[Test]
		public void ScenarioRun_UnequipReturnsEquipmentToTabletopAndRemovesGameplayEffect()
		{
			using EquipmentScenarioContext context = CreateScenario();
			CharacterCard character = (CharacterCard)context.Run.Tabletop.CreateCard(
				context.Character.ContentId,
				new Vector2(1f, 0f));
			TabletopCard equipment = context.Run.Tabletop.CreateCard(
				context.FirstEquipment.ContentId,
				new Vector2(-1f, 0f));

			context.Run.StartAction(ActionRequest.FromCandidate(context.FindCandidates(equipment, character).Single()));
			TickGasWorld();
			Assert.That(character.AbilitySystem.HasTag(XTag.State_Buff_SpeedUp), Is.True);
			ActionCandidate unequipCandidate = context.FindClickCandidates(character).Single();
			context.Run.StartAction(ActionRequest.FromCandidate(unequipCandidate));
			TickGasWorld();

			Assert.That(character.TryGetEquippedCard(context.WeaponSlot.ContentId, out _), Is.False);
			Assert.That(character.EquippedCardCount, Is.Zero);
			Assert.That(context.Run.Tabletop.Cards.TryGetCard(equipment.Id, out TabletopCard returned), Is.True);
			Assert.That(returned.ContentId, Is.EqualTo(context.FirstEquipment.ContentId));
			Assert.That(character.AbilitySystem.HasTag(XTag.State_Buff_SpeedUp), Is.False);
		}

		[Test]
		public void ScenarioRun_EquippedCardRestoresFromFullRunSnapshotAndReappliesGameplayEffect()
		{
			using EquipmentScenarioContext context = CreateScenario();
			CharacterCard character = (CharacterCard)context.Run.Tabletop.CreateCard(
				context.Character.ContentId,
				new Vector2(1f, 0f));
			TabletopCard equipment = context.Run.Tabletop.CreateCard(
				context.FirstEquipment.ContentId,
				new Vector2(-1f, 0f));
			context.Run.StartAction(ActionRequest.FromCandidate(context.FindCandidates(equipment, character).Single()));

			ScenarioRunSnapshot snapshot = JsonUtility.FromJson<ScenarioRunSnapshot>(
				JsonUtility.ToJson(context.Run.CreateSnapshot()));
			ScenarioRun restored = ScenarioRun.Restore(context.Scenario, context.Content, snapshot);
			TickGasWorld();
			try
			{
				Assert.That(restored.Tabletop.Cards.TryGetCard(character.Id, out TabletopCard restoredCard), Is.True);
				CharacterCard restoredCharacter = (CharacterCard)restoredCard;
				Assert.That(restoredCharacter.TryGetEquippedCard(context.WeaponSlot.ContentId, out EquippedCardState restoredEquipped), Is.True);
				Assert.That(restoredCharacter.EquippedCardCount, Is.EqualTo(1));
				Assert.That(restoredCharacter.EquippedCards.Single(), Is.SameAs(restoredEquipped));
				Assert.That(restoredEquipped.CardSnapshot.CardId, Is.EqualTo(equipment.Id));
				Assert.That(restored.Tabletop.Cards.TryGetCard(equipment.Id, out _), Is.False);
				Assert.That(restoredCharacter.AbilitySystem.HasTag(XTag.State_Buff_SpeedUp), Is.True);
			}
			finally
			{
				restored.End();
			}
		}

		[Test]
		public void ScenarioRun_EquipCompletesEquipmentQuestFact()
		{
			using EquipmentScenarioContext context = CreateScenario(includeEquipQuest: true);
			CharacterCard character = (CharacterCard)context.Run.Tabletop.CreateCard(
				context.Character.ContentId,
				new Vector2(1f, 0f));
			TabletopCard equipment = context.Run.Tabletop.CreateCard(
				context.FirstEquipment.ContentId,
				new Vector2(-1f, 0f));

			context.Run.StartAction(ActionRequest.FromCandidate(context.FindCandidates(equipment, character).Single()));

			QuestProgress quest = context.Run.QuestLog.GetQuest(context.EquipQuest.ContentId);
			Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed));
			Assert.That(quest.Tasks[0].Progress.CurrentAmount, Is.EqualTo(1));
		}

		private static EquipmentScenarioContext CreateScenario(bool includeEquipQuest = false)
		{
			EquipmentSlotDefinition weaponSlot = CreateEquipmentSlot("test.equipment.slot.weapon");
			CharacterCardDefinition character = CreateCharacter("test.equipment.character");
			EquipmentCardDefinition firstEquipment = CreateEquipment(
				"test.equipment.first",
				weaponSlot.ContentId);
			EquipmentCardDefinition secondEquipment = CreateEquipment(
				"test.equipment.second",
				weaponSlot.ContentId);
			ActionDefinition equipAction = CreateEquipAction(
				"test.equipment.equip",
				character.ContentId,
				firstEquipment.ContentId,
				secondEquipment.ContentId);
			ActionDefinition unequipAction = CreateUnequipAction(
				"test.equipment.unequip",
				character.ContentId,
				weaponSlot.ContentId);
			QuestDefinition equipQuest = includeEquipQuest
				? CreateEquipmentQuest("test.equipment.quest", firstEquipment.ContentId)
				: null;
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.equipment.region\"}}",
				region);
			ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
			string questIdsJson = includeEquipQuest
				? ",\"m_questIds\":[{\"m_value\":\"" + equipQuest.ContentId.Value + "\"}]"
				: string.Empty;
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.equipment.scenario\"}," +
				"\"m_initialRegionId\":{\"m_value\":\"test.equipment.region\"}," +
				"\"m_regionIds\":[{\"m_value\":\"test.equipment.region\"}]" +
				questIdsJson + "}",
				scenario);

			List<ContentAsset> contentAssets = new List<ContentAsset>
			{
				weaponSlot,
				character,
				firstEquipment,
				secondEquipment,
				equipAction,
				unequipAction,
				region,
				scenario
			};
			List<Object> disposableAssets = new List<Object>
			{
				weaponSlot,
				character,
				firstEquipment,
				secondEquipment,
				equipAction,
				unequipAction,
				region,
				scenario
			};
			if (includeEquipQuest)
			{
				contentAssets.Add(equipQuest);
				disposableAssets.Add(equipQuest);
			}
			ContentIndex content = ContentIndex.Build(contentAssets);
			ScenarioRun run = new ScenarioRun(scenario, content, 27182u);
			run.DiscoverContent(equipAction.ContentId);
			run.DiscoverContent(unequipAction.ContentId);
			if (includeEquipQuest)
			{
				run.ActivateInitialQuests();
			}
			return new EquipmentScenarioContext(
				run,
				disposableAssets.ToArray(),
				content,
				scenario,
				weaponSlot,
				character,
				firstEquipment,
				secondEquipment,
				equipQuest);
		}

		private static EquipmentSlotDefinition CreateEquipmentSlot(string contentId)
		{
			EquipmentSlotDefinition slot = ScriptableObject.CreateInstance<EquipmentSlotDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}", slot);
			return slot;
		}

		private static CharacterCardDefinition CreateCharacter(string contentId)
		{
			CharacterCardDefinition definition = ScriptableObject.CreateInstance<CharacterCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}," +
				"\"m_abilitySystemPresetId\":1001}",
				definition);
			return definition;
		}

		private static EquipmentCardDefinition CreateEquipment(string contentId, ContentId slotId)
		{
			EquipmentCardDefinition definition = ScriptableObject.CreateInstance<EquipmentCardDefinition>();
			SerializedObject serialized = new SerializedObject(definition);
			serialized.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serialized.FindProperty("m_slotId").FindPropertyRelative("m_value").stringValue = slotId.Value;
			serialized.FindProperty("m_onEquippedGameplayEffectId").intValue = 1002;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateEquipmentQuest(string contentId, ContentId equipmentCardId)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serialized = new SerializedObject(definition);
			SerializedProperty tasks = serialized.FindProperty("m_tasks");
			tasks.arraySize = 1;
			SerializedProperty task = tasks.GetArrayElementAtIndex(0);
			task.managedReferenceValue = new CardEquipQuestTaskDefinition();
			task.FindPropertyRelative("m_equipmentCardId").FindPropertyRelative("m_value").stringValue =
				equipmentCardId.Value;
			task.FindPropertyRelative("m_requiredEquipCount").intValue = 1;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static ActionDefinition CreateEquipAction(
			string contentId,
			ContentId characterId,
			ContentId firstEquipmentId,
			ContentId secondEquipmentId)
		{
			ActionDefinition action = CreateActionCore(
				contentId,
				"{\"m_key\":\"equipment\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1," +
				"\"m_allowedContentIds\":[{\"m_value\":\"" + firstEquipmentId.Value + "\"},{\"m_value\":\"" + secondEquipmentId.Value + "\"}]}," +
				"{\"m_key\":\"character\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1," +
				"\"m_allowedContentIds\":[{\"m_value\":\"" + characterId.Value + "\"}]}");
			SerializedObject serialized = new SerializedObject(action);
			SetResult(serialized, 0, new EquipCardResultIntent(), "{\"m_equipmentSlotKey\":\"equipment\",\"m_characterSlotKey\":\"character\"}");
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return action;
		}

		private static ActionDefinition CreateUnequipAction(
			string contentId,
			ContentId characterId,
			ContentId slotId)
		{
			ActionDefinition action = CreateActionCore(
				contentId,
				"{\"m_key\":\"character\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1," +
				"\"m_allowedContentIds\":[{\"m_value\":\"" + characterId.Value + "\"}]}",
				canStartFromClick: true);
			SerializedObject serialized = new SerializedObject(action);
			SetResult(serialized, 0, new UnequipCardResultIntent(),
				"{\"m_characterSlotKey\":\"character\",\"m_equipmentSlotId\":{\"m_value\":\"" + slotId.Value + "\"}}");
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return action;
		}

		private static ActionDefinition CreateActionCore(
			string contentId,
			string slotsJson,
			bool canStartFromClick = false)
		{
			ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"},\"m_turnCost\":0," +
				"\"m_canStartFromClick\":" + (canStartFromClick ? "true" : "false") + "," +
				"\"m_participationSlots\":[" + slotsJson + "]}",
				action);
			return action;
		}

		private static void SetResult(SerializedObject serializedAction, int index, ActionResultIntent intent, string json)
		{
			JsonUtility.FromJsonOverwrite(json, intent);
			SerializedProperty results = serializedAction.FindProperty("m_resultIntents");
			results.arraySize = Math.Max(results.arraySize, index + 1);
			results.GetArrayElementAtIndex(index).managedReferenceValue = intent;
		}

		private static void TickGasWorld(int frameCount = 4)
		{
			Type gasManagerType = typeof(GASManager);
			object world = gasManagerType.GetProperty("ExWorld")?.GetValue(null);
			if (world == null)
			{
				throw new InvalidOperationException("EX-GAS World 尚未初始化，不能推进装备效果生命周期。");
			}
			object isCreated = world.GetType().GetProperty("IsCreated")?.GetValue(world);
			if (isCreated is not true)
			{
				throw new InvalidOperationException("EX-GAS World 尚未创建，不能推进装备效果生命周期。");
			}
			System.Reflection.MethodInfo update = world.GetType().GetMethod("Update", Type.EmptyTypes)
				?? throw new InvalidOperationException("EX-GAS World 缺少 Update 入口。");
			for (int i = 0; i < frameCount; i++)
			{
				update.Invoke(world, null);
			}
		}

		private static void InvokeFormalGasBootstrap(string methodName)
		{
			Type bootstrapType = typeof(GameManager).Assembly.GetType(
				"GameCore.FormalAbilityRuntimeBootstrap",
				throwOnError: true);
			System.Reflection.MethodInfo method = bootstrapType.GetMethod(
				methodName,
				System.Reflection.BindingFlags.Static |
				System.Reflection.BindingFlags.Public |
				System.Reflection.BindingFlags.NonPublic);
			if (method == null)
			{
				throw new InvalidOperationException($"找不到 FormalAbilityRuntimeBootstrap.{methodName}。");
			}
			method.Invoke(null, null);
		}

		private sealed class EquipmentScenarioContext : IDisposable
		{
			private readonly Object[] m_assets;

			internal ScenarioRun Run { get; }
			internal ContentIndex Content { get; }
			internal ScenarioDefinition Scenario { get; }
			internal EquipmentSlotDefinition WeaponSlot { get; }
			internal CharacterCardDefinition Character { get; }
			internal EquipmentCardDefinition FirstEquipment { get; }
			internal EquipmentCardDefinition SecondEquipment { get; }
			internal QuestDefinition EquipQuest { get; }

			internal EquipmentScenarioContext(
				ScenarioRun run,
				Object[] assets,
				ContentIndex content,
				ScenarioDefinition scenario,
				EquipmentSlotDefinition weaponSlot,
				CharacterCardDefinition character,
				EquipmentCardDefinition firstEquipment,
				EquipmentCardDefinition secondEquipment,
				QuestDefinition equipQuest)
			{
				Run = run;
				m_assets = assets;
				Content = content;
				Scenario = scenario;
				WeaponSlot = weaponSlot;
				Character = character;
				FirstEquipment = firstEquipment;
				SecondEquipment = secondEquipment;
				EquipQuest = equipQuest;
			}

			internal ActionCandidate[] FindCandidates(TabletopCard source, TabletopCard target)
			{
				return Run.FindActionCandidates(new TabletopCardPointerReleaseIntent(
					source.Id,
					source.Position,
					target.Position,
					source.Position,
					isDrag: true,
					target.Id));
			}

			internal ActionCandidate[] FindClickCandidates(TabletopCard card)
			{
				return Run.FindActionCandidates(new TabletopCardPointerReleaseIntent(
					card.Id,
					card.Position,
					card.Position,
					card.Position,
					isDrag: false,
					default));
			}

			public void Dispose()
			{
				Run.End();
				for (int i = 0; i < m_assets.Length; i++)
				{
					Object.DestroyImmediate(m_assets[i]);
				}
			}
		}
	}
}




