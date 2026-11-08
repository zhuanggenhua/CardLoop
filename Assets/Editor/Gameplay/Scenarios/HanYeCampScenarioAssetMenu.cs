using System;
using System.Collections.Generic;
using System.Linq;
using GAS.Runtime;
using GameCore;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Editor.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using UnityEditor;
using UnityEngine;

namespace Gameplay.Editor.Scenarios
{
	/// <summary>
	/// 生成首个内置剧本《寒夜营地》的作者资产。
	/// 这是编辑器完成前的可重复入口，不作为第二套长期内容维护方式。
	/// </summary>
	public static class HanYeCampScenarioAssetMenu
	{
		public const string ScenarioFolder = "Assets/Gameplay/Scenarios/寒夜营地";
		public const string ScenarioId = "scenario.hanye-camp";
		public const string RegionId = "scenario.hanye-camp.region";
		public const string ScoutId = "card.hanye-camp.scout-linxia";
		public const string MechanicId = "card.hanye-camp.mechanic-laozhou";
		public const string SurvivorAningId = "card.hanye-camp.survivor-aning";
		public const string SurvivorChengyuanId = "card.hanye-camp.survivor-chengyuan";
		public const string SurvivorWandererId = "card.hanye-camp.survivor-wanderer";
		public const string BaseServiceAreaId = "card.hanye-camp.base-service-area";
		public const string LocationStoreId = "card.hanye-camp.location-store";
		public const string LocationClinicId = "card.hanye-camp.location-clinic";
		public const string LocationDinerId = "card.hanye-camp.location-diner";
		public const string LocationHouseId = "card.hanye-camp.location-house";
		public const string FoodCannedId = "card.hanye-camp.food-canned";
		public const string FoodScrapsId = "card.hanye-camp.food-scraps";
		public const string ResourcePlankId = "card.hanye-camp.resource-plank";
		public const string ResourceMedicineId = "card.hanye-camp.resource-medicine";
		public const string ThreatBrokenFenceId = "card.hanye-camp.threat-broken-fence";
		public const string EventArgumentId = "card.hanye-camp.event-argument";
		public const string EventColdWaveId = "card.hanye-camp.event-cold-wave";
		public const string EventInfectionDoubtId = "card.hanye-camp.event-infection-doubt";
		public const string ExploreLocationActionId = "action.hanye-camp.explore-location";
		public const string ScavengeSuppliesActionId = "action.hanye-camp.scavenge-supplies";
		public const string DefendCampActionId = "action.hanye-camp.defend-camp";
		public const string StabilizeCampQuestId = "quest.hanye-camp.stabilize-camp";

		public const string ScenarioPath = ScenarioFolder + "/寒夜营地.asset";

		private const string RegionPath = ScenarioFolder + "/寒夜营地_初始地区.asset";
		private const string ScoutPath = ScenarioFolder + "/角色_斥候林夏.asset";
		private const string MechanicPath = ScenarioFolder + "/角色_维修员老周.asset";
		private const string SurvivorAningPath = ScenarioFolder + "/幸存者_学生阿宁.asset";
		private const string SurvivorChengyuanPath = ScenarioFolder + "/幸存者_伤员程远.asset";
		private const string SurvivorWandererPath = ScenarioFolder + "/幸存者_流浪幸存者.asset";
		private const string BaseServiceAreaPath = ScenarioFolder + "/基地_废弃服务区.asset";
		private const string LocationStorePath = ScenarioFolder + "/地点_便利店.asset";
		private const string LocationClinicPath = ScenarioFolder + "/地点_废弃诊所.asset";
		private const string LocationDinerPath = ScenarioFolder + "/地点_公路餐馆.asset";
		private const string LocationHousePath = ScenarioFolder + "/地点_民居.asset";
		private const string FoodCannedPath = ScenarioFolder + "/食物_罐头箱.asset";
		private const string FoodScrapsPath = ScenarioFolder + "/食物_零散食物.asset";
		private const string ResourcePlankPath = ScenarioFolder + "/资源_木板.asset";
		private const string ResourceMedicinePath = ScenarioFolder + "/资源_药品.asset";
		private const string ThreatBrokenFencePath = ScenarioFolder + "/威胁_破损栅栏.asset";
		private const string EventArgumentPath = ScenarioFolder + "/事件_争执.asset";
		private const string EventColdWavePath = ScenarioFolder + "/事件_寒潮.asset";
		private const string EventInfectionDoubtPath = ScenarioFolder + "/事件_感染疑云.asset";
		private const string ExploreLocationActionPath = ScenarioFolder + "/行动_探索地点.asset";
		private const string ScavengeSuppliesActionPath = ScenarioFolder + "/行动_搜刮物资.asset";
		private const string DefendCampActionPath = ScenarioFolder + "/行动_守备营地.asset";
		private const string StabilizeCampQuestPath = ScenarioFolder + "/任务_稳住营地.asset";

