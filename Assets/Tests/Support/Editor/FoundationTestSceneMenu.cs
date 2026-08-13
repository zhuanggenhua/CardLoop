using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using GameCore;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using YooAsset.Editor;
using CoreInputSystem = GameCore.InputSystem;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using Gameplay.Tests.Support;

namespace Gameplay.Tests.Support.Editor
{
    /// <summary>
    /// 创建可重复使用的 Gameplay 地基测试场景。
    /// 后续吸收 StackCraft 模块时，测试对象统一追加到这张场景，不把参考 Title 场景当正式入口。
    /// </summary>
    public static class FoundationTestSceneMenu
    {
        /// <summary>统一 Gameplay 地基运行验收场景的固定资产路径。</summary>
        internal const string ScenePath = "Assets/Scenes/FoundationTest.unity";

        /// <summary>场景切换验收使用的第一张附加地图场景路径。</summary>
        internal const string MapScenePath = "Assets/Scenes/FoundationMapTest.unity";

        /// <summary>场景切换验收使用的第二张附加地图场景路径。</summary>
        internal const string SecondMapScenePath = "Assets/Scenes/FoundationSecondMapTest.unity";
        private const string ConfigPath = "Assets/Scenes/FoundationTestConfig.asset";
        private const string ConfigAssetName = "FoundationTestConfig";
        private const string CollectorSettingPath = "Assets/BundleCollectorSetting.asset";
        private const string DefaultPackageName = "DefaultPackage";
        private const string TestSceneGroupName = "Gameplay地基测试";
        private const string TestContentFolder = "Assets/Gameplay/Tests";
        private const string TestContentPath = TestContentFolder + "/地基测试卡牌.asset";
        private const string TestProductPath = TestContentFolder + "/地基测试产物.asset";
        private const string TestActionPath = TestContentFolder + "/地基测试行动.asset";
		private const string TestActionPlanPath = TestContentFolder + "/地基测试填槽行动.asset";
        private const string TestQuestPath = TestContentFolder + "/地基测试任务.asset";
        private const string TestScenarioPath = TestContentFolder + "/地基测试剧本.asset";
        private const string TestSceneScenarioPath = TestContentFolder + "/地基场景测试剧本.asset";
		private const string TestRegionPath = TestContentFolder + "/地基测试地区.asset";
		private const string TestBattleRegionPath = TestContentFolder + "/地基战斗测试地区.asset";
		private const string TestSceneRegionPath = TestContentFolder + "/地基场景测试地区.asset";
		private const string TestSecondSceneRegionPath = TestContentFolder + "/地基第二场景测试地区.asset";
        private const string TabletopTestFolder = TestContentFolder + "/牌桌";
        private const string TabletopCardViewPrefabPath = TabletopTestFolder + "/牌桌测试卡牌视图.prefab";
        private const string TabletopActionProgressViewPrefabPath = TabletopTestFolder + "/牌桌测试行动进度.prefab";
        private const string TabletopActionChoicePanelPrefabPath = TabletopTestFolder + "/TabletopActionChoicePanel.prefab";
		private const string TabletopActionPlanPanelPrefabPath = TabletopTestFolder + "/TabletopActionPlanPanel.prefab";
        private const string ScenarioTurnPanelPrefabPath = TabletopTestFolder + "/ScenarioTurnPanel.prefab";
        private const string TabletopCardInfoPanelPrefabPath = TabletopTestFolder + "/TabletopCardInfoPanel.prefab";
        private const string TabletopViewSettingsPath = TabletopTestFolder + "/牌桌测试视图设置.asset";
        private const string TabletopCardArtPath = "Assets/StackCraft/Sprites/Square.png";
        private const string TabletopCardViewAddress = "牌桌测试卡牌视图";
        private const string TabletopActionProgressViewAddress = "牌桌测试行动进度";
        private const string TabletopCardArtAddress = "Square";
        private const string TestPanelFontPath =
            TabletopTestFolder + "/地基测试中文字体.asset";
		private const string TestPanelFontSourcePath =
			"Packages/com.besty.unity-skills/Editor/UI/Fonts/UnitySkillsCN-Regular.ttf";
        private const string InputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string ConfigFieldName = "m_config";

        /// <summary>
        /// 从正式作者源重建 Gameplay 地基测试场景、测试资产、Build Settings 和 YooAsset 收集项。
        /// 该入口会覆盖三张固定测试场景的内容，只能用于地基自动化验收，不能作为关卡编辑器或正式剧本入口。
        /// </summary>
        [MenuItem("Gameplay/地基/重建测试场景")]
        public static void RebuildTestScene()
        {
            RebuildMapTestScene(MapScenePath, "FoundationMapMarker");
            RebuildMapTestScene(SecondMapScenePath, "FoundationSecondMapMarker");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameConfig config = EnsureConfigAsset();
            EnsureTestCardAsset();
            EnsureTestProductAsset();
            EnsureTestActionAsset();
			EnsureTestActionPlanAsset();
            EnsureTestQuestAsset();
            EnsureTestScenarioAssets();
            TabletopViewSettings viewSettings = EnsureTabletopTestAssets();

            GameObject gameManagerObject = new("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            gameManagerObject.AddComponent<ScenarioDirector>();
            gameManagerObject.AddComponent<SceneSystem>();
            gameManagerObject.AddComponent<TransitionSystem>();
            gameManagerObject.AddComponent<GameStateSystem>();
            CreateInputSystem(gameManagerObject);
            FieldInfo configField = typeof(GameManager).GetField(
                ConfigFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (configField == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(GameManager)} 不再包含 {ConfigFieldName} 序列化字段，测试场景生成器需要随正式入口同步更新。");
            }

            configField.SetValue(gameManager, config);
            EditorUtility.SetDirty(gameManager);

            SerializedObject serializedGameManager = new(gameManager);
            serializedGameManager.Update();
            SerializedProperty configProperty = serializedGameManager.FindProperty(ConfigFieldName);
            if (configProperty == null)
            {
                throw new MissingReferenceException(
                    $"Unity 无法读取 {nameof(GameManager)}.{ConfigFieldName}，测试场景不会保存不完整入口。");
            }

            if (configProperty.objectReferenceValue != config)
            {
                throw new MissingReferenceException(
                    $"{nameof(GameManager)} 的测试配置写入后回读不一致，场景不会保存不完整入口。");
            }

            EditorSceneManager.MarkSceneDirty(scene);

            GameObject testRoot = new("FoundationTest");
            GameObject cameraObject = new("Main Camera");
            cameraObject.transform.SetParent(testRoot.transform);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.075f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

			CreateTabletopTestRoot(testRoot.transform, viewSettings);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new MissingReferenceException($"无法保存测试场景：{ScenePath}");
            }

            VerifySavedSceneConfig(config);
            RemoveTestScenesFromBuildSettings();
            EnsureTestSceneCollector();
            AssetDatabase.SaveAssets();
            // batchmode 会在本方法返回后立即退出；先同步收完本轮导入，避免资源工作线程仍在通信时被强制清理。
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (!Application.isBatchMode)
            {
                Selection.activeObject = gameManagerObject;
            }

            Debug.Log(
                $"Gameplay 地基测试场景已重建：{ScenePath}。入口对象为 GameManager，配置资产为 {ConfigPath}。",
                gameManagerObject);
        }

        private static void VerifySavedSceneConfig(GameConfig expectedConfig)
        {
            Scene savedScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameManager savedGameManager = savedScene.GetRootGameObjects()
                .Select(root => root.GetComponent<GameManager>())
                .SingleOrDefault(component => component != null);
            if (savedGameManager == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有唯一的 {nameof(GameManager)} 根对象：{ScenePath}");
            }

            ScenarioDirector savedScenarioDirector = savedGameManager.GetComponent<ScenarioDirector>();
            if (savedScenarioDirector == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有 {nameof(ScenarioDirector)}：{ScenePath}");
            }

            if (savedGameManager.GetComponent<SceneSystem>() == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有正式 {nameof(SceneSystem)}：{ScenePath}");
            }

            if (savedGameManager.GetComponent<GameStateSystem>() == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有正式 {nameof(GameStateSystem)}：{ScenePath}");
            }

            if (savedGameManager.GetComponentInChildren<CoreInputSystem>() == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有正式 {nameof(CoreInputSystem)}：{ScenePath}");
            }

            if (savedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FoundationTestSceneHarness>(true))
                .Count() != 1)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有唯一的 {nameof(FoundationTestSceneHarness)}：{ScenePath}");
            }

