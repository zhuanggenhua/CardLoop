using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证剧本作者组成和导演单局入口的 EditMode 行为合同。
	/// </summary>
	public sealed class ScenarioDirectorEditModeTests
	{
		private string m_saveDirectory;

		[SetUp]
		public void SetUp()
		{
			m_saveDirectory = Path.Combine(
				Path.GetTempPath(),
				"Gameplay-ScenarioDirector-" + Guid.NewGuid().ToString("N"));
			InvokeSaveSystemMethod("ConfigureSaveKit", m_saveDirectory);
		}

		[TearDown]
		public void TearDown()
		{
			InvokeSaveSystemMethod("ResetSaveKitConfigurationForTests");
			SaveKit.Reset();
			if (Directory.Exists(m_saveDirectory))
			{
				Directory.Delete(m_saveDirectory, recursive: true);
			}
		}

		[Test]
		public void SaveActiveRunToSlot_WritesWholeRunSnapshotAndDerivedMetadata()
		{
			ScenarioRegionDefinition region = CreateRegion("test.save.region");
			ScenarioDefinition scenario = CreateScenarioWithRegion(
				"test.save.scenario",
				"存档测试剧本",
				region,
				"存档测试地区");
			ScenarioDirector director = new GameObject("ScenarioDirector-Save-Test")
				.AddComponent<ScenarioDirector>();
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { scenario, region });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				run.ConfirmTurn();
				SaveData existingContainer = GameCore.SaveSystem.CreateSaveContainer();
				existingContainer.RegisterModule(new IndependentSaveProbe { Value = 17 });
				Assert.That(
					GameCore.SaveSystem.StoreSaveDataToFile(3, existingContainer, "旧标题"),
					Is.True);
				SetPrivateField(director, "m_activeRun", run);
				if (!director.enabled)
				{
					director.OnSystemStart();
				}

				Assert.That(director.SaveActiveRunToSlot(3), Is.True);

				SaveData container = GameCore.SaveSystem.ExtractSaveContainerFromFile(3);
				ScenarioRunSnapshot snapshot = container.GetModule<ScenarioRunSnapshot>();
				SaveMeta metadata = GameCore.SaveSystem.GetSaveMetadata(3);
				Assert.That(snapshot, Is.Not.Null);
				Assert.That(snapshot.ScenarioId, Is.EqualTo(scenario.ContentId));
				Assert.That(snapshot.ActiveRegionId, Is.EqualTo(region.ContentId));
				Assert.That(snapshot.ConfirmedTurnIndex, Is.EqualTo(1));
				Assert.That(container.GetModule<IndependentSaveProbe>().Value, Is.EqualTo(17));
				Assert.That(metadata.DisplayName, Is.EqualTo("存档测试剧本 · 存档测试地区 · 第 1 天"));
			}
			finally
			{
				director.OnSystemShutdown();
				Object.DestroyImmediate(director.gameObject);
				Destroy(scenario, region);
			}
		}

		[Test]
		public void ContinueDayCycle_StartsNewDayAndOverwritesTheRunsAssignedSlot()
		{
			ScenarioRegionDefinition region = CreateRegion("test.autosave.region");
			ScenarioDefinition scenario = CreateScenarioWithRegion(
				"test.autosave.scenario",
				"自动存档测试剧本",
				region,
				"自动存档测试地区");
			JsonUtility.FromJsonOverwrite(
				"{\"m_turnsPerDay\":1,\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":0,\"m_baseCardLimit\":10}}",
				scenario);
			CharacterCardDefinition character = ScriptableObject.CreateInstance<CharacterCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.autosave.character\"},\"m_abilitySystemPresetId\":1001}",
				character);
			ScenarioDirector director = new GameObject("ScenarioDirector-AutoSave-Test")
				.AddComponent<ScenarioDirector>();
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { scenario, region, character });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.Tabletop.CreateCard(character.ContentId, Vector2.zero);
				run.ConfirmTurn();
				run.ContinueDayCycle();
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingNewDayConfirmation));
				SetPrivateField(director, "m_activeRun", run);
				SetPrivateField(director, "m_activeSaveSlotId", 4);
				if (!director.enabled)
				{
					director.OnSystemStart();
				}

				director.ContinueDayCycle();

				Assert.That(run.CurrentDay, Is.EqualTo(2));
				Assert.That(director.ActiveSaveSlotId, Is.EqualTo(4));
				SaveData container = GameCore.SaveSystem.ExtractSaveContainerFromFile(4);
				Assert.That(container, Is.Not.Null);
				Assert.That(container.GetModule<ScenarioRunSnapshot>().ConfirmedTurnIndex, Is.EqualTo(1));
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				director.OnSystemShutdown();
				Object.DestroyImmediate(director.gameObject);
				Destroy(scenario, region, character);
			}
		}

		[Test]
		public void RestoreRunFromSaveContainer_RejectsMissingScenarioModuleWithoutReplacingActiveRun()
		{
			ScenarioRegionDefinition region = CreateRegion("test.load-missing.region");
			ScenarioDefinition scenario = CreateScenarioWithRegion(
				"test.load-missing.scenario",
				"原活动剧本",
				region,
				"原地区");
			ScenarioDirector director = new GameObject("ScenarioDirector-Load-Missing-Test")
				.AddComponent<ScenarioDirector>();
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { scenario, region });
				ScenarioRun activeRun = new ScenarioRun(scenario, contentIndex, 12345u);
				activeRun.ActivateInitialQuests();
				SetPrivateField(director, "m_activeRun", activeRun);
				if (!director.enabled)
				{
					director.OnSystemStart();
				}

				InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
					InvokeCreateRunFromSaveContainer(
						director,
						GameCore.SaveSystem.CreateSaveContainer(),
						contentIndex));

				StringAssert.Contains("不包含剧本单局模块", exception.Message);
				Assert.That(director.ActiveRun, Is.SameAs(activeRun));
				Assert.That(activeRun.IsEnded, Is.False);
				Assert.That(director.ConfirmTurn(), Is.EqualTo(1));
			}
			finally
			{
				director.OnSystemShutdown();
				Object.DestroyImmediate(director.gameObject);
				Destroy(scenario, region);
			}
		}

		[Test]
		public void RestoreRunFromSaveContainer_ReplacesActiveRunOnlyAfterWholeSnapshotIsValid()
		{
			ScenarioRegionDefinition region = CreateRegion("test.load.region");
			ScenarioDefinition scenario = CreateScenarioWithRegion(
				"test.load.scenario",
				"读取测试剧本",
				region,
				"读取测试地区");
			ScenarioDirector director = new GameObject("ScenarioDirector-Load-Test")
				.AddComponent<ScenarioDirector>();
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { scenario, region });
				ScenarioRun savedRun = new ScenarioRun(scenario, contentIndex, 12345u);
				savedRun.ActivateInitialQuests();
				savedRun.ConfirmTurn();
				savedRun.ConfirmTurn();
				SaveData container = GameCore.SaveSystem.CreateSaveContainer();
				container.RegisterModule(savedRun.CreateSnapshot());

				ScenarioRun previousRun = new ScenarioRun(scenario, contentIndex, 54321u);
				previousRun.ActivateInitialQuests();
				SetPrivateField(director, "m_activeRun", previousRun);
				if (!director.enabled)
				{
					director.OnSystemStart();
				}

				ScenarioRun restored = InvokeCreateRunFromSaveContainer(director, container, contentIndex);
				InvokeReplaceActiveRun(director, restored);

				Assert.That(restored, Is.SameAs(director.ActiveRun));
				Assert.That(restored, Is.Not.SameAs(previousRun));
				Assert.That(restored.ConfirmedTurnIndex, Is.EqualTo(2));
				Assert.That(previousRun.IsEnded, Is.True);
			}
			finally
			{
				director.OnSystemShutdown();
				Object.DestroyImmediate(director.gameObject);
				Destroy(scenario, region);
			}
		}
		[Test]
		public void ScenarioDefinition_ReferencesAnInitialRegionInsteadOfOwningSceneAddress()
		{
			FieldInfo initialRegionField = typeof(ScenarioDefinition).GetField(
				"m_initialRegionId",
				BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo sceneAddressField = typeof(ScenarioDefinition).GetField(
				"m_initialSceneAddress",
				BindingFlags.Instance | BindingFlags.NonPublic);

			Assert.That(initialRegionField, Is.Not.Null,
				"剧本作者源必须引用初始地区，让地区拥有场景与牌桌配置。");
			Assert.That(initialRegionField.FieldType, Is.EqualTo(typeof(ContentId)));
			Assert.That(
				initialRegionField.GetCustomAttributes(inherit: true)
					.Any(attribute => attribute.GetType().Name == "ContentIdReferenceAttribute"),
				Is.True,
				"初始地区必须由内容引用选择器维护，不能要求作者手填内部 ID。");
			Assert.That(sceneAddressField, Is.Null,
				"场景地址属于地区定义，剧本不能继续保存第二份场景配置。");
		}

		[Test]
		public void ScenarioDirector_DeclaresSceneSystemAsARealStartupDependency()
		{
			ScenarioDirector director = new GameObject("ScenarioDirector-SceneDependency-Test")
				.AddComponent<ScenarioDirector>();
			try
			{
				Assert.That(director.StartupDependencies, Does.Contain(typeof(GameCore.SceneSystem)));
			}
			finally
			{
				Object.DestroyImmediate(director.gameObject);
			}
		}

		[Test]
		public void ScenarioDirector_DoesNotDependOnProcessLevelContentRegistry()
		{
			ScenarioDirector director = new GameObject("ScenarioDirector-Test")
				.AddComponent<ScenarioDirector>();
			try
			{
				IReadOnlyCollection<Type> dependencies = director.StartupDependencies;
				Assert.That(
					dependencies.Any(type => type.Name == "ContentRegistrySystem"),
					Is.False,
					"剧本内容集合不能在进程启动时由 ContentRegistrySystem 全量建立。");

				FieldInfo[] fields = typeof(ScenarioDirector).GetFields(
					BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.That(
					fields.Any(field => field.FieldType.Name == "ContentRegistrySystem"),
					Is.False,
					"ScenarioDirector 仍保存进程级内容登记引用，没有让内容集合跟随单局。\n");
			}
			finally
			{
				Object.DestroyImmediate(director.gameObject);
			}
		}

		[Test]
		public void ContentValidator_RejectsInvalidScenarioQuestComposition()
		{
			CardDefinition card = CreateCard("test.card");
			QuestDefinition root = CreateQuest("test.quest.root");
			QuestDefinition child = CreateQuest("test.quest.child", "test.quest.root");
			ScenarioRegionDefinition region = CreateRegion("test.scenario.invalid.region");
			ScenarioDefinition scenario = CreateScenario("test.scenario.invalid", child.ContentId.Value, child.ContentId.Value, card.ContentId.Value, "test.quest.missing");
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(new ContentAsset[5] { card, root, child, region, scenario });
				Assert.That<bool>(report.HasErrors, (IResolveConstraint)(object)Is.True);
				AssertIssue(report, "SCENARIO_QUEST_DUPLICATE");
				AssertIssue(report, "SCENARIO_QUEST_TYPE_INVALID");
				AssertIssue(report, "SCENARIO_QUEST_UNKNOWN");
				AssertIssue(report, "SCENARIO_QUEST_PREREQUISITE_MISSING");
			}
			finally
			{
				Destroy((Object)card, (Object)root, (Object)child, (Object)region, (Object)scenario);
			}
		}

		private static QuestDefinition CreateQuest(string contentId, params string[] prerequisiteQuestIds)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			string prerequisitesJson = string.Join(",", Array.ConvertAll(prerequisiteQuestIds, (string prerequisiteId) => "{\"m_value\":\"" + prerequisiteId + "\"}"));
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"},\"m_prerequisiteQuestIds\":[" + prerequisitesJson + "]}", (object)definition);
			return definition;
		}

		private static CardDefinition CreateCard(string contentId)
		{
			CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}", (object)definition);
			return definition;
		}

		private static ScenarioDefinition CreateScenario(string contentId, params string[] questIds)
		{
			ScenarioDefinition definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
			string questJson = string.Join(",", Array.ConvertAll(questIds, (string questId) => "{\"m_value\":\"" + questId + "\"}"));
			string regionId = contentId + ".region";
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
				"\"},\"m_initialRegionId\":{\"m_value\":\"" + regionId +
				"\"},\"m_regionIds\":[{\"m_value\":\"" + regionId +
				"\"}],\"m_questIds\":[" + questJson + "]}",
				definition);
			return definition;
		}

		private static ScenarioRegionDefinition CreateRegion(string contentId)
		{
			ScenarioRegionDefinition definition = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			return definition;
		}

		private static ScenarioDefinition CreateScenarioWithRegion(
			string contentId,
			string displayName,
			ScenarioRegionDefinition region,
			string regionDisplayName)
		{
			JsonUtility.FromJsonOverwrite(
				"{\"m_displayName\":\"" + regionDisplayName + "\"}",
				region);
			ScenarioDefinition definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
				"\"},\"m_displayName\":\"" + displayName +
				"\",\"m_turnsPerDay\":2,\"m_initialRegionId\":{\"m_value\":\"" + region.ContentId.Value +
				"\"},\"m_regionIds\":[{\"m_value\":\"" + region.ContentId.Value + "\"}]}",
				definition);
			return definition;
		}

		private static void SetPrivateField(object target, string fieldName, object value)
		{
			target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
				.SetValue(target, value);
		}

		private static void InvokeSaveSystemMethod(string methodName, params object[] arguments)
		{
			typeof(GameCore.SaveSystem).GetMethod(
				methodName,
				BindingFlags.Static | BindingFlags.NonPublic)
				.Invoke(null, arguments);
		}

		private static void InvokeFormalGasBootstrap(string methodName)
		{
			Type bootstrapType = typeof(GameCore.GameManager).Assembly.GetType(
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

		private static ScenarioRun InvokeCreateRunFromSaveContainer(
			ScenarioDirector director,
			SaveData container,
			ContentIndex contentIndex)
		{
			MethodInfo method = typeof(ScenarioDirector).GetMethod(
				"CreateRunFromSaveContainer",
				BindingFlags.Static | BindingFlags.NonPublic);
			if (method == null)
			{
				throw new MissingMethodException(typeof(ScenarioDirector).FullName, "CreateRunFromSaveContainer");
			}
			try
			{
				return (ScenarioRun)method.Invoke(null, new object[] { container, contentIndex });
			}
			catch (TargetInvocationException exception) when (exception.InnerException != null)
			{
				throw exception.InnerException;
			}
		}

		private static void InvokeReplaceActiveRun(ScenarioDirector director, ScenarioRun run)
		{
			typeof(ScenarioDirector).GetMethod(
				"ReplaceActiveRun",
				BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(director, new object[] { run });
		}

		private static void Destroy(params Object[] objects)
		{
			for (int i = 0; i < objects.Length; i++)
			{
				if (objects[i] != (Object)null)
				{
					Object.DestroyImmediate(objects[i]);
				}
			}
		}

		private static void AssertIssue(ContentValidationReport report, string code)
		{
			Assert.That<bool>(report.Issues.Any((ContentValidationIssue issue) => issue.Code == code), (IResolveConstraint)(object)Is.True, "校验报告缺少问题码：" + code, Array.Empty<object>());
		}

		[Serializable]
		private sealed class IndependentSaveProbe
		{
			public int Value;
		}
	}
}