		private const string CardArtPath = "Assets/Art/Sprites/卡牌占位图.png";
		private const string CardArtAddress = "卡牌占位图";
		private const string CharacterSurfacePath = "Assets/Art/Materials/卡牌表面_角色.mat";
		private const string CharacterSurfaceAddress = "卡牌表面_角色";
		private const string AreaSurfacePath = "Assets/Art/Materials/卡牌表面_地区.mat";
		private const string AreaSurfaceAddress = "卡牌表面_地区";
		private const string ConsumableSurfacePath = "Assets/Art/Materials/卡牌表面_消耗品.mat";
		private const string ConsumableSurfaceAddress = "卡牌表面_消耗品";
		private const string MaterialSurfacePath = "Assets/Art/Materials/卡牌表面_材料.mat";
		private const string MaterialSurfaceAddress = "卡牌表面_材料";
		private const string StructureSurfacePath = "Assets/Art/Materials/卡牌表面_建筑.mat";
		private const string StructureSurfaceAddress = "卡牌表面_建筑";
		private const string ThreatSurfacePath = "Assets/Art/Materials/卡牌表面_主动敌人.mat";
		private const string ThreatSurfaceAddress = "卡牌表面_主动敌人";

		private const int CharacterAbilitySystemPresetId = 1001;
		private const int FeedingHealingEffectId = 2005;

		private static readonly string[] WorkerIds = { ScoutId, MechanicId };
		private static readonly string[] LocationIds =
		{
			LocationStoreId,
			LocationClinicId,
			LocationDinerId,
			LocationHouseId
		};
		private static readonly string[] PressureIds =
		{
			ThreatBrokenFenceId,
			EventArgumentId,
			EventColdWaveId,
			EventInfectionDoubtId
		};

		[MenuItem("Gameplay/剧本/重建寒夜营地")]
		public static void RebuildScenarioAssetsFromMenu()
		{
			RebuildScenarioAssets();
		}

