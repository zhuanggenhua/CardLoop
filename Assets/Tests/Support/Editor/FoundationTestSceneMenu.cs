using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using GameCore;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YokiFrame;
using UnityEngine.TextCore.LowLevel;
using YooAsset;
using YooAsset.Editor;
using CoreInputSystem = GameCore.InputSystem;
using Object = UnityEngine.Object;

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
    public static partial class FoundationTestSceneMenu
    {
        /// <summary>统一 Gameplay 地基运行验收场景的固定资产路径。</summary>
        internal const string ScenePath = "Assets/Scenes/FoundationTest.unity";

		/// <summary>StackCraft 开局卡包同态验收场景的固定资产路径。</summary>
		internal const string StackCraftParityScenePath = "Assets/Scenes/FoundationStackCraftParityTest.unity";

		private const string StackCraftParitySceneName = "FoundationStackCraftParityTest";

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
		private const string TestFoodPath = TestContentFolder + "/地基日终食物.asset";
		private const string TestSellableCardPath = TestContentFolder + "/地基日终可售卡.asset";
		private const string TestCurrencyCardPath = TestContentFolder + "/地基日终货币卡.asset";
		private const string TestBuyerCardPath = TestContentFolder + "/地基日终收购点.asset";
		private const string TestEncounterCardPath = TestContentFolder + "/地基日终遭遇卡.asset";
		private const string TestSellActionPath = TestContentFolder + "/地基日终出售行动.asset";
		private const string TestDayCycleScenarioPath = TestContentFolder + "/地基日终测试剧本.asset";
		private const string TestCardPackPath = TestContentFolder + "/地基测试卡包.asset";
		private const string TestBeginningPackPath = TestContentFolder + "/地基开端卡包.asset";
		private const string TestCardPackFirstRewardPath = TestContentFolder + "/地基卡包奖励一.asset";
		private const string TestCardPackSecondRewardPath = TestContentFolder + "/地基卡包奖励二.asset";
		private const string TestBeginningSoilPath = TestContentFolder + "/地基开端土壤.asset";
		private const string TestBeginningTreePath = TestContentFolder + "/地基开端树.asset";
		private const string TestBeginningChickenPath = TestContentFolder + "/地基开端鸡.asset";
		private const string TestBeginningSlimePath = TestContentFolder + "/地基开端史莱姆.asset";
		private const string TestBeginningGoldenKeyPath = TestContentFolder + "/地基开端金钥匙.asset";
		private const string TestBeginningEggPath = TestContentFolder + "/地基开端鸡蛋.asset";
		private const int NeutralCreatureAscPresetId = 1005;
		private const int HostileCreatureAscPresetId = 1006;
		private const string TestRecipeGrowingBerryActionPath = TestContentFolder + "/地基配方种植浆果行动.asset";
		private const string TestRecipeBuildingHouseActionPath = TestContentFolder + "/地基配方建造房屋行动.asset";
		private const string TestRecipeMakingLoveActionPath = TestContentFolder + "/地基配方孕育行动.asset";
		private const string TestRecipeMakingTimberActionPath = TestContentFolder + "/地基配方制作木材行动.asset";
		private const string TestRecipeCraftingStickActionPath = TestContentFolder + "/地基配方制作木棍行动.asset";
		private const string TestRecipeGrowingBerryCardPath = TestContentFolder + "/地基配方卡种植浆果.asset";
		private const string TestRecipeBuildingHouseCardPath = TestContentFolder + "/地基配方卡建造房屋.asset";
		private const string TestRecipeMakingLoveCardPath = TestContentFolder + "/地基配方卡孕育.asset";
		private const string TestRecipeMakingTimberCardPath = TestContentFolder + "/地基配方卡制作木材.asset";
		private const string TestRecipeCraftingStickCardPath = TestContentFolder + "/地基配方卡制作木棍.asset";
		private const string TestOpenCardPackActionPath = TestContentFolder + "/地基打开卡包行动.asset";
		private const string TestPackVendorPath = TestContentFolder + "/地基卡包商贩.asset";
		private const string TestBeginningPackVendorPath = TestContentFolder + "/地基开端卡包商贩.asset";
		private const string TestPurchaseCardPackActionPath = TestContentFolder + "/地基购买卡包行动.asset";
        private const string TestChestPath = TestContentFolder + "/地基测试箱子.asset";
		private const string TestDepositCurrencyIntoChestActionPath = TestContentFolder + "/地基存币行动.asset";
		private const string TestWithdrawCurrencyFromChestActionPath = TestContentFolder + "/地基取币行动.asset";
        private const string TestQuestPath = TestContentFolder + "/地基测试任务.asset";
		private const string TestStackCraftParityQuestPath =
			TestContentFolder + "/地基StackCraft同态打开初始卡包任务.asset";
        private const string TestScenarioPath = TestContentFolder + "/地基测试剧本.asset";
		private const string TestStackCraftParityScenarioPath = TestContentFolder + "/地基StackCraft同态测试剧本.asset";
        private const string TestSceneScenarioPath = TestContentFolder + "/地基场景测试剧本.asset";
		private const string TestRegionPath = TestContentFolder + "/地基测试地区.asset";
		private const string TestStackCraftParityRegionPath = TestContentFolder + "/地基StackCraft同态测试地区.asset";
		private const string TestBattleRegionPath = TestContentFolder + "/地基战斗测试地区.asset";
		private const string TestSceneRegionPath = TestContentFolder + "/地基场景测试地区.asset";
		private const string TestSecondSceneRegionPath = TestContentFolder + "/地基第二场景测试地区.asset";
        private const string TabletopTestFolder = TestContentFolder + "/牌桌";
		private const string GameplayPrefabFolder = "Assets/Art/Prefabs";
		private const string TabletopPrefabFolder = GameplayPrefabFolder + "/牌桌";
		private const string GameplayUiPrefabFolder = GameplayPrefabFolder + "/UI";
        private const string TabletopCardViewPrefabPath = TabletopPrefabFolder + "/卡牌视图.prefab";
		private const string TabletopActionProgressViewPrefabPath = TabletopPrefabFolder + "/行动进度.prefab";
		private const string TabletopBattleAreaViewPrefabPath = TabletopPrefabFolder + "/战斗区域.prefab";
		private const string TabletopProjectileViewPrefabPath = TabletopPrefabFolder + "/投射物.prefab";
		private const string TabletopHitResultViewPrefabPath = TabletopPrefabFolder + "/命中结果.prefab";
		private const string TabletopActionChoicePanelPrefabPath = GameplayUiPrefabFolder + "/TabletopActionChoicePanel.prefab";
		private const string TabletopActionPlanPanelPrefabPath = GameplayUiPrefabFolder + "/TabletopActionPlanPanel.prefab";
        private const string ScenarioTurnPanelPrefabPath = GameplayUiPrefabFolder + "/ScenarioTurnPanel.prefab";
        private const string ScenarioJournalPanelPrefabPath = GameplayUiPrefabFolder + "/ScenarioJournalPanel.prefab";
        private const string ScenarioPausePanelPrefabPath = GameplayUiPrefabFolder + "/ScenarioPausePanel.prefab";
        private const string ScenarioSavePanelPrefabPath = GameplayUiPrefabFolder + "/ScenarioSavePanel.prefab";
        private const string ConfirmationDialogPanelPrefabPath = GameplayUiPrefabFolder + "/ConfirmationDialogPanel.prefab";
        private const string FoundationGameUiPrefabPath = GameplayUiPrefabFolder + "/FoundationGameUI.prefab";
        private const string TabletopCardInfoPanelPrefabPath = GameplayUiPrefabFolder + "/TabletopCardInfoPanel.prefab";
        private const string TabletopViewSettingsPath = TabletopTestFolder + "/牌桌测试视图设置.asset";
		private const string ScenarioScreenEffectProfilePath = TabletopTestFolder + "/剧本屏幕效果配置.asset";
        private const string GameplaySpriteFolder = "Assets/Art/Sprites";
		private const string GameplayMaterialFolder = "Assets/Art/Materials";
		private const string GameplayShaderFolder = "Assets/Art/Shaders";
		private const string GameplayModelFolder = "Assets/Art/Models";
        private const string GameplayAudioClipFolder = "Assets/Audio/SFX";
        private const string GameplayCardArtFolder = GameplaySpriteFolder + "/CardArts";
		private const string StackCraftSpriteFolder = GameplaySpriteFolder + "/StackCraft";
		private const string TabletopCardArtPath = GameplaySpriteFolder + "/卡牌占位图.png";
		private const string StackCraftSquareSpritePath = StackCraftSpriteFolder + "/Square.png";
		private const string StackCraftStatsCardSpritePath = StackCraftSpriteFolder + "/Stats_Card.png";
		private const string StackCraftStatsCurrencySpritePath = StackCraftSpriteFolder + "/Stats_Currency.png";
		private const string StackCraftStatsNutritionSpritePath = StackCraftSpriteFolder + "/Stats_Nutrition.png";
		private const string StackCraftTimePacePausedSpritePath = StackCraftSpriteFolder + "/TimePace_0.png";
		private const string StackCraftTimePaceNormalSpritePath = StackCraftSpriteFolder + "/TimePace_1.png";
		private const string StackCraftTimePaceFastSpritePath = StackCraftSpriteFolder + "/TimePace_2.png";
		private const int UnityUiLayer = 5;
        private const string VillagerCardArtPath = GameplayCardArtFolder + "/村民.png";
        private const string WoodCardArtPath = GameplayCardArtFolder + "/木头.png";
        private const string BerryCardArtPath = GameplayCardArtFolder + "/浆果.png";
        private const string BerryBushCardArtPath = GameplayCardArtFolder + "/浆果丛.png";
        private const string RockCardArtPath = GameplayCardArtFolder + "/岩石.png";
        private const string StoneCardArtPath = GameplayCardArtFolder + "/石头.png";
        private const string CoinCardArtPath = GameplayCardArtFolder + "/金币.png";
		private const string StackCraftPlaceholderCardArtPath = StackCraftSpriteFolder + "/CardArts/Placeholder.png";
        private const string GoblinCardArtPath = GameplayCardArtFolder + "/哥布林.png";
        private const string TreasureChestCardArtPath = GameplayCardArtFolder + "/宝箱.png";
        private const string WoodenChestCardArtPath = GameplayCardArtFolder + "/木箱.png";
        private const string UnusedStarterPackCardArtPath = GameplayCardArtFolder + "/初始卡包.png";
		private const string UnusedBeginningPackCardArtPath = GameplayCardArtFolder + "/开端卡包.png";
        private const string StarterPackCardArtPath = StackCraftSpriteFolder + "/PackArts/Starter.png";
		private const string BeginningPackCardArtPath = StackCraftSpriteFolder + "/PackArts/Beginning.png";
		private const string SoilCardArtPath = GameplayCardArtFolder + "/土壤.png";
		private const string TreeCardArtPath = GameplayCardArtFolder + "/树.png";
		private const string ChickenCardArtPath = GameplayCardArtFolder + "/鸡.png";
		private const string SlimeCardArtPath = GameplayCardArtFolder + "/史莱姆.png";
		private const string GoldenKeyCardArtPath = GameplayCardArtFolder + "/金钥匙.png";
		private const string EggCardArtPath = GameplayCardArtFolder + "/鸡蛋.png";
		private const string RecipeCardArtPath = GameplayCardArtFolder + "/配方卡.png";
		private const string CardSurfaceShaderPath = GameplayShaderFolder + "/卡牌表面.shadergraph";
		private const string CardOutlineShaderPath = GameplayShaderFolder + "/卡牌轮廓.shadergraph";
		private const string CardMeshPath = GameplayModelFolder + "/卡牌.fbx";
		private const string PackMeshPath = GameplayModelFolder + "/卡包.fbx";
		private const string EquipmentPanelMeshPath = GameplayModelFolder + "/装备面板.fbx";
		private const string BoardMeshPath = GameplayModelFolder + "/牌桌.fbx";
		private const string BoardBodyMaterialPath = GameplayMaterialFolder + "/牌桌主体_01.mat";
		private const string BoardHeaderMaterialPath = GameplayMaterialFolder + "/牌桌页眉_01.mat";
		private const string IslandBoardBodyMaterialPath = GameplayMaterialFolder + "/牌桌主体_02.mat";
		private const string IslandBoardHeaderMaterialPath = GameplayMaterialFolder + "/牌桌页眉_02.mat";
		private const string GrassBackgroundMaterialPath = GameplayMaterialFolder + "/草地背景.mat";
		private const string WaterBackgroundMaterialPath = GameplayMaterialFolder + "/水面背景.mat";
		private const string EquipmentPanelMaterialPath = GameplayMaterialFolder + "/装备面板.mat";
		private const string CardBuyerSurfacePath = GameplayMaterialFolder + "/交易区.mat";
		private const string CardBuyerCurrencyIconMaterialPath = GameplayMaterialFolder + "/货币图标.mat";
		private static readonly Vector2 StackCraftTradeZoneSpawnOffset = new Vector2(0f, -1.4f);
		private const int StackCraftConvertedTextStyleHashCode = -1183493901;
		private const string CharacterCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_角色.mat";
		private const string MobCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_生物.mat";
		private const string AggressiveMobCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_主动敌人.mat";
		private const string ConsumableCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_消耗品.mat";
		private const string CurrencyCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_货币.mat";
		private const string EquipmentCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_装备.mat";
		private const string MaterialCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_材料.mat";
		private const string RecipeCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_配方.mat";
		private const string ResourceCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_资源.mat";
		private const string StructureCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_建筑.mat";
		private const string ValuableCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_贵重物.mat";
		private const string PackCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_卡包.mat";
		private const string AreaCardSurfacePath = GameplayMaterialFolder + "/卡牌表面_地区.mat";
        private const string HitNormalSpritePath = GameplaySpriteFolder + "/普通命中图标.png";
        private const string HitMissSpritePath = GameplaySpriteFolder + "/未命中图标.png";
        private const string HitCriticalSpritePath = GameplaySpriteFolder + "/暴击图标.png";
        private const string AdvantageSpritePath = GameplaySpriteFolder + "/优势图标.png";
        private const string DisadvantageSpritePath = GameplaySpriteFolder + "/劣势图标.png";
		private const string ArrowProjectileSpritePath = GameplaySpriteFolder + "/箭矢投射物.png";
		private const string MagicProjectileSpritePath = GameplaySpriteFolder + "/魔法投射物.png";
        private const string TabletopAudioFolder = TabletopTestFolder + "/音效";
		private const string CardPickAudioPath = TabletopAudioFolder + "/拿起卡牌音效.asset";
		private const string CardDropAudioPath = TabletopAudioFolder + "/放下卡牌音效.asset";
		private const string CardSwipeAudioPath = TabletopAudioFolder + "/卡牌滑动音效.asset";
		private const string EatAudioPath = TabletopAudioFolder + "/进食音效.asset";
		private const string PopAudioPath = TabletopAudioFolder + "/生成完成音效.asset";
		private const string CardSmokeAudioPath = TabletopAudioFolder + "/卡牌烟雾反馈音效.asset";
		private const string CoinAudioPath = TabletopAudioFolder + "/单枚货币音效.asset";
		private const string CoinsAudioPath = TabletopAudioFolder + "/多枚货币音效.asset";
		private const string CashRegisterAudioPath = TabletopAudioFolder + "/购买成交音效.asset";
		private const string UiSubmitAudioPath = TabletopAudioFolder + "/界面点击音效.asset";
        private const string MeleeAttackAudioPath = TabletopAudioFolder + "/近战起手音效.asset";
        private const string RangedAttackAudioPath = TabletopAudioFolder + "/远程起手音效.asset";
        private const string MagicAttackAudioPath = TabletopAudioFolder + "/魔法起手音效.asset";
        private const string MeleeHitAudioPath = TabletopAudioFolder + "/近战命中音效.asset";
        private const string RangedHitAudioPath = TabletopAudioFolder + "/远程命中音效.asset";
        private const string MagicHitAudioPath = TabletopAudioFolder + "/魔法命中音效.asset";
        private const string MissAudioPath = TabletopAudioFolder + "/未命中音效.asset";
        private const string CriticalAudioPath = TabletopAudioFolder + "/暴击音效.asset";
        private const string TabletopCardViewAddress = "卡牌视图";
		private const string TabletopActionProgressViewAddress = "行动进度";
		private const string TabletopBattleAreaViewAddress = "战斗区域";
		private const string TabletopProjectileViewAddress = "投射物";
		private const string TabletopHitResultViewAddress = "命中结果";
		private const string TabletopCardArtAddress = "卡牌占位图";
        private const string VillagerCardArtAddress = "村民";
        private const string WoodCardArtAddress = "木头";
        private const string BerryCardArtAddress = "浆果";
        private const string BerryBushCardArtAddress = "浆果丛";
        private const string RockCardArtAddress = "岩石";
        private const string StoneCardArtAddress = "石头";
        private const string CoinCardArtAddress = "金币";
		private const string StackCraftPlaceholderCardArtAddress = "CardArts_Placeholder";
        private const string GoblinCardArtAddress = "哥布林";
        private const string TreasureChestCardArtAddress = "宝箱";
        private const string WoodenChestCardArtAddress = "木箱";
        private const string StarterPackCardArtAddress = "PackArts_Starter";
		private const string BeginningPackCardArtAddress = "PackArts_Beginning";
		private const string SoilCardArtAddress = "土壤";
		private const string TreeCardArtAddress = "树";
		private const string ChickenCardArtAddress = "鸡";
		private const string SlimeCardArtAddress = "史莱姆";
		private const string GoldenKeyCardArtAddress = "金钥匙";
		private const string EggCardArtAddress = "鸡蛋";
		private const string RecipeCardArtAddress = "配方卡";
		private const string CharacterCardSurfaceAddress = "卡牌表面_角色";
		private const string MobCardSurfaceAddress = "卡牌表面_生物";
		private const string AggressiveMobCardSurfaceAddress = "卡牌表面_主动敌人";
		private const string ConsumableCardSurfaceAddress = "卡牌表面_消耗品";
		private const string CurrencyCardSurfaceAddress = "卡牌表面_货币";
		private const string EquipmentCardSurfaceAddress = "卡牌表面_装备";
		private const string MaterialCardSurfaceAddress = "卡牌表面_材料";
		private const string RecipeCardSurfaceAddress = "卡牌表面_配方";
		private const string ResourceCardSurfaceAddress = "卡牌表面_资源";
		private const string StructureCardSurfaceAddress = "卡牌表面_建筑";
		private const string ValuableCardSurfaceAddress = "卡牌表面_贵重物";
		private const string PackCardSurfaceAddress = "卡牌表面_卡包";
		private const string AreaCardSurfaceAddress = "卡牌表面_地区";
		private const string CardBuyerSurfaceAddress = "交易区";
		private const string CardPickClipPath = GameplayAudioClipFolder + "/拿起卡牌.wav";
		private const string CardDropClipPath = GameplayAudioClipFolder + "/放下卡牌.wav";
		private const string CardSwipeClipPath = GameplayAudioClipFolder + "/卡牌滑动.wav";
		private const string EatClipPath = GameplayAudioClipFolder + "/进食.wav";
		private const string PopClipPath = GameplayAudioClipFolder + "/生成完成.wav";
		private const string CoinClipPath = GameplayAudioClipFolder + "/单枚货币.wav";
		private const string CoinsClipPath = GameplayAudioClipFolder + "/多枚货币.wav";
		private const string CashRegisterClipPath = GameplayAudioClipFolder + "/购买成交.wav";
		private const string AttackMeleeClipPath = GameplayAudioClipFolder + "/近战起手.wav";
		private const string AttackRangedClipPath = GameplayAudioClipFolder + "/远程起手.wav";
		private const string AttackMagicClipPath = GameplayAudioClipFolder + "/魔法起手.wav";
		private const string HitMeleeClipPath = GameplayAudioClipFolder + "/近战命中.wav";
		private const string HitRangedClipPath = GameplayAudioClipFolder + "/远程命中.wav";
		private const string HitMagicClipPath = GameplayAudioClipFolder + "/魔法命中.wav";
		private const string MissClipPath = GameplayAudioClipFolder + "/未命中.wav";
		private const string CriticalClipPath = GameplayAudioClipFolder + "/暴击.wav";
		private const string GameplayCardSmokeEffectPrefabPath = GameplayPrefabFolder + "/卡牌烟雾粒子.prefab";
		private const string GameplayCardSmokeEffectAddress = "卡牌烟雾粒子";
		private const string GameplayCardSmokeMaterialPath = GameplayMaterialFolder + "/卡牌烟雾材质.mat";
		private const string GameplayCardSmokeClipPath = GameplayAudioClipFolder + "/卡牌烟雾反馈.wav";
		private const string UiSubmitClipPath = GameplayAudioClipFolder + "/界面点击.wav";
        private const string TestPanelFontPath =
            TabletopTestFolder + "/地基测试中文字体.asset";
		private const string StackCraftSurfaceFontPath =
			"Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
		private static readonly Vector2 StackCraftPackInstanceViewSize = new(0.9f, 1.3000002f);
		private static readonly Vector2 StackCraftTradeZoneViewSize = new(0.90000004f, 1.1250004f);
		private static readonly Rect StackCraftBoardPlacementBounds = new Rect(-6f, -4f, 12f, 8f);
		private static readonly Rect StackCraftBoardHeaderRestrictedArea = new Rect(-6f, 2.5f, 12f, 1.5f);
		private static readonly Vector2 StackCraftBoardCardSize = new Vector2(0.8f, 1f);
		private static readonly Vector2 StackCraftBoardCardMargin = new Vector2(0.1f, 0.1f);
		private static readonly Vector2 StackCraftBoardStackStep = new Vector2(0f, -0.18f);
		private const int StackCraftBoardOverlapResolveMaxIterations = 8;
		private const float StackCraftBoardSpawnAttachRadius = 1f;
		private static readonly Vector2 StackCraftBoardCardLimitBonusExpansionPerPoint = new Vector2(0.06f, 0.04f);
		private static readonly Vector3 StackCraftBoardLocalBoundsSize = new Vector3(48f, 0f, 32f);
		private const float StackCraftBoardSurfaceHeight = -0.05f;
		private const uint StackCraftParityAuthoritativeRandomSeed = 20260821u;
		private const int StackCraftBaseCardLimit = 24;
		private const int StackCraftHungerPerCharacter = 2;
		private const float StackCraftReferenceDayDurationSeconds = 120f;
		private const float StackCraftAutomaticMovementIntervalSeconds = 5f;
		private const float StackCraftAutomaticMovementRadius = 1f;
		private const int StackCraftAutomaticMovementMaxAttempts = 5;
		private const string RuntimeRootPrefabPath = TabletopTestFolder + "/FoundationTestRuntimeRoot.prefab";
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
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            RebuildMapTestScene(MapScenePath, "FoundationMapMarker");
            RebuildMapTestScene(SecondMapScenePath, "FoundationSecondMapMarker");
            GameConfig config = EnsureConfigAsset();
            EnsureTestCardAsset();
            EnsureTestProductAsset();
            EnsureTestActionAsset();
			EnsureTestActionPlanAsset();
			EnsureDayCycleTestAssets();
			EnsureCardPackTestAssets();
			EnsurePackVendorTestAssets();
			EnsureChestTestAssets();
            EnsureTestQuestAsset();
            EnsureTestScenarioAssets();
            TabletopViewSettings viewSettings = EnsureTabletopTestAssets();
			GameObject runtimeRootPrefab = EnsureRuntimeRootPrefab(config);
			viewSettings = LoadPersistentTabletopViewSettings();

			RebuildTabletopScene(
				ScenePath,
				"FoundationTest",
				FoundationTestSceneHarness.TestScenarioContentId,
				FoundationTestInitialLayout.StackDragAndActionTest,
				0u,
				runtimeRootPrefab,
				viewSettings);
			RebuildTabletopScene(
				StackCraftParityScenePath,
				StackCraftParitySceneName,
				FoundationTestSceneHarness.TestStackCraftParityScenarioContentId,
				FoundationTestInitialLayout.StackCraftStarterPack,
				StackCraftParityAuthoritativeRandomSeed,
				runtimeRootPrefab,
				viewSettings);
            RemoveTestScenesFromBuildSettings();
            EnsureTestSceneCollector();
            AssetDatabase.SaveAssets();
            // batchmode 会在本方法返回后立即退出；先同步收完本轮导入，避免资源工作线程仍在通信时被强制清理。
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            RefreshEditorSimulateManifest();
			NormalizeGeneratedYamlWhitespace();

            if (!Application.isBatchMode)
            {
				Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(StackCraftParityScenePath);
            }

            Debug.Log(
                $"Gameplay 地基测试场景已重建：{ScenePath}，StackCraft 同态场景已重建：{StackCraftParityScenePath}。独立运行时使用 {RuntimeRootPrefabPath}。");
        }

		private static void RebuildTabletopScene(
			string scenePath,
			string rootName,
			string scenarioContentId,
			FoundationTestInitialLayout initialLayout,
			uint authoritativeRandomSeedOverride,
			GameObject runtimeRootPrefab,
			TabletopViewSettings viewSettings)
		{
			runtimeRootPrefab = LoadPersistentRuntimeRootPrefab();
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			EditorSceneManager.MarkSceneDirty(scene);
			ApplyStackCraftMainRenderSettings();

			GameObject testRoot = new(rootName);
			FoundationTestRuntimeEntry runtimeEntry = testRoot.AddComponent<FoundationTestRuntimeEntry>();
			SerializedObject serializedEntry = new(runtimeEntry);
			serializedEntry.FindProperty("m_runtimeRootPrefab").objectReferenceValue = runtimeRootPrefab;
			serializedEntry.ApplyModifiedPropertiesWithoutUndo();
			CreateStackCraftDirectionalLight();
			CreateStackCraftBackgroundSurface(testRoot.transform);
			CreateStackCraftBoardSurface(testRoot.transform);
			TabletopView tabletopView = CreateTabletopTestRoot(
				testRoot.transform,
				viewSettings,
				scenarioContentId,
				initialLayout,
				authoritativeRandomSeedOverride);
			GameObject cameraControllerObject = new("CameraController");
			cameraControllerObject.transform.SetParent(testRoot.transform);
			cameraControllerObject.transform.position = new Vector3(0f, 10f, -1f);
			cameraControllerObject.transform.rotation = Quaternion.Euler(85f, 0f, 0f);

			Camera camera = CreateMainCamera(cameraControllerObject.transform);
			GameObject cameraObject = camera.gameObject;
			ConfigureStackCraftMainCamera(camera);
			cameraObject.AddComponent<PhysicsRaycaster>();
			ConfigurePostProcessingCamera(cameraObject);
			ConfigureTemplateCameraShake(cameraObject.AddComponent<CameraShake>());
			ConfigureTabletopCameraController(
				cameraControllerObject.AddComponent<TabletopCameraController>(),
				tabletopView,
				camera);

			if (!EditorSceneManager.SaveScene(scene, scenePath))
			{
				throw new MissingReferenceException($"无法保存测试场景：{scenePath}");
			}

			VerifySavedSceneConfig(
				runtimeRootPrefab,
				scenePath,
				scenarioContentId,
				initialLayout,
				authoritativeRandomSeedOverride);
		}

		/// <summary>生成测试场景唯一主相机，并确保音频监听随相机一起保存。</summary>
		private static Camera CreateMainCamera(Transform parent)
		{
			GameObject cameraObject = new("Main Camera");
			cameraObject.transform.SetParent(parent, false);
			cameraObject.tag = "MainCamera";
			Camera camera = cameraObject.AddComponent<Camera>();
			cameraObject.AddComponent<AudioListener>();
			return camera;
		}

		/// <summary>生成无父级的测试入口主相机，并移动到目标场景。</summary>
		private static Camera CreateMainCamera(Scene scene)
		{
			GameObject cameraObject = new("Main Camera");
			SceneManager.MoveGameObjectToScene(cameraObject, scene);
			cameraObject.tag = "MainCamera";
			Camera camera = cameraObject.AddComponent<Camera>();
			cameraObject.AddComponent<AudioListener>();
			return camera;
		}

		/// <summary>按 StackCraft CameraController.prefab 的主相机参数配置测试相机。</summary>
		private static void ConfigureStackCraftMainCamera(Camera camera)
		{
			camera.orthographic = false;
			camera.fieldOfView = 30f;
			camera.nearClipPlane = 2f;
			camera.farClipPlane = 40f;
			camera.depth = -1f;
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.1882353f, 0.21568628f, 0.22745098f, 1f);
		}

		/// <summary>按 StackCraft Main.unity 的场景环境光配置写入测试场景。</summary>
		private static void ApplyStackCraftMainRenderSettings()
		{
			RenderSettings.fog = false;
			RenderSettings.fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
			RenderSettings.fogMode = FogMode.ExponentialSquared;
			RenderSettings.fogDensity = 0.01f;
			RenderSettings.fogStartDistance = 0f;
			RenderSettings.fogEndDistance = 300f;
			RenderSettings.ambientSkyColor = new Color(0.60480005f, 0.63552f, 0.72f, 1f);
			RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
			RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
			RenderSettings.ambientIntensity = 1f;
			RenderSettings.ambientMode = AmbientMode.Flat;
			RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.478f, 0.627f, 1f);
			RenderSettings.skybox = null;
			RenderSettings.haloStrength = 0.5f;
			RenderSettings.flareStrength = 1f;
			RenderSettings.flareFadeSpeed = 3f;
			RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
			RenderSettings.defaultReflectionResolution = 128;
			RenderSettings.reflectionBounces = 1;
			RenderSettings.reflectionIntensity = 1f;
			RenderSettings.customReflection = null;
			RenderSettings.sun = null;
		}

		/// <summary>按 StackCraft Main.unity 的 DirectionalLight 参数生成场景光源。</summary>
		private static Light CreateStackCraftDirectionalLight()
		{
			GameObject lightObject = new("DirectionalLight");
			lightObject.transform.position = new Vector3(0f, 3f, 0f);
			lightObject.transform.rotation =
				new Quaternion(0.1464466f, 0.8535535f, -0.35355338f, 0.35355338f);
			lightObject.transform.localScale = Vector3.one;

			Light light = lightObject.AddComponent<Light>();
			light.type = LightType.Directional;
			light.color = new Color(1f, 0.95686275f, 0.8392157f, 1f);
			light.intensity = 1f;
			light.range = 10f;
			light.shadows = LightShadows.Hard;
			light.shadowStrength = 0.7f;
			light.shadowBias = 0.05f;
			light.shadowNormalBias = 0.4f;
			light.shadowNearPlane = 0.2f;
			light.renderMode = LightRenderMode.Auto;
			light.cullingMask = ~0;
			light.bounceIntensity = 0f;
			light.colorTemperature = 6570f;
			light.useColorTemperature = false;

			UniversalAdditionalLightData additionalLightData =
				lightObject.AddComponent<UniversalAdditionalLightData>();
			additionalLightData.usePipelineSettings = true;
			return light;
		}

        private static void VerifySavedSceneConfig(
			GameObject expectedRuntimeRootPrefab,
			string scenePath,
			string expectedScenarioId,
			FoundationTestInitialLayout expectedInitialLayout,
			uint expectedAuthoritativeRandomSeedOverride)
        {
            Scene savedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (savedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameManager>(true))
                .Any())
            {
                throw new System.InvalidOperationException(
                    $"剧本内容场景不得重复保存进程级 {nameof(GameManager)}：{scenePath}");
            }

            FoundationTestRuntimeEntry entry = savedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FoundationTestRuntimeEntry>(true))
                .SingleOrDefault();
            SerializedProperty runtimeRootProperty = entry == null
                ? null
                : new SerializedObject(entry).FindProperty("m_runtimeRootPrefab");
			GameObject savedRuntimeRootPrefab = runtimeRootProperty?.objectReferenceValue as GameObject;
            if (savedRuntimeRootPrefab == null ||
				savedRuntimeRootPrefab != expectedRuntimeRootPrefab ||
				!string.Equals(
					AssetDatabase.GetAssetPath(savedRuntimeRootPrefab),
					RuntimeRootPrefabPath,
					StringComparison.Ordinal))
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有引用唯一测试进程根预制体：{scenePath}");
            }

            FoundationTestSceneHarness savedHarness = savedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FoundationTestSceneHarness>(true))
                .SingleOrDefault();
            if (savedHarness == null)
            {
                throw new MissingReferenceException(
                    $"保存后的测试场景没有唯一的 {nameof(FoundationTestSceneHarness)}：{scenePath}");
            }

			SerializedObject serializedHarness = new(savedHarness);
			string savedScenarioId = RequireProperty(serializedHarness, "m_scenarioId")
				.FindPropertyRelative("m_value").stringValue;
			if (!string.Equals(savedScenarioId, expectedScenarioId, StringComparison.Ordinal))
			{
				throw new MissingReferenceException(
					$"保存后的测试场景剧本 ID 不正确：{scenePath}，当前 {savedScenarioId}，期望 {expectedScenarioId}。");
			}

			FoundationTestInitialLayout savedInitialLayout =
				(FoundationTestInitialLayout)RequireProperty(serializedHarness, "m_initialLayout").enumValueIndex;
			if (savedInitialLayout != expectedInitialLayout)
			{
				throw new MissingReferenceException(
					$"保存后的测试场景初始布局不正确：{scenePath}，当前 {savedInitialLayout}，期望 {expectedInitialLayout}。");
			}

			uint savedAuthoritativeRandomSeedOverride =
				(uint)RequireProperty(serializedHarness, "m_authoritativeRandomSeedOverride").longValue;
			if (savedAuthoritativeRandomSeedOverride != expectedAuthoritativeRandomSeedOverride)
			{
				throw new MissingReferenceException(
					$"保存后的测试场景随机根种子不正确：{scenePath}，当前 {savedAuthoritativeRandomSeedOverride}，期望 {expectedAuthoritativeRandomSeedOverride}。");
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
                    $"保存后的 {nameof(TabletopView)} 没有牌桌视图设置引用：{scenePath}");
            }

			TabletopCameraController savedCameraController = savedScene.GetRootGameObjects()
				.SelectMany(root => root.GetComponentsInChildren<TabletopCameraController>(true))
				.SingleOrDefault();
			SerializedProperty savedCameraTabletopView = savedCameraController == null
				? null
				: new SerializedObject(savedCameraController).FindProperty("m_tabletopView");
			if (savedCameraTabletopView?.objectReferenceValue != savedTabletopView)
			{
				throw new MissingReferenceException(
					$"保存后的主相机没有引用唯一 {nameof(TabletopView)}：{scenePath}");
			}

        }

		private static GameObject EnsureRuntimeRootPrefab(GameConfig config)
		{
			GameObject root = new("FoundationTestRuntimeRoot");
			try
			{
				GameManager gameManager = root.AddComponent<GameManager>();
				root.AddComponent<ScenarioDirector>();
				root.AddComponent<SceneSystem>();
				root.AddComponent<DisplaySettingsSystem>();
				root.AddComponent<TransitionSystem>();
				root.AddComponent<GameStateSystem>();
				CreateScenarioScreenEffect(root.transform);
				UISystem uiSystem = root.AddComponent<UISystem>();
				SerializedObject serializedUiSystem = new(uiSystem);
				RequireProperty(serializedUiSystem, "m_uiPrefab").objectReferenceValue = EnsureGameUiPrefab();
				serializedUiSystem.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(uiSystem);
				AudioSystem audioSystem = root.AddComponent<AudioSystem>();
				ConfigureTestAudioSystem(root.transform, audioSystem);
				CreateInputSystem(root);
				AssignGameConfig(gameManager, config);

				if (PrefabUtility.SaveAsPrefabAsset(root, RuntimeRootPrefabPath) == null)
				{
					throw new MissingReferenceException($"无法保存测试进程根预制体：{RuntimeRootPrefabPath}");
				}

				AssetDatabase.SaveAssets();
				return LoadPersistentRuntimeRootPrefab();
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(root);
			}
		}

		private static GameObject LoadPersistentRuntimeRootPrefab()
		{
			AssetDatabase.ImportAsset(RuntimeRootPrefabPath, ImportAssetOptions.ForceSynchronousImport);
			GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeRootPrefabPath);
			GameManager gameManager = saved == null ? null : saved.GetComponent<GameManager>();
			if (gameManager == null)
			{
				throw new MissingReferenceException($"无法重新载入测试进程根预制体：{RuntimeRootPrefabPath}");
			}

			if (!EditorUtility.IsPersistent(saved) ||
				!string.Equals(
					AssetDatabase.GetAssetPath(saved),
					RuntimeRootPrefabPath,
					StringComparison.Ordinal))
			{
				throw new MissingReferenceException(
					$"测试进程根不是可保存的 Prefab 作者资源：{RuntimeRootPrefabPath}");
			}

			return saved;
		}

		private static void ConfigureTestAudioSystem(Transform runtimeRoot, AudioSystem audioSystem)
		{
			AudioChannel backgroundMusicChannel = CreateTestAudioChannel(
				runtimeRoot,
				nameof(EAudioChannel.BackgroundMusic),
				0.5f,
				1);
			AudioChannel gameplayChannel = CreateTestAudioChannel(
				runtimeRoot,
				nameof(EAudioChannel.GameplaySoundFX),
				0.65f,
				8);
			AudioChannel interfaceChannel = CreateTestAudioChannel(
				runtimeRoot,
				nameof(EAudioChannel.InterfaceSoundFX),
				0.75f,
				8);

			FieldInfo audioChannelsField = typeof(AudioSystem).GetField(
				"m_audioChannels",
				BindingFlags.Instance | BindingFlags.NonPublic) ??
				throw new MissingFieldException(typeof(AudioSystem).FullName, "m_audioChannels");
			System.Collections.IDictionary channels =
				(System.Collections.IDictionary)System.Activator.CreateInstance(audioChannelsField.FieldType);
			channels.Add(EAudioChannel.BackgroundMusic, backgroundMusicChannel);
			channels.Add(EAudioChannel.GameplaySoundFX, gameplayChannel);
			channels.Add(EAudioChannel.InterfaceSoundFX, interfaceChannel);
			audioChannelsField.SetValue(audioSystem, channels);
			EditorUtility.SetDirty(audioSystem);
		}

		private static AudioChannel CreateTestAudioChannel(
			Transform runtimeRoot,
			string channelName,
			float volumeScale,
			int prewarmCount)
		{
			GameObject channelObject = new(channelName);
			channelObject.transform.SetParent(runtimeRoot, false);
			AudioSource audioSource = channelObject.AddComponent<AudioSource>();
			audioSource.playOnAwake = false;
			audioSource.spatialBlend = 0f;
			AudioChannel channel = channelObject.AddComponent<AudioChannel>();

			SerializedObject serializedChannel = new(channel);
			RequireProperty(serializedChannel, "m_audioChannelMode").enumValueIndex = (int)EAudioChannelMode.Multiple;
			RequireProperty(serializedChannel, "m_audioSource").objectReferenceValue = audioSource;
			RequireProperty(serializedChannel, "m_volumeScale").floatValue = volumeScale;
			RequireProperty(serializedChannel, "m_multipleModePrewarmCount").intValue = prewarmCount;
			RequireProperty(serializedChannel, "m_multipleModeMaxPlayers").intValue = -1;
			serializedChannel.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(channel);

			return channel;
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

                ConfigureFoundationConfig(config);
                return config;
            }

            config = ScriptableObject.CreateInstance<GameConfig>();
            config.name = ConfigAssetName;
            AssetDatabase.CreateAsset(config, ConfigPath);
            ConfigureFoundationConfig(config);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) ??
                throw new MissingReferenceException($"无法从 AssetDatabase 重新载入测试配置：{ConfigPath}");
        }

        private static void ConfigureFoundationConfig(GameConfig config)
        {
			AudioClipResolver uiSubmitAudio = EnsureAudioResolver(
				UiSubmitAudioPath,
				"界面点击音效",
				UiSubmitClipPath,
				EAudioChannel.InterfaceSoundFX);

            SerializedObject serializedConfig = new(config);
            SerializedProperty cameraShakeSources =
                serializedConfig.FindProperty("m_cameraShakeSources");
            if (cameraShakeSources == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(GameConfig)} 缺少镜头震动来源配置字段。");
            }

            cameraShakeSources.intValue = (int)ECameraShakeSources.AbilitySystemDamageResolved;
			RequireProperty(serializedConfig, "m_submitSound").objectReferenceValue = uiSubmitAudio;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureTemplateCameraShake(CameraShake cameraShake)
        {
            SerializedObject serializedShake = new(cameraShake);
            SerializedProperty amplitude = serializedShake.FindProperty("m_amplitude");
            SerializedProperty duration = serializedShake.FindProperty("m_duration");
            if (amplitude == null || duration == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(CameraShake)} 的镜头震动参数字段已变更，测试场景生成器需要同步更新。");
            }

            // 参考模板命中反馈使用 Shake(duration: 0.3f, strength: 0.1f)。
            amplitude.floatValue = 0.1f;
            duration.floatValue = 0.3f;
            serializedShake.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cameraShake);
        }

		private static void ConfigureTabletopCameraController(
			TabletopCameraController cameraController,
			TabletopView tabletopView,
			Camera camera)
		{
			SerializedObject serializedController = new(cameraController);
			SerializedProperty tabletopViewProperty =
				serializedController.FindProperty("m_tabletopView");
			SerializedProperty cameraTransformProperty =
				serializedController.FindProperty("m_cameraTransform");
			if (tabletopViewProperty == null ||
				cameraTransformProperty == null)
			{
				throw new MissingReferenceException(
					$"{nameof(TabletopCameraController)} 缺少牌桌视图或唯一相机 Transform 字段。");
			}

			tabletopViewProperty.objectReferenceValue = tabletopView;
			cameraTransformProperty.objectReferenceValue = camera.transform;
			serializedController.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(cameraController);
		}

        private static void RebuildMapTestScene(string scenePath, string markerName)
        {
            Scene mapScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			ApplyStackCraftMainRenderSettings();
			CreateStackCraftDirectionalLight();
            GameObject marker = new(markerName);
            marker.AddComponent<MapInfo>();

			bool isIslandScene = string.Equals(
				scenePath,
				SecondMapScenePath,
				System.StringComparison.Ordinal);
			if (isIslandScene)
			{
				CreateStackCraftBackgroundSurface(
					null,
					WaterBackgroundMaterialPath,
					"StackCraft Island 水面背景材质自有副本");
				CreateStackCraftBoardSurface(
					null,
					IslandBoardBodyMaterialPath,
					IslandBoardHeaderMaterialPath,
					"StackCraft Board02 主体材质自有副本",
					"StackCraft Board02 页眉材质自有副本");
			}
			else
			{
				CreateStackCraftBackgroundSurface(
					null,
					GrassBackgroundMaterialPath,
					"StackCraft Main 草地背景材质自有副本");
				CreateStackCraftBoardSurface(
					null,
					BoardBodyMaterialPath,
					BoardHeaderMaterialPath,
					"StackCraft Board01 主体材质自有副本",
					"StackCraft Board01 页眉材质自有副本");
			}

			GameObject cameraControllerObject = new("CameraController");
			SceneManager.MoveGameObjectToScene(cameraControllerObject, mapScene);
			cameraControllerObject.transform.position = new Vector3(0f, 10f, -1f);
			cameraControllerObject.transform.rotation = Quaternion.Euler(85f, 0f, 0f);
			Camera camera = CreateMainCamera(cameraControllerObject.transform);
			ConfigureStackCraftMainCamera(camera);

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
			string[] exactAssetPaths =
			{
				TabletopCardViewPrefabPath,
				TabletopActionProgressViewPrefabPath,
				TabletopBattleAreaViewPrefabPath,
				TabletopProjectileViewPrefabPath,
				TabletopHitResultViewPrefabPath,
				GameplayCardSmokeEffectPrefabPath,
				TabletopActionChoicePanelPrefabPath,
				TabletopActionPlanPanelPrefabPath,
				ScenarioTurnPanelPrefabPath,
				ScenarioJournalPanelPrefabPath,
				ScenarioPausePanelPrefabPath,
				ScenarioSavePanelPrefabPath,
				ScenarioTitlePanelPrefabPath,
				SettingsPanelPrefabPath,
				ConfirmationDialogPanelPrefabPath,
				FoundationGameUiPrefabPath,
				TabletopCardInfoPanelPrefabPath,
				TabletopCardArtPath,
				VillagerCardArtPath,
				WoodCardArtPath,
				BerryCardArtPath,
				BerryBushCardArtPath,
				RockCardArtPath,
				StoneCardArtPath,
				CoinCardArtPath,
				GoblinCardArtPath,
				TreasureChestCardArtPath,
				WoodenChestCardArtPath,
				UnusedStarterPackCardArtPath,
				UnusedBeginningPackCardArtPath,
				SoilCardArtPath,
				TreeCardArtPath,
				ChickenCardArtPath,
				SlimeCardArtPath,
				GoldenKeyCardArtPath,
				EggCardArtPath,
				RecipeCardArtPath,
				CharacterCardSurfacePath,
				MobCardSurfacePath,
				AggressiveMobCardSurfacePath,
				ConsumableCardSurfacePath,
				CurrencyCardSurfacePath,
				EquipmentCardSurfacePath,
				MaterialCardSurfacePath,
				RecipeCardSurfacePath,
				ResourceCardSurfacePath,
				StructureCardSurfacePath,
				ValuableCardSurfacePath,
				PackCardSurfacePath,
				AreaCardSurfacePath,
				EquipmentPanelMaterialPath,
				CardBuyerSurfacePath,
				CardBuyerCurrencyIconMaterialPath,
				ArrowProjectileSpritePath,
				MagicProjectileSpritePath
			};
			HashSet<string> ownedCollectorPaths = new(exactAssetPaths, System.StringComparer.Ordinal)
			{
				"Assets/Scenes",
				StackCraftSpriteFolder
			};
			group.Collectors.RemoveAll(candidate =>
				!ownedCollectorPaths.Contains(candidate.CollectPath));
			for (int i = 0; i < exactAssetPaths.Length; i++)
			{
				EnsureExactTestAssetCollector(group, exactAssetPaths[i]);
			}
			EnsureStackCraftSpriteCollector(group);
            EditorUtility.SetDirty(setting);
        }

        private static void RefreshEditorSimulateManifest()
        {
            PackageBuildResult result = EditorSimulateBuildInvoker.Build(
                DefaultPackageName,
                (int)EBundleType.VirtualAssetBundle);
            if (result == null || string.IsNullOrWhiteSpace(result.PackageRootDirectory))
            {
                throw new MissingReferenceException(
                    $"YooAsset EditorSimulate 清单刷新失败：{DefaultPackageName}");
            }

            Debug.Log($"YooAsset EditorSimulate 清单已刷新：{result.PackageRootDirectory}");
        }

		/// <summary>
		/// 清理测试场景生成器写出的 Unity YAML 空字段尾随空白，避免同一入口每次重建都留下无意义 diff。
		/// </summary>
		private static void NormalizeGeneratedYamlWhitespace()
		{
			IEnumerable<string> candidateFiles = new[]
				{
					CollectorSettingPath,
					ScenePath,
					StackCraftParityScenePath,
					MapScenePath,
					SecondMapScenePath,
					ConfigPath
				}
				.Concat(EnumerateGeneratedYamlFiles(TestContentFolder))
				.Concat(EnumerateGeneratedYamlFiles(GameplayPrefabFolder))
				.Distinct(StringComparer.Ordinal);

			foreach (string assetPath in candidateFiles)
			{
				string absolutePath = ToProjectAbsolutePath(assetPath);
				if (!System.IO.File.Exists(absolutePath))
				{
					continue;
				}

				string text = System.IO.File.ReadAllText(absolutePath);
				string normalized = System.Text.RegularExpressions.Regex.Replace(
					text,
					@"[ \t]+(?=\r?\n|\z)",
					string.Empty);
				if (!string.Equals(text, normalized, StringComparison.Ordinal))
				{
					System.IO.File.WriteAllText(absolutePath, normalized);
				}
			}
		}

		private static IEnumerable<string> EnumerateGeneratedYamlFiles(string folderPath)
		{
			string absoluteFolderPath = ToProjectAbsolutePath(folderPath);
			if (!System.IO.Directory.Exists(absoluteFolderPath))
			{
				return Enumerable.Empty<string>();
			}

			return System.IO.Directory
				.EnumerateFiles(absoluteFolderPath, "*.*", System.IO.SearchOption.AllDirectories)
				.Select(ToProjectAssetPath)
				.Where(path =>
					path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
					path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ||
					path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
					path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
		}

		private static string ToProjectAbsolutePath(string assetPath)
		{
			string projectRoot = System.IO.Directory.GetParent(Application.dataPath)?.FullName
				?? throw new MissingReferenceException("无法定位 Unity 项目根目录，不能安全清理生成的 YAML。");
			return System.IO.Path.Combine(
				projectRoot,
				assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
		}

		private static string ToProjectAssetPath(string absolutePath)
		{
			string projectRoot = System.IO.Directory.GetParent(Application.dataPath)?.FullName
				?? throw new MissingReferenceException("无法定位 Unity 项目根目录，不能安全清理生成的 YAML。");
			string relativePath = System.IO.Path.GetRelativePath(projectRoot, absolutePath);
			return relativePath.Replace(System.IO.Path.DirectorySeparatorChar, '/');
		}

		private static void EnsureTestCardAsset()
		{
			EnsureTestCharacterCardAsset(
				TestContentPath,
                FoundationTestSceneHarness.TestContentId,
                "村民",
                "健康的村民。",
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
			WriteCharacterAttributeOverrides(
				RequireProperty(serializedContent, "m_attributeOverrides"),
				CreateStackCraftCombatAttributeOverrides(
					maxHealth: 15,
					attack: 2,
					defense: 1,
					attackSpeed: 130,
					accuracy: 95,
					dodge: 5,
					criticalChance: 5,
					criticalMultiplier: 150));
			SerializedProperty automaticBattleAbility = serializedContent.FindProperty(
				"m_automaticBattleAbilityCode");
			if (automaticBattleAbility == null)
			{
				throw new MissingReferenceException(
					$"{nameof(CharacterCardDefinition)} 缺少自动战斗 Ability 作者字段。");
			}
			automaticBattleAbility.intValue = XAbility.ABILITY_TabletopBasicAttack;
			serializedContent.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(content);
		}

        private static void EnsureTestProductAsset()
        {
            EnsureTestCardAsset(
                TestProductPath,
                FoundationTestSceneHarness.TestProductContentId,
                "木头",
                "一截木材。",
                XTag.Faction_Player);
			WriteCardLimitAndTradeFields(TestProductPath, sellValue: 1, countsTowardLimit: true);
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
            if (content != null && content.GetType() != typeof(CardDefinition))
            {
                AssetDatabase.DeleteAsset(assetPath);
                content = null;
            }

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

            ResolveCardArtReference(
                contentIdValue,
                out string cardArtPath,
                out string cardArtAddressValue);
			WriteSoftSpriteReference(
				serializedContent,
				"m_cardArt",
				cardArtPath,
				cardArtAddressValue,
				"卡面美术");
			ResolveCardSurfaceReference(
				content,
				contentIdValue,
				out string cardSurfacePath,
				out string cardSurfaceAddress);
			WriteSoftAssetReference(
				serializedContent,
				"m_cardSurface",
				cardSurfacePath,
				cardSurfaceAddress,
				"卡牌表面");
			WriteCardViewSizeFields(serializedContent, overrideViewSize: false, Vector2.one);
            WriteIntArray(
				serializedContent.FindProperty("m_tagCodes"),
				BuildCardTagCodes(cardSurfaceAddress, tagCode));
			return serializedContent;
		}

		/// <summary>按 StackCraft 卡面类别补齐 GAS 类别标签；业务标签仍由调用方显式传入。</summary>
		private static int[] BuildCardTagCodes(string cardSurfaceAddress, int primaryTagCode)
		{
			List<int> tagCodes = new();
			if (primaryTagCode != 0)
			{
				tagCodes.Add(primaryTagCode);
			}

			int categoryTagCode = ResolveStackCraftCategoryTagCode(cardSurfaceAddress);
			if (categoryTagCode != 0 && !tagCodes.Contains(categoryTagCode))
			{
				tagCodes.Add(categoryTagCode);
			}

			return tagCodes.ToArray();
		}

		private static int ResolveStackCraftCategoryTagCode(string cardSurfaceAddress)
		{
			switch (cardSurfaceAddress)
			{
				case ResourceCardSurfaceAddress:
					return XTag.Card_Category_Resource;
				case CharacterCardSurfaceAddress:
					return XTag.Card_Category_Character;
				case ConsumableCardSurfaceAddress:
					return XTag.Card_Category_Consumable;
				case MaterialCardSurfaceAddress:
					return XTag.Card_Category_Material;
				case EquipmentCardSurfaceAddress:
					return XTag.Card_Category_Equipment;
				case StructureCardSurfaceAddress:
				case CardBuyerSurfaceAddress:
					return XTag.Card_Category_Structure;
				case CurrencyCardSurfaceAddress:
					return XTag.Card_Category_Currency;
				case RecipeCardSurfaceAddress:
					return XTag.Card_Category_Recipe;
				case MobCardSurfaceAddress:
				case AggressiveMobCardSurfaceAddress:
					return XTag.Card_Category_Mob;
				case AreaCardSurfaceAddress:
					return XTag.Card_Category_Area;
				case ValuableCardSurfaceAddress:
					return XTag.Card_Category_Valuable;
				case PackCardSurfaceAddress:
					return 0;
				default:
					throw new MissingReferenceException(
						$"未登记 StackCraft 卡牌表面类别，无法生成 GAS 类别标签：{cardSurfaceAddress}");
			}
		}

		private static void WriteCardViewSizeFields(
			SerializedObject serializedContent,
			bool overrideViewSize,
			Vector2 viewSize)
		{
			RequireProperty(serializedContent, "m_overrideViewSize").boolValue = overrideViewSize;
			RequireProperty(serializedContent, "m_viewSize").vector2Value = viewSize;
		}

private static void WriteSoftSpriteReference(
			SerializedObject serializedContent,
			string propertyName,
			string assetPath,
			string address,
			string fieldDisplayName)
		{
			WriteSoftAssetReference(serializedContent, propertyName, assetPath, address, fieldDisplayName);
		}

		private static void WriteSoftAssetReference(
			SerializedObject serializedContent,
			string propertyName,
			string assetPath,
			string address,
			string fieldDisplayName)
		{
			SerializedProperty reference = serializedContent.FindProperty(propertyName);
			SerializedProperty referenceAddress = reference?.FindPropertyRelative("Address");
			if (referenceAddress == null)
			{
				throw new MissingReferenceException(
					$"{nameof(CardDefinition)} 的{fieldDisplayName}字段已变更，测试内容生成器需要同步更新。");
			}

			referenceAddress.stringValue = address;
#if UNITY_EDITOR
			SerializedProperty referenceGuid = reference.FindPropertyRelative("Guid");
			SerializedProperty referenceLocked = reference.FindPropertyRelative("Locked");
			if (referenceGuid != null)
			{
				referenceGuid.stringValue = AssetDatabase.AssetPathToGUID(assetPath);
			}
			if (referenceLocked != null)
			{
				referenceLocked.boolValue = true;
			}
#endif
		}

        private static void ResolveCardArtReference(
            string contentIdValue,
            out string cardArtPath,
            out string cardArtAddress)
        {
            switch (contentIdValue)
            {
                case FoundationTestSceneHarness.TestContentId:
                    cardArtPath = VillagerCardArtPath;
                    cardArtAddress = VillagerCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestProductContentId:
                    cardArtPath = WoodCardArtPath;
                    cardArtAddress = WoodCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestFoodContentId:
                    cardArtPath = BerryCardArtPath;
                    cardArtAddress = BerryCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestCardPackFirstRewardContentId:
                    cardArtPath = BerryBushCardArtPath;
                    cardArtAddress = BerryBushCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestCardPackSecondRewardContentId:
                    cardArtPath = RockCardArtPath;
                    cardArtAddress = RockCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestSellableCardContentId:
                    cardArtPath = StoneCardArtPath;
                    cardArtAddress = StoneCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestCurrencyCardContentId:
                    cardArtPath = CoinCardArtPath;
                    cardArtAddress = CoinCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestEncounterCardContentId:
                    cardArtPath = GoblinCardArtPath;
                    cardArtAddress = GoblinCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestBuyerCardContentId:
					cardArtPath = StackCraftPlaceholderCardArtPath;
					cardArtAddress = StackCraftPlaceholderCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestChestContentId:
                    cardArtPath = WoodenChestCardArtPath;
                    cardArtAddress = WoodenChestCardArtAddress;
                    return;
                case FoundationTestSceneHarness.TestCardPackContentId:
                    cardArtPath = StarterPackCardArtPath;
                    cardArtAddress = StarterPackCardArtAddress;
                    return;
				case FoundationTestSceneHarness.TestBeginningPackContentId:
					cardArtPath = BeginningPackCardArtPath;
					cardArtAddress = BeginningPackCardArtAddress;
					return;
                case FoundationTestSceneHarness.TestPackVendorContentId:
				case FoundationTestSceneHarness.TestBeginningPackVendorContentId:
                    cardArtPath = TreasureChestCardArtPath;
                    cardArtAddress = TreasureChestCardArtAddress;
                    return;
				case FoundationTestSceneHarness.TestBeginningPackSoilContentId:
					cardArtPath = SoilCardArtPath;
					cardArtAddress = SoilCardArtAddress;
					return;
				case FoundationTestSceneHarness.TestBeginningPackTreeContentId:
					cardArtPath = TreeCardArtPath;
					cardArtAddress = TreeCardArtAddress;
					return;
				case FoundationTestSceneHarness.TestBeginningPackChickenContentId:
					cardArtPath = ChickenCardArtPath;
					cardArtAddress = ChickenCardArtAddress;
					return;
				case FoundationTestSceneHarness.TestBeginningPackSlimeContentId:
					cardArtPath = SlimeCardArtPath;
					cardArtAddress = SlimeCardArtAddress;
					return;
				case FoundationTestSceneHarness.TestBeginningPackGoldenKeyContentId:
					cardArtPath = GoldenKeyCardArtPath;
					cardArtAddress = GoldenKeyCardArtAddress;
					return;
				case FoundationTestSceneHarness.TestBeginningPackEggContentId:
					cardArtPath = EggCardArtPath;
					cardArtAddress = EggCardArtAddress;
					return;
				case FoundationTestSceneHarness.TestRecipeGrowingBerryCardContentId:
				case FoundationTestSceneHarness.TestRecipeBuildingHouseCardContentId:
				case FoundationTestSceneHarness.TestRecipeMakingLoveCardContentId:
				case FoundationTestSceneHarness.TestRecipeMakingTimberCardContentId:
				case FoundationTestSceneHarness.TestRecipeCraftingStickCardContentId:
					cardArtPath = RecipeCardArtPath;
					cardArtAddress = RecipeCardArtAddress;
					return;
                default:
                    cardArtPath = TabletopCardArtPath;
                    cardArtAddress = TabletopCardArtAddress;
                    return;
            }
        }

		private static void ResolveCardSurfaceReference(
			CardDefinition content,
			string contentIdValue,
			out string cardSurfacePath,
			out string cardSurfaceAddress)
		{
			if (content is CharacterCardDefinition)
			{
				if (contentIdValue == FoundationTestSceneHarness.TestBeginningPackSlimeContentId)
				{
					cardSurfacePath = AggressiveMobCardSurfacePath;
					cardSurfaceAddress = AggressiveMobCardSurfaceAddress;
					return;
				}
				if (contentIdValue == FoundationTestSceneHarness.TestBeginningPackChickenContentId)
				{
					cardSurfacePath = MobCardSurfacePath;
					cardSurfaceAddress = MobCardSurfaceAddress;
					return;
				}
				cardSurfacePath = CharacterCardSurfacePath;
				cardSurfaceAddress = CharacterCardSurfaceAddress;
				return;
			}
			if (content is FoodCardDefinition)
			{
				cardSurfacePath = ConsumableCardSurfacePath;
				cardSurfaceAddress = ConsumableCardSurfaceAddress;
				return;
			}
			if (content is EquipmentCardDefinition)
			{
				cardSurfacePath = EquipmentCardSurfacePath;
				cardSurfaceAddress = EquipmentCardSurfaceAddress;
				return;
			}
			if (content is CardPackDefinition)
			{
				cardSurfacePath = PackCardSurfacePath;
				cardSurfaceAddress = PackCardSurfaceAddress;
				return;
			}
			if (content is CardBuyerDefinition)
			{
				cardSurfacePath = CardBuyerSurfacePath;
				cardSurfaceAddress = CardBuyerSurfaceAddress;
				return;
			}
			if (content is PackVendorDefinition)
			{
				cardSurfacePath = StructureCardSurfacePath;
				cardSurfaceAddress = StructureCardSurfaceAddress;
				return;
			}
			if (content is ChestCardDefinition)
			{
				cardSurfacePath = ValuableCardSurfacePath;
				cardSurfaceAddress = ValuableCardSurfaceAddress;
				return;
			}

			switch (contentIdValue)
			{
				case FoundationTestSceneHarness.TestCurrencyCardContentId:
					cardSurfacePath = CurrencyCardSurfacePath;
					cardSurfaceAddress = CurrencyCardSurfaceAddress;
					return;
				case FoundationTestSceneHarness.TestRecipeGrowingBerryCardContentId:
				case FoundationTestSceneHarness.TestRecipeBuildingHouseCardContentId:
				case FoundationTestSceneHarness.TestRecipeMakingLoveCardContentId:
				case FoundationTestSceneHarness.TestRecipeMakingTimberCardContentId:
				case FoundationTestSceneHarness.TestRecipeCraftingStickCardContentId:
					cardSurfacePath = RecipeCardSurfacePath;
					cardSurfaceAddress = RecipeCardSurfaceAddress;
					return;
				case FoundationTestSceneHarness.TestBuyerCardContentId:
					cardSurfacePath = StructureCardSurfacePath;
					cardSurfaceAddress = StructureCardSurfaceAddress;
					return;
				case FoundationTestSceneHarness.TestEncounterCardContentId:
					cardSurfacePath = AggressiveMobCardSurfacePath;
					cardSurfaceAddress = AggressiveMobCardSurfaceAddress;
					return;
				case FoundationTestSceneHarness.TestCardPackFirstRewardContentId:
				case FoundationTestSceneHarness.TestCardPackSecondRewardContentId:
				case FoundationTestSceneHarness.TestBeginningPackSoilContentId:
				case FoundationTestSceneHarness.TestBeginningPackTreeContentId:
					cardSurfacePath = ResourceCardSurfacePath;
					cardSurfaceAddress = ResourceCardSurfaceAddress;
					return;
				case FoundationTestSceneHarness.TestBeginningPackGoldenKeyContentId:
					cardSurfacePath = ValuableCardSurfacePath;
					cardSurfaceAddress = ValuableCardSurfaceAddress;
					return;
				default:
					cardSurfacePath = MaterialCardSurfacePath;
					cardSurfaceAddress = MaterialCardSurfaceAddress;
					return;
			}
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
                "测试行动",
                "仅用于验证行动作者源、参与条件、回合进度和权威随机结果经过 YooAsset 进入正式链路。");
            SerializedProperty slots = serializedAction.FindProperty("m_participationSlots");
            SerializedProperty turnCost = serializedAction.FindProperty("m_turnCost");
            SerializedProperty resultIntents = serializedAction.FindProperty("m_resultIntents");
            SerializedProperty resultBranches = serializedAction.FindProperty("m_resultBranches");
            SerializedProperty journalGroupName = serializedAction.FindProperty("m_journalGroupName");
            if (slots == null || turnCost == null || resultIntents == null ||
                resultBranches == null || journalGroupName == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(ActionDefinition)} 的回合消耗、日志分组、参与槽位、结果意图或随机分支字段已变更，测试行动生成器需要同步更新。");
            }

            turnCost.intValue = 2;
            journalGroupName.stringValue = "建造";
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
			SerializedProperty journalGroupName = serializedAction.FindProperty("m_journalGroupName");
			if (slots == null || turnCost == null || resultIntents == null ||
				resultBranches == null || journalGroupName == null)
			{
				throw new MissingReferenceException(
					$"{nameof(ActionDefinition)} 的作者字段已变更，填槽测试行动生成器需要同步更新。");
			}

			turnCost.intValue = 1;
			journalGroupName.stringValue = "杂项";
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

		private static void EnsureDayCycleTestAssets()
		{
			EnsureTestFoodAsset();
			EnsureTestCardAsset(
				TestSellableCardPath,
				FoundationTestSceneHarness.TestSellableCardContentId,
				"石头",
				"一块小石头。",
				XTag.Faction_Player);
			WriteCardInstanceFields(TestSellableCardPath, initialUses: 1, sellValue: 1, countsTowardLimit: true);
			EnsureTestCardAsset(
				TestCurrencyCardPath,
				FoundationTestSceneHarness.TestCurrencyCardContentId,
				"金币",
				"一枚闪亮的金币。",
				XTag.Faction_Player);
			WriteCardInstanceFields(TestCurrencyCardPath, initialUses: 1, sellValue: 0, countsTowardLimit: false);
			EnsureCardBuyerAsset();
			WriteCardLimitAndTradeFields(TestBuyerCardPath, sellValue: 0, countsTowardLimit: false);
			EnsureTestCardAsset(
				TestEncounterCardPath,
				FoundationTestSceneHarness.TestEncounterCardContentId,
				"夜间来客",
				"日终遭遇阶段生成，并在现有日程 HUD 中反馈。",
				XTag.Faction_Player);
			WriteCardLimitAndTradeFields(TestEncounterCardPath, sellValue: 1, countsTowardLimit: true);
			EnsureTestSellActionAsset();
			EnsureTestDayCycleScenarioAsset();
		}

		private static void EnsureCardBuyerAsset()
		{
			CardDefinition existing = AssetDatabase.LoadAssetAtPath<CardDefinition>(TestBuyerCardPath);
			if (existing != null && existing is not CardBuyerDefinition)
			{
				AssetDatabase.DeleteAsset(TestBuyerCardPath);
				existing = null;
			}

			CardBuyerDefinition buyer = existing as CardBuyerDefinition;
			if (buyer == null)
			{
				buyer = ScriptableObject.CreateInstance<CardBuyerDefinition>();
				AssetDatabase.CreateAsset(buyer, TestBuyerCardPath);
			}

			SerializedObject serializedBuyer = WriteCardFields(
				buyer,
				FoundationTestSceneHarness.TestBuyerCardContentId,
				"收购点",
				"接收可售卡牌的固定交互节点。",
				XTag.Faction_Player);
			WriteCardViewSizeFields(
				serializedBuyer,
				overrideViewSize: true,
				StackCraftTradeZoneViewSize);
			RequireProperty(serializedBuyer, "m_currencyCardId")
				.FindPropertyRelative("m_value").stringValue =
					FoundationTestSceneHarness.TestCurrencyCardContentId;
			RequireProperty(serializedBuyer, "m_currencySpawnOffset").vector2Value =
				StackCraftTradeZoneSpawnOffset;
			serializedBuyer.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(buyer);
		}

		private static void EnsureCardPackTestAssets()
		{
			EnsureTestCardAsset(
				TestCardPackFirstRewardPath,
				FoundationTestSceneHarness.TestCardPackFirstRewardContentId,
				"浆果丛",
				"这里生长着浆果。",
				XTag.Faction_Player);
			WriteCardInstanceFields(TestCardPackFirstRewardPath, initialUses: 5, sellValue: 3, countsTowardLimit: true);
			EnsureTestCardAsset(
				TestCardPackSecondRewardPath,
				FoundationTestSceneHarness.TestCardPackSecondRewardContentId,
				"岩石",
				"一块巨大的岩石。",
				XTag.Faction_Player);
			WriteCardInstanceFields(TestCardPackSecondRewardPath, initialUses: 3, sellValue: 0, countsTowardLimit: true);
			EnsureBeginningPackBusinessAssets();

			CardDefinition existing = AssetDatabase.LoadAssetAtPath<CardDefinition>(TestCardPackPath);
			if (existing != null && existing is not CardPackDefinition)
			{
				AssetDatabase.DeleteAsset(TestCardPackPath);
				existing = null;
			}
			CardPackDefinition pack = existing as CardPackDefinition;
			if (pack == null)
			{
				pack = ScriptableObject.CreateInstance<CardPackDefinition>();
				AssetDatabase.CreateAsset(pack, TestCardPackPath);
			}
			SerializedObject serializedPack = WriteCardFields(
				pack,
				FoundationTestSceneHarness.TestCardPackContentId,
				"初始卡包",
				"一个初始卡包。",
				XTag.Faction_Player);
			WriteCardViewSizeFields(
				serializedPack,
				overrideViewSize: true,
				StackCraftPackInstanceViewSize);
			RequireProperty(serializedPack, "m_countsTowardCardLimit").boolValue = false;
			SerializedProperty slots = RequireProperty(serializedPack, "m_slots");
			slots.arraySize = 4;
			WriteFixedCardPackSlot(
				slots.GetArrayElementAtIndex(0),
				FoundationTestSceneHarness.TestContentId);
			WriteFixedCardPackSlot(
				slots.GetArrayElementAtIndex(1),
				FoundationTestSceneHarness.TestCardPackFirstRewardContentId);
			WriteFixedCardPackSlot(
				slots.GetArrayElementAtIndex(2),
				FoundationTestSceneHarness.TestCardPackSecondRewardContentId);
			WriteFixedCardPackSlot(
				slots.GetArrayElementAtIndex(3),
				FoundationTestSceneHarness.TestProductContentId);
			serializedPack.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(pack);

			ActionDefinition openAction =
				AssetDatabase.LoadAssetAtPath<ActionDefinition>(TestOpenCardPackActionPath);
			if (openAction == null)
			{
				openAction = ScriptableObject.CreateInstance<ActionDefinition>();
				AssetDatabase.CreateAsset(openAction, TestOpenCardPackActionPath);
			}
			SerializedObject serializedAction = WriteCommonContentFields(
				openAction,
				FoundationTestSceneHarness.TestOpenCardPackActionContentId,
				"打开卡包",
				"多次点击卡包，完全打开并取得里面的所有卡牌。"
			);
			RequireProperty(serializedAction, "m_turnCost").intValue = 0;
			RequireProperty(serializedAction, "m_canStartFromClick").boolValue = true;
			SerializedProperty actionSlots = RequireProperty(serializedAction, "m_participationSlots");
			actionSlots.arraySize = 1;
			WriteExactContentSlot(
				actionSlots.GetArrayElementAtIndex(0),
				"pack",
				"卡包",
				FoundationTestSceneHarness.TestCardPackContentId);
			WriteContentIdArray(
				RequireRelative(actionSlots.GetArrayElementAtIndex(0), "m_allowedContentIds"),
				FoundationTestSceneHarness.TestCardPackContentId,
				FoundationTestSceneHarness.TestBeginningPackContentId);
			RequireProperty(serializedAction, "m_conditions").arraySize = 0;

			SerializedProperty intents = RequireProperty(serializedAction, "m_resultIntents");
			intents.arraySize = 1;
			SerializedProperty openIntent = intents.GetArrayElementAtIndex(0);
			openIntent.managedReferenceValue = new OpenCardPackResultIntent();
			RequireRelative(openIntent, "m_packSlotKey").stringValue = "pack";
			RequireProperty(serializedAction, "m_resultBranches").arraySize = 0;
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
			openAction.EnsureLocalAuthoringKeys();
			EditorUtility.SetDirty(openAction);
		}

		private static void WriteFixedCardPackSlot(SerializedProperty slot, string rewardContentId)
		{
			SerializedProperty entries = RequireRelative(slot, "m_entries");
			entries.arraySize = 1;
			SerializedProperty entry = entries.GetArrayElementAtIndex(0);
			RequireRelative(entry, "m_cardId").FindPropertyRelative("m_value").stringValue = rewardContentId;
			RequireRelative(entry, "m_weight").intValue = 1;
			RequireRelative(slot, "m_recipeEntries").arraySize = 0;
			RequireRelative(slot, "m_recipeChance").floatValue = 0f;
		}


		private static void EnsureBeginningPackBusinessAssets()
		{
			EnsureTestCardAsset(
				TestBeginningEggPath,
				FoundationTestSceneHarness.TestBeginningPackEggContentId,
				"鸡蛋",
				"先烹饪再食用。",
				XTag.Faction_Player);
			WriteCardInstanceFields(TestBeginningEggPath, initialUses: 1, sellValue: 1, countsTowardLimit: true);
			EnsureTestCardAsset(
				TestBeginningSoilPath,
				FoundationTestSceneHarness.TestBeginningPackSoilContentId,
				"土壤",
				"植物生长的土壤。",
				XTag.Faction_Player);
			WriteCardInstanceFields(TestBeginningSoilPath, initialUses: 1, sellValue: 3, countsTowardLimit: true);
			EnsureTestCardAsset(
				TestBeginningTreePath,
				FoundationTestSceneHarness.TestBeginningPackTreeContentId,
				"树",
				"一棵高大的树。",
				XTag.Faction_Player);
			WriteCardInstanceFields(TestBeginningTreePath, initialUses: 3, sellValue: 0, countsTowardLimit: true);
			EnsureBeginningPackMobAsset(
				TestBeginningChickenPath,
				FoundationTestSceneHarness.TestBeginningPackChickenContentId,
				"鸡",
				"健康的鸡。",
				XTag.Faction,
				abilitySystemPresetId: NeutralCreatureAscPresetId,
				hasAutomaticHostility: false,
				attributeOverrides: CreateStackCraftCombatAttributeOverrides(
					maxHealth: 5,
					attack: 1,
					defense: 1,
					attackSpeed: 100,
					accuracy: 95,
					dodge: 20,
					criticalChance: 5,
					criticalMultiplier: 150));
			WriteCardInstanceFields(TestBeginningChickenPath, initialUses: 1, sellValue: 0, countsTowardLimit: true);
			WritePeriodicProductionFields(
				TestBeginningChickenPath,
				FoundationTestSceneHarness.TestBeginningPackEggContentId,
				intervalSeconds: 30f);
			WriteAutomaticMovementFields(
				TestBeginningChickenPath,
				intervalSeconds: StackCraftAutomaticMovementIntervalSeconds,
				radius: StackCraftAutomaticMovementRadius,
				maxAttempts: StackCraftAutomaticMovementMaxAttempts);
			EnsureBeginningPackMobAsset(
				TestBeginningSlimePath,
				FoundationTestSceneHarness.TestBeginningPackSlimeContentId,
				"史莱姆",
				"湿滑的史莱姆。",
				XTag.Faction_Enemy,
				abilitySystemPresetId: HostileCreatureAscPresetId,
				hasAutomaticHostility: true,
				attributeOverrides: CreateStackCraftCombatAttributeOverrides(
					maxHealth: 7,
					attack: 3,
					defense: 0,
					attackSpeed: 60,
					accuracy: 75,
					dodge: 5,
					criticalChance: 5,
					criticalMultiplier: 150));
			WriteCardInstanceFields(TestBeginningSlimePath, initialUses: 1, sellValue: 1, countsTowardLimit: true);
			WriteAutomaticMovementFields(
				TestBeginningSlimePath,
				intervalSeconds: StackCraftAutomaticMovementIntervalSeconds,
				radius: StackCraftAutomaticMovementRadius,
				maxAttempts: StackCraftAutomaticMovementMaxAttempts);
			WriteAutomaticHostileBehaviorFields(
				TestBeginningSlimePath,
				aggroRadius: 5f,
				attackRadius: 1.5f);
			EnsureTestCardAsset(
				TestBeginningGoldenKeyPath,
				FoundationTestSceneHarness.TestBeginningPackGoldenKeyContentId,
				"金钥匙",
				"纯金制成的钥匙。能打开的东西应该很珍贵。",
				XTag.Faction_Player);
			WriteCardInstanceFields(TestBeginningGoldenKeyPath, initialUses: 1, sellValue: 3, countsTowardLimit: true);

			EnsureRecipeActionAsset(
				TestRecipeGrowingBerryActionPath,
				FoundationTestSceneHarness.TestRecipeGrowingBerryActionContentId,
				"种植浆果",
				"StackCraft 配方参考：土壤 ×1，浆果 ×1。制作耗时 120 秒。",
				"畜牧");
			EnsureRecipeActionAsset(
				TestRecipeBuildingHouseActionPath,
				FoundationTestSceneHarness.TestRecipeBuildingHouseActionContentId,
				"建造房屋",
				"StackCraft 配方参考：石头 ×1，木头 ×2，村民 ×1。制作耗时 30 秒。",
				"建造");
			EnsureRecipeActionAsset(
				TestRecipeMakingLoveActionPath,
				FoundationTestSceneHarness.TestRecipeMakingLoveActionContentId,
				"孕育",
				"StackCraft 配方参考：房屋 ×1，村民 ×2。制作耗时 20 秒。",
				"畜牧");
			EnsureRecipeActionAsset(
				TestRecipeMakingTimberActionPath,
				FoundationTestSceneHarness.TestRecipeMakingTimberActionContentId,
				"制作木材",
				"StackCraft 配方参考：木头 ×1，村民 ×1。制作耗时 10 秒。",
				"加工");
			EnsureRecipeActionAsset(
				TestRecipeCraftingStickActionPath,
				FoundationTestSceneHarness.TestRecipeCraftingStickActionContentId,
				"制作木棍",
				"StackCraft 配方参考：木材 ×1，村民 ×1。制作耗时 10 秒。",
				"加工");

			EnsureRecipeCardAsset(
				TestRecipeGrowingBerryCardPath,
				FoundationTestSceneHarness.TestRecipeGrowingBerryCardContentId,
				"配方：浆果丛",
				"土壤 ×1，浆果 ×1。");
			EnsureRecipeCardAsset(
				TestRecipeBuildingHouseCardPath,
				FoundationTestSceneHarness.TestRecipeBuildingHouseCardContentId,
				"配方：房屋",
				"石头 ×1，木头 ×2，村民 ×1。");
			EnsureRecipeCardAsset(
				TestRecipeMakingLoveCardPath,
				FoundationTestSceneHarness.TestRecipeMakingLoveCardContentId,
				"配方：婴儿",
				"房屋 ×1，村民 ×2。");
			EnsureRecipeCardAsset(
				TestRecipeMakingTimberCardPath,
				FoundationTestSceneHarness.TestRecipeMakingTimberCardContentId,
				"配方：木材",
				"木头 ×1，村民 ×1。");
			EnsureRecipeCardAsset(
				TestRecipeCraftingStickCardPath,
				FoundationTestSceneHarness.TestRecipeCraftingStickCardContentId,
				"配方：木棍",
				"木材 ×1，村民 ×1。");

			EnsureBeginningPackAsset();
		}

		private static void EnsureBeginningPackMobAsset(
			string assetPath,
			string contentId,
			string displayName,
			string description,
			int tagCode,
			int abilitySystemPresetId,
			bool hasAutomaticHostility,
			CharacterAttributeOverride[] attributeOverrides)
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
				contentId,
				displayName,
				description,
				tagCode);
			RequireProperty(serializedContent, "m_abilitySystemPresetId").intValue = abilitySystemPresetId;
			RequireProperty(serializedContent, "m_automaticBattleAbilityCode").intValue =
				XAbility.ABILITY_TabletopBasicAttack;
			WriteCharacterAttributeOverrides(
				RequireProperty(serializedContent, "m_attributeOverrides"),
				attributeOverrides);
			if (!hasAutomaticHostility)
			{
				RequireProperty(serializedContent, "m_automaticAggroRadius").floatValue = 0f;
				RequireProperty(serializedContent, "m_automaticAttackRadius").floatValue = 0f;
			}
			serializedContent.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(content);
		}

		private static CharacterAttributeOverride[] CreateStackCraftCombatAttributeOverrides(
			float maxHealth,
			float attack,
			float defense,
			float attackSpeed,
			float accuracy,
			float dodge,
			float criticalChance,
			float criticalMultiplier)
		{
			return new[]
			{
				new CharacterAttributeOverride(XAttribute.Health, maxHealth),
				new CharacterAttributeOverride(XAttribute.MaxHealth, maxHealth),
				new CharacterAttributeOverride(XAttribute.Attack, attack),
				new CharacterAttributeOverride(XAttribute.Defense, defense),
				new CharacterAttributeOverride(XAttribute.AttackSpeed, attackSpeed),
				new CharacterAttributeOverride(XAttribute.Accuracy, accuracy),
				new CharacterAttributeOverride(XAttribute.Dodge, dodge),
				new CharacterAttributeOverride(XAttribute.CriticalChance, criticalChance),
				new CharacterAttributeOverride(XAttribute.CriticalMultiplier, criticalMultiplier)
			};
		}

		private static void WriteCharacterAttributeOverrides(
			SerializedProperty property,
			IReadOnlyList<CharacterAttributeOverride> overrides)
		{
			if (overrides == null)
			{
				property.arraySize = 0;
				return;
			}

			property.arraySize = overrides.Count;
			for (int overrideIndex = 0; overrideIndex < overrides.Count; overrideIndex++)
			{
				SerializedProperty item = property.GetArrayElementAtIndex(overrideIndex);
				CharacterAttributeOverride attributeOverride = overrides[overrideIndex];
				RequireRelative(item, "m_attributeCode").intValue = attributeOverride.AttributeCode;
				RequireRelative(item, "m_baseValue").floatValue = attributeOverride.BaseValue;
			}
		}

		private static void EnsureRecipeActionAsset(
			string assetPath,
			string contentId,
			string displayName,
			string description,
			string journalGroupName)
		{
			ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(assetPath);
			if (action == null)
			{
				action = ScriptableObject.CreateInstance<ActionDefinition>();
				AssetDatabase.CreateAsset(action, assetPath);
			}
			SerializedObject serializedAction = WriteCommonContentFields(action, contentId, displayName, description);
			RequireProperty(serializedAction, "m_journalGroupName").stringValue = journalGroupName;
			RequireProperty(serializedAction, "m_turnCost").intValue = 1;
			RequireProperty(serializedAction, "m_canStartFromClick").boolValue = false;
			RequireProperty(serializedAction, "m_participationSlots").arraySize = 0;
			RequireProperty(serializedAction, "m_conditions").arraySize = 0;
			RequireProperty(serializedAction, "m_resultIntents").arraySize = 0;
			RequireProperty(serializedAction, "m_resultBranches").arraySize = 0;
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
			action.EnsureLocalAuthoringKeys();
			EditorUtility.SetDirty(action);
		}

		private static void EnsureRecipeCardAsset(
			string assetPath,
			string contentId,
			string displayName,
			string description)
		{
			EnsureTestCardAsset(assetPath, contentId, displayName, description, XTag.Faction_Player);
			WriteCardInstanceFields(assetPath, initialUses: 1, sellValue: 1, countsTowardLimit: true);
		}

		private static void EnsureBeginningPackAsset()
		{
			CardDefinition existing = AssetDatabase.LoadAssetAtPath<CardDefinition>(TestBeginningPackPath);
			if (existing != null && existing is not CardPackDefinition)
			{
				AssetDatabase.DeleteAsset(TestBeginningPackPath);
				existing = null;
			}
			CardPackDefinition pack = existing as CardPackDefinition;
			if (pack == null)
			{
				pack = ScriptableObject.CreateInstance<CardPackDefinition>();
				AssetDatabase.CreateAsset(pack, TestBeginningPackPath);
			}
			SerializedObject serializedPack = WriteCardFields(
				pack,
				FoundationTestSceneHarness.TestBeginningPackContentId,
				"开端卡包",
				"一个开端卡包。",
				XTag.Faction_Player);
			WriteCardViewSizeFields(
				serializedPack,
				overrideViewSize: true,
				StackCraftPackInstanceViewSize);
			RequireProperty(serializedPack, "m_countsTowardCardLimit").boolValue = false;
			SerializedProperty slots = RequireProperty(serializedPack, "m_slots");
			slots.arraySize = 3;
			for (int slotIndex = 0; slotIndex < slots.arraySize; slotIndex++)
			{
				WriteBeginningCardPackSlot(slots.GetArrayElementAtIndex(slotIndex));
			}
			serializedPack.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(pack);
		}

		private static void WriteBeginningCardPackSlot(SerializedProperty slot)
		{
			SerializedProperty entries = RequireRelative(slot, "m_entries");
			entries.arraySize = 9;
			WriteCardPackEntry(entries.GetArrayElementAtIndex(0), FoundationTestSceneHarness.TestSellableCardContentId, 16);
			WriteCardPackEntry(entries.GetArrayElementAtIndex(1), FoundationTestSceneHarness.TestProductContentId, 16);
			WriteCardPackEntry(entries.GetArrayElementAtIndex(2), FoundationTestSceneHarness.TestCardPackFirstRewardContentId, 14);
			WriteCardPackEntry(entries.GetArrayElementAtIndex(3), FoundationTestSceneHarness.TestCardPackSecondRewardContentId, 14);
			WriteCardPackEntry(entries.GetArrayElementAtIndex(4), FoundationTestSceneHarness.TestBeginningPackSoilContentId, 14);
			WriteCardPackEntry(entries.GetArrayElementAtIndex(5), FoundationTestSceneHarness.TestBeginningPackTreeContentId, 14);
			WriteCardPackEntry(entries.GetArrayElementAtIndex(6), FoundationTestSceneHarness.TestBeginningPackChickenContentId, 4);
			WriteCardPackEntry(entries.GetArrayElementAtIndex(7), FoundationTestSceneHarness.TestBeginningPackSlimeContentId, 4);
			WriteCardPackEntry(entries.GetArrayElementAtIndex(8), FoundationTestSceneHarness.TestBeginningPackGoldenKeyContentId, 4);

			SerializedProperty recipes = RequireRelative(slot, "m_recipeEntries");
			recipes.arraySize = 5;
			WriteCardPackRecipeEntry(
				recipes.GetArrayElementAtIndex(0),
				FoundationTestSceneHarness.TestRecipeGrowingBerryActionContentId,
				FoundationTestSceneHarness.TestRecipeGrowingBerryCardContentId);
			WriteCardPackRecipeEntry(
				recipes.GetArrayElementAtIndex(1),
				FoundationTestSceneHarness.TestRecipeBuildingHouseActionContentId,
				FoundationTestSceneHarness.TestRecipeBuildingHouseCardContentId);
			WriteCardPackRecipeEntry(
				recipes.GetArrayElementAtIndex(2),
				FoundationTestSceneHarness.TestRecipeMakingLoveActionContentId,
				FoundationTestSceneHarness.TestRecipeMakingLoveCardContentId);
			WriteCardPackRecipeEntry(
				recipes.GetArrayElementAtIndex(3),
				FoundationTestSceneHarness.TestRecipeMakingTimberActionContentId,
				FoundationTestSceneHarness.TestRecipeMakingTimberCardContentId);
			WriteCardPackRecipeEntry(
				recipes.GetArrayElementAtIndex(4),
				FoundationTestSceneHarness.TestRecipeCraftingStickActionContentId,
				FoundationTestSceneHarness.TestRecipeCraftingStickCardContentId);
			RequireRelative(slot, "m_recipeChance").floatValue = 0.1f;
		}

		private static void WriteCardPackEntry(SerializedProperty entry, string cardId, int weight)
		{
			RequireRelative(entry, "m_cardId").FindPropertyRelative("m_value").stringValue = cardId;
			RequireRelative(entry, "m_weight").intValue = weight;
		}

		private static void WriteCardPackRecipeEntry(SerializedProperty entry, string actionId, string recipeCardId)
		{
			RequireRelative(entry, "m_actionId").FindPropertyRelative("m_value").stringValue = actionId;
			RequireRelative(entry, "m_recipeCardId").FindPropertyRelative("m_value").stringValue = recipeCardId;
		}

		private static void EnsurePackVendorAsset(
			string assetPath,
			string contentId,
			string displayName,
			string description,
			string offeredPackId,
			int price,
			int minimumCompletedQuests)
		{
			PackVendorDefinition vendor = AssetDatabase.LoadAssetAtPath<PackVendorDefinition>(assetPath);
			if (vendor == null)
			{
				CardDefinition wrongType = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
				if (wrongType != null)
				{
					AssetDatabase.DeleteAsset(assetPath);
				}
				vendor = ScriptableObject.CreateInstance<PackVendorDefinition>();
				AssetDatabase.CreateAsset(vendor, assetPath);
			}
			SerializedObject serializedVendor = WriteCardFields(
				vendor,
				contentId,
				displayName,
				description,
				XTag.Faction_Player);
			WriteCardViewSizeFields(
				serializedVendor,
				overrideViewSize: true,
				StackCraftTradeZoneViewSize);
			RequireProperty(serializedVendor, "m_offeredPackId").FindPropertyRelative("m_value").stringValue = offeredPackId;
			RequireProperty(serializedVendor, "m_price").intValue = price;
			RequireProperty(serializedVendor, "m_minimumCompletedQuests").intValue = minimumCompletedQuests;
			RequireProperty(serializedVendor, "m_countsTowardCardLimit").boolValue = false;
			RequireProperty(serializedVendor, "m_packSpawnOffset").vector2Value =
				StackCraftTradeZoneSpawnOffset;
			serializedVendor.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(vendor);
		}

		private static void EnsurePackVendorTestAssets()
		{
			PackVendorDefinition vendor = AssetDatabase.LoadAssetAtPath<PackVendorDefinition>(TestPackVendorPath);
			if (vendor == null)
			{
				CardDefinition wrongType = AssetDatabase.LoadAssetAtPath<CardDefinition>(TestPackVendorPath);
				if (wrongType != null)
				{
					AssetDatabase.DeleteAsset(TestPackVendorPath);
				}
				vendor = ScriptableObject.CreateInstance<PackVendorDefinition>();
				AssetDatabase.CreateAsset(vendor, TestPackVendorPath);
			}
			SerializedObject serializedVendor = WriteCardFields(
				vendor,
				FoundationTestSceneHarness.TestPackVendorContentId,
				"卡包商贩",
				"投入金币购买初始卡包。",
				XTag.Faction_Player);
			WriteCardViewSizeFields(
				serializedVendor,
				overrideViewSize: true,
				StackCraftTradeZoneViewSize);
			RequireProperty(serializedVendor, "m_offeredPackId").FindPropertyRelative("m_value").stringValue =
				FoundationTestSceneHarness.TestCardPackContentId;
			RequireProperty(serializedVendor, "m_price").intValue = 2;
			RequireProperty(serializedVendor, "m_minimumCompletedQuests").intValue = 0;
			RequireProperty(serializedVendor, "m_countsTowardCardLimit").boolValue = false;
			RequireProperty(serializedVendor, "m_packSpawnOffset").vector2Value =
				StackCraftTradeZoneSpawnOffset;
			serializedVendor.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(vendor);

			EnsurePackVendorAsset(
				TestBeginningPackVendorPath,
				FoundationTestSceneHarness.TestBeginningPackVendorContentId,
				"开端卡包商贩",
				"投入金币购买开端卡包。",
				FoundationTestSceneHarness.TestBeginningPackContentId,
				price: 3,
				minimumCompletedQuests: 3);

			ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(TestPurchaseCardPackActionPath);
			if (action == null)
			{
				action = ScriptableObject.CreateInstance<ActionDefinition>();
				AssetDatabase.CreateAsset(action, TestPurchaseCardPackActionPath);
			}
			SerializedObject serializedAction = WriteCommonContentFields(
				action,
				FoundationTestSceneHarness.TestPurchaseCardPackActionContentId,
				"购买卡包",
				"把货币投入商贩；付款可以分次保存，满价后生成卡包。");
			RequireProperty(serializedAction, "m_turnCost").intValue = 0;
			RequireProperty(serializedAction, "m_canStartFromClick").boolValue = false;
			SerializedProperty slots = RequireProperty(serializedAction, "m_participationSlots");
			slots.arraySize = 2;
			WriteExactContentSlot(
				slots.GetArrayElementAtIndex(0),
				"payment",
				"货币",
				FoundationTestSceneHarness.TestCurrencyCardContentId);
			WriteContentIdArray(
				RequireRelative(slots.GetArrayElementAtIndex(0), "m_allowedContentIds"),
				FoundationTestSceneHarness.TestCurrencyCardContentId,
				FoundationTestSceneHarness.TestChestContentId);
			RequireRelative(slots.GetArrayElementAtIndex(0), "m_maximumParticipants").intValue = 0;
			WriteExactContentSlot(
				slots.GetArrayElementAtIndex(1),
				"vendor",
				"商贩",
				FoundationTestSceneHarness.TestPackVendorContentId);
			WriteContentIdArray(
				RequireRelative(slots.GetArrayElementAtIndex(1), "m_allowedContentIds"),
				FoundationTestSceneHarness.TestPackVendorContentId,
				FoundationTestSceneHarness.TestBeginningPackVendorContentId);

			SerializedProperty conditions = RequireProperty(serializedAction, "m_conditions");
			conditions.arraySize = 2;
			SerializedProperty unlockCondition = conditions.GetArrayElementAtIndex(0);
			unlockCondition.managedReferenceValue = new PackVendorUnlockedCondition();
			RequireRelative(unlockCondition, "m_vendorSlotKey").stringValue = "vendor";
			SerializedProperty paymentCondition = conditions.GetArrayElementAtIndex(1);
			paymentCondition.managedReferenceValue = new CardPaymentSourceAvailableCondition();
			RequireRelative(paymentCondition, "m_paymentSlotKey").stringValue = "payment";

			SerializedProperty intents = RequireProperty(serializedAction, "m_resultIntents");
			intents.arraySize = 1;
			SerializedProperty purchaseIntent = intents.GetArrayElementAtIndex(0);
			purchaseIntent.managedReferenceValue = new PurchaseCardPackResultIntent();
			RequireRelative(purchaseIntent, "m_vendorSlotKey").stringValue = "vendor";
			RequireRelative(purchaseIntent, "m_paymentSlotKey").stringValue = "payment";
			RequireProperty(serializedAction, "m_resultBranches").arraySize = 0;
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
			action.EnsureLocalAuthoringKeys();
			EditorUtility.SetDirty(action);
		}

		private static void EnsureChestTestAssets()
		{
			CardDefinition existing = AssetDatabase.LoadAssetAtPath<CardDefinition>(TestChestPath);
			if (existing != null && existing is not ChestCardDefinition)
			{
				AssetDatabase.DeleteAsset(TestChestPath);
				existing = null;
			}
			ChestCardDefinition chest = existing as ChestCardDefinition;
			if (chest == null)
			{
				chest = ScriptableObject.CreateInstance<ChestCardDefinition>();
				AssetDatabase.CreateAsset(chest, TestChestPath);
			}
			SerializedObject serializedChest = WriteCardFields(
				chest,
				FoundationTestSceneHarness.TestChestContentId,
				"测试钱箱",
				"拖入金币可存储，单击可取出，并可直接作为商贩付款来源。",
				XTag.Faction_Player);
			RequireProperty(serializedChest, "m_capacity").intValue = 2;
			RequireProperty(serializedChest, "m_currencyCardId")
				.FindPropertyRelative("m_value").stringValue =
					FoundationTestSceneHarness.TestCurrencyCardContentId;
			RequireProperty(serializedChest, "m_countsTowardCardLimit").boolValue = true;
			serializedChest.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(chest);

			EnsureDepositCurrencyIntoChestActionAsset();
			EnsureWithdrawCurrencyFromChestActionAsset();
		}

		private static void EnsureDepositCurrencyIntoChestActionAsset()
		{
			ActionDefinition action =
				AssetDatabase.LoadAssetAtPath<ActionDefinition>(TestDepositCurrencyIntoChestActionPath);
			if (action == null)
			{
				action = ScriptableObject.CreateInstance<ActionDefinition>();
				AssetDatabase.CreateAsset(action, TestDepositCurrencyIntoChestActionPath);
			}
			SerializedObject serializedAction = WriteCommonContentFields(
				action,
				FoundationTestSceneHarness.TestDepositCurrencyIntoChestActionContentId,
				"存入钱箱",
				"把货币拖到钱箱，立即把货币卡转换为钱箱里的存币数量。");
			RequireProperty(serializedAction, "m_turnCost").intValue = 0;
			RequireProperty(serializedAction, "m_canStartFromClick").boolValue = false;
			SerializedProperty slots = RequireProperty(serializedAction, "m_participationSlots");
			slots.arraySize = 2;
			WriteExactContentSlot(
				slots.GetArrayElementAtIndex(0),
				"currency",
				"货币",
				FoundationTestSceneHarness.TestCurrencyCardContentId);
			RequireRelative(slots.GetArrayElementAtIndex(0), "m_maximumParticipants").intValue = 0;
			WriteExactContentSlot(
				slots.GetArrayElementAtIndex(1),
				"chest",
				"钱箱",
				FoundationTestSceneHarness.TestChestContentId);

			SerializedProperty conditions = RequireProperty(serializedAction, "m_conditions");
			conditions.arraySize = 1;
			SerializedProperty capacityCondition = conditions.GetArrayElementAtIndex(0);
			capacityCondition.managedReferenceValue = new ChestHasCapacityCondition();
			RequireRelative(capacityCondition, "m_chestSlotKey").stringValue = "chest";

			SerializedProperty intents = RequireProperty(serializedAction, "m_resultIntents");
			intents.arraySize = 1;
			SerializedProperty depositIntent = intents.GetArrayElementAtIndex(0);
			depositIntent.managedReferenceValue = new DepositCurrencyIntoChestResultIntent();
			RequireRelative(depositIntent, "m_chestSlotKey").stringValue = "chest";
			RequireRelative(depositIntent, "m_currencySlotKey").stringValue = "currency";
			RequireProperty(serializedAction, "m_resultBranches").arraySize = 0;
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
			action.EnsureLocalAuthoringKeys();
			EditorUtility.SetDirty(action);
		}

		private static void EnsureWithdrawCurrencyFromChestActionAsset()
		{
			ActionDefinition action =
				AssetDatabase.LoadAssetAtPath<ActionDefinition>(TestWithdrawCurrencyFromChestActionPath);
			if (action == null)
			{
				action = ScriptableObject.CreateInstance<ActionDefinition>();
				AssetDatabase.CreateAsset(action, TestWithdrawCurrencyFromChestActionPath);
			}
			SerializedObject serializedAction = WriteCommonContentFields(
				action,
				FoundationTestSceneHarness.TestWithdrawCurrencyFromChestActionContentId,
				"取出钱箱货币",
				"单击存有 Coin 的钱箱，立即取出一张 Coin 卡。");
			RequireProperty(serializedAction, "m_turnCost").intValue = 0;
			RequireProperty(serializedAction, "m_canStartFromClick").boolValue = true;
			SerializedProperty slots = RequireProperty(serializedAction, "m_participationSlots");
			slots.arraySize = 1;
			WriteExactContentSlot(
				slots.GetArrayElementAtIndex(0),
				"chest",
				"钱箱",
				FoundationTestSceneHarness.TestChestContentId);

			SerializedProperty conditions = RequireProperty(serializedAction, "m_conditions");
			conditions.arraySize = 1;
			SerializedProperty storedCondition = conditions.GetArrayElementAtIndex(0);
			storedCondition.managedReferenceValue = new ChestHasStoredCurrencyCondition();
			RequireRelative(storedCondition, "m_chestSlotKey").stringValue = "chest";

			SerializedProperty intents = RequireProperty(serializedAction, "m_resultIntents");
			intents.arraySize = 1;
			SerializedProperty withdrawIntent = intents.GetArrayElementAtIndex(0);
			withdrawIntent.managedReferenceValue = new WithdrawCurrencyFromChestResultIntent();
			RequireRelative(withdrawIntent, "m_chestSlotKey").stringValue = "chest";
			RequireProperty(serializedAction, "m_resultBranches").arraySize = 0;
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
			action.EnsureLocalAuthoringKeys();
			EditorUtility.SetDirty(action);
		}

		private static void EnsureTestFoodAsset()
		{
			EnsureTestFoodAsset(
				TestFoodPath,
				FoundationTestSceneHarness.TestFoodContentId,
				"Berry",
				"Picked from the bush.",
				nutritionPerUse: 1,
				sellValue: 1);
		}

		private static void EnsureTestFoodAsset(
			string assetPath,
			string contentId,
			string displayName,
			string description,
			int nutritionPerUse,
			int sellValue)
		{
			EnsureFolder(TestContentFolder);
			CardDefinition existing = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
			if (existing != null && existing is not FoodCardDefinition)
			{
				AssetDatabase.DeleteAsset(assetPath);
				existing = null;
			}
			FoodCardDefinition food = existing as FoodCardDefinition;
			if (food == null)
			{
				food = ScriptableObject.CreateInstance<FoodCardDefinition>();
				AssetDatabase.CreateAsset(food, assetPath);
			}
			SerializedObject serializedFood = WriteCardFields(
				food,
				contentId,
				displayName,
				description,
				XTag.Faction_Player);
			RequireProperty(serializedFood, "m_initialUses").intValue = 1;
			RequireProperty(serializedFood, "m_sellValue").intValue = sellValue;
			RequireProperty(serializedFood, "m_nutritionPerUse").intValue = nutritionPerUse;
			RequireProperty(serializedFood, "m_countsTowardCardLimit").boolValue = true;
			serializedFood.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(food);
		}

		private static void WriteCardInstanceFields(
			string assetPath,
			int initialUses,
			int sellValue,
			bool countsTowardLimit)
		{
			CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
			if (card == null)
			{
				throw new MissingReferenceException($"测试卡牌不存在：{assetPath}");
			}
			SerializedObject serializedCard = new SerializedObject(card);
			RequireProperty(serializedCard, "m_initialUses").intValue = initialUses;
			RequireProperty(serializedCard, "m_sellValue").intValue = sellValue;
			RequireProperty(serializedCard, "m_countsTowardCardLimit").boolValue = countsTowardLimit;
			RequireProperty(serializedCard, "m_cardLimitBonus").intValue = 0;
			serializedCard.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(card);
		}

		private static void WritePeriodicProductionFields(
			string assetPath,
			string producedCardId,
			float intervalSeconds)
		{
			CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
			if (card == null)
			{
				throw new MissingReferenceException($"测试卡牌不存在：{assetPath}");
			}
			SerializedObject serializedCard = new SerializedObject(card);
			RequireProperty(serializedCard, "m_periodicProductionCardId")
				.FindPropertyRelative("m_value").stringValue = producedCardId;
			RequireProperty(serializedCard, "m_periodicProductionIntervalSeconds").floatValue = intervalSeconds;
			serializedCard.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(card);
		}

		private static void WriteAutomaticMovementFields(
			string assetPath,
			float intervalSeconds,
			float radius,
			int maxAttempts)
		{
			CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
			if (card == null)
			{
				throw new MissingReferenceException($"测试卡牌不存在：{assetPath}");
			}
			SerializedObject serializedCard = new SerializedObject(card);
			RequireProperty(serializedCard, "m_automaticMovementIntervalSeconds").floatValue = intervalSeconds;
			RequireProperty(serializedCard, "m_automaticMovementRadius").floatValue = radius;
			RequireProperty(serializedCard, "m_automaticMovementMaxAttempts").intValue = maxAttempts;
			serializedCard.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(card);
		}

		private static void WriteAutomaticHostileBehaviorFields(
			string assetPath,
			float aggroRadius,
			float attackRadius)
		{
			CharacterCardDefinition character = AssetDatabase.LoadAssetAtPath<CharacterCardDefinition>(assetPath);
			if (character == null)
			{
				throw new MissingReferenceException($"敌对角色卡不存在：{assetPath}");
			}
			SerializedObject serializedCard = new SerializedObject(character);
			RequireProperty(serializedCard, "m_automaticAggroRadius").floatValue = aggroRadius;
			RequireProperty(serializedCard, "m_automaticAttackRadius").floatValue = attackRadius;
			serializedCard.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(character);
		}

		private static void WriteCardLimitAndTradeFields(
			string assetPath,
			int sellValue,
			bool countsTowardLimit)
		{
			WriteCardInstanceFields(assetPath, 1, sellValue, countsTowardLimit);
		}

		private static void EnsureTestSellActionAsset()
		{
			ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(TestSellActionPath);
			if (action == null)
			{
				action = ScriptableObject.CreateInstance<ActionDefinition>();
				AssetDatabase.CreateAsset(action, TestSellActionPath);
			}

			SerializedObject serializedAction = WriteCommonContentFields(
				action,
				FoundationTestSceneHarness.TestSellActionContentId,
				"出售",
				"把可售卡拖到收购点，立即移除该卡并按出售价值生成货币。");
			RequireProperty(serializedAction, "m_turnCost").intValue = 0;
			SerializedProperty slots = RequireProperty(serializedAction, "m_participationSlots");
			slots.arraySize = 2;
			SerializedProperty soldSlot = slots.GetArrayElementAtIndex(0);
			WriteAnyContentSlot(
				soldSlot,
				"sold",
				"出售卡牌");
			RequireRelative(soldSlot, "m_maximumParticipants").intValue = 0;
			WriteExactContentSlot(
				slots.GetArrayElementAtIndex(1),
				"buyer",
				"收购点",
				FoundationTestSceneHarness.TestBuyerCardContentId);

			SerializedProperty conditions = RequireProperty(serializedAction, "m_conditions");
			conditions.arraySize = 1;
			SerializedProperty saleCondition = conditions.GetArrayElementAtIndex(0);
			saleCondition.managedReferenceValue = new CardSaleSourceAvailableCondition();
			RequireRelative(saleCondition, "m_soldSlotKey").stringValue = "sold";

			SerializedProperty intents = RequireProperty(serializedAction, "m_resultIntents");
			intents.arraySize = 1;
			SerializedProperty sellIntent = intents.GetArrayElementAtIndex(0);
			sellIntent.managedReferenceValue = new SellCardsResultIntent();
			RequireRelative(sellIntent, "m_soldSlotKey").stringValue = "sold";
			RequireRelative(sellIntent, "m_currencyCardId")
				.FindPropertyRelative("m_value").stringValue =
					FoundationTestSceneHarness.TestCurrencyCardContentId;
			RequireRelative(sellIntent, "m_anchorSlotKey").stringValue = "buyer";
			RequireProperty(serializedAction, "m_resultBranches").arraySize = 0;
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
			action.EnsureLocalAuthoringKeys();
			EditorUtility.SetDirty(action);
		}

		private static void WriteAnyContentSlot(
			SerializedProperty slot,
			string key,
			string displayName)
		{
			RequireRelative(slot, "m_key").stringValue = key;
			RequireRelative(slot, "m_displayName").stringValue = displayName;
			RequireRelative(slot, "m_minimumParticipants").intValue = 1;
			RequireRelative(slot, "m_maximumParticipants").intValue = 1;
			WriteContentIdArray(RequireRelative(slot, "m_allowedContentIds"));
			WriteIntArray(RequireRelative(slot, "m_requiredAllContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAnyContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredNoneContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAllAbilitySystemTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAnyAbilitySystemTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredNoneAbilitySystemTagCodes"));
		}

		private static void WriteExactContentSlot(
			SerializedProperty slot,
			string key,
			string displayName,
			string contentId)
		{
			RequireRelative(slot, "m_key").stringValue = key;
			RequireRelative(slot, "m_displayName").stringValue = displayName;
			RequireRelative(slot, "m_minimumParticipants").intValue = 1;
			RequireRelative(slot, "m_maximumParticipants").intValue = 1;
			WriteContentIdArray(RequireRelative(slot, "m_allowedContentIds"), contentId);
			WriteIntArray(RequireRelative(slot, "m_requiredAllContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAnyContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredNoneContentTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAllAbilitySystemTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredAnyAbilitySystemTagCodes"));
			WriteIntArray(RequireRelative(slot, "m_requiredNoneAbilitySystemTagCodes"));
		}

		private static void EnsureTestDayCycleScenarioAsset()
		{
			ScenarioDefinition scenario =
				AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(TestDayCycleScenarioPath);
			if (scenario == null)
			{
				scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
				AssetDatabase.CreateAsset(scenario, TestDayCycleScenarioPath);
			}

			SerializedObject serializedScenario = WriteCommonContentFields(
				scenario,
				FoundationTestSceneHarness.TestDayCycleScenarioContentId,
				"日终效果测试剧本",
				"用于在统一场景试玩进食、出售超限卡、遭遇反馈与跨日自动存档。");
			RequireProperty(serializedScenario, "m_initialRegionId")
				.FindPropertyRelative("m_value").stringValue = FoundationTestSceneHarness.TestRegionContentId;
			WriteContentIdArray(
				RequireProperty(serializedScenario, "m_regionIds"),
				FoundationTestSceneHarness.TestRegionContentId);
			WriteContentIdArray(RequireProperty(serializedScenario, "m_questIds"));
			RequireProperty(serializedScenario, "m_turnsPerDay").intValue = 1;
			RequireProperty(serializedScenario, "m_secondsPerTurn").floatValue = 0.35f;
			WriteTestBattleFormation(RequireProperty(serializedScenario, "m_battleFormationRules"));

			SerializedProperty dayCycle = RequireProperty(serializedScenario, "m_dayCycleRules");
			RequireRelative(dayCycle, "m_enabled").boolValue = true;
			RequireRelative(dayCycle, "m_hungerPerCharacter").intValue = 1;
			RequireRelative(dayCycle, "m_baseCardLimit").intValue = 3;
			RequireRelative(dayCycle, "m_feedingHealingEffectId").intValue = 2005;
			SerializedProperty encounters = RequireRelative(dayCycle, "m_encounters");
			encounters.arraySize = 1;
			SerializedProperty encounter = encounters.GetArrayElementAtIndex(0);
			RequireRelative(encounter, "m_key").stringValue = "night-visitor";
			RequireRelative(encounter, "m_notificationMessage").stringValue = "夜里传来了陌生脚步声。";
			RequireRelative(encounter, "m_cardId")
				.FindPropertyRelative("m_value").stringValue =
					FoundationTestSceneHarness.TestEncounterCardContentId;
			RequireRelative(encounter, "m_count").intValue = 1;
			RequireRelative(encounter, "m_oneTimeOnly").boolValue = true;
			RequireRelative(encounter, "m_minimumDay").intValue = 1;
			RequireRelative(encounter, "m_maximumDay").intValue = 99;
			RequireRelative(encounter, "m_interval").intValue = 0;
			RequireRelative(encounter, "m_priority").intValue = 10;
			RequireRelative(encounter, "m_chance").floatValue = 1f;
			RequireRelative(encounter, "m_maxCardsOnTabletop").intValue = 100;
			serializedScenario.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(scenario);
		}

		private static SerializedProperty RequireProperty(
			SerializedObject serializedObject,
			string propertyName)
		{
			SerializedProperty property = serializedObject.FindProperty(propertyName);
			if (property == null)
			{
				throw new MissingReferenceException(
					$"{serializedObject.targetObject.GetType().Name} 缺少作者字段 {propertyName}。");
			}
			return property;
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
            RequireProperty(serializedQuest, "m_journalGroupName").stringValue = "基础";
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

		private static void EnsureStackCraftParityQuestAsset()
		{
			QuestDefinition quest =
				AssetDatabase.LoadAssetAtPath<QuestDefinition>(TestStackCraftParityQuestPath);
			if (quest == null)
			{
				quest = ScriptableObject.CreateInstance<QuestDefinition>();
				AssetDatabase.CreateAsset(quest, TestStackCraftParityQuestPath);
			}

			SerializedObject serializedQuest = WriteCommonContentFields(
				quest,
				FoundationTestSceneHarness.TestStackCraftParityQuestContentId,
				"打开初始卡包",
				"多次点击初始卡包，完全打开并取得里面的所有卡牌。");
			RequireProperty(serializedQuest, "m_journalGroupName").stringValue = "入门";
			WriteContentIdArray(serializedQuest.FindProperty("m_prerequisiteQuestIds"));
			SerializedProperty tasks = RequireProperty(serializedQuest, "m_tasks");
			tasks.arraySize = 1;
			SerializedProperty task = tasks.GetArrayElementAtIndex(0);
			task.managedReferenceValue = new ActionCompletionQuestTaskDefinition();
			serializedQuest.ApplyModifiedPropertiesWithoutUndo();
			serializedQuest.Update();
			task = serializedQuest.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			RequireRelative(task, "m_actionId")
				.FindPropertyRelative("m_value").stringValue =
					FoundationTestSceneHarness.TestOpenCardPackActionContentId;
			RequireRelative(task, "m_requiredCompletionCount").intValue = 1;
			serializedQuest.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(quest);
		}

        private static void EnsureTestScenarioAssets()
        {
			SceneAsset foundationScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
			if (foundationScene == null)
			{
				throw new MissingReferenceException(
					$"统一地基测试剧本缺少牌桌场景资产：{ScenePath}");
			}

			EnsureTestRegionAsset(
				TestRegionPath,
				FoundationTestSceneHarness.TestRegionContentId,
				"地基测试地区",
				"统一牌桌测试使用的地区。",
				foundationScene.name);
			EnsureTestRegionAsset(
				TestBattleRegionPath,
				FoundationTestSceneHarness.TestBattleRegionContentId,
				"地基战斗测试地区",
				"阶段 B 组合验收中承接旅行角色与牌桌战斗的第二地区。",
				string.Empty);
			EnsureTestRegionAsset(
				TestStackCraftParityRegionPath,
				FoundationTestSceneHarness.TestStackCraftParityRegionContentId,
				"地基模板同态测试地区",
				"只用于验证模板开局初始卡包和开包玩家链的地区。",
				StackCraftParitySceneName);
			EnsureStackCraftParityQuestAsset();
            EnsureTestScenarioAsset(
                TestScenarioPath,
                FoundationTestSceneHarness.TestScenarioContentId,
                "地基测试剧本",
                "仅用于验证活动剧本统一拥有任务集合和世界回合生命周期。",
				FoundationTestSceneHarness.TestRegionContentId,
				FoundationTestSceneHarness.TestQuestContentId,
				FoundationTestSceneHarness.TestRegionContentId,
				FoundationTestSceneHarness.TestBattleRegionContentId);
			EnsureTestScenarioAsset(
				TestStackCraftParityScenarioPath,
				FoundationTestSceneHarness.TestStackCraftParityScenarioContentId,
				"地基模板同态测试剧本",
				"只用于验证新 Gameplay 框架复现模板默认初始卡包开局。",
				FoundationTestSceneHarness.TestStackCraftParityRegionContentId,
				FoundationTestSceneHarness.TestStackCraftParityQuestContentId,
				FoundationTestSceneHarness.TestStackCraftParityRegionContentId);
			WriteStackCraftParityScenarioStats(TestStackCraftParityScenarioPath);

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
				FoundationTestSceneHarness.TestQuestContentId,
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
			string questId,
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
                questId);
            WriteTestBattleFormation(
                serializedScenario.FindProperty("m_battleFormationRules"));
            serializedScenario.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(scenario);
        }

		private static void WriteStackCraftParityScenarioStats(string assetPath)
		{
			ScenarioDefinition scenario =
				AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(assetPath);
			if (scenario == null)
			{
				throw new MissingReferenceException($"StackCraft 同态测试剧本不存在，无法写入默认 HUD 统计：{assetPath}");
			}

			SerializedObject serializedScenario = new(scenario);
			RequireProperty(serializedScenario, "m_turnsPerDay").intValue = 1;
			RequireProperty(serializedScenario, "m_secondsPerTurn").floatValue =
				StackCraftReferenceDayDurationSeconds;
			SerializedProperty dayCycle = RequireProperty(serializedScenario, "m_dayCycleRules");
			RequireRelative(dayCycle, "m_hungerPerCharacter").intValue = StackCraftHungerPerCharacter;
			RequireRelative(dayCycle, "m_baseCardLimit").intValue = StackCraftBaseCardLimit;
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
            SerializedProperty cardMargin = placement.FindPropertyRelative("m_cardMargin");
            SerializedProperty stackStep = placement.FindPropertyRelative("m_stackStep");
            SerializedProperty overlapResolveMaxIterations =
                placement.FindPropertyRelative("m_overlapResolveMaxIterations");
            SerializedProperty spawnAttachRadius =
                placement.FindPropertyRelative("m_spawnAttachRadius");
            SerializedProperty cardLimitBonusExpansionPerPoint =
                placement.FindPropertyRelative("m_cardLimitBonusExpansionPerPoint");
            if (bounds == null || restrictedAreas == null || cardSize == null || cardMargin == null ||
                stackStep == null || overlapResolveMaxIterations == null || spawnAttachRadius == null ||
                cardLimitBonusExpansionPerPoint == null)
            {
                throw new MissingReferenceException("牌桌放置作者字段已变更，测试剧本生成器需要同步更新。");
            }

            bounds.rectValue = StackCraftBoardPlacementBounds;
            restrictedAreas.arraySize = 1;
            restrictedAreas.GetArrayElementAtIndex(0).rectValue = StackCraftBoardHeaderRestrictedArea;
            cardSize.vector2Value = StackCraftBoardCardSize;
            cardMargin.vector2Value = StackCraftBoardCardMargin;
            stackStep.vector2Value = StackCraftBoardStackStep;
            overlapResolveMaxIterations.intValue = StackCraftBoardOverlapResolveMaxIterations;
            spawnAttachRadius.floatValue = StackCraftBoardSpawnAttachRadius;
            cardLimitBonusExpansionPerPoint.vector2Value = StackCraftBoardCardLimitBonusExpansionPerPoint;
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
			EnsureFolder(TabletopPrefabFolder);
			EnsureFolder(GameplayUiPrefabFolder);
            Sprite cardSprite = LoadRequiredSprite(TabletopCardArtPath, "项目卡牌占位图");
			Sprite hitNormalSprite = LoadRequiredSprite(HitNormalSpritePath, "项目普通命中图标");
			Sprite hitMissSprite = LoadRequiredSprite(HitMissSpritePath, "项目未命中图标");
			Sprite hitCriticalSprite = LoadRequiredSprite(HitCriticalSpritePath, "项目暴击图标");
			Sprite advantageSprite = LoadRequiredSprite(AdvantageSpritePath, "项目优势图标");
			Sprite disadvantageSprite = LoadRequiredSprite(DisadvantageSpritePath, "项目劣势图标");
			Sprite arrowProjectileSprite = LoadRequiredSprite(
				ArrowProjectileSpritePath,
				"项目箭矢投射物图片");
			Sprite magicProjectileSprite = LoadRequiredSprite(
				MagicProjectileSpritePath,
				"项目魔法投射物图片");

            EnsureTabletopCardViewPrefab(cardSprite);
            EnsureTabletopActionProgressViewPrefab();
			EnsureTabletopBattleAreaViewPrefab(cardSprite);
			EnsureTabletopProjectileViewPrefab(arrowProjectileSprite, magicProjectileSprite);
			EnsureTabletopHitResultViewPrefab(
				hitMissSprite,
				hitNormalSprite,
				hitCriticalSprite,
				advantageSprite,
				disadvantageSprite);
			EnsureTabletopCardSmokeEffectViewPrefab();
			EnsureTestPanelFont();
            EnsureTabletopActionChoicePanelPrefab();
			EnsureTabletopActionPlanPanelPrefab();
			EnsureScenarioTurnPanelPrefab();
			EnsureScenarioJournalPanelPrefab();
			EnsureScenarioPausePanelPrefab();
			EnsureSettingsPanelPrefab();
			EnsureScenarioSavePanelPrefab();
			EnsureGameUiPrefab();
			EnsureConfirmationDialogPanelPrefab();
			EnsureTabletopCardInfoPanelPrefab();
			AudioClipResolver cardPickAudio = EnsureAudioResolver(
				CardPickAudioPath,
				"拿起卡牌音效",
				CardPickClipPath);
			AudioClipResolver cardDropAudio = EnsureAudioResolver(
				CardDropAudioPath,
				"放下卡牌音效",
				CardDropClipPath);
			AudioClipResolver cardSwipeAudio = EnsureAudioResolver(
				CardSwipeAudioPath,
				"卡牌滑动音效",
				CardSwipeClipPath);
			AudioClipResolver eatAudio = EnsureAudioResolver(
				EatAudioPath,
				"进食音效",
				EatClipPath);
			AudioClipResolver popAudio = EnsureAudioResolver(
				PopAudioPath,
				"生成完成音效",
				PopClipPath);
			AudioClipResolver cardSmokeAudio = EnsureAudioResolver(
				CardSmokeAudioPath,
				"卡牌烟雾反馈音效",
				GameplayCardSmokeClipPath);
			AudioClipResolver coinAudio = EnsureAudioResolver(
				CoinAudioPath,
				"单枚货币音效",
				CoinClipPath);
			AudioClipResolver coinsAudio = EnsureAudioResolver(
				CoinsAudioPath,
				"多枚货币音效",
				CoinsClipPath);
			AudioClipResolver cashRegisterAudio = EnsureAudioResolver(
				CashRegisterAudioPath,
				"购买成交音效",
				CashRegisterClipPath);
			AudioClipResolver meleeAttackAudio = EnsureAudioResolver(
				MeleeAttackAudioPath,
				"近战起手音效",
				AttackMeleeClipPath);
			AudioClipResolver rangedAttackAudio = EnsureAudioResolver(
				RangedAttackAudioPath,
				"远程起手音效",
				AttackRangedClipPath);
			AudioClipResolver magicAttackAudio = EnsureAudioResolver(
				MagicAttackAudioPath,
				"魔法起手音效",
				AttackMagicClipPath);
			AudioClipResolver meleeHitAudio = EnsureAudioResolver(
				MeleeHitAudioPath,
				"近战命中音效",
				HitMeleeClipPath);
			AudioClipResolver rangedHitAudio = EnsureAudioResolver(
				RangedHitAudioPath,
				"远程命中音效",
				HitRangedClipPath);
			AudioClipResolver magicHitAudio = EnsureAudioResolver(
				MagicHitAudioPath,
				"魔法命中音效",
				HitMagicClipPath);
			AudioClipResolver missAudio = EnsureAudioResolver(
				MissAudioPath,
				"未命中音效",
				MissClipPath);
			AudioClipResolver criticalAudio = EnsureAudioResolver(
				CriticalAudioPath,
				"暴击音效",
				CriticalClipPath);
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
			SerializedProperty battleAreaPrefabReference =
				serializedSettings.FindProperty("m_battleAreaViewPrefab");
			SerializedProperty battleAreaPrefabAddress =
				battleAreaPrefabReference?.FindPropertyRelative("Address");
			SerializedProperty projectilePrefabReference =
				serializedSettings.FindProperty("m_projectileViewPrefab");
			SerializedProperty projectilePrefabAddress =
				projectilePrefabReference?.FindPropertyRelative("Address");
			SerializedProperty cardSmokeEffectPrefabReference =
				serializedSettings.FindProperty("m_cardSmokeEffectPrefab");
			SerializedProperty cardSmokeEffectPrefabAddress =
				cardSmokeEffectPrefabReference?.FindPropertyRelative("Address");
			SerializedProperty hitResultPrefabReference =
				serializedSettings.FindProperty("m_hitResultViewPrefab");
			SerializedProperty hitResultPrefabAddress =
				hitResultPrefabReference?.FindPropertyRelative("Address");
            SerializedProperty stackHeightStep = serializedSettings.FindProperty("m_stackHeightStep");
            SerializedProperty baseSortingOrder = serializedSettings.FindProperty("m_baseSortingOrder");
            SerializedProperty battleBaseSortingOrder = serializedSettings.FindProperty("m_battleBaseSortingOrder");
			SerializedProperty projectileSortingOrder = serializedSettings.FindProperty("m_projectileSortingOrder");
            SerializedProperty cardSmokeSortingOrder = serializedSettings.FindProperty("m_cardSmokeSortingOrder");
			SerializedProperty hitResultSortingOrder = serializedSettings.FindProperty("m_hitResultSortingOrder");
            SerializedProperty dragFollowSharpness = serializedSettings.FindProperty("m_dragFollowSharpness");
            SerializedProperty clickThreshold = serializedSettings.FindProperty("m_clickThreshold");
            SerializedProperty dragHeight = serializedSettings.FindProperty("m_dragHeight");
            SerializedProperty moveDurationSeconds = serializedSettings.FindProperty("m_moveDurationSeconds");
            if (prefabAddress == null || actionProgressPrefabAddress == null ||
				battleAreaPrefabAddress == null ||
				projectilePrefabAddress == null ||
				cardSmokeEffectPrefabAddress == null ||
				hitResultPrefabAddress == null ||
                stackHeightStep == null ||
                baseSortingOrder == null || battleBaseSortingOrder == null ||
				projectileSortingOrder == null ||
				cardSmokeSortingOrder == null ||
				hitResultSortingOrder == null ||
                dragFollowSharpness == null ||
                clickThreshold == null ||
                dragHeight == null ||
                moveDurationSeconds == null)
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

			battleAreaPrefabAddress.stringValue = TabletopBattleAreaViewAddress;
			SerializedProperty battleAreaPrefabGuid =
				battleAreaPrefabReference.FindPropertyRelative("Guid");
			SerializedProperty battleAreaPrefabLocked =
				battleAreaPrefabReference.FindPropertyRelative("Locked");
			if (battleAreaPrefabGuid != null)
			{
				battleAreaPrefabGuid.stringValue =
					AssetDatabase.AssetPathToGUID(TabletopBattleAreaViewPrefabPath);
			}
			if (battleAreaPrefabLocked != null)
			{
				battleAreaPrefabLocked.boolValue = true;
			}

			projectilePrefabAddress.stringValue = TabletopProjectileViewAddress;
			SerializedProperty projectilePrefabGuid =
				projectilePrefabReference.FindPropertyRelative("Guid");
			SerializedProperty projectilePrefabLocked =
				projectilePrefabReference.FindPropertyRelative("Locked");
			if (projectilePrefabGuid != null)
			{
				projectilePrefabGuid.stringValue =
					AssetDatabase.AssetPathToGUID(TabletopProjectileViewPrefabPath);
			}
			if (projectilePrefabLocked != null)
			{
				projectilePrefabLocked.boolValue = true;
			}

			cardSmokeEffectPrefabAddress.stringValue = GameplayCardSmokeEffectAddress;
			SerializedProperty cardSmokeEffectPrefabGuid =
				cardSmokeEffectPrefabReference.FindPropertyRelative("Guid");
			SerializedProperty cardSmokeEffectPrefabLocked =
				cardSmokeEffectPrefabReference.FindPropertyRelative("Locked");
			if (cardSmokeEffectPrefabGuid != null)
			{
				cardSmokeEffectPrefabGuid.stringValue =
					AssetDatabase.AssetPathToGUID(GameplayCardSmokeEffectPrefabPath);
			}
			if (cardSmokeEffectPrefabLocked != null)
			{
				cardSmokeEffectPrefabLocked.boolValue = true;
			}

			hitResultPrefabAddress.stringValue = TabletopHitResultViewAddress;
			SerializedProperty hitResultPrefabGuid =
				hitResultPrefabReference.FindPropertyRelative("Guid");
			SerializedProperty hitResultPrefabLocked =
				hitResultPrefabReference.FindPropertyRelative("Locked");
			if (hitResultPrefabGuid != null)
			{
				hitResultPrefabGuid.stringValue =
					AssetDatabase.AssetPathToGUID(TabletopHitResultViewPrefabPath);
			}
			if (hitResultPrefabLocked != null)
			{
				hitResultPrefabLocked.boolValue = true;
			}

			RequireProperty(serializedSettings, "m_cardPickAudio").objectReferenceValue = cardPickAudio;
			RequireProperty(serializedSettings, "m_cardDropAudio").objectReferenceValue = cardDropAudio;
			RequireProperty(serializedSettings, "m_cardSwipeAudio").objectReferenceValue = cardSwipeAudio;
			RequireProperty(serializedSettings, "m_eatAudio").objectReferenceValue = eatAudio;
			RequireProperty(serializedSettings, "m_popAudio").objectReferenceValue = popAudio;
			RequireProperty(serializedSettings, "m_cardSmokeAudio").objectReferenceValue = cardSmokeAudio;
			RequireProperty(serializedSettings, "m_coinAudio").objectReferenceValue = coinAudio;
			RequireProperty(serializedSettings, "m_coinsAudio").objectReferenceValue = coinsAudio;
			RequireProperty(serializedSettings, "m_cashRegisterAudio").objectReferenceValue = cashRegisterAudio;
			RequireProperty(serializedSettings, "m_meleeAttackAudio").objectReferenceValue = meleeAttackAudio;
			RequireProperty(serializedSettings, "m_rangedAttackAudio").objectReferenceValue = rangedAttackAudio;
			RequireProperty(serializedSettings, "m_magicAttackAudio").objectReferenceValue = magicAttackAudio;
			RequireProperty(serializedSettings, "m_meleeHitAudio").objectReferenceValue = meleeHitAudio;
			RequireProperty(serializedSettings, "m_rangedHitAudio").objectReferenceValue = rangedHitAudio;
			RequireProperty(serializedSettings, "m_magicHitAudio").objectReferenceValue = magicHitAudio;
			RequireProperty(serializedSettings, "m_missAudio").objectReferenceValue = missAudio;
			RequireProperty(serializedSettings, "m_criticalAudio").objectReferenceValue = criticalAudio;

            stackHeightStep.floatValue = 0.002f;
            baseSortingOrder.intValue = 10;
            battleBaseSortingOrder.intValue = 100;
			projectileSortingOrder.intValue = 140;
			cardSmokeSortingOrder.intValue = 150;
			hitResultSortingOrder.intValue = 160;
            dragFollowSharpness.floatValue = 100f;
            clickThreshold.floatValue = 0.02f;
            dragHeight.floatValue = 0.1f;
            moveDurationSeconds.floatValue = 0.1f;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { TabletopViewSettingsPath });
            return LoadPersistentTabletopViewSettings();
        }

		private static TabletopViewSettings LoadPersistentTabletopViewSettings()
		{
			AssetDatabase.ImportAsset(TabletopViewSettingsPath, ImportAssetOptions.ForceSynchronousImport);
			TabletopViewSettings settings =
				AssetDatabase.LoadMainAssetAtPath(TabletopViewSettingsPath) as TabletopViewSettings ??
				AssetDatabase.LoadAssetAtPath<TabletopViewSettings>(TabletopViewSettingsPath);
			if (settings == null)
			{
				throw new MissingReferenceException(
					$"无法从 AssetDatabase 重新载入牌桌测试视图设置：{TabletopViewSettingsPath}");
			}

			if (!EditorUtility.IsPersistent(settings) ||
				!string.Equals(
					AssetDatabase.GetAssetPath(settings),
					TabletopViewSettingsPath,
					StringComparison.Ordinal))
			{
				throw new MissingReferenceException(
					$"牌桌测试视图设置没有从正式作者资源载入：{TabletopViewSettingsPath}");
			}

			return settings;
		}

		private static Sprite LoadRequiredSprite(string assetPath, string description)
		{
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
			if (sprite == null)
			{
				throw new MissingReferenceException($"缺少 {description}：{assetPath}");
			}

			return sprite;
		}

		private static Mesh LoadRequiredMesh(string assetPath, string description)
		{
			Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(assetPath)
				.OfType<Mesh>()
				.FirstOrDefault(candidate =>
					candidate != null &&
					candidate.name.IndexOf("__preview", StringComparison.OrdinalIgnoreCase) < 0);
			if (mesh == null)
			{
				throw new MissingReferenceException($"缺少 {description}：{assetPath}");
			}

			return mesh;
		}

		private static Material LoadRequiredMaterial(string assetPath, string description)
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
			if (material == null)
			{
				material = AssetDatabase.LoadAllAssetsAtPath(assetPath)
					.OfType<Material>()
					.FirstOrDefault(candidate => candidate != null);
			}
			if (material == null)
			{
				throw new MissingReferenceException($"缺少 {description}：{assetPath}");
			}

			return material;
		}

		private static void CreateStackCraftBoardSurface(Transform parent)
		{
			CreateStackCraftBoardSurface(
				parent,
				BoardBodyMaterialPath,
				BoardHeaderMaterialPath,
				"StackCraft Board01 主体材质自有副本",
				"StackCraft Board01 页眉材质自有副本");
		}

		private static void CreateStackCraftBoardSurface(
			Transform parent,
			string bodyMaterialPath,
			string headerMaterialPath,
			string bodyMaterialDescription,
			string headerMaterialDescription)
		{
			Mesh boardMesh = LoadRequiredMesh(BoardMeshPath, "StackCraft Board.fbx 自有副本");
			Material bodyMaterial = LoadRequiredMaterial(
				bodyMaterialPath,
				bodyMaterialDescription);
			Material headerMaterial = LoadRequiredMaterial(
				headerMaterialPath,
				headerMaterialDescription);

			GameObject boardObject = new("牌桌底板");
			boardObject.transform.SetParent(parent, false);
			// 新框架中 Board 规则边界由牌桌/地区作者源承载；这里的 Board mesh 只做视觉底板，必须低于 Y=0 的卡牌交互平面。
			boardObject.transform.localPosition = new Vector3(0f, StackCraftBoardSurfaceHeight, 0f);
			boardObject.transform.localRotation = Quaternion.identity;
			boardObject.transform.localScale = Vector3.one;

			SkinnedMeshRenderer renderer = boardObject.AddComponent<SkinnedMeshRenderer>();
			renderer.sharedMesh = boardMesh;
			renderer.sharedMaterials = new[] { bodyMaterial, headerMaterial };
			renderer.shadowCastingMode = ShadowCastingMode.On;
			renderer.receiveShadows = true;
			renderer.updateWhenOffscreen = true;
			renderer.localBounds = new Bounds(Vector3.zero, StackCraftBoardLocalBoundsSize);
		}

		private static void CreateStackCraftBackgroundSurface(Transform parent)
		{
			CreateStackCraftBackgroundSurface(
				parent,
				GrassBackgroundMaterialPath,
				"StackCraft Main 草地背景材质自有副本");
		}

		private static void CreateStackCraftBackgroundSurface(
			Transform parent,
			string materialPath,
			string materialDescription)
		{
			Material backgroundMaterial = LoadRequiredMaterial(
				materialPath,
				materialDescription);

			GameObject backgroundObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
			backgroundObject.name = "桌面背景";
			backgroundObject.transform.SetParent(parent, false);
			backgroundObject.transform.localPosition = new Vector3(0f, -10f, 0f);
			backgroundObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
			backgroundObject.transform.localScale = new Vector3(10f, 1f, 10f);
			ApplyStackCraftBackgroundTransform(backgroundObject.transform);

			Collider collider = backgroundObject.GetComponent<Collider>();
			if (collider != null)
			{
				Object.DestroyImmediate(collider);
			}

			MeshRenderer renderer = backgroundObject.GetComponent<MeshRenderer>();
			renderer.sharedMaterial = backgroundMaterial;
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			renderer.receiveShadows = false;
			renderer.lightProbeUsage = LightProbeUsage.Off;
			renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
		}

		private static void ApplyStackCraftBackgroundTransform(Transform transform)
		{
			transform.localRotation = new Quaternion(0f, 1f, 0f, 0f);

			// 背景是 StackCraft 场景级对账对象，四元数和欧拉提示都要按原 YAML 精确落盘。
			SerializedObject serializedTransform = new(transform);
			SerializedProperty localRotation = RequireProperty(serializedTransform, "m_LocalRotation");
			RequireRelative(localRotation, "x").floatValue = 0f;
			RequireRelative(localRotation, "y").floatValue = 1f;
			RequireRelative(localRotation, "z").floatValue = 0f;
			RequireRelative(localRotation, "w").floatValue = 0f;
			RequireProperty(serializedTransform, "m_LocalEulerAnglesHint").vector3Value =
				new Vector3(0f, 180f, 0f);
			serializedTransform.ApplyModifiedPropertiesWithoutUndo();
		}

		private static AudioClipResolver EnsureAudioResolver(
			string assetPath,
			string displayName,
			string clipPath,
			EAudioChannel targetChannel = EAudioChannel.GameplaySoundFX)
		{
			EnsureFolder(TabletopAudioFolder);
			AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath) ??
				throw new MissingReferenceException($"缺少项目音效素材：{clipPath}");
			AudioClipResolver resolver = AssetDatabase.LoadAssetAtPath<AudioClipResolver>(assetPath);
			if (resolver == null)
			{
				resolver = ScriptableObject.CreateInstance<AudioClipResolver>();
				AssetDatabase.CreateAsset(resolver, assetPath);
			}

			resolver.name = displayName;
			SerializedObject serializedResolver = new(resolver);
			SerializedProperty audioClips = RequireProperty(serializedResolver, "m_audioClips");
			audioClips.arraySize = 1;
			audioClips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
			RequireProperty(serializedResolver, "m_targetChannel").enumValueIndex = (int)targetChannel;
			RequireProperty(serializedResolver, "m_resolvingAlgorithm").enumValueIndex =
				(int)EAudioClipResolvingAlgorithm.First;
			serializedResolver.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(resolver);
			return resolver;
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

		private static TMP_FontAsset LoadStackCraftSurfaceFont()
		{
			TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
				StackCraftSurfaceFontPath);
			if (fontAsset == null)
			{
				throw new MissingReferenceException(
					$"缺少 StackCraft 卡牌 / 命中 UI 字体来源：{StackCraftSurfaceFontPath}");
			}

			return fontAsset;
		}

		private static void EnsureStackCraftSurfaceFontFallback(
			TMP_FontAsset stackCraftFont,
			TMP_FontAsset chineseFallbackFont)
		{
			if (stackCraftFont == null)
			{
				throw new MissingReferenceException("StackCraft 卡牌主字体为空，无法生成卡牌视图。");
			}
			if (chineseFallbackFont == null)
			{
				throw new MissingReferenceException("卡牌中文 fallback 字体为空，无法保证中文卡牌内容可显示。");
			}

			stackCraftFont.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
			if (stackCraftFont.fallbackFontAssetTable.Contains(chineseFallbackFont))
			{
				return;
			}

			stackCraftFont.fallbackFontAssetTable.Add(chineseFallbackFont);
			EditorUtility.SetDirty(stackCraftFont);
			AssetDatabase.SaveAssets();
		}

        private static void EnsureTabletopCardViewPrefab(Sprite cardSprite)
        {
			Mesh cardMesh = LoadRequiredMesh(CardMeshPath, "StackCraft 卡牌 FBX 自有副本");
			Mesh packMesh = LoadRequiredMesh(PackMeshPath, "StackCraft Pack.fbx 自有副本");
			Mesh equipmentPanelMesh = LoadRequiredMesh(
				EquipmentPanelMeshPath,
				"StackCraft EquipmentPanel.fbx 自有副本");
			Material defaultSurfaceMaterial = LoadRequiredMaterial(
				CharacterCardSurfacePath,
				"StackCraft 角色卡面材质自有副本");
			Material equipmentPanelMaterial = LoadRequiredMaterial(
				EquipmentPanelMaterialPath,
				"StackCraft EquipmentPanel 材质自有副本");
			Material outlineMaterial = LoadRequiredMaterial(
				CardOutlineShaderPath,
				"StackCraft 卡牌轮廓材质自有副本");
			Material cardBuyerCurrencyIconMaterial = LoadRequiredMaterial(
				CardBuyerCurrencyIconMaterialPath,
				"StackCraft CardBuyer 货币图标材质自有副本");
			TMP_FontAsset cardFont = LoadStackCraftSurfaceFont();
			EnsureStackCraftSurfaceFontFallback(cardFont, EnsureTestPanelFont());
			TMP_FontAsset previousDefaultFont = TMP_Settings.instance == null
				? null
				: TMP_Settings.defaultFontAsset;
			bool replacedDefaultFont = TMP_Settings.instance != null;
			if (replacedDefaultFont)
			{
				TMP_Settings.defaultFontAsset = cardFont;
			}
                GameObject root = new("卡牌视图");
            try
            {
                root.transform.localScale = Vector3.one;
				MeshFilter meshFilter = root.AddComponent<MeshFilter>();
				meshFilter.sharedMesh = cardMesh;
				MeshRenderer surfaceRenderer = root.AddComponent<MeshRenderer>();
				surfaceRenderer.sharedMaterial = defaultSurfaceMaterial;

                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.8f, 0f, 1.0000002f);

				GameObject cardBuyerCurrencyIcon = GameObject.CreatePrimitive(PrimitiveType.Quad);
				cardBuyerCurrencyIcon.name = "收购货币图标";
				cardBuyerCurrencyIcon.transform.SetParent(root.transform, false);
				SetStackCraftFaceTransform(
					cardBuyerCurrencyIcon.transform,
					new Vector3(0f, 0.001f, 0.15f));
				cardBuyerCurrencyIcon.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
				if (cardBuyerCurrencyIcon.TryGetComponent(out Collider iconCollider))
				{
					Object.DestroyImmediate(iconCollider);
				}
				MeshRenderer cardBuyerCurrencyIconRenderer =
					cardBuyerCurrencyIcon.GetComponent<MeshRenderer>();
				cardBuyerCurrencyIconRenderer.sharedMaterial = cardBuyerCurrencyIconMaterial;
				cardBuyerCurrencyIconRenderer.shadowCastingMode = ShadowCastingMode.Off;
				cardBuyerCurrencyIconRenderer.receiveShadows = false;
				cardBuyerCurrencyIconRenderer.lightProbeUsage = LightProbeUsage.Off;
				cardBuyerCurrencyIconRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
				cardBuyerCurrencyIcon.SetActive(false);

				GameObject titleTextObject = new("标题");
				titleTextObject.transform.SetParent(root.transform, false);
				SetStackCraftFaceTransform(titleTextObject.transform, new Vector3(0f, 0f, 0.4f));
				TextMeshPro titleLabel = titleTextObject.AddComponent<TextMeshPro>();
				titleLabel.font = cardFont;
				titleLabel.fontSize = 1.2f;
				titleLabel.fontSizeMin = 0.4f;
				titleLabel.fontSizeMax = 1.2f;
				titleLabel.enableAutoSizing = true;
				titleLabel.fontStyle = FontStyles.Normal;
				titleLabel.color = Color.white;
				titleLabel.text = string.Empty;
				titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
				titleLabel.overflowMode = TextOverflowModes.Overflow;
				titleLabel.rectTransform.sizeDelta = new Vector2(0.8f, 0.2f);
				titleLabel.rectTransform.anchoredPosition = new Vector2(0f, 0.001f);
				ApplyStackCraftTextParameters(
					titleLabel,
					fontSizeBase: 1f,
					HorizontalAlignmentOptions.Left,
					VerticalAlignmentOptions.Middle,
					new Vector4(0.05f, 0.025f, 0.05f, 0.025f));

				GameObject priceTextObject = new("价格");
				priceTextObject.transform.SetParent(root.transform, false);
				SetStackCraftFaceTransform(priceTextObject.transform, new Vector3(0f, 0f, -0.355f));
				TextMeshPro priceLabel = priceTextObject.AddComponent<TextMeshPro>();
				priceLabel.font = cardFont;
				priceLabel.fontSize = 1.5f;
				priceLabel.fontSizeMin = 0.5f;
				priceLabel.fontSizeMax = 1.5f;
				priceLabel.enableAutoSizing = true;
				priceLabel.fontStyle = FontStyles.Normal;
				priceLabel.color = Color.white;
				priceLabel.text = string.Empty;
				priceLabel.textWrappingMode = TextWrappingModes.NoWrap;
				priceLabel.overflowMode = TextOverflowModes.Overflow;
				priceLabel.rectTransform.sizeDelta = new Vector2(0.12f, 0.2f);
				priceLabel.rectTransform.anchoredPosition = new Vector2(-0.25f, 0.001f);
				ApplyStackCraftTextParameters(
					priceLabel,
					fontSizeBase: 1.5f,
					HorizontalAlignmentOptions.Geometry,
					VerticalAlignmentOptions.Geometry,
					Vector4.zero);

				GameObject nutritionTextObject = new("营养");
				nutritionTextObject.transform.SetParent(root.transform, false);
				SetStackCraftFaceTransform(nutritionTextObject.transform, new Vector3(0f, 0f, -0.363f));
				TextMeshPro nutritionLabel = nutritionTextObject.AddComponent<TextMeshPro>();
				nutritionLabel.font = cardFont;
				nutritionLabel.fontSize = 1.5f;
				nutritionLabel.fontSizeMin = 0.5f;
				nutritionLabel.fontSizeMax = 1.5f;
				nutritionLabel.enableAutoSizing = true;
				nutritionLabel.fontStyle = FontStyles.Normal;
				nutritionLabel.color = Color.white;
				nutritionLabel.text = string.Empty;
				nutritionLabel.textWrappingMode = TextWrappingModes.NoWrap;
				nutritionLabel.overflowMode = TextOverflowModes.Overflow;
				nutritionLabel.rectTransform.sizeDelta = new Vector2(0.14f, 0.2f);
				nutritionLabel.rectTransform.anchoredPosition = new Vector2(0.258f, 0.001f);
				ApplyStackCraftTextParameters(
					nutritionLabel,
					fontSizeBase: 1.5f,
					HorizontalAlignmentOptions.Geometry,
					VerticalAlignmentOptions.Geometry,
					Vector4.zero);

                GameObject highlightRoot = new("候选高亮");
                highlightRoot.transform.SetParent(root.transform, false);
				highlightRoot.transform.localPosition = Vector3.zero;
				highlightRoot.transform.localRotation = Quaternion.identity;
				highlightRoot.transform.localScale = Vector3.one;
				MeshFilter highlightMeshFilter = highlightRoot.AddComponent<MeshFilter>();
				highlightMeshFilter.sharedMesh = cardMesh;
				MeshRenderer highlightRenderer = highlightRoot.AddComponent<MeshRenderer>();
				highlightRenderer.sharedMaterial = outlineMaterial;
				highlightRenderer.shadowCastingMode = ShadowCastingMode.Off;
				highlightRenderer.receiveShadows = false;
                highlightRoot.SetActive(false);

				GameObject characterStatusRoot = new("角色状态");
				characterStatusRoot.transform.SetParent(root.transform, false);
				characterStatusRoot.transform.localPosition = Vector3.zero;

				GameObject equipmentPanelObject = new("装备面板");
				equipmentPanelObject.transform.SetParent(characterStatusRoot.transform, false);
				equipmentPanelObject.transform.localPosition = new Vector3(0f, 0.001f, -0.525f);
				equipmentPanelObject.transform.localRotation = Quaternion.identity;
				equipmentPanelObject.transform.localScale = Vector3.one;
				MeshFilter equipmentPanelMeshFilter = equipmentPanelObject.AddComponent<MeshFilter>();
				equipmentPanelMeshFilter.sharedMesh = equipmentPanelMesh;
				MeshRenderer equipmentPanelRenderer = equipmentPanelObject.AddComponent<MeshRenderer>();
				equipmentPanelRenderer.sharedMaterial = equipmentPanelMaterial;
				equipmentPanelRenderer.shadowCastingMode = ShadowCastingMode.Off;
				equipmentPanelRenderer.receiveShadows = false;
				BoxCollider equipmentPanelCollider = equipmentPanelObject.AddComponent<BoxCollider>();
				equipmentPanelCollider.size = new Vector3(0.45f, 0f, 0.15000002f);

				GameObject healthTextObject = new("生命");
				healthTextObject.transform.SetParent(characterStatusRoot.transform, false);
				SetStackCraftFaceTransform(healthTextObject.transform, new Vector3(0f, 0f, -0.345f));
				TextMeshPro healthLabel = healthTextObject.AddComponent<TextMeshPro>();
				healthLabel.font = cardFont;
				healthLabel.fontSize = 1.25f;
				healthLabel.fontSizeMin = 0.5f;
				healthLabel.fontSizeMax = 1.5f;
				healthLabel.enableAutoSizing = true;
				healthLabel.color = Color.white;
				healthLabel.text = string.Empty;
				healthLabel.textWrappingMode = TextWrappingModes.NoWrap;
				healthLabel.overflowMode = TextOverflowModes.Overflow;
				healthLabel.rectTransform.sizeDelta = new Vector2(0.14f, 0.2f);
				healthLabel.rectTransform.anchoredPosition = new Vector2(0.254f, 0.001f);
				ApplyStackCraftTextParameters(
					healthLabel,
					fontSizeBase: 1.5f,
					HorizontalAlignmentOptions.Geometry,
					VerticalAlignmentOptions.Geometry,
					Vector4.zero);
				characterStatusRoot.SetActive(false);

                TabletopCardView cardView = root.AddComponent<TabletopCardView>();
                SerializedObject serializedView = new(cardView);
				serializedView.FindProperty("m_surfaceRenderer").objectReferenceValue = surfaceRenderer;
				serializedView.FindProperty("m_surfaceMeshFilter").objectReferenceValue = meshFilter;
				serializedView.FindProperty("m_defaultSurfaceMesh").objectReferenceValue = cardMesh;
				serializedView.FindProperty("m_packSurfaceMesh").objectReferenceValue = packMesh;
				serializedView.FindProperty("m_highlightMeshFilter").objectReferenceValue = highlightMeshFilter;
				serializedView.FindProperty("m_surfaceTextureProperty").stringValue = "_OverlayTex";
				serializedView.FindProperty("m_surfaceFlashProperty").stringValue = "_FlashAmount";
                serializedView.FindProperty("m_highlightRoot").objectReferenceValue = highlightRoot;
				serializedView.FindProperty("m_cardBuyerCurrencyIconRenderer").objectReferenceValue =
					cardBuyerCurrencyIconRenderer;
				serializedView.FindProperty("m_cardBuyerCurrencyTextureProperty").stringValue = "_MainTex";
				serializedView.FindProperty("m_titleLabel").objectReferenceValue = titleLabel;
				serializedView.FindProperty("m_priceLabel").objectReferenceValue = priceLabel;
				serializedView.FindProperty("m_nutritionLabel").objectReferenceValue = nutritionLabel;
				serializedView.FindProperty("m_characterStatusRoot").objectReferenceValue = characterStatusRoot;
				serializedView.FindProperty("m_healthLabel").objectReferenceValue = healthLabel;
				serializedView.FindProperty("m_hurtFlashDelaySeconds").floatValue = 0.05f;
				serializedView.FindProperty("m_hurtFlashTweenSeconds").floatValue = 0.1f;
				serializedView.FindProperty("m_hurtFlashLoopCount").intValue = 2;
				serializedView.FindProperty("m_hurtPunchRotationDegrees").floatValue = 15f;
				serializedView.FindProperty("m_hurtPunchDurationSeconds").floatValue = 0.25f;
				serializedView.FindProperty("m_hurtPunchVibrato").intValue = 25;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(root, TabletopCardViewPrefabPath) == null)
                {
                    throw new MissingReferenceException($"无法保存卡牌视图：{TabletopCardViewPrefabPath}");
                }
            }
            finally
            {
				if (replacedDefaultFont)
				{
					TMP_Settings.defaultFontAsset = previousDefaultFont;
				}
                Object.DestroyImmediate(root);
            }
        }

		private static void EnsureTabletopHitResultViewPrefab(
			Sprite missSprite,
			Sprite normalSprite,
			Sprite criticalSprite,
			Sprite advantageSprite,
			Sprite disadvantageSprite)
		{
			TMP_FontAsset fontAsset = LoadStackCraftSurfaceFont();
			EnsureStackCraftSurfaceFontFallback(fontAsset, EnsureTestPanelFont());
			GameObject root = new(
				"命中结果",
				typeof(RectTransform),
				typeof(Canvas),
				typeof(CanvasRenderer),
				typeof(Image));
			try
			{
				RectTransform rootRect = root.GetComponent<RectTransform>();
				rootRect.anchorMin = new Vector2(0.5f, 0.5f);
				rootRect.anchorMax = new Vector2(0.5f, 0.5f);
				rootRect.pivot = new Vector2(0.5f, 0.5f);
				rootRect.anchoredPosition = Vector2.zero;
				rootRect.sizeDelta = new Vector2(0.4f, 0.4f);
				root.transform.localScale = Vector3.one;
				root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

				Canvas canvas = root.GetComponent<Canvas>();
				canvas.renderMode = RenderMode.WorldSpace;
				canvas.overrideSorting = true;
				canvas.sortingOrder = 160;

				Image hitImage = root.GetComponent<Image>();
				hitImage.sprite = normalSprite;
				hitImage.color = Color.white;
				hitImage.raycastTarget = false;

				GameObject effectivenessObject = new(
					"Effectiveness",
					typeof(RectTransform),
					typeof(CanvasRenderer),
					typeof(Image));
				effectivenessObject.transform.SetParent(root.transform, false);
				RectTransform effectivenessRect = effectivenessObject.GetComponent<RectTransform>();
				effectivenessRect.anchorMin = new Vector2(1f, 1f);
				effectivenessRect.anchorMax = new Vector2(1f, 1f);
				effectivenessRect.pivot = new Vector2(1f, 1f);
				effectivenessRect.anchoredPosition = new Vector2(0.15f, 0f);
				effectivenessRect.sizeDelta = new Vector2(0.15f, 0.15f);
				Image effectivenessImage = effectivenessObject.GetComponent<Image>();
				effectivenessImage.sprite = null;
				effectivenessImage.color = Color.white;
				effectivenessImage.raycastTarget = false;
				effectivenessImage.enabled = false;

				GameObject damageObject = new(
					"DamageLabel",
					typeof(RectTransform),
					typeof(CanvasRenderer),
					typeof(TextMeshProUGUI));
				damageObject.transform.SetParent(root.transform, false);
				RectTransform damageRect = damageObject.GetComponent<RectTransform>();
				damageRect.anchorMin = Vector2.zero;
				damageRect.anchorMax = Vector2.one;
				damageRect.pivot = new Vector2(0.5f, 0.5f);
				damageRect.anchoredPosition = Vector2.zero;
				damageRect.sizeDelta = Vector2.zero;
				TextMeshProUGUI damageLabel = damageObject.GetComponent<TextMeshProUGUI>();
				damageLabel.font = fontAsset;
				damageLabel.fontSize = 0.2f;
				damageLabel.fontSizeMin = 0.2f;
				damageLabel.fontSizeMax = 0.3f;
				damageLabel.enableAutoSizing = false;
				damageLabel.fontStyle = FontStyles.Normal;
				damageLabel.color = Color.white;
				damageLabel.text = string.Empty;
				damageLabel.textWrappingMode = TextWrappingModes.NoWrap;
				damageLabel.overflowMode = TextOverflowModes.Overflow;
				damageLabel.raycastTarget = false;
				ApplyStackCraftTextParameters(
					damageLabel,
					fontSizeBase: 0.2f,
					HorizontalAlignmentOptions.Geometry,
					VerticalAlignmentOptions.Geometry,
					Vector4.zero);

				TabletopHitResultView hitResultView = root.AddComponent<TabletopHitResultView>();
				SerializedObject serializedView = new(hitResultView);
				serializedView.FindProperty("m_hitImage").objectReferenceValue = hitImage;
				serializedView.FindProperty("m_effectivenessImage").objectReferenceValue = effectivenessImage;
				serializedView.FindProperty("m_damageLabel").objectReferenceValue = damageLabel;
				serializedView.FindProperty("m_missSprite").objectReferenceValue = missSprite;
				serializedView.FindProperty("m_normalSprite").objectReferenceValue = normalSprite;
				serializedView.FindProperty("m_criticalSprite").objectReferenceValue = criticalSprite;
				serializedView.FindProperty("m_advantageSprite").objectReferenceValue = advantageSprite;
				serializedView.FindProperty("m_disadvantageSprite").objectReferenceValue = disadvantageSprite;
				serializedView.FindProperty("m_punchScale").floatValue = 0.15f;
				serializedView.FindProperty("m_punchDurationSeconds").floatValue = 1f;
				serializedView.ApplyModifiedPropertiesWithoutUndo();
				root.SetActive(false);
				SetLayerRecursively(root, UnityUiLayer);

				if (PrefabUtility.SaveAsPrefabAsset(root, TabletopHitResultViewPrefabPath) == null)
				{
					throw new MissingReferenceException(
						$"无法保存命中结果视图：{TabletopHitResultViewPrefabPath}");
				}
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		private static void SetStackCraftFaceTransform(Transform transform, Vector3 localPosition)
		{
			transform.localPosition = localPosition;
			transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			transform.localEulerAngles = new Vector3(90f, 0f, 0f);
			// Unity 不总是把运行时欧拉角写入 m_LocalEulerAnglesHint；这里按 StackCraft Prefab 的 YAML 闭包显式落盘。
			SerializedObject serializedTransform = new(transform);
			SerializedProperty eulerHint = serializedTransform.FindProperty("m_LocalEulerAnglesHint");
			if (eulerHint == null)
			{
				throw new MissingReferenceException("Transform 缺少 m_LocalEulerAnglesHint，无法对齐 StackCraft 卡面姿态参数。");
			}
			eulerHint.vector3Value = new Vector3(90f, 0f, 0f);
			serializedTransform.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void ApplyStackCraftTextParameters(
			TMP_Text text,
			float fontSizeBase,
			HorizontalAlignmentOptions horizontalAlignment,
			VerticalAlignmentOptions verticalAlignment,
			Vector4 margin)
		{
			text.horizontalAlignment = horizontalAlignment;
			text.verticalAlignment = verticalAlignment;
			text.margin = margin;

			SerializedObject serializedText = new(text);
			RequireProperty(serializedText, "m_fontSizeBase").floatValue = fontSizeBase;
			RequireProperty(serializedText, "m_HorizontalAlignment").intValue = (int)horizontalAlignment;
			RequireProperty(serializedText, "m_VerticalAlignment").intValue = (int)verticalAlignment;
			RequireProperty(serializedText, "m_textAlignment").intValue = (int)TextAlignmentOptions.Converted;
			RequireProperty(serializedText, "m_TextStyleHashCode").intValue =
				StackCraftConvertedTextStyleHashCode;
			RequireProperty(serializedText, "m_enableKerning").boolValue = true;
			RequireProperty(serializedText, "m_margin").vector4Value = margin;
			serializedText.ApplyModifiedPropertiesWithoutUndo();
		}

        private static void EnsureTabletopActionProgressViewPrefab()
        {
			Sprite squareSprite = LoadRequiredSprite(
				StackCraftSquareSpritePath,
				"StackCraft Square 进度条图片自有副本");
            GameObject root = new("行动进度", typeof(RectTransform));
            try
            {
				RectTransform rootTransform = root.GetComponent<RectTransform>();
				rootTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
				rootTransform.localScale = Vector3.one;
				rootTransform.anchorMin = new Vector2(0.5f, 0.5f);
				rootTransform.anchorMax = new Vector2(0.5f, 0.5f);
				rootTransform.pivot = new Vector2(0.5f, 0.5f);
				rootTransform.sizeDelta = new Vector2(0.8f, 0.05f);
				Canvas canvas = root.AddComponent<Canvas>();
				canvas.renderMode = RenderMode.WorldSpace;
				canvas.overrideSorting = true;
				canvas.sortingOrder = 0;
				root.AddComponent<CanvasRenderer>();
                Image backgroundImage = root.AddComponent<Image>();
                backgroundImage.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);
				backgroundImage.raycastTarget = false;

                GameObject fillRoot = new("ProgressFill", typeof(RectTransform));
                fillRoot.transform.SetParent(root.transform, false);
				RectTransform fillTransform = fillRoot.GetComponent<RectTransform>();
				fillTransform.anchorMin = Vector2.zero;
				fillTransform.anchorMax = Vector2.one;
				fillTransform.pivot = new Vector2(0.5f, 0.5f);
				fillTransform.anchoredPosition = Vector2.zero;
				fillTransform.sizeDelta = Vector2.zero;
                Image fillImage = fillRoot.AddComponent<Image>();
                fillImage.sprite = squareSprite;
                fillImage.color = new Color(1f, 0.7974138f, 0f, 1f);
				fillImage.raycastTarget = false;
				fillImage.type = Image.Type.Filled;
				fillImage.fillMethod = Image.FillMethod.Horizontal;
				fillImage.fillAmount = 0.725f;

                TabletopActionProgressView progressView =
                    root.AddComponent<TabletopActionProgressView>();
                SerializedObject serializedProgressView = new(progressView);
                serializedProgressView.FindProperty("m_backgroundImage").objectReferenceValue =
                    backgroundImage;
                serializedProgressView.FindProperty("m_progressFill").objectReferenceValue =
                    fillImage;
				serializedProgressView.FindProperty("m_displayOffset").vector3Value =
					new Vector3(0f, 0f, 0.55f);
                serializedProgressView.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(false);
				SetLayerRecursively(root, UnityUiLayer);

                if (PrefabUtility.SaveAsPrefabAsset(root, TabletopActionProgressViewPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存行动进度视图：{TabletopActionProgressViewPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

		private static void EnsureTabletopBattleAreaViewPrefab(Sprite cardSprite)
		{
			GameObject root = new("战斗区域");
			try
			{
				root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
				SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
				renderer.sprite = cardSprite;
				renderer.color = new Color(0.42f, 0.12f, 0.10f, 0.34f);
				TabletopBattleAreaView view = root.AddComponent<TabletopBattleAreaView>();
				SerializedObject serializedView = new(view);
				serializedView.FindProperty("m_renderer").objectReferenceValue = renderer;
				serializedView.ApplyModifiedPropertiesWithoutUndo();
				root.SetActive(false);

				if (PrefabUtility.SaveAsPrefabAsset(root, TabletopBattleAreaViewPrefabPath) == null)
				{
					throw new MissingReferenceException(
						$"无法保存战斗区域视图：{TabletopBattleAreaViewPrefabPath}");
				}
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		private static void EnsureTabletopProjectileViewPrefab(
			Sprite arrowProjectileSprite,
			Sprite magicProjectileSprite)
		{
			GameObject root = new("投射物");
			try
			{
				root.transform.localScale = Vector3.one;
				SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
				renderer.sprite = arrowProjectileSprite;
				renderer.color = Color.white;
				renderer.size = new Vector2(1.28f, 1.28f);
				TabletopProjectileView view = root.AddComponent<TabletopProjectileView>();
				SerializedObject serializedView = new(view);
				serializedView.FindProperty("m_renderer").objectReferenceValue = renderer;
				serializedView.FindProperty("m_rangedSprite").objectReferenceValue = arrowProjectileSprite;
				serializedView.FindProperty("m_magicSprite").objectReferenceValue = magicProjectileSprite;
				serializedView.ApplyModifiedPropertiesWithoutUndo();
				root.SetActive(false);

				if (PrefabUtility.SaveAsPrefabAsset(root, TabletopProjectileViewPrefabPath) == null)
				{
					throw new MissingReferenceException(
						$"无法保存投射物视图：{TabletopProjectileViewPrefabPath}");
				}
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		private static void EnsureTabletopCardSmokeEffectViewPrefab()
		{
			Material cardSmokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(GameplayCardSmokeMaterialPath);
			if (cardSmokeMaterial == null)
			{
				throw new MissingReferenceException($"缺少卡牌烟雾粒子材质：{GameplayCardSmokeMaterialPath}");
			}

			EnsureFolder(GameplayPrefabFolder);
			if (AssetDatabase.LoadAssetAtPath<GameObject>(GameplayCardSmokeEffectPrefabPath) != null)
			{
				GameObject existingRoot = PrefabUtility.LoadPrefabContents(GameplayCardSmokeEffectPrefabPath);
				try
				{
					ConfigureTabletopCardSmokeEffectViewPrefab(existingRoot, cardSmokeMaterial);
					if (PrefabUtility.SaveAsPrefabAsset(existingRoot, GameplayCardSmokeEffectPrefabPath) == null)
					{
						throw new MissingReferenceException(
							$"无法保存卡牌烟雾粒子预制体：{GameplayCardSmokeEffectPrefabPath}");
					}
				}
				finally
				{
					PrefabUtility.UnloadPrefabContents(existingRoot);
				}
				return;
			}

			throw new MissingReferenceException(
				$"缺少卡牌烟雾粒子预制体，不能用近似默认值重建：{GameplayCardSmokeEffectPrefabPath}");
		}

		private static void ConfigureTabletopCardSmokeEffectViewPrefab(
			GameObject root,
			Material cardSmokeMaterial)
		{
			root.name = "卡牌烟雾粒子";
			root.SetActive(false);
			GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

			ParticleSystem particleSystem = root.GetComponent<ParticleSystem>() ??
				root.AddComponent<ParticleSystem>();

			ParticleSystem.MainModule main = particleSystem.main;
			main.loop = false;
			main.playOnAwake = false;
			main.simulationSpace = ParticleSystemSimulationSpace.Local;

			ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
			if (renderer == null)
			{
				throw new MissingReferenceException("卡牌烟雾粒子预制体缺少 ParticleSystemRenderer。");
			}
			renderer.sharedMaterial = cardSmokeMaterial;
			renderer.sortingOrder = 150;
			renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

			TabletopCardSmokeEffectView view = root.GetComponent<TabletopCardSmokeEffectView>() ??
				root.AddComponent<TabletopCardSmokeEffectView>();
			foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>())
			{
				if (behaviour != null && behaviour != view)
				{
					Object.DestroyImmediate(behaviour, allowDestroyingAssets: true);
				}
			}

			SerializedObject serializedView = new(view);
			serializedView.FindProperty("m_particleSystem").objectReferenceValue = particleSystem;
			serializedView.FindProperty("m_renderer").objectReferenceValue = renderer;
			serializedView.ApplyModifiedPropertiesWithoutUndo();
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
                    typeof(UINavigationTarget),
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
                    "行动",
                    42f);
                SetStretchRect(choiceText.rectTransform, 24f);
				choiceTemplateObject.SetActive(false);

                GameObject cancelObject = new(
                    "Cancel",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
					typeof(UINavigationTarget));
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
                    "取消",
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
				SetLayerRecursively(root, UnityUiLayer);

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
					typeof(Button),
					typeof(UINavigationTarget));
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
				SetLayerRecursively(root, UnityUiLayer);

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
				typeof(Button),
					typeof(UINavigationTarget));
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
				typeof(Button),
					typeof(UINavigationTarget));
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
			TMP_FontAsset statsFontAsset = LoadStackCraftSurfaceFont();
			Sprite squareSprite = LoadRequiredSprite(
				StackCraftSquareSpritePath,
				"StackCraft HUD 时间进度条图片");
			Sprite[] paceSprites =
			{
				LoadRequiredSprite(StackCraftTimePacePausedSpritePath, "StackCraft HUD 暂停速度图标"),
				LoadRequiredSprite(StackCraftTimePaceNormalSpritePath, "StackCraft HUD 普通速度图标"),
				LoadRequiredSprite(StackCraftTimePaceFastSpritePath, "StackCraft HUD 加速速度图标"),
			};
			Sprite nutritionStatsSprite = LoadRequiredSprite(
				StackCraftStatsNutritionSpritePath,
				"StackCraft HUD 营养统计图标");
			Sprite currencyStatsSprite = LoadRequiredSprite(
				StackCraftStatsCurrencySpritePath,
				"StackCraft HUD 货币统计图标");
			Sprite cardStatsSprite = LoadRequiredSprite(
				StackCraftStatsCardSpritePath,
				"StackCraft HUD 卡牌容量统计图标");

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

                GameObject dayTimeObject = new(
					"ConfirmTurn",
					typeof(RectTransform),
					typeof(Image),
					typeof(Button),
					typeof(CanvasGroup),
					typeof(UINavigationTarget));
                dayTimeObject.transform.SetParent(root.transform, false);
                RectTransform dayTimeRect = dayTimeObject.GetComponent<RectTransform>();
                SetAnchoredRect(
                    dayTimeRect,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
					new Vector2(300f, 50f),
                    new Vector2(30f, -30f));
                Image dayTimeImage = dayTimeObject.GetComponent<Image>();
                dayTimeImage.color = new Color(0f, 0f, 0f, 0.8f);
				dayTimeImage.raycastTarget = true;
				Button confirmButton = dayTimeObject.GetComponent<Button>();
				confirmButton.targetGraphic = dayTimeImage;
				ColorBlock confirmColors = confirmButton.colors;
				confirmColors.normalColor = Color.white;
				confirmColors.highlightedColor = Color.white;
				confirmColors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
				confirmColors.selectedColor = Color.white;
				confirmColors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
				confirmColors.colorMultiplier = 1f;
				confirmButton.colors = confirmColors;
				CanvasGroup dayTimeCanvasGroup = dayTimeObject.GetComponent<CanvasGroup>();
				dayTimeCanvasGroup.alpha = 1f;
				dayTimeCanvasGroup.interactable = true;
				dayTimeCanvasGroup.blocksRaycasts = true;

				GameObject progressFillObject = new(
					"TimeProgress",
					typeof(RectTransform),
					typeof(Image));
				progressFillObject.transform.SetParent(dayTimeRect, false);
				SetStretchRect(progressFillObject.GetComponent<RectTransform>(), 0f);
				Image progressFill = progressFillObject.GetComponent<Image>();
				progressFill.sprite = squareSprite;
				progressFill.type = Image.Type.Filled;
				progressFill.fillMethod = Image.FillMethod.Horizontal;
				progressFill.fillOrigin = 0;
				progressFill.fillAmount = 0.7f;
				progressFill.color = new Color(0.21960784f, 0.64705884f, 0.8784314f, 0.8f);
				progressFill.raycastTarget = false;

                TextMeshProUGUI turnLabel = CreatePanelText(
                    "DayText",
                    dayTimeRect,
                    statsFontAsset,
                    "第 N 天",
                    30f);
				RectTransform turnLabelRect = turnLabel.rectTransform;
				turnLabelRect.anchorMin = Vector2.zero;
				turnLabelRect.anchorMax = Vector2.one;
				turnLabelRect.anchoredPosition = Vector2.zero;
				turnLabelRect.sizeDelta = new Vector2(-20f, -20f);
				turnLabelRect.pivot = new Vector2(0.5f, 0.5f);
				ApplyStackCraftTextParameters(
					turnLabel,
					fontSizeBase: 30f,
					HorizontalAlignmentOptions.Left,
					VerticalAlignmentOptions.Middle,
					Vector4.zero);

				GameObject paceObject = new(
					"PaceImage",
					typeof(RectTransform),
					typeof(Image));
				paceObject.transform.SetParent(dayTimeRect, false);
				RectTransform paceRect = paceObject.GetComponent<RectTransform>();
				SetAnchoredRect(
					paceRect,
					new Vector2(1f, 0.5f),
					new Vector2(1f, 0.5f),
					new Vector2(30f, 30f),
					new Vector2(-10f, 0f));
				Image paceImage = paceObject.GetComponent<Image>();
				paceImage.sprite = paceSprites[1];
				paceImage.raycastTarget = false;

				GameObject statsObject = new(
					"CardStatsUI",
					typeof(RectTransform),
					typeof(Image),
					typeof(HorizontalLayoutGroup),
					typeof(ContentSizeFitter),
					typeof(CanvasGroup));
				statsObject.transform.SetParent(root.transform, false);
				RectTransform statsRect = statsObject.GetComponent<RectTransform>();
				SetAnchoredRect(
					statsRect,
					new Vector2(0f, 1f),
					new Vector2(0f, 1f),
					new Vector2(0f, 50f),
					new Vector2(360f, -30f));
				Image statsBackground = statsObject.GetComponent<Image>();
				statsBackground.color = new Color(0f, 0f, 0f, 0.8f);
				statsBackground.raycastTarget = true;
				HorizontalLayoutGroup statsLayout = statsObject.GetComponent<HorizontalLayoutGroup>();
				statsLayout.padding = new RectOffset(0, 34, 0, 0);
				statsLayout.childAlignment = TextAnchor.MiddleLeft;
				statsLayout.spacing = 0f;
				statsLayout.childForceExpandWidth = false;
				statsLayout.childForceExpandHeight = false;
				statsLayout.childControlWidth = true;
				statsLayout.childControlHeight = false;
				ContentSizeFitter statsFitter = statsObject.GetComponent<ContentSizeFitter>();
				statsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
				statsFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
				CanvasGroup statsCanvasGroup = statsObject.GetComponent<CanvasGroup>();
				statsCanvasGroup.alpha = 1f;
				statsCanvasGroup.interactable = true;
				statsCanvasGroup.blocksRaycasts = true;

				Image nutritionIcon = CreateStatsIcon(
					"NutritionIcon",
					statsRect,
					nutritionStatsSprite);
				TextMeshProUGUI nutritionLabel = CreateStatsLabel(
					"NutritionLabel",
					statsRect,
					statsFontAsset,
					"1/11");
				Image currencyIcon = CreateStatsIcon(
					"CurrencyIcon",
					statsRect,
					currencyStatsSprite);
				TextMeshProUGUI currencyLabel = CreateStatsLabel(
					"CurrencyLabel",
					statsRect,
					statsFontAsset,
					"2/22");
				Image cardCountIcon = CreateStatsIcon(
					"CardIcon",
					statsRect,
					cardStatsSprite);
				TextMeshProUGUI cardCountLabel = CreateStatsLabel(
					"CardLabel",
					statsRect,
					statsFontAsset,
					"3/33");

				TextMeshProUGUI watermark = CreatePanelText(
					"Watermark",
					rootRect,
					statsFontAsset,
					"模板玩法演示",
					24f);
				SetAnchoredRect(
					watermark.rectTransform,
					new Vector2(0.5f, 0f),
					new Vector2(0.5f, 0f),
					new Vector2(600f, 40f),
					Vector2.zero);
				watermark.color = new Color(1f, 1f, 1f, 0.69803923f);
				ApplyStackCraftTextParameters(
					watermark,
					fontSizeBase: 24f,
					HorizontalAlignmentOptions.Center,
					VerticalAlignmentOptions.Top,
					Vector4.zero);

                ScenarioTurnPanel panel = root.GetComponent<ScenarioTurnPanel>();
                SerializedObject serializedPanel = new(panel);
                RequireProperty(serializedPanel, "m_turnLabel").objectReferenceValue = turnLabel;
				RequireProperty(serializedPanel, "m_dayProgressFill").objectReferenceValue = progressFill;
				RequireProperty(serializedPanel, "m_paceImage").objectReferenceValue = paceImage;
				SerializedProperty paceIconsProperty = RequireProperty(serializedPanel, "m_paceIcons");
				paceIconsProperty.arraySize = paceSprites.Length;
				for (int paceIndex = 0; paceIndex < paceSprites.Length; paceIndex++)
				{
					paceIconsProperty.GetArrayElementAtIndex(paceIndex).objectReferenceValue = paceSprites[paceIndex];
				}
				RequireProperty(serializedPanel, "m_dayTimeGroup").objectReferenceValue = dayTimeCanvasGroup;
				RequireProperty(serializedPanel, "m_cardStatsGroup").objectReferenceValue = statsCanvasGroup;
				RequireProperty(serializedPanel, "m_nutritionIcon").objectReferenceValue = nutritionIcon;
				RequireProperty(serializedPanel, "m_nutritionLabel").objectReferenceValue = nutritionLabel;
				RequireProperty(serializedPanel, "m_currencyIcon").objectReferenceValue = currencyIcon;
				RequireProperty(serializedPanel, "m_currencyLabel").objectReferenceValue = currencyLabel;
				RequireProperty(serializedPanel, "m_cardCountIcon").objectReferenceValue = cardCountIcon;
				RequireProperty(serializedPanel, "m_cardCountLabel").objectReferenceValue = cardCountLabel;
				RequireProperty(serializedPanel, "m_confirmTurnLabel").objectReferenceValue = null;
                RequireProperty(serializedPanel, "m_confirmTurnButton").objectReferenceValue = confirmButton;
				RequireProperty(serializedPanel, "m_progressionModeButton").objectReferenceValue = null;
				RequireProperty(serializedPanel, "m_progressionModeLabel").objectReferenceValue = null;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
				SetLayerRecursively(root, UnityUiLayer);

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

        private static GameObject EnsureGameUiPrefab()
        {
            GameObject root = new("FoundationGameUI", typeof(UIManager));
            try
            {
                UIManager uiManager = root.GetComponent<UIManager>();
                WriteMenuPanelBindings(
                    uiManager,
                    (EMenu.Pause, typeof(ScenarioPausePanel)),
                    (EMenu.Settings, typeof(UISettings)));

                SerializedObject serializedManager = new(uiManager);
                RequireProperty(serializedManager, "m_stackName").stringValue = "game-menu";
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(uiManager);
				SetLayerRecursively(root, UnityUiLayer);

                if (PrefabUtility.SaveAsPrefabAsset(root, FoundationGameUiPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存地基 UI 宿主预制体：{FoundationGameUiPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(FoundationGameUiPrefabPath) ??
                throw new MissingReferenceException(
                    $"无法重新载入地基 UI 宿主预制体：{FoundationGameUiPrefabPath}");
        }

        private static void WriteMenuPanelBindings(
            UIManager uiManager,
            params (EMenu Menu, Type PanelType)[] bindings)
        {
            FieldInfo registrationsField = RequireField(typeof(UIManager), "m_registeredMenuPanels");
            Type bindingType = registrationsField.FieldType.GetElementType() ??
                throw new MissingFieldException(
                    $"{nameof(UIManager)}.{registrationsField.Name} 不是菜单注册数组。");
            Array runtimeBindings = Array.CreateInstance(bindingType, bindings.Length);

            for (int i = 0; i < bindings.Length; i++)
            {
                runtimeBindings.SetValue(
                    CreateMenuPanelBinding(bindingType, bindings[i].Menu, bindings[i].PanelType),
                    i);
            }

            registrationsField.SetValue(uiManager, runtimeBindings);
        }

        private static object CreateMenuPanelBinding(
            Type bindingType,
            EMenu menu,
            Type panelType)
        {
            object binding = Activator.CreateInstance(bindingType);
            RequireField(bindingType, "m_menu").SetValue(binding, menu);
            RequireField(bindingType, "m_panelType").SetValue(binding, CreateMenuPanelTypeReference(panelType));
            // UILevel 是 YokiFrame 的 readonly struct，当前 Unity SerializedProperty 不能直接写它的内部排序值。
            RequireField(bindingType, "m_level").SetValue(binding, UILevel.Pop);
            return binding;
        }

        private static UIKitMenuPanelTypeReference CreateMenuPanelTypeReference(Type panelType)
        {
            if (!typeof(UIKitMenuPanelBase).IsAssignableFrom(panelType))
            {
                throw new ArgumentException(
                    $"地基测试菜单面板必须继承 {nameof(UIKitMenuPanelBase)}：{panelType.FullName}",
                    nameof(panelType));
            }

            UIKitMenuPanelTypeReference reference = new();
            RequireField(typeof(UIKitMenuPanelTypeReference), "m_assemblyQualifiedName")
                .SetValue(reference, panelType.AssemblyQualifiedName);
            return reference;
        }

        private static FieldInfo RequireField(Type type, string fieldName)
        {
            return type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new MissingFieldException(type.FullName, fieldName);
        }

        private static void EnsureScenarioPausePanelPrefab()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestPanelFontPath);
            if (uiSprite == null || fontAsset == null)
            {
                throw new MissingReferenceException("缺少剧本暂停菜单所需的内置图片或测试字体。");
            }

            GameObject root = new(
                "ScenarioPausePanel",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(ScenarioPausePanel));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                Image overlay = root.GetComponent<Image>();
                overlay.color = new Color(0f, 0f, 0f, 0.64f);

                GameObject windowObject = new("PauseWindow", typeof(RectTransform), typeof(Image));
                windowObject.transform.SetParent(root.transform, false);
                RectTransform window = windowObject.GetComponent<RectTransform>();
                SetAnchoredRect(
                    window,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(760f, 620f),
                    Vector2.zero);
                Image windowImage = windowObject.GetComponent<Image>();
                windowImage.sprite = uiSprite;
                windowImage.type = Image.Type.Sliced;
                windowImage.color = new Color(0.045f, 0.06f, 0.07f, 0.98f);

                TextMeshProUGUI title = CreatePanelText("Title", window, fontAsset, "暂停", 64f);
                SetAnchoredRect(
                    title.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(560f, 92f),
                    new Vector2(0f, -54f));

                Button continueButton = CreateSavePanelButton(
                    "Continue",
                    window,
                    uiSprite,
                    fontAsset,
                    "继续",
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(520f, 104f),
                    new Vector2(0f, -188f),
                    new Color(0.13f, 0.43f, 0.34f, 1f),
                    44f);
                Button settingsButton = CreateSavePanelButton(
                    "Settings",
                    window,
                    uiSprite,
                    fontAsset,
                    "设置",
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(520f, 104f),
                    new Vector2(0f, -320f),
                    new Color(0.10f, 0.13f, 0.15f, 1f),
                    44f);
                Button saveAndExitButton = CreateSavePanelButton(
                    "SaveAndExit",
                    window,
                    uiSprite,
                    fontAsset,
                    "保存并退出",
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(520f, 104f),
                    new Vector2(0f, -452f),
                    new Color(0.34f, 0.13f, 0.13f, 1f),
                    44f);

                ScenarioPausePanel panel = root.GetComponent<ScenarioPausePanel>();
                SerializedObject serializedPanel = new(panel);
                serializedPanel.FindProperty("m_continueButton").objectReferenceValue = continueButton;
                serializedPanel.FindProperty("m_settingsButton").objectReferenceValue = settingsButton;
                serializedPanel.FindProperty("m_saveAndExitButton").objectReferenceValue = saveAndExitButton;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
				SetLayerRecursively(root, UnityUiLayer);

                if (PrefabUtility.SaveAsPrefabAsset(root, ScenarioPausePanelPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存剧本暂停菜单：{ScenarioPausePanelPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureScenarioSavePanelPrefab()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestPanelFontPath);
            if (uiSprite == null || fontAsset == null)
            {
                throw new MissingReferenceException("缺少剧本存档窗口所需的内置图片或测试字体。");
            }

            GameObject root = new(
                "ScenarioSavePanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(ScenarioSavePanel));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                Image overlay = root.GetComponent<Image>();
                overlay.color = new Color(0f, 0f, 0f, 0.68f);

                GameObject windowObject = new("SaveWindow", typeof(RectTransform), typeof(Image));
                windowObject.transform.SetParent(root.transform, false);
                RectTransform window = windowObject.GetComponent<RectTransform>();
                SetAnchoredRect(
                    window,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(1560f, 1080f),
                    Vector2.zero);
                Image windowImage = windowObject.GetComponent<Image>();
                windowImage.sprite = uiSprite;
                windowImage.type = Image.Type.Sliced;
                windowImage.color = new Color(0.045f, 0.06f, 0.07f, 0.99f);

                TextMeshProUGUI title = CreatePanelText(
                    "Title",
                    window,
                    fontAsset,
                    "保存单局",
                    64f);
                SetAnchoredRect(
                    title.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(1240f, 96f),
                    new Vector2(0f, -40f));

                Button closeButton = CreateSavePanelButton(
                    "Close",
                    window,
                    uiSprite,
                    fontAsset,
                    "×",
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(84f, 84f),
                    new Vector2(-32f, -32f),
                    new Color(0.16f, 0.18f, 0.2f, 1f),
                    48f);

                GameObject viewportObject = new(
                    "Viewport",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(RectMask2D),
                    typeof(ScrollRect));
                viewportObject.transform.SetParent(window, false);
                RectTransform viewport = viewportObject.GetComponent<RectTransform>();
                SetAnchoredRect(
                    viewport,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(1380f, 720f),
                    new Vector2(0f, 28f));
                Image viewportImage = viewportObject.GetComponent<Image>();
                viewportImage.sprite = uiSprite;
                viewportImage.type = Image.Type.Sliced;
                viewportImage.color = new Color(0.025f, 0.032f, 0.038f, 0.9f);

                GameObject contentObject = new(
                    "Slots",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter));
                contentObject.transform.SetParent(viewport, false);
                RectTransform slotRoot = contentObject.GetComponent<RectTransform>();
                slotRoot.anchorMin = new Vector2(0f, 1f);
                slotRoot.anchorMax = new Vector2(1f, 1f);
                slotRoot.pivot = new Vector2(0.5f, 1f);
                slotRoot.anchoredPosition = new Vector2(0f, -24f);
                slotRoot.sizeDelta = new Vector2(-48f, 0f);
                VerticalLayoutGroup slotLayout = contentObject.GetComponent<VerticalLayoutGroup>();
                slotLayout.padding = new RectOffset(0, 0, 0, 24);
                slotLayout.spacing = 18f;
                slotLayout.childControlWidth = true;
                slotLayout.childControlHeight = true;
                slotLayout.childForceExpandWidth = true;
                slotLayout.childForceExpandHeight = false;
                contentObject.GetComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
                ScrollRect scrollRect = viewportObject.GetComponent<ScrollRect>();
                scrollRect.content = slotRoot;
                scrollRect.viewport = viewport;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;

                GameObject slotTemplateObject = new(
                    "SaveSlotTemplate",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(LayoutElement),
                    typeof(ScenarioSaveSlotView));
                slotTemplateObject.transform.SetParent(slotRoot, false);
                RectTransform slotTemplateRect = slotTemplateObject.GetComponent<RectTransform>();
                slotTemplateRect.sizeDelta = new Vector2(1332f, 190f);
                Image slotImage = slotTemplateObject.GetComponent<Image>();
                slotImage.sprite = uiSprite;
                slotImage.type = Image.Type.Sliced;
                slotImage.color = new Color(0.075f, 0.095f, 0.105f, 1f);
                LayoutElement slotElement = slotTemplateObject.GetComponent<LayoutElement>();
                slotElement.preferredHeight = 190f;
                TextMeshProUGUI summary = CreatePanelText(
                    "Summary",
                    slotTemplateRect,
                    fontAsset,
                    "槽位 01\n单局摘要\n2026-08-13 12:00",
                    38f);
                summary.alignment = TextAlignmentOptions.MidlineLeft;
                SetAnchoredRect(
                    summary.rectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(900f, 158f),
                    new Vector2(30f, 0f));
                Button primary = CreateSavePanelButton(
                    "Primary",
                    slotTemplateRect,
                    uiSprite,
                    fontAsset,
                    "读取",
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(220f, 92f),
                    new Vector2(-138f, 0f),
                    new Color(0.12f, 0.42f, 0.34f, 1f),
                    38f);
                TextMeshProUGUI primaryLabel = primary.GetComponentInChildren<TextMeshProUGUI>();
                Button delete = CreateSavePanelButton(
                    "Delete",
                    slotTemplateRect,
                    uiSprite,
                    fontAsset,
                    "×",
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(78f, 78f),
                    new Vector2(-24f, 0f),
                    new Color(0.38f, 0.13f, 0.13f, 1f),
                    42f);
                ScenarioSaveSlotView slotView = slotTemplateObject.GetComponent<ScenarioSaveSlotView>();
                SerializedObject serializedSlot = new(slotView);
                serializedSlot.FindProperty("m_summaryLabel").objectReferenceValue = summary;
                serializedSlot.FindProperty("m_primaryButton").objectReferenceValue = primary;
                serializedSlot.FindProperty("m_primaryLabel").objectReferenceValue = primaryLabel;
                serializedSlot.FindProperty("m_deleteButton").objectReferenceValue = delete;
                serializedSlot.ApplyModifiedPropertiesWithoutUndo();
                slotTemplateObject.SetActive(false);

                TextMeshProUGUI emptyLabel = CreatePanelText(
                    "EmptyState",
                    viewport,
                    fontAsset,
                    "暂无存档",
                    44f);
                SetStretchRect(emptyLabel.rectTransform, 36f);

                Button createSave = CreateSavePanelButton(
                    "CreateSave",
                    window,
                    uiSprite,
                    fontAsset,
                    "新建存档",
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(320f, 92f),
                    new Vector2(90f, 60f),
                    new Color(0.12f, 0.42f, 0.34f, 1f),
                    38f);
                Button clearAll = CreateSavePanelButton(
                    "ClearAll",
                    window,
                    uiSprite,
                    fontAsset,
                    "清空全部",
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(320f, 92f),
                    new Vector2(-170f, 60f),
                    new Color(0.34f, 0.13f, 0.13f, 1f),
                    38f);
                Button saveAndExit = CreateSavePanelButton(
                    "SaveAndExit",
                    window,
                    uiSprite,
                    fontAsset,
                    "保存并退出",
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(360f, 92f),
                    new Vector2(-90f, 60f),
                    new Color(0.17f, 0.38f, 0.47f, 1f),
                    38f);

                ScenarioSavePanel panel = root.GetComponent<ScenarioSavePanel>();
                SerializedObject serializedPanel = new(panel);
                serializedPanel.FindProperty("m_titleLabel").objectReferenceValue = title;
                serializedPanel.FindProperty("m_slotRoot").objectReferenceValue = slotRoot;
                serializedPanel.FindProperty("m_slotTemplate").objectReferenceValue = slotView;
                serializedPanel.FindProperty("m_emptyState").objectReferenceValue = emptyLabel.gameObject;
                serializedPanel.FindProperty("m_createSaveButton").objectReferenceValue = createSave;
                serializedPanel.FindProperty("m_clearAllButton").objectReferenceValue = clearAll;
                serializedPanel.FindProperty("m_saveAndExitButton").objectReferenceValue = saveAndExit;
                serializedPanel.FindProperty("m_closeButton").objectReferenceValue = closeButton;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
				SetLayerRecursively(root, UnityUiLayer);

                if (PrefabUtility.SaveAsPrefabAsset(root, ScenarioSavePanelPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存剧本存档窗口：{ScenarioSavePanelPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureConfirmationDialogPanelPrefab()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestPanelFontPath);
            if (uiSprite == null || fontAsset == null)
            {
                throw new MissingReferenceException("缺少通用确认框所需的内置图片或测试字体。");
            }

            GameObject root = new(
                "ConfirmationDialogPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(ConfirmationDialogPanel));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

                GameObject windowObject = new("DialogWindow", typeof(RectTransform), typeof(Image));
                windowObject.transform.SetParent(root.transform, false);
                RectTransform window = windowObject.GetComponent<RectTransform>();
                SetAnchoredRect(
                    window,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(1120f, 600f),
                    Vector2.zero);
                Image windowImage = windowObject.GetComponent<Image>();
                windowImage.sprite = uiSprite;
                windowImage.type = Image.Type.Sliced;
                windowImage.color = new Color(0.055f, 0.07f, 0.08f, 1f);

                TextMeshProUGUI title = CreatePanelText("Title", window, fontAsset, "请确认", 60f);
                SetAnchoredRect(
                    title.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(940f, 90f),
                    new Vector2(0f, -48f));
                TextMeshProUGUI message = CreatePanelText(
                    "Message",
                    window,
                    fontAsset,
                    "确认执行这项操作吗？",
                    42f);
                SetAnchoredRect(
                    message.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(920f, 210f),
                    new Vector2(0f, 28f));
                Button confirm = CreateSavePanelButton(
                    "Confirm",
                    window,
                    uiSprite,
                    fontAsset,
                    "确定",
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(320f, 96f),
                    new Vector2(-180f, 54f),
                    new Color(0.4f, 0.14f, 0.14f, 1f),
                    40f);
                Button cancel = CreateSavePanelButton(
                    "Cancel",
                    window,
                    uiSprite,
                    fontAsset,
                    "取消",
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(320f, 96f),
                    new Vector2(180f, 54f),
                    new Color(0.18f, 0.21f, 0.23f, 1f),
                    40f);

                ConfirmationDialogPanel panel = root.GetComponent<ConfirmationDialogPanel>();
                SerializedObject serializedPanel = new(panel);
                serializedPanel.FindProperty("m_titleLabel").objectReferenceValue = title;
                serializedPanel.FindProperty("m_messageLabel").objectReferenceValue = message;
                serializedPanel.FindProperty("m_confirmButton").objectReferenceValue = confirm;
                serializedPanel.FindProperty("m_cancelButton").objectReferenceValue = cancel;
                serializedPanel.FindProperty("m_confirmLabel").objectReferenceValue =
                    confirm.GetComponentInChildren<TextMeshProUGUI>();
                serializedPanel.FindProperty("m_cancelLabel").objectReferenceValue =
                    cancel.GetComponentInChildren<TextMeshProUGUI>();
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
				SetLayerRecursively(root, UnityUiLayer);

                if (PrefabUtility.SaveAsPrefabAsset(root, ConfirmationDialogPanelPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存通用确认框：{ConfirmationDialogPanelPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
		}

		private static void CreateScenarioScreenEffect(Transform runtimeRoot)
		{
			GameObject screenEffectObject = new(
				"剧本屏幕效果",
				typeof(Volume),
				typeof(ScenarioScreenEffectView));
			screenEffectObject.transform.SetParent(runtimeRoot, false);
			Volume volume = screenEffectObject.GetComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = 100f;
			volume.sharedProfile = EnsureScenarioScreenEffectProfile();

			ScenarioScreenEffectView screenEffect = screenEffectObject.GetComponent<ScenarioScreenEffectView>();
			SerializedObject serializedEffect = new(screenEffect);
			RequireProperty(serializedEffect, "m_volume").objectReferenceValue = volume;
			serializedEffect.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(screenEffect);
		}

		private static VolumeProfile EnsureScenarioScreenEffectProfile()
		{
			EnsureFolder(TabletopTestFolder);
			VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ScenarioScreenEffectProfilePath);
			if (profile == null)
			{
				profile = ScriptableObject.CreateInstance<VolumeProfile>();
				profile.name = "剧本屏幕效果配置";
				AssetDatabase.CreateAsset(profile, ScenarioScreenEffectProfilePath);
			}

			ColorAdjustments colorAdjustments = EnsureVolumeOverride<ColorAdjustments>(profile);
			colorAdjustments.saturation.overrideState = true;
			colorAdjustments.saturation.value = 0f;
			Vignette vignette = EnsureVolumeOverride<Vignette>(profile);
			vignette.intensity.overrideState = true;
			vignette.intensity.value = 0f;
			EditorUtility.SetDirty(profile);
			return profile;
		}

		private static TVolumeComponent EnsureVolumeOverride<TVolumeComponent>(VolumeProfile profile)
			where TVolumeComponent : VolumeComponent
		{
			profile.components.RemoveAll(component => component == null);
			if (profile.TryGet(out TVolumeComponent component) && component != null)
			{
				return component;
			}

			component = profile.Add<TVolumeComponent>(overrides: true);
			AttachVolumeComponentToProfileAsset(profile, component);
			return component;
		}

		private static void AttachVolumeComponentToProfileAsset(
			VolumeProfile profile,
			VolumeComponent component)
		{
			string profilePath = AssetDatabase.GetAssetPath(profile);
			if (string.IsNullOrEmpty(profilePath) ||
				component == null ||
				!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(component)))
			{
				return;
			}

			AssetDatabase.AddObjectToAsset(component, profile);
			EditorUtility.SetDirty(component);
		}

		private static void ConfigurePostProcessingCamera(GameObject cameraObject)
		{
			UniversalAdditionalCameraData cameraData =
				cameraObject.GetComponent<UniversalAdditionalCameraData>() ??
				cameraObject.AddComponent<UniversalAdditionalCameraData>();
			cameraData.renderPostProcessing = false;
		}

        private static Button CreateSavePanelButton(
            string name,
            Transform parent,
            Sprite sprite,
            TMP_FontAsset fontAsset,
            string label,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 size,
            Vector2 position,
            Color color,
            float fontSize)
        {
            GameObject buttonObject = new(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
					typeof(UINavigationTarget));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetAnchoredRect(rect, anchor, pivot, size, position);
            Image image = buttonObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI text = CreatePanelText("Label", rect, fontAsset, label, fontSize);
            SetStretchRect(text.rectTransform, 12f);
            return button;
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

                GameObject contentRoot = new(
					"InfoPanel",
					typeof(RectTransform),
					typeof(Image),
					typeof(VerticalLayoutGroup),
					typeof(ContentSizeFitter));
                contentRoot.transform.SetParent(root.transform, false);
                RectTransform contentRect = contentRoot.GetComponent<RectTransform>();
                SetAnchoredRect(
                    contentRect,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(400f, 0f),
                    new Vector2(30f, 30f));
                Image background = contentRoot.GetComponent<Image>();
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.color = new Color(0f, 0f, 0f, 0.9019608f);
                background.raycastTarget = false;

				VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
				layout.childAlignment = TextAnchor.UpperLeft;
				layout.childControlWidth = true;
				layout.childControlHeight = true;
				layout.childForceExpandWidth = false;
				layout.childForceExpandHeight = false;
				layout.spacing = 0f;
				ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>();
				fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
				fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

				TextMeshProUGUI infoText = CreatePanelText(
					"InfoText",
					contentRect,
					fontAsset,
					string.Empty,
					34f);
				RectTransform infoTextRect = infoText.rectTransform;
				infoTextRect.anchorMin = Vector2.zero;
				infoTextRect.anchorMax = Vector2.zero;
				infoTextRect.pivot = new Vector2(0.5f, 0.5f);
				infoTextRect.anchoredPosition = Vector2.zero;
				infoTextRect.sizeDelta = Vector2.zero;
				infoText.textWrappingMode = TextWrappingModes.Normal;
				infoText.color = new Color(1f, 1f, 1f, 0.8f);
				infoText.lineSpacing = -15f;
				infoText.paragraphSpacing = 20f;
				ApplyStackCraftTextParameters(
					infoText,
					fontSizeBase: 34f,
					HorizontalAlignmentOptions.Left,
					(VerticalAlignmentOptions)1024,
					new Vector4(20f, 20f, 20f, 20f));

                TabletopCardInfoPanel panel = root.GetComponent<TabletopCardInfoPanel>();
                SerializedObject serializedPanel = new(panel);
                SerializedProperty contentRootProperty = serializedPanel.FindProperty("m_contentRoot");
                SerializedProperty infoLabelProperty = serializedPanel.FindProperty("m_infoLabel");
				SerializedProperty headerSizeProperty = serializedPanel.FindProperty("m_headerSize");
				SerializedProperty bodySizeProperty = serializedPanel.FindProperty("m_bodySize");
                if (contentRootProperty == null ||
					infoLabelProperty == null ||
					headerSizeProperty == null ||
					bodySizeProperty == null)
                {
                    throw new MissingReferenceException(
                        $"{nameof(TabletopCardInfoPanel)} 的预制体引用字段已变更，测试资源生成器需要同步更新。");
                }

                contentRootProperty.objectReferenceValue = contentRoot;
                infoLabelProperty.objectReferenceValue = infoText;
				headerSizeProperty.intValue = 34;
				bodySizeProperty.intValue = 30;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
				SetLayerRecursively(root, UnityUiLayer);

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

        private static void EnsureScenarioJournalPanelPrefab()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset fontAsset = LoadStackCraftSurfaceFont();
			EnsureStackCraftSurfaceFontFallback(fontAsset, EnsureTestPanelFont());
            if (uiSprite == null || fontAsset == null)
            {
                throw new MissingReferenceException("缺少剧本日志所需的内置图片或测试字体。");
            }

            GameObject root = new(
                "ScenarioJournalPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(ScenarioJournalPanel));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                Image rootImage = root.GetComponent<Image>();
                rootImage.sprite = uiSprite;
                rootImage.type = Image.Type.Sliced;
                rootImage.color = Color.clear;
                rootImage.raycastTarget = false;

                GameObject menuPanelObject = new("MenuPanel", typeof(RectTransform));
                menuPanelObject.transform.SetParent(root.transform, false);
                RectTransform menuPanel = menuPanelObject.GetComponent<RectTransform>();
                SetAnchoredRect(
                    menuPanel,
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(400f, 0f),
                    new Vector2(0f, 0f));
				menuPanel.anchorMin = new Vector2(1f, 0f);
				menuPanel.anchorMax = new Vector2(1f, 1f);
				menuPanel.offsetMin = new Vector2(-400f, 0f);
				menuPanel.offsetMax = Vector2.zero;

				GameObject headerObject = new(
					"Header",
					typeof(RectTransform),
					typeof(Image),
					typeof(HorizontalLayoutGroup),
					typeof(CanvasGroup),
					typeof(ToggleGroup));
				headerObject.transform.SetParent(menuPanel, false);
				RectTransform header = headerObject.GetComponent<RectTransform>();
				header.anchorMin = new Vector2(0f, 1f);
				header.anchorMax = new Vector2(1f, 1f);
				header.pivot = new Vector2(0.5f, 1f);
				header.anchoredPosition = Vector2.zero;
				header.sizeDelta = new Vector2(0f, 60f);
				header.localScale = Vector3.one;
				header.localRotation = Quaternion.identity;
				Image headerImage = headerObject.GetComponent<Image>();
				headerImage.sprite = uiSprite;
				headerImage.color = new Color(0.5019608f, 0.5019608f, 0.5019608f, 0.9019608f);
				headerImage.raycastTarget = false;
				HorizontalLayoutGroup headerLayout = headerObject.GetComponent<HorizontalLayoutGroup>();
				headerLayout.childAlignment = TextAnchor.UpperLeft;
				headerLayout.childControlWidth = true;
				headerLayout.childControlHeight = true;
				headerLayout.childForceExpandWidth = true;
				headerLayout.childForceExpandHeight = true;
				headerLayout.spacing = 0f;
				ToggleGroup headerToggleGroup = headerObject.GetComponent<ToggleGroup>();
				headerToggleGroup.allowSwitchOff = false;

				Toggle questsTab = CreateStackCraftJournalTabToggle(
					"QuestsToggle",
					header,
					uiSprite,
					fontAsset,
					headerToggleGroup,
					"任务",
					isOn: true);
				Toggle actionsTab = CreateStackCraftJournalTabToggle(
					"RecipesToggle",
					header,
					uiSprite,
					fontAsset,
					headerToggleGroup,
					"配方",
					isOn: false);

				Button menuToggle = CreateStackCraftJournalMenuButton(
					"MenuToggle",
					menuPanel,
					uiSprite,
					fontAsset);
				TextMeshProUGUI menuToggleLabel = menuToggle.GetComponentInChildren<TextMeshProUGUI>(true);
				if (menuToggleLabel == null)
				{
					throw new MissingReferenceException("剧本日志菜单折叠按钮缺少文字。");
				}

                Button closeButton = CreateSavePanelButton(
                    "Close",
                    menuPanel,
                    uiSprite,
                    fontAsset,
                    "×",
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1200f, 1200f),
                    new Color(0.16f, 0.18f, 0.2f, 1f),
                    1f);
				closeButton.gameObject.SetActive(false);

				CanvasGroup questsViewGroup = CreateStackCraftJournalView(
					"QuestsView",
					menuPanel,
					uiSprite,
					showByDefault: true,
					out RectTransform questsView,
					out ScrollRect questsScrollRect);
				TextMeshProUGUI questsContent = CreateStackCraftJournalContent(
					"QuestContent",
					questsView,
					questsScrollRect,
					fontAsset,
					"暂无任务");
				CanvasGroup actionsViewGroup = CreateStackCraftJournalView(
					"RecipesView",
					menuPanel,
					uiSprite,
					showByDefault: false,
					out RectTransform actionsView,
					out ScrollRect actionsScrollRect);
				TextMeshProUGUI actionsContent = CreateStackCraftJournalContent(
					"RecipeContent",
					actionsView,
					actionsScrollRect,
					fontAsset,
					"暂无已发现配方");

                ScenarioJournalPanel panel = root.GetComponent<ScenarioJournalPanel>();
                SerializedObject serializedPanel = new(panel);
				RequireProperty(serializedPanel, "m_menuPanel").objectReferenceValue = menuPanel;
				RequireProperty(serializedPanel, "m_questsTabToggle").objectReferenceValue = questsTab;
				RequireProperty(serializedPanel, "m_actionsTabToggle").objectReferenceValue = actionsTab;
				RequireProperty(serializedPanel, "m_menuToggleButton").objectReferenceValue = menuToggle;
				RequireProperty(serializedPanel, "m_menuToggleLabel").objectReferenceValue = menuToggleLabel;
				RequireProperty(serializedPanel, "m_headerGroup").objectReferenceValue =
					headerObject.GetComponent<CanvasGroup>();
				RequireProperty(serializedPanel, "m_questsViewGroup").objectReferenceValue = questsViewGroup;
				RequireProperty(serializedPanel, "m_actionsViewGroup").objectReferenceValue = actionsViewGroup;
				RequireProperty(serializedPanel, "m_questsContentLabel").objectReferenceValue = questsContent;
				RequireProperty(serializedPanel, "m_actionsContentLabel").objectReferenceValue = actionsContent;
				RequireProperty(serializedPanel, "m_closeButton").objectReferenceValue = closeButton;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
				SetLayerRecursively(root, UnityUiLayer);

                if (PrefabUtility.SaveAsPrefabAsset(root, ScenarioJournalPanelPrefabPath) == null)
                {
                    throw new MissingReferenceException(
                        $"无法保存剧本日志测试面板：{ScenarioJournalPanelPrefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

		private static Toggle CreateStackCraftJournalTabToggle(
			string objectName,
			Transform parent,
			Sprite sprite,
			TMP_FontAsset fontAsset,
			ToggleGroup group,
			string label,
			bool isOn)
		{
			GameObject buttonObject = new(
				objectName,
				typeof(RectTransform),
				typeof(Image),
				typeof(Toggle));
			buttonObject.transform.SetParent(parent, false);
			RectTransform rect = buttonObject.GetComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.zero;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = Vector2.zero;
			rect.sizeDelta = Vector2.zero;
			rect.localScale = Vector3.one;
			rect.localRotation = Quaternion.identity;
			Image image = buttonObject.GetComponent<Image>();
			image.sprite = sprite;
			image.color = new Color(0.21933962f, 0.6486481f, 0.8773585f, 1f);
			TextMeshProUGUI text = CreatePanelText("Label", rect, fontAsset, label, 32f);
			SetStretchRect(text.rectTransform, 0f);
			text.rectTransform.anchoredPosition = new Vector2(0.000061035156f, 0f);
			ApplyStackCraftTextParameters(
				text,
				fontSizeBase: 32f,
				HorizontalAlignmentOptions.Center,
				VerticalAlignmentOptions.Middle,
				Vector4.zero);
			Toggle toggle = buttonObject.GetComponent<Toggle>();
			toggle.targetGraphic = text;
			toggle.graphic = image;
			toggle.group = group;
			toggle.isOn = isOn;
			ColorBlock colors = toggle.colors;
			colors.selectedColor = Color.white;
			toggle.colors = colors;
			return toggle;
		}

		private static Button CreateStackCraftJournalMenuButton(
			string objectName,
			Transform parent,
			Sprite sprite,
			TMP_FontAsset fontAsset)
		{
			Button button = CreateSavePanelButton(
				objectName,
				parent,
				sprite,
				fontAsset,
				">>",
				new Vector2(0f, 1f),
				new Vector2(0f, 1f),
				new Vector2(60f, 60f),
				new Vector2(-60f, -60f),
				new Color(0f, 0f, 0f, 0.9019608f),
				32f);
			Image image = button.GetComponent<Image>();
			image.type = Image.Type.Simple;
			TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
			if (label == null)
			{
				throw new MissingReferenceException("StackCraft 剧本日志折叠按钮缺少文字组件。");
			}

			SetStretchRect(label.rectTransform, 0f);
			ApplyStackCraftTextParameters(
				label,
				fontSizeBase: 32f,
				HorizontalAlignmentOptions.Center,
				VerticalAlignmentOptions.Middle,
				Vector4.zero);
			button.targetGraphic = label;
			return button;
		}

		private static CanvasGroup CreateStackCraftJournalView(
			string objectName,
			Transform parent,
			Sprite sprite,
			bool showByDefault,
			out RectTransform viewportRect,
			out ScrollRect scrollRect)
		{
			GameObject viewObject = new(
				objectName,
				typeof(RectTransform),
				typeof(Image),
				typeof(ScrollRect),
				typeof(CanvasGroup));
			viewObject.transform.SetParent(parent, false);
			RectTransform viewRect = viewObject.GetComponent<RectTransform>();
			viewRect.anchorMin = Vector2.zero;
			viewRect.anchorMax = Vector2.one;
			viewRect.pivot = new Vector2(0.5f, 0.5f);
			viewRect.anchoredPosition = new Vector2(0f, -30f);
			viewRect.sizeDelta = new Vector2(0f, -60f);
			viewRect.localScale = Vector3.one;
			viewRect.localRotation = Quaternion.identity;
			Image image = viewObject.GetComponent<Image>();
			image.sprite = sprite;
			image.type = Image.Type.Sliced;
			image.color = new Color(0f, 0f, 0f, 0.9019608f);
			CanvasGroup group = viewObject.GetComponent<CanvasGroup>();
			group.alpha = showByDefault ? 1f : 0f;
			group.interactable = true;
			group.blocksRaycasts = showByDefault;

			GameObject viewportObject = new(
				"Viewport",
				typeof(RectTransform),
				typeof(Image),
				typeof(Mask));
			viewportObject.transform.SetParent(viewRect, false);
			viewportRect = viewportObject.GetComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.pivot = new Vector2(0f, 1f);
			viewportRect.anchoredPosition = Vector2.zero;
			viewportRect.sizeDelta = Vector2.zero;
			viewportRect.localScale = Vector3.one;
			viewportRect.localRotation = Quaternion.identity;
			Image viewportImage = viewportObject.GetComponent<Image>();
			viewportImage.sprite = sprite;
			viewportImage.type = Image.Type.Sliced;
			viewportImage.color = Color.white;
			Mask viewportMask = viewportObject.GetComponent<Mask>();
			viewportMask.showMaskGraphic = false;

			scrollRect = viewObject.GetComponent<ScrollRect>();
			scrollRect.viewport = viewportRect;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Elastic;
			scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
			scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
			scrollRect.horizontalScrollbarSpacing = -3f;
			scrollRect.verticalScrollbarSpacing = -3f;
			return group;
		}

		private static TextMeshProUGUI CreateStackCraftJournalContent(
			string objectName,
			RectTransform parent,
			ScrollRect scrollRect,
			TMP_FontAsset fontAsset,
			string text)
		{
			TextMeshProUGUI content = CreatePanelText(objectName, parent, fontAsset, text, 30f);
			content.alignment = TextAlignmentOptions.TopLeft;
			content.textWrappingMode = TextWrappingModes.Normal;
			content.lineSpacing = 0f;
			ApplyStackCraftTextParameters(
				content,
				fontSizeBase: 30f,
				HorizontalAlignmentOptions.Left,
				VerticalAlignmentOptions.Top,
				Vector4.zero);
			RectTransform contentRect = content.rectTransform;
			contentRect.anchorMin = new Vector2(0f, 1f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.pivot = new Vector2(0f, 1f);
			contentRect.anchoredPosition = Vector2.zero;
			contentRect.sizeDelta = Vector2.zero;
			VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
			layout.padding.left = 20;
			layout.padding.right = 20;
			layout.padding.top = 10;
			layout.padding.bottom = 10;
			layout.childAlignment = TextAnchor.UpperLeft;
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = false;
			layout.spacing = 10f;
			ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
			contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			scrollRect.content = contentRect;
			return content;
		}

        private static TextMeshProUGUI CreatePanelText(
            string objectName,
            Transform parent,
            TMP_FontAsset fontAsset,
            string text,
            float fontSize)
        {
            TMP_FontAsset previousDefaultFont = TMP_Settings.instance == null
                ? null
                : TMP_Settings.defaultFontAsset;
            bool replacedDefaultFont = TMP_Settings.instance != null && fontAsset != null;
            if (replacedDefaultFont)
            {
                TMP_Settings.defaultFontAsset = fontAsset;
            }

            try
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
            finally
            {
                if (replacedDefaultFont)
                {
                    TMP_Settings.defaultFontAsset = previousDefaultFont;
                }
            }
        }

		private static Image CreateStatsIcon(string objectName, Transform parent, Sprite sprite)
		{
			GameObject iconObject = new(objectName, typeof(RectTransform), typeof(Image));
			iconObject.transform.SetParent(parent, false);
			RectTransform iconRect = iconObject.GetComponent<RectTransform>();
			SetAnchoredRect(
				iconRect,
				Vector2.zero,
				new Vector2(1f, 0.5f),
				new Vector2(0f, 30f),
				Vector2.zero);
			Image icon = iconObject.GetComponent<Image>();
			icon.sprite = sprite;
			icon.preserveAspect = true;
			icon.raycastTarget = false;
			return icon;
		}

		private static TextMeshProUGUI CreateStatsLabel(
			string objectName,
			Transform parent,
			TMP_FontAsset fontAsset,
			string text)
		{
			TextMeshProUGUI label = CreatePanelText(objectName, parent, fontAsset, text, 30f);
			SetAnchoredRect(
				label.rectTransform,
				Vector2.zero,
				new Vector2(0.5f, 0.5f),
				new Vector2(0f, 30f),
				Vector2.zero);
			ApplyStackCraftTextParameters(
				label,
				fontSizeBase: 30f,
				HorizontalAlignmentOptions.Left,
				VerticalAlignmentOptions.Middle,
				Vector4.zero);
			return label;
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

		private static void SetLayerRecursively(GameObject root, int layer)
		{
			root.layer = layer;
			foreach (Transform child in root.transform)
			{
				SetLayerRecursively(child.gameObject, layer);
			}
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

		private static TabletopView CreateTabletopTestRoot(
			Transform parent,
			TabletopViewSettings viewSettings,
			string scenarioContentId,
			FoundationTestInitialLayout initialLayout,
			uint authoritativeRandomSeedOverride)
		{
			viewSettings = LoadPersistentTabletopViewSettings();
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

            tabletopRoot.AddComponent<TabletopCardDragInput>();
			tabletopRoot.AddComponent<TabletopInteraction>();
			tabletopRoot.AddComponent<ScenarioPauseInput>();

            FoundationTestSceneHarness controller =
                tabletopRoot.AddComponent<FoundationTestSceneHarness>();
			SerializedObject serializedController = new(controller);
			RequireProperty(serializedController, "m_scenarioId")
				.FindPropertyRelative("m_value").stringValue = scenarioContentId;
			RequireProperty(serializedController, "m_initialLayout").enumValueIndex = (int)initialLayout;
			RequireProperty(serializedController, "m_authoritativeRandomSeedOverride").longValue =
				authoritativeRandomSeedOverride;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
			return tabletopView;
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

        private static void EnsureStackCraftSpriteCollector(BundleCollectorGroup group)
        {
            BundleCollector collector = group.Collectors.SingleOrDefault(candidate =>
                string.Equals(candidate.CollectPath, StackCraftSpriteFolder, System.StringComparison.Ordinal));
            if (collector == null)
            {
                collector = new BundleCollector();
                group.Collectors.Add(collector);
            }

            collector.CollectPath = StackCraftSpriteFolder;
            collector.CollectorGUID = AssetDatabase.AssetPathToGUID(StackCraftSpriteFolder);
            collector.CollectorType = ECollectorType.MainAssetCollector;
            collector.AddressRuleName = nameof(AddressByFolderAndFileName);
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
            HashSet<string> nonBuildScenes = new(
                new[]
                {
                    ScenePath,
					StackCraftParityScenePath,
                    MapScenePath,
                    SecondMapScenePath
                },
                StringComparer.OrdinalIgnoreCase);

            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(scene =>
                {
                    string scenePath = scene.path.Replace('\\', '/');
                    return SceneUtil.IsProjectScenePath(scenePath) &&
                        !nonBuildScenes.Contains(scenePath);
                })
                .ToArray();
        }
    }
}