            TabletopView savedTabletopView = savedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TabletopView>(true))
                .SingleOrDefault();
            SerializedProperty savedSettings = savedTabletopView == null
                ? null
                : new SerializedObject(savedTabletopView).FindProperty("m_settings");
            if (savedSettings?.objectReferenceValue == null)
            {
                throw new MissingReferenceException(
                    $"保存后的 {nameof(TabletopView)} 没有牌桌视图设置引用：{ScenePath}");
            }

            SerializedObject serializedGameManager = new(savedGameManager);
            SerializedProperty configProperty = serializedGameManager.FindProperty(ConfigFieldName);
            if (configProperty?.objectReferenceValue != expectedConfig)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有保留 {nameof(GameConfig)} 引用：{ScenePath}");
            }
        }

        private static GameConfig EnsureConfigAsset()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config != null)
            {
                if (!string.Equals(config.name, ConfigAssetName, System.StringComparison.Ordinal))
                {
                    config.name = ConfigAssetName;
                    EditorUtility.SetDirty(config);
                }

                return config;
            }

            config = ScriptableObject.CreateInstance<GameConfig>();
            config.name = ConfigAssetName;
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) ??
                throw new MissingReferenceException($"无法从 AssetDatabase 重新载入测试配置：{ConfigPath}");
        }

        private static void RebuildMapTestScene(string scenePath, string markerName)
        {
            Scene mapScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject marker = new(markerName);
            marker.AddComponent<MapInfo>();

            if (!EditorSceneManager.SaveScene(mapScene, scenePath))
            {
                throw new MissingReferenceException($"无法保存地基地图测试场景：{scenePath}");
            }
        }

        private static void EnsureTestSceneCollector()
        {
            BundleCollectorSetting setting =
                AssetDatabase.LoadAssetAtPath<BundleCollectorSetting>(CollectorSettingPath);
            if (setting == null)
            {
                throw new MissingReferenceException($"缺少 YooAsset 收集配置：{CollectorSettingPath}");
            }

            BundleCollectorPackage package = setting.GetPackage(DefaultPackageName);
            BundleCollectorGroup group = package.Groups.SingleOrDefault(candidate =>
                string.Equals(candidate.GroupName, TestSceneGroupName, System.StringComparison.Ordinal));
            if (group == null)
            {
                throw new MissingReferenceException(
                    $"YooAsset 默认包缺少测试场景分组：{TestSceneGroupName}");
            }

            BundleCollector collector = group.Collectors.SingleOrDefault(candidate =>
                string.Equals(candidate.CollectPath, ScenePath, System.StringComparison.Ordinal) ||
                string.Equals(candidate.CollectPath, "Assets/Scenes", System.StringComparison.Ordinal));
            if (collector == null)
            {
                collector = new BundleCollector();
                group.Collectors.Add(collector);
            }

            group.GroupDesc = "仅收集 Gameplay 地基入口场景和 YooAsset 附加地图测试场景";
            collector.CollectPath = "Assets/Scenes";
            collector.CollectorGUID = AssetDatabase.AssetPathToGUID(collector.CollectPath);
            collector.CollectorType = ECollectorType.MainAssetCollector;
            collector.AddressRuleName = nameof(AddressByFileName);
            collector.PackRuleName = nameof(PackDirectory);
            collector.FilterRuleName = nameof(FoundationSceneFilterRule);
            collector.AssetTags = "test";
            collector.UserData = string.Empty;
            EnsureExactTestAssetCollector(group, TabletopCardViewPrefabPath);
            EnsureExactTestAssetCollector(group, TabletopActionProgressViewPrefabPath);
            EnsureExactTestAssetCollector(group, TabletopActionChoicePanelPrefabPath);
			EnsureExactTestAssetCollector(group, TabletopActionPlanPanelPrefabPath);
			EnsureExactTestAssetCollector(group, ScenarioTurnPanelPrefabPath);
			EnsureExactTestAssetCollector(group, TabletopCardInfoPanelPrefabPath);
			EnsureExactTestAssetCollector(group, TabletopCardArtPath);
            EditorUtility.SetDirty(setting);
        }

		private static void EnsureTestCardAsset()
		{
			EnsureTestCharacterCardAsset(
				TestContentPath,
                FoundationTestSceneHarness.TestContentId,
                "Foundation Test Card",
                "Validates YooAsset content discovery, tabletop view and formal pointer input.",
				XTag.Faction_Player);
		}

		private static void EnsureTestCharacterCardAsset(
			string assetPath,
			string contentIdValue,
			string displayNameValue,
			string descriptionValue,
			int tagCode)
		{
			EnsureFolder(TestContentFolder);
			CardDefinition existing = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
			if (existing != null && existing is not CharacterCardDefinition)
			{
				AssetDatabase.DeleteAsset(assetPath);
				existing = null;
			}

			CharacterCardDefinition content = existing as CharacterCardDefinition;
			if (content == null)
			{
				content = ScriptableObject.CreateInstance<CharacterCardDefinition>();
				AssetDatabase.CreateAsset(content, assetPath);
			}

			SerializedObject serializedContent = WriteCardFields(
				content,
				contentIdValue,
				displayNameValue,
				descriptionValue,
				tagCode);
			SerializedProperty preset = serializedContent.FindProperty("m_abilitySystemPresetId");
			if (preset == null)
			{
				throw new MissingReferenceException(
					$"{nameof(CharacterCardDefinition)} 缺少 EX-GAS ASC 预设作者字段。");
			}
			preset.intValue = 1001;
			serializedContent.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(content);
		}

        private static void EnsureTestProductAsset()
        {
            EnsureTestCardAsset(
                TestProductPath,
                FoundationTestSceneHarness.TestProductContentId,
                "地基测试产物",
                "行动完成后由牌桌结果意图生成的测试卡牌。",
                XTag.Faction_Player);
        }

        private static void EnsureTestCardAsset(
            string assetPath,
            string contentIdValue,
            string displayNameValue,
            string descriptionValue,
            int tagCode)
        {
            EnsureFolder(TestContentFolder);
            CardDefinition content = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<CardDefinition>();
                AssetDatabase.CreateAsset(content, assetPath);
            }

			SerializedObject serializedContent = WriteCardFields(
				content,
				contentIdValue,
				displayNameValue,
				descriptionValue,
				tagCode);
			serializedContent.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(content);
		}

		private static SerializedObject WriteCardFields(
			CardDefinition content,
			string contentIdValue,
			string displayNameValue,
			string descriptionValue,
			int tagCode)
		{
			SerializedObject serializedContent = WriteCommonContentFields(
				content,
				contentIdValue,
				displayNameValue,
				descriptionValue);

            SerializedProperty cardArt = serializedContent.FindProperty("m_cardArt");
            SerializedProperty cardArtAddress = cardArt?.FindPropertyRelative("Address");
            if (cardArtAddress == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(CardDefinition)} 的卡面资源字段已变更，测试内容生成器需要同步更新。");
            }

            cardArtAddress.stringValue = TabletopCardArtAddress;
            WriteIntArray(serializedContent.FindProperty("m_tagCodes"), tagCode);
#if UNITY_EDITOR
            SerializedProperty cardArtGuid = cardArt.FindPropertyRelative("Guid");
            SerializedProperty cardArtLocked = cardArt.FindPropertyRelative("Locked");
            if (cardArtGuid != null)
            {
                cardArtGuid.stringValue = AssetDatabase.AssetPathToGUID(TabletopCardArtPath);
            }

            if (cardArtLocked != null)
            {
                cardArtLocked.boolValue = true;
            }