		public static ScenarioDefinition RebuildScenarioAssets()
		{
			EnsureRequiredAsset(CardArtPath, "共享卡牌占位图");
			EnsureRequiredAsset(CharacterSurfacePath, "角色卡牌表面材质");
			EnsureRequiredAsset(AreaSurfacePath, "地区卡牌表面材质");
			EnsureRequiredAsset(ConsumableSurfacePath, "消耗品卡牌表面材质");
			EnsureRequiredAsset(MaterialSurfacePath, "材料卡牌表面材质");
			EnsureRequiredAsset(StructureSurfacePath, "建筑卡牌表面材质");
			EnsureRequiredAsset(ThreatSurfacePath, "威胁卡牌表面材质");
			EnsureFolder(ScenarioFolder);

			XLuban.LoadTablesForEditor();
			XLauncher.InitCache();
			WriteCharacter(
				EnsureAsset<CharacterCardDefinition>(ScoutPath),
				ScoutId,
				"斥候林夏",
				"擅长外出侦察的可控角色。首版负责探索、搜刮和守备。",
				canAct: true);
			WriteCharacter(
				EnsureAsset<CharacterCardDefinition>(MechanicPath),
				MechanicId,
				"维修员老周",
				"懂修补和营地维护的可控角色。首版负责探索、搜刮和守备。",
				canAct: true);
			WriteCharacter(
				EnsureAsset<CharacterCardDefinition>(SurvivorAningPath),
				SurvivorAningId,
				"学生阿宁",
				"需要保护的幸存者。首版不参与派工，但会消耗食物。",
				canAct: false);
			WriteCharacter(
				EnsureAsset<CharacterCardDefinition>(SurvivorChengyuanPath),
				SurvivorChengyuanId,
				"伤员程远",
				"需要保护的伤员。首版不参与派工，但会消耗食物。",
				canAct: false);
			WriteCharacter(
				EnsureAsset<CharacterCardDefinition>(SurvivorWandererPath),
				SurvivorWandererId,
				"流浪幸存者",
				"探索中可能加入营地的人。会增加保护目标，也会增加食物压力。",
				canAct: false);

			WriteBasicCard(
				EnsureAsset<CardDefinition>(BaseServiceAreaPath),
				BaseServiceAreaId,
				"废弃服务区",
				"临时营地核心。提供卡牌上限加成，让玩家有固守空间。",
				StructureSurfacePath,
				StructureSurfaceAddress,
				initialUses: 1,
				countsTowardCardLimit: false,
				cardLimitBonus: 4,
				XTag.Card_Category_Structure);
			WriteAreaCard(
				EnsureAsset<CardDefinition>(LocationStorePath),
				LocationStoreId,
				"便利店",
				"附近被翻过一半的小便利店。主要产出食物，也可能带来麻烦。",
				initialUses: 3);
			WriteAreaCard(
				EnsureAsset<CardDefinition>(LocationClinicPath),
				LocationClinicId,
				"废弃诊所",
				"停电后的乡镇诊所。能找到药品，也可能暴露救援风险。",
				initialUses: 3);
			WriteAreaCard(
				EnsureAsset<CardDefinition>(LocationDinerPath),
				LocationDinerId,
				"公路餐馆",
				"路边餐馆还剩少量可用物资。首版不再向外扩展。",
				initialUses: 2);
			WriteAreaCard(
				EnsureAsset<CardDefinition>(LocationHousePath),
				LocationHouseId,
				"民居",
				"公路边的空屋。能找到材料和少量食物，也可能有隐患。",
				initialUses: 2);
			WriteFood(
				EnsureAsset<FoodCardDefinition>(FoodCannedPath),
				FoodCannedId,
				"罐头箱",
				"能撑过第一晚的大份食物。",
				initialUses: 6);
			WriteFood(
				EnsureAsset<FoodCardDefinition>(FoodScrapsPath),
				FoodScrapsId,
				"零散食物",
				"临时搜到的小份食物。",
				initialUses: 2);
			WriteBasicCard(
				EnsureAsset<CardDefinition>(ResourcePlankPath),
				ResourcePlankId,
				"木板",
				"修补营地的基础材料。首版主要作为压力缓冲。",
				MaterialSurfacePath,
				MaterialSurfaceAddress,
				initialUses: 1,
				countsTowardCardLimit: true,
				cardLimitBonus: 0,
				XTag.Card_Category_Material);
			WriteBasicCard(
				EnsureAsset<CardDefinition>(ResourceMedicinePath),
				ResourceMedicineId,
				"药品",
				"从诊所找到的基础药品。首版先作为资源牌保留。",
				ConsumableSurfacePath,
				ConsumableSurfaceAddress,
				initialUses: 1,
				countsTowardCardLimit: true,
				cardLimitBonus: 0,
				XTag.Card_Category_Consumable);
			WritePressureCard(EnsureAsset<CardDefinition>(ThreatBrokenFencePath), ThreatBrokenFenceId, "破损栅栏", "营地外围出现缺口。需要用守备行动移除。");
			WritePressureCard(EnsureAsset<CardDefinition>(EventArgumentPath), EventArgumentId, "争执", "幸存者之间爆发争执。需要花行动平息。");
			WritePressureCard(EnsureAsset<CardDefinition>(EventColdWavePath), EventColdWaveId, "寒潮", "夜里气温骤降。会挤压玩家的处理节奏。");
			WritePressureCard(EnsureAsset<CardDefinition>(EventInfectionDoubtPath), EventInfectionDoubtId, "感染疑云", "有人开始发烧，营地气氛紧张。需要优先处理。");

			WriteLocationLoot();
			WriteExploreAction(EnsureAsset<ActionDefinition>(ExploreLocationActionPath));
			WriteScavengeAction(EnsureAsset<ActionDefinition>(ScavengeSuppliesActionPath));
			WriteDefendAction(EnsureAsset<ActionDefinition>(DefendCampActionPath));
			WriteQuest(EnsureAsset<QuestDefinition>(StabilizeCampQuestPath));
			WriteRegion(EnsureAsset<ScenarioRegionDefinition>(RegionPath));
			ScenarioDefinition scenario = EnsureAsset<ScenarioDefinition>(ScenarioPath);
			WriteScenario(scenario);

			AssetDatabase.SaveAssets();
			ContentValidationReport report = ContentValidationMenu.ValidateContentAssetsForEditor(
				LoadScenarioContentAssets());
			report.ThrowIfHasErrors();
			Debug.Log($"寒夜营地生成完成：{ScenarioPath}");
			return scenario;
		}

