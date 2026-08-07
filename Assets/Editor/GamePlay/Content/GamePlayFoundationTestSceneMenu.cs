using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using GameCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YooAsset.Editor;
using CoreInputSystem = GameCore.InputSystem;

namespace GamePlay.Editor
{
    /// <summary>
    /// 创建可重复使用的 GamePlay 地基测试场景。
    /// 后续吸收 StackCraft 模块时，测试对象统一追加到这张场景，不把参考 Title 场景当正式入口。
    /// </summary>
    public static class GamePlayFoundationTestSceneMenu
    {
        /// <summary>统一 GamePlay 地基运行验收场景的固定资产路径。</summary>
        internal const string ScenePath = "Assets/Scenes/GamePlayFoundationTest.unity";

        /// <summary>场景切换验收使用的第一张附加地图场景路径。</summary>
        internal const string MapScenePath = "Assets/Scenes/GamePlayFoundationMapTest.unity";

        /// <summary>场景切换验收使用的第二张附加地图场景路径。</summary>
        internal const string SecondMapScenePath = "Assets/Scenes/GamePlayFoundationSecondMapTest.unity";
        private const string ConfigPath = "Assets/Scenes/GamePlayFoundationTestConfig.asset";
        private const string CollectorSettingPath = "Assets/BundleCollectorSetting.asset";
        private const string DefaultPackageName = "DefaultPackage";
        private const string TestSceneGroupName = "GamePlay地基测试";
        private const string TestContentFolder = "Assets/GamePlay/Tests";
        private const string TestContentPath = TestContentFolder + "/地基测试卡牌.asset";
        private const string TestProductPath = TestContentFolder + "/地基测试产物.asset";
        private const string TestActionPath = TestContentFolder + "/地基测试行动.asset";
        private const string TestTurnTimingPath = TestContentFolder + "/地基测试回合时间换算.asset";
        private const string TabletopTestFolder = TestContentFolder + "/牌桌";
        private const string TabletopCardViewPrefabPath = TabletopTestFolder + "/牌桌测试卡牌视图.prefab";
        private const string TabletopCardPresentationSettingsPath = TabletopTestFolder + "/牌桌测试表现设置.asset";
        private const string TabletopCardArtPath = "Assets/StackCraft/Sprites/Square.png";
        private const string TabletopCardViewAddress = "牌桌测试卡牌视图";
        private const string TabletopCardArtAddress = "Square";
        private const string InputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string ConfigFieldName = "m_config";

        /// <summary>
        /// 从正式作者源重建 GamePlay 地基测试场景、测试资产、Build Settings 和 YooAsset 收集项。
        /// 该入口会覆盖三张固定测试场景的内容，只能用于地基自动化验收，不能作为关卡编辑器或正式剧本入口。
        /// </summary>
        [MenuItem("GamePlay/地基/重建测试场景")]
        public static void RebuildTestScene()
        {
            RebuildMapTestScene(MapScenePath, "GamePlayFoundationMapMarker");
            RebuildMapTestScene(SecondMapScenePath, "GamePlayFoundationSecondMapMarker");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameConfig config = EnsureConfigAsset();
            EnsureTestCardAsset();
            EnsureTestProductAsset();
            EnsureTestActionAsset();
            EnsureTestTurnTimingAsset();
            TabletopCardPresentationSettings presentationSettings = EnsureTabletopTestAssets();

            GameObject gameManagerObject = new("GameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            gameManagerObject.AddComponent<GamePlayContentSystem>();
            gameManagerObject.AddComponent<GamePlayWorldTurnSystem>();
            gameManagerObject.AddComponent<TabletopCardActionSystem>();
            gameManagerObject.AddComponent<MapSystem>();
            gameManagerObject.AddComponent<TransitionSystem>();
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

            GameObject testRoot = new("GamePlayFoundationTest");
            GameObject cameraObject = new("Main Camera");
            cameraObject.transform.SetParent(testRoot.transform);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.075f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            CreateTabletopTestRoot(testRoot.transform, camera, presentationSettings);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new MissingReferenceException($"无法保存测试场景：{ScenePath}");
            }

            VerifySavedSceneConfig(config);
            AddToBuildSettings(ScenePath);
            AddToBuildSettings(MapScenePath);
            AddToBuildSettings(SecondMapScenePath);
            EnsureTestSceneCollector();
            AssetDatabase.SaveAssets();
            // batchmode 会在本方法返回后立即退出；先同步收完本轮导入，避免资源工作线程仍在通信时被强制清理。
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (!Application.isBatchMode)
            {
                Selection.activeObject = gameManagerObject;
            }

            Debug.Log(
                $"GamePlay 地基测试场景已重建：{ScenePath}。入口对象为 GameManager，配置资产为 {ConfigPath}。",
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

            if (savedGameManager.GetComponent<GamePlayContentSystem>() == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有 {nameof(GamePlayContentSystem)}：{ScenePath}");
            }

            if (savedGameManager.GetComponent<TabletopCardActionSystem>() == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有 {nameof(TabletopCardActionSystem)}：{ScenePath}");
            }

            if (savedGameManager.GetComponent<GamePlayWorldTurnSystem>() == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有 {nameof(GamePlayWorldTurnSystem)}：{ScenePath}");
            }

            if (savedGameManager.GetComponentInChildren<CoreInputSystem>() == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有正式 {nameof(CoreInputSystem)}：{ScenePath}");
            }

            if (savedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TabletopCardFoundationTestController>(true))
                .Count() != 1)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有唯一的 {nameof(TabletopCardFoundationTestController)}：{ScenePath}");
            }

