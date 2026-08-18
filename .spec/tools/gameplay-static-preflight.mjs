#!/usr/bin/env node
/**
 * gameplay-static-preflight — Gameplay / StackCraft 吸收进入 Unity 前的静态预检。
 *
 * 只做文件与源码扫描，不启动 Unity，不替代 Unity 编译、Prefab / Scene 回读或 PlayMode。
 * 用法：node .spec/tools/gameplay-static-preflight.mjs [仓库根目录]
 */
import fs from "node:fs";
import path from "node:path";

const root = process.argv[2] ? path.resolve(process.argv[2]) : process.cwd();
const errors = [];
const warnings = [];

function fail(message) {
  errors.push(message);
}

function warn(message) {
  warnings.push(message);
}

function rel(file) {
  return path.relative(root, file).replaceAll(path.sep, "/");
}

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function readIfExists(relativePath) {
  const absolute = path.join(root, relativePath);
  return fs.existsSync(absolute) ? fs.readFileSync(absolute, "utf8") : null;
}

function unityEscapedString(value) {
  return value.replace(/[^\x20-\x7e]/g, (char) =>
    "\\u" + char.charCodeAt(0).toString(16).toUpperCase().padStart(4, "0"));
}

function unityUnescapedString(value) {
  return value.replace(/\\u([0-9a-fA-F]{4})/g, (_match, code) =>
    String.fromCharCode(Number.parseInt(code, 16)));
}

function collectorSettingHasCollectPath(settingText, assetPath) {
  if (settingText == null) return false;
  return settingText.includes(`CollectPath: ${assetPath}`) ||
    settingText.includes(`CollectPath: "${unityEscapedString(assetPath)}"`);
}

function tryReadText(file) {
  try {
    return fs.readFileSync(file, "utf8");
  } catch {
    return null;
  }
}

function walk(relativePath) {
  const absolute = path.join(root, relativePath);
  if (!fs.existsSync(absolute)) return [];
  const out = [];
  for (const entry of fs.readdirSync(absolute, { withFileTypes: true })) {
    const full = path.join(absolute, entry.name);
    if (entry.isDirectory()) out.push(...walk(rel(full)));
    else out.push(full);
  }
  return out;
}

function lineNumber(text, index) {
  return text.slice(0, index).split(/\r?\n/).length;
}

function scanFiles(files, patterns, label) {
  for (const file of files) {
    const text = fs.readFileSync(file, "utf8");
    for (const { pattern, message } of patterns) {
      pattern.lastIndex = 0;
      const match = pattern.exec(text);
      if (match) {
        fail(`${label}: ${rel(file)}:${lineNumber(text, match.index)} ${message}`);
      }
    }
  }
}

function csharpFiles(relativeRoots) {
  return relativeRoots
    .flatMap((relativeRoot) => walk(relativeRoot))
    .filter((file) => file.endsWith(".cs"));
}

function isTextReferenceFile(file) {
  const ext = path.extname(file).toLowerCase();
  return [
    ".asmdef",
    ".asset",
    ".cs",
    ".inputactions",
    ".json",
    ".mat",
    ".meta",
    ".prefab",
    ".shader",
    ".unity",
    ".uss",
    ".uxml",
  ].includes(ext);
}

function isIgnoredReferencePath(relativePath) {
  return (
    relativePath.startsWith("Assets/StackCraft/") ||
    relativePath.startsWith("Library/") ||
    relativePath.startsWith("Temp/") ||
    relativePath.startsWith("Logs/") ||
    relativePath.startsWith("UserSettings/") ||
    relativePath.startsWith("Packages/com.aibridge.unity/")
  );
}

const gameplayAndTestFiles = csharpFiles([
  "Assets/Scripts/Gameplay",
  "Assets/Editor/Gameplay",
  "Assets/Tests/Support",
  "Assets/Tests/PlayMode",
]);

const nonPlayModeImplementationFiles = csharpFiles([
  "Assets/Scripts/Gameplay",
  "Assets/Editor/Gameplay",
  "Assets/Tests/Support",
]);

const projectReferenceFiles = [
  "Assets",
  "ProjectSettings",
  "Packages",
]
  .flatMap((referenceRoot) => walk(referenceRoot))
  .filter((file) => {
    const relativePath = rel(file);
    return !isIgnoredReferencePath(relativePath) && isTextReferenceFile(file);
  });

scanFiles(
  projectReferenceFiles,
  [
    { pattern: /Assets\/StackCraft|Assets\\StackCraft/, message: "正式工程文本配置不得指向 StackCraft 参考目录。" },
    { pattern: /Assets\/Gameplay\/素材|Assets\\Gameplay\\素材/, message: "正式工程文本配置不得指向旧中文素材分类目录；使用 Assets/Art/Sprites、Assets/Art/Textures、Assets/Art/Materials、Assets/Art/Prefabs、Assets/Audio/SFX。" },
  ],
  "旧模板路径扫描",
);

if (exists("Assets/Gameplay/素材")) {
  fail("旧中文素材分类目录 Assets/Gameplay/素材 仍然存在；项目素材应按标准资源目录放入 Assets/Art/Sprites、Assets/Art/Textures、Assets/Art/Materials、Assets/Art/Prefabs、Assets/Audio/SFX。");
}

scanFiles(
  projectReferenceFiles.filter((file) => file.endsWith(".cs")),
  [
    { pattern: /\bCryingSnow\b/, message: "正式工程源码不得引用 StackCraft 命名空间。" },
  ],
  "旧模板命名空间扫描",
);