		public static IReadOnlyList<ContentAsset> LoadScenarioContentAssets()
		{
			if (!AssetDatabase.IsValidFolder(ScenarioFolder))
			{
				return Array.Empty<ContentAsset>();
			}

			string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { ScenarioFolder });
			List<ContentAsset> assets = new List<ContentAsset>(guids.Length);
			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				ContentAsset asset = AssetDatabase.LoadAssetAtPath<ContentAsset>(path);
				if (asset != null)
				{
					assets.Add(asset);
				}
			}
			assets.Sort((left, right) => string.CompareOrdinal(left.ContentId.Value, right.ContentId.Value));
			return assets;
		}

		private static void WriteCharacter(
			CharacterCardDefinition character,
			string contentId,
			string displayName,
			string description,
			bool canAct)
		{
			SerializedObject serialized = WriteBasicCard(
				character,
				contentId,
				displayName,
				description,
				CharacterSurfacePath,
				CharacterSurfaceAddress,
				initialUses: 1,
				countsTowardCardLimit: true,
				cardLimitBonus: 0,
				XTag.Card_Category_Character);
			RequireProperty(serialized, "m_abilitySystemPresetId").intValue = CharacterAbilitySystemPresetId;
			RequireProperty(serialized, "m_attributeOverrides").arraySize = 0;
			RequireProperty(serialized, "m_automaticBattleAbilityCode").intValue =
				canAct ? XAbility.ABILITY_TabletopBasicAttack : 0;
			RequireProperty(serialized, "m_automaticAggroRadius").floatValue = 0f;
			RequireProperty(serialized, "m_automaticAttackRadius").floatValue = 0f;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(character);
		}

		private static void WriteAreaCard(
			CardDefinition card,
			string contentId,
			string displayName,
			string description,
			int initialUses)
		{
			WriteBasicCard(
				card,
				contentId,
				displayName,
				description,
				AreaSurfacePath,
				AreaSurfaceAddress,
				initialUses,
				countsTowardCardLimit: true,
				cardLimitBonus: 0,
				XTag.Card_Category_Area);
		}

		private static void WriteFood(
			FoodCardDefinition food,
			string contentId,
			string displayName,
			string description,
			int initialUses)
		{
			SerializedObject serialized = WriteBasicCard(
				food,
				contentId,
				displayName,
				description,
				ConsumableSurfacePath,
				ConsumableSurfaceAddress,
				initialUses,
				countsTowardCardLimit: true,
				cardLimitBonus: 0,
				XTag.Card_Category_Consumable);
			RequireProperty(serialized, "m_nutritionPerUse").intValue = 1;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(food);
		}

		private static void WritePressureCard(
			CardDefinition card,
			string contentId,
			string displayName,
			string description)
		{
			WriteBasicCard(
				card,
				contentId,
				displayName,
				description,
				ThreatSurfacePath,
				ThreatSurfaceAddress,
				initialUses: 1,
				countsTowardCardLimit: true,
				cardLimitBonus: 0,
				XTag.Card_Category_Mob);
		}

		private static SerializedObject WriteBasicCard(
			CardDefinition card,
			string contentId,
			string displayName,
			string description,
			string surfacePath,
			string surfaceAddress,
			int initialUses,
			bool countsTowardCardLimit,
			int cardLimitBonus,
			params int[] tagCodes)
		{
			SerializedObject serialized = WriteCommonContentFields(
				card,
				contentId,
				displayName,
				description);
			RequireProperty(serialized, "m_initialUses").intValue = initialUses;
			RequireProperty(serialized, "m_sellValue").intValue = 0;
			RequireProperty(serialized, "m_countsTowardCardLimit").boolValue = countsTowardCardLimit;
			RequireProperty(serialized, "m_cardLimitBonus").intValue = cardLimitBonus;
			RequireProperty(serialized, "m_periodicProductionCardId")
				.FindPropertyRelative("m_value").stringValue = string.Empty;
			RequireProperty(serialized, "m_periodicProductionIntervalSeconds").floatValue = 0f;
			RequireProperty(serialized, "m_automaticMovementRetentionCapacity").intValue = 0;
			RequireProperty(serialized, "m_loot").arraySize = 0;
			WriteSoftAssetReference(serialized, "m_cardArt", CardArtPath, CardArtAddress);
			WriteSoftAssetReference(serialized, "m_cardSurface", surfacePath, surfaceAddress);
			RequireProperty(serialized, "m_overrideViewSize").boolValue = false;
			RequireProperty(serialized, "m_viewSize").vector2Value = Vector2.one;
			WriteIntArray(RequireProperty(serialized, "m_tagCodes"), tagCodes);
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(card);
			return serialized;
		}

		private static void WriteLocationLoot()
		{
			WriteLoot(
				AssetDatabase.LoadAssetAtPath<CardDefinition>(LocationStorePath),
				(FoodCannedId, 4),
				(FoodScrapsId, 3),
				(ResourcePlankId, 2),
				(EventArgumentId, 1),
				(LocationDinerId, 2),
				(SurvivorWandererId, 1),
				(ThreatBrokenFenceId, 1));
			WriteLoot(
				AssetDatabase.LoadAssetAtPath<CardDefinition>(LocationClinicPath),
				(ResourceMedicineId, 4),
				(FoodScrapsId, 2),
				(ResourcePlankId, 2),
				(EventInfectionDoubtId, 2),
				(LocationHouseId, 2),
				(SurvivorWandererId, 1),
				(EventColdWaveId, 1));
			WriteLoot(
				AssetDatabase.LoadAssetAtPath<CardDefinition>(LocationDinerPath),
				(FoodScrapsId, 4),
				(FoodCannedId, 2),
				(EventArgumentId, 2));
			WriteLoot(
				AssetDatabase.LoadAssetAtPath<CardDefinition>(LocationHousePath),
				(ResourcePlankId, 4),
				(ResourceMedicineId, 3),
				(FoodScrapsId, 3),
				(ThreatBrokenFenceId, 2));
		}

		private static void WriteExploreAction(ActionDefinition action)
		{
			SerializedObject serialized = WriteCommonContentFields(
				action,
				ExploreLocationActionId,
				"探索地点",
				"派一名可控角色调查一个地点，标记地点已探索，并翻出一张新牌。");
			WriteActionCore(serialized, turnCost: 1, canStartFromClick: false, "探索");
			SerializedProperty slots = RequireProperty(serialized, "m_participationSlots");
			slots.arraySize = 2;
			WriteSlot(slots.GetArrayElementAtIndex(0), "actor", "行动者", WorkerIds);
			WriteSlot(slots.GetArrayElementAtIndex(1), "location", "地点", LocationIds);
			WriteResultIntents(
				serialized,
				new ExploreCardsResultIntent(),
				intent => RequireRelative(intent, "m_exploredSlotKey").stringValue = "location",
				new ExploreLootResultIntent(),
				intent => RequireRelative(intent, "m_areaSlotKey").stringValue = "location");
			FinalizeAction(action, serialized);
		}

		private static void WriteScavengeAction(ActionDefinition action)
		{
			SerializedObject serialized = WriteCommonContentFields(
				action,
				ScavengeSuppliesActionId,
				"搜刮物资",
				"派一名可控角色搜刮一个地点，翻出资源或事件，并消耗该地点一次使用次数。");
			WriteActionCore(serialized, turnCost: 1, canStartFromClick: false, "探索");
			SerializedProperty slots = RequireProperty(serialized, "m_participationSlots");
			slots.arraySize = 2;
			WriteSlot(slots.GetArrayElementAtIndex(0), "actor", "行动者", WorkerIds);
			WriteSlot(slots.GetArrayElementAtIndex(1), "location", "地点", LocationIds);
			WriteResultIntents(
				serialized,
				new ExploreLootResultIntent(),
				intent => RequireRelative(intent, "m_areaSlotKey").stringValue = "location",
				new UseCardsResultIntent(),
				intent => RequireRelative(intent, "m_slotKey").stringValue = "location");
			FinalizeAction(action, serialized);
		}

		private static void WriteDefendAction(ActionDefinition action)
		{
			SerializedObject serialized = WriteCommonContentFields(
				action,
				DefendCampActionId,
				"守备营地",
				"派一名可控角色处理一张威胁或事件牌。成功后移除该压力牌。");
			WriteActionCore(serialized, turnCost: 1, canStartFromClick: false, "守备");
			SerializedProperty slots = RequireProperty(serialized, "m_participationSlots");
			slots.arraySize = 2;
			WriteSlot(slots.GetArrayElementAtIndex(0), "actor", "行动者", WorkerIds);
			WriteSlot(slots.GetArrayElementAtIndex(1), "pressure", "威胁或事件", PressureIds);
			WriteResultIntents(
				serialized,
				new RemoveCardsResultIntent(),
				intent => RequireRelative(intent, "m_slotKey").stringValue = "pressure");
			FinalizeAction(action, serialized);
		}

		private static void WriteActionCore(
			SerializedObject serialized,
			int turnCost,
			bool canStartFromClick,
			string journalGroupName)
		{
			RequireProperty(serialized, "m_turnCost").intValue = turnCost;
			RequireProperty(serialized, "m_canStartFromClick").boolValue = canStartFromClick;
			RequireProperty(serialized, "m_allowExcessCardsInStack").boolValue = false;
			RequireProperty(serialized, "m_recheckStackAfterCompletion").boolValue = false;
			RequireProperty(serialized, "m_recipeSelectionWeight").floatValue = 1f;
			RequireProperty(serialized, "m_journalGroupName").stringValue = journalGroupName;
			RequireProperty(serialized, "m_conditions").arraySize = 0;
			RequireProperty(serialized, "m_resultBranches").arraySize = 0;
		}

		private static void WriteResultIntents(
			SerializedObject serialized,
			ActionResultIntent firstIntent,
			Action<SerializedProperty> configureFirst,
			ActionResultIntent secondIntent = null,
			Action<SerializedProperty> configureSecond = null)
		{
			SerializedProperty intents = RequireProperty(serialized, "m_resultIntents");
			intents.arraySize = secondIntent == null ? 1 : 2;
			SerializedProperty first = intents.GetArrayElementAtIndex(0);
			first.managedReferenceValue = firstIntent;
			configureFirst(first);
			if (secondIntent == null)
			{
				return;
			}

			SerializedProperty second = intents.GetArrayElementAtIndex(1);
			second.managedReferenceValue = secondIntent;
			configureSecond?.Invoke(second);
		}

		private static void FinalizeAction(ActionDefinition action, SerializedObject serialized)
		{
			serialized.ApplyModifiedPropertiesWithoutUndo();
			action.EnsureLocalAuthoringKeys();
			EditorUtility.SetDirty(action);
		}

		private static void WriteSlot(
			SerializedProperty slot,
			string key,
			string displayName,
			IReadOnlyList<string> allowedContentIds)
		{
			RequireRelative(slot, "m_key").stringValue = key;
			RequireRelative(slot, "m_displayName").stringValue = displayName;
			RequireRelative(slot, "m_minimumParticipants").intValue = 1;
			RequireRelative(slot, "m_maximumParticipants").intValue = 1;
			RequireRelative(slot, "m_allowAdditionalMatchingParticipantsInStack").boolValue = false;
			WriteContentIdArray(RequireRelative(slot, "m_allowedContentIds"), allowedContentIds);
			WriteIntArray(RequireRelative(slot, "m_requiredAllContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAnyContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredNoneContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAllAbilitySystemTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAnyAbilitySystemTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredNoneAbilitySystemTagCodes"));
		}

		private static void WriteQuest(QuestDefinition quest)
		{
			SerializedObject serialized = WriteCommonContentFields(
				quest,
				StabilizeCampQuestId,
				"稳住营地",
				"撑到第 4 天，保住阿宁，并留下至少 2 点食物。");
			RequireProperty(serialized, "m_journalGroupName").stringValue = "目标";
			WriteContentIdArray(RequireProperty(serialized, "m_prerequisiteQuestIds"));
			SerializedProperty tasks = RequireProperty(serialized, "m_tasks");
			tasks.arraySize = 4;

			SerializedProperty exploreTask = tasks.GetArrayElementAtIndex(0);
			exploreTask.managedReferenceValue = new CardExplorationQuestTaskDefinition();
			RequireRelative(exploreTask, "m_cardId").FindPropertyRelative("m_value").stringValue = LocationStoreId;
			RequireRelative(exploreTask, "m_requiredExplorationCount").intValue = 1;

			SerializedProperty dayTask = tasks.GetArrayElementAtIndex(1);
			dayTask.managedReferenceValue = new DayReachedQuestTaskDefinition();
			RequireRelative(dayTask, "m_requiredDay").intValue = 4;

			SerializedProperty survivorTask = tasks.GetArrayElementAtIndex(2);
			survivorTask.managedReferenceValue = new CardPossessionQuestTaskDefinition();
			RequireRelative(survivorTask, "m_cardId").FindPropertyRelative("m_value").stringValue = SurvivorAningId;
			RequireRelative(survivorTask, "m_requiredCardCount").intValue = 1;

			SerializedProperty foodTask = tasks.GetArrayElementAtIndex(3);
			foodTask.managedReferenceValue = new FoodNutritionQuestTaskDefinition();
			RequireRelative(foodTask, "m_requiredNutrition").intValue = 2;

			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(quest);
		}

		private static void WriteRegion(ScenarioRegionDefinition region)
		{
			SerializedObject serialized = WriteCommonContentFields(
				region,
				RegionId,
				"废弃服务区周边",
				"首个固守探索剧本使用的单地区牌桌。");
			RequireProperty(serialized, "m_sceneAddress").stringValue = string.Empty;
			RequireProperty(serialized, "m_arrivalPosition").vector2Value = Vector2.zero;
			WritePlacement(RequireProperty(serialized, "m_tabletopPlacement"));

			SerializedProperty initialCards = RequireProperty(serialized, "m_initialCards");
			InitialCardSpec[] specs =
			{
				new InitialCardSpec(ScoutId, new Vector2(-4.2f, 1.2f), false),
				new InitialCardSpec(MechanicId, new Vector2(-3f, 1.2f), false),
				new InitialCardSpec(SurvivorAningId, new Vector2(-4.2f, -0.3f), false),
				new InitialCardSpec(SurvivorChengyuanId, new Vector2(-3f, -0.3f), false),
				new InitialCardSpec(BaseServiceAreaId, new Vector2(-1.2f, 0.5f), true),
				new InitialCardSpec(LocationStoreId, new Vector2(1.2f, 1.2f), true),
				new InitialCardSpec(LocationClinicId, new Vector2(2.7f, 1.2f), true),
				new InitialCardSpec(FoodCannedId, new Vector2(1.2f, -0.4f), false),
				new InitialCardSpec(ResourcePlankId, new Vector2(2.7f, -0.4f), false),
				new InitialCardSpec(ThreatBrokenFenceId, new Vector2(0f, -1.8f), true)
			};
			initialCards.arraySize = specs.Length;
			for (int i = 0; i < specs.Length; i++)
			{
				WriteInitialCard(initialCards.GetArrayElementAtIndex(i), specs[i]);
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(region);
		}

		private static void WriteScenario(ScenarioDefinition scenario)
		{
			SerializedObject serialized = WriteCommonContentFields(
				scenario,
				ScenarioId,
				"寒夜营地",
				"短局固守探索剧本。白天派角色探索和搜刮，夜晚处理食物与事件压力。");
			RequireProperty(serialized, "m_initialRegionId")
				.FindPropertyRelative("m_value").stringValue = RegionId;
			WriteContentIdArray(RequireProperty(serialized, "m_regionIds"), RegionId);
			WriteContentIdArray(RequireProperty(serialized, "m_questIds"), StabilizeCampQuestId);
			WriteContentIdArray(
				RequireProperty(serialized, "m_initialDiscoveredContentIds"),
				ExploreLocationActionId,
				ScavengeSuppliesActionId,
				DefendCampActionId);
			RequireProperty(serialized, "m_turnsPerDay").intValue = 2;
			RequireProperty(serialized, "m_secondsPerTurn").floatValue = 1f;
			WriteDayCycle(RequireProperty(serialized, "m_dayCycleRules"));
			RequireProperty(serialized, "m_battleFormationRules")
				.FindPropertyRelative("m_sideLayouts").arraySize = 0;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(scenario);
		}

		private static void WriteDayCycle(SerializedProperty dayCycle)
		{
			RequireRelative(dayCycle, "m_enabled").boolValue = true;
			RequireRelative(dayCycle, "m_hungerPerCharacter").intValue = 1;
			RequireRelative(dayCycle, "m_baseCardLimit").intValue = 8;
			RequireRelative(dayCycle, "m_feedingHealingEffectId").intValue = FeedingHealingEffectId;
			SerializedProperty encounters = RequireRelative(dayCycle, "m_encounters");
			encounters.arraySize = 3;
			WriteEncounter(
				encounters.GetArrayElementAtIndex(0),
				"broken-fence-night",
				"夜里围栏又被撞开一段。",
				ThreatBrokenFenceId,
				minimumDay: 1,
				maximumDay: 1,
				priority: 30);
			WriteEncounter(
				encounters.GetArrayElementAtIndex(1),
				"cold-wave-night",
				"寒潮压下来，营地必须腾出人手处理。",
				EventColdWaveId,
				minimumDay: 2,
				maximumDay: 2,
				priority: 20);
			WriteEncounter(
				encounters.GetArrayElementAtIndex(2),
				"infection-doubt-night",
				"有人开始发烧，感染疑云在营地里蔓延。",
				EventInfectionDoubtId,
				minimumDay: 3,
				maximumDay: 3,
				priority: 10);
		}

		private static void WriteEncounter(
			SerializedProperty encounter,
			string key,
			string message,
			string cardId,
			int minimumDay,
			int maximumDay,
			int priority)
		{
			RequireRelative(encounter, "m_key").stringValue = key;
			RequireRelative(encounter, "m_notificationMessage").stringValue = message;
			RequireRelative(encounter, "m_cardId").FindPropertyRelative("m_value").stringValue = cardId;
			RequireRelative(encounter, "m_count").intValue = 1;
			RequireRelative(encounter, "m_oneTimeOnly").boolValue = true;
			RequireRelative(encounter, "m_minimumDay").intValue = minimumDay;
			RequireRelative(encounter, "m_maximumDay").intValue = maximumDay;
			RequireRelative(encounter, "m_interval").intValue = 0;
			RequireRelative(encounter, "m_priority").intValue = priority;
			RequireRelative(encounter, "m_chance").floatValue = 1f;
			RequireRelative(encounter, "m_maxCardsOnTabletop").intValue = 100;
		}

		private static void WritePlacement(SerializedProperty placement)
		{
			RequireRelative(placement, "m_bounds").rectValue = new Rect(-6f, -4f, 12f, 8f);
			SerializedProperty restrictedAreas = RequireRelative(placement, "m_restrictedAreas");
			restrictedAreas.arraySize = 1;
			restrictedAreas.GetArrayElementAtIndex(0).rectValue = new Rect(-6f, 2.5f, 12f, 1.5f);
			RequireRelative(placement, "m_cardSize").vector2Value = new Vector2(0.8f, 1f);
			RequireRelative(placement, "m_cardMargin").vector2Value = new Vector2(0.1f, 0.1f);
			RequireRelative(placement, "m_stackStep").vector2Value = new Vector2(0f, -0.18f);
			RequireRelative(placement, "m_overlapResolveMaxIterations").intValue = 8;
			RequireRelative(placement, "m_spawnAttachRadius").floatValue = 1f;
			RequireRelative(placement, "m_cardLimitBonusExpansionPerPoint").vector2Value =
				new Vector2(0.06f, 0.04f);
			RequireRelative(placement, "m_automaticMovementIntervalSeconds").floatValue = 5f;
			RequireRelative(placement, "m_automaticMovementRadius").floatValue = 1f;
			RequireRelative(placement, "m_automaticMovementMaxAttempts").intValue = 5;
		}

		private static void WriteInitialCard(SerializedProperty property, InitialCardSpec spec)
		{
			RequireRelative(property, "m_cardId").FindPropertyRelative("m_value").stringValue = spec.ContentId;
			RequireRelative(property, "m_count").intValue = 1;
			RequireRelative(property, "m_position").vector2Value = spec.Position;
			RequireRelative(property, "m_isPlacementLocked").boolValue = spec.IsPlacementLocked;
		}

		private static void WriteLoot(CardDefinition source, params (string ContentId, int Weight)[] entries)
		{
			if (source == null)
			{
				throw new MissingReferenceException("写入地点产出前，地点资产不存在。");
			}

			SerializedObject serialized = new SerializedObject(source);
			SerializedProperty loot = RequireProperty(serialized, "m_loot");
			loot.arraySize = entries.Length;
			for (int i = 0; i < entries.Length; i++)
			{
				SerializedProperty entry = loot.GetArrayElementAtIndex(i);
				RequireRelative(entry, "m_cardId").FindPropertyRelative("m_value").stringValue =
					entries[i].ContentId;
				RequireRelative(entry, "m_weight").intValue = entries[i].Weight;
			}
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(source);
		}

		private static T EnsureAsset<T>(string assetPath)
			where T : ScriptableObject
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
			if (asset != null)
			{
				return asset;
			}

			ScriptableObject existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
			if (existing != null)
			{
				throw new InvalidOperationException(
					$"{assetPath} 已存在但不是 {typeof(T).Name}，请先人工确认是否要替换。");
			}

			asset = ScriptableObject.CreateInstance<T>();
			AssetDatabase.CreateAsset(asset, assetPath);
			return asset;
		}

		private static void EnsureRequiredAsset(string assetPath, string description)
		{
			if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
			{
				throw new MissingReferenceException($"缺少{description}：{assetPath}");
			}
		}

		private static void EnsureFolder(string folderPath)
		{
			string[] parts = folderPath.Split('/');
			string current = parts[0];
			for (int i = 1; i < parts.Length; i++)
			{
				string next = current + "/" + parts[i];
				if (!AssetDatabase.IsValidFolder(next))
				{
					AssetDatabase.CreateFolder(current, parts[i]);
				}
				current = next;
			}
		}

		private static SerializedObject WriteCommonContentFields(
			ContentAsset content,
			string contentId,
			string displayName,
			string description)
		{
			SerializedObject serialized = new SerializedObject(content);
			RequireProperty(serialized, "m_contentId")
				.FindPropertyRelative("m_value").stringValue = contentId;
			RequireProperty(serialized, "m_displayName").stringValue = displayName;
			RequireProperty(serialized, "m_description").stringValue = description;
			return serialized;
		}

		private static void WriteSoftAssetReference(
			SerializedObject serialized,
			string propertyName,
			string assetPath,
			string address)
		{
			SerializedProperty reference = RequireProperty(serialized, propertyName);
			RequireRelative(reference, "Address").stringValue = address;
			SerializedProperty guid = reference.FindPropertyRelative("Guid");
			if (guid != null)
			{
				guid.stringValue = AssetDatabase.AssetPathToGUID(assetPath);
			}
			SerializedProperty locked = reference.FindPropertyRelative("Locked");
			if (locked != null)
			{
				locked.boolValue = true;
			}
		}

		private static void WriteContentIdArray(
			SerializedProperty property,
			params string[] values)
		{
			WriteContentIdArray(property, (IReadOnlyList<string>)values);
		}

		private static void WriteContentIdArray(
			SerializedProperty property,
			IReadOnlyList<string> values)
		{
			property.arraySize = values.Count;
			for (int i = 0; i < values.Count; i++)
			{
				RequireRelative(property.GetArrayElementAtIndex(i), "m_value").stringValue = values[i];
			}
		}

		private static void WriteIntArray(SerializedProperty property, params int[] values)
		{
			property.arraySize = values.Length;
			for (int i = 0; i < values.Length; i++)
			{
				property.GetArrayElementAtIndex(i).intValue = values[i];
			}
		}

		private static SerializedProperty RequireProperty(SerializedObject serialized, string propertyName)
		{
			SerializedProperty property = serialized.FindProperty(propertyName);
			if (property == null)
			{
				throw new MissingReferenceException($"寒夜营地生成器缺少序列化字段：{propertyName}");
			}
			return property;
		}

		private static SerializedProperty RequireRelative(SerializedProperty parent, string relativeName)
		{
			SerializedProperty property = parent?.FindPropertyRelative(relativeName);
			if (property == null)
			{
				throw new MissingReferenceException($"寒夜营地生成器缺少序列化字段：{relativeName}");
			}
			return property;
		}

		private readonly struct InitialCardSpec
		{
			internal string ContentId { get; }
			internal Vector2 Position { get; }
			internal bool IsPlacementLocked { get; }

			internal InitialCardSpec(string contentId, Vector2 position, bool isPlacementLocked)
			{
				ContentId = contentId;
				Position = position;
				IsPlacementLocked = isPlacementLocked;
			}
		}
	}
}