#endif
			return serializedContent;
		}

        private static void EnsureTestActionAsset()
        {
            ActionDefinition action =
                AssetDatabase.LoadAssetAtPath<ActionDefinition>(TestActionPath);
            if (action == null)
            {
                action = ScriptableObject.CreateInstance<ActionDefinition>();
                AssetDatabase.CreateAsset(action, TestActionPath);
            }

            SerializedObject serializedAction = WriteCommonContentFields(
                action,
                FoundationTestSceneHarness.TestActionContentId,
                "Test Action",
                "仅用于验证行动作者源、参与条件、回合进度和权威随机结果经过 YooAsset 进入正式链路。");
            SerializedProperty slots = serializedAction.FindProperty("m_participationSlots");
            SerializedProperty turnCost = serializedAction.FindProperty("m_turnCost");
            SerializedProperty resultIntents = serializedAction.FindProperty("m_resultIntents");
            SerializedProperty resultBranches = serializedAction.FindProperty("m_resultBranches");
            if (slots == null || turnCost == null || resultIntents == null || resultBranches == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(ActionDefinition)} 的回合消耗、参与槽位、结果意图或随机分支字段已变更，测试行动生成器需要同步更新。");
            }

            turnCost.intValue = 2;
            slots.arraySize = 1;
            SerializedProperty slot = slots.GetArrayElementAtIndex(0);
            RequireRelative(slot, "m_displayName").stringValue = "参与者";
            RequireRelative(slot, "m_minimumParticipants").intValue = 2;
            RequireRelative(slot, "m_maximumParticipants").intValue = 2;
            WriteContentIdArray(RequireRelative(slot, "m_allowedContentIds"), "test.foundation.card");
            WriteIntArray(RequireRelative(slot, "m_requiredAllContentTagCodes"), XTag.Faction);
            WriteIntArray(
                RequireRelative(slot, "m_requiredAnyContentTagCodes"),
                XTag.Faction_Player,
                XTag.State);
            WriteIntArray(RequireRelative(slot, "m_requiredNoneContentTagCodes"), XTag.State_Debuff);
			WriteIntArray(RequireRelative(slot, "m_requiredAllAbilitySystemTagCodes"), XTag.State);
			WriteIntArray(
				RequireRelative(slot, "m_requiredAnyAbilitySystemTagCodes"),
				XTag.Ability_Die,
				XTag.State_Buff);
            WriteIntArray(RequireRelative(slot, "m_requiredNoneAbilitySystemTagCodes"), XTag.State_Debuff);

            resultIntents.arraySize = 1;
            SerializedProperty removeIntent = resultIntents.GetArrayElementAtIndex(0);
            removeIntent.managedReferenceValue = new RemoveCardsResultIntent();

            resultBranches.arraySize = 2;
            WriteTestResultBranch(
                resultBranches.GetArrayElementAtIndex(0),
                weight: 1,
                productCount: 1);
            WriteTestResultBranch(
                resultBranches.GetArrayElementAtIndex(1),
                weight: 3,
                productCount: 2);

            serializedAction.ApplyModifiedPropertiesWithoutUndo();
            action.EnsureLocalAuthoringKeys();
            EditorUtility.SetDirty(action);
        }

		private static void EnsureTestActionPlanAsset()
		{
			ActionDefinition action =
				AssetDatabase.LoadAssetAtPath<ActionDefinition>(TestActionPlanPath);
			if (action == null)
			{
				action = ScriptableObject.CreateInstance<ActionDefinition>();
				AssetDatabase.CreateAsset(action, TestActionPlanPath);
			}

			SerializedObject serializedAction = WriteCommonContentFields(
				action,
				FoundationTestSceneHarness.TestActionPlanContentId,
				"协同行动",
				"仅用于验证多个行动候选、牌桌行动计划与 UIKit 填槽交互。");
			SerializedProperty slots = serializedAction.FindProperty("m_participationSlots");
			SerializedProperty turnCost = serializedAction.FindProperty("m_turnCost");
			SerializedProperty resultIntents = serializedAction.FindProperty("m_resultIntents");
			SerializedProperty resultBranches = serializedAction.FindProperty("m_resultBranches");
			if (slots == null || turnCost == null || resultIntents == null || resultBranches == null)
			{
				throw new MissingReferenceException(
					$"{nameof(ActionDefinition)} 的作者字段已变更，填槽测试行动生成器需要同步更新。");
			}

			turnCost.intValue = 1;
			slots.arraySize = 1;
			SerializedProperty slot = slots.GetArrayElementAtIndex(0);
			RequireRelative(slot, "m_displayName").stringValue = "参与者";
			RequireRelative(slot, "m_minimumParticipants").intValue = 3;
			RequireRelative(slot, "m_maximumParticipants").intValue = 3;
			WriteContentIdArray(
				RequireRelative(slot, "m_allowedContentIds"),
				FoundationTestSceneHarness.TestContentId);
			WriteIntArray(RequireRelative(slot, "m_requiredAllContentTagCodes"), XTag.Faction);
			WriteIntArray(RequireRelative(slot, "m_requiredAnyContentTagCodes"), XTag.Faction_Player);
			WriteIntArray(RequireRelative(slot, "m_requiredNoneContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAllAbilitySystemTagCodes"), XTag.State);
			WriteIntArray(RequireRelative(slot, "m_requiredAnyAbilitySystemTagCodes"), XTag.Ability_Die);
			WriteIntArray(RequireRelative(slot, "m_requiredNoneAbilitySystemTagCodes"), XTag.State_Debuff);
			resultIntents.arraySize = 0;
			resultBranches.arraySize = 0;
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
			action.EnsureLocalAuthoringKeys();
			EditorUtility.SetDirty(action);
		}

        private static void WriteTestResultBranch(
            SerializedProperty branch,
            int weight,
            int productCount)
        {
            RequireRelative(branch, "m_weight").intValue = weight;
            SerializedProperty branchIntents = RequireRelative(branch, "m_resultIntents");
            branchIntents.arraySize = 1;
            SerializedProperty createIntent = branchIntents.GetArrayElementAtIndex(0);
            createIntent.managedReferenceValue = new CreateCardsResultIntent();
            RequireRelative(createIntent, "m_contentId")
                .FindPropertyRelative("m_value").stringValue =
                    FoundationTestSceneHarness.TestProductContentId;
            RequireRelative(createIntent, "m_count").intValue = productCount;
        }

        private static void EnsureTestQuestAsset()
        {
            QuestDefinition quest =
                AssetDatabase.LoadAssetAtPath<QuestDefinition>(TestQuestPath);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(quest, TestQuestPath);
            }

            SerializedObject serializedQuest = WriteCommonContentFields(
                quest,
                FoundationTestSceneHarness.TestQuestContentId,
                "地基测试任务",
                "完成一次地基测试行动后完成，用于验证剧本任务子项消费已结算行动事实。");
            WriteContentIdArray(
                serializedQuest.FindProperty("m_prerequisiteQuestIds"));
            SerializedProperty tasks = serializedQuest.FindProperty("m_tasks");
            if (tasks == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(QuestDefinition)} 的任务子项字段已变更，测试任务生成器需要同步更新。");
            }

            tasks.arraySize = 1;
            SerializedProperty task = tasks.GetArrayElementAtIndex(0);
            task.managedReferenceValue = new ActionCompletionQuestTaskDefinition();
            serializedQuest.ApplyModifiedPropertiesWithoutUndo();
            serializedQuest.Update();
            task = serializedQuest.FindProperty("m_tasks").GetArrayElementAtIndex(0);
            RequireRelative(task, "m_actionId")
                .FindPropertyRelative("m_value").stringValue =
                    FoundationTestSceneHarness.TestActionContentId;
            RequireRelative(task, "m_requiredCompletionCount").intValue = 1;
            serializedQuest.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quest);
        }

        private static void EnsureTestScenarioAssets()
        {
			EnsureTestRegionAsset(
				TestRegionPath,
				FoundationTestSceneHarness.TestRegionContentId,
				"地基测试地区",
				"统一牌桌测试使用的地区。",
				string.Empty);
			EnsureTestRegionAsset(
				TestBattleRegionPath,
				FoundationTestSceneHarness.TestBattleRegionContentId,
				"地基战斗测试地区",
				"阶段 B 组合验收中承接旅行角色与牌桌战斗的第二地区。",
				string.Empty);
            EnsureTestScenarioAsset(
                TestScenarioPath,
                FoundationTestSceneHarness.TestScenarioContentId,
                "地基测试剧本",
                "仅用于验证活动剧本统一拥有任务集合和世界回合生命周期。",
				FoundationTestSceneHarness.TestRegionContentId,
				FoundationTestSceneHarness.TestRegionContentId,
				FoundationTestSceneHarness.TestBattleRegionContentId);

            SceneAsset initialScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MapScenePath);
            if (initialScene == null)
            {
                throw new MissingReferenceException(
                    $"场景型测试剧本缺少初始场景资产：{MapScenePath}");
            }

			SceneAsset secondScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SecondMapScenePath);
			if (secondScene == null)
			{
				throw new MissingReferenceException(
					$"场景型测试剧本缺少第二场景资产：{SecondMapScenePath}");
			}

			EnsureTestRegionAsset(
				TestSceneRegionPath,
				FoundationTestSceneHarness.TestSceneRegionContentId,
				"地基场景测试地区",
				"场景旅行测试使用的第一地区。",
				initialScene.name);
			EnsureTestRegionAsset(
				TestSecondSceneRegionPath,
				FoundationTestSceneHarness.TestSecondSceneRegionContentId,
				"地基第二场景测试地区",
				"场景旅行测试使用的第二地区。",
				secondScene.name);

			EnsureTestScenarioAsset(
                TestSceneScenarioPath,
                FoundationTestSceneHarness.TestSceneScenarioContentId,
                "地基场景测试剧本",
                "仅用于验证剧本导演通过正式场景系统组合和释放剧本场景。",
				FoundationTestSceneHarness.TestSceneRegionContentId,
				FoundationTestSceneHarness.TestSceneRegionContentId,
				FoundationTestSceneHarness.TestSecondSceneRegionContentId);
        }

		private static void EnsureTestRegionAsset(
			string assetPath,
			string contentId,
			string displayName,
			string description,
			string sceneAddress)
		{
			ScenarioRegionDefinition region =
				AssetDatabase.LoadAssetAtPath<ScenarioRegionDefinition>(assetPath);
			if (region == null)
			{
				region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
				AssetDatabase.CreateAsset(region, assetPath);
			}

			SerializedObject serializedRegion = WriteCommonContentFields(
				region,
				contentId,
				displayName,
				description);
			SerializedProperty address = serializedRegion.FindProperty("m_sceneAddress");
			if (address == null)
			{
				throw new MissingReferenceException(
					$"{nameof(ScenarioRegionDefinition)} 缺少场景地址作者字段。");
			}
			address.stringValue = sceneAddress;
			WriteTestTabletopPlacement(serializedRegion.FindProperty("m_tabletopPlacement"));
			serializedRegion.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(region);
		}

        private static void EnsureTestScenarioAsset(
            string assetPath,
            string contentId,
            string displayName,
			string description,
			string initialRegionId,
			params string[] regionIds)
        {
            ScenarioDefinition scenario =
                AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(assetPath);
            if (scenario == null)
            {
                scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
                AssetDatabase.CreateAsset(scenario, assetPath);
            }

            SerializedObject serializedScenario = WriteCommonContentFields(
                scenario,
                contentId,
                displayName,
                description);
			SerializedProperty initialRegion = serializedScenario.FindProperty("m_initialRegionId");
			if (initialRegion == null)
			{
				throw new MissingReferenceException(
					$"{nameof(ScenarioDefinition)} 缺少初始地区作者字段，测试剧本不会保存不完整配置。");
			}
			initialRegion.FindPropertyRelative("m_value").stringValue = initialRegionId;
			WriteContentIdArray(serializedScenario.FindProperty("m_regionIds"), regionIds);
			SerializedProperty secondsPerTurn = serializedScenario.FindProperty("m_secondsPerTurn");
			if (secondsPerTurn == null)
			{
				throw new MissingReferenceException(
					$"{nameof(ScenarioDefinition)} 缺少每回合秒数作者字段，测试剧本不会保存不完整配置。");
			}
			secondsPerTurn.floatValue = 0.35f;
            WriteContentIdArray(
                serializedScenario.FindProperty("m_questIds"),
                FoundationTestSceneHarness.TestQuestContentId);
            WriteTestBattleFormation(
                serializedScenario.FindProperty("m_battleFormationRules"));
            serializedScenario.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(scenario);
        }

        private static void WriteTestTabletopPlacement(SerializedProperty placement)
        {
            if (placement == null)
            {
                throw new MissingReferenceException(
					$"{nameof(ScenarioRegionDefinition)} 缺少牌桌放置作者字段。");
            }

            SerializedProperty bounds = placement.FindPropertyRelative("m_bounds");
            SerializedProperty restrictedAreas = placement.FindPropertyRelative("m_restrictedAreas");
            SerializedProperty cardSize = placement.FindPropertyRelative("m_cardSize");
            SerializedProperty stackStep = placement.FindPropertyRelative("m_stackStep");
            if (bounds == null || restrictedAreas == null || cardSize == null ||
                stackStep == null)
            {
                throw new MissingReferenceException("牌桌放置作者字段已变更，测试剧本生成器需要同步更新。");
            }

            bounds.rectValue = new Rect(-5f, -3f, 10f, 6f);
            restrictedAreas.arraySize = 0;
            cardSize.vector2Value = new Vector2(1.4f, 2f);
            stackStep.vector2Value = new Vector2(0.35f, 0.22f);
        }

        private static void WriteTestBattleFormation(SerializedProperty formationRules)
        {
			SerializedProperty sideLayouts =
				RequireRelative(formationRules, "m_sideLayouts");
			sideLayouts.arraySize = 2;
			WriteBattleFormationLayout(
				sideLayouts.GetArrayElementAtIndex(0),
				new Vector2(-1.5f, 0f),
                Vector2.right,
                Vector2.down,
                2);
			WriteBattleFormationLayout(
				sideLayouts.GetArrayElementAtIndex(1),
				new Vector2(1.5f, 0f),
                Vector2.left,
                Vector2.up,
                2);
        }

		private static void WriteBattleFormationLayout(
			SerializedProperty layout,
			Vector2 centerOffset,
            Vector2 columnStep,
            Vector2 rankStep,
            int columnsPerRank)
        {
			RequireRelative(layout, "m_centerOffset").vector2Value = centerOffset;
            RequireRelative(layout, "m_columnStep").vector2Value = columnStep;
            RequireRelative(layout, "m_rankStep").vector2Value = rankStep;
            RequireRelative(layout, "m_columnsPerRank").intValue = columnsPerRank;
        }

        private static SerializedProperty RequireRelative(SerializedProperty parent, string relativeName)
        {
            SerializedProperty property = parent?.FindPropertyRelative(relativeName);
            if (property == null)
            {
                throw new MissingReferenceException($"测试作者数据缺少序列化字段：{relativeName}");
            }

            return property;
        }

        private static void WriteContentIdArray(SerializedProperty property, params string[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty value = property.GetArrayElementAtIndex(i)?.FindPropertyRelative("m_value");
                if (value == null)
                {
                    throw new MissingReferenceException("测试行动无法写入唯一内容 ID 数组。");
                }

                value.stringValue = values[i];
            }
        }

        private static void WriteIntArray(SerializedProperty property, params int[] values)
        {
            if (property == null)
            {
                throw new MissingReferenceException("测试作者数据缺少 EX-GAS 标签数组字段。");
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).intValue = values[i];
            }
        }

        private static SerializedObject WriteCommonContentFields(
            ContentAsset content,
            string contentIdValue,
            string displayNameValue,
            string descriptionValue)
        {
            var serializedContent = new SerializedObject(content);
            SerializedProperty contentId =
                serializedContent.FindProperty("m_contentId")?.FindPropertyRelative("m_value");
            SerializedProperty displayName = serializedContent.FindProperty("m_displayName");
            SerializedProperty description = serializedContent.FindProperty("m_description");
            if (contentId == null || displayName == null || description == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(ContentAsset)} 的作者字段已变更，地基测试内容生成器需要同步更新。");
            }

            contentId.stringValue = contentIdValue;
            displayName.stringValue = displayNameValue;
            description.stringValue = descriptionValue;
            return serializedContent;
        }

        private static TabletopViewSettings EnsureTabletopTestAssets()
        {
            EnsureFolder(TabletopTestFolder);
            Sprite cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TabletopCardArtPath);
            if (cardSprite == null)
            {
                throw new MissingReferenceException($"缺少 StackCraft 临时卡牌图片：{TabletopCardArtPath}");
            }

            EnsureTabletopCardViewPrefab(cardSprite);
            EnsureTabletopActionProgressViewPrefab(cardSprite);
			EnsureTestPanelFont();
            EnsureTabletopActionChoicePanelPrefab();
			EnsureTabletopActionPlanPanelPrefab();
			EnsureScenarioTurnPanelPrefab();
			EnsureTabletopCardInfoPanelPrefab();
            TabletopViewSettings settings =
                AssetDatabase.LoadAssetAtPath<TabletopViewSettings>(TabletopViewSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TabletopViewSettings>();
                AssetDatabase.CreateAsset(settings, TabletopViewSettingsPath);
            }

            settings.name = "牌桌测试视图设置";

            SerializedObject serializedSettings = new(settings);
            SerializedProperty prefabReference = serializedSettings.FindProperty("m_cardViewPrefab");
            SerializedProperty prefabAddress = prefabReference?.FindPropertyRelative("Address");
            SerializedProperty actionProgressPrefabReference =
                serializedSettings.FindProperty("m_actionProgressViewPrefab");
            SerializedProperty actionProgressPrefabAddress =
                actionProgressPrefabReference?.FindPropertyRelative("Address");
            SerializedProperty stackDepthStep = serializedSettings.FindProperty("m_stackDepthStep");
            SerializedProperty baseSortingOrder = serializedSettings.FindProperty("m_baseSortingOrder");
            SerializedProperty battleBaseSortingOrder = serializedSettings.FindProperty("m_battleBaseSortingOrder");
            SerializedProperty dragFollowSharpness = serializedSettings.FindProperty("m_dragFollowSharpness");
            if (prefabAddress == null || actionProgressPrefabAddress == null ||
                stackDepthStep == null ||
                baseSortingOrder == null || battleBaseSortingOrder == null ||
                dragFollowSharpness == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(TabletopViewSettings)} 的作者字段已变更，测试资产生成器需要同步更新。");
            }

            prefabAddress.stringValue = TabletopCardViewAddress;
            SerializedProperty prefabGuid = prefabReference.FindPropertyRelative("Guid");
            SerializedProperty prefabLocked = prefabReference.FindPropertyRelative("Locked");
            if (prefabGuid != null)
            {
                prefabGuid.stringValue = AssetDatabase.AssetPathToGUID(TabletopCardViewPrefabPath);
            }

            if (prefabLocked != null)
            {
                prefabLocked.boolValue = true;
            }

            actionProgressPrefabAddress.stringValue = TabletopActionProgressViewAddress;
            SerializedProperty actionProgressPrefabGuid =
                actionProgressPrefabReference.FindPropertyRelative("Guid");
            SerializedProperty actionProgressPrefabLocked =
                actionProgressPrefabReference.FindPropertyRelative("Locked");
            if (actionProgressPrefabGuid != null)
            {
                actionProgressPrefabGuid.stringValue =
                    AssetDatabase.AssetPathToGUID(TabletopActionProgressViewPrefabPath);
            }

            if (actionProgressPrefabLocked != null)
            {
                actionProgressPrefabLocked.boolValue = true;
            }

            stackDepthStep.floatValue = -0.05f;
            baseSortingOrder.intValue = 10;
            battleBaseSortingOrder.intValue = 100;
            dragFollowSharpness.floatValue = 12f;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { TabletopViewSettingsPath });
            return AssetDatabase.LoadAssetAtPath<TabletopViewSettings>(TabletopViewSettingsPath) ??
                throw new MissingReferenceException(
                    $"无法从 AssetDatabase 重新载入牌桌测试视图设置：{TabletopViewSettingsPath}");
        }

		private static TMP_FontAsset EnsureTestPanelFont()
		{
			TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestPanelFontPath);
			if (existing != null && existing.atlasTextures != null &&
				existing.atlasTextures.Length > 0 &&
				existing.atlasTextures[0] != null &&
				AssetDatabase.Contains(existing.atlasTextures[0]) &&
				existing.material != null && AssetDatabase.Contains(existing.material))
			{
				return existing;
			}
			if (existing != null)
			{
				AssetDatabase.DeleteAsset(TestPanelFontPath);
			}

			Font source = AssetDatabase.LoadAssetAtPath<Font>(TestPanelFontSourcePath);
			if (source == null)
			{
				throw new MissingReferenceException(
					$"缺少地基测试中文字体来源：{TestPanelFontSourcePath}");
			}
			TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
				source,
				48,
				5,
				GlyphRenderMode.SDFAA,
				1024,
				1024);
			if (fontAsset == null)
			{
				throw new System.InvalidOperationException("TextMeshPro 无法创建地基测试中文字体资产。");
			}
			fontAsset.name = "地基测试中文字体";
			AssetDatabase.CreateAsset(fontAsset, TestPanelFontPath);
			for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
			{
				Texture2D atlas = fontAsset.atlasTextures[i];
				if (atlas != null)
				{
					atlas.name = $"地基测试中文字体 Atlas {i}";
					AssetDatabase.AddObjectToAsset(atlas, fontAsset);
				}
			}
			if (fontAsset.material != null)
			{
				fontAsset.material.name = "地基测试中文字体 Material";
				AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
			}
			EditorUtility.SetDirty(fontAsset);
			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset(TestPanelFontPath, ImportAssetOptions.ForceUpdate);
			return fontAsset;
		}

        private static void EnsureTabletopCardViewPrefab(Sprite cardSprite)
        {
            GameObject root = new("牌桌测试卡牌视图");
            try
            {
                root.transform.localScale = Vector3.one;
                SpriteRenderer artworkRenderer = root.AddComponent<SpriteRenderer>();
                artworkRenderer.color = new Color(0.18f, 0.42f, 0.52f, 1f);

                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.size = new Vector3(1f, 1f, 0.15f);

                GameObject highlightRoot = new("候选高亮");
                highlightRoot.transform.SetParent(root.transform, false);
                highlightRoot.transform.localPosition = new Vector3(0f, 0f, -0.03f);
                highlightRoot.transform.localScale = new Vector3(1.08f, 1.06f, 1f);
                SpriteRenderer highlightRenderer = highlightRoot.AddComponent<SpriteRenderer>();
                highlightRenderer.sprite = cardSprite;
                highlightRenderer.color = new Color(0.25f, 0.95f, 0.45f, 0.42f);
                highlightRoot.SetActive(false);

				GameObject characterStatusRoot = new("角色状态");
				characterStatusRoot.transform.SetParent(root.transform, false);
				characterStatusRoot.transform.localPosition = new Vector3(0f, -0.39f, -0.05f);
				characterStatusRoot.transform.localScale = new Vector3(0.82f, 0.16f, 1f);
				SpriteRenderer statusBackground = characterStatusRoot.AddComponent<SpriteRenderer>();
				statusBackground.sprite = cardSprite;
				statusBackground.color = new Color(0.03f, 0.05f, 0.06f, 0.92f);

				GameObject healthTextObject = new("生命");
				healthTextObject.transform.SetParent(characterStatusRoot.transform, false);
				healthTextObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
				healthTextObject.transform.localScale = new Vector3(1.22f, 6.25f, 1f);
				TextMeshPro healthLabel = healthTextObject.AddComponent<TextMeshPro>();
				healthLabel.font = EnsureTestPanelFont();
				healthLabel.fontSize = 1.8f;
				healthLabel.alignment = TextAlignmentOptions.Center;
				healthLabel.color = Color.white;
				healthLabel.text = string.Empty;
				healthLabel.enableWordWrapping = false;
				healthLabel.overflowMode = TextOverflowModes.Overflow;
				healthLabel.rectTransform.sizeDelta = new Vector2(1.8f, 0.6f);
				characterStatusRoot.SetActive(false);

                TabletopCardView cardView = root.AddComponent<TabletopCardView>();
                SerializedObject serializedView = new(cardView);
                serializedView.FindProperty("m_artworkRenderer").objectReferenceValue = artworkRenderer;
                serializedView.FindProperty("m_highlightRoot").objectReferenceValue = highlightRoot;
				serializedView.FindProperty("m_characterStatusRoot").objectReferenceValue = characterStatusRoot;
				serializedView.FindProperty("m_healthLabel").objectReferenceValue = healthLabel;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(root, TabletopCardViewPrefabPath) == null)
                {
                    throw new MissingReferenceException($"无法保存牌桌测试卡牌视图：{TabletopCardViewPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureTabletopActionProgressViewPrefab(Sprite cardSprite)
        {
            GameObject root = new("牌桌测试行动进度");
            try
            {
                root.transform.localScale = new Vector3(0.86f, 0.1f, 1f);
                SpriteRenderer backgroundRenderer = root.AddComponent<SpriteRenderer>();
                backgroundRenderer.sprite = cardSprite;
                backgroundRenderer.color = new Color(0.03f, 0.05f, 0.07f, 0.88f);

                GameObject fillRoot = new("进度填充");
                fillRoot.transform.SetParent(root.transform, false);
                fillRoot.transform.localScale = new Vector3(0.9f, 0.72f, 1f);
                SpriteRenderer fillRenderer = fillRoot.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = cardSprite;
                fillRenderer.color = new Color(0.24f, 0.86f, 0.94f, 1f);

                TabletopActionProgressView progressView =
                    root.AddComponent<TabletopActionProgressView>();
                SerializedObject serializedProgressView = new(progressView);
                serializedProgressView.FindProperty("m_backgroundRenderer").objectReferenceValue =
                    backgroundRenderer;
                serializedProgressView.FindProperty("m_fillRenderer").objectReferenceValue =
                    fillRenderer;
                serializedProgressView.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(false);

                if (PrefabUtility.SaveAsPrefabAsset(root, TabletopActionProgressViewPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存牌桌测试行动进度视图：{TabletopActionProgressViewPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureTabletopActionChoicePanelPrefab()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                TestPanelFontPath);
            if (uiSprite == null)
            {
                throw new MissingReferenceException("Unity UGUI 缺少内置 UI 皮肤图片，无法创建行动选择测试面板。");
            }
            if (fontAsset == null)
            {
                    throw new MissingReferenceException(
                    $"缺少测试面板字体资产：{TestPanelFontPath}");
            }

            GameObject root = new(
                "TabletopActionChoicePanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(TabletopActionChoicePanel));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                Image overlay = root.GetComponent<Image>();
                overlay.color = new Color(0f, 0f, 0f, 0f);
                overlay.raycastTarget = true;

                GameObject windowObject = new("ActionWindow", typeof(RectTransform), typeof(Image));
                windowObject.transform.SetParent(root.transform, false);
                RectTransform window = windowObject.GetComponent<RectTransform>();
                SetAnchoredRect(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(900f, 520f), Vector2.zero);
                Image windowImage = windowObject.GetComponent<Image>();
                windowImage.sprite = uiSprite;
                windowImage.type = Image.Type.Sliced;
                windowImage.color = new Color(0.035f, 0.055f, 0.075f, 0.96f);

                TextMeshProUGUI title = CreatePanelText(
                    "Title",
                    window,
                    fontAsset,
                    "可用行动",
                    52f);
                SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(800f, 70f), new Vector2(0f, -30f));

                GameObject choiceRootObject = new("ActionChoices", typeof(RectTransform), typeof(VerticalLayoutGroup));
                choiceRootObject.transform.SetParent(window, false);
                RectTransform choiceRoot = choiceRootObject.GetComponent<RectTransform>();
                SetAnchoredRect(choiceRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(800f, 250f), new Vector2(0f, 12f));
                VerticalLayoutGroup choiceLayout = choiceRootObject.GetComponent<VerticalLayoutGroup>();
                choiceLayout.spacing = 16f;
                choiceLayout.childAlignment = TextAnchor.UpperCenter;
                choiceLayout.childControlWidth = true;
                choiceLayout.childControlHeight = true;
                choiceLayout.childForceExpandWidth = true;
                choiceLayout.childForceExpandHeight = false;

                GameObject choiceTemplateObject = new(
                    "ActionChoiceTemplate",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(LayoutElement));
                choiceTemplateObject.transform.SetParent(choiceRoot, false);
                RectTransform choiceTemplateRect = choiceTemplateObject.GetComponent<RectTransform>();
                choiceTemplateRect.sizeDelta = new Vector2(800f, 90f);
                Image choiceImage = choiceTemplateObject.GetComponent<Image>();
                choiceImage.sprite = uiSprite;
                choiceImage.type = Image.Type.Sliced;
                choiceImage.color = new Color(0.12f, 0.32f, 0.4f, 1f);
                Button choiceButton = choiceTemplateObject.GetComponent<Button>();
                choiceButton.targetGraphic = choiceImage;
                LayoutElement choiceLayoutElement = choiceTemplateObject.GetComponent<LayoutElement>();
                choiceLayoutElement.preferredWidth = 800f;
                choiceLayoutElement.preferredHeight = 90f;
                TextMeshProUGUI choiceText = CreatePanelText(
                    "Label",
                    choiceTemplateRect,
                    fontAsset,
                    "Action",
                    42f);
                SetStretchRect(choiceText.rectTransform, 24f);
                choiceTemplateObject.SetActive(false);

                GameObject cancelObject = new(
                    "Cancel",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                cancelObject.transform.SetParent(window, false);
                RectTransform cancelRect = cancelObject.GetComponent<RectTransform>();
                SetAnchoredRect(cancelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(260f, 70f), new Vector2(0f, 28f));
                Image cancelImage = cancelObject.GetComponent<Image>();
                cancelImage.sprite = uiSprite;
                cancelImage.type = Image.Type.Sliced;
                cancelImage.color = new Color(0.18f, 0.2f, 0.23f, 1f);
                Button cancelButton = cancelObject.GetComponent<Button>();
                cancelButton.targetGraphic = cancelImage;
                TextMeshProUGUI cancelText = CreatePanelText(
                    "Label",
                    cancelRect,
                    fontAsset,
                    "Cancel",
                    36f);
                SetStretchRect(cancelText.rectTransform, 16f);

                TabletopActionChoicePanel panel = root.GetComponent<TabletopActionChoicePanel>();
                SerializedObject serializedPanel = new(panel);
                SerializedProperty windowProperty = serializedPanel.FindProperty("m_window");
                SerializedProperty titleProperty = serializedPanel.FindProperty("m_titleLabel");
                SerializedProperty choiceRootProperty = serializedPanel.FindProperty("m_choiceRoot");
                SerializedProperty choiceTemplateProperty = serializedPanel.FindProperty("m_choiceTemplate");
                SerializedProperty cancelProperty = serializedPanel.FindProperty("m_cancelButton");
                if (windowProperty == null || titleProperty == null || choiceRootProperty == null ||
                    choiceTemplateProperty == null || cancelProperty == null)
                {
                    throw new MissingReferenceException(
                        $"{nameof(TabletopActionChoicePanel)} 的预制体引用字段已变更，测试资源生成器需要同步更新。");
                }

                windowProperty.objectReferenceValue = window;
                titleProperty.objectReferenceValue = title;
                choiceRootProperty.objectReferenceValue = choiceRoot;
                choiceTemplateProperty.objectReferenceValue = choiceButton;
                cancelProperty.objectReferenceValue = cancelButton;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(root, TabletopActionChoicePanelPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存牌桌行动选择面板：{TabletopActionChoicePanelPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

		private static void EnsureTabletopActionPlanPanelPrefab()
		{
			Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
			TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestPanelFontPath);
			if (uiSprite == null || fontAsset == null)
			{
				throw new MissingReferenceException("缺少行动计划测试面板所需的内置图片或字体。");
			}

			GameObject root = new(
				"TabletopActionPlanPanel",
				typeof(RectTransform),
				typeof(TabletopActionPlanPanel));
			try
			{
				RectTransform rootRect = root.GetComponent<RectTransform>();
				rootRect.anchorMin = Vector2.zero;
				rootRect.anchorMax = Vector2.one;
				rootRect.offsetMin = Vector2.zero;
				rootRect.offsetMax = Vector2.zero;

				GameObject windowObject = new("PlanWindow", typeof(RectTransform), typeof(Image));
				windowObject.transform.SetParent(root.transform, false);
				RectTransform window = windowObject.GetComponent<RectTransform>();
				SetAnchoredRect(
					window,
					new Vector2(1f, 0.5f),
					new Vector2(1f, 0.5f),
					new Vector2(520f, 620f),
					new Vector2(-296f, 0f));
				Image windowImage = windowObject.GetComponent<Image>();
				windowImage.sprite = uiSprite;
				windowImage.type = Image.Type.Sliced;
				windowImage.color = new Color(0.045f, 0.06f, 0.07f, 0.97f);

				TextMeshProUGUI title = CreatePanelText(
					"Title",
					window,
					fontAsset,
					"行动计划",
					38f);
				SetAnchoredRect(
					title.rectTransform,
					new Vector2(0.5f, 1f),
					new Vector2(0.5f, 1f),
					new Vector2(448f, 72f),
					new Vector2(0f, -30f));

				Button previousPlanButton = CreatePlanNavigationButton(
					"PreviousPlan",
					window,
					uiSprite,
					fontAsset,
					"<",
					new Vector2(-90f, -104f));
				TextMeshProUGUI planIndex = CreatePanelText(
					"PlanIndex",
					window,
					fontAsset,
					"1/1",
					24f);
				SetAnchoredRect(
					planIndex.rectTransform,
					new Vector2(0.5f, 1f),
					new Vector2(0.5f, 1f),
					new Vector2(96f, 52f),
					new Vector2(0f, -104f));
				Button nextPlanButton = CreatePlanNavigationButton(
					"NextPlan",
					window,
					uiSprite,
					fontAsset,
					">",
					new Vector2(90f, -104f));

				GameObject slotRootObject = new(
					"PlanSlots",
					typeof(RectTransform),
					typeof(VerticalLayoutGroup));
				slotRootObject.transform.SetParent(window, false);
				RectTransform slotRoot = slotRootObject.GetComponent<RectTransform>();
				SetAnchoredRect(
					slotRoot,
					new Vector2(0.5f, 0.5f),
					new Vector2(0.5f, 0.5f),
					new Vector2(448f, 350f),
					new Vector2(0f, 24f));
				VerticalLayoutGroup slotLayout = slotRootObject.GetComponent<VerticalLayoutGroup>();
				slotLayout.spacing = 14f;
				slotLayout.childControlWidth = true;
				slotLayout.childControlHeight = true;
				slotLayout.childForceExpandWidth = true;
				slotLayout.childForceExpandHeight = false;

				GameObject slotTemplateObject = new(
					"ActionPlanSlotTemplate",
					typeof(RectTransform),
					typeof(Image),
					typeof(LayoutElement),
					typeof(TabletopActionPlanSlotView));
				slotTemplateObject.transform.SetParent(slotRoot, false);
				RectTransform slotTemplateRect = slotTemplateObject.GetComponent<RectTransform>();
				slotTemplateRect.sizeDelta = new Vector2(448f, 112f);
				Image slotImage = slotTemplateObject.GetComponent<Image>();
				slotImage.sprite = uiSprite;
				slotImage.type = Image.Type.Sliced;
				slotImage.color = new Color(0.1f, 0.24f, 0.27f, 1f);
				LayoutElement slotElement = slotTemplateObject.GetComponent<LayoutElement>();
				slotElement.preferredHeight = 112f;
				TextMeshProUGUI slotLabel = CreatePanelText(
					"Label",
					slotTemplateRect,
					fontAsset,
					"参与者  0/3",
					30f);
				SetAnchoredRect(
					slotLabel.rectTransform,
					new Vector2(0f, 0.5f),
					new Vector2(0f, 0.5f),
					new Vector2(310f, 80f),
					new Vector2(20f, 0f));

				GameObject removeObject = new(
					"RemoveLastCard",
					typeof(RectTransform),
					typeof(Image),
					typeof(Button));
				removeObject.transform.SetParent(slotTemplateRect, false);
				RectTransform removeRect = removeObject.GetComponent<RectTransform>();
				SetAnchoredRect(
					removeRect,
					new Vector2(1f, 0.5f),
					new Vector2(1f, 0.5f),
					new Vector2(88f, 64f),
					new Vector2(-18f, 0f));
				Image removeImage = removeObject.GetComponent<Image>();
				removeImage.sprite = uiSprite;
				removeImage.type = Image.Type.Sliced;
				removeImage.color = new Color(0.28f, 0.16f, 0.16f, 1f);
				Button removeButton = removeObject.GetComponent<Button>();
				removeButton.targetGraphic = removeImage;
				TextMeshProUGUI removeText = CreatePanelText(
					"Label",
					removeRect,
					fontAsset,
					"-",
					38f);
				SetStretchRect(removeText.rectTransform, 8f);

				TabletopActionPlanSlotView slotView =
					slotTemplateObject.GetComponent<TabletopActionPlanSlotView>();
				SerializedObject serializedSlot = new(slotView);
				serializedSlot.FindProperty("m_label").objectReferenceValue = slotLabel;
				serializedSlot.FindProperty("m_removeButton").objectReferenceValue = removeButton;
				serializedSlot.ApplyModifiedPropertiesWithoutUndo();
				slotTemplateObject.SetActive(false);

				Button submitButton = CreatePlanButton(
					"SubmitPlan",
					window,
					uiSprite,
					fontAsset,
					"开始",
					new Vector2(-120f, 34f),
					new Color(0.12f, 0.4f, 0.28f, 1f));
				Button cancelButton = CreatePlanButton(
					"CancelPlan",
					window,
					uiSprite,
					fontAsset,
					"取消",
					new Vector2(120f, 34f),
					new Color(0.2f, 0.22f, 0.24f, 1f));

				TabletopActionPlanPanel panel = root.GetComponent<TabletopActionPlanPanel>();
				SerializedObject serializedPanel = new(panel);
				serializedPanel.FindProperty("m_titleLabel").objectReferenceValue = title;
				serializedPanel.FindProperty("m_slotRoot").objectReferenceValue = slotRoot;
				serializedPanel.FindProperty("m_slotTemplate").objectReferenceValue = slotView;
				serializedPanel.FindProperty("m_planIndexLabel").objectReferenceValue = planIndex;
				serializedPanel.FindProperty("m_previousPlanButton").objectReferenceValue = previousPlanButton;
				serializedPanel.FindProperty("m_nextPlanButton").objectReferenceValue = nextPlanButton;
				serializedPanel.FindProperty("m_submitButton").objectReferenceValue = submitButton;
				serializedPanel.FindProperty("m_cancelButton").objectReferenceValue = cancelButton;
				serializedPanel.ApplyModifiedPropertiesWithoutUndo();

				if (PrefabUtility.SaveAsPrefabAsset(root, TabletopActionPlanPanelPrefabPath) == null)
				{
					throw new MissingReferenceException(
						$"无法保存牌桌行动计划面板：{TabletopActionPlanPanelPrefabPath}");
				}
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		private static Button CreatePlanNavigationButton(
			string name,
			RectTransform parent,
			Sprite sprite,
			TMP_FontAsset fontAsset,
			string label,
			Vector2 position)
		{
			GameObject buttonObject = new(
				name,
				typeof(RectTransform),
				typeof(Image),
				typeof(Button));
			buttonObject.transform.SetParent(parent, false);
			RectTransform rect = buttonObject.GetComponent<RectTransform>();
			SetAnchoredRect(
				rect,
				new Vector2(0.5f, 1f),
				new Vector2(0.5f, 1f),
				new Vector2(64f, 52f),
				position);
			Image image = buttonObject.GetComponent<Image>();
			image.sprite = sprite;
			image.type = Image.Type.Sliced;
			image.color = new Color(0.12f, 0.24f, 0.27f, 1f);
			Button button = buttonObject.GetComponent<Button>();
			button.targetGraphic = image;
			TextMeshProUGUI text = CreatePanelText(
				"Label",
				rect,
				fontAsset,
				label,
				26f);
			SetStretchRect(text.rectTransform, 8f);
			return button;
		}

		private static Button CreatePlanButton(
			string name,
			RectTransform parent,
			Sprite sprite,
			TMP_FontAsset fontAsset,
			string label,
			Vector2 position,
			Color color)
		{
			GameObject buttonObject = new(
				name,
				typeof(RectTransform),
				typeof(Image),
				typeof(Button));
			buttonObject.transform.SetParent(parent, false);
			RectTransform rect = buttonObject.GetComponent<RectTransform>();
			SetAnchoredRect(
				rect,
				new Vector2(0.5f, 0f),
				new Vector2(0.5f, 0f),
				new Vector2(200f, 72f),
				position);
			Image image = buttonObject.GetComponent<Image>();
			image.sprite = sprite;
			image.type = Image.Type.Sliced;
			image.color = Color.white;
			Button button = buttonObject.GetComponent<Button>();
			button.targetGraphic = image;
			ColorBlock colors = button.colors;
			colors.normalColor = color;
			colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
			colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
			colors.selectedColor = color;
			colors.disabledColor = new Color(0.2f, 0.22f, 0.24f, 0.72f);
			colors.colorMultiplier = 1f;
			button.colors = colors;
			TextMeshProUGUI text = CreatePanelText(
				"Label",
				rect,
				fontAsset,
				label,
				30f);
			SetStretchRect(text.rectTransform, 12f);
			return button;
		}

        private static void EnsureScenarioTurnPanelPrefab()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestPanelFontPath);
            if (uiSprite == null)
            {
                throw new MissingReferenceException("Unity UGUI 缺少内置 UI 皮肤图片，无法创建回合 HUD。");
            }
            if (fontAsset == null)
            {
                throw new MissingReferenceException($"缺少测试面板字体资产：{TestPanelFontPath}");
            }

            GameObject root = new(
                "ScenarioTurnPanel",
                typeof(RectTransform),
                typeof(ScenarioTurnPanel));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                GameObject controlObject = new("TurnControl", typeof(RectTransform), typeof(Image));
                controlObject.transform.SetParent(root.transform, false);
                RectTransform controlRect = controlObject.GetComponent<RectTransform>();
                SetAnchoredRect(
                    controlRect,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(460f, 136f),
                    new Vector2(0f, 32f));
                Image controlImage = controlObject.GetComponent<Image>();
                controlImage.sprite = uiSprite;
                controlImage.type = Image.Type.Sliced;
                controlImage.color = new Color(0.035f, 0.055f, 0.075f, 0.94f);

                TextMeshProUGUI turnLabel = CreatePanelText(
                    "TurnLabel",
                    controlRect,
                    fontAsset,
                    "Day 1 / Turn 0",
                    32f);
                SetAnchoredRect(
                    turnLabel.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(400f, 42f),
                    new Vector2(0f, 35f));

				GameObject progressBackgroundObject = new(
					"DayProgressBackground",
					typeof(RectTransform),
					typeof(Image));
				progressBackgroundObject.transform.SetParent(controlRect, false);
				RectTransform progressBackgroundRect =
					progressBackgroundObject.GetComponent<RectTransform>();
				SetAnchoredRect(
					progressBackgroundRect,
					new Vector2(0.5f, 0.5f),
					new Vector2(0.5f, 0.5f),
					new Vector2(400f, 12f),
					new Vector2(0f, 6f));
				Image progressBackground = progressBackgroundObject.GetComponent<Image>();
				progressBackground.sprite = uiSprite;
				progressBackground.type = Image.Type.Sliced;
				progressBackground.color = new Color(0.02f, 0.025f, 0.03f, 0.9f);

				GameObject progressFillObject = new(
					"DayProgressFill",
					typeof(RectTransform),
					typeof(Image));
				progressFillObject.transform.SetParent(progressBackgroundRect, false);
				SetStretchRect(progressFillObject.GetComponent<RectTransform>(), 1f);
				Image progressFill = progressFillObject.GetComponent<Image>();
				progressFill.sprite = uiSprite;
				progressFill.type = Image.Type.Filled;
				progressFill.fillMethod = Image.FillMethod.Horizontal;
				progressFill.fillOrigin = 0;
				progressFill.color = new Color(0.22f, 0.72f, 0.52f, 1f);
				progressFill.fillAmount = 0f;

                GameObject confirmObject = new(
                    "ConfirmTurn",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                confirmObject.transform.SetParent(controlRect, false);
                RectTransform confirmRect = confirmObject.GetComponent<RectTransform>();
                SetAnchoredRect(
                    confirmRect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(280f, 58f),
                    new Vector2(0f, -28f));
                Image confirmImage = confirmObject.GetComponent<Image>();
                confirmImage.sprite = uiSprite;
                confirmImage.type = Image.Type.Sliced;
                confirmImage.color = new Color(0.17f, 0.5f, 0.55f, 1f);
                Button confirmButton = confirmObject.GetComponent<Button>();
                confirmButton.targetGraphic = confirmImage;
				ColorBlock confirmColors = confirmButton.colors;
				confirmColors.normalColor = Color.white;
				confirmColors.highlightedColor = new Color(0.9f, 1f, 1f, 1f);
				confirmColors.pressedColor = new Color(0.72f, 0.82f, 0.84f, 1f);
				confirmColors.selectedColor = Color.white;
				confirmColors.disabledColor = new Color(0.32f, 0.36f, 0.4f, 0.9f);
				confirmColors.colorMultiplier = 1f;
				confirmButton.colors = confirmColors;
                TextMeshProUGUI confirmLabel = CreatePanelText(
                    "Label",
                    confirmRect,
                    fontAsset,
                    "Advance Turn",
                    30f);
                SetStretchRect(confirmLabel.rectTransform, 16f);

                ScenarioTurnPanel panel = root.GetComponent<ScenarioTurnPanel>();
                SerializedObject serializedPanel = new(panel);
                SerializedProperty turnLabelProperty = serializedPanel.FindProperty("m_turnLabel");
				SerializedProperty dayProgressFillProperty = serializedPanel.FindProperty("m_dayProgressFill");
				SerializedProperty confirmTurnLabelProperty = serializedPanel.FindProperty("m_confirmTurnLabel");
                SerializedProperty confirmButtonProperty = serializedPanel.FindProperty("m_confirmTurnButton");
                if (turnLabelProperty == null || dayProgressFillProperty == null ||
					confirmTurnLabelProperty == null || confirmButtonProperty == null)
                {
                    throw new MissingReferenceException(
                        $"{nameof(ScenarioTurnPanel)} 的预制体引用字段已变更，测试资源生成器需要同步更新。");
                }

                turnLabelProperty.objectReferenceValue = turnLabel;
				dayProgressFillProperty.objectReferenceValue = progressFill;
				confirmTurnLabelProperty.objectReferenceValue = confirmLabel;
                confirmButtonProperty.objectReferenceValue = confirmButton;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(root, ScenarioTurnPanelPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存剧本回合 HUD：{ScenarioTurnPanelPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureTabletopCardInfoPanelPrefab()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestPanelFontPath);
            if (uiSprite == null || fontAsset == null)
            {
                throw new MissingReferenceException("缺少卡牌详情测试面板所需的内置图片或字体。");
            }

            GameObject root = new(
                "TabletopCardInfoPanel",
                typeof(RectTransform),
                typeof(TabletopCardInfoPanel));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                GameObject contentRoot = new("CardInfo", typeof(RectTransform), typeof(Image));
                contentRoot.transform.SetParent(root.transform, false);
                RectTransform contentRect = contentRoot.GetComponent<RectTransform>();
                SetAnchoredRect(
                    contentRect,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(500f, 260f),
                    new Vector2(-32f, 32f));
                Image background = contentRoot.GetComponent<Image>();
                background.sprite = uiSprite;
                background.type = Image.Type.Sliced;
                background.color = new Color(0.035f, 0.055f, 0.075f, 0.94f);
                background.raycastTarget = false;

                TextMeshProUGUI title = CreatePanelText(
                    "Title",
                    contentRect,
                    fontAsset,
                    string.Empty,
                    36f);
                title.alignment = TextAlignmentOptions.TopLeft;
                SetAnchoredRect(
                    title.rectTransform,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(448f, 50f),
                    new Vector2(26f, -22f));

                TextMeshProUGUI description = CreatePanelText(
                    "Description",
                    contentRect,
                    fontAsset,
                    string.Empty,
                    28f);
                description.alignment = TextAlignmentOptions.TopLeft;
                description.enableWordWrapping = true;
                SetAnchoredRect(
                    description.rectTransform,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(448f, 152f),
                    new Vector2(26f, -84f));

                TabletopCardInfoPanel panel = root.GetComponent<TabletopCardInfoPanel>();
                SerializedObject serializedPanel = new(panel);
                SerializedProperty contentRootProperty = serializedPanel.FindProperty("m_contentRoot");
                SerializedProperty titleProperty = serializedPanel.FindProperty("m_titleLabel");
                SerializedProperty descriptionProperty = serializedPanel.FindProperty("m_descriptionLabel");
                if (contentRootProperty == null || titleProperty == null || descriptionProperty == null)
                {
                    throw new MissingReferenceException(
                        $"{nameof(TabletopCardInfoPanel)} 的预制体引用字段已变更，测试资源生成器需要同步更新。");
                }

                contentRootProperty.objectReferenceValue = contentRoot;
                titleProperty.objectReferenceValue = title;
                descriptionProperty.objectReferenceValue = description;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(root, TabletopCardInfoPanelPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存牌桌卡牌详情测试面板：{TabletopCardInfoPanelPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static TextMeshProUGUI CreatePanelText(
            string objectName,
            Transform parent,
            TMP_FontAsset fontAsset,
            string text,
            float fontSize)
        {
            GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
            textComponent.font = fontAsset;
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = Color.white;
            textComponent.raycastTarget = false;
            return textComponent;
        }

        private static void SetAnchoredRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 size,
            Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetStretchRect(RectTransform rect, float horizontalPadding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, 0f);
            rect.offsetMax = new Vector2(-horizontalPadding, 0f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void CreateInputSystem(GameObject gameManagerObject)
        {
            InputActionAsset actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            if (actionsAsset == null)
            {
                throw new MissingReferenceException($"缺少正式输入作者源：{InputActionsAssetPath}");
            }

            GameObject inputObject = new("InputSystem");
            inputObject.transform.SetParent(gameManagerObject.transform, false);
            PlayerInput playerInput = inputObject.AddComponent<PlayerInput>();
            playerInput.actions = actionsAsset;
            playerInput.defaultActionMap = EActionMap.Gameplay.ToString();
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            inputObject.AddComponent<CoreInputSystem>();
        }

		private static void CreateTabletopTestRoot(
			Transform parent,
			TabletopViewSettings viewSettings)
		{
			if (viewSettings == null || !EditorUtility.IsPersistent(viewSettings))
            {
                throw new MissingReferenceException(
					$"牌桌测试视图设置不是可保存的作者资产：{TabletopViewSettingsPath}");
			}
            GameObject tabletopRoot = new("牌桌测试");
            tabletopRoot.transform.SetParent(parent, false);

            TabletopView tabletopView = tabletopRoot.AddComponent<TabletopView>();
            SerializedObject serializedTabletopView = new(tabletopView);
            serializedTabletopView.FindProperty("m_settings").objectReferenceValue = viewSettings;
            serializedTabletopView.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tabletopView);

            SerializedObject verifiedTabletopView = new(tabletopView);
            if (verifiedTabletopView.FindProperty("m_settings")?.objectReferenceValue != viewSettings)
            {
                throw new MissingReferenceException(
                    $"{nameof(TabletopView)} 的视图设置写入后回读不一致，拒绝保存不完整测试场景。");
            }

            TabletopCardDragInput dragInput = tabletopRoot.AddComponent<TabletopCardDragInput>();
			TabletopInteraction tabletopInteraction = tabletopRoot.AddComponent<TabletopInteraction>();

            FoundationTestSceneHarness controller =
                tabletopRoot.AddComponent<FoundationTestSceneHarness>();
			SerializedObject serializedController = new(controller);
			serializedController.FindProperty("m_tabletopView").objectReferenceValue = tabletopView;
			serializedController.FindProperty("m_dragInput").objectReferenceValue = dragInput;
			serializedController.FindProperty("m_tabletopInteraction").objectReferenceValue = tabletopInteraction;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureExactTestAssetCollector(BundleCollectorGroup group, string assetPath)
        {
            BundleCollector collector = group.Collectors.SingleOrDefault(candidate =>
                string.Equals(candidate.CollectPath, assetPath, System.StringComparison.Ordinal));
            if (collector == null)
            {
                collector = new BundleCollector();
                group.Collectors.Add(collector);
            }

            collector.CollectPath = assetPath;
            collector.CollectorGUID = AssetDatabase.AssetPathToGUID(assetPath);
            collector.CollectorType = ECollectorType.MainAssetCollector;
            collector.AddressRuleName = nameof(AddressByFileName);
            collector.PackRuleName = nameof(PackDirectory);
            collector.FilterRuleName = nameof(CollectAll);
            collector.AssetTags = "test";
            collector.UserData = string.Empty;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void RemoveTestScenesFromBuildSettings()
        {
            string[] nonBuildScenes =
            {
                ScenePath,
                MapScenePath,
                SecondMapScenePath,
                "Assets/StackCraft/Scenes/Title.unity",
                "Assets/StackCraft/Scenes/Main.unity",
                "Assets/StackCraft/Scenes/Island.unity"
            };

            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(scene => !nonBuildScenes.Contains(scene.path))
                .ToArray();
        }
    }
}