scanFiles(
  gameplayAndTestFiles,
  [
    { pattern: /Assets\/StackCraft|Assets\\StackCraft/, message: "正式 Gameplay / 测试链不得直接依赖 StackCraft 旧资源路径。" },
    { pattern: /\bCryingSnow\b/, message: "正式 Gameplay / 测试链不得引用 StackCraft 命名空间。" },
    { pattern: /Resources\.Load(?:All)?\s*\(/, message: "正式 Gameplay / 测试链不得恢复 Resources 资源扫描入口。" },
    { pattern: /\b(?:AudioId|AudioManager|CombatManager|ProjectileManager|HitUI|QuestManager|CraftingManager|GameData|SeenItems|MenuView|RecipesView|QuestsView)\b/, message: "旧模板 Manager / DTO / UI 名称回流。" },
  ],
  "旧模板结构扫描",
);

scanFiles(
  gameplayAndTestFiles,
  [
    { pattern: /\bGamePlay\b/, message: "正式命名使用 Gameplay，不新增 GamePlay 拼写。" },
    { pattern: /\bCardLoop\b/, message: "Gameplay 玩法链、测试和作者入口不使用项目名作为模块命名。" },
  ],
  "Gameplay 命名扫描",
);

scanFiles(
  gameplayAndTestFiles,
  [
    { pattern: /\bSetSurfaceSprite\b/, message: "StackCraft 卡面表面不得回退为 SpriteRenderer 底板；必须使用 MeshRenderer + Card shadergraph 材质链。" },
    { pattern: /\bm_artworkRenderer\b/, message: "StackCraft 卡面插画不得保留独立 SpriteRenderer 字段；插画必须写入卡面材质 _OverlayTex。" },
    { pattern: /\bm_artworkPadding\b/, message: "StackCraft 覆盖图比例由材质 _OverlayScale/_OverlayOffset 承载，不得保留运行时插画 padding 字段。" },
    { pattern: /new Vector3\s*\(\s*stackPosition\.x\s*,\s*stackPosition\.y\s*,\s*0f\s*\)/, message: "牌桌静态卡面不得回退到 XY 平面适配；二维牌桌坐标必须经 TabletopCoordinateSpace 映射到 StackCraft XZ 桌面。" },
    { pattern: /new Plane\s*\(\s*tablePlane\.forward\s*,/, message: "牌桌射线投影不得使用 XY 平面 forward 法线；必须使用 StackCraft XZ 桌面的 tablePlane.up。" },
    { pattern: /\bm_stackDepthStep\b/, message: "牌堆视觉步进不得再用 Z 深度字段；StackCraft stackStep.y = 0.002 必须写入 Unity Y 抬升字段 m_stackHeightStep。" },
  ],
  "StackCraft 卡面表面回流扫描",
);

scanFiles(
  nonPlayModeImplementationFiles,
  [
    { pattern: /\bFindObjectOfType(?:<|\s*\()/, message: "正式实现 / 测试支撑不得用全局对象查找作为依赖入口。" },
    { pattern: /\bFindObjectsOfType(?:<|\s*\()/, message: "正式实现 / 测试支撑不得用全局对象查找作为依赖入口。" },
    { pattern: /\bGameObject\.Find\s*\(/, message: "正式实现 / 测试支撑不得按名字全局查找依赖。" },
    { pattern: /\.Find\s*\(\s*"/, message: "正式实现 / 测试支撑不得用 Transform.Find 字符串查找依赖。" },
    { pattern: /\bFindWithTag\s*\(/, message: "正式实现 / 测试支撑不得用标签全局查找依赖。" },
    { pattern: /\bCamera\.main\b/, message: "正式实现 / 测试支撑不得用 Camera.main 获取唯一相机依赖。" },
    { pattern: /\bSendMessage\s*\(/, message: "正式实现 / 测试支撑不得使用 SendMessage 隐式调用。" },
  ],
  "正式依赖入口扫描",
);

const projectileMetaPath = "Assets/Gameplay/Tests/牌桌/牌桌测试投射物.prefab.meta";
const hitResultMetaPath = "Assets/Gameplay/Tests/牌桌/牌桌测试命中结果.prefab.meta";
const tabletopSettingsPath = "Assets/Gameplay/Tests/牌桌/牌桌测试视图设置.asset";
if (exists(projectileMetaPath) && exists(tabletopSettingsPath)) {
  const meta = read(projectileMetaPath);
  const settings = read(tabletopSettingsPath);
  const guid = meta.match(/guid:\s*([0-9a-f]{32})/)?.[1];
  if (!guid) {
    fail(`${projectileMetaPath} 缺少合法 GUID。`);
  } else if (!settings.includes(guid)) {
    fail(`${tabletopSettingsPath} 没有引用投射物 Prefab GUID ${guid}。`);
  }
  if (!settings.includes("m_projectileViewPrefab:")) {
    fail(`${tabletopSettingsPath} 缺少 m_projectileViewPrefab。`);
  }
  if (!settings.includes("m_projectileSortingOrder: 140")) {
    fail(`${tabletopSettingsPath} 缺少 m_projectileSortingOrder: 140。`);
  }
  if (exists(hitResultMetaPath)) {
    const hitResultMeta = read(hitResultMetaPath);
    const hitResultGuid = hitResultMeta.match(/guid:\s*([0-9a-f]{32})/)?.[1];
    if (!hitResultGuid) {
      fail(`${hitResultMetaPath} 缺少合法 GUID。`);
    } else if (!settings.includes(hitResultGuid)) {
      fail(`${tabletopSettingsPath} 没有引用命中结果 Prefab GUID ${hitResultGuid}。`);
    }
  } else {
    fail(`缺少命中结果测试 Prefab meta：${hitResultMetaPath}。`);
  }
  if (!settings.includes("m_hitResultViewPrefab:")) {
    fail(`${tabletopSettingsPath} 缺少 m_hitResultViewPrefab。`);
  }
  if (!settings.includes("m_hitResultSortingOrder: 160")) {
    fail(`${tabletopSettingsPath} 缺少 m_hitResultSortingOrder: 160。`);
  }
  if (settings.includes("m_stackDepthStep:")) {
    fail(`${tabletopSettingsPath} 仍保留旧 m_stackDepthStep；StackCraft 堆叠高度必须由 m_stackHeightStep 承载。`);
  }
  if (!settings.includes("m_stackHeightStep: 0.002")) {
    fail(`${tabletopSettingsPath} 缺少 StackCraft stackStep.y 对应的 m_stackHeightStep: 0.002。`);
  }
  if (!settings.includes("m_dragFollowSharpness: 100")) {
    fail(`${tabletopSettingsPath} 缺少 StackCraft swaySharpness 对应的 m_dragFollowSharpness: 100。`);
  }
  if (!settings.includes("m_moveDurationSeconds: 0.1")) {
    fail(`${tabletopSettingsPath} 缺少 StackCraft moveDuration 对应的 m_moveDurationSeconds: 0.1。`);
  }
} else {
  warn("未找到投射物测试 Prefab 或牌桌测试视图设置，跳过投射物资源引用检查。");
}

const collectorSettingText = readIfExists("Assets/BundleCollectorSetting.asset");
if (collectorSettingText == null) {
  fail("缺少 Assets/BundleCollectorSetting.asset，无法证明 StackCraft 表面素材进入 ResourceSystem / YooAsset 正式地址。");
} else {
  if (!collectorSettingText.includes("CollectPath: Assets/Art/Sprites/StackCraft")) {
    fail("YooAsset 收集配置缺少 Assets/Art/Sprites/StackCraft 收集器，StackCraft 卡面插画和贴图无法由 ResourceSystem 加载。");
  }
  if (!collectorSettingText.includes("AddressRuleName: AddressByFolderAndFileName")) {
    fail("StackCraft 图片素材收集器必须使用 AddressByFolderAndFileName，保证 CardArts/Villager.png 等地址稳定。");
  }
}

const expectedCardSurfaceAddresses = [
  "卡牌表面_角色",
  "卡牌表面_生物",
  "卡牌表面_主动敌人",
  "卡牌表面_消耗品",
  "卡牌表面_货币",
  "卡牌表面_装备",
  "卡牌表面_材料",
  "卡牌表面_配方",
  "卡牌表面_资源",
  "卡牌表面_建筑",
  "卡牌表面_贵重物",
  "卡牌表面_地区",
];

const expectedCardSurfaceMaterialFiles = [
  "Assets/Art/Materials/卡牌表面_角色.mat",
  "Assets/Art/Materials/卡牌表面_生物.mat",
  "Assets/Art/Materials/卡牌表面_主动敌人.mat",
  "Assets/Art/Materials/卡牌表面_消耗品.mat",
  "Assets/Art/Materials/卡牌表面_货币.mat",
  "Assets/Art/Materials/卡牌表面_装备.mat",
  "Assets/Art/Materials/卡牌表面_材料.mat",
  "Assets/Art/Materials/卡牌表面_配方.mat",
  "Assets/Art/Materials/卡牌表面_资源.mat",
  "Assets/Art/Materials/卡牌表面_建筑.mat",
  "Assets/Art/Materials/卡牌表面_贵重物.mat",
  "Assets/Art/Materials/卡牌表面_地区.mat",
];

const expectedCardSurfaceTextureFiles = [
  "Assets/Art/Sprites/StackCraft/Cards/Character.png",
  "Assets/Art/Sprites/StackCraft/Cards/Mob.png",
  "Assets/Art/Sprites/StackCraft/Cards/Mob_Aggressive.png",
  "Assets/Art/Sprites/StackCraft/Cards/Consumable.png",
  "Assets/Art/Sprites/StackCraft/Cards/Currency.png",
  "Assets/Art/Sprites/StackCraft/Cards/Equipment.png",
  "Assets/Art/Sprites/StackCraft/Cards/Material.png",
  "Assets/Art/Sprites/StackCraft/Cards/Recipe.png",
  "Assets/Art/Sprites/StackCraft/Cards/Resource.png",
  "Assets/Art/Sprites/StackCraft/Cards/Structure.png",
  "Assets/Art/Sprites/StackCraft/Cards/Valuable.png",
  "Assets/Art/Sprites/StackCraft/Cards/Area.png",
];

for (const surfaceFile of expectedCardSurfaceTextureFiles) {
  if (!exists(surfaceFile)) {
    fail(`缺少 StackCraft 卡牌分族贴图自有副本：${surfaceFile}`);
  }
  if (!exists(`${surfaceFile}.meta`)) {
    fail(`缺少 StackCraft 卡牌分族贴图自有副本 meta：${surfaceFile}.meta`);
  }
}

for (const surfaceFile of expectedCardSurfaceMaterialFiles) {
  if (!exists(surfaceFile)) {
    fail(`缺少 StackCraft 卡牌类别材质自有副本：${surfaceFile}`);
  }
  if (!exists(`${surfaceFile}.meta`)) {
    fail(`缺少 StackCraft 卡牌类别材质自有副本 meta：${surfaceFile}.meta`);
  }
  if (collectorSettingText != null && !collectorSettingHasCollectPath(collectorSettingText, surfaceFile)) {
    fail(`YooAsset 收集配置缺少卡牌类别材质：${surfaceFile}`);
  }
}

if (!exists("Assets/Art/Models/卡牌.fbx") || !exists("Assets/Art/Models/卡牌.fbx.meta")) {
  fail("缺少 StackCraft Card.fbx 自有副本：Assets/Art/Models/卡牌.fbx。");
}

if (!exists("Assets/Art/Shaders/卡牌表面.shadergraph") || !exists("Assets/Art/Shaders/卡牌表面.shadergraph.meta")) {
  fail("缺少 StackCraft Card.shadergraph 自有副本：Assets/Art/Shaders/卡牌表面.shadergraph。");
}

const tabletopGeometrySource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Placement/TabletopCardPlacementContracts.cs");
if (tabletopGeometrySource == null) {
  fail("缺少牌桌放置几何源码，无法证明卡牌可见尺寸和占地 margin 已拆分。");
} else {
  for (const token of [
    "private Vector2 m_cardSize = new Vector2(0.8f, 1f);",
    "private Vector2 m_cardMargin = new Vector2(0.1f, 0.1f);",
    "private Vector2 m_stackStep = new Vector2(0f, -0.18f);",
    "public Vector2 CardMargin { get; }",
    "public Vector2 FootprintSize { get; }",
    "FootprintSize = cardSize + cardMargin;",
  ]) {
    if (!tabletopGeometrySource.includes(token)) {
      fail(`牌桌放置几何没有保留 StackCraft 静态表面语义：${token}`);
    }
  }
  if (tabletopGeometrySource.includes("Vector2 size = CardSize + new Vector2(Mathf.Abs(span.x), Mathf.Abs(span.y));")) {
    fail("牌桌放置几何仍把可见卡牌尺寸直接当占地尺寸，缺少 StackCraft margin 语义。");
  }
}

const tabletopCoordinateSpaceSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/TabletopCoordinateSpace.cs");
if (tabletopCoordinateSpaceSource == null) {
  fail("缺少牌桌坐标唯一映射入口 TabletopCoordinateSpace；无法证明 StackCraft XZ 桌面没有被再次适配成 XY。");
} else {
  for (const token of [
    "return new Vector3(tablePosition.x, height, tablePosition.y);",
    "return new Vector2(localPosition.x, localPosition.z);",
    "return new Plane(tableTransform.up, tableTransform.position);",
  ]) {
    if (!tabletopCoordinateSpaceSource.includes(token)) {
      fail(`牌桌坐标映射入口没有保留 StackCraft XZ 桌面语义：${token}`);
    }
  }
}

const tabletopLayoutSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopCardLayout.cs");
if (tabletopLayoutSource == null) {
  fail("缺少牌桌卡牌布局源码，无法证明堆叠位置按 StackCraft XZ 桌面投影。");
} else if (!tabletopLayoutSource.includes("TabletopCoordinateSpace.ToLocalPosition(stackPosition)")) {
  fail("牌桌卡牌布局没有通过 TabletopCoordinateSpace 映射二维牌桌坐标。");
}

const tabletopViewSettingsSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/TabletopViewSettings.cs");
if (tabletopViewSettingsSource == null) {
  fail("缺少牌桌视图设置源码，无法证明堆叠视觉步进来自 StackCraft 参数。");
} else {
  for (const token of [
    "private float m_stackHeightStep = 0.002f;",
    "private float m_moveDurationSeconds = 0.1f;",
    "new Vector3(geometry.StackStep.x, m_stackHeightStep, geometry.StackStep.y)",
  ]) {
    if (!tabletopViewSettingsSource.includes(token)) {
      fail(`牌桌视图设置没有保留 StackCraft stackStep = (0, 0.002, -0.18) 语义：${token}`);
    }
  }
}

const tabletopSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Tabletop.cs");
if (tabletopSource == null) {
  fail("缺少牌桌聚合根源码，无法证明投射物飞行结算时长。");
} else if (!tabletopSource.includes("private const float ProjectileAttackPreActivationSeconds = 0.5f;")) {
  fail("牌桌远程 / 魔法投射物飞行时长未对齐 StackCraft Projectile Prefab 的 duration: 0.5。");
}

const projectileViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopProjectileView.cs");
if (projectileViewSource == null) {
  fail("缺少投射物视图源码，无法证明投射物朝向公式。");
} else {
  for (const token of [
    "direction.y = 0f;",
    "Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(90f, 0f, 0f)",
  ]) {
    if (!projectileViewSource.includes(token)) {
      fail(`投射物视图没有对齐 StackCraft CombatProjectile.Fire 朝向公式：${token}`);
    }
  }
}

const stackCraftRegionGeometryPaths = [
  "Assets/Gameplay/Tests/地基测试地区.asset",
  "Assets/Gameplay/Tests/地基场景测试地区.asset",
  "Assets/Gameplay/Tests/地基第二场景测试地区.asset",
  "Assets/Gameplay/Tests/地基战斗测试地区.asset",
];

for (const regionPath of stackCraftRegionGeometryPaths) {
  const regionText = readIfExists(regionPath);
  if (regionText == null) {
    fail(`缺少牌桌测试地区作者源：${regionPath}`);
    continue;
  }
  for (const token of [
    "m_cardSize: {x: 0.8, y: 1}",
    "m_cardMargin: {x: 0.1, y: 0.1}",
    "m_stackStep: {x: 0, y: -0.18}",
  ]) {
    if (!regionText.includes(token)) {
      fail(`${regionPath} 未对齐 StackCraft 卡牌静态几何：${token}`);
    }
  }
}

const foundationVillagerText = readIfExists("Assets/Gameplay/Tests/地基测试卡牌.asset");
if (foundationVillagerText == null) {
  fail("缺少地基测试 Villager 作者源，无法证明基础卡面数值来自 StackCraft。");
} else {
  for (const token of [
    "m_displayName: Villager",
    "m_description: A healthy villager.",
    "m_baseValue: 15",
    "m_baseValue: 2",
    "m_baseValue: 1",
    "m_baseValue: 130",
    "m_baseValue: 95",
    "m_baseValue: 5",
    "m_baseValue: 150",
  ]) {
    if (!foundationVillagerText.includes(token)) {
      fail(`地基测试 Villager 没有保留 StackCraft 原始静态/战斗数值：${token}`);
    }
  }
  if (foundationVillagerText.includes("m_attributeOverrides: []")) {
    fail("地基测试 Villager 仍使用默认 ASC 数值，会在卡面显示 100 而不是 StackCraft 原始 15。");
  }
}

const testContentAssets = walk("Assets/Gameplay/Tests").filter((file) => file.endsWith(".asset"));
for (const file of testContentAssets) {
  const text = tryReadText(file);
  if (text == null || !text.includes("m_cardSurface:")) continue;

  const match = text.match(/m_cardSurface:\s*\r?\n\s+Address:\s*"?([^"\r\n]*)"?/);
  if (!match) {
    fail(`${rel(file)} 有 m_cardSurface 但没有可读 Address。`);
    continue;
  }

  const address = unityUnescapedString(match[1].trim());
  if (!expectedCardSurfaceAddresses.includes(address)) {
    fail(`${rel(file)} 的卡牌表面地址 ${address || "(空)"} 不在 StackCraft 分族表面地址白名单内。`);
  }
}

const cardViewPrefabText = readIfExists("Assets/Gameplay/Tests/牌桌/牌桌测试卡牌视图.prefab");
const tabletopCardViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopCardView.cs");

if (tabletopCardViewSource == null) {
  fail("缺少牌桌卡牌视图源码，无法证明受击闪白 / 摇晃由正式视图承接。");
} else {
  for (const token of [
    "using DG.Tweening;",
    "DOLocalMove(pose.LocalPosition, durationSeconds)",
    "SetEase(Ease.OutQuad)",
    "DOTween.To(",
    "SetDelay(m_hurtFlashDelaySeconds)",
    "SetLoops(m_hurtFlashLoopCount, LoopType.Yoyo)",
    "transform.DOPunchRotation(",
    "new Vector3(0f, m_hurtPunchRotationDegrees, 0f)",
    "m_hurtPunchDurationSeconds",
    "m_hurtPunchVibrato",
    "DOTween.Sequence()",
    ".SetUpdate(true)",
    ".SetLink(gameObject, LinkBehaviour.KillOnDisable)",
  ]) {
    if (!tabletopCardViewSource.includes(token)) {
      fail(`牌桌卡牌视图没有用 DOTween 精确承接 StackCraft 受击反馈参数：${token}`);
    }
  }
  for (const obsoleteToken of [
    "m_isMovingToPose",
    "m_moveElapsedSeconds",
    "m_moveStartLocalPosition",
    "m_moveTargetLocalPosition",
    "Vector3.LerpUnclamped(",
    "m_hurtFeedbackElapsedSeconds",
    "CalculateHurtFlashAmount",
    "HurtFeedbackDurationSeconds",
    "EaseOutQuad",
    "Mathf.Sin(punchNormalized",
  ]) {
    if (tabletopCardViewSource.includes(obsoleteToken)) {
      fail(`牌桌卡牌视图仍保留手写受击动画近似算法，应使用 DOTween 参数闭包：${obsoleteToken}`);
    }
  }
}

function unityGuid(metaText) {
  return metaText?.match(/^guid:\s*([0-9a-f]{32})/m)?.[1] ?? null;
}

for (const obsolete of [
  "Assets/Art/Shaders/卡牌插画黑底透明.shader",
  "Assets/Art/Shaders/卡牌插画黑底透明.shader.meta",
  "Assets/Art/Materials/卡牌插画覆盖材质.mat",
  "Assets/Art/Materials/卡牌插画覆盖材质.mat.meta",
]) {
  if (exists(obsolete)) {
    fail(`旧 SpriteRenderer 卡面模拟资产仍存在，应删除避免第二套卡面真相：${obsolete}`);
  }
}

const cardSurfaceShaderGuid = unityGuid(readIfExists("Assets/Art/Shaders/卡牌表面.shadergraph.meta"));
if (cardSurfaceShaderGuid == null) {
  fail("Assets/Art/Shaders/卡牌表面.shadergraph.meta 缺少合法 GUID。");
}

const cardOutlineShaderGuid = unityGuid(readIfExists("Assets/Art/Shaders/卡牌轮廓.shadergraph.meta"));
if (cardOutlineShaderGuid == null) {
  fail("Assets/Art/Shaders/卡牌轮廓.shadergraph.meta 缺少合法 GUID。");
}

const cardMeshGuid = unityGuid(readIfExists("Assets/Art/Models/卡牌.fbx.meta"));
if (cardMeshGuid == null) {
  fail("Assets/Art/Models/卡牌.fbx.meta 缺少合法 GUID。");
}

const defaultCardSurfaceMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/卡牌表面_角色.mat.meta"));
if (defaultCardSurfaceMaterialGuid == null) {
  fail("Assets/Art/Materials/卡牌表面_角色.mat.meta 缺少合法 GUID。");
}

for (const materialFile of expectedCardSurfaceMaterialFiles) {
  const materialText = readIfExists(materialFile);
  if (materialText == null) continue;
  if (cardSurfaceShaderGuid != null && !materialText.includes(cardSurfaceShaderGuid)) {
    fail(`${materialFile} 没有引用自有 StackCraft Card shadergraph GUID ${cardSurfaceShaderGuid}。`);
  }
  for (const token of [
    "- _BaseTex:",
    "- _MainTex:",
    "- _OverlayTex:",
    "- _FlashAmount: 0",
    "- _OverlayScale: 0.8",
    "- _OverlayOffset:",
    "- _OverlayTint:",
  ]) {
    if (!materialText.includes(token)) {
      fail(`${materialFile} 缺少 StackCraft Card 材质参数：${token}`);
    }
  }
}

if (cardViewPrefabText == null) {
  fail("缺少牌桌测试卡牌视图 Prefab，无法证明卡牌表面、标题和受击反馈静态承载。");
} else {
  for (const token of [
    "MeshFilter:",
    "MeshRenderer:",
    "m_surfaceRenderer:",
    "m_surfaceTextureProperty: _OverlayTex",
    "m_surfaceFlashProperty: _FlashAmount",
    "m_titleLabel:",
    "m_priceLabel:",
    "m_nutritionLabel:",
    "m_usesLabel:",
    "m_hurtFlashDelaySeconds: 0.05",
    "m_hurtFlashTweenSeconds: 0.1",
    "m_hurtFlashLoopCount: 2",
    "m_hurtPunchRotationDegrees: 15",
    "m_hurtPunchDurationSeconds: 0.25",
    "m_hurtPunchVibrato: 25",
  ]) {
    if (!cardViewPrefabText.includes(token)) {
      fail(`牌桌测试卡牌视图 Prefab 缺少静态表面 / 受击反馈字段：${token}`);
    }
  }
  for (const obsoleteHitToken of [
    "m_battleResultLabel:",
    "m_battleHitIconRenderer:",
    "m_battleEffectivenessIconRenderer:",
    "m_battleMissSprite:",
    "m_battleNormalHitSprite:",
    "m_battleCriticalHitSprite:",
    "m_battleAdvantageSprite:",
    "m_battleDisadvantageSprite:",
    "m_battleResultPunchScale:",
    "m_battleResultPunchDurationSeconds:",
    'm_Name: "\\u6218\\u6597\\u7ED3\\u679C"',
    'm_Name: "\\u547D\\u4E2D\\u56FE\\u6807"',
    'm_Name: "\\u514B\\u5236\\u56FE\\u6807"',
  ]) {
    if (cardViewPrefabText.includes(obsoleteHitToken)) {
      fail(`牌桌测试卡牌视图 Prefab 仍保留卡牌内部命中 UI 残留：${obsoleteHitToken}`);
    }
  }
  if (cardMeshGuid != null && !cardViewPrefabText.includes(cardMeshGuid)) {
    fail(`牌桌测试卡牌视图 Prefab 没有引用自有 StackCraft Card.fbx GUID ${cardMeshGuid}。`);
  }
  if (defaultCardSurfaceMaterialGuid != null && !cardViewPrefabText.includes(defaultCardSurfaceMaterialGuid)) {
    fail(`牌桌测试卡牌视图 Prefab 没有引用自有 StackCraft 角色卡面材质 GUID ${defaultCardSurfaceMaterialGuid}。`);
  }
  if (cardOutlineShaderGuid != null && !cardViewPrefabText.includes(cardOutlineShaderGuid)) {
    fail(`牌桌测试卡牌视图 Prefab 没有引用自有 StackCraft CardOutline shadergraph GUID ${cardOutlineShaderGuid}。`);
  }
  if (cardViewPrefabText.includes('m_Name: "\\u8868\\u9762\\u8BE6\\u60C5"')) {
    fail("牌桌测试卡牌视图 Prefab 仍保留旧的“表面详情”混合文本节点，应改为价格 / 营养分区。");
  }
  if (cardViewPrefabText.includes("m_Color: {r: 0.25, g: 0.95, b: 0.45, a: 0.42}")) {
    fail("牌桌测试卡牌视图 Prefab 仍保留整块绿色候选遮罩，应改为模板式外轮廓。");
  }
  if (!cardViewPrefabText.includes("m_Size: {x: 0.8, y: 0, z: 1.0000002}")) {
    fail("牌桌测试卡牌视图 Prefab 的碰撞盒没有对齐 StackCraft XZ 牌桌参数 0.8 × 0 × 1.0000002。");
  }
  const faceRotationCount = [...cardViewPrefabText.matchAll(/m_LocalEulerAnglesHint: \{x: 90, y: 0, z: 0\}/g)].length;
  if (faceRotationCount < 5) {
    fail("牌桌测试卡牌视图 Prefab 的卡面文字 / 子表现没有足够的 x=90 旋转，可能仍在使用 XY 平面适配。");
  }
  for (const token of [
    "m_LocalPosition: {x: 0, y: 0, z: 0.4}",
    "m_LocalPosition: {x: 0, y: 0, z: -0.355}",
    "m_LocalPosition: {x: 0, y: 0, z: -0.363}",
    "m_LocalPosition: {x: 0, y: 0, z: -0.345}",
    "m_AnchoredPosition: {x: 0.254, y: 0.001}",
  ]) {
    if (!cardViewPrefabText.includes(token)) {
      fail(`牌桌测试卡牌视图 Prefab 没有回写 StackCraft 卡面文字参数：${token}`);
    }
  }
  if (cardViewPrefabText.includes("m_artworkRenderer:") ||
      cardViewPrefabText.includes("m_artworkPadding:") ||
      cardViewPrefabText.includes('m_Name: "\\u5361\\u9762\\u63D2\\u753B"')) {
    fail("牌桌测试卡牌视图 Prefab 仍保留独立 SpriteRenderer 插画层；StackCraft 插画必须写入卡面材质 _OverlayTex。");
  }
  if (cardViewPrefabText.includes("m_artworkPadding: 0.86") || cardViewPrefabText.includes("m_artworkPadding: 0.62")) {
    fail("牌桌测试卡牌视图 Prefab 仍使用旧卡图占比 0.86/0.62，会挤压标题、价格、营养和生命数字。");
  }
  if (cardViewPrefabText.includes("m_Color: {r: 0.03, g: 0.05, b: 0.06, a: 0.92}")) {
    fail("牌桌测试卡牌视图 Prefab 仍保留旧黑色生命条，应改为 StackCraft 式右下生命数字。");
  }
  for (const obsoleteOutlineToken of [
    'm_Name: "\\u4E0A\\u8F6E\\u5ED3"',
    'm_Name: "\\u4E0B\\u8F6E\\u5ED3"',
    'm_Name: "\\u5DE6\\u8F6E\\u5ED3"',
    'm_Name: "\\u53F3\\u8F6E\\u5ED3"',
  ]) {
    if (cardViewPrefabText.includes(obsoleteOutlineToken)) {
      fail(`牌桌测试卡牌视图 Prefab 仍在用四条 Sprite 线框模拟高亮，应改为 StackCraft Mesh + CardOutline 材质：${obsoleteOutlineToken}`);
    }
  }
  if (!cardViewPrefabText.includes('m_Name: "Highlight"') &&
      !cardViewPrefabText.includes('m_Name: "\\u5019\\u9009\\u9AD8\\u4EAE"')) {
    fail("牌桌测试卡牌视图 Prefab 缺少 StackCraft Mesh 高亮节点。");
  }
}

const hitResultPrefabText = readIfExists("Assets/Gameplay/Tests/牌桌/牌桌测试命中结果.prefab");
if (hitResultPrefabText == null) {
  fail("缺少牌桌测试命中结果 Prefab，无法证明 参考模板命中结果 UI 静态承载。");
} else {
  for (const token of [
    "m_hitImage:",
    "m_effectivenessImage:",
    "m_damageLabel:",
    "m_missSprite:",
    "m_normalSprite:",
    "m_criticalSprite:",
    "m_advantageSprite:",
    "m_disadvantageSprite:",
    "m_punchScale: 0.15",
    "m_punchDurationSeconds: 1",
    "m_SizeDelta: {x: 0.4, y: 0.4}",
    "m_AnchoredPosition: {x: 0.15, y: 0}",
    "m_SizeDelta: {x: 0.15, y: 0.15}",
    "m_fontSize: 0.2",
  ]) {
    if (!hitResultPrefabText.includes(token)) {
      fail(`牌桌测试命中结果 Prefab 没有回写 参考模板命中结果 UI 参数：${token}`);
    }
  }
}

const progressPrefabText = readIfExists("Assets/Gameplay/Tests/牌桌/牌桌测试行动进度.prefab");
if (progressPrefabText == null) {
  fail("缺少牌桌测试行动进度 Prefab，无法证明行动进度条表面。");
} else {
  if (!progressPrefabText.includes("m_Color: {r: 0.25, g: 0.25, b: 0.25, a: 0.8}")) {
    fail("牌桌测试行动进度 Prefab 底板颜色未对齐 StackCraft 灰底。");
  }
  if (!progressPrefabText.includes("m_Color: {r: 1, g: 0.7974138, b: 0, a: 1}")) {
    fail("牌桌测试行动进度 Prefab 填充颜色未对齐 StackCraft 黄色。");
  }
  if (!progressPrefabText.includes("m_runningColor: {r: 1, g: 0.7974138, b: 0, a: 1}")) {
    fail("牌桌测试行动进度组件运行颜色未对齐 StackCraft 黄色。");
  }
}

const cardSmokePrefabText = readIfExists("Assets/Art/Prefabs/卡牌烟雾粒子.prefab");
if (cardSmokePrefabText == null) {
  fail("缺少卡牌烟雾粒子 Prefab，无法证明 StackCraft PuffParticle 表现闭包。");
} else {
  for (const token of [
    'm_Name: "\\u5361\\u724C\\u70DF\\u96FE\\u7C92\\u5B50"',
    "m_IsActive: 0",
    "lengthInSec: 1",
    "looping: 0",
    "playOnAwake: 0",
    "useUnscaledTime: 1",
    "m_SortingOrder: 150",
    "m_RenderMode: 2",
    "m_particleSystem:",
    "m_renderer:",
  ]) {
    if (!cardSmokePrefabText.includes(token)) {
      fail(`卡牌烟雾粒子 Prefab 没有回写 StackCraft PuffParticle 参数或自有生命周期字段：${token}`);
    }
  }
  if (cardSmokePrefabText.includes("m_RenderMode: 0")) {
    fail("卡牌烟雾粒子 Prefab 仍使用 Billboard；StackCraft PuffParticle 使用 HorizontalBillboard。");
  }
}

const projectilePrefabText = readIfExists("Assets/Gameplay/Tests/牌桌/牌桌测试投射物.prefab");
if (projectilePrefabText == null) {
  fail("缺少牌桌测试投射物 Prefab，无法证明箭矢 / 魔法投射物表面。");
} else {
  for (const token of ["m_rangedSprite:", "m_magicSprite:"]) {
    if (!projectilePrefabText.includes(token)) {
      fail(`牌桌测试投射物 Prefab 缺少 ${token}`);
    }
  }
  if (!projectilePrefabText.includes("m_Size: {x: 1.28, y: 1.28}")) {
    fail("牌桌测试投射物 SpriteRenderer 尺寸未对齐 StackCraft Projectile Prefab 的 1.28 × 1.28。");
  }
  if (projectilePrefabText.includes("m_LocalScale: {x: 0.28, y: 0.08, z: 1}")) {
    fail("牌桌测试投射物仍保留旧占位缩放，未使用 StackCraft 投射物图片自身比例。");
  }
  if (projectilePrefabText.includes("m_Size: {x: 0.25, y: 0.25}")) {
    fail("牌桌测试投射物仍保留 0.25 × 0.25 的默认 SpriteRenderer 尺寸，未对齐 StackCraft。");
  }
}

const uiClickMetaPath = "Assets/Audio/SFX/界面点击.wav.meta";
if (exists(uiClickMetaPath)) {
  const bytes = fs.readFileSync(path.join(root, uiClickMetaPath));
  if (bytes.length === 0 || bytes[bytes.length - 1] !== 10) {
    fail(`${uiClickMetaPath} 文件末尾不是 LF，Unity YAML 可能继续解析失败。`);
  }
}

const stackCraftScriptsRoot = "Assets/StackCraft/Scripts";
const stackCraftMatrixPath = ".spec/knowledge/features/project/stackcraft-system-reference-matrix.md";
if (exists(stackCraftScriptsRoot) && exists(stackCraftMatrixPath)) {
  const matrix = read(stackCraftMatrixPath);
  const missing = walk(stackCraftScriptsRoot)
    .filter((file) => file.endsWith(".cs"))
    .map((file) => path.basename(file, ".cs"))
    .filter((name) => !matrix.includes(name));
  for (const name of missing) {
    fail(`StackCraft 脚本 ${name}.cs 未在吸收矩阵中登记，不能进入完整等价验收。`);
  }
} else {
  warn("未找到 StackCraft 脚本目录或吸收矩阵，跳过参考脚本覆盖检查。");
}

const stackCraftRoot = "Assets/StackCraft";
if (exists(stackCraftRoot)) {
  const stackCraftGuids = new Map();
  for (const metaFile of walk(stackCraftRoot).filter((file) => file.endsWith(".meta"))) {
    const text = tryReadText(metaFile);
    const guid = text?.match(/^guid:\s*([0-9a-f]{32})/m)?.[1];
    if (guid) stackCraftGuids.set(guid, rel(metaFile));
  }

  if (stackCraftGuids.size === 0) {
    warn("未能从 Assets/StackCraft 读取任何 Unity GUID，跳过旧模板资产 GUID 引用检查。");
  } else {
    const guidPattern = /\b[0-9a-f]{32}\b/g;
    for (const file of projectReferenceFiles) {
      const relativePath = rel(file);
      const text = tryReadText(file);
      if (text == null) continue;

      guidPattern.lastIndex = 0;
      for (const match of text.matchAll(guidPattern)) {
        const source = stackCraftGuids.get(match[0]);
        if (!source) continue;
        fail(`旧模板资产 GUID 回流: ${relativePath}:${lineNumber(text, match.index ?? 0)} 引用了 ${source}。正式链路必须先迁入项目自有资源目录。`);
        break;
      }
    }
  }
} else {
  warn("未找到 Assets/StackCraft，跳过旧模板资产 GUID 引用检查。");
}

const standaloneProjectFiles = walk(".")
  .filter((file) => file.endsWith(".sln") || file.endsWith(".csproj"))
  .filter((file) => !rel(file).startsWith("Library/") && !rel(file).startsWith("Temp/"));
if (standaloneProjectFiles.length === 0) {
  warn("未发现 .sln / .csproj；C# 编译必须留到 Unity 编译阶段验证。");
}

for (const message of warnings) {
  console.warn(`gameplay-static-preflight warning: ${message}`);
}

if (errors.length > 0) {
  console.error("gameplay-static-preflight failed:");
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log("gameplay-static-preflight passed");