            TabletopCardViewProjector savedProjector = savedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TabletopCardViewProjector>(true))
                .SingleOrDefault();
            SerializedProperty savedSettings = savedProjector == null
                ? null
                : new SerializedObject(savedProjector).FindProperty("m_settings");
            if (savedSettings?.objectReferenceValue == null)
            {
                throw new MissingReferenceException(
                    $"保存后的 {nameof(TabletopCardViewProjector)} 没有表现设置引用：{ScenePath}");
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
                return config;
            }

            config = ScriptableObject.CreateInstance<GameConfig>();
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

            group.GroupDesc = "仅收集 GamePlay 地基入口场景和 YooAsset 附加地图测试场景";
            collector.CollectPath = "Assets/Scenes";
            collector.CollectorGUID = AssetDatabase.AssetPathToGUID(collector.CollectPath);
            collector.CollectorType = ECollectorType.MainAssetCollector;
            collector.AddressRuleName = nameof(AddressByFileName);
            collector.PackRuleName = nameof(PackDirectory);
            collector.FilterRuleName = nameof(CollectGamePlayFoundationScenes);
            collector.AssetTags = "test";
            collector.UserData = string.Empty;
            EnsureExactTestAssetCollector(group, TabletopCardViewPrefabPath);
            EnsureExactTestAssetCollector(group, TabletopCardArtPath);
            EditorUtility.SetDirty(setting);
        }

        private static void EnsureTestCardAsset()
        {
            EnsureTestCardAsset(
                TestContentPath,
                TabletopCardFoundationTestController.TestContentId,
                "地基测试卡牌",
                "仅用于验证 YooAsset 内容发现、牌桌视图和正式拖拽输入链路。",
                XTag.Faction_Player);
        }

        private static void EnsureTestProductAsset()
        {
            EnsureTestCardAsset(
                TestProductPath,
                TabletopCardFoundationTestController.TestProductContentId,
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
            GamePlayCardDefinition content = AssetDatabase.LoadAssetAtPath<GamePlayCardDefinition>(assetPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<GamePlayCardDefinition>();
                AssetDatabase.CreateAsset(content, assetPath);
            }

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
                    $"{nameof(GamePlayCardDefinition)} 的卡面资源字段已变更，测试内容生成器需要同步更新。");
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
            serializedContent.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(content);
        }

        private static void EnsureTestActionAsset()
        {
            GamePlayActionDefinition action =
                AssetDatabase.LoadAssetAtPath<GamePlayActionDefinition>(TestActionPath);
            if (action == null)
            {
                action = ScriptableObject.CreateInstance<GamePlayActionDefinition>();
                AssetDatabase.CreateAsset(action, TestActionPath);
            }

            SerializedObject serializedAction = WriteCommonContentFields(
                action,
                TabletopCardFoundationTestController.TestActionContentId,
                "地基测试行动",
                "仅用于验证行动作者源、参与条件、回合进度和权威随机结果经过 YooAsset 进入正式链路。");
            SerializedProperty slots = serializedAction.FindProperty("m_participationSlots");
            SerializedProperty turnCost = serializedAction.FindProperty("m_turnCost");
            SerializedProperty resultIntents = serializedAction.FindProperty("m_resultIntents");
            SerializedProperty resultBranches = serializedAction.FindProperty("m_resultBranches");
            if (slots == null || turnCost == null || resultIntents == null || resultBranches == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(GamePlayActionDefinition)} 的回合消耗、参与槽位、结果意图或随机分支字段已变更，测试行动生成器需要同步更新。");
            }

            turnCost.intValue = 2;
            slots.arraySize = 1;
            SerializedProperty slot = slots.GetArrayElementAtIndex(0);
            RequireRelative(slot, "m_key").stringValue = "participant";
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
            WriteIntArray(RequireRelative(slot, "m_requiredAllAbilitySystemTagCodes"), XTag.Ability_Gun);
            WriteIntArray(
                RequireRelative(slot, "m_requiredAnyAbilitySystemTagCodes"),
                XTag.Ability_Gun_Shoot,
                XTag.State_Buff);
            WriteIntArray(RequireRelative(slot, "m_requiredNoneAbilitySystemTagCodes"), XTag.State_Debuff);

            resultIntents.arraySize = 1;
            SerializedProperty removeIntent = resultIntents.GetArrayElementAtIndex(0);
            removeIntent.managedReferenceValue = new TabletopCardRemoveResultIntent();
            RequireRelative(removeIntent, "m_slotKey").stringValue = "participant";

            resultBranches.arraySize = 2;
            WriteTestResultBranch(
                resultBranches.GetArrayElementAtIndex(0),
                "one-product",
                weight: 1,
                productCount: 1);
            WriteTestResultBranch(
                resultBranches.GetArrayElementAtIndex(1),
                "two-products",
                weight: 3,
                productCount: 2);

            serializedAction.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
        }

        private static void WriteTestResultBranch(
            SerializedProperty branch,
            string key,
            int weight,
            int productCount)
        {
            RequireRelative(branch, "m_key").stringValue = key;
            RequireRelative(branch, "m_weight").intValue = weight;
            SerializedProperty branchIntents = RequireRelative(branch, "m_resultIntents");
            branchIntents.arraySize = 1;
            SerializedProperty createIntent = branchIntents.GetArrayElementAtIndex(0);
            createIntent.managedReferenceValue = new TabletopCardCreateResultIntent();
            RequireRelative(createIntent, "m_contentId")
                .FindPropertyRelative("m_value").stringValue =
                    TabletopCardFoundationTestController.TestProductContentId;
            RequireRelative(createIntent, "m_count").intValue = productCount;
            RequireRelative(createIntent, "m_anchorSlotKey").stringValue = "participant";
        }

        private static void EnsureTestTurnTimingAsset()
        {
            GamePlayTurnTimingDefinition timingDefinition =
                AssetDatabase.LoadAssetAtPath<GamePlayTurnTimingDefinition>(TestTurnTimingPath);
            if (timingDefinition == null)
            {
                timingDefinition = ScriptableObject.CreateInstance<GamePlayTurnTimingDefinition>();
                AssetDatabase.CreateAsset(timingDefinition, TestTurnTimingPath);
            }

            SerializedObject serializedTiming = WriteCommonContentFields(
                timingDefinition,
                TabletopCardFoundationTestController.TestTurnTimingContentId,
                "地基测试回合时间换算",
                "仅用于验证普通行动切换即时制时从唯一回合规则换算推进速度。");
            SerializedProperty secondsPerTurn = serializedTiming.FindProperty("m_secondsPerTurn");
            if (secondsPerTurn == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(GamePlayTurnTimingDefinition)} 的每回合秒数字段已变更，测试规则生成器需要同步更新。");
            }

            secondsPerTurn.floatValue = 0.35f;
            serializedTiming.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(timingDefinition);
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
            GamePlayContentAsset content,
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
                    $"{nameof(GamePlayContentAsset)} 的作者字段已变更，地基测试内容生成器需要同步更新。");
            }

            contentId.stringValue = contentIdValue;
            displayName.stringValue = displayNameValue;
            description.stringValue = descriptionValue;
            return serializedContent;
        }

        private static TabletopCardPresentationSettings EnsureTabletopTestAssets()
        {
            EnsureFolder(TabletopTestFolder);
            Sprite cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TabletopCardArtPath);
            if (cardSprite == null)
            {
                throw new MissingReferenceException($"缺少 StackCraft 临时卡牌图片：{TabletopCardArtPath}");
            }

            EnsureTabletopCardViewPrefab(cardSprite);
            TabletopCardPresentationSettings settings =
                AssetDatabase.LoadAssetAtPath<TabletopCardPresentationSettings>(TabletopCardPresentationSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TabletopCardPresentationSettings>();
                AssetDatabase.CreateAsset(settings, TabletopCardPresentationSettingsPath);
            }

            SerializedObject serializedSettings = new(settings);
            SerializedProperty prefabReference = serializedSettings.FindProperty("m_cardViewPrefab");
            SerializedProperty prefabAddress = prefabReference?.FindPropertyRelative("Address");
            SerializedProperty stackVisualStep = serializedSettings.FindProperty("m_stackVisualStep");
            SerializedProperty baseSortingOrder = serializedSettings.FindProperty("m_baseSortingOrder");
            SerializedProperty dragFollowSharpness = serializedSettings.FindProperty("m_dragFollowSharpness");
            if (prefabAddress == null || stackVisualStep == null ||
                baseSortingOrder == null || dragFollowSharpness == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(TabletopCardPresentationSettings)} 的作者字段已变更，测试资产生成器需要同步更新。");
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

            stackVisualStep.vector3Value = new Vector3(0.35f, 0.22f, -0.05f);
            baseSortingOrder.intValue = 10;
            dragFollowSharpness.floatValue = 12f;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { TabletopCardPresentationSettingsPath });
            return AssetDatabase.LoadAssetAtPath<TabletopCardPresentationSettings>(TabletopCardPresentationSettingsPath) ??
                throw new MissingReferenceException(
                    $"无法从 AssetDatabase 重新载入牌桌测试表现设置：{TabletopCardPresentationSettingsPath}");
        }

        private static void EnsureTabletopCardViewPrefab(Sprite cardSprite)
        {
            GameObject root = new("牌桌测试卡牌视图");
            try
            {
                root.transform.localScale = new Vector3(1.4f, 2f, 1f);
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

                TabletopCardView cardView = root.AddComponent<TabletopCardView>();
                SerializedObject serializedView = new(cardView);
                serializedView.FindProperty("m_artworkRenderer").objectReferenceValue = artworkRenderer;
                serializedView.FindProperty("m_highlightRoot").objectReferenceValue = highlightRoot;
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
            Camera camera,
            TabletopCardPresentationSettings presentationSettings)
        {
            if (presentationSettings == null || !EditorUtility.IsPersistent(presentationSettings))
            {
                throw new MissingReferenceException(
                    $"牌桌测试表现设置不是可保存的作者资产：{TabletopCardPresentationSettingsPath}");
            }

            GameObject tabletopRoot = new("牌桌测试");
            tabletopRoot.transform.SetParent(parent, false);

            GameObject viewRoot = new("卡牌视图");
            viewRoot.transform.SetParent(tabletopRoot.transform, false);

            TabletopCardViewProjector projector = tabletopRoot.AddComponent<TabletopCardViewProjector>();
            SerializedObject serializedProjector = new(projector);
            serializedProjector.FindProperty("m_settings").objectReferenceValue = presentationSettings;
            serializedProjector.FindProperty("m_viewRoot").objectReferenceValue = viewRoot.transform;
            serializedProjector.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(projector);

            SerializedObject verifiedProjector = new(projector);
            if (verifiedProjector.FindProperty("m_settings")?.objectReferenceValue != presentationSettings)
            {
                throw new MissingReferenceException(
                    $"{nameof(TabletopCardViewProjector)} 的表现设置写入后回读不一致，拒绝保存不完整测试场景。");
            }

            TabletopCardDragInput dragInput = tabletopRoot.AddComponent<TabletopCardDragInput>();
            SerializedObject serializedDragInput = new(dragInput);
            serializedDragInput.FindProperty("m_tabletopCamera").objectReferenceValue = camera;
            serializedDragInput.FindProperty("m_tablePlane").objectReferenceValue = tabletopRoot.transform;
            serializedDragInput.FindProperty("m_cardHitMask").intValue = ~0;
            serializedDragInput.FindProperty("m_maxHitDistance").floatValue = 100f;
            serializedDragInput.FindProperty("m_dragStartDistance").floatValue = 0.15f;
            serializedDragInput.ApplyModifiedPropertiesWithoutUndo();

            TabletopCardFoundationTestController controller =
                tabletopRoot.AddComponent<TabletopCardFoundationTestController>();
            SerializedObject serializedController = new(controller);
            serializedController.FindProperty("m_viewProjector").objectReferenceValue = projector;
            serializedController.FindProperty("m_dragInput").objectReferenceValue = dragInput;
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

        private static void AddToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == scenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
