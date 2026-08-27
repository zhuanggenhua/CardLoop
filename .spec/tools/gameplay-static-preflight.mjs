#!/usr/bin/env node
/**
 * gameplay-static-preflight — Gameplay / StackCraft 吸收进入 Unity 前的静态预检。
 *
 * 只做文件与源码扫描，不启动 Unity，不替代 Unity 编译、Prefab / Scene 回读或 PlayMode。
 * 用法：node .spec/tools/gameplay-static-preflight.mjs [仓库根目录]
 */
import fs from "node:fs";
import crypto from "node:crypto";
import path from "node:path";
import zlib from "node:zlib";

const positionalArgs = process.argv.slice(2).filter((arg) => !arg.startsWith("--"));
const root = positionalArgs[0] ? path.resolve(positionalArgs[0]) : process.cwd();
const errors = [];
const warnings = [];

function fail(message) {
  errors.push(message);
}

function warn(message) {
  warnings.push(message);
}

function auxiliary(message) {
  fail(`表面 / 反馈未对齐：${message}`);
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

function readBinaryIfExists(relativePath) {
  const absolute = path.join(root, relativePath);
  return fs.existsSync(absolute) ? fs.readFileSync(absolute) : null;
}

function sha256IfExists(relativePath) {
  const absolute = path.join(root, relativePath);
  return fs.existsSync(absolute)
    ? crypto.createHash("sha256").update(fs.readFileSync(absolute)).digest("hex")
    : null;
}

function assertSameFileHash(leftPath, rightPath, description) {
  const leftHash = sha256IfExists(leftPath);
  const rightHash = sha256IfExists(rightPath);
  if (leftHash == null) {
    fail(`缺少 ${description} 的 StackCraft 来源文件：${leftPath}`);
  }
  if (rightHash == null) {
    fail(`缺少 ${description} 的自有副本：${rightPath}`);
  }
  if (leftHash !== rightHash) {
    fail(`${description} 自有副本与 StackCraft 来源不一致：${leftPath} -> ${rightPath}`);
  }
}

function yamlPropertyLine(text, propertyName) {
  const escaped = propertyName.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return text.match(new RegExp(`^\\s*- ${escaped}:.*$`, "m"))?.[0].trim() ?? null;
}

function yamlMappingPropertyLine(text, propertyName) {
  const escaped = propertyName.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return text.match(new RegExp(`^\\s*${escaped}:.*$`, "m"))?.[0].trim() ?? null;
}

function assertYamlPropertyLinesMatch(sourceText, localText, sourcePath, localPath, propertyNames, label) {
  for (const propertyName of propertyNames) {
    const sourceLine = yamlPropertyLine(sourceText, propertyName);
    const localLine = yamlPropertyLine(localText, propertyName);
    if (sourceLine == null) {
      fail(`${sourcePath} 缺少 StackCraft ${label} 参数：${propertyName}`);
    } else if (localLine !== sourceLine) {
      fail(`${localPath} 的 ${propertyName} 与 StackCraft ${label} 不一致：${localLine ?? "<缺失>"}，应为 ${sourceLine}`);
    }
  }
}

function assertYamlPropertyLinesPresent(text, propertyNames, sourcePath, label) {
  for (const propertyName of propertyNames) {
    if (yamlPropertyLine(text, propertyName) == null) {
      fail(`${sourcePath} 缺少 ${label} 参数：${propertyName}`);
    }
  }
}

function materialTextureGuid(text, propertyName) {
  const escaped = propertyName.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return text.match(new RegExp(`^\\s*- ${escaped}:\\s*\\r?\\n\\s*m_Texture: \\{fileID: 2800000, guid: ([0-9a-f]{32}), type: 3\\}`, "m"))?.[1] ?? null;
}

function materialShaderReference(text, materialPath, label) {
  const match = text.match(/^  m_Shader: \{fileID: ([^,]+), guid: ([0-9a-f]{32}), type: ([0-9]+)\}$/m);
  if (match == null) {
    fail(`${materialPath} 缺少可回读的 m_Shader 引用，无法证明 ${label}。`);
    return null;
  }

  return {
    fileId: match[1],
    guid: match[2],
    type: match[3],
  };
}

function assertMaterialShaderGuid(text, expectedGuid, materialPath, label) {
  if (expectedGuid == null) {
    fail(`${label} 缺少期望 shader GUID，无法做材质 shader 字段级对账。`);
    return;
  }

  const actual = materialShaderReference(text, materialPath, label);
  if (actual == null) return;

  if (actual.guid !== expectedGuid) {
    fail(`${materialPath} 的 m_Shader 没有引用目标 shader GUID：当前 ${actual.guid}，应为 ${expectedGuid}（${label}）。`);
  }
}

function assertMaterialShaderReference(text, expectedLine, materialPath, label) {
  const actualLine = text.match(/^  m_Shader: \{fileID: [^}]+\}$/m)?.[0]?.trim() ?? null;
  if (actualLine == null) {
    fail(`${materialPath} 缺少可回读的 m_Shader 引用，无法证明 ${label}。`);
    return;
  }

  if (actualLine !== expectedLine) {
    fail(`${materialPath} 的 m_Shader 没有对齐 ${label}：当前 ${actualLine}，应为 ${expectedLine}。`);
  }
}

function assertYamlReferenceLine(text, fieldName, expectedFileId, expectedGuid, expectedType, sourcePath, label) {
  if (expectedGuid == null) {
    fail(`${label} 缺少期望 GUID，无法做 YAML 引用字段对账。`);
    return;
  }

  const actualLine = yamlMappingPropertyLine(text, fieldName);
  const expectedLine = `${fieldName}: {fileID: ${expectedFileId}, guid: ${expectedGuid}, type: ${expectedType}}`;
  if (actualLine !== expectedLine) {
    fail(`${sourcePath} 的 ${fieldName} 没有字段级引用目标资源：当前 ${actualLine ?? "<缺失>"}，应为 ${expectedLine}（${label}）。`);
  }
}

function assertYamlListContainsReference(text, fieldName, expectedFileId, expectedGuid, expectedType, sourcePath, label) {
  if (expectedGuid == null) {
    fail(`${label} 缺少期望 GUID，无法做 YAML 列表引用字段对账。`);
    return;
  }

  const listLines = yamlReferenceListLines(text, fieldName);
  if (listLines == null) {
    fail(`${sourcePath} 缺少 ${label} 的 YAML 列表字段：${fieldName}。`);
    return;
  }

  const expectedLine = `- {fileID: ${expectedFileId}, guid: ${expectedGuid}, type: ${expectedType}}`;
  if (!listLines.includes(expectedLine)) {
    fail(`${sourcePath} 的 ${fieldName} 没有列表级引用目标资源：${expectedLine}（${label}）。`);
  }
}

function yamlReferenceListLines(text, fieldName) {
  const block = text.match(new RegExp(`^\\s*${escapeRegExp(fieldName)}:\\r?\\n((?:\\s+- \\{[^\\r\\n]+\\}\\r?\\n?)+)`, "m"))?.[1] ?? null;
  return block == null
    ? null
    : block.split(/\r?\n/).map((line) => line.trim()).filter((line) => line.length > 0);
}

function unityReferenceLine(fieldName, expectedFileId, expectedGuid, expectedType) {
  return `${fieldName}: {fileID: ${expectedFileId}, guid: ${expectedGuid}, type: ${expectedType}}`;
}

function unityInlineReference(text, fieldName, label) {
  const line = yamlMappingPropertyLine(text, fieldName);
  if (line == null) {
    fail(`${label} 缺少 Unity 引用字段：${fieldName}。`);
    return null;
  }

  const match = line.match(new RegExp(`^${escapeRegExp(fieldName)}: \\{fileID: (-?\\d+), guid: ([0-9a-f]{32}), type: (\\d+)\\}$`));
  if (match == null) {
    fail(`${label} 的 ${fieldName} 不是可识别的 Unity 引用：${line}。`);
    return null;
  }

  return {
    fileId: match[1],
    guid: match[2],
    type: match[3],
    line,
  };
}

function assertUnityInlineReferencePath(text, fieldName, expectedPath, label) {
  const reference = unityInlineReference(text, fieldName, label);
  if (reference == null) return null;

  const actualPath = assetPathByGuid(reference.guid);
  if (actualPath !== expectedPath) {
    fail(`${label} 的 ${fieldName} 没有指向期望资源：当前 ${actualPath ?? reference.guid}，应为 ${expectedPath}。`);
  }

  return reference;
}

function assertUnityReferenceListPaths(text, fieldName, expectedPaths, label) {
  const lines = yamlReferenceListLines(text, fieldName);
  if (lines == null) {
    fail(`${label} 缺少 Unity 引用列表字段：${fieldName}。`);
    return;
  }

  const actualPaths = [];
  for (const line of lines) {
    const reference = line.match(/^- \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: \d+\}$/);
    if (reference == null) {
      fail(`${label} 的 ${fieldName} 不是可识别的 Unity 引用列表项：${line}。`);
      continue;
    }

    actualPaths.push(assetPathByGuid(reference[1]) ?? reference[1]);
  }

  if (actualPaths.length !== expectedPaths.length) {
    fail(`${label} 的 ${fieldName} 数量不一致：当前 ${actualPaths.length}，应为 ${expectedPaths.length}。`);
  }

  const maxLength = Math.max(actualPaths.length, expectedPaths.length);
  for (let index = 0; index < maxLength; index += 1) {
    const actualPath = actualPaths[index] ?? "<缺失>";
    const expectedPath = expectedPaths[index] ?? "<多余>";
    if (actualPath !== expectedPath) {
      fail(`${label} 的 ${fieldName}[${index}] 不一致：当前 ${actualPath}，应为 ${expectedPath}。`);
    }
  }
}

function assertUnitySingleReferenceListPath(text, fieldName, expectedPath, label) {
  const lines = yamlReferenceListLines(text, fieldName);
  if (lines == null) {
    fail(`${label} 缺少 Unity 引用列表字段：${fieldName}。`);
    return null;
  }
  if (lines.length !== 1) {
    fail(`${label} 的 ${fieldName} 当前不是单项引用列表：${lines.join(" / ")}。`);
    return null;
  }

  const reference = lines[0].match(/^- \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: \d+\}$/);
  if (reference == null) {
    fail(`${label} 的 ${fieldName} 不是可识别的 Unity 引用列表项：${lines[0]}。`);
    return null;
  }

  const actualPath = assetPathByGuid(reference[1]);
  if (actualPath !== expectedPath) {
    fail(`${label} 的 ${fieldName} 没有指向期望资产：当前 ${actualPath ?? reference[1]}，应为 ${expectedPath}。`);
  }

  return reference[1];
}

function assertUnityScenePrefabInstanceSources(sceneText, expectedPaths, label) {
  const actualPaths = [];
  for (const match of sceneText.matchAll(/--- !u!1001 &-?\d+\r?\n[\s\S]*?(?=\r?\n--- !u!|\s*$)/g)) {
    const prefab = match[0].match(/m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32}), type: 3\}/);
    if (prefab != null) {
      actualPaths.push(assetPathByGuid(prefab[1]) ?? prefab[1]);
    }
  }

  const actualSorted = [...actualPaths].sort();
  const expectedSorted = [...expectedPaths].sort();
  if (actualSorted.length !== expectedSorted.length) {
    fail(`${label} 的 Prefab 实例数量不一致：当前 ${actualSorted.length}，应为 ${expectedSorted.length}。当前 ${actualSorted.join(" / ")}`);
  }

  const maxLength = Math.max(actualSorted.length, expectedSorted.length);
  for (let index = 0; index < maxLength; index += 1) {
    const actualPath = actualSorted[index] ?? "<缺失>";
    const expectedPath = expectedSorted[index] ?? "<多余>";
    if (actualPath !== expectedPath) {
      fail(`${label} 的 Prefab 实例来源不一致：当前 ${actualPath}，应为 ${expectedPath}。`);
    }
  }
}

function assertStackCraftCardManagerCommonReferences(componentText, label) {
  assertUnityInlineReferencePath(
    componentText,
    "packPrefab",
    "Assets/StackCraft/Prefabs/PackInstance.prefab",
    `${label} 卡包 Prefab 字段`);
  assertUnityInlineReferencePath(
    componentText,
    "aggressiveMobPrefab",
    "Assets/StackCraft/Prefabs/Cards/Card_Mob_Aggressive.prefab",
    `${label} 主动敌人 Prefab 字段`);
  assertUnityInlineReferencePath(
    componentText,
    "recipeCardTemplate",
    "Assets/StackCraft/Resources/Cards/Card_Recipe.asset",
    `${label} 配方卡模板字段`);
  assertUnityInlineReferencePath(
    componentText,
    "stackingMatrix",
    "Assets/StackCraft/Settings/SRM_Default.asset",
    `${label} 堆叠矩阵字段`);
  assertUnityInlineReferencePath(
    componentText,
    "cardSettings",
    "Assets/StackCraft/Settings/Default_Card_Settings.asset",
    `${label} 默认设置字段`);

  const cardPrefabsBlock = componentText.match(/^  cardPrefabs:\r?\n([\s\S]*?)^  aggressiveMobPrefab:/m)?.[1] ?? null;
  const expectedCardPrefabPaths = new Map([
    ["1", "Assets/StackCraft/Prefabs/Cards/Card_Resource.prefab"],
    ["2", "Assets/StackCraft/Prefabs/Cards/Card_Character.prefab"],
    ["3", "Assets/StackCraft/Prefabs/Cards/Card_Consumable.prefab"],
    ["4", "Assets/StackCraft/Prefabs/Cards/Card_Material.prefab"],
    ["5", "Assets/StackCraft/Prefabs/Cards/Card_Equipment.prefab"],
    ["6", "Assets/StackCraft/Prefabs/Cards/Card_Structure.prefab"],
    ["7", "Assets/StackCraft/Prefabs/Cards/Card_Currency.prefab"],
    ["8", "Assets/StackCraft/Prefabs/Cards/Card_Recipe.prefab"],
    ["9", "Assets/StackCraft/Prefabs/Cards/Card_Mob.prefab"],
    ["10", "Assets/StackCraft/Prefabs/Cards/Card_Area.prefab"],
    ["11", "Assets/StackCraft/Prefabs/Cards/Card_Valuable.prefab"],
  ]);
  if (cardPrefabsBlock == null) {
    fail(`${label}.cardPrefabs 缺少可回读的卡牌分族 Prefab 列表。`);
  } else {
    const actualCardPrefabPaths = new Map();
    for (const match of cardPrefabsBlock.matchAll(/^\s*-\s+category:\s*(\d+)\r?\n\s+prefab:\s+\{fileID:\s*(-?\d+), guid:\s*([0-9a-f]{32}), type:\s*(\d+)\}/gm)) {
      actualCardPrefabPaths.set(match[1], assetPathByGuid(match[3]) ?? match[3]);
    }
    if (actualCardPrefabPaths.size !== expectedCardPrefabPaths.size) {
      fail(`${label}.cardPrefabs 数量不等于 11：当前 ${actualCardPrefabPaths.size}。`);
    }
    for (const [category, expectedPath] of expectedCardPrefabPaths) {
      const actualPath = actualCardPrefabPaths.get(category);
      if (actualPath !== expectedPath) {
        fail(`${label}.cardPrefabs 类别 ${category} 没有指向期望 Prefab：当前 ${actualPath ?? "<缺失>"}，应为 ${expectedPath}。`);
      }
    }
  }
}

function assertStackCraftTradeManagerReferences(componentText, currencyCardPath, offeredPackPaths, label) {
  assertUnityInlineReferencePath(
    componentText,
    "buyerPrefab",
    "Assets/StackCraft/Prefabs/Trading/CardBuyer.prefab",
    `${label} 收购点 Prefab 字段`);
  assertUnityInlineReferencePath(
    componentText,
    "currencyCard",
    currencyCardPath,
    `${label} 货币卡字段`);
  assertUnityInlineReferencePath(
    componentText,
    "vendorPrefab",
    "Assets/StackCraft/Prefabs/Trading/PackVendor.prefab",
    `${label} 卡包商贩 Prefab 字段`);
  assertUnityReferenceListPaths(
    componentText,
    "offeredPacks",
    offeredPackPaths,
    `${label} 可售卡包列表字段`);
}

function assertStackCraftCombatManagerReferences(componentText, label) {
  assertUnityInlineReferencePath(
    componentText,
    "combatRectPrefab",
    "Assets/StackCraft/Prefabs/UI/CombatRect.prefab",
    `${label} 战斗区域 Prefab 字段`);
  assertUnityInlineReferencePath(
    componentText,
    "hitUIPrefab",
    "Assets/StackCraft/Prefabs/UI/HitUI.prefab",
    `${label} 命中反馈 Prefab 字段`);
  assertUnityInlineReferencePath(
    componentText,
    "arrowProjectile",
    "Assets/StackCraft/Prefabs/UI/Projectile_Arrow.prefab",
    `${label} 箭矢投射物 Prefab 字段`);
  assertUnityInlineReferencePath(
    componentText,
    "magicProjectile",
    "Assets/StackCraft/Prefabs/UI/Projectile_Magic.prefab",
    `${label} 魔法投射物 Prefab 字段`);
}

function assertStackCraftCraftingManagerReferences(componentText, label) {
  assertUnityInlineReferencePath(
    componentText,
    "progressUIPrefab",
    "Assets/StackCraft/Prefabs/UI/ProgressUI.prefab",
    `${label} 进度 UI Prefab 字段`);
}

function assertStackCraftQuestGroupReferences(componentText, groupName, expectedQuestPaths, label) {
  const groupBlock = componentText.match(
    new RegExp(`^  - GroupName: ${escapeRegExp(groupName)}\\r?\\n    Quests:\\r?\\n((?:    - \\{[^\\r\\n]+\\}\\r?\\n?)+)`, "m"))?.[1] ?? null;
  if (groupBlock == null) {
    fail(`${label} 缺少任务组：${groupName}。`);
    return;
  }

  const actualPaths = [];
  for (const match of groupBlock.matchAll(/^\s*-\s+\{fileID: -?\d+, guid: ([0-9a-f]{32}), type: \d+\}$/gm)) {
    actualPaths.push(assetPathByGuid(match[1]) ?? match[1]);
  }
  if (actualPaths.length !== expectedQuestPaths.length) {
    fail(`${label} 的 ${groupName} 任务数量不一致：当前 ${actualPaths.length}，应为 ${expectedQuestPaths.length}。`);
  }

  const maxLength = Math.max(actualPaths.length, expectedQuestPaths.length);
  for (let index = 0; index < maxLength; index += 1) {
    const actualPath = actualPaths[index] ?? "<缺失>";
    const expectedPath = expectedQuestPaths[index] ?? "<多余>";
    if (actualPath !== expectedPath) {
      fail(`${label} 的 ${groupName} 任务[${index}] 不一致：当前 ${actualPath}，应为 ${expectedPath}。`);
    }
  }
}

function assertModelMaterialExternalObjectGuid(metaText, materialName, expectedGuid, metaPath, label) {
  if (expectedGuid == null) {
    fail(`${label} 缺少期望材质 GUID，无法做模型材质映射字段级对账。`);
    return;
  }

  const escapedName = materialName.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = metaText.match(new RegExp(`^\\s*name: ${escapedName}\\s*\\r?\\n\\s*second: \\{fileID: 2100000, guid: ([0-9a-f]{32}), type: 2\\}`, "m"));
  if (match == null) {
    fail(`${metaPath} 缺少 ${label} 的模型材质映射项：${materialName}。`);
    return;
  }

  if (match[1] !== expectedGuid) {
    fail(`${metaPath} 的 ${materialName} 材质映射没有指向目标材质：当前 ${match[1]}，应为 ${expectedGuid}（${label}）。`);
  }
}

let assetPathByGuidCache = null;

function assetPathByGuid(guid) {
  if (assetPathByGuidCache == null) {
    assetPathByGuidCache = new Map();
    const stack = [path.join(root, "Assets")];
    while (stack.length > 0) {
      const current = stack.pop();
      if (!fs.existsSync(current)) continue;
      for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
        const absolutePath = path.join(current, entry.name);
        if (entry.isDirectory()) {
          stack.push(absolutePath);
          continue;
        }
        if (!entry.name.endsWith(".meta")) continue;

        const metaText = fs.readFileSync(absolutePath, "utf8");
        const metaGuid = unityGuid(metaText);
        if (metaGuid != null) {
          assetPathByGuidCache.set(metaGuid, rel(absolutePath).replace(/\.meta$/, ""));
        }
      }
    }
  }

  return assetPathByGuidCache.get(guid) ?? null;
}

function assertMaterialTextureGuid(text, propertyName, expectedGuid, materialPath, label) {
  const actualGuid = materialTextureGuid(text, propertyName);
  if (actualGuid == null) {
    fail(`${materialPath} 的 ${propertyName} 缺少可回读的贴图 GUID，无法证明 ${label}。`);
    return;
  }

  if (actualGuid !== expectedGuid) {
    fail(`${materialPath} 的 ${propertyName} 没有引用自有贴图 GUID：当前 ${actualGuid}，应为 ${expectedGuid}（${label}）。`);
  }
}

function assertMappedStackCraftTextureGuid(sourceMaterialText, localMaterialText, propertyName, sourceMaterialPath, localMaterialPath, label) {
  const sourceTextureGuid = materialTextureGuid(sourceMaterialText, propertyName);
  if (sourceTextureGuid == null) {
    fail(`${sourceMaterialPath} 的 ${propertyName} 缺少可回读的 StackCraft 来源贴图 GUID，无法证明 ${label}。`);
    return;
  }

  const sourceTexturePath = assetPathByGuid(sourceTextureGuid);
  if (sourceTexturePath == null) {
    fail(`${sourceMaterialPath} 的 ${propertyName} 引用了未知 StackCraft 来源贴图 GUID：${sourceTextureGuid}（${label}）。`);
    return;
  }
  if (!sourceTexturePath.startsWith("Assets/StackCraft/Textures/")) {
    fail(`${sourceMaterialPath} 的 ${propertyName} 来源不是 StackCraft 贴图目录：${sourceTexturePath}（${label}）。`);
    return;
  }

  const localTexturePath = sourceTexturePath.replace("Assets/StackCraft/Textures/", "Assets/Art/Sprites/StackCraft/");
  const localTextureGuid = guidFromMetaPath(`${localTexturePath}.meta`, `${label} 自有贴图副本`);
  if (localTextureGuid == null) return;
  if (sourceTextureGuid === localTextureGuid) {
    fail(`${localTexturePath}.meta 复用了 StackCraft 来源贴图 GUID ${sourceTextureGuid}；自有复制素材必须使用项目新 GUID。`);
  }

  assertSameFileHash(sourceTexturePath, localTexturePath, `${label} 自有贴图副本`);
  assertMaterialTextureGuid(localMaterialText, propertyName, localTextureGuid, localMaterialPath, label);
}

const textureImportVisualFields = [
  "mipMapMode",
  "enableMipMap",
  "sRGBTexture",
  "linearTexture",
  "fadeOut",
  "borderMipMap",
  "mipMapsPreserveCoverage",
  "alphaTestReferenceValue",
  "mipMapFadeDistanceStart",
  "mipMapFadeDistanceEnd",
  "convertToNormalMap",
  "externalNormalMap",
  "heightScale",
  "normalMapFilter",
  "flipGreenChannel",
  "isReadable",
  "streamingMipmaps",
  "streamingMipmapsPriority",
  "vTOnly",
  "ignoreMipmapLimit",
  "grayScaleToAlpha",
  "generateCubemap",
  "cubemapConvolution",
  "seamlessCubemap",
  "textureFormat",
  "maxTextureSize",
  "filterMode",
  "aniso",
  "mipBias",
  "wrapU",
  "wrapV",
  "wrapW",
  "nPOTScale",
  "lightmap",
  "compressionQuality",
  "spriteExtrude",
  "spriteMeshType",
  "alignment",
  "spritePivot",
  "spritePixelsToUnits",
  "spriteBorder",
  "spriteGenerateFallbackPhysicsShape",
  "alphaUsage",
  "alphaIsTransparency",
  "spriteTessellationDetail",
  "textureShape",
  "singleChannelComponent",
  "flipbookRows",
  "flipbookColumns",
  "maxTextureSizeSet",
  "compressionQualitySet",
  "textureFormatSet",
  "ignorePngGamma",
  "applyGammaDecoding",
  "swizzle",
  "cookieLightType",
  "resizeAlgorithm",
  "textureCompression",
  "crunchedCompression",
  "allowsAlphaSplitting",
  "overridden",
  "ignorePlatformSupport",
  "androidETC2FallbackOverride",
  "forceMaximumCompressionQuality_BC6H_BC7",
  "mipmapLimitGroupName",
  "pSDRemoveMatte",
];

function yamlScalarLines(text, propertyName) {
  const escaped = propertyName.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return [...text.matchAll(new RegExp(`^\\s*${escaped}:.*$`, "gm"))]
    .map((match) => match[0].trim());
}

function assertTextureImportVisualSettingsMatch(sourceTexturePath, localTexturePath, label, options = {}) {
  const { requireSpriteImport = true } = options;
  const sourceMetaPath = `${sourceTexturePath}.meta`;
  const localMetaPath = `${localTexturePath}.meta`;
  const sourceMetaText = readIfExists(sourceMetaPath);
  const localMetaText = readIfExists(localMetaPath);
  if (sourceMetaText == null) {
    fail(`缺少 ${label} 的 StackCraft 来源贴图导入设置：${sourceMetaPath}`);
    return;
  }
  if (localMetaText == null) {
    fail(`缺少 ${label} 的自有贴图导入设置：${localMetaPath}`);
    return;
  }

  for (const propertyName of textureImportVisualFields) {
    const sourceLines = yamlScalarLines(sourceMetaText, propertyName);
    const localLines = yamlScalarLines(localMetaText, propertyName);
    if (sourceLines.length === 0 && localLines.length === 0) continue;
    if (sourceLines.join("\n") !== localLines.join("\n")) {
      fail(`${localMetaPath} 的 Unity 贴图导入视觉参数 ${propertyName} 没有对齐 StackCraft 来源。该参数会影响 mipmap、边缘采样、透明或压缩等玩家可见观感：当前 ${localLines.join(" / ") || "<缺失>"}，应为 ${sourceLines.join(" / ") || "<缺失>"}。`);
    }
  }

  if (requireSpriteImport) {
    for (const [propertyName, expectedValue] of [
      ["textureType", "textureType: 8"],
      ["spriteMode", "spriteMode: 1"],
    ]) {
      const localLines = yamlScalarLines(localMetaText, propertyName);
      if (!localLines.includes(expectedValue)) {
        fail(`${localMetaPath} 必须保留项目自有 Sprite 导入能力：缺少 ${expectedValue}。不能为复刻 StackCraft 采样参数而破坏 UGUI / SpriteRenderer 可引用的 Sprite fileID。`);
      }
    }
  } else {
    for (const propertyName of ["textureType", "spriteMode"]) {
      const sourceLines = yamlScalarLines(sourceMetaText, propertyName);
      const localLines = yamlScalarLines(localMetaText, propertyName);
      if (sourceLines.join("\n") !== localLines.join("\n")) {
        fail(`${localMetaPath} 的 Unity 贴图导入类型 ${propertyName} 没有对齐 StackCraft 来源。材质贴图副本不能被错误导入为 Sprite 或其它类型：当前 ${localLines.join(" / ") || "<缺失>"}，应为 ${sourceLines.join(" / ") || "<缺失>"}。`);
      }
    }
  }
}

function assertStackCraftTextureCopiesImportVisualSettings() {
  const sourceTextureRoot = "Assets/StackCraft/Textures";
  for (const sourceTextureFile of walk(sourceTextureRoot)
    .map((file) => rel(file))
    .filter((file) => /\.(png|jpg|jpeg|tga)$/i.test(file))) {
    const localTextureFile = sourceTextureFile.replace(
      "Assets/StackCraft/Textures/",
      "Assets/Art/Sprites/StackCraft/");
    assertSameFileHash(sourceTextureFile, localTextureFile, `StackCraft 复制贴图 ${sourceTextureFile}`);
    assertTextureImportVisualSettingsMatch(
      sourceTextureFile,
      localTextureFile,
      `StackCraft 复制贴图 ${sourceTextureFile}`);
  }
}

function assertLocalizedStackCraftCardArtCopiesImportVisualSettings() {
  const localizedCardArtPairs = [
    ["Assets/StackCraft/Textures/PackArts/Starter.png", "Assets/Art/Sprites/CardArts/初始卡包.png", "初始卡包卡图"],
    ["Assets/StackCraft/Textures/CardArts/Slime.png", "Assets/Art/Sprites/CardArts/史莱姆.png", "史莱姆卡图"],
    ["Assets/StackCraft/Textures/CardArts/Goblin.png", "Assets/Art/Sprites/CardArts/哥布林.png", "哥布林卡图"],
    ["Assets/StackCraft/Textures/CardArts/Soil.png", "Assets/Art/Sprites/CardArts/土壤.png", "土壤卡图"],
    ["Assets/StackCraft/Textures/CardArts/TreasureChest.png", "Assets/Art/Sprites/CardArts/宝箱.png", "宝箱卡图"],
    ["Assets/StackCraft/Textures/CardArts/Rock.png", "Assets/Art/Sprites/CardArts/岩石.png", "岩石卡图"],
    ["Assets/StackCraft/Textures/PackArts/Beginning.png", "Assets/Art/Sprites/CardArts/开端卡包.png", "开端卡包卡图"],
    ["Assets/StackCraft/Textures/CardArts/Wood.png", "Assets/Art/Sprites/CardArts/木头.png", "木头卡图"],
    ["Assets/StackCraft/Textures/CardArts/WoodenChest.png", "Assets/Art/Sprites/CardArts/木箱.png", "木箱卡图"],
    ["Assets/StackCraft/Textures/CardArts/Villager.png", "Assets/Art/Sprites/CardArts/村民.png", "村民卡图"],
    ["Assets/StackCraft/Textures/CardArts/Tree.png", "Assets/Art/Sprites/CardArts/树.png", "树卡图"],
    ["Assets/StackCraft/Textures/CardArts/Berry.png", "Assets/Art/Sprites/CardArts/浆果.png", "浆果卡图"],
    ["Assets/StackCraft/Textures/CardArts/BerryBush.png", "Assets/Art/Sprites/CardArts/浆果丛.png", "浆果丛卡图"],
    ["Assets/StackCraft/Textures/CardArts/Stone.png", "Assets/Art/Sprites/CardArts/石头.png", "石头卡图"],
    ["Assets/StackCraft/Textures/CardArts/Recipe.png", "Assets/Art/Sprites/CardArts/配方卡.png", "配方卡卡图"],
    ["Assets/StackCraft/Textures/CardArts/Coin.png", "Assets/Art/Sprites/CardArts/金币.png", "金币卡图"],
    ["Assets/StackCraft/Textures/CardArts/GoldenKey.png", "Assets/Art/Sprites/CardArts/金钥匙.png", "金钥匙卡图"],
    ["Assets/StackCraft/Textures/CardArts/Chicken.png", "Assets/Art/Sprites/CardArts/鸡.png", "鸡卡图"],
    ["Assets/StackCraft/Textures/CardArts/Egg.png", "Assets/Art/Sprites/CardArts/鸡蛋.png", "鸡蛋卡图"],
  ];

  for (const [sourceTextureFile, localTextureFile, label] of localizedCardArtPairs) {
    assertSameFileHash(sourceTextureFile, localTextureFile, `StackCraft 中文自有 ${label}`);
    assertTextureImportVisualSettingsMatch(
      sourceTextureFile,
      localTextureFile,
      `StackCraft 中文自有 ${label}`);
  }
}

const audioImportFields = [
  "loadType",
  "sampleRateSetting",
  "sampleRateOverride",
  "compressionFormat",
  "quality",
  "conversionMode",
  "preloadAudioData",
  "platformSettingOverrides",
  "forceToMono",
  "normalize",
  "loadInBackground",
  "ambisonic",
  "3D",
];

function assertAudioImportSettingsMatch(sourceAudioPath, localAudioPath, label) {
  const sourceMetaPath = `${sourceAudioPath}.meta`;
  const localMetaPath = `${localAudioPath}.meta`;
  const sourceMetaText = readIfExists(sourceMetaPath);
  const localMetaText = readIfExists(localMetaPath);
  if (sourceMetaText == null) {
    fail(`缺少 ${label} 的 StackCraft 来源音频导入设置：${sourceMetaPath}`);
    return;
  }
  if (localMetaText == null) {
    fail(`缺少 ${label} 的自有音频导入设置：${localMetaPath}`);
    return;
  }

  for (const propertyName of audioImportFields) {
    const sourceLines = yamlScalarLines(sourceMetaText, propertyName);
    const localLines = yamlScalarLines(localMetaText, propertyName);
    if (sourceLines.join("\n") !== localLines.join("\n")) {
      fail(`${localMetaPath} 的 Unity 音频导入参数 ${propertyName} 没有对齐 StackCraft 来源。该参数会影响播放加载、压缩、采样率或 3D 音频表现：当前 ${localLines.join(" / ") || "<缺失>"}，应为 ${sourceLines.join(" / ") || "<缺失>"}。`);
    }
  }
}

const modelImportFields = [
  "materialImportMode",
  "materialName",
  "materialSearch",
  "materialLocation",
  "legacyGenerateAnimations",
  "bakeSimulation",
  "resampleCurves",
  "optimizeGameObjects",
  "removeConstantScaleCurves",
  "motionNodeName",
  "importAnimatedCustomProperties",
  "importConstraints",
  "animationCompression",
  "animationRotationError",
  "animationPositionError",
  "animationScaleError",
  "animationWrapMode",
  "isReadable",
  "globalScale",
  "meshCompression",
  "addColliders",
  "useSRGBMaterialColor",
  "sortHierarchyByName",
  "importPhysicalCameras",
  "importVisibility",
  "importBlendShapes",
  "importCameras",
  "importLights",
  "nodeNameCollisionStrategy",
  "fileIdsGeneration",
  "swapUVChannels",
  "generateSecondaryUV",
  "useFileUnits",
  "keepQuads",
  "weldVertices",
  "bakeAxisConversion",
  "preserveHierarchy",
  "skinWeightsMode",
  "maxBonesPerVertex",
  "minBoneWeight",
  "optimizeBones",
  "meshOptimizationFlags",
  "indexFormat",
  "secondaryUVAngleDistortion",
  "secondaryUVAreaDistortion",
  "secondaryUVHardAngle",
  "secondaryUVMarginMethod",
  "secondaryUVMinLightmapResolution",
  "secondaryUVMinObjectScale",
  "secondaryUVPackMargin",
  "useFileScale",
  "strictVertexDataChecks",
  "normalSmoothAngle",
  "normalImportMode",
  "tangentImportMode",
  "normalCalculationMode",
  "legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes",
  "blendShapeNormalImportMode",
  "normalSmoothingSource",
  "importAnimation",
  "animationType",
  "humanoidOversampling",
  "avatarSetup",
  "addHumanoidExtraRootOnlyWhenUsingAvatar",
  "importBlendShapeDeformPercent",
  "remapMaterialsIfMaterialImportModeIsNone",
  "additionalBone",
];

function assertModelImportSettingsMatch(sourceModelPath, localModelPath, label) {
  const sourceMetaPath = `${sourceModelPath}.meta`;
  const localMetaPath = `${localModelPath}.meta`;
  const sourceMetaText = readIfExists(sourceMetaPath);
  const localMetaText = readIfExists(localMetaPath);
  if (sourceMetaText == null) {
    fail(`缺少 ${label} 的 StackCraft 来源模型导入设置：${sourceMetaPath}`);
    return;
  }
  if (localMetaText == null) {
    fail(`缺少 ${label} 的自有模型导入设置：${localMetaPath}`);
    return;
  }

  for (const propertyName of modelImportFields) {
    const sourceLines = yamlScalarLines(sourceMetaText, propertyName);
    const localLines = yamlScalarLines(localMetaText, propertyName);
    if (sourceLines.join("\n") !== localLines.join("\n")) {
      fail(`${localMetaPath} 的 Unity 模型导入参数 ${propertyName} 没有对齐 StackCraft 来源。该参数会影响网格尺寸、法线、切线、动画或材质导入语义：当前 ${localLines.join(" / ") || "<缺失>"}，应为 ${sourceLines.join(" / ") || "<缺失>"}。`);
    }
  }
}

function assertYamlMappingPropertyLinesMatch(sourceText, localText, sourcePath, localPath, propertyNames, label) {
  for (const propertyName of propertyNames) {
    const sourceLine = yamlMappingPropertyLine(sourceText, propertyName);
    const localLine = yamlMappingPropertyLine(localText, propertyName);
    if (sourceLine == null) {
      fail(`${sourcePath} 缺少 StackCraft ${label} 参数：${propertyName}`);
    } else if (localLine !== sourceLine) {
      fail(`${localPath} 的 ${propertyName} 与 StackCraft ${label} 不一致：${localLine ?? "<缺失>"}，应为 ${sourceLine}`);
    }
  }
}

function assertScriptedImporterMetaMatches(sourceAssetPath, localAssetPath, label) {
  const sourceMetaPath = `${sourceAssetPath}.meta`;
  const localMetaPath = `${localAssetPath}.meta`;
  const sourceMetaText = readIfExists(sourceMetaPath);
  const localMetaText = readIfExists(localMetaPath);
  if (sourceMetaText == null) {
    fail(`缺少 ${label} 的 StackCraft 来源导入设置：${sourceMetaPath}`);
    return;
  }
  if (localMetaText == null) {
    fail(`缺少 ${label} 的自有导入设置：${localMetaPath}`);
    return;
  }

  if (!sourceMetaText.includes("ScriptedImporter:") || !localMetaText.includes("ScriptedImporter:")) {
    fail(`${label} 必须保持 ShaderGraph 的 ScriptedImporter 导入语义：${sourceMetaPath} -> ${localMetaPath}`);
  }

  const sourceGuid = unityGuid(sourceMetaText);
  const localGuid = unityGuid(localMetaText);
  if (sourceGuid != null && localGuid != null && sourceGuid === localGuid) {
    fail(`${localMetaPath} 复用了 StackCraft 来源 GUID ${sourceGuid}；ShaderGraph 自有副本必须使用项目新 GUID。`);
  }

  assertYamlMappingPropertyLinesMatch(
    sourceMetaText,
    localMetaText,
    sourceMetaPath,
    localMetaPath,
    ["script"],
    `${label} 导入器`);
}

function unityEscapedString(value) {
  return value.replace(/[^\x20-\x7e]/g, (char) =>
    "\\u" + char.charCodeAt(0).toString(16).toUpperCase().padStart(4, "0"));
}

function unityUnescapedString(value) {
  return value.replace(/\\u([0-9a-fA-F]{4})/g, (_match, code) =>
    String.fromCharCode(Number.parseInt(code, 16)));
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function unityYamlObjects(text) {
  const objects = [];
  const byFileId = new Map();
  const objectPattern = /(?:^|\r?\n)(--- !u!(\d+) &(-?\d+)\r?\n[\s\S]*?)(?=\r?\n--- !u!|\s*$)/g;
  for (const match of text.matchAll(objectPattern)) {
    const unityObject = {
      text: match[1],
      classId: match[2],
      fileId: match[3],
    };
    objects.push(unityObject);
    byFileId.set(unityObject.fileId, unityObject);
  }

  return { objects, byFileId };
}

function unityGameObjectByName(parsedYaml, objectName) {
  return parsedYaml.objects.find((unityObject) =>
    unityObject.classId === "1" &&
    unquoteUnityString(unityPropertyValue(unityObject.text, "m_Name") ?? "") === objectName) ?? null;
}

function unityGameObjectsByName(parsedYaml, objectName) {
  return parsedYaml.objects.filter((unityObject) =>
    unityObject.classId === "1" &&
    unquoteUnityString(unityPropertyValue(unityObject.text, "m_Name") ?? "") === objectName);
}

function unityComponentIds(gameObjectBlock) {
  return [...gameObjectBlock.text.matchAll(/component: \{fileID: (-?\d+)\}/g)]
    .map((match) => match[1]);
}

function unityComponentsByClassOnGameObject(parsedYaml, gameObject, classId) {
  if (gameObject == null) return [];

  return unityComponentIds(gameObject)
    .map((componentId) => parsedYaml.byFileId.get(componentId))
    .filter((component) => component?.classId === String(classId));
}

function unityComponentsByClass(parsedYaml, objectName, classId) {
  const gameObject = unityGameObjectByName(parsedYaml, objectName);
  return unityComponentsByClassOnGameObject(parsedYaml, gameObject, classId);
}

function unityComponentByClass(parsedYaml, objectName, classId) {
  return unityComponentsByClass(parsedYaml, objectName, classId)[0] ?? null;
}

function unityComponentByProperty(parsedYaml, objectName, classId, propertyName) {
  return unityComponentsByClass(parsedYaml, objectName, classId)
    .find((component) => unityPropertyLine(component.text, propertyName) != null) ?? null;
}

function unityPropertyLine(text, propertyName) {
  const escaped = escapeRegExp(propertyName);
  return text.match(new RegExp(`^\\s*${escaped}:.*$`, "m"))?.[0].trim() ?? null;
}

function unityPropertyValue(text, propertyName) {
  const line = unityPropertyLine(text, propertyName);
  return line?.slice(line.indexOf(":") + 1).trim() ?? null;
}

function assertUnityTextWrappingSemanticsMatch(sourceComponent, targetComponent, label) {
  const sourceWrapping = unityPropertyValue(sourceComponent.text, "m_enableWordWrapping") ??
    unityPropertyValue(sourceComponent.text, "m_TextWrappingMode");
  const targetWrapping = unityPropertyValue(targetComponent.text, "m_enableWordWrapping") ??
    unityPropertyValue(targetComponent.text, "m_TextWrappingMode");

  if (sourceWrapping == null) {
    fail(`${label} 的 StackCraft 来源文字组件缺少换行模式字段，请先复核参考 Prefab。`);
    return;
  }

  if (targetWrapping !== sourceWrapping) {
    fail(`${label} 的文字换行模式没有对齐 StackCraft：当前 ${targetWrapping ?? "<缺失>"}，应为 ${sourceWrapping}。`);
  }
}

function csharpFloatLiteral(value) {
  let normalized = value.trim();
  normalized = normalized.replace(/[fF]$/, "");
  if (normalized === "-0" || normalized === "-0.0") normalized = "0";
  return `${normalized}f`;
}

function unityNumberLiteralFromCsharp(value) {
  let normalized = value.trim();
  normalized = normalized.replace(/[fF]$/, "");
  if (normalized === "-0" || normalized === "-0.0") normalized = "0";
  return normalized;
}

function unityCanonicalNumber(value) {
  const number = typeof value === "number" ? value : Number.parseFloat(String(value).trim());
  if (!Number.isFinite(number)) {
    fail(`无法把 Unity 数值转换为规范数字：${value}`);
    return String(value).trim();
  }

  const roundedInteger = Math.round(number);
  if (Math.abs(number - roundedInteger) < 0.00001) {
    return String(roundedInteger);
  }

  return String(Number.parseFloat(number.toFixed(6)));
}

function csharpFloatLiteralFromUnityNumber(value) {
  return `${unityCanonicalNumber(value)}f`;
}

function unityColorLiteralFromCsharpAssignment(sourceText, assignmentPrefix, label) {
  const pattern = new RegExp(`${escapeRegExp(assignmentPrefix)}\\s*=\\s*new\\s+Color\\s*\\(([^)]*)\\)`);
  const match = sourceText.match(pattern);
  if (match == null) {
    fail(`${label} 缺少可解析的 C# Color 赋值：${assignmentPrefix} = new Color(...)。`);
    return null;
  }

  const rawValues = match[1].split(",").map((item) => item.trim());
  if (rawValues.length !== 4) {
    fail(`${label} 的 C# Color 参数数量不是 4：${match[0]}`);
    return null;
  }

  return `{r: ${unityCanonicalNumber(unityNumberLiteralFromCsharp(rawValues[0]))}, g: ${unityCanonicalNumber(unityNumberLiteralFromCsharp(rawValues[1]))}, b: ${unityCanonicalNumber(unityNumberLiteralFromCsharp(rawValues[2]))}, a: ${unityCanonicalNumber(unityNumberLiteralFromCsharp(rawValues[3]))}}`;
}

function unityVector3LiteralFromCsharpAssignment(sourceText, assignmentPrefix, label) {
  const values = unityVector3ValuesFromCsharpAssignment(sourceText, assignmentPrefix, label);
  if (values == null) return null;

  return unityInlineNumericObjectLiteral(values, ["x", "y", "z"], label);
}

function unityVector3ValuesFromCsharpAssignment(sourceText, assignmentPrefix, label) {
  const pattern = new RegExp(`${escapeRegExp(assignmentPrefix)}\\s*=\\s*new\\s+Vector3\\s*\\(([^)]*)\\)`);
  const match = sourceText.match(pattern);
  if (match == null) {
    fail(`${label} 缺少可解析的 C# Vector3 赋值：${assignmentPrefix} = new Vector3(...)。`);
    return null;
  }

  const rawValues = match[1].split(",").map((item) => item.trim());
  if (rawValues.length !== 3) {
    fail(`${label} 的 C# Vector3 参数数量不是 3：${match[0]}`);
    return null;
  }

  return new Map([
    ["x", unityNumberLiteralFromCsharp(rawValues[0])],
    ["y", unityNumberLiteralFromCsharp(rawValues[1])],
    ["z", unityNumberLiteralFromCsharp(rawValues[2])],
  ]);
}

function unityQuaternionLiteralFromCsharpEulerXAssignment(sourceText, assignmentPrefix, label) {
  const values = unityQuaternionValuesFromCsharpEulerXAssignment(sourceText, assignmentPrefix, label);
  if (values == null) return null;

  return unityInlineNumericObjectLiteral(values, ["x", "y", "z", "w"], label);
}

function unityQuaternionValuesFromCsharpEulerXAssignment(sourceText, assignmentPrefix, label) {
  const pattern = new RegExp(`${escapeRegExp(assignmentPrefix)}\\s*=\\s*Quaternion\\.Euler\\s*\\(([^)]*)\\)`);
  const match = sourceText.match(pattern);
  if (match == null) {
    fail(`${label} 缺少可解析的 C# Quaternion.Euler 赋值：${assignmentPrefix} = Quaternion.Euler(...)。`);
    return null;
  }

  const rawValues = match[1].split(",").map((item) => item.trim());
  if (rawValues.length !== 3) {
    fail(`${label} 的 C# Quaternion.Euler 参数数量不是 3：${match[0]}`);
    return null;
  }

  const y = Number.parseFloat(unityNumberLiteralFromCsharp(rawValues[1]));
  const z = Number.parseFloat(unityNumberLiteralFromCsharp(rawValues[2]));
  if (y !== 0 || z !== 0) {
    fail(`${label} 目前只支持从 Quaternion.Euler(x, 0, 0) 派生 YAML 四元数；当前为 ${match[0]}。`);
    return null;
  }

  const xDegrees = Number.parseFloat(unityNumberLiteralFromCsharp(rawValues[0]));
  const halfRadians = xDegrees * Math.PI / 360;
  return new Map([
    ["x", String(Math.sin(halfRadians))],
    ["y", "0"],
    ["z", "0"],
    ["w", String(Math.cos(halfRadians))],
  ]);
}

function assertSerializedScalarFromCsharpLiteral(targetText, targetFieldName, sourceLiteral, targetPath, label) {
  assertYamlScalarEquals(
    targetText,
    targetFieldName,
    unityNumberLiteralFromCsharp(sourceLiteral),
    `${targetPath} ${label}`);
}

function csharpVectorComponents(sourceText, fieldName, typeName, componentNames, label) {
  const pattern = new RegExp(`${escapeRegExp(fieldName)}\\s*=\\s*new\\s+${typeName}\\s*\\(([^)]*)\\)`);
  const match = sourceText.match(pattern);
  if (match == null) {
    fail(`${label} 缺少可解析的 ${typeName} 字段 ${fieldName}。`);
    return null;
  }

  const rawValues = match[1].split(",").map((item) => item.trim());
  if (rawValues.length < componentNames.length) {
    fail(`${label} 的 ${fieldName} 分量不足，无法从参考源码提取参数：${match[0]}`);
    return null;
  }

  const values = new Map();
  componentNames.forEach((componentName, index) => {
    values.set(componentName, csharpFloatLiteral(rawValues[index]));
  });
  return values;
}

function csharpScalarInitializer(sourceText, fieldName, label) {
  const pattern = new RegExp(`${escapeRegExp(fieldName)}\\s*=\\s*([^;]+);`);
  const match = sourceText.match(pattern);
  if (match == null) {
    fail(`${label} 缺少可解析的标量字段 ${fieldName}。`);
    return null;
  }

  return csharpFloatLiteral(match[1]);
}

function csharpRawInitializer(sourceText, fieldName, label) {
  const pattern = new RegExp(`${escapeRegExp(fieldName)}\\s*=\\s*([^;]+);`);
  const match = sourceText.match(pattern);
  if (match == null) {
    fail(`${label} 缺少可解析的字段默认值 ${fieldName}。`);
    return null;
  }

  return match[1].trim();
}

function csharpConstIntValue(sourceText, constantName, label) {
  const pattern = new RegExp(`\\bconst\\s+int\\s+${escapeRegExp(constantName)}\\s*=\\s*([^;]+);`);
  const match = sourceText.match(pattern);
  if (match == null) {
    fail(`${label} 缺少可解析的 const int ${constantName}。`);
    return null;
  }

  const value = match[1].trim();
  if (!/^-?\d+$/.test(value)) {
    fail(`${label} 的 const int ${constantName} 不是整数字面量：${value}。`);
    return null;
  }
  return value;
}

function csharpDefaultParameter(sourceText, methodName, parameterName, label) {
  const pattern = new RegExp(`${escapeRegExp(methodName)}\\s*\\([^)]*\\b${escapeRegExp(parameterName)}\\s*=\\s*([^,)]+)`);
  const match = sourceText.match(pattern);
  if (match == null) {
    fail(`${label} 缺少可解析的 ${methodName}(${parameterName} = ...) 默认参数。`);
    return null;
  }

  return csharpFloatLiteral(match[1]);
}

function csharpWaitForSecondsRealtimeAfter(sourceText, anchor, label) {
  const anchorIndex = sourceText.indexOf(anchor);
  if (anchorIndex < 0) {
    fail(`${label} 缺少源码锚点：${anchor}`);
    return null;
  }

  const tail = sourceText.slice(anchorIndex);
  const match = tail.match(/WaitForSecondsRealtime\s*\(\s*([^)]+)\)/);
  if (match == null) {
    fail(`${label} 缺少可解析的 WaitForSecondsRealtime(...) 调用。`);
    return null;
  }

  return csharpFloatLiteral(match[1]);
}

function yamlScalarPropertyValue(text, propertyName, label) {
  const line = unityPropertyLine(text, propertyName);
  const match = line?.match(/:\s*([^\r\n]+)/);
  if (match == null) {
    fail(`${label} 缺少可解析的 Unity YAML 标量属性 ${propertyName}。`);
    return null;
  }

  return match[1].trim();
}

function assertSerializedScalarFromReference(targetText, targetFieldName, expectedValue, targetPath, label) {
  assertYamlScalarEquals(
    targetText,
    targetFieldName,
    expectedValue,
    `${targetPath} ${label}`);
}

function assertYamlScalarEquals(text, fieldName, expectedValue, label) {
  if (expectedValue == null) return;

  const actualValue = yamlScalarPropertyValue(text, fieldName, label);
  if (actualValue == null) return;

  const normalizedExpectedValue = String(expectedValue).trim();
  if (actualValue !== normalizedExpectedValue) {
    fail(`${label} 的 ${fieldName} 不一致：当前 ${actualValue}，应为 ${normalizedExpectedValue}。`);
  }
}

function assertYamlScalarStringEquals(text, fieldName, expectedValue, label) {
  if (expectedValue == null) return;

  const actualValue = yamlScalarPropertyValue(text, fieldName, label);
  if (actualValue == null) return;

  const escapedValue = unityEscapedString(String(expectedValue));
  const expectedValues = [
    String(expectedValue),
    `"${escapedValue}"`,
  ];
  if (!expectedValues.includes(actualValue)) {
    fail(`${label} 的 ${fieldName} 不一致：当前 ${actualValue}，应为 ${expectedValues.join(" 或 ")}。`);
  }
}

function yamlPlainMultilineScalarValue(text, fieldName, label) {
  const lines = text.split(/\r?\n/);
  const fieldPattern = new RegExp("^(\\s*)" + escapeRegExp(fieldName) + ":\\s*(.*)$");
  const start = lines.findIndex((line) => fieldPattern.test(line));
  if (start < 0) {
    fail(label + " 缺少 Unity YAML 字段 " + fieldName + "。");
    return null;
  }

  const match = lines[start].match(fieldPattern);
  const fieldIndent = match[1].length;
  const continuationIndent = fieldIndent + 2;
  const parts = [match[2].trimEnd()];
  for (let index = start + 1; index < lines.length; index += 1) {
    const line = lines[index];
    if (line.trim().length === 0) {
      parts.push("");
      continue;
    }

    const currentIndent = line.match(/^\s*/)?.[0].length ?? 0;
    if (currentIndent <= fieldIndent) break;
    parts.push(line.slice(Math.min(continuationIndent, line.length)).trimEnd());
  }

  return parts.join("\n").trimEnd();
}
function assertYamlPlainMultilineScalarEquals(text, fieldName, expectedValue, label) {
  const actualValue = yamlPlainMultilineScalarValue(text, fieldName, label);
  if (actualValue == null) return;

  const normalizedExpectedValue = String(expectedValue).replace(/\r\n/g, "\n").trimEnd();
  if (actualValue !== normalizedExpectedValue) {
    fail(`${label} 的 ${fieldName} 不一致：当前 ${actualValue}，应为 ${normalizedExpectedValue}。`);
  }
}

function unquoteUnityString(value) {
  const trimmed = String(value).trim();
  const unquoted = (trimmed.startsWith("\"") && trimmed.endsWith("\"")) ||
    (trimmed.startsWith("'") && trimmed.endsWith("'"))
    ? trimmed.slice(1, -1)
    : trimmed;

  return unityUnescapedString(unquoted);
}

function assertMaterialNameEquals(text, expectedName, materialPath) {
  const parsedYaml = unityYamlObjects(text);
  const materialObject = parsedYaml.byFileId.get("2100000") ??
    parsedYaml.objects.find((unityObject) => unityObject.classId === "21") ??
    null;
  if (materialObject == null) {
    fail(`${materialPath} 缺少 Unity Material 对象，无法验证材质名称。`);
    return;
  }

  const actualValue = unityPropertyValue(materialObject.text, "m_Name");
  if (actualValue == null) {
    fail(`${materialPath} 的 Unity Material 对象缺少 m_Name，无法验证材质名称。`);
    return;
  }

  const actualName = unquoteUnityString(actualValue);
  if (actualName !== expectedName) {
    fail(`${materialPath} 的材质名称不一致：当前 ${actualName}，应为 ${expectedName}。`);
  }
}

function csharpVector2FromVectorComponents(values, firstComponent, secondComponent, label) {
  const first = values?.get(firstComponent);
  const second = values?.get(secondComponent);
  if (first == null || second == null) {
    fail(`${label} 缺少 ${firstComponent}/${secondComponent} 分量，无法生成 Vector2 对账值。`);
    return null;
  }

  return `new Vector2(${first}, ${second})`;
}

function normalizeCsharpExpression(expression) {
  return String(expression).replace(/\s+/g, "");
}

function splitTopLevelCsharpArguments(argumentText, label) {
  const args = [];
  let start = 0;
  let parenDepth = 0;
  let bracketDepth = 0;
  let braceDepth = 0;
  let inString = false;
  let stringQuote = null;
  let isVerbatimString = false;
  for (let i = 0; i < argumentText.length; i++) {
    const character = argumentText[i];
    if (inString) {
      if (isVerbatimString && character === '"' && argumentText[i + 1] === '"') {
        i++;
        continue;
      }
      if (!isVerbatimString && character === "\\") {
        i++;
        continue;
      }
      if (character === stringQuote) {
        inString = false;
        stringQuote = null;
        isVerbatimString = false;
      }
      continue;
    }

    if (character === '"' || character === "'") {
      inString = true;
      stringQuote = character;
      isVerbatimString = character === '"' && i > 0 && argumentText[i - 1] === "@";
      continue;
    }

    if (character === "(") {
      parenDepth++;
      continue;
    }
    if (character === ")") {
      parenDepth--;
      continue;
    }
    if (character === "[") {
      bracketDepth++;
      continue;
    }
    if (character === "]") {
      bracketDepth--;
      continue;
    }
    if (character === "{") {
      braceDepth++;
      continue;
    }
    if (character === "}") {
      braceDepth--;
      continue;
    }

    if (character === "," && parenDepth === 0 && bracketDepth === 0 && braceDepth === 0) {
      args.push(argumentText.slice(start, i).trim());
      start = i + 1;
    }
  }

  if (inString || parenDepth !== 0 || bracketDepth !== 0 || braceDepth !== 0) {
    fail(`${label} 的 C# 调用实参列表没有正确闭合。`);
    return null;
  }

  const tail = argumentText.slice(start).trim();
  if (tail.length > 0) {
    args.push(tail);
  }
  return args;
}

function csharpCallInvocations(sourceText, callExpression, label) {
  const calls = [];
  let searchFrom = 0;
  while (searchFrom < sourceText.length) {
    const callIndex = sourceText.indexOf(callExpression, searchFrom);
    if (callIndex < 0) {
      break;
    }

    const openParenIndex = sourceText.indexOf("(", callIndex + callExpression.length);
    if (openParenIndex < 0) {
      fail(`${label} 第 ${lineNumber(sourceText, callIndex)} 行缺少调用左括号：${callExpression}`);
      return calls;
    }

    let depth = 0;
    let inString = false;
    let stringQuote = null;
    let isVerbatimString = false;
    for (let i = openParenIndex; i < sourceText.length; i++) {
      const character = sourceText[i];
      if (inString) {
        if (isVerbatimString && character === '"' && sourceText[i + 1] === '"') {
          i++;
          continue;
        }
        if (!isVerbatimString && character === "\\") {
          i++;
          continue;
        }
        if (character === stringQuote) {
          inString = false;
          stringQuote = null;
          isVerbatimString = false;
        }
        continue;
      }

      if (character === '"' || character === "'") {
        inString = true;
        stringQuote = character;
        isVerbatimString = character === '"' && i > 0 && sourceText[i - 1] === "@";
        continue;
      }

      if (character === "(") {
        depth++;
        continue;
      }
      if (character !== ")") {
        continue;
      }

      depth--;
      if (depth === 0) {
        const argumentText = sourceText.slice(openParenIndex + 1, i);
        const args = splitTopLevelCsharpArguments(argumentText, `${label} 第 ${lineNumber(sourceText, callIndex)} 行`);
        calls.push({
          index: callIndex,
          argumentText,
          args,
        });
        searchFrom = i + 1;
        break;
      }
    }

    if (searchFrom <= callIndex) {
      fail(`${label} 第 ${lineNumber(sourceText, callIndex)} 行调用括号没有闭合：${callExpression}`);
      break;
    }
  }

  return calls;
}

function csharpBlockAfter(sourceText, anchor, label) {
  const anchorIndex = sourceText.indexOf(anchor);
  if (anchorIndex < 0) {
    fail(`${label} 缺少源码锚点：${anchor}`);
    return null;
  }

  const openBraceIndex = sourceText.indexOf("{", anchorIndex);
  if (openBraceIndex < 0) {
    fail(`${label} 缺少方法体起始大括号：${anchor}`);
    return null;
  }

  let depth = 0;
  for (let i = openBraceIndex; i < sourceText.length; i++) {
    const character = sourceText[i];
    if (character === "{") {
      depth++;
    } else if (character === "}") {
      depth--;
      if (depth === 0) {
        return sourceText.slice(openBraceIndex, i + 1);
      }
    }
  }

  fail(`${label} 的源码块大括号没有闭合：${anchor}`);
  return null;
}

function csharpDeclarationAndBlockAfter(sourceText, anchor, label) {
  const anchorIndex = sourceText.indexOf(anchor);
  if (anchorIndex < 0) {
    fail(`${label} 缺少源码锚点：${anchor}`);
    return null;
  }

  const block = csharpBlockAfter(sourceText, anchor, label);
  if (block == null) return null;

  const blockStart = sourceText.indexOf(block, anchorIndex);
  return sourceText.slice(anchorIndex, blockStart + block.length);
}

function assertSourceContainsOrdered(sourceText, tokens, label) {
  let searchFrom = 0;
  for (const token of tokens.filter((value) => value != null)) {
    const index = sourceText.indexOf(token, searchFrom);
    if (index < 0) {
      fail(`${label} 缺少按顺序出现的源码片段：${token}`);
      return;
    }
    searchFrom = index + token.length;
  }
}

function assertCsharpBlockContainsOrdered(sourceText, anchor, tokens, label) {
  const block = csharpBlockAfter(sourceText, anchor, label);
  if (block == null) return;

  assertSourceContainsOrdered(block, tokens, label);
}

function assertCsharpDeclarationAndBlockContainsOrdered(sourceText, anchor, tokens, label) {
  const block = csharpDeclarationAndBlockAfter(sourceText, anchor, label);
  if (block == null) return;

  assertSourceContainsOrdered(block, tokens, label);
}

function csharpMethodBlock(sourceText, methodName, label) {
  const signaturePattern = new RegExp(`\\b(?:(?:public|private|protected|internal|static|async|virtual|override|sealed)\\s+)+[\\w.<>\\[\\],]+\\s+${escapeRegExp(methodName)}\\s*\\(`);
  const match = sourceText.match(signaturePattern);
  if (match == null) {
    fail(`${label} 缺少 C# 方法声明：${methodName}`);
    return null;
  }

  return csharpBlockAfter(sourceText, match[0], `${label} / ${methodName}`);
}

function assertCsharpMethodContainsOrdered(sourceText, methodName, tokens, label) {
  const block = csharpMethodBlock(sourceText, methodName, label);
  if (block == null) return;

  assertSourceContainsOrdered(block, tokens, `${label} / ${methodName}`);
}

function assertCsharpMethodsExist(sourceText, methodNames, label) {
  for (const methodName of methodNames) {
    csharpMethodBlock(sourceText, methodName, label);
  }
}

function nthIndexOf(sourceText, needle, zeroBasedOccurrence) {
  let searchFrom = 0;
  for (let occurrence = 0; occurrence <= zeroBasedOccurrence; occurrence += 1) {
    const index = sourceText.indexOf(needle, searchFrom);
    if (index < 0) return -1;
    if (occurrence === zeroBasedOccurrence) return index;
    searchFrom = index + needle.length;
  }
  return -1;
}

function assertCsharpNthDeclarationAndBlockContainsOrdered(sourceText, anchor, zeroBasedOccurrence, tokens, label) {
  const anchorIndex = nthIndexOf(sourceText, anchor, zeroBasedOccurrence);
  if (anchorIndex < 0) {
    fail(`${label} 缺少第 ${zeroBasedOccurrence + 1} 个源码锚点：${anchor}`);
    return;
  }

  const block = csharpDeclarationAndBlockAfter(sourceText.slice(anchorIndex), anchor, label);
  if (block == null) return;

  assertSourceContainsOrdered(block, tokens, label);
}

function assertCsharpBlockExcludes(sourceText, anchor, forbiddenTokens, label) {
  const block = csharpBlockAfter(sourceText, anchor, label);
  if (block == null) return;

  for (const token of forbiddenTokens.filter((value) => value != null)) {
    if (block.includes(token)) {
      fail(`${label} 不应包含源码片段：${token}`);
    }
  }
}

function assertCsharpFieldInitializerEquals(targetSource, fieldName, expectedInitializer, label) {
  const match = targetSource.match(new RegExp(`\\b${escapeRegExp(fieldName)}\\s*=\\s*([^;]+);`, "m"));
  if (match == null) {
    fail(`${label} 缺少字段初始化：${fieldName} = ${expectedInitializer};`);
    return;
  }

  const actualInitializer = match[1].trim();
  if (normalizeCsharpExpression(actualInitializer) !== normalizeCsharpExpression(expectedInitializer)) {
    fail(`${label} 的字段初始化不一致：当前 ${fieldName} = ${actualInitializer};，应为 ${fieldName} = ${expectedInitializer};。`);
  }
}

function assertCsharpConstFloatEquals(targetSource, constantName, expectedLiteral, label) {
  const match = targetSource.match(new RegExp(`\\bconst\\s+float\\s+${escapeRegExp(constantName)}\\s*=\\s*([^;]+);`, "m"));
  if (match == null) {
    fail(`${label} 缺少 float 常量：${constantName} = ${expectedLiteral};`);
    return;
  }

  const actualLiteral = unityNumberLiteralFromCsharp(match[1]);
  const expectedNormalized = unityNumberLiteralFromCsharp(expectedLiteral);
  if (actualLiteral !== expectedNormalized) {
    fail(`${label} 的常量值不一致：当前 ${constantName} = ${match[1].trim()};，应为 ${constantName} = ${expectedLiteral};。`);
  }
}

function assertCsharpConstIntEquals(targetSource, constantName, expectedLiteral, label) {
  const match = targetSource.match(new RegExp(`\\bconst\\s+int\\s+${escapeRegExp(constantName)}\\s*=\\s*([^;]+);`, "m"));
  if (match == null) {
    fail(`${label} 缺少 int 常量：${constantName} = ${expectedLiteral};`);
    return;
  }

  const actualLiteral = match[1].trim();
  if (actualLiteral !== expectedLiteral) {
    fail(`${label} 的常量值不一致：当前 ${constantName} = ${actualLiteral};，应为 ${constantName} = ${expectedLiteral};。`);
  }
}

function parseGeneratedIntArrayEntries(entriesText) {
  return entriesText
    .split(",")
    .map((entry) => entry.trim())
    .filter((entry) => entry.length > 0);
}

function assertStringArraysEqual(actualEntries, expectedEntries, label) {
  if (actualEntries.length !== expectedEntries.length) {
    fail(`${label} 数量不一致：当前 ${actualEntries.length}，应为 ${expectedEntries.length}。`);
    return;
  }

  for (let index = 0; index < expectedEntries.length; index += 1) {
    if (actualEntries[index] !== expectedEntries[index]) {
      fail(`${label}[${index}] 不一致：当前 ${actualEntries[index] ?? "<缺失>"}，应为 ${expectedEntries[index]}。`);
      return;
    }
  }
}

function assertGeneratedGameplayTagEntry(sourceText, constantName, expectedParents, expectedChildren, expectedDisplayName, label) {
  const escapedConstantName = escapeRegExp(constantName);
  const tagMatch = sourceText.match(new RegExp(
    `\\{\\s*${escapedConstantName},\\s*new GameplayTag\\(${escapedConstantName},\\s*new int\\[\\]\\s*\\{([^}]*)\\},\\s*new int\\[\\]\\s*\\{([^}]*)\\}\\)\\s*\\}`,
    "m"));
  if (tagMatch == null) {
    fail(`${label} 缺少 GameplayTag 层级登记：${constantName}。`);
  } else {
    assertStringArraysEqual(
      parseGeneratedIntArrayEntries(tagMatch[1]),
      expectedParents,
      `${label} 父标签`);
    assertStringArraysEqual(
      parseGeneratedIntArrayEntries(tagMatch[2]),
      expectedChildren,
      `${label} 子标签`);
  }

  const displayMatch = sourceText.match(new RegExp(`\\{\\s*${escapedConstantName},\\s*"([^"]+)"\\s*\\}`, "m"));
  if (displayMatch == null) {
    fail(`${label} 缺少点分隔显示名登记：${constantName}。`);
  } else if (displayMatch[1] !== expectedDisplayName) {
    fail(`${label} 显示名不一致：当前 ${displayMatch[1]}，应为 ${expectedDisplayName}。`);
  }
}

function assertCsharpAssignmentEquals(targetSource, assignmentTarget, expectedInitializer, label) {
  const match = targetSource.match(new RegExp(`${escapeRegExp(assignmentTarget)}\\s*=\\s*([^;]+);`, "m"));
  if (match == null) {
    fail(`${label} 缺少赋值：${assignmentTarget} = ${expectedInitializer};`);
    return;
  }

  const actualInitializer = match[1].trim();
  if (normalizeCsharpExpression(actualInitializer) !== normalizeCsharpExpression(expectedInitializer)) {
    fail(`${label} 的赋值不一致：当前 ${assignmentTarget} = ${actualInitializer};，应为 ${assignmentTarget} = ${expectedInitializer};。`);
  }
}

function csharpDOPunchScaleParameters(sourceText, label) {
  const match = sourceText.match(/DOPunchScale\s*\(\s*new\s+Vector3\s*\(\s*([^,\)]+)\s*,\s*([^,\)]+)(?:\s*,\s*[^)]*)?\)\s*,\s*([^)]+)\)/s);
  if (match == null) {
    fail(`${label} 缺少可解析的 DOPunchScale(new Vector3(x, y), duration) 调用。`);
    return null;
  }

  const scaleX = match[1].trim();
  const scaleY = match[2].trim();
  if (unityNumberLiteralFromCsharp(scaleX) !== unityNumberLiteralFromCsharp(scaleY)) {
    fail(`${label} 的 DOPunchScale x/y 不一致，当前 Gameplay 不能只用一个弹跳幅度字段：x=${scaleX} y=${scaleY}。`);
    return null;
  }

  return {
    scale: scaleX,
    duration: match[3].trim(),
  };
}

function csharpHurtFeedbackParameters(sourceText, label) {
  const flashMatch = sourceText.match(/DOFloat\s*\(\s*([^,]+)\s*,\s*"([^"]+)"\s*,\s*([^)]+)\)/s);
  const delayMatch = sourceText.match(/\.SetDelay\s*\(\s*([^)]+)\)/s);
  const loopsMatch = sourceText.match(/\.SetLoops\s*\(\s*([^,\)]+)\s*,\s*LoopType\.Yoyo\s*\)/s);
  const punchMatch = sourceText.match(/DOPunchRotation\s*\(\s*new\s+Vector3\s*\(\s*([^,]+)\s*,\s*([^,]+)\s*,\s*([^)]+)\)\s*,\s*([^,\)]+)\s*,\s*vibrato:\s*([^)]+)\)/s);
  if (flashMatch == null || delayMatch == null || loopsMatch == null || punchMatch == null) {
    fail(`${label} 缺少可解析的受击 DOFloat / SetDelay / SetLoops / DOPunchRotation 调用。`);
    return null;
  }

  if (unityNumberLiteralFromCsharp(punchMatch[1]) !== "0" ||
      unityNumberLiteralFromCsharp(punchMatch[3]) !== "0") {
    fail(`${label} 的 DOPunchRotation 不是只绕 Y 轴摇晃，当前 Gameplay 字段不能只保存 Y 轴角度。`);
    return null;
  }

  return {
    flashProperty: flashMatch[2],
    flashTweenSeconds: flashMatch[3].trim(),
    flashDelaySeconds: delayMatch[1].trim(),
    flashLoopCount: loopsMatch[1].trim(),
    punchRotationDegrees: punchMatch[2].trim(),
    punchDurationSeconds: punchMatch[4].trim(),
    punchVibrato: punchMatch[5].trim(),
  };
}

function unityComponentInlineObjectValues(parsedYaml, objectName, classId, propertyName, label) {
  const component = unityComponentByClass(parsedYaml, objectName, classId);
  if (component == null) {
    fail(`${label} 缺少 StackCraft 来源对象 ${objectName} 的 class ${classId} 组件。`);
    return null;
  }

  return unityInlineObjectProperty(component.text, propertyName, label);
}

function assertTabletopViewSettingsMatchStackCraft(defaultCardSettingsText, targetText, targetPath, csharpSourceText = null) {
  if (defaultCardSettingsText == null) {
    fail("缺少 StackCraft Default_Card_Settings.asset，无法从参考 SO 派生牌桌视图设置参数。");
    return;
  }

  const stackStep = unityInlineObjectProperty(
    defaultCardSettingsText,
    "stackStep",
    "StackCraft Default_Card_Settings.stackStep");
  const stackHeight = stackStep?.get("y") ?? null;
  if (stackHeight == null) {
    fail("StackCraft Default_Card_Settings.stackStep 缺少 y 分量，无法对账 m_stackHeightStep。");
  } else {
    assertSerializedScalarFromReference(
      targetText,
      "m_stackHeightStep",
      stackHeight,
      targetPath,
      "StackCraft stackStep.y");
    if (csharpSourceText != null) {
      assertCsharpFieldInitializerEquals(
        csharpSourceText,
        "m_stackHeightStep",
        csharpFloatLiteral(stackHeight),
        "TabletopViewSettings 默认堆叠高度");
    }
  }

  for (const [sourceField, targetField, label] of [
    ["swaySharpness", "m_dragFollowSharpness", "StackCraft swaySharpness"],
    ["clickThreshold", "m_clickThreshold", "StackCraft clickThreshold"],
    ["attachRadius", "m_attachRadius", "StackCraft attachRadius"],
    ["dragHeight", "m_dragHeight", "StackCraft dragHeight"],
    ["moveDuration", "m_moveDurationSeconds", "StackCraft moveDuration"],
  ]) {
    const expectedValue = yamlScalarPropertyValue(defaultCardSettingsText, sourceField, label);
    if (expectedValue == null) continue;

    assertSerializedScalarFromReference(targetText, targetField, expectedValue, targetPath, label);
    if (csharpSourceText != null) {
      assertCsharpFieldInitializerEquals(
        csharpSourceText,
        targetField,
        csharpFloatLiteral(expectedValue),
        `TabletopViewSettings 默认 ${label}`);
    }
  }
}

function unityInlineObjectProperty(componentText, propertyName, label) {
  const line = unityPropertyLine(componentText, propertyName);
  if (line == null) {
    fail(`${label} 缺少 Unity 属性 ${propertyName}，无法从参考源提取参数。`);
    return null;
  }

  const match = line.match(/\{([^}]*)\}/);
  if (match == null) {
    fail(`${label} 的 ${propertyName} 不是可解析的内联对象：${line}`);
    return null;
  }

  const values = new Map();
  for (const item of match[1].split(",")) {
    const [rawKey, rawValue] = item.split(":");
    if (rawKey == null || rawValue == null) continue;
    values.set(rawKey.trim(), rawValue.trim());
  }
  return values;
}

function csharpInlineConstructor(typeName, values, fieldNames, label) {
  const literals = [];
  for (const fieldName of fieldNames) {
    const value = values.get(fieldName);
    if (value == null) {
      fail(`${label} 缺少 ${fieldName} 分量，无法生成 ${typeName} 对账值。`);
      return null;
    }
    literals.push(csharpFloatLiteral(value));
  }
  return `new ${typeName}(${literals.join(", ")})`;
}

function unityInlineObjectLiteral(values, fieldNames, label) {
  const literals = [];
  for (const fieldName of fieldNames) {
    const value = values.get(fieldName);
    if (value == null) {
      fail(`${label} 缺少 ${fieldName} 分量，无法生成 Unity YAML 对账值。`);
      return null;
    }
    literals.push(`${fieldName}: ${value}`);
  }
  return `{${literals.join(", ")}}`;
}

function unityInlineNumericObjectLiteral(values, fieldNames, label) {
  const literals = [];
  for (const fieldName of fieldNames) {
    const value = values.get(fieldName);
    if (value == null) {
      fail(`${label} 缺少 ${fieldName} 分量，无法生成 Unity YAML 数值对账值。`);
      return null;
    }
    literals.push(`${fieldName}: ${unityCanonicalNumber(value)}`);
  }
  return `{${literals.join(", ")}}`;
}

function unityFloat32ProductLiteral(left, right, label) {
  const leftNumber = Number.parseFloat(String(left).trim());
  const rightNumber = Number.parseFloat(String(right).trim());
  if (!Number.isFinite(leftNumber) || !Number.isFinite(rightNumber)) {
    fail(`${label} 无法按 C# float 乘法派生 Unity YAML 数值：${left} * ${right}`);
    return null;
  }

  const value = Math.fround(Math.fround(leftNumber) * Math.fround(rightNumber));
  if (Object.is(value, -0)) return "0";
  return Number.parseFloat(value.toPrecision(8)).toString();
}

function unityVector2LiteralFromCollider(sourceParsedYaml, sourceObjectName, label) {
  const size = unityComponentInlineObjectValues(
    sourceParsedYaml,
    sourceObjectName,
    65,
    "m_Size",
    label);
  if (size == null) return null;

  const x = size.get("x");
  const z = size.get("z");
  if (x == null || z == null) {
    fail(`${label} 的 StackCraft BoxCollider.m_Size 缺少 x/z 分量。`);
    return null;
  }

  return `{x: ${x}, y: ${z}}`;
}

function unityVector2LiteralFromColliderWithScale(
  sourceParsedYaml,
  sourceObjectName,
  scaleLiteral,
  label) {
  const size = unityComponentInlineObjectValues(
    sourceParsedYaml,
    sourceObjectName,
    65,
    "m_Size",
    label);
  if (size == null) return null;

  const x = size.get("x");
  const z = size.get("z");
  if (x == null || z == null) {
    fail(`${label} 的 StackCraft BoxCollider.m_Size 缺少 x/z 分量。`);
    return null;
  }

  const scale = unityNumberLiteralFromCsharp(scaleLiteral);
  const width = unityFloat32ProductLiteral(x, scale, `${label}.x`);
  const height = unityFloat32ProductLiteral(z, scale, `${label}.z`);
  return width == null || height == null ? null : `{x: ${width}, y: ${height}}`;
}

function assertCardViewSizeAsset(targetText, targetPath, expectedViewSize, label) {
  if (expectedViewSize == null) return;
  assertYamlScalarEquals(
    targetText,
    "m_overrideViewSize",
    "1",
    `${targetPath} ${label} 作者源牌桌可见尺寸覆盖`);
  assertYamlScalarEquals(
    targetText,
    "m_viewSize",
    expectedViewSize,
    `${targetPath} ${label} 牌桌可见尺寸`);
}

function csharpTargetTypedVector2FromUnityLiteral(vector2Literal, label) {
  if (vector2Literal == null) return null;
  const match = vector2Literal.match(/^\{x: ([^,]+), y: ([^}]+)\}$/);
  if (match == null) {
    fail(`${label} 不是可解析的 Unity Vector2 字面量：${vector2Literal}`);
    return null;
  }

  return `new(${csharpFloatLiteral(match[1])}, ${csharpFloatLiteral(match[2])})`;
}
function unityInlineNumber(values, fieldName, label) {
  const value = values?.get(fieldName);
  if (value == null) {
    fail(`${label} 缺少 ${fieldName} 分量，无法派生数值。`);
    return null;
  }

  const number = Number.parseFloat(value);
  if (!Number.isFinite(number)) {
    fail(`${label} 的 ${fieldName} 不是有效数值：${value}`);
    return null;
  }

  return number;
}

function yamlScalarNumber(text, propertyName, label) {
  const rawValue = yamlScalarPropertyValue(text, propertyName, label);
  if (rawValue == null) return null;

  const number = Number.parseFloat(rawValue);
  if (!Number.isFinite(number)) {
    fail(`${label} 的 ${propertyName} 不是有效数值：${rawValue}`);
    return null;
  }

  return number;
}

function softAssetReferenceBlock(text, fieldName, label) {
  const match = text.match(new RegExp(`^\\s*${escapeRegExp(fieldName)}:\\r?\\n((?:\\s{4}[^\\r\\n]*\\r?\\n?)+)`, "m"));
  if (match == null) {
    fail(`${label} 缺少 SoftAssetReference 字段块：${fieldName}`);
    return null;
  }

  return match[1];
}

function yamlBlockScalarValue(block, fieldName) {
  const match = block.match(new RegExp(`^\\s*(?:-\\s+)?${escapeRegExp(fieldName)}:\\s*([^\\r\\n]*)$`, "m"));
  return match?.[1]?.trim() ?? null;
}

function yamlLineIndent(line) {
  return line.match(/^\s*/)?.[0]?.length ?? 0;
}

function yamlFieldBlock(text, fieldName, label) {
  const pattern = new RegExp(`^([ \\t]*)${escapeRegExp(fieldName)}:\\s*$`, "m");
  const match = pattern.exec(text);
  if (match == null) {
    fail(`${label} 缺少 YAML 字段块：${fieldName}`);
    return null;
  }

  const baseIndent = match[1].length;
  const tail = text.slice(match.index + match[0].length).split(/\r?\n/);
  const blockLines = [];
  for (const line of tail) {
    if (line.trim().length === 0) {
      blockLines.push(line);
      continue;
    }

    if (yamlLineIndent(line) <= baseIndent) break;
    blockLines.push(line);
  }

  return blockLines.join("\n");
}

function yamlUnityListBlockInfo(text, fieldName, label) {
  const pattern = new RegExp(`^([ \\t]*)${escapeRegExp(fieldName)}:\\s*$`, "m");
  const match = pattern.exec(text);
  if (match == null) {
    fail(`${label} 缺少 YAML 列表字段：${fieldName}`);
    return null;
  }

  const baseIndent = match[1].length;
  const tail = text.slice(match.index + match[0].length).split(/\r?\n/);
  const blockLines = [];
  for (const line of tail) {
    if (line.trim().length === 0) {
      blockLines.push(line);
      continue;
    }

    const indent = yamlLineIndent(line);
    const trimmed = line.trimStart();
    if (indent < baseIndent || (indent === baseIndent && !trimmed.startsWith("- "))) break;
    blockLines.push(line);
  }

  return {
    itemIndent: baseIndent,
    text: blockLines.join("\n"),
  };
}

function yamlUnityListItemBlockByScalar(text, listFieldName, keyFieldName, expectedValue, label) {
  const block = yamlUnityListBlockInfo(text, listFieldName, label);
  if (block == null) return null;

  const entries = [];
  let current = [];
  for (const line of block.text.split(/\r?\n/)) {
    const isTopLevelItem = yamlLineIndent(line) === block.itemIndent && line.trimStart().startsWith("- ");
    if (isTopLevelItem && current.length > 0) {
      entries.push(current.join("\n"));
      current = [];
    }
    if (line.trim().length > 0 || current.length > 0) {
      current.push(line);
    }
  }
  if (current.length > 0) entries.push(current.join("\n"));

  const matched = entries.find((entry) =>
    unquoteUnityString(yamlBlockScalarValue(entry, keyFieldName) ?? "") === expectedValue) ?? null;
  if (matched == null) {
    fail(`${label} 的 ${listFieldName} 缺少 ${keyFieldName}=${expectedValue} 的列表项。`);
  }

  return matched;
}

function yamlUnityListScalarValues(text, listFieldName, scalarFieldName, label) {
  const block = yamlUnityListBlockInfo(text, listFieldName, label);
  if (block == null) return null;

  return [...block.text.matchAll(new RegExp(`^\\s*-\\s+${escapeRegExp(scalarFieldName)}:\\s*([^\\r\\n]*)$`, "gm"))]
    .map((match) => unquoteUnityString(match[1].trim()));
}

function yamlUnityListItemBlocks(text, listFieldName, label) {
  const block = yamlUnityListBlockInfo(text, listFieldName, label);
  if (block == null) return null;

  const lines = block.text.split(/\r?\n/);
  const items = [];
  let current = [];
  for (const line of lines) {
    if (/^\s*-\s+/.test(line)) {
      if (current.length > 0) items.push(current.join("\n"));
      current = [line];
      continue;
    }

    if (current.length > 0) {
      current.push(line);
    }
  }
  if (current.length > 0) items.push(current.join("\n"));

  return items;
}

function assertUnityListItemCount(text, listFieldName, expectedCount, label) {
  const items = yamlUnityListItemBlocks(text, listFieldName, label);
  if (items == null) return;

  if (items.length !== expectedCount) {
    fail(`${label} 的 ${listFieldName} 列表项数量不一致：当前 ${items.length}，应为 ${expectedCount}。`);
  }
}

function assertAttributeOverrideEquals(text, attributeCode, expectedBaseValue, label) {
  if (attributeCode == null || expectedBaseValue == null) return;

  const items = yamlUnityListItemBlocks(text, "m_attributeOverrides", label);
  if (items == null) return;

  const matched = items.filter((item) =>
    yamlBlockScalarValue(item, "m_attributeCode") === String(attributeCode));
  if (matched.length !== 1) {
    fail(`${label} 的 m_attributeOverrides 中属性码 ${attributeCode} 出现次数不一致：当前 ${matched.length}，应为 1。`);
    return;
  }

  assertYamlBlockScalarEquals(
    matched[0],
    "m_baseValue",
    String(expectedBaseValue).trim(),
    `${label} 属性码 ${attributeCode}`);
}

function assertYamlNestedScalarEquals(text, blockFieldName, scalarFieldName, expectedValue, label) {
  const block = yamlFieldBlock(text, blockFieldName, label);
  if (block == null) return;

  const actualValue = yamlBlockScalarValue(block, scalarFieldName);
  const normalizedExpectedValue = String(expectedValue).trim();
  if (actualValue !== normalizedExpectedValue) {
    fail(`${label} 的 ${blockFieldName}.${scalarFieldName} 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${normalizedExpectedValue}。`);
  }
}

function assertYamlBlockScalarEquals(block, fieldName, expectedValue, label) {
  const actualValue = yamlBlockScalarValue(block, fieldName);
  const normalizedExpectedValue = String(expectedValue).trim();
  if (actualValue !== normalizedExpectedValue) {
    fail(`${label} 的 ${fieldName} 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${normalizedExpectedValue}。`);
  }
}

function assertStringArrayEquals(actualValues, expectedValues, label) {
  if (actualValues == null) return;

  const actualText = actualValues.join(", ");
  const expectedText = expectedValues.join(", ");
  if (actualText !== expectedText) {
    fail(`${label} 不一致：当前 [${actualText}]，应为 [${expectedText}]。`);
  }
}

function unitySerializeReferenceBlockByType(text, className, namespaceName, assemblyName, label) {
  const pattern = new RegExp(
    `^\\s*- rid:\\s*([^\\r\\n]+)\\r?\\n` +
    `\\s+type: \\{class: ${escapeRegExp(className)}, ns: ${escapeRegExp(namespaceName)}, asm: ${escapeRegExp(assemblyName)}\\}\\r?\\n` +
    `((?:\\s{6,}[^\\r\\n]*\\r?\\n?)*)`,
    "m");
  const match = pattern.exec(text);
  if (match == null) {
    fail(`${label} 缺少 SerializeReference 类型：${namespaceName}.${className} (${assemblyName})。`);
    return null;
  }

  return {
    rid: match[1].trim(),
    text: match[0],
  };
}

function assertSoftAssetReference(text, fieldName, expectedAddress, expectedGuid, label) {
  const block = softAssetReferenceBlock(text, fieldName, label);
  if (block == null) return;

  if (expectedAddress != null) {
    const actualAddressValue = yamlBlockScalarValue(block, "Address");
    if (actualAddressValue == null) {
      fail(`${label} 的 ${fieldName} 缺少 Address 字段。`);
    } else {
      const actualAddress = unquoteUnityString(actualAddressValue);
      if (actualAddress !== expectedAddress) {
        fail(`${label} 的 ${fieldName}.Address 不一致：当前 ${actualAddress || "<空>"}，应为 ${expectedAddress}。`);
      }
    }
  }

  if (expectedGuid != null) {
    const actualGuid = yamlBlockScalarValue(block, "Guid");
    if (actualGuid !== expectedGuid) {
      fail(`${label} 的 ${fieldName}.Guid 不一致：当前 ${actualGuid || "<空>"}，应为 ${expectedGuid}。`);
    }
  }

  const actualLocked = yamlBlockScalarValue(block, "Locked");
  if (actualLocked !== "1") {
    fail(`${label} 的 ${fieldName}.Locked 不一致：当前 ${actualLocked ?? "<缺失>"}，应为 1；未锁定可能导致资源引用重新生成后漂移。`);
  }
}

function guidFromMetaPath(metaPath, label) {
  const guid = unityGuid(readIfExists(metaPath));
  if (guid == null) {
    fail(`${label} 缺少合法 Unity GUID：${metaPath}`);
  }
  return guid;
}

function readFbxProperty(buffer, offset, label) {
  const type = String.fromCharCode(buffer[offset]);
  offset += 1;

  switch (type) {
    case "Y":
      return { value: buffer.readInt16LE(offset), offset: offset + 2 };
    case "C":
      return { value: buffer[offset] !== 0, offset: offset + 1 };
    case "I":
      return { value: buffer.readInt32LE(offset), offset: offset + 4 };
    case "F":
      return { value: buffer.readFloatLE(offset), offset: offset + 4 };
    case "D":
      return { value: buffer.readDoubleLE(offset), offset: offset + 8 };
    case "L":
      return { value: Number(buffer.readBigInt64LE(offset)), offset: offset + 8 };
    case "S":
    case "R": {
      const length = buffer.readUInt32LE(offset);
      offset += 4;
      const raw = buffer.subarray(offset, offset + length);
      offset += length;
      return {
        value: type === "S" ? raw.toString("utf8") : raw,
        offset,
      };
    }
    case "f":
    case "d":
    case "i":
    case "l":
    case "q":
    case "b": {
      const length = buffer.readUInt32LE(offset);
      const encoding = buffer.readUInt32LE(offset + 4);
      const byteLength = buffer.readUInt32LE(offset + 8);
      offset += 12;
      let raw = buffer.subarray(offset, offset + byteLength);
      offset += byteLength;
      if (encoding === 1) {
        raw = zlib.inflateSync(raw);
      } else if (encoding !== 0) {
        fail(`${label} 使用了未支持的 FBX 数组压缩编码：${encoding}。`);
        return { value: [], offset };
      }

      const values = [];
      for (let index = 0; index < length; index++) {
        const elementOffset = type === "d" || type === "l" || type === "q"
          ? index * 8
          : index * 4;
        switch (type) {
          case "f":
            values.push(raw.readFloatLE(elementOffset));
            break;
          case "d":
            values.push(raw.readDoubleLE(elementOffset));
            break;
          case "i":
            values.push(raw.readInt32LE(elementOffset));
            break;
          case "l":
          case "q":
            values.push(Number(raw.readBigInt64LE(elementOffset)));
            break;
          case "b":
            values.push(raw[elementOffset] !== 0);
            break;
        }
      }

      return { value: values, offset };
    }
    default:
      fail(`${label} 使用了未支持的 FBX 属性类型：${type}。`);
      return { value: null, offset };
  }
}

function readFbxNode(buffer, offset, label) {
  if (offset + 13 > buffer.length) {
    return { node: null, offset: buffer.length };
  }

  const endOffset = buffer.readUInt32LE(offset);
  const propertyCount = buffer.readUInt32LE(offset + 4);
  offset += 12;
  const nameLength = buffer[offset];
  offset += 1;
  const name = buffer.subarray(offset, offset + nameLength).toString("utf8");
  offset += nameLength;

  if (endOffset === 0 && propertyCount === 0 && nameLength === 0) {
    return { node: null, offset };
  }

  const properties = [];
  for (let index = 0; index < propertyCount; index++) {
    const property = readFbxProperty(buffer, offset, label);
    properties.push(property.value);
    offset = property.offset;
  }

  const children = [];
  while (offset < endOffset) {
    const child = readFbxNode(buffer, offset, label);
    offset = child.offset;
    if (child.node == null) {
      break;
    }
    children.push(child.node);
  }

  return {
    node: {
      name,
      properties,
      children,
    },
    offset,
  };
}

function readFbxNodes(buffer, label) {
  if (buffer == null) {
    fail(`${label} 缺少 FBX 文件。`);
    return [];
  }
  if (!buffer.subarray(0, 21).toString("binary").startsWith("Kaydara FBX Binary")) {
    fail(`${label} 不是 Unity 可导入的二进制 FBX，无法静态派生 Board 网格边界。`);
    return [];
  }

  const nodes = [];
  let offset = 27;
  while (offset < buffer.length) {
    const result = readFbxNode(buffer, offset, label);
    offset = result.offset;
    if (result.node == null) {
      break;
    }
    nodes.push(result.node);
  }
  return nodes;
}

function findFbxGeometry(nodes, geometryKind) {
  const stack = [...nodes];
  while (stack.length > 0) {
    const node = stack.pop();
    if (node.name === "Geometry" && node.properties[2] === geometryKind) {
      return node;
    }
    stack.push(...node.children);
  }
  return null;
}

function fbxChild(node, childName) {
  return node?.children.find((child) => child.name === childName) ?? null;
}

function boundsFromFbxVertices(vertices) {
  let minX = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let minZ = Number.POSITIVE_INFINITY;
  let maxZ = Number.NEGATIVE_INFINITY;
  for (let offset = 0; offset < vertices.length; offset += 3) {
    const x = vertices[offset];
    const z = vertices[offset + 2];
    minX = Math.min(minX, x);
    maxX = Math.max(maxX, x);
    minZ = Math.min(minZ, z);
    maxZ = Math.max(maxZ, z);
  }

  return {
    x: minX,
    y: minZ,
    width: maxX - minX,
    height: maxZ - minZ,
  };
}

function deriveBoardMeshGeometryFromStackCraftFbx(boardFbxBuffer, label) {
  const nodes = readFbxNodes(boardFbxBuffer, label);
  const mesh = findFbxGeometry(nodes, "Mesh");
  const shape = findFbxGeometry(nodes, "Shape");
  if (mesh == null) {
    fail(`${label} 缺少 Board Mesh Geometry，无法派生运行时 BakeMesh 基础边界。`);
    return null;
  }
  if (shape == null) {
    fail(`${label} 缺少 Scale BlendShape Geometry，无法派生 LimitBooster 扩展比例。`);
    return null;
  }

  const baseVertices = fbxChild(mesh, "Vertices")?.properties[0];
  const shapeIndexes = fbxChild(shape, "Indexes")?.properties[0];
  const shapeDeltas = fbxChild(shape, "Vertices")?.properties[0];
  if (!Array.isArray(baseVertices) || baseVertices.length % 3 !== 0) {
    fail(`${label} 的 Board Mesh 顶点数组不可解析。`);
    return null;
  }
  if (!Array.isArray(shapeIndexes) || !Array.isArray(shapeDeltas) || shapeDeltas.length !== shapeIndexes.length * 3) {
    fail(`${label} 的 Scale BlendShape 顶点或索引数组不可解析。`);
    return null;
  }

  const expandedVertices = [...baseVertices];
  for (let index = 0; index < shapeIndexes.length; index++) {
    const vertexIndex = shapeIndexes[index] * 3;
    const deltaIndex = index * 3;
    expandedVertices[vertexIndex] += shapeDeltas[deltaIndex];
    expandedVertices[vertexIndex + 1] += shapeDeltas[deltaIndex + 1];
    expandedVertices[vertexIndex + 2] += shapeDeltas[deltaIndex + 2];
  }

  const baseBounds = boundsFromFbxVertices(baseVertices);
  const expandedBounds = boundsFromFbxVertices(expandedVertices);
  return {
    baseBounds,
    expansionPerPoint: {
      x: (expandedBounds.width - baseBounds.width) * 0.5 / 100,
      y: (expandedBounds.height - baseBounds.height) * 0.5 / 100,
    },
  };
}

function deriveBoardPlacementFromStackCraft(sourceParsedYaml, sourceObjectName, label) {
  const renderer = unityComponentByClass(sourceParsedYaml, sourceObjectName, 137);
  const behaviour = unityComponentByClass(sourceParsedYaml, sourceObjectName, 114);
  if (renderer == null) {
    fail(`${label} 缺少 SkinnedMeshRenderer，无法从 StackCraft Board 派生牌桌边界。`);
    return null;
  }
  if (behaviour == null) {
    fail(`${label} 缺少 Board MonoBehaviour，无法从 StackCraft Board 派生页眉禁放区。`);
    return null;
  }

  const meshGeometry = stackCraftBoardMeshGeometry;
  if (meshGeometry == null) {
    return null;
  }

  const center = unityInlineObjectProperty(renderer.text, "m_Center", `${label}.m_AABB.m_Center`);
  const extent = unityInlineObjectProperty(renderer.text, "m_Extent", `${label}.m_AABB.m_Extent`);
  const topMargin = yamlScalarNumber(behaviour.text, "topMargin", `${label}.topMargin`);
  const centerX = unityInlineNumber(center, "x", `${label}.m_AABB.m_Center`);
  const centerZ = unityInlineNumber(center, "z", `${label}.m_AABB.m_Center`);
  const extentX = unityInlineNumber(extent, "x", `${label}.m_AABB.m_Extent`);
  const extentZ = unityInlineNumber(extent, "z", `${label}.m_AABB.m_Extent`);
  if ([centerX, centerZ, extentX, extentZ, topMargin].some((value) => value == null)) {
    return null;
  }

  const bounds = meshGeometry.baseBounds;
  const restricted = {
    x: bounds.x,
    y: bounds.y + bounds.height - topMargin,
    width: bounds.width,
    height: topMargin,
  };
  const localBoundsSize = {
    x: extentX * 2,
    y: 0,
    z: extentZ * 2,
  };

  return {
    bounds,
    restricted,
    localBoundsSize,
    expansionPerPoint: meshGeometry.expansionPerPoint,
  };
}

function deriveCardPlacementGeometryFromStackCraft(cardPrefabText, defaultCardSettingsText, label) {
  if (cardPrefabText == null) {
    fail(`${label} 缺少 StackCraft Card_Character.prefab，无法派生卡牌占位几何。`);
    return null;
  }
  if (defaultCardSettingsText == null) {
    fail(`${label} 缺少 StackCraft Default_Card_Settings.asset，无法派生卡牌 margin / stackStep。`);
    return null;
  }

  const cardYaml = unityYamlObjects(cardPrefabText);
  const colliderSize = unityComponentInlineObjectValues(
    cardYaml,
    "Card_Character",
    65,
    "m_Size",
    `${label} Card_Character BoxCollider.m_Size`);
  const margin = unityInlineObjectProperty(
    defaultCardSettingsText,
    "margin",
    `${label} Default_Card_Settings.margin`);
  const stackStep = unityInlineObjectProperty(
    defaultCardSettingsText,
    "stackStep",
    `${label} Default_Card_Settings.stackStep`);
  if (colliderSize == null || margin == null || stackStep == null) return null;

  const cardSizeX = unityInlineNumber(colliderSize, "x", `${label} Card_Character BoxCollider.m_Size`);
  const cardSizeY = unityInlineNumber(colliderSize, "z", `${label} Card_Character BoxCollider.m_Size`);
  const cardMarginX = unityInlineNumber(margin, "x", `${label} Default_Card_Settings.margin`);
  const cardMarginY = unityInlineNumber(margin, "y", `${label} Default_Card_Settings.margin`);
  const stackStepX = unityInlineNumber(stackStep, "x", `${label} Default_Card_Settings.stackStep`);
  const stackStepY = unityInlineNumber(stackStep, "z", `${label} Default_Card_Settings.stackStep`);
  if ([cardSizeX, cardSizeY, cardMarginX, cardMarginY, stackStepX, stackStepY].some((value) => value == null)) {
    return null;
  }

  return {
    cardSize: { x: cardSizeX, y: cardSizeY },
    cardMargin: { x: cardMarginX, y: cardMarginY },
    stackStep: { x: stackStepX, y: stackStepY },
  };
}

function csharpVector2ConstructorFromNumbers(first, second) {
  return `new Vector2(${csharpFloatLiteralFromUnityNumber(first)}, ${csharpFloatLiteralFromUnityNumber(second)})`;
}

function csharpVector3ConstructorFromNumbers(first, second, third) {
  return `new Vector3(${csharpFloatLiteralFromUnityNumber(first)}, ${csharpFloatLiteralFromUnityNumber(second)}, ${csharpFloatLiteralFromUnityNumber(third)})`;
}

function csharpConstructorFromUnityComponentInlineProperty(
  sourceParsedYaml,
  sourceObjectName,
  sourceClassId,
  propertyName,
  typeName,
  fieldNames,
  label) {
  const component = unityComponentByClass(sourceParsedYaml, sourceObjectName, sourceClassId);
  if (component == null) {
    fail(`${label} 缺少 StackCraft 来源对象 ${sourceObjectName} 的 class ${sourceClassId} 组件。`);
    return null;
  }

  const values = unityInlineObjectProperty(component.text, propertyName, label);
  if (values == null) return null;
  return csharpInlineConstructor(typeName, values, fieldNames, label);
}

function unityVector2YamlLiteralFromNumbers(first, second) {
  return `{x: ${unityCanonicalNumber(first)}, y: ${unityCanonicalNumber(second)}}`;
}

function assertYamlRectBlockEquals(text, blockFieldName, expectedRect, label) {
  const block = yamlFieldBlock(text, blockFieldName, label);
  if (block == null) return;

  for (const [fieldName, expectedValue] of [
    ["x", expectedRect.x],
    ["y", expectedRect.y],
    ["width", expectedRect.width],
    ["height", expectedRect.height],
  ]) {
    assertYamlBlockScalarEquals(
      block,
      fieldName,
      unityCanonicalNumber(expectedValue),
      `${label} ${blockFieldName}`);
  }
}

function assertYamlSingleRectListEquals(text, listFieldName, expectedRect, label) {
  const block = yamlUnityListBlockInfo(text, listFieldName, label);
  if (block == null) return;

  const itemCount = [...block.text.matchAll(/^\s*-\s+/gm)].length;
  if (itemCount !== 1) {
    fail(`${label} 的 ${listFieldName} 列表项数量不一致：当前 ${itemCount}，应为 1。`);
    return;
  }

  for (const [fieldName, expectedValue] of [
    ["x", expectedRect.x],
    ["y", expectedRect.y],
    ["width", expectedRect.width],
    ["height", expectedRect.height],
  ]) {
    assertYamlBlockScalarEquals(
      block.text,
      fieldName,
      unityCanonicalNumber(expectedValue),
      `${label} ${listFieldName}[0]`);
  }
}

function assertYamlInlineVector2Equals(text, fieldName, expectedVector, label) {
  const values = unityInlineObjectProperty(text, fieldName, label);
  if (values == null) return;

  for (const [componentName, expectedValue] of [
    ["x", expectedVector.x],
    ["y", expectedVector.y],
  ]) {
    const actualValue = values.get(componentName);
    const normalizedExpectedValue = unityCanonicalNumber(expectedValue);
    if (actualValue !== normalizedExpectedValue) {
      fail(`${label} 的 ${fieldName}.${componentName} 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${normalizedExpectedValue}。`);
    }
  }
}

function csharpRectConstructor(rect) {
  return `new Rect(${csharpFloatLiteralFromUnityNumber(rect.x)}, ${csharpFloatLiteralFromUnityNumber(rect.y)}, ${csharpFloatLiteralFromUnityNumber(rect.width)}, ${csharpFloatLiteralFromUnityNumber(rect.height)})`;
}

function assertCsharpConstantFromUnityInlineProperty(
  sourceParsedYaml,
  sourceObjectName,
  sourceClassId,
  propertyName,
  targetSource,
  constantName,
  typeName,
  fieldNames,
  label) {
  const sourceComponent = unityComponentByClass(sourceParsedYaml, sourceObjectName, sourceClassId);
  if (sourceComponent == null) {
    fail(`${label} 缺少 StackCraft 来源对象 ${sourceObjectName} 的 class ${sourceClassId} 组件。`);
    return;
  }

  const values = unityInlineObjectProperty(sourceComponent.text, propertyName, label);
  if (values == null) return;

  const constructorText = csharpInlineConstructor(typeName, values, fieldNames, label);
  if (constructorText == null) return;

  assertCsharpFieldInitializerEquals(
    targetSource,
    constantName,
    constructorText,
    `${label} 从 StackCraft ${sourceObjectName}.${propertyName} 派生的运行时常量`);
}

function assertSharedCsharpConstantFromUnityInlineProperties(
  sourceEntries,
  targetSource,
  constantName,
  typeName,
  fieldNames,
  label) {
  let expectedConstructorText = null;
  let expectedSourceLabel = null;

  for (const entry of sourceEntries) {
    if (entry.sourceParsedYaml == null) continue;

    const sourceComponent = unityComponentByClass(
      entry.sourceParsedYaml,
      entry.sourceObjectName,
      entry.sourceClassId);
    const sourceLabel = `${entry.sourcePrefabLabel}.${entry.sourceObjectName}.${entry.propertyName}`;
    if (sourceComponent == null) {
      fail(`${label} 缺少 StackCraft 来源对象 ${sourceLabel} 的 class ${entry.sourceClassId} 组件。`);
      continue;
    }

    const values = unityInlineObjectProperty(sourceComponent.text, entry.propertyName, `${label} ${sourceLabel}`);
    if (values == null) continue;

    const constructorText = csharpInlineConstructor(typeName, values, fieldNames, `${label} ${sourceLabel}`);
    if (constructorText == null) continue;

    if (expectedConstructorText == null) {
      expectedConstructorText = constructorText;
      expectedSourceLabel = sourceLabel;
      continue;
    }

    if (normalizeCsharpExpression(constructorText) !== normalizeCsharpExpression(expectedConstructorText)) {
      fail(`${label} 的 StackCraft 来源锚点不一致，不能共用 ${constantName}：${sourceLabel}=${constructorText}，${expectedSourceLabel}=${expectedConstructorText}。`);
    }
  }

  if (expectedConstructorText == null) {
    fail(`${label} 没有可解析的 StackCraft 来源锚点，无法对账 ${constantName}。`);
    return;
  }

  assertCsharpFieldInitializerEquals(
    targetSource,
    constantName,
    expectedConstructorText,
    `${label} 从多个 StackCraft 文本 RectTransform.m_AnchoredPosition 派生的共享运行时常量`);
}

function assertSharedCsharpIntConstantFromUnityScalarProperties(
  sourceEntries,
  targetSource,
  constantName,
  label) {
  let expectedValue = null;
  let expectedSourceLabel = null;

  for (const entry of sourceEntries) {
    const sourceLabel = `${entry.sourcePrefabLabel}.${entry.sourceObjectName}.${entry.propertyName}`;
    if (entry.sourceParsedYaml == null) {
      fail(`${label} 缺少 StackCraft 来源 ${entry.sourcePrefabLabel}，无法对账 ${constantName}。`);
      continue;
    }

    const sourceComponent = unityComponentByClass(
      entry.sourceParsedYaml,
      entry.sourceObjectName,
      entry.sourceClassId);
    if (sourceComponent == null) {
      fail(`${label} 缺少 StackCraft 来源对象 ${sourceLabel} 的 class ${entry.sourceClassId} 组件。`);
      continue;
    }

    const value = unityPropertyValue(sourceComponent.text, entry.propertyName);
    if (value == null) {
      fail(`${label} 缺少可解析的 Unity 标量属性 ${sourceLabel}。`);
      continue;
    }
    if (!/^-?\d+$/.test(value)) {
      fail(`${label} 的 ${sourceLabel} 不是整数标量：${value}。`);
      continue;
    }

    if (expectedValue == null) {
      expectedValue = value;
      expectedSourceLabel = sourceLabel;
      continue;
    }

    if (value !== expectedValue) {
      fail(`${label} 的 StackCraft 来源整数不一致，不能共用 ${constantName}：${sourceLabel}=${value}，${expectedSourceLabel}=${expectedValue}。`);
    }
  }

  if (expectedValue == null) {
    fail(`${label} 没有可解析的 StackCraft 来源整数，无法对账 ${constantName}。`);
    return;
  }

  assertCsharpFieldInitializerEquals(
    targetSource,
    constantName,
    expectedValue,
    `${label} 从多个 StackCraft TMP 文本字段派生的共享生成器常量`);
}

function assertCsharpConstantFromUnityScalarProperty(
  sourceParsedYaml,
  sourceObjectName,
  sourceClassId,
  propertyName,
  targetSource,
  constantName,
  label) {
  const sourceComponent = unityComponentByClass(sourceParsedYaml, sourceObjectName, sourceClassId);
  if (sourceComponent == null) {
    fail(`${label} 缺少 StackCraft 来源对象 ${sourceObjectName} 的 class ${sourceClassId} 组件。`);
    return;
  }

  const line = unityPropertyLine(sourceComponent.text, propertyName);
  const match = line?.match(/:\s*([^\r\n]+)/);
  if (match == null) {
    fail(`${label} 缺少可解析的 Unity 标量属性 ${propertyName}。`);
    return;
  }

  assertCsharpFieldInitializerEquals(
    targetSource,
    constantName,
    csharpFloatLiteral(match[1]),
    `${label} 从 StackCraft ${sourceObjectName}.${propertyName} 派生的运行时常量`);
}

function assertUnityGameObjectExists(parsedYaml, objectName, label) {
  if (unityGameObjectByName(parsedYaml, objectName) == null) {
    fail(`${label} 缺少对象：${objectName}`);
  }
}

function assertUnityComponentExists(parsedYaml, objectName, classId, label) {
  const component = unityComponentByClass(parsedYaml, objectName, classId);
  if (component == null) {
    fail(`${label} 缺少 ${objectName} 的 Unity 组件 class ${classId}。`);
  }
}

function assertUnityGameObjectActiveState(parsedYaml, objectName, expectedValue, label) {
  const gameObject = unityGameObjectByName(parsedYaml, objectName);
  if (gameObject == null) {
    fail(`${label} 缺少对象：${objectName}`);
    return;
  }

  const actualValue = unityPropertyValue(gameObject.text, "m_IsActive");
  const normalizedExpectedValue = expectedValue ? "1" : "0";
  if (actualValue !== normalizedExpectedValue) {
      fail(`${label} 的 ${objectName}.m_IsActive 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${normalizedExpectedValue}。`);
  }
}

function assertUnityGameObjectLayer(parsedYaml, objectName, expectedLayer, label) {
  const gameObject = unityGameObjectByName(parsedYaml, objectName);
  if (gameObject == null) {
    fail(`${label} 缺少对象：${objectName}`);
    return;
  }

  const actualLayer = unityPropertyValue(gameObject.text, "m_Layer");
  const expectedLayerText = String(expectedLayer);
  if (actualLayer !== expectedLayerText) {
    fail(`${label} 的 ${objectName}.m_Layer 不一致：当前 ${actualLayer ?? "<缺失>"}，应为 ${expectedLayerText}。`);
  }
}

function assertAllUnityGameObjectsLayer(parsedYaml, expectedLayer, label) {
  const expectedLayerText = String(expectedLayer);
  for (const gameObject of parsedYaml.objects.filter((unityObject) => unityObject.classId === "1")) {
    const objectName = unquoteUnityString(unityPropertyValue(gameObject.text, "m_Name") ?? "<未命名>");
    const actualLayer = unityPropertyValue(gameObject.text, "m_Layer");
    if (actualLayer !== expectedLayerText) {
      fail(`${label} 的 ${objectName}.m_Layer 不一致：当前 ${actualLayer ?? "<缺失>"}，应为 ${expectedLayerText}。`);
    }
  }
}

function assertUnityComponentScalarEquals(parsedYaml, objectName, classId, fieldName, expectedValue, label) {
  if (expectedValue == null) return;

  const component = unityComponentByClass(parsedYaml, objectName, classId);
  if (component == null) {
    fail(`${label} 缺少 ${objectName} 的 Unity 组件 class ${classId}，无法做字段级对账。`);
    return;
  }

  const actualValue = unityPropertyValue(component.text, fieldName);
  const normalizedExpectedValue = String(expectedValue).trim();
  if (actualValue !== normalizedExpectedValue) {
    fail(`${label} 的 ${objectName}.${fieldName} 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${normalizedExpectedValue}。`);
  }
}

function assertUnityComponentByPropertyScalarEquals(
  parsedYaml,
  objectName,
  classId,
  discriminatorFieldName,
  fieldName,
  expectedValue,
  label) {
  const components = unityComponentsByClass(parsedYaml, objectName, classId)
    .filter((component) => unityPropertyLine(component.text, discriminatorFieldName) != null);
  if (components.length === 0) {
    fail(`${label} 缺少 ${objectName} 的 Unity 组件 class ${classId}，且没有字段 ${discriminatorFieldName}，无法做字段级对账。`);
    return;
  }
  if (components.length > 1) {
    fail(`${label} 命中多个 ${objectName} 的 Unity 组件 class ${classId} / ${discriminatorFieldName}，无法证明字段唯一。`);
    return;
  }

  const actualValue = unityPropertyValue(components[0].text, fieldName);
  const normalizedExpectedValue = String(expectedValue).trim();
  if (actualValue !== normalizedExpectedValue) {
    fail(`${label} 的 ${objectName}.${fieldName} 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${normalizedExpectedValue}。`);
  }
}

function assertUnityComponentReferenceEquals(
  parsedYaml,
  objectName,
  classId,
  fieldName,
  expectedFileId,
  expectedGuid,
  expectedType,
  label) {
  if (expectedGuid == null) {
    fail(`${label} 缺少 ${fieldName} 的期望 GUID，无法做组件字段引用对账。`);
    return;
  }

  const component = unityComponentByClass(parsedYaml, objectName, classId);
  if (component == null) {
    fail(`${label} 缺少 ${objectName} 的 Unity 组件 class ${classId}，无法做字段引用对账。`);
    return;
  }

  const expectedLine = unityReferenceLine(fieldName, expectedFileId, expectedGuid, expectedType);
  const actualLine = unityPropertyLine(component.text, fieldName);
  if (actualLine !== expectedLine) {
    fail(`${label} 的 ${objectName}.${fieldName} 没有字段级引用目标资源：当前 ${actualLine ?? "<缺失>"}，应为 ${expectedLine}。`);
  }
}

function assertUnityComponentReferenceListEquals(
  parsedYaml,
  objectName,
  classId,
  fieldName,
  expectedReferences,
  label) {
  const component = unityComponentByClass(parsedYaml, objectName, classId);
  if (component == null) {
    fail(`${label} 缺少 ${objectName} 的 Unity 组件 class ${classId}，无法做列表引用对账。`);
    return;
  }

  const expectedLines = [];
  for (const reference of expectedReferences) {
    if (reference.guid == null) {
      fail(`${label} 缺少 ${fieldName} 的期望 GUID，无法做组件列表引用对账。`);
      return;
    }
    expectedLines.push(`- {fileID: ${reference.fileId}, guid: ${reference.guid}, type: ${reference.type}}`);
  }

  const actualLines = yamlReferenceListLines(component.text, fieldName);
  if (actualLines == null) {
    fail(`${label} 的 ${objectName}.${fieldName} 缺少可解析的引用列表。`);
    return;
  }

  const actualText = actualLines.join("\n");
  const expectedText = expectedLines.join("\n");
  if (actualText !== expectedText) {
    fail(`${label} 的 ${objectName}.${fieldName} 引用列表不一致：当前 ${actualText}，应为 ${expectedText}。`);
  }
}

function unityMonoBehaviourByEditorClassIdentifier(parsedYaml, objectName, editorClassIdentifier) {
  return unityComponentsByClass(parsedYaml, objectName, 114)
    .find((component) =>
      unquoteUnityString(unityPropertyValue(component.text, "m_EditorClassIdentifier") ?? "") === editorClassIdentifier) ?? null;
}

function unityMonoBehaviourObjectsByEditorClassIdentifier(parsedYaml, editorClassIdentifier) {
  return parsedYaml.objects.filter((unityObject) =>
    unityObject.classId === "114" &&
    unquoteUnityString(unityPropertyValue(unityObject.text, "m_EditorClassIdentifier") ?? "") === editorClassIdentifier);
}

function uniqueUnityMonoBehaviourObjectByEditorClassIdentifier(parsedYaml, editorClassIdentifier, label) {
  const components = unityMonoBehaviourObjectsByEditorClassIdentifier(parsedYaml, editorClassIdentifier);
  if (components.length === 0) {
    fail(label + " 缺少脚本对象 " + editorClassIdentifier + "。");
    return null;
  }
  if (components.length > 1) {
    fail(label + " 命中多个脚本对象 " + editorClassIdentifier + "，无法证明对象唯一。");
    return null;
  }

  return components[0];
}
function assertUnityMonoBehaviourPropertyExists(parsedYaml, objectName, editorClassIdentifier, fieldName, label) {
  const component = unityMonoBehaviourByEditorClassIdentifier(parsedYaml, objectName, editorClassIdentifier);
  if (component == null) {
    fail(`${label} 缺少 ${objectName} 上的脚本组件 ${editorClassIdentifier}，无法做字段存在性对账。`);
    return;
  }

  if (unityPropertyLine(component.text, fieldName) == null) {
    fail(`${label} 的 ${objectName}.${editorClassIdentifier} 缺少字段：${fieldName}。`);
  }
}

function assertUnityMonoBehaviourPropertyAbsent(parsedYaml, objectName, editorClassIdentifier, fieldName, label) {
  const component = unityMonoBehaviourByEditorClassIdentifier(parsedYaml, objectName, editorClassIdentifier);
  if (component == null) {
    fail(`${label} 缺少 ${objectName} 上的脚本组件 ${editorClassIdentifier}，无法做字段不存在对账。`);
    return;
  }

  if (unityPropertyLine(component.text, fieldName) != null) {
    fail(`${label} 的 ${objectName}.${editorClassIdentifier} 仍保留不应手填的字段：${fieldName}。`);
  }
}

function assertUnityMonoBehaviourScalarEquals(
  parsedYaml,
  objectName,
  editorClassIdentifier,
  fieldName,
  expectedValue,
  label) {
  if (expectedValue == null) return;

  const component = unityMonoBehaviourByEditorClassIdentifier(parsedYaml, objectName, editorClassIdentifier);
  if (component == null) {
    fail(`${label} 缺少 ${objectName} 上的脚本组件 ${editorClassIdentifier}，无法做字段级对账。`);
    return;
  }

  const actualValue = unityPropertyValue(component.text, fieldName);
  const normalizedExpectedValue = String(expectedValue).trim();
  if (actualValue !== normalizedExpectedValue) {
    fail(`${label} 的 ${objectName}.${fieldName} 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${normalizedExpectedValue}。`);
  }
}

function assertUnityMonoBehaviourNestedScalarEquals(
  parsedYaml,
  objectName,
  editorClassIdentifier,
  blockFieldName,
  scalarFieldName,
  expectedValue,
  label) {
  if (expectedValue == null) return;

  const component = unityMonoBehaviourByEditorClassIdentifier(parsedYaml, objectName, editorClassIdentifier);
  if (component == null) {
    fail(`${label} 缺少 ${objectName} 上的脚本组件 ${editorClassIdentifier}，无法做嵌套字段级对账。`);
    return;
  }

  assertYamlNestedScalarEquals(
    component.text,
    blockFieldName,
    scalarFieldName,
    expectedValue,
    `${label} ${objectName}`);
}

function assertUnityComponentFieldReferences(
  parsedYaml,
  ownerObjectName,
  fieldName,
  targetObjectName,
  targetClassId,
  label) {
  const targetComponent = unityComponentByClass(parsedYaml, targetObjectName, targetClassId);
  if (targetComponent == null) {
    fail(`${label} 缺少被引用对象 ${targetObjectName} 的 Unity 组件 class ${targetClassId}。`);
    return;
  }

  const ownerMonoBehaviours = unityComponentsByClass(parsedYaml, ownerObjectName, 114);
  if (ownerMonoBehaviours.length === 0) {
    fail(`${label} 缺少持有字段的 MonoBehaviour：${ownerObjectName}`);
    return;
  }

  const referencePattern = new RegExp(`^\\s*${escapeRegExp(fieldName)}: \\{fileID: ${escapeRegExp(targetComponent.fileId)}\\}`, "m");
  if (!ownerMonoBehaviours.some((component) => referencePattern.test(component.text))) {
    fail(`${label} 的 ${ownerObjectName}.${fieldName} 没有指向 ${targetObjectName} 的 class ${targetClassId} 组件 fileID ${targetComponent.fileId}。`);
  }
}

function assertUnityMonoBehaviourFieldReferences(
  parsedYaml,
  ownerObjectName,
  fieldName,
  targetObjectName,
  targetEditorClassIdentifier,
  label) {
  const targetComponent = unityMonoBehaviourByEditorClassIdentifier(
    parsedYaml,
    targetObjectName,
    targetEditorClassIdentifier);
  if (targetComponent == null) {
    fail(`${label} 缺少被引用对象 ${targetObjectName} 的脚本组件 ${targetEditorClassIdentifier}。`);
    return;
  }

  const ownerMonoBehaviours = unityComponentsByClass(parsedYaml, ownerObjectName, 114);
  if (ownerMonoBehaviours.length === 0) {
    fail(`${label} 缺少持有字段的 MonoBehaviour：${ownerObjectName}`);
    return;
  }

  const referencePattern = new RegExp(`^\\s*${escapeRegExp(fieldName)}: \\{fileID: ${escapeRegExp(targetComponent.fileId)}\\}`, "m");
  if (!ownerMonoBehaviours.some((component) => referencePattern.test(component.text))) {
    fail(`${label} 的 ${ownerObjectName}.${fieldName} 没有指向 ${targetObjectName} 的脚本组件 ${targetEditorClassIdentifier} fileID ${targetComponent.fileId}。`);
  }
}

function unityReferenceFileIdValue(text, fieldName) {
  const line = unityPropertyLine(text, fieldName);
  return line?.match(/\{fileID: (-?\d+)/)?.[1] ?? null;
}

function unityGameObjectNameByTransformFileId(parsedYaml, transformFileId) {
  const transform = parsedYaml.byFileId.get(transformFileId);
  if (transform == null) return null;

  const gameObjectFileId = unityReferenceFileIdValue(transform.text, "m_GameObject");
  if (gameObjectFileId == null) return null;

  const gameObject = parsedYaml.byFileId.get(gameObjectFileId);
  if (gameObject == null || gameObject.classId !== "1") return null;

  return unquoteUnityString(unityPropertyValue(gameObject.text, "m_Name") ?? "");
}

function unityComponentDirectParentObjectName(parsedYaml, component) {
  const gameObjectFileId = unityReferenceFileIdValue(component.text, "m_GameObject");
  if (gameObjectFileId == null) return null;

  const gameObject = parsedYaml.byFileId.get(gameObjectFileId);
  const rectTransform = unityComponentsByClassOnGameObject(parsedYaml, gameObject, 224)[0];
  if (rectTransform == null) return null;

  const parentTransformFileId = unityReferenceFileIdValue(rectTransform.text, "m_Father");
  if (parentTransformFileId == null || parentTransformFileId === "0") return null;

  return unityGameObjectNameByTransformFileId(parsedYaml, parentTransformFileId);
}

function unityChildComponentsByParentName(parsedYaml, childObjectName, parentObjectName, classId) {
  return unityGameObjectsByName(parsedYaml, childObjectName)
    .flatMap((gameObject) => unityComponentsByClassOnGameObject(parsedYaml, gameObject, classId))
    .filter((component) => unityComponentDirectParentObjectName(parsedYaml, component) === parentObjectName);
}

function assertUnityChildComponentScalarEquals(
  parsedYaml,
  childObjectName,
  parentObjectName,
  classId,
  fieldName,
  expectedValue,
  label) {
  const components = unityChildComponentsByParentName(parsedYaml, childObjectName, parentObjectName, classId);
  if (components.length === 0) {
    fail(`${label} 缺少 ${parentObjectName}/${childObjectName} 的 Unity 组件 class ${classId}。`);
    return;
  }
  if (components.length > 1) {
    fail(`${label} 命中多个 ${parentObjectName}/${childObjectName} 的 Unity 组件 class ${classId}，无法证明字段唯一。`);
    return;
  }

  const actualValue = unityPropertyValue(components[0].text, fieldName);
  const normalizedExpectedValue = String(expectedValue).trim();
  if (actualValue !== normalizedExpectedValue) {
    fail(`${label} 的 ${parentObjectName}/${childObjectName}.${fieldName} 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${normalizedExpectedValue}。`);
  }
}

function assertUnityMonoBehaviourFieldReferencesChild(
  parsedYaml,
  ownerObjectName,
  fieldName,
  targetObjectName,
  targetParentObjectName,
  targetEditorClassIdentifier,
  label) {
  const targetComponents = unityGameObjectsByName(parsedYaml, targetObjectName)
    .flatMap((gameObject) => unityComponentsByClassOnGameObject(parsedYaml, gameObject, 114))
    .filter((component) =>
      unquoteUnityString(unityPropertyValue(component.text, "m_EditorClassIdentifier") ?? "") ===
        targetEditorClassIdentifier &&
      unityComponentDirectParentObjectName(parsedYaml, component) === targetParentObjectName);

  if (targetComponents.length === 0) {
    fail(`${label} 缺少 ${targetParentObjectName} 下被引用对象 ${targetObjectName} 的脚本组件 ${targetEditorClassIdentifier}。`);
    return;
  }

  const ownerMonoBehaviours = unityComponentsByClass(parsedYaml, ownerObjectName, 114);
  if (ownerMonoBehaviours.length === 0) {
    fail(`${label} 缺少持有字段的 MonoBehaviour：${ownerObjectName}`);
    return;
  }

  if (!ownerMonoBehaviours.some((owner) =>
      targetComponents.some((target) =>
        new RegExp(`^\\s*${escapeRegExp(fieldName)}: \\{fileID: ${escapeRegExp(target.fileId)}\\}`, "m")
          .test(owner.text)))) {
    fail(`${label} 的 ${ownerObjectName}.${fieldName} 没有指向 ${targetParentObjectName}/${targetObjectName} 的脚本组件 ${targetEditorClassIdentifier}。`);
  }
}

function assertUnityMonoBehaviourFieldReferencesGuid(
  parsedYaml,
  ownerObjectName,
  fieldName,
  expectedFileId,
  expectedGuid,
  expectedType,
  label) {
  if (expectedGuid == null) {
    fail(`${label} 缺少 ${fieldName} 的期望 GUID，无法做对象级字段引用对账。`);
    return;
  }
  if (expectedFileId == null) {
    fail(`${label} 缺少 ${fieldName} 的期望 fileID，无法做对象级字段引用对账。`);
    return;
  }

  const ownerMonoBehaviours = unityComponentsByClass(parsedYaml, ownerObjectName, 114);
  if (ownerMonoBehaviours.length === 0) {
    fail(`${label} 缺少持有字段的 MonoBehaviour：${ownerObjectName}`);
    return;
  }

  const expectedLine = unityReferenceLine(fieldName, expectedFileId, expectedGuid, expectedType);
  if (!ownerMonoBehaviours.some((component) => unityPropertyLine(component.text, fieldName) === expectedLine)) {
    fail(`${label} 的 ${ownerObjectName}.${fieldName} 没有对象级引用目标资源：${expectedLine}`);
  }
}

function assertUnityComponentPropertiesMatch(
  sourceParsedYaml,
  sourceObjectName,
  targetParsedYaml,
  targetObjectName,
  classId,
  propertyNames,
  label) {
  const sourceComponent = unityComponentByClass(sourceParsedYaml, sourceObjectName, classId);
  const targetComponent = unityComponentByClass(targetParsedYaml, targetObjectName, classId);
  if (sourceComponent == null) {
    fail(`${label} 缺少 StackCraft 来源对象 ${sourceObjectName} 的 class ${classId} 组件，无法确定参考参数。`);
    return;
  }
  if (targetComponent == null) {
    fail(`${label} 缺少当前对象 ${targetObjectName} 的 class ${classId} 组件，不能证明复刻到正确对象。`);
    return;
  }

  for (const propertyName of propertyNames) {
    const sourceLine = unityPropertyLine(sourceComponent.text, propertyName);
    const targetLine = unityPropertyLine(targetComponent.text, propertyName);
    if (sourceLine == null) {
      fail(`${label} 的 StackCraft 来源 ${sourceObjectName} 缺少 ${propertyName}，请先复核参考 Prefab。`);
      continue;
    }
    if (targetLine !== sourceLine) {
      fail(`${label} 的 ${targetObjectName}.${propertyName} 没有对齐 StackCraft ${sourceObjectName}：当前 ${targetLine ?? "<缺失>"}，应为 ${sourceLine}`);
    }
  }
}

function assertUnityComponentInlineNumericPropertyMatches(
  parsedYaml,
  objectName,
  classId,
  propertyName,
  expectedValues,
  fieldNames,
  label,
  tolerance = 0.00001) {
  const component = unityComponentByClass(parsedYaml, objectName, classId);
  if (component == null) {
    fail(`${label} 缺少 ${objectName} 的 Unity 组件 class ${classId}，无法做数值字段对账。`);
    return;
  }

  const actualValues = unityInlineObjectProperty(component.text, propertyName, label);
  if (actualValues == null) return;

  for (const fieldName of fieldNames) {
    const expected = expectedValues.get(fieldName);
    const actual = actualValues.get(fieldName);
    if (expected == null || actual == null) {
      fail(`${label} 的 ${propertyName} 缺少 ${fieldName} 分量，当前 ${actual ?? "<缺失>"}，应为 ${expected ?? "<缺失>"}。`);
      continue;
    }

    const expectedNumber = Number.parseFloat(String(expected));
    const actualNumber = Number.parseFloat(String(actual));
    if (!Number.isFinite(expectedNumber) || !Number.isFinite(actualNumber)) {
      fail(`${label} 的 ${propertyName}.${fieldName} 不是可比较数值：当前 ${actual}，应为 ${expected}。`);
      continue;
    }

    if (Math.abs(expectedNumber - actualNumber) > tolerance) {
      fail(`${label} 的 ${propertyName}.${fieldName} 没有对齐：当前 ${actual}，应为 ${expected}。`);
    }
  }
}

function unityMonoBehaviourScriptGuid(componentText) {
  return componentText.match(/m_Script: \{fileID: 11500000, guid: ([0-9a-fA-F]{32}), type: 3\}/)?.[1] ?? null;
}

function unityMonoBehaviourByScriptGuid(parsedYaml, objectName, scriptGuid) {
  return unityComponentsByClass(parsedYaml, objectName, 114)
    .find((component) => unityMonoBehaviourScriptGuid(component.text) === scriptGuid) ?? null;
}

function assertUnityMonoBehaviourPropertiesMatchBySourceProperty(
  sourceParsedYaml,
  sourceObjectName,
  sourcePropertyName,
  targetParsedYaml,
  targetObjectName,
  propertyNames,
  label) {
  const sourceComponent = unityComponentByProperty(sourceParsedYaml, sourceObjectName, 114, sourcePropertyName);
  if (sourceComponent == null) {
    fail(`${label} 缺少 StackCraft 来源对象 ${sourceObjectName} 上带 ${sourcePropertyName} 的 MonoBehaviour。`);
    return;
  }

  const scriptGuid = unityMonoBehaviourScriptGuid(sourceComponent.text);
  if (scriptGuid == null) {
    fail(`${label} 无法从 StackCraft 来源 ${sourceObjectName}.${sourcePropertyName} 派生脚本 GUID。`);
    return;
  }

  const targetComponent = unityMonoBehaviourByScriptGuid(targetParsedYaml, targetObjectName, scriptGuid);
  if (targetComponent == null) {
    fail(`${label} 缺少当前对象 ${targetObjectName} 上同脚本 GUID ${scriptGuid} 的 MonoBehaviour。`);
    return;
  }

  for (const propertyName of propertyNames) {
    const sourceLine = unityPropertyLine(sourceComponent.text, propertyName);
    const targetLine = unityPropertyLine(targetComponent.text, propertyName);
    if (sourceLine == null) {
      fail(`${label} 的 StackCraft 来源 ${sourceObjectName} 缺少 ${propertyName}，请先复核参考 Prefab。`);
      continue;
    }
    if (targetLine !== sourceLine) {
      fail(`${label} 的 ${targetObjectName}.${propertyName} 没有对齐 StackCraft ${sourceObjectName}：当前 ${targetLine ?? "<缺失>"}，应为 ${sourceLine}`);
    }
  }
}

function normalizedUnityComponentLines(componentText, ignoredPropertyNames = []) {
  const ignoredProperties = new Set(ignoredPropertyNames);
  return componentText
    .split(/\r?\n/)
    .map((line) => line.trimEnd())
    .filter((line) => {
      const trimmed = line.trim();
      if (trimmed.length === 0) return false;
      if (trimmed.startsWith("--- !u!")) return false;
      if (trimmed.startsWith("m_GameObject:")) return false;
      const propertyName = trimmed.match(/^([A-Za-z0-9_]+):/)?.[1] ?? null;
      return propertyName == null || !ignoredProperties.has(propertyName);
    });
}

function assertUnityComponentMatchesSourceExcept(
  sourceParsedYaml,
  sourceObjectName,
  targetParsedYaml,
  targetObjectName,
  classId,
  ignoredPropertyNames,
  label) {
  const sourceComponent = unityComponentByClass(sourceParsedYaml, sourceObjectName, classId);
  const targetComponent = unityComponentByClass(targetParsedYaml, targetObjectName, classId);
  if (sourceComponent == null) {
    fail(`${label} 缺少 StackCraft 来源对象 ${sourceObjectName} 的 class ${classId} 组件。`);
    return;
  }
  if (targetComponent == null) {
    fail(`${label} 缺少当前对象 ${targetObjectName} 的 class ${classId} 组件。`);
    return;
  }

  const sourceLines = normalizedUnityComponentLines(sourceComponent.text, ignoredPropertyNames);
  const targetLines = normalizedUnityComponentLines(targetComponent.text, ignoredPropertyNames);
  const maxLength = Math.max(sourceLines.length, targetLines.length);
  for (let index = 0; index < maxLength; index += 1) {
    const sourceLine = sourceLines[index] ?? "<缺失>";
    const targetLine = targetLines[index] ?? "<缺失>";
    if (sourceLine !== targetLine) {
      fail(`${label} 的 class ${classId} 组件没有整体对齐 StackCraft ${sourceObjectName}；第 ${index + 1} 行当前为 ${targetLine}，应为 ${sourceLine}。`);
      return;
    }
  }
}

function assertUnityImageObjectMatchesSource(
  sourceParsedYaml,
  sourceObjectName,
  targetParsedYaml,
  targetObjectName,
  label) {
  assertUnityComponentPropertiesMatch(
    sourceParsedYaml,
    sourceObjectName,
    targetParsedYaml,
    targetObjectName,
    224,
    [
      "m_LocalPosition",
      "m_LocalScale",
      "m_AnchoredPosition",
      "m_SizeDelta",
      "m_Pivot",
    ],
    `${label} RectTransform`);
  assertUnityComponentPropertiesMatch(
    sourceParsedYaml,
    sourceObjectName,
    targetParsedYaml,
    targetObjectName,
    114,
    [
      "m_Color",
      "m_RaycastTarget",
      "m_Maskable",
      "m_Type",
      "m_PreserveAspect",
      "m_FillCenter",
      "m_FillMethod",
      "m_FillAmount",
      "m_FillClockwise",
      "m_FillOrigin",
      "m_UseSpriteMesh",
      "m_PixelsPerUnitMultiplier",
    ],
    `${label} Image`);
}

function assertUnityTextObjectMatchesSource(
  sourceParsedYaml,
  sourceObjectName,
  targetParsedYaml,
  targetObjectName,
  label) {
  assertUnityGameObjectExists(targetParsedYaml, targetObjectName, label);
  assertUnityComponentPropertiesMatch(
    sourceParsedYaml,
    sourceObjectName,
    targetParsedYaml,
    targetObjectName,
    224,
    [
      "m_LocalRotation",
      "m_LocalPosition",
      "m_LocalScale",
      "m_LocalEulerAnglesHint",
      "m_AnchoredPosition",
      "m_SizeDelta",
      "m_Pivot",
    ],
    label);
  assertUnityComponentPropertiesMatch(
    sourceParsedYaml,
    sourceObjectName,
    targetParsedYaml,
    targetObjectName,
    114,
    [
      "m_Color",
      "m_fontSize",
      "m_fontSizeBase",
      "m_fontSizeMin",
      "m_fontSizeMax",
      "m_fontColor",
      "m_enableVertexGradient",
      "m_TextStyleHashCode",
      "m_overrideHtmlColors",
      "m_fontWeight",
      "m_enableAutoSizing",
      "m_fontStyle",
      "m_HorizontalAlignment",
      "m_VerticalAlignment",
      "m_textAlignment",
      "m_wordWrappingRatios",
      "m_overflowMode",
      "m_enableKerning",
      "m_enableExtraPadding",
      "m_margin",
    ],
    label);
  const sourceComponent = unityComponentByClass(sourceParsedYaml, sourceObjectName, 114);
  const targetComponent = unityComponentByClass(targetParsedYaml, targetObjectName, 114);
  if (sourceComponent != null && targetComponent != null) {
    assertUnityTextWrappingSemanticsMatch(sourceComponent, targetComponent, label);
  }
}

function assertUnityTextObjectsUseFontReference(
  parsedYaml,
  objectNames,
  expectedFontGuid,
  expectedMaterialFileId,
  label) {
  if (parsedYaml == null) {
    fail(`${label} 缺少目标 Prefab，无法检查 TMP 字体引用。`);
    return;
  }

  const expectedFontAssetLine = `m_fontAsset: {fileID: 11400000, guid: ${expectedFontGuid}, type: 2}`;
  const expectedSharedMaterialLine = `m_sharedMaterial: {fileID: ${expectedMaterialFileId}, guid: ${expectedFontGuid}, type: 2}`;
  const expectedRendererMaterialLine = `- {fileID: ${expectedMaterialFileId}, guid: ${expectedFontGuid}, type: 2}`;

  for (const objectName of objectNames) {
    const textComponent = unityComponentByClass(parsedYaml, objectName, 114);
    if (textComponent == null) {
      fail(`${label} 缺少 ${objectName} 的 TMP 文本组件，无法承载文字内容。`);
      continue;
    }

    const fontAssetLine = unityPropertyLine(textComponent.text, "m_fontAsset");
    if (fontAssetLine !== expectedFontAssetLine) {
      fail(`${label} 的 ${objectName}.m_fontAsset 未使用指定 TMP 字体：当前 ${fontAssetLine ?? "<缺失>"}，应为 ${expectedFontAssetLine}。`);
    }

    const sharedMaterialLine = unityPropertyLine(textComponent.text, "m_sharedMaterial");
    if (sharedMaterialLine !== expectedSharedMaterialLine) {
      fail(`${label} 的 ${objectName}.m_sharedMaterial 未使用指定 TMP 字体材质：当前 ${sharedMaterialLine ?? "<缺失>"}，应为 ${expectedSharedMaterialLine}。`);
    }

    const rendererComponent = unityComponentByClass(parsedYaml, objectName, 23);
    if (rendererComponent != null) {
      const materialLines = yamlReferenceListLines(rendererComponent.text, "m_Materials");
      if (materialLines == null) {
        fail(`${label} 的 ${objectName} MeshRenderer 缺少 m_Materials 列表，无法证明使用指定 TMP 字体材质。`);
      } else if (!materialLines.includes(expectedRendererMaterialLine)) {
        fail(`${label} 的 ${objectName} MeshRenderer.m_Materials 未使用指定 TMP 字体材质：当前 ${materialLines.join(", ")}，应包含 ${expectedRendererMaterialLine}。`);
      }
    }
  }
}

function assertUnitySpriteRendererObjectMatchesSource(
  sourceParsedYaml,
  sourceObjectName,
  targetParsedYaml,
  targetObjectName,
  label) {
  assertUnityComponentPropertiesMatch(
    sourceParsedYaml,
    sourceObjectName,
    targetParsedYaml,
    targetObjectName,
    4,
    ["m_LocalRotation", "m_LocalPosition", "m_LocalScale", "m_LocalEulerAnglesHint"],
    `${label} Transform`);
  assertUnityComponentPropertiesMatch(
    sourceParsedYaml,
    sourceObjectName,
    targetParsedYaml,
    targetObjectName,
    212,
    [
      "m_CastShadows",
      "m_ReceiveShadows",
      "m_RenderingLayerMask",
      "m_SortingLayerID",
      "m_SortingLayer",
      "m_SortingOrder",
      "m_MaskInteraction",
      "m_Color",
      "m_FlipX",
      "m_FlipY",
      "m_DrawMode",
      "m_Size",
      "m_AdaptiveModeThreshold",
      "m_SpriteTileMode",
      "m_WasSpriteAssigned",
      "m_SpriteSortPoint",
    ],
    `${label} SpriteRenderer`);
}

function collectorSettingEntryBlock(settingText, assetPath) {
  if (settingText == null) return null;

  const collectPathLines = new Set([
    `- CollectPath: ${assetPath}`,
    `- CollectPath: "${unityEscapedString(assetPath)}"`,
  ]);
  const lines = settingText.split(/\r?\n/);
  const start = lines.findIndex((line) => collectPathLines.has(line.trim()));
  if (start < 0) return null;

  let end = lines.length;
  for (let index = start + 1; index < lines.length; index += 1) {
    if (lines[index].trim().startsWith("- CollectPath:")) {
      end = index;
      break;
    }
  }

  return lines.slice(start, end).join("\n");
}

function collectorSettingGroupBlockByCollectPath(settingText, assetPath) {
  if (settingText == null) return null;

  const collectPathLines = new Set([
    `- CollectPath: ${assetPath}`,
    `- CollectPath: "${unityEscapedString(assetPath)}"`,
  ]);
  const lines = settingText.split(/\r?\n/);
  const collectorStart = lines.findIndex((line) => collectPathLines.has(line.trim()));
  if (collectorStart < 0) return null;

  let groupStart = -1;
  for (let index = collectorStart; index >= 0; index -= 1) {
    if (/^\s*- GroupName:/.test(lines[index])) {
      groupStart = index;
      break;
    }
  }
  if (groupStart < 0) return null;

  let groupEnd = lines.length;
  for (let index = groupStart + 1; index < lines.length; index += 1) {
    if (/^\s*- GroupName:/.test(lines[index])) {
      groupEnd = index;
      break;
    }
  }

  return lines.slice(groupStart, groupEnd).join("\n");
}

function assertCollectorSettingEntry(settingText, assetPath, expectedFields, label) {
  const block = collectorSettingEntryBlock(settingText, assetPath);
  if (block == null) {
    fail(`YooAsset 收集配置缺少 ${label} 路径：${assetPath}。`);
    return;
  }

  for (const [fieldName, expectedValue] of Object.entries(expectedFields)) {
    const actualValue = yamlBlockScalarValue(block, fieldName);
    if (actualValue !== String(expectedValue)) {
      fail(`YooAsset 收集配置中 ${label} 的 ${fieldName} 未对齐：${assetPath} 当前 ${actualValue ?? "<缺失>"}，应为 ${expectedValue}。`);
    }
  }
}

function assertCollectorSettingGroupForEntry(settingText, assetPath, expectedFields, label) {
  const block = collectorSettingGroupBlockByCollectPath(settingText, assetPath);
  if (block == null) {
    fail(`YooAsset 收集配置缺少 ${label} 所属分组，目标路径：${assetPath}。`);
    return;
  }

  for (const [fieldName, expectedValue] of Object.entries(expectedFields)) {
    const actualValue = yamlBlockScalarValue(block, fieldName);
    if (actualValue !== String(expectedValue)) {
      fail(`YooAsset 收集配置中 ${label} 所属分组的 ${fieldName} 未对齐：${assetPath} 当前 ${actualValue ?? "<缺失>"}，应为 ${expectedValue}。`);
    }
  }
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

function assertFileContains(relativePath, pattern, message) {
  const text = readIfExists(relativePath);
  if (text == null) {
    fail(`缺少 ${relativePath}，${message}`);
    return;
  }
  pattern.lastIndex = 0;
  if (!pattern.test(text)) {
    fail(`${relativePath} ${message}`);
  }
}

function assertFileDoesNotContain(relativePath, pattern, message) {
  const text = readIfExists(relativePath);
  if (text == null) {
    fail(`缺少 ${relativePath}，无法执行反向检查：${message}`);
    return;
  }
  pattern.lastIndex = 0;
  if (pattern.test(text)) {
    fail(`${relativePath} ${message}`);
  }
}

function decodeUnityScalar(rawValue) {
  const trimmed = rawValue.trim();
  if (trimmed.length === 0) return "";
  if (trimmed.startsWith("\"") && trimmed.endsWith("\"")) {
    try {
      return JSON.parse(trimmed);
    } catch {
      return trimmed.slice(1, -1);
    }
  }
  if (trimmed.startsWith("'") && trimmed.endsWith("'")) {
    return trimmed.slice(1, -1);
  }
  return trimmed;
}

function decodeUnityIntArrayHex(rawValue, assetPath, fieldName) {
  const hex = rawValue.trim();
  if (hex.length === 0) return [];
  if (hex.length % 8 !== 0) {
    fail(`${assetPath} 的 ${fieldName} 不是合法 int[] 十六进制序列：${hex}`);
    return [];
  }

  const values = [];
  for (let index = 0; index < hex.length; index += 8) {
    values.push(Buffer.from(hex.slice(index, index + 8), "hex").readInt32LE(0));
  }
  return values;
}

function assertGameplayTestCardCategoryTags() {
  const categoryBySurface = new Map([
    ["卡牌表面_资源", [8001001, "Card.Category.Resource"]],
    ["卡牌表面_角色", [8001002, "Card.Category.Character"]],
    ["卡牌表面_消耗品", [8001003, "Card.Category.Consumable"]],
    ["卡牌表面_材料", [8001004, "Card.Category.Material"]],
    ["卡牌表面_装备", [8001005, "Card.Category.Equipment"]],
    ["卡牌表面_建筑", [8001006, "Card.Category.Structure"]],
    ["交易区", [8001006, "Card.Category.Structure"]],
    ["卡牌表面_货币", [8001007, "Card.Category.Currency"]],
    ["卡牌表面_配方", [8001008, "Card.Category.Recipe"]],
    ["卡牌表面_主动敌人", [8001009, "Card.Category.Mob"]],
    ["卡牌表面_生物", [8001009, "Card.Category.Mob"]],
    ["卡牌表面_地区", [8001010, "Card.Category.Area"]],
    ["卡牌表面_贵重物", [8001011, "Card.Category.Valuable"]],
  ]);
  const ordinaryCategoryCodes = new Set([...categoryBySurface.values()].map(([code]) => code));

  for (const absolutePath of walk("Assets/Gameplay/Tests").filter((file) => file.endsWith(".asset"))) {
    const relativePath = rel(absolutePath);
    const text = fs.readFileSync(absolutePath, "utf8");
    const surfaceMatch = text.match(/^  m_cardSurface:\r?\n    Address:\s*(.*)$/m);
    if (surfaceMatch == null) continue;

    const tagLine = text.match(/^  m_tagCodes:\s*(.*)$/m);
    if (tagLine == null) {
      fail(`${relativePath} 是卡牌作者源但缺少 m_tagCodes。`);
      continue;
    }

    const surface = decodeUnityScalar(surfaceMatch[1]);
    const tags = decodeUnityIntArrayHex(tagLine[1], relativePath, "m_tagCodes");
    if (surface === "卡牌表面_卡包") {
      if (tags.some((tag) => ordinaryCategoryCodes.has(tag))) {
        fail(`${relativePath} 是 StackCraft Pack/None 类别，不应写入普通 Card.Category.* 合堆类别。`);
      }
      continue;
    }

    const expected = categoryBySurface.get(surface);
    if (expected == null) {
      fail(`${relativePath} 使用了未登记的卡牌表面“${surface}”，无法证明 StackCraft 类别映射。`);
      continue;
    }
    const [tagCode, tagName] = expected;
    if (!tags.includes(tagCode)) {
      fail(`${relativePath} 的卡牌表面“${surface}”缺少 GAS 类别标签 ${tagName}(${tagCode})，普通合堆规则无法按 StackCraft 类别矩阵生效。`);
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
    relativePath.startsWith("Assets/Screenshots/") ||
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

const stackCraftReferenceCaptureFiles = new Set([
  "Assets/Editor/Gameplay/Automation/StackCraftReferenceCaptureMenu.cs",
]);

function isStackCraftReferenceCaptureFile(file) {
  return stackCraftReferenceCaptureFiles.has(rel(file));
}

const nonPlayModeImplementationFiles = csharpFiles([
  "Assets/Scripts/GameCore",
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

const actionSlotReferenceDrawerSource = readIfExists("Assets/Editor/Gameplay/Actions/ActionSlotReferenceDrawer.cs");
if (actionSlotReferenceDrawerSource == null) {
  fail("缺少行动槽位引用下拉抽屉，无法证明作者不需要手填内部槽位 key。");
} else {
  assertCsharpDeclarationAndBlockContainsOrdered(
    actionSlotReferenceDrawerSource,
    "[CustomPropertyDrawer(typeof(ActionSlotReferenceAttribute))]",
    [
      "[CustomPropertyDrawer(typeof(ActionSlotReferenceAttribute))]",
      "public sealed class ActionSlotReferenceDrawer : PropertyDrawer",
    ],
    "行动槽位引用下拉抽屉必须绑定 ActionSlotReferenceAttribute");
  assertCsharpBlockContainsOrdered(
    actionSlotReferenceDrawerSource,
    "public override void OnGUI",
    [
      "SlotOptions options = BuildOptions(property, action);",
      "EditorGUI.Popup(",
      "AssignReference(property, action, options.Keys[selectedIndex]);",
      "EditorGUI.HelpBox(helpRect, options.ErrorMessage, MessageType.Error);",
    ],
    "行动槽位引用下拉抽屉必须从 ActionDefinition 槽位生成 Inspector 选择器和中文错误");
  assertCsharpBlockContainsOrdered(
    actionSlotReferenceDrawerSource,
    "internal static void AssignReference",
    [
      "slotKeyProperty.propertyType != SerializedPropertyType.String",
      "IReadOnlyList<ActionSlotDefinition> slots = action.ParticipationSlots;",
      "string.IsNullOrWhiteSpace(selectedKey)",
      "slots.Count != 1",
      "slotKeyProperty.stringValue = string.Empty;",
      "StringComparer.Ordinal.Equals(slot.Key, selectedKey)",
      "slotKeyProperty.stringValue = selectedSlot.Key;",
    ],
    "行动槽位引用写入必须只保存稳定槽位键，并让单槽位空值代表自动推导");
  assertCsharpBlockContainsOrdered(
    actionSlotReferenceDrawerSource,
    "private static SlotOptions BuildOptions",
    [
      "property.propertyType != SerializedPropertyType.String",
      "action.ParticipationSlots",
      "HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);",
      "自动推导（唯一槽位",
      "slots.Count > 1 && string.IsNullOrEmpty(currentKey)",
      "多槽位行动必须明确选择结果所引用的参与槽位。",
      "当前引用的参与槽位不存在",
    ],
    "行动槽位引用选项必须自动推导唯一槽位，并对多槽位缺失 / 失效 key 报中文错误");
}

const xTagGeneratedSource = readIfExists("Assets/Scripts/Gen/XTag.gen.cs");
if (xTagGeneratedSource == null) {
  fail("缺少 EX-GAS 生成标签常量，无法证明 Card.Category.* 来自作者表。");
} else {
  const expectedCardCategoryChildren = [
    "Card_Category_Resource",
    "Card_Category_Character",
    "Card_Category_Consumable",
    "Card_Category_Material",
    "Card_Category_Equipment",
    "Card_Category_Structure",
    "Card_Category_Currency",
    "Card_Category_Recipe",
    "Card_Category_Mob",
    "Card_Category_Area",
    "Card_Category_Valuable",
  ];
  const expectedCardCategoryTags = [
    ["Card", "800", [], ["Card_Category", ...expectedCardCategoryChildren], "Card"],
    ["Card_Category", "8001", ["Card"], expectedCardCategoryChildren, "Card.Category"],
    ["Card_Category_Resource", "8001001", ["Card", "Card_Category"], [], "Card.Category.Resource"],
    ["Card_Category_Character", "8001002", ["Card", "Card_Category"], [], "Card.Category.Character"],
    ["Card_Category_Consumable", "8001003", ["Card", "Card_Category"], [], "Card.Category.Consumable"],
    ["Card_Category_Material", "8001004", ["Card", "Card_Category"], [], "Card.Category.Material"],
    ["Card_Category_Equipment", "8001005", ["Card", "Card_Category"], [], "Card.Category.Equipment"],
    ["Card_Category_Structure", "8001006", ["Card", "Card_Category"], [], "Card.Category.Structure"],
    ["Card_Category_Currency", "8001007", ["Card", "Card_Category"], [], "Card.Category.Currency"],
    ["Card_Category_Recipe", "8001008", ["Card", "Card_Category"], [], "Card.Category.Recipe"],
    ["Card_Category_Mob", "8001009", ["Card", "Card_Category"], [], "Card.Category.Mob"],
    ["Card_Category_Area", "8001010", ["Card", "Card_Category"], [], "Card.Category.Area"],
    ["Card_Category_Valuable", "8001011", ["Card", "Card_Category"], [], "Card.Category.Valuable"],
  ];
  for (const [constantName, value, expectedParents, expectedChildren, displayName] of expectedCardCategoryTags) {
    assertCsharpConstIntEquals(
      xTagGeneratedSource,
      constantName,
      value,
      `EX-GAS 生成标签常量 ${constantName}`);
    assertGeneratedGameplayTagEntry(
      xTagGeneratedSource,
      constantName,
      expectedParents,
      expectedChildren,
      displayName,
      `EX-GAS 生成标签 ${constantName} 必须同时登记层级与点分隔显示名`);
  }
}

const tabletopPlacementContractsSourceForStacking = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Placement/TabletopCardPlacementContracts.cs");
if (tabletopPlacementContractsSourceForStacking == null) {
  fail("缺少牌桌放置契约源码，无法证明 StackCraft 合堆矩阵由 EX-GAS 标签承接。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopPlacementContractsSourceForStacking,
    "internal static TabletopStackingRulesDefinition CreateStackCraftDefault",
    [
      "Rule(XTag.Card_Category_Resource, XTag.Card_Category_Resource, requiresSameContent: true)",
      "Rule(XTag.Card_Category_Character, XTag.Card_Category_Resource)",
      "Rule(XTag.Card_Category_Equipment, XTag.Card_Category_Character)",
      "Rule(XTag.Card_Category_Mob, XTag.Card_Category_Mob)",
      "Rule(XTag.Card_Category_Area, XTag.Card_Category_Area)",
      "Rule(XTag.Card_Category_Valuable, XTag.Card_Category_Valuable)",
    ],
    "StackCraft 合堆默认矩阵必须由 EX-GAS Card.Category.* 标签声明，Resource 同类合堆必须要求同内容");
  assertCsharpBlockContainsOrdered(
    tabletopPlacementContractsSourceForStacking,
    "private static bool MatchesAtLeastOneActualTag",
    [
      "for (int actualIndex = 0; actualIndex < actualTags.Count; actualIndex++)",
      "if (TagHelper.HasTag(actualTags[actualIndex], requiredTag))",
      "return true;",
      "return false;",
    ],
    "合堆类别匹配必须走 EX-GAS 标签层级查询，不能用整数相等冒充标签语义");
  assertFileDoesNotContain(
    "Assets/Scripts/Gameplay/Runtime/Tabletop/Placement/TabletopCardPlacementContracts.cs",
    /\b(?:CardCategory\.|CardCategory\s+[A-Za-z_]|typeof\(CardCategory\)|IReadOnlyList<CardCategory>|List<CardCategory>|CardCategory\[\])/,
    "不能引入 StackCraft 的 CardCategory 枚举作为第二套卡牌类别真相。",
  );
}
const tabletopInteractionSourceForRelease = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Input/TabletopInteraction.cs");
if (tabletopInteractionSourceForRelease == null) {
  fail("缺少牌桌交互源码，无法证明释放目标解释链路已由正式入口承接。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopInteractionSourceForRelease,
    "public ActionCandidate[] HandleRelease",
    [
      "if (!intent.IsDrag)",
      "return PresentActionCandidates(scenarioRun.FindActionCandidates(intent));",
      "scenarioRun.Tabletop.TryDropBattleParticipant(",
      "if (!intent.TargetCardId.IsValid)",
      "scenarioRun.Tabletop.TryPlaceStack(",
      "ActionCandidate[] candidates = scenarioRun.FindActionCandidates(intent);",
      "if (candidates.Length > 0)",
      "return PresentActionCandidates(candidates);",
      "scenarioRun.Tabletop.TryDropStackOnto(intent.CardId, intent.TargetCardId, out _)",
      "scenarioRun.Tabletop.TryPlaceStack(",
    ],
    "牌桌释放入口必须按点击候选、战斗投放、空白放置、目标行动、普通合堆、兜底放置的顺序解释一次释放");
  assertCsharpBlockContainsOrdered(
    tabletopInteractionSourceForRelease,
    "public bool CanShowDropTargetHighlight",
    [
      "ActionCandidate[] candidates = scenarioRun.FindActionCandidates(intent);",
      "if (candidates.Length > 0)",
      "return true;",
      "return intent.TargetCardId.IsValid &&",
      "scenarioRun.Tabletop.CanStackOnto(intent.CardId, intent.TargetCardId);",
    ],
    "拖拽高亮必须同时覆盖行动候选和普通合堆候选，且只读查询不得打开 UI");
}
assertGameplayTestCardCategoryTags();

scanFiles(
  projectReferenceFiles.filter((file) => !isStackCraftReferenceCaptureFile(file)),
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
  projectReferenceFiles.filter((file) => file.endsWith(".cs") && !isStackCraftReferenceCaptureFile(file)),
  [
    { pattern: /\bCryingSnow\b/, message: "正式工程源码不得引用 StackCraft 命名空间。" },
  ],
  "旧模板命名空间扫描",
);

scanFiles(
  gameplayAndTestFiles.filter((file) => !isStackCraftReferenceCaptureFile(file)),
  [
    { pattern: /Assets\/StackCraft|Assets\\StackCraft/, message: "正式 Gameplay / 测试链不得直接依赖 StackCraft 旧资源路径。" },
    { pattern: /\bCryingSnow\b/, message: "正式 Gameplay / 测试链不得引用 StackCraft 命名空间。" },
    { pattern: /Resources\.Load(?:All)?\s*\(/, message: "正式 Gameplay / 测试链不得恢复 Resources 资源扫描入口。" },
    { pattern: /\b(?:AudioId|AudioManager|CombatManager|ProjectileManager|HitUI|QuestManager|CraftingManager|EncounterManager|EncounterDefinition|EncounterType|GameData|SeenItems|MenuView)\b|\b(?:class|new|typeof)\s*\(?\s*(?:RecipesView|QuestsView)\b/, message: "旧模板 Manager / DTO / UI 类型回流。" },
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
    { pattern: /\bPresentationTagCode\b/, message: "自动战斗的 Combat.* 标签是战斗类型事实，不得命名为表现标签。" },
    { pattern: /\bpresentationTagCode\b/, message: "自动战斗的 Combat.* 标签是战斗类型事实，不得用 presentationTagCode 命名。" },
    { pattern: /\bResolveAttackPresentationTagCode\b/, message: "自动战斗类型解析入口必须表达 CombatType，不得回退到 PresentationTag 命名。" },
    { pattern: /\bTryGetCurrentAttackPresentationTag\b/, message: "自动战斗当前攻击类型读取入口必须表达 CombatType，不得回退到 PresentationTag 命名。" },
    { pattern: /攻击表现标签/, message: "用户可读错误文案必须说明 Combat.* 是战斗类型标签，不得称为攻击表现标签。" },
  ],
  "战斗类型标签命名扫描",
);

const stackCraftAudioPairs = [
  ["Assets/StackCraft/Sounds/SFX/AttackMagic.wav", "Assets/Audio/SFX/魔法起手.wav", "魔法起手音效"],
  ["Assets/StackCraft/Sounds/SFX/AttackMelee.wav", "Assets/Audio/SFX/近战起手.wav", "近战起手音效"],
  ["Assets/StackCraft/Sounds/SFX/AttackRanged.wav", "Assets/Audio/SFX/远程起手.wav", "远程起手音效"],
  ["Assets/StackCraft/Sounds/SFX/CardDrop.wav", "Assets/Audio/SFX/放下卡牌.wav", "放下卡牌音效"],
  ["Assets/StackCraft/Sounds/SFX/CardPick.wav", "Assets/Audio/SFX/拿起卡牌.wav", "拿起卡牌音效"],
  ["Assets/StackCraft/Sounds/SFX/CardSwipe.wav", "Assets/Audio/SFX/卡牌滑动.wav", "卡牌滑动音效"],
  ["Assets/StackCraft/Sounds/SFX/CashRegister.wav", "Assets/Audio/SFX/购买成交.wav", "购买成交音效"],
  ["Assets/StackCraft/Sounds/SFX/Click.wav", "Assets/Audio/SFX/界面点击.wav", "界面点击音效"],
  ["Assets/StackCraft/Sounds/SFX/Coin.wav", "Assets/Audio/SFX/单枚货币.wav", "单枚货币音效"],
  ["Assets/StackCraft/Sounds/SFX/Coins.wav", "Assets/Audio/SFX/多枚货币.wav", "多枚货币音效"],
  ["Assets/StackCraft/Sounds/SFX/Critical.wav", "Assets/Audio/SFX/暴击.wav", "暴击音效"],
  ["Assets/StackCraft/Sounds/SFX/Eat.wav", "Assets/Audio/SFX/进食.wav", "进食音效"],
  ["Assets/StackCraft/Sounds/SFX/HitMagic.wav", "Assets/Audio/SFX/魔法命中.wav", "魔法命中音效"],
  ["Assets/StackCraft/Sounds/SFX/HitMelee.wav", "Assets/Audio/SFX/近战命中.wav", "近战命中音效"],
  ["Assets/StackCraft/Sounds/SFX/HitRanged.wav", "Assets/Audio/SFX/远程命中.wav", "远程命中音效"],
  ["Assets/StackCraft/Sounds/SFX/Miss.wav", "Assets/Audio/SFX/未命中.wav", "未命中音效"],
  ["Assets/StackCraft/Sounds/SFX/Pop.wav", "Assets/Audio/SFX/生成完成.wav", "生成完成音效"],
  ["Assets/StackCraft/Sounds/SFX/Puff.wav", "Assets/Audio/SFX/卡牌烟雾反馈.wav", "卡牌烟雾反馈音效"],
  ["Assets/StackCraft/Sounds/KingsFeast.wav", "Assets/Audio/Music/国王盛宴.wav", "国王盛宴音乐"],
];

for (const [stackCraftAudio, projectAudio, description] of stackCraftAudioPairs) {
  assertSameFileHash(stackCraftAudio, projectAudio, description);
  if (!exists(`${projectAudio}.meta`)) {
    fail(`${description} 缺少 Unity meta 文件：${projectAudio}.meta`);
  }
  assertAudioImportSettingsMatch(stackCraftAudio, projectAudio, description);
}

scanFiles(
  gameplayAndTestFiles,
  [
    { pattern: /\bSetSurfaceSprite\b/, message: "StackCraft 卡面表面不得回退为 SpriteRenderer 底板；必须使用 MeshRenderer + Card shadergraph 材质链。" },
    { pattern: /\bm_artworkRenderer\b/, message: "StackCraft 卡面插画不得保留独立 SpriteRenderer 字段；插画必须写入卡面材质 _OverlayTex。" },
    { pattern: /\bm_artworkPadding\b/, message: "StackCraft 覆盖图比例由材质 _OverlayScale/_OverlayOffset 承载，不得保留运行时插画 padding 字段。" },
    { pattern: /new Vector3\s*\(\s*stackPosition\.x\s*,\s*stackPosition\.y\s*,\s*0f\s*\)/, message: "牌桌静态卡面不得回退到 XY 平面适配；二维牌桌坐标必须经 TabletopCoordinateSpace 映射到 StackCraft XZ 桌面。" },
    { pattern: /new Vector3\s*\(\s*battleAnchor\.x\s*[+-]\s*[\d.]+f\s*,\s*battleAnchor\.y\s*,\s*0f\s*\)/, message: "战斗阵型断言不得把牌桌二维坐标拼成 XY 世界坐标；必须经 TabletopCoordinateSpace 映射到 StackCraft XZ 桌面。" },
    { pattern: /new Vector3\s*\(\s*releaseTablePosition\.x\s*,\s*releaseTablePosition\.y\s*,\s*0f\s*\)/, message: "牌桌释放点不得把二维牌桌坐标拼成 XY 世界坐标；必须经 TabletopCoordinateSpace 映射到 StackCraft XZ 桌面。" },
    { pattern: /Vector2\s+\w+\s*=\s*\w+View\.transform\.localPosition\s*;/, message: "测试不能把 Unity 本地坐标隐式截成 Vector2；读取牌桌位置必须使用 TabletopCoordinateSpace.ToTablePosition。" },
    { pattern: /new Plane\s*\(\s*tablePlane\.forward\s*,/, message: "牌桌射线投影不得使用 XY 平面 forward 法线；必须使用 StackCraft XZ 桌面的 tablePlane.up。" },
    { pattern: /\bm_stackDepthStep\b/, message: "牌堆视觉步进不得再用 Z 深度字段；StackCraft stackStep.y = 0.002 必须写入 Unity Y 抬升字段 m_stackHeightStep。" },
  ],
  "StackCraft 卡面表面回流扫描",
);

scanFiles(
  nonPlayModeImplementationFiles,
  [
    { pattern: /\bUnityEngine\.Input(?!System)\b/, message: "正式实现 / 测试支撑不得读取旧 UnityEngine.Input；必须走新输入系统和 GameCore.InputSystem。" },
    { pattern: /\bStandaloneInputModule\b/, message: "正式实现 / 测试支撑不得恢复 UGUI StandaloneInputModule；EventSystem 必须使用 InputSystemUIInputModule。" },
    { pattern: /\bSceneManager\.LoadScene(?:Async)?\s*\(\s*"(?:Main|Island|Title)"\s*[,)]/, message: "正式实现 / 测试支撑不得按 StackCraft 固定场景名直接切场景；必须走 SceneSystem / ResourceSystem 场景加载链。" },
    { pattern: /\bLoadSceneAsync\s*\(\s*"(?:Main|Island|Title)"\s*[,)]/, message: "正式实现 / 测试支撑不得按 StackCraft 固定场景名直接切场景；必须走 SceneSystem / ResourceSystem 场景加载链。" },
    { pattern: /\bFindObjectOfType(?:<|\s*\()/, message: "正式实现 / 测试支撑不得用全局对象查找作为依赖入口。" },
    { pattern: /\bFindObjectsOfType(?:<|\s*\()/, message: "正式实现 / 测试支撑不得用全局对象查找作为依赖入口。" },
    { pattern: /\bGameObject\.Find\s*\(/, message: "正式实现 / 测试支撑不得按名字全局查找依赖。" },
    { pattern: /\b(?:transform|[A-Za-z_][A-Za-z0-9_]*Transform[A-Za-z0-9_]*)\.Find\s*\(\s*"/, message: "正式实现 / 测试支撑不得用 Transform.Find 字符串查找依赖。" },
    { pattern: /\bFindWithTag\s*\(/, message: "正式实现 / 测试支撑不得用标签全局查找依赖。" },
    { pattern: /\bCamera\.main\b/, message: "正式实现 / 测试支撑不得用 Camera.main 获取唯一相机依赖。" },
    { pattern: /\bSendMessage\s*\(/, message: "正式实现 / 测试支撑不得使用 SendMessage 隐式调用。" },
  ],
  "正式依赖入口扫描",
);

const projectileMetaPath = "Assets/Art/Prefabs/牌桌/投射物.prefab.meta";
const hitResultMetaPath = "Assets/Art/Prefabs/牌桌/命中结果.prefab.meta";
const tabletopSettingsPath = "Assets/Gameplay/Tests/牌桌/牌桌测试视图设置.asset";
const stackCraftDefaultCardSettingsText = readIfExists("Assets/StackCraft/Settings/Default_Card_Settings.asset");
const testBeginningChickenText = readIfExists("Assets/Gameplay/Tests/地基开端鸡.asset");
const testBeginningSlimeText = readIfExists("Assets/Gameplay/Tests/地基开端史莱姆.asset");
const testDayCycleScenarioText = readIfExists("Assets/Gameplay/Tests/地基日终测试剧本.asset");
const testStackCraftParityScenarioText = readIfExists("Assets/Gameplay/Tests/地基StackCraft同态测试剧本.asset");
const testStackCraftParityQuestText = readIfExists("Assets/Gameplay/Tests/地基StackCraft同态打开初始卡包任务.asset");
const stackCraftBaseCardLimit = stackCraftDefaultCardSettingsText == null
  ? null
  : yamlScalarPropertyValue(
    stackCraftDefaultCardSettingsText,
    "baseCardLimit",
    "StackCraft Default_Card_Settings.baseCardLimit");
const stackCraftHungerPerCharacter = stackCraftDefaultCardSettingsText == null
  ? null
  : yamlScalarPropertyValue(
    stackCraftDefaultCardSettingsText,
    "hungerPerCharacter",
    "StackCraft Default_Card_Settings.hungerPerCharacter");
const stackCraftOverlapResolveMaxIterations = stackCraftDefaultCardSettingsText == null
  ? null
  : yamlScalarPropertyValue(
    stackCraftDefaultCardSettingsText,
    "maxIterations",
    "StackCraft Default_Card_Settings.maxIterations");
const stackCraftSpawnAttachRadius = stackCraftDefaultCardSettingsText == null
  ? null
  : yamlScalarPropertyValue(
    stackCraftDefaultCardSettingsText,
    "spawnAttachRadius",
    "StackCraft Default_Card_Settings.spawnAttachRadius");
const stackCraftMoveEase = stackCraftDefaultCardSettingsText == null
  ? null
  : yamlScalarPropertyValue(
    stackCraftDefaultCardSettingsText,
    "moveEase",
    "StackCraft Default_Card_Settings.moveEase");
const stackCraftAutomaticMovementIntervalSeconds = stackCraftDefaultCardSettingsText == null
  ? null
  : yamlScalarPropertyValue(
    stackCraftDefaultCardSettingsText,
    "moveInterval",
    "StackCraft Default_Card_Settings.moveInterval");
const stackCraftAutomaticMovementRadius = stackCraftDefaultCardSettingsText == null
  ? null
  : yamlScalarPropertyValue(
    stackCraftDefaultCardSettingsText,
    "moveRadius",
    "StackCraft Default_Card_Settings.moveRadius");
const stackCraftAutomaticMovementMaxAttempts = stackCraftDefaultCardSettingsText == null
  ? null
  : yamlScalarPropertyValue(
    stackCraftDefaultCardSettingsText,
    "maxAttemptsPerMove",
    "StackCraft Default_Card_Settings.maxAttemptsPerMove");
const tabletopViewSettingsSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/TabletopViewSettings.cs");
if (exists(projectileMetaPath) && exists(tabletopSettingsPath)) {
  const meta = read(projectileMetaPath);
  const settings = read(tabletopSettingsPath);
  const guid = meta.match(/guid:\s*([0-9a-f]{32})/)?.[1];
  if (!guid) {
    fail(`${projectileMetaPath} 缺少合法 GUID。`);
  }
  assertSoftAssetReference(
    settings,
    "m_projectileViewPrefab",
    "投射物",
    guid,
    `${tabletopSettingsPath} 投射物 Prefab 引用`);
  const projectileSortingOrder = tabletopViewSettingsSource == null
    ? null
    : csharpRawInitializer(
      tabletopViewSettingsSource,
      "m_projectileSortingOrder",
      "TabletopViewSettings 投射物排序默认值");
  assertYamlScalarEquals(
    settings,
    "m_projectileSortingOrder",
    projectileSortingOrder,
    `${tabletopSettingsPath} TabletopViewSettings 投射物排序默认值`);
  if (exists(hitResultMetaPath)) {
    const hitResultMeta = read(hitResultMetaPath);
    const hitResultGuid = hitResultMeta.match(/guid:\s*([0-9a-f]{32})/)?.[1];
    if (!hitResultGuid) {
      fail(`${hitResultMetaPath} 缺少合法 GUID。`);
    }
    assertSoftAssetReference(
      settings,
      "m_hitResultViewPrefab",
      "命中结果",
      hitResultGuid,
      `${tabletopSettingsPath} 命中结果 Prefab 引用`);
  } else {
    fail(`缺少命中结果 Prefab meta：${hitResultMetaPath}。`);
  }
  const hitResultSortingOrder = tabletopViewSettingsSource == null
    ? null
    : csharpRawInitializer(
      tabletopViewSettingsSource,
      "m_hitResultSortingOrder",
      "TabletopViewSettings 命中结果排序默认值");
  assertYamlScalarEquals(
    settings,
    "m_hitResultSortingOrder",
    hitResultSortingOrder,
    `${tabletopSettingsPath} TabletopViewSettings 命中结果排序默认值`);
  if (settings.includes("m_stackDepthStep:")) {
    fail(`${tabletopSettingsPath} 仍保留旧 m_stackDepthStep；StackCraft 堆叠高度必须由 m_stackHeightStep 承载。`);
  }
  assertTabletopViewSettingsMatchStackCraft(stackCraftDefaultCardSettingsText, settings, tabletopSettingsPath);
  for (const obsoleteAddress of [
    "Address: \"\\u724C\\u684C\\u6D4B\\u8BD5\\u5361\\u724C\\u89C6\\u56FE\"",
    "Address: \"\\u724C\\u684C\\u6D4B\\u8BD5\\u884C\\u52A8\\u8FDB\\u5EA6\"",
    "Address: \"\\u724C\\u684C\\u6D4B\\u8BD5\\u6218\\u6597\\u533A\\u57DF\"",
    "Address: \"\\u724C\\u684C\\u6D4B\\u8BD5\\u6295\\u5C04\\u7269\"",
    "Address: \"\\u724C\\u684C\\u6D4B\\u8BD5\\u547D\\u4E2D\\u7ED3\\u679C\"",
  ]) {
    if (settings.includes(obsoleteAddress)) {
      fail(`${tabletopSettingsPath} 仍保留测试命名资源地址：${obsoleteAddress}。`);
    }
  }
  for (const [fieldName, expectedAddress] of [
    ["m_cardViewPrefab", "卡牌视图"],
    ["m_actionProgressViewPrefab", "行动进度"],
    ["m_battleAreaViewPrefab", "战斗区域"],
    ["m_projectileViewPrefab", "投射物"],
    ["m_hitResultViewPrefab", "命中结果"],
  ]) {
    assertSoftAssetReference(
      settings,
      fieldName,
      expectedAddress,
      null,
      `${tabletopSettingsPath} 正式中文资源地址`);
  }
} else {
  warn("未找到投射物 Prefab 或牌桌测试视图设置，跳过投射物资源引用检查。");
}

const collectorSettingText = readIfExists("Assets/BundleCollectorSetting.asset");
if (collectorSettingText == null) {
  fail("缺少 Assets/BundleCollectorSetting.asset，无法证明 StackCraft 表面素材进入 ResourceSystem / YooAsset 正式地址。");
} else {
  assertCollectorSettingGroupForEntry(
    collectorSettingText,
    "Assets",
    {
      GroupName: '"Gameplay\\u5185\\u5BB9\\u5B9A\\u4E49"',
      AssetTags: "gameplay-content",
    },
    "Gameplay 内容作者源收集器");
  assertCollectorSettingEntry(
    collectorSettingText,
    "Assets",
    {
      AddressRuleName: "AddressDisable",
      PackRuleName: "PackCollector",
      FilterRuleName: "ContentAssetFilterRule",
    },
    "Gameplay 内容作者源收集器");
  assertCollectorSettingEntry(
    collectorSettingText,
    "Assets/Art/Sprites/StackCraft",
    {
      AddressRuleName: "AddressByFolderAndFileName",
      PackRuleName: "PackDirectory",
      FilterRuleName: "CollectAll",
      AssetTags: "test",
    },
    "StackCraft 图片素材文件夹收集器");
  for (const obsoletePath of [
    "Assets/Gameplay/Tests/\\u724C\\u684C/\\u724C\\u684C\\u6D4B\\u8BD5\\u5361\\u724C\\u89C6\\u56FE.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/\\u724C\\u684C\\u6D4B\\u8BD5\\u884C\\u52A8\\u8FDB\\u5EA6.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/\\u724C\\u684C\\u6D4B\\u8BD5\\u6218\\u6597\\u533A\\u57DF.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/\\u724C\\u684C\\u6D4B\\u8BD5\\u6295\\u5C04\\u7269.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/\\u724C\\u684C\\u6D4B\\u8BD5\\u547D\\u4E2D\\u7ED3\\u679C.prefab",
  ]) {
    if (collectorSettingText.includes(obsoletePath)) {
      fail(`YooAsset 收集配置仍把正式表现 Prefab 指向测试目录：${obsoletePath}。`);
    }
  }
  for (const requiredPath of [
    "Assets/Art/Prefabs/牌桌/卡牌视图.prefab",
    "Assets/Art/Prefabs/牌桌/行动进度.prefab",
    "Assets/Art/Prefabs/牌桌/战斗区域.prefab",
    "Assets/Art/Prefabs/牌桌/投射物.prefab",
    "Assets/Art/Prefabs/牌桌/命中结果.prefab",
  ]) {
    assertCollectorSettingEntry(
      collectorSettingText,
      requiredPath,
      {
        AddressRuleName: "AddressByFileName",
        PackRuleName: "PackDirectory",
        FilterRuleName: "CollectAll",
        AssetTags: "test",
      },
      "正式表现 Prefab 收集器");
  }
  for (const obsoleteUiPath of [
    "Assets/Gameplay/Tests/\\u724C\\u684C/ScenarioTurnPanel.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/TabletopCardInfoPanel.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/ConfirmationDialogPanel.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/TabletopActionChoicePanel.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/TabletopActionPlanPanel.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/ScenarioSavePanel.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/ScenarioJournalPanel.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/ScenarioTitlePanel.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/UISettings.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/ScenarioPausePanel.prefab",
    "Assets/Gameplay/Tests/\\u724C\\u684C/FoundationGameUI.prefab",
  ]) {
    if (collectorSettingText.includes(obsoleteUiPath)) {
      fail(`YooAsset 收集配置仍把正式 UI Prefab 指向测试目录：${obsoleteUiPath}。`);
    }
  }
  for (const obsoletePrefabPath of [
    "Assets/Gameplay/Tests/牌桌/ScenarioTurnPanel.prefab",
    "Assets/Gameplay/Tests/牌桌/TabletopCardInfoPanel.prefab",
    "Assets/Gameplay/Tests/牌桌/ConfirmationDialogPanel.prefab",
    "Assets/Gameplay/Tests/牌桌/TabletopActionChoicePanel.prefab",
    "Assets/Gameplay/Tests/牌桌/TabletopActionPlanPanel.prefab",
    "Assets/Gameplay/Tests/牌桌/ScenarioSavePanel.prefab",
    "Assets/Gameplay/Tests/牌桌/ScenarioJournalPanel.prefab",
    "Assets/Gameplay/Tests/牌桌/ScenarioTitlePanel.prefab",
    "Assets/Gameplay/Tests/牌桌/UISettings.prefab",
    "Assets/Gameplay/Tests/牌桌/ScenarioPausePanel.prefab",
    "Assets/Gameplay/Tests/牌桌/FoundationGameUI.prefab",
    "Assets/Gameplay/Tests/牌桌/牌桌测试卡牌视图.prefab",
    "Assets/Gameplay/Tests/牌桌/牌桌测试命中结果.prefab",
    "Assets/Gameplay/Tests/牌桌/牌桌测试战斗区域.prefab",
    "Assets/Gameplay/Tests/牌桌/牌桌测试投射物.prefab",
    "Assets/Gameplay/Tests/牌桌/牌桌测试行动进度.prefab",
  ]) {
    if (exists(obsoletePrefabPath)) {
      fail(`旧测试表现 Prefab 仍存在，会制造第二套 StackCraft 复刻真相：${obsoletePrefabPath}。正式 UI / 牌桌表现 Prefab 必须放在 Assets/Art/Prefabs。`);
    }
  }
  for (const requiredUiPath of [
    "Assets/Art/Prefabs/UI/ScenarioTurnPanel.prefab",
    "Assets/Art/Prefabs/UI/TabletopCardInfoPanel.prefab",
    "Assets/Art/Prefabs/UI/ConfirmationDialogPanel.prefab",
    "Assets/Art/Prefabs/UI/TabletopActionChoicePanel.prefab",
    "Assets/Art/Prefabs/UI/TabletopActionPlanPanel.prefab",
    "Assets/Art/Prefabs/UI/ScenarioSavePanel.prefab",
    "Assets/Art/Prefabs/UI/ScenarioJournalPanel.prefab",
    "Assets/Art/Prefabs/UI/ScenarioTitlePanel.prefab",
    "Assets/Art/Prefabs/UI/UISettings.prefab",
    "Assets/Art/Prefabs/UI/ScenarioPausePanel.prefab",
    "Assets/Art/Prefabs/UI/FoundationGameUI.prefab",
  ]) {
    assertCollectorSettingEntry(
      collectorSettingText,
      requiredUiPath,
      {
        AddressRuleName: "AddressByFileName",
        PackRuleName: "PackDirectory",
        FilterRuleName: "CollectAll",
        AssetTags: "test",
      },
      "正式 UI Prefab 收集器");
  }
}

const stackCraftUiLayer = 5;
const uiLayerPrefabPaths = [
  "Assets/Art/Prefabs/UI/ScenarioTurnPanel.prefab",
  "Assets/Art/Prefabs/UI/TabletopCardInfoPanel.prefab",
  "Assets/Art/Prefabs/UI/ConfirmationDialogPanel.prefab",
  "Assets/Art/Prefabs/UI/TabletopActionChoicePanel.prefab",
  "Assets/Art/Prefabs/UI/TabletopActionPlanPanel.prefab",
  "Assets/Art/Prefabs/UI/ScenarioSavePanel.prefab",
  "Assets/Art/Prefabs/UI/ScenarioJournalPanel.prefab",
  "Assets/Art/Prefabs/UI/ScenarioTitlePanel.prefab",
  "Assets/Art/Prefabs/UI/UISettings.prefab",
  "Assets/Art/Prefabs/UI/ScenarioPausePanel.prefab",
  "Assets/Art/Prefabs/UI/FoundationGameUI.prefab",
  "Assets/Art/Prefabs/牌桌/命中结果.prefab",
  "Assets/Art/Prefabs/牌桌/行动进度.prefab",
];
for (const prefabPath of uiLayerPrefabPaths) {
  const prefabText = readIfExists(prefabPath);
  if (prefabText == null) {
    fail(`缺少 UI Layer 对账目标 Prefab：${prefabPath}`);
  } else {
    assertAllUnityGameObjectsLayer(
      unityYamlObjects(prefabText),
      stackCraftUiLayer,
      `${prefabPath} 的 StackCraft UI Layer`);
  }
}
const foundationSceneMenuSourceForUiLayer = readIfExists("Assets/Tests/Support/Editor/FoundationTestSceneMenu.cs");
const foundationTitleSceneMenuSourceForUiLayer = readIfExists("Assets/Tests/Support/Editor/FoundationTitleTestSceneMenu.cs");
if (foundationSceneMenuSourceForUiLayer == null) {
  fail("缺少 FoundationTestSceneMenu，无法证明正式 UI Prefab 重建时会稳定写入 Unity UI Layer。");
} else {
  assertCsharpFieldInitializerEquals(
    foundationSceneMenuSourceForUiLayer,
    "UnityUiLayer",
    "5",
    "FoundationTestSceneMenu 使用 StackCraft UI Layer");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSourceForUiLayer,
    "private static void SetLayerRecursively(",
    [
      "root.layer = layer;",
      "foreach (Transform child in root.transform)",
      "SetLayerRecursively(child.gameObject, layer);",
    ],
    "FoundationTestSceneMenu 递归写入 UI Layer");
  for (const [anchor, label] of [
    ["private static void EnsureTabletopHitResultViewPrefab(", "命中结果 Prefab"],
    ["private static void EnsureTabletopActionProgressViewPrefab()", "行动进度 Prefab"],
    ["private static void EnsureTabletopActionChoicePanelPrefab()", "行动选择 UI Prefab"],
    ["private static void EnsureTabletopActionPlanPanelPrefab()", "行动计划 UI Prefab"],
    ["private static void EnsureScenarioTurnPanelPrefab()", "剧本回合 HUD Prefab"],
    ["private static GameObject EnsureGameUiPrefab()", "地基 UI 宿主 Prefab"],
    ["private static void EnsureScenarioPausePanelPrefab()", "暂停 UI Prefab"],
    ["private static void EnsureScenarioSavePanelPrefab()", "存档 UI Prefab"],
    ["private static void EnsureConfirmationDialogPanelPrefab()", "确认框 UI Prefab"],
    ["private static void EnsureTabletopCardInfoPanelPrefab()", "卡牌详情 UI Prefab"],
    ["private static void EnsureScenarioJournalPanelPrefab()", "剧本日志 UI Prefab"],
  ]) {
    assertCsharpBlockContainsOrdered(
      foundationSceneMenuSourceForUiLayer,
      anchor,
      ["SetLayerRecursively(root, UnityUiLayer);", "PrefabUtility.SaveAsPrefabAsset(root,"],
      `FoundationTestSceneMenu ${label} 保存前写入 UI Layer`);
  }
}
if (foundationTitleSceneMenuSourceForUiLayer == null) {
  fail("缺少 FoundationTitleTestSceneMenu，无法证明标题 / 设置 UI Prefab 重建时会稳定写入 Unity UI Layer。");
} else {
  for (const [anchor, label] of [
    ["private static void EnsureScenarioTitlePanelPrefab()", "标题 UI Prefab"],
    ["private static void EnsureSettingsPanelPrefab()", "设置 UI Prefab"],
  ]) {
    assertCsharpBlockContainsOrdered(
      foundationTitleSceneMenuSourceForUiLayer,
      anchor,
      ["SetLayerRecursively(root, UnityUiLayer);", "PrefabUtility.SaveAsPrefabAsset(root,"],
      `FoundationTitleTestSceneMenu ${label} 保存前写入 UI Layer`);
  }
}

assertStackCraftTextureCopiesImportVisualSettings();
assertLocalizedStackCraftCardArtCopiesImportVisualSettings();

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
  "卡牌表面_卡包",
  "卡牌表面_地区",
  "交易区",
];

const packCardSurfaceMaterialFile = "Assets/Art/Materials/卡牌表面_卡包.mat";
const packMeshFile = "Assets/Art/Models/卡包.fbx";
const equipmentPanelShaderFile = "Assets/Art/Shaders/装备面板.shadergraph";
const equipmentPanelMeshFile = "Assets/Art/Models/装备面板.fbx";
const equipmentPanelMaterialFile = "Assets/Art/Materials/装备面板.mat";
const cardBuyerSurfaceMaterialFile = "Assets/Art/Materials/交易区.mat";
const cardBuyerCurrencyIconMaterialFile = "Assets/Art/Materials/货币图标.mat";

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

const cardSurfaceMaterialPairs = [
  ["Assets/StackCraft/Materials/Cards/Character.mat", "Assets/Art/Materials/卡牌表面_角色.mat", "Assets/StackCraft/Textures/Cards/Character.png", "Assets/Art/Sprites/StackCraft/Cards/Character.png", "角色"],
  ["Assets/StackCraft/Materials/Cards/Mob.mat", "Assets/Art/Materials/卡牌表面_生物.mat", "Assets/StackCraft/Textures/Cards/Mob.png", "Assets/Art/Sprites/StackCraft/Cards/Mob.png", "生物"],
  ["Assets/StackCraft/Materials/Cards/Mob_Aggressive.mat", "Assets/Art/Materials/卡牌表面_主动敌人.mat", "Assets/StackCraft/Textures/Cards/Mob_Aggressive.png", "Assets/Art/Sprites/StackCraft/Cards/Mob_Aggressive.png", "主动敌人"],
  ["Assets/StackCraft/Materials/Cards/Consumable.mat", "Assets/Art/Materials/卡牌表面_消耗品.mat", "Assets/StackCraft/Textures/Cards/Consumable.png", "Assets/Art/Sprites/StackCraft/Cards/Consumable.png", "消耗品"],
  ["Assets/StackCraft/Materials/Cards/Currency.mat", "Assets/Art/Materials/卡牌表面_货币.mat", "Assets/StackCraft/Textures/Cards/Currency.png", "Assets/Art/Sprites/StackCraft/Cards/Currency.png", "货币"],
  ["Assets/StackCraft/Materials/Cards/Equipment.mat", "Assets/Art/Materials/卡牌表面_装备.mat", "Assets/StackCraft/Textures/Cards/Equipment.png", "Assets/Art/Sprites/StackCraft/Cards/Equipment.png", "装备"],
  ["Assets/StackCraft/Materials/Cards/Material.mat", "Assets/Art/Materials/卡牌表面_材料.mat", "Assets/StackCraft/Textures/Cards/Material.png", "Assets/Art/Sprites/StackCraft/Cards/Material.png", "材料"],
  ["Assets/StackCraft/Materials/Cards/Recipe.mat", "Assets/Art/Materials/卡牌表面_配方.mat", "Assets/StackCraft/Textures/Cards/Recipe.png", "Assets/Art/Sprites/StackCraft/Cards/Recipe.png", "配方"],
  ["Assets/StackCraft/Materials/Cards/Resource.mat", "Assets/Art/Materials/卡牌表面_资源.mat", "Assets/StackCraft/Textures/Cards/Resource.png", "Assets/Art/Sprites/StackCraft/Cards/Resource.png", "资源"],
  ["Assets/StackCraft/Materials/Cards/Structure.mat", "Assets/Art/Materials/卡牌表面_建筑.mat", "Assets/StackCraft/Textures/Cards/Structure.png", "Assets/Art/Sprites/StackCraft/Cards/Structure.png", "建筑"],
  ["Assets/StackCraft/Materials/Cards/Valuable.mat", "Assets/Art/Materials/卡牌表面_贵重物.mat", "Assets/StackCraft/Textures/Cards/Valuable.png", "Assets/Art/Sprites/StackCraft/Cards/Valuable.png", "贵重物"],
  ["Assets/StackCraft/Materials/Cards/Area.mat", "Assets/Art/Materials/卡牌表面_地区.mat", "Assets/StackCraft/Textures/Cards/Area.png", "Assets/Art/Sprites/StackCraft/Cards/Area.png", "地区"],
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

for (const textureFile of [
  ["Assets/StackCraft/Textures/Cards/Character.png", "Assets/Art/Sprites/StackCraft/Cards/Character.png", "角色"],
  ["Assets/StackCraft/Textures/Cards/Mob.png", "Assets/Art/Sprites/StackCraft/Cards/Mob.png", "生物"],
  ["Assets/StackCraft/Textures/Cards/Mob_Aggressive.png", "Assets/Art/Sprites/StackCraft/Cards/Mob_Aggressive.png", "主动敌人"],
  ["Assets/StackCraft/Textures/Cards/Consumable.png", "Assets/Art/Sprites/StackCraft/Cards/Consumable.png", "消耗品"],
  ["Assets/StackCraft/Textures/Cards/Currency.png", "Assets/Art/Sprites/StackCraft/Cards/Currency.png", "货币"],
  ["Assets/StackCraft/Textures/Cards/Equipment.png", "Assets/Art/Sprites/StackCraft/Cards/Equipment.png", "装备"],
  ["Assets/StackCraft/Textures/Cards/Material.png", "Assets/Art/Sprites/StackCraft/Cards/Material.png", "材料"],
  ["Assets/StackCraft/Textures/Cards/Recipe.png", "Assets/Art/Sprites/StackCraft/Cards/Recipe.png", "配方"],
  ["Assets/StackCraft/Textures/Cards/Resource.png", "Assets/Art/Sprites/StackCraft/Cards/Resource.png", "资源"],
  ["Assets/StackCraft/Textures/Cards/Structure.png", "Assets/Art/Sprites/StackCraft/Cards/Structure.png", "建筑"],
  ["Assets/StackCraft/Textures/Cards/Valuable.png", "Assets/Art/Sprites/StackCraft/Cards/Valuable.png", "贵重物"],
  ["Assets/StackCraft/Textures/Cards/Area.png", "Assets/Art/Sprites/StackCraft/Cards/Area.png", "地区"],
]) {
  assertSameFileHash(textureFile[0], textureFile[1], `StackCraft ${textureFile[2]}卡牌分族贴图`);
}

for (const surfaceFile of expectedCardSurfaceMaterialFiles) {
  if (!exists(surfaceFile)) {
    fail(`缺少 StackCraft 卡牌类别材质自有副本：${surfaceFile}`);
  }
  if (!exists(`${surfaceFile}.meta`)) {
    fail(`缺少 StackCraft 卡牌类别材质自有副本 meta：${surfaceFile}.meta`);
  }
  if (collectorSettingText != null) {
    assertCollectorSettingEntry(
      collectorSettingText,
      surfaceFile,
      {
        AddressRuleName: "AddressByFileName",
        PackRuleName: "PackDirectory",
        FilterRuleName: "CollectAll",
        AssetTags: "test",
      },
      "卡牌类别材质收集器");
  }
}

for (const [sourceMaterialFile, localMaterialFile, sourceTextureFile, localTextureFile, label] of cardSurfaceMaterialPairs) {
  const sourceMaterialText = readIfExists(sourceMaterialFile);
  const localMaterialText = readIfExists(localMaterialFile);
  if (sourceMaterialText == null) {
    fail(`缺少 StackCraft ${label}卡牌材质来源：${sourceMaterialFile}`);
    continue;
  }
  if (localMaterialText == null) {
    fail(`缺少 StackCraft ${label}卡牌材质自有副本：${localMaterialFile}`);
    continue;
  }
  const sourceTextureGuid = guidFromMetaPath(`${sourceTextureFile}.meta`, `StackCraft ${label}卡牌分族来源贴图`);
  const localTextureGuid = guidFromMetaPath(`${localTextureFile}.meta`, `StackCraft ${label}卡牌分族自有贴图`);
  if (sourceTextureGuid === localTextureGuid) {
    fail(`${localTextureFile}.meta 复用了 StackCraft 来源贴图 GUID ${sourceTextureGuid}；自有复制素材必须使用项目新 GUID。`);
  }
  for (const propertyName of ["_BaseTex", "_MainTex", "_OverlayTex"]) {
    assertMappedStackCraftTextureGuid(
      sourceMaterialText,
      localMaterialText,
      propertyName,
      sourceMaterialFile,
      localMaterialFile,
      `StackCraft ${label}卡牌材质 ${propertyName}`,
    );
  }
  for (const propertyName of [
    "_OverlayScale",
    "_OverlayOffset",
    "_OverlayTint",
    "_FlashAmount",
  ]) {
    const sourceLine = yamlPropertyLine(sourceMaterialText, propertyName);
    const localLine = yamlPropertyLine(localMaterialText, propertyName);
    if (sourceLine == null) {
      fail(`${sourceMaterialFile} 缺少 StackCraft 卡图比例 / 颜色参数：${propertyName}`);
    } else if (localLine !== sourceLine) {
      fail(`${localMaterialFile} 的 ${propertyName} 与 StackCraft ${label}卡牌材质不一致：${localLine ?? "<缺失>"}，应为 ${sourceLine}`);
    }
  }
}

if (!exists(packCardSurfaceMaterialFile)) {
  fail(`缺少 StackCraft PackInstance 独立卡包材质自有副本：${packCardSurfaceMaterialFile}`);
} else {
  if (!exists(`${packCardSurfaceMaterialFile}.meta`)) {
    fail(`缺少 StackCraft PackInstance 独立卡包材质 meta：${packCardSurfaceMaterialFile}.meta`);
  }
  if (collectorSettingText != null) {
    assertCollectorSettingEntry(
      collectorSettingText,
      packCardSurfaceMaterialFile,
      {
        AddressRuleName: "AddressByFileName",
        PackRuleName: "PackDirectory",
        FilterRuleName: "CollectAll",
        AssetTags: "test",
      },
      "StackCraft PackInstance 独立卡包材质收集器");
  }
}

for (const [assetFile, label] of [
  [equipmentPanelMaterialFile, "StackCraft 角色卡装备面板材质"],
  [cardBuyerSurfaceMaterialFile, "StackCraft CardBuyer 交易区材质"],
  [cardBuyerCurrencyIconMaterialFile, "StackCraft CardBuyer 货币图标材质"],
]) {
  if (!exists(assetFile)) {
    fail(`缺少 ${label} 自有副本：${assetFile}`);
  }
  if (!exists(`${assetFile}.meta`)) {
    fail(`缺少 ${label} meta：${assetFile}.meta`);
  }
  if (collectorSettingText != null) {
    assertCollectorSettingEntry(
      collectorSettingText,
      assetFile,
      {
        AddressRuleName: "AddressByFileName",
        PackRuleName: "PackDirectory",
        FilterRuleName: "CollectAll",
        AssetTags: "test",
      },
      `${label} 收集器`);
  }
}

for (const packArtFile of [
  "Assets/Art/Sprites/StackCraft/Pack.png",
  "Assets/Art/Sprites/StackCraft/PackArts/Starter.png",
  "Assets/Art/Sprites/StackCraft/PackArts/Beginning.png",
  "Assets/Art/Sprites/StackCraft/Square.png",
]) {
  if (!exists(packArtFile)) {
    fail(`缺少 StackCraft PackInstance / PackArts 自有副本：${packArtFile}`);
  }
  if (!exists(`${packArtFile}.meta`)) {
    fail(`缺少 StackCraft PackInstance / PackArts 自有副本 meta：${packArtFile}.meta`);
  }
}

if (!exists("Assets/Art/Models/卡牌.fbx") || !exists("Assets/Art/Models/卡牌.fbx.meta")) {
  fail("缺少 StackCraft Card.fbx 自有副本：Assets/Art/Models/卡牌.fbx。");
}
if (!exists(packMeshFile) || !exists(`${packMeshFile}.meta`)) {
  fail(`缺少 StackCraft Pack.fbx 自有副本：${packMeshFile}。`);
}

if (!exists("Assets/Art/Shaders/卡牌表面.shadergraph") || !exists("Assets/Art/Shaders/卡牌表面.shadergraph.meta")) {
  fail("缺少 StackCraft Card.shadergraph 自有副本：Assets/Art/Shaders/卡牌表面.shadergraph。");
}

const tabletopGeometrySource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Placement/TabletopCardPlacementContracts.cs");
const tabletopCardsEditModeTestsSource = readIfExists("Assets/Editor/Gameplay/Tests/TabletopCardsEditModeTests.cs");
const stackCraftBoardMeshGeometry = deriveBoardMeshGeometryFromStackCraftFbx(
  readBinaryIfExists("Assets/StackCraft/Models/Board.fbx"),
  "StackCraft Board.fbx");
let tabletopCardLimitBonusExpansionPerPoint = null;
if (tabletopGeometrySource == null) {
  fail("缺少牌桌放置几何源码，无法证明卡牌可见尺寸和占地 margin 已拆分。");
} else {
  const stackCraftCharacterCardGeometryText = readIfExists("Assets/StackCraft/Prefabs/Cards/Card_Character.prefab");
  const stackCraftCardPlacementGeometry = deriveCardPlacementGeometryFromStackCraft(
    stackCraftCharacterCardGeometryText,
    stackCraftDefaultCardSettingsText,
    "StackCraft 卡牌放置几何");

  const cardSizeInitializer = stackCraftCardPlacementGeometry == null
    ? null
    : csharpVector2ConstructorFromNumbers(stackCraftCardPlacementGeometry.cardSize.x, stackCraftCardPlacementGeometry.cardSize.y);
  const cardMarginInitializer = stackCraftCardPlacementGeometry == null
    ? null
    : csharpVector2ConstructorFromNumbers(stackCraftCardPlacementGeometry.cardMargin.x, stackCraftCardPlacementGeometry.cardMargin.y);
  const stackStepInitializer = stackCraftCardPlacementGeometry == null
    ? null
    : csharpVector2ConstructorFromNumbers(stackCraftCardPlacementGeometry.stackStep.x, stackCraftCardPlacementGeometry.stackStep.y);
  tabletopCardLimitBonusExpansionPerPoint = stackCraftBoardMeshGeometry?.expansionPerPoint ?? null;

  if (stackCraftBoardMeshGeometry != null) {
    assertCsharpFieldInitializerEquals(
      tabletopGeometrySource,
      "m_bounds",
      csharpRectConstructor(stackCraftBoardMeshGeometry.baseBounds),
      "牌桌放置几何 StackCraft Board.fbx 基础 BakeMesh 边界");
  }
  if (cardSizeInitializer != null) {
    assertCsharpFieldInitializerEquals(
      tabletopGeometrySource,
      "m_cardSize",
      cardSizeInitializer,
      "牌桌放置几何卡牌可见尺寸");
  }
  if (cardMarginInitializer != null) {
    assertCsharpFieldInitializerEquals(
      tabletopGeometrySource,
      "m_cardMargin",
      cardMarginInitializer,
      "牌桌放置几何 StackCraft margin");
  }
  if (stackStepInitializer != null) {
    assertCsharpFieldInitializerEquals(
      tabletopGeometrySource,
      "m_stackStep",
      stackStepInitializer,
      "牌桌放置几何 StackCraft 堆叠步进");
  }
  if (stackCraftOverlapResolveMaxIterations != null) {
    const defaultOverlapResolveMaxIterations = csharpConstIntValue(
      tabletopGeometrySource,
      "DefaultOverlapResolveMaxIterations",
      "牌桌放置几何 StackCraft 重叠解算迭代默认值");
    if (defaultOverlapResolveMaxIterations != null &&
        defaultOverlapResolveMaxIterations !== stackCraftOverlapResolveMaxIterations) {
      fail(`牌桌放置几何重叠解算迭代次数没有对齐 StackCraft Default_Card_Settings.maxIterations：当前 ${defaultOverlapResolveMaxIterations}，应为 ${stackCraftOverlapResolveMaxIterations}。`);
    }
    assertCsharpFieldInitializerEquals(
      tabletopGeometrySource,
      "m_overlapResolveMaxIterations",
      "TabletopCardPlacementRules.DefaultOverlapResolveMaxIterations",
      "牌桌放置几何 StackCraft 重叠解算迭代作者源默认值");
  }
  if (stackCraftSpawnAttachRadius != null) {
    assertCsharpConstFloatEquals(
      tabletopGeometrySource,
      "DefaultSpawnAttachRadius",
      csharpFloatLiteral(stackCraftSpawnAttachRadius),
      "牌桌放置几何 StackCraft 出生吸附半径默认值");
    assertCsharpFieldInitializerEquals(
      tabletopGeometrySource,
      "m_spawnAttachRadius",
      "TabletopCardPlacementRules.DefaultSpawnAttachRadius",
      "牌桌放置几何 StackCraft 出生吸附半径作者源默认值");
  }
  if (tabletopCardLimitBonusExpansionPerPoint != null) {
    assertCsharpFieldInitializerEquals(
      tabletopGeometrySource,
      "m_cardLimitBonusExpansionPerPoint",
      csharpVector2ConstructorFromNumbers(
        tabletopCardLimitBonusExpansionPerPoint.x,
        tabletopCardLimitBonusExpansionPerPoint.y),
      "牌桌放置几何 StackCraft Board BlendShape 每点扩展默认值");
  }
  const placementExpansionCap = csharpConstIntValue(
    tabletopGeometrySource,
    "MaxCardLimitBonusPlacementExpansion",
    "牌桌放置几何 StackCraft Board BlendShape 扩展上限");
  if (placementExpansionCap != null && placementExpansionCap !== "100") {
    fail(`牌桌放置几何扩展上限没有对齐 StackCraft Board.HandleStatsChanged 的 Mathf.Min(stats.TotalBoost, 100)：当前 ${placementExpansionCap}，应为 100。`);
  }
  assertCsharpBlockContainsOrdered(
    tabletopGeometrySource,
    "public TabletopCardPlacementRules CreateRuntime()",
    [
      "new TabletopCardPlacementArea(m_bounds, m_restrictedAreas)",
      "new TabletopCardStackGeometry(m_cardSize, m_stackStep, m_cardMargin)",
      "m_overlapResolveMaxIterations",
      "m_cardLimitBonusExpansionPerPoint",
      "m_spawnAttachRadius",
    ],
    "牌桌放置作者源创建运行规则方法");
  assertCsharpDeclarationAndBlockContainsOrdered(
    tabletopGeometrySource,
    "public TabletopCardPlacementRules(",
    [
      "int overlapResolveMaxIterations = DefaultOverlapResolveMaxIterations",
      "float spawnAttachRadius = DefaultSpawnAttachRadius",
      "if (overlapResolveMaxIterations <= 0)",
      "if (!float.IsFinite(spawnAttachRadius) || spawnAttachRadius < 0f)",
      "OverlapResolveMaxIterations = overlapResolveMaxIterations;",
      "SpawnAttachRadius = spawnAttachRadius;",
    ],
    "牌桌运行放置规则持有 StackCraft 重叠解算迭代参数");
  assertCsharpBlockContainsOrdered(
    tabletopGeometrySource,
    "public readonly struct TabletopCardStackGeometry",
    [
      "public Vector2 CardSize { get; }",
      "public Vector2 CardMargin { get; }",
      "public Vector2 FootprintSize { get; }",
      "public Vector2 StackStep { get; }",
    ],
    "牌桌放置几何 StackCraft 表面尺寸结构");
  assertCsharpBlockContainsOrdered(
    tabletopGeometrySource,
    "public TabletopCardStackGeometry(Vector2 cardSize, Vector2 stackStep, Vector2 cardMargin)",
    [
      "CardSize = cardSize;",
      "CardMargin = cardMargin;",
      "FootprintSize = cardSize + cardMargin;",
      "StackStep = stackStep;",
    ],
    "牌桌放置几何 StackCraft margin 构造语义");
  assertCsharpBlockContainsOrdered(
    tabletopGeometrySource,
    "internal Rect CalculateFootprint(Vector2 stackPosition, int cardCount)",
    [
      "return CalculateFootprint(stackPosition, cardCount, CardSize);",
    ],
    "牌桌放置几何默认尺寸占地只能作为程序集内部测试/协作者入口");
  if (tabletopGeometrySource.includes("public Rect CalculateFootprint(Vector2 stackPosition, int cardCount)")) {
    fail("牌桌放置几何默认尺寸占地重载不应作为公开 API；正式牌桌必须注入内容定义尺寸。");
  }
  assertCsharpBlockContainsOrdered(
    tabletopGeometrySource,
    "internal Rect CalculateFootprint(Vector2 stackPosition, int cardCount, Vector2 cardSize)",
    [
      "if (!IsFinite(cardSize) || cardSize.x <= 0f || cardSize.y <= 0f)",
      "Vector2 span = StackStep * (cardCount - 1);",
      "Vector2 center = stackPosition + span * 0.5f;",
      "Vector2 size = cardSize + CardMargin + new Vector2(Mathf.Abs(span.x), Mathf.Abs(span.y));",
      "return new Rect(center - size * 0.5f, size);",
    ],
    "牌桌放置几何使用实际卡牌尺寸和 StackCraft margin 占地计算语义");
  if (tabletopGeometrySource.includes("Vector2 size = CardSize + new Vector2(Mathf.Abs(span.x), Mathf.Abs(span.y));")) {
    fail("牌桌放置几何仍把可见卡牌尺寸直接当占地尺寸，缺少 StackCraft margin 语义。");
  }
  if (tabletopGeometrySource.includes("Vector2 size = FootprintSize + new Vector2(Mathf.Abs(span.x), Mathf.Abs(span.y));")) {
    fail("牌桌放置几何仍只使用地区默认卡牌尺寸，卡包/交易区等 StackCraft 特殊尺寸没有进入玩法占地。");
  }
  assertCsharpBlockContainsOrdered(
    tabletopGeometrySource,
    "public TabletopCardPlacementRules CreateForCardLimitBonus",
    [
      "int placementExpansionBonus = Math.Min(cardLimitBonus, MaxCardLimitBonusPlacementExpansion);",
      "Vector2 expansion = CardLimitBonusExpansionPerPoint * placementExpansionBonus;",
      "Rect bounds = Area.Bounds;",
      "Rect expandedBounds = new Rect(",
      "bounds.xMin - expansion.x",
      "bounds.yMin - expansion.y",
      "bounds.width + expansion.x * 2f",
      "bounds.height + expansion.y * 2f",
      "IReadOnlyList<Rect> expandedRestrictedAreas = CreateExpandedRestrictedAreas(bounds, Area.RestrictedAreas, expansion);",
      "new TabletopCardPlacementArea(expandedBounds, expandedRestrictedAreas)",
      "SpawnAttachRadius",
    ],
    "牌桌卡牌上限加成扩展边界和顶部页眉禁放区方法");
  assertCsharpBlockContainsOrdered(
    tabletopGeometrySource,
    "private static IReadOnlyList<Rect> CreateExpandedRestrictedAreas",
    [
      "if (restrictedAreas == null || restrictedAreas.Count == 0)",
      "List<Rect> expandedRestrictedAreas = new List<Rect>(restrictedAreas.Count);",
      "if (TabletopCardPlacementArea.IsFullWidthTopRestrictedBand(originalBounds, restrictedArea))",
      "restrictedArea.xMin - expansion.x",
      "restrictedArea.yMin + expansion.y",
      "restrictedArea.width + expansion.x * 2f",
      "restrictedArea.height",
      "expandedRestrictedAreas.Add(restrictedArea);",
    ],
    "牌桌卡牌上限加成同步移动 StackCraft 顶部页眉禁放区方法");
  assertCsharpBlockContainsOrdered(
    tabletopGeometrySource,
    "internal static bool IsFullWidthTopRestrictedBand",
    [
      "Mathf.Approximately(restrictedArea.xMin, bounds.xMin)",
      "Mathf.Approximately(restrictedArea.xMax, bounds.xMax)",
      "Mathf.Approximately(restrictedArea.yMax, bounds.yMax)",
    ],
    "牌桌顶部页眉禁放区识别方法");
  if (tabletopGeometrySource.includes("new TabletopCardPlacementArea(expandedBounds, Area.RestrictedAreas)")) {
    fail("牌桌卡牌上限加成仍只扩展边界但复用旧禁放区；StackCraft Board 顶部页眉禁放区必须跟随扩展后的顶部边界。");
  }
}

function hasThirdPositionalVector2Argument(sourceText, constructorName) {
  const needle = `new ${constructorName}(`;
  let searchFrom = 0;
  while (searchFrom < sourceText.length) {
    const start = sourceText.indexOf(needle, searchFrom);
    if (start < 0) return false;
    const openParen = start + needle.length - 1;
    const args = [];
    let argStart = openParen + 1;
    let depth = 1;
    for (let index = openParen + 1; index < sourceText.length; index++) {
      const char = sourceText[index];
      if (char === "(") {
        depth++;
        continue;
      }
      if (char === ")") {
        depth--;
        if (depth === 0) {
          args.push(sourceText.slice(argStart, index).trim());
          break;
        }
        continue;
      }
      if (char === "," && depth === 1) {
        args.push(sourceText.slice(argStart, index).trim());
        argStart = index + 1;
      }
    }
    if (args.length >= 3 && args[2].startsWith("new Vector2(")) {
      return true;
    }
    searchFrom = openParen + 1;
  }
  return false;
}

if (tabletopCardsEditModeTestsSource != null &&
    hasThirdPositionalVector2Argument(tabletopCardsEditModeTestsSource, "TabletopCardPlacementRules")) {
  fail("牌桌 EditMode 测试仍用第三个位置参数传入 Vector2；TabletopCardPlacementRules 第三个参数是重叠解算迭代次数，扩展向量必须用 cardLimitBonusExpansionPerPoint 命名参数。");
}

const tabletopCoordinateSpaceSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/TabletopCoordinateSpace.cs");
if (tabletopCoordinateSpaceSource == null) {
  fail("缺少牌桌坐标唯一映射入口 TabletopCoordinateSpace；无法证明 StackCraft XZ 桌面没有被再次适配成 XY。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopCoordinateSpaceSource,
    "public static Vector3 ToLocalPosition",
    ["return new Vector3(tablePosition.x, height, tablePosition.y);"],
    "牌桌坐标映射入口 ToLocalPosition 的 StackCraft XZ 桌面语义");
  assertCsharpBlockContainsOrdered(
    tabletopCoordinateSpaceSource,
    "public static Vector2 ToTablePosition",
    ["return new Vector2(localPosition.x, localPosition.z);"],
    "牌桌坐标映射入口 ToTablePosition 的 StackCraft XZ 桌面语义");
  assertCsharpBlockContainsOrdered(
    tabletopCoordinateSpaceSource,
    "public static Vector3 ToLocalDelta",
    ["return new Vector3(tableDelta.x, heightDelta, tableDelta.y);"],
    "牌桌坐标映射入口 ToLocalDelta 的 StackCraft XZ 桌面语义");
  assertCsharpBlockContainsOrdered(
    tabletopCoordinateSpaceSource,
    "public static Plane CreateTablePlane",
    ["return new Plane(tableTransform.up, tableTransform.position);"],
    "牌桌坐标映射入口 CreateTablePlane 的 StackCraft 桌面平面语义");
}

const tabletopLayoutSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopCardLayout.cs");
if (tabletopLayoutSource == null) {
  fail("缺少牌桌卡牌布局源码，无法证明堆叠位置按 StackCraft XZ 桌面投影。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopLayoutSource,
    "public static TabletopCardPose Calculate(Vector2 stackPosition",
    [
      "Vector3 stackBasePosition = TabletopCoordinateSpace.ToLocalPosition(stackPosition)",
      "stackBasePosition + parameters.StackVisualStep * cardIndex",
      "checked(parameters.BaseSortingOrder + cardIndex)",
    ],
    "牌桌卡牌布局 Calculate(Vector2) 的 StackCraft XZ 堆叠投影语义");
}

const foundationSceneMenuSource = readIfExists("Assets/Tests/Support/Editor/FoundationTestSceneMenu.cs");
const foundationTitleSceneMenuSource = readIfExists("Assets/Tests/Support/Editor/FoundationTitleTestSceneMenu.cs");
const tabletopBattleAreaViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopBattleAreaView.cs");
if (tabletopBattleAreaViewSource == null) {
  fail("缺少牌桌战斗区域视图源码，无法证明战斗区域按 StackCraft XZ 桌面投影。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopBattleAreaViewSource,
    "public sealed class TabletopBattleAreaView",
    [
      '[LabelText("区域渲染器")]',
      '[Tooltip("战斗区域的桌面贴图渲染器；Prefab 根节点必须旋转到 XZ 牌桌平面，区域尺寸由牌桌权威战斗区域派生。")]',
      "private SpriteRenderer m_renderer;",
    ],
    "牌桌战斗区域视图 Inspector 作者字段说明");
  assertCsharpBlockContainsOrdered(
    tabletopBattleAreaViewSource,
    "internal void ApplyArea",
    [
      "DisplayedArea = area;",
      "transform.localPosition = TabletopCoordinateSpace.ToLocalPosition(area.center, -0.002f);",
      "transform.localScale = new Vector3(area.width, area.height, 1f);",
      "m_renderer.sortingOrder = sortingOrder;",
      "gameObject.SetActive(true);",
    ],
    "牌桌战斗区域视图 ApplyArea 的 StackCraft XZ 桌面投影语义");
}
const battleAreaPrefabText = readIfExists("Assets/Art/Prefabs/牌桌/战斗区域.prefab");
const tabletopWorldSpaceRotation = foundationSceneMenuSource == null
  ? null
  : unityQuaternionValuesFromCsharpEulerXAssignment(
    foundationSceneMenuSource,
    "root.transform.localRotation",
    "FoundationTestSceneMenu 牌桌 WorldSpace 表现旋转");
if (battleAreaPrefabText == null) {
  fail("缺少战斗区域 Prefab，无法证明战斗区域表面落在 StackCraft XZ 桌面。");
} else {
  const battleAreaGeneratorSource = readIfExists("Assets/Tests/Support/Editor/FoundationTestSceneMenu.cs");
  const battleAreaColor = battleAreaGeneratorSource == null
    ? null
    : unityColorLiteralFromCsharpAssignment(
      battleAreaGeneratorSource,
      "renderer.color",
      "战斗区域本地适配颜色");
  const battleAreaPrefabYaml = unityYamlObjects(battleAreaPrefabText);
  if (tabletopWorldSpaceRotation != null) {
    assertUnityComponentInlineNumericPropertyMatches(
      battleAreaPrefabYaml,
      "战斗区域",
      4,
      "m_LocalRotation",
      tabletopWorldSpaceRotation,
      ["x", "y", "z", "w"],
      "战斗区域 Prefab XZ 桌面旋转");
  }
  assertUnityMonoBehaviourPropertyExists(
    battleAreaPrefabYaml,
    "战斗区域",
    "Gameplay.Runtime::Gameplay.Tabletop.TabletopBattleAreaView",
    "m_renderer",
    "战斗区域 Prefab 视图脚本");
  if (battleAreaColor != null) {
    assertUnityComponentScalarEquals(
      battleAreaPrefabYaml,
      "战斗区域",
      212,
      "m_Color",
      battleAreaColor,
      "战斗区域 Prefab 本地适配颜色");
  }
}

if (tabletopViewSettingsSource == null) {
  fail("缺少牌桌视图设置源码，无法证明堆叠视觉步进来自 StackCraft 参数。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopViewSettingsSource,
    "public sealed class TabletopViewSettings",
    [
      "public float DragFollowSharpness => m_dragFollowSharpness;",
      "public float ClickThreshold => m_clickThreshold;",
      "public float AttachRadius => m_attachRadius;",
      "public float DragHeight => m_dragHeight;",
    ],
    "牌桌视图设置 StackCraft 拖拽手感只读参数");
  assertCsharpBlockContainsOrdered(
    tabletopViewSettingsSource,
    "public TabletopCardLayoutParameters CreateLayoutParameters",
    [
      "new Vector3(geometry.StackStep.x, m_stackHeightStep, geometry.StackStep.y)",
      "m_baseSortingOrder",
    ],
    "牌桌视图设置 StackCraft 牌堆视觉步进参数");
}

const tabletopViewSettingsAssetText = readIfExists("Assets/Gameplay/Tests/牌桌/牌桌测试视图设置.asset");
if (tabletopViewSettingsAssetText == null) {
  fail("缺少牌桌测试视图设置资产，无法证明 StackCraft 牌堆视觉参数已回写到作者源。");
} else {
  assertTabletopViewSettingsMatchStackCraft(
    stackCraftDefaultCardSettingsText,
    tabletopViewSettingsAssetText,
    "Assets/Gameplay/Tests/牌桌/牌桌测试视图设置.asset",
    tabletopViewSettingsSource);
}

const stackCraftTradeManagerSource = readIfExists("Assets/StackCraft/Scripts/Trading/TradeManager.cs");
const packVendorUnlockDurationSeconds = stackCraftTradeManagerSource == null
  ? null
  : csharpWaitForSecondsRealtimeAfter(
    stackCraftTradeManagerSource,
    "private IEnumerator PlayActivationSequence",
    "StackCraft TradeManager.PlayActivationSequence 解锁提示时长");

const tabletopViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopView.cs");
if (tabletopViewSource == null) {
  fail("缺少牌桌视图源码，无法证明 StackCraft 牌堆拖拽链式跟随已由正式视图承接。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void RefreshPackVendorSurfaces",
    [
      "foreach (ViewEntry entry in m_views.Values)",
      "ApplyPackVendorSurface(entry);",
    ],
    "牌桌视图刷新商贩表面方法");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void ApplyPackVendorSurface",
    [
      "entry.TabletopCard is not PackVendorCard vendorCard",
      "entry.Definition is not PackVendorDefinition vendorDefinition",
      "m_tabletop.ContentIndex.TryGet(",
      "vendorDefinition.OfferedPackId",
      "out CardPackDefinition resolvedPack",
      "entry.View.ApplyPackVendorSurface(",
      "offeredPack.DisplayName",
      "vendorCard.RemainingPrice",
      "offeredPack.GetCollectionProgress(m_tabletop.IsContentDiscovered)",
    ],
    "牌桌视图投影 StackCraft 卡包商贩表面方法");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "internal bool TryFindNearestCardViewWithinAttachRadius",
    [
      "float attachRadius = m_settings.AttachRadius;",
      "TabletopCardView bestView = null;",
      "candidate.TabletopCard?.Stack",
      "ContainsCandidateStackBottomCardId(allowedStackBottomCardIds, candidate)",
      "candidate.DistanceToVisibleFootprint(tablePosition)",
      "candidate.SortingOrder > bestSortingOrder",
    ],
    "牌桌视图 StackCraft AttachRadius 在可合堆底牌集合内查找最近可见卡面候选");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "internal void SetDropTargetHighlights",
    [
      "m_highlightRemovalBuffer",
      "m_highlightedDropTargetCardIds",
      "m_highlightRemovalBuffer",
      "RequireLiveCardOrEmpty(cardId, \"高亮\")",
      "SetCardHighlight(cardId, highlighted: true)",
    ],
    "牌桌视图用本地集合高亮 StackCraft 可堆叠目标底牌");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "internal void ClearDropTargetHighlights",
    [
      "foreach (TabletopCardId cardId in m_highlightedDropTargetCardIds)",
      "SetCardHighlight(cardId, highlighted: false)",
      "m_highlightedDropTargetCardIds.Clear()",
    ],
    "牌桌视图释放或取消时清理 StackCraft 可堆叠目标底牌高亮");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void EnsureCardBuyerCurrencyArtwork",
    [
      "entry.Definition is not CardBuyerDefinition buyerDefinition",
      "m_cardBuyerCurrencyArtwork.TryGetValue(buyerDefinition.ContentId, out Sprite cachedArtwork)",
      "entry.View.ApplyCardBuyerSurface(cachedArtwork)",
      "m_tabletop.ContentIndex.TryGet(",
      "buyerDefinition.CurrencyCardId",
      "out currencyDefinition",
      "SoftAssetReference<Sprite> artReference = currencyDefinition.Artwork",
      "ResourceSystem.LoadAssetAsync<Sprite>(artReference.Address)",
      "m_cardBuyerCurrencyArtHandles.Add(buyerDefinition.ContentId, handle)",
      "m_cardBuyerCurrencyArtwork[buyerDefinition.ContentId] = artwork",
      "ApplyCardBuyerCurrencyArtwork(buyerDefinition.ContentId, artwork)",
    ],
    "牌桌视图加载 StackCraft 收购点货币图标方法");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void ApplyCardBuyerCurrencyArtwork",
    [
      "foreach (ViewEntry entry in m_views.Values)",
      "entry.Definition.ContentId.Equals(buyerContentId)",
      "entry.View.ApplyCardBuyerSurface(artwork)",
    ],
    "牌桌视图回填 StackCraft 收购点货币图标方法");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void ApplyCardPose(",
    [
      "m_tabletop.TryGetBattlePose(",
      "entry.View.SetCharacterStatusVisible(",
      "int previewStartIndex = (m_hasDragPreview ? stack.IndexOf(m_dragPreviewCardId) : (-1));",
      "if (previewStartIndex >= 0 && cardIndex >= previewStartIndex)",
      "int previewCardIndex = cardIndex - previewStartIndex;",
      "entry.View.ApplyDragPose(CreateDragPreviewPose(stack, cardIndex, previewCardIndex), previewCardIndex == 0, m_settings.DragFollowSharpness)",
      "TabletopCardLayout.Calculate(",
    ],
    "牌桌视图卡牌姿态和拖拽牌段跟随方法");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private TabletopCardPose CreateDragPreviewPose",
    [
      "TabletopCoordinateSpace.ToLocalPosition(m_dragPreviewPosition, m_settings.DragHeight)",
      "layoutParameters.StackVisualStep * previewCardIndex",
      "precedingEntry.View.transform.localPosition + layoutParameters.StackVisualStep",
    ],
    "牌桌视图拖拽预览姿态方法");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void PlayPresentationCue(TabletopPresentationCue cue)",
    [
      "cue.Kind == TabletopPresentationCueKind.CameraFocus",
      "cue.Kind == TabletopPresentationCueKind.CardHighlight",
      "PlayCardHighlight(cue)",
      "PlayAudio(m_settings.GetPresentationAudio(cue.Kind));",
      "cue.Kind != TabletopPresentationCueKind.CardSmoke",
      "RequestCardSmokeEffect(cue.TablePosition)",
    ],
    "牌桌视图播放 StackCraft 商贩解锁表现提示方法");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void PlayCardHighlight",
    [
      "cue.HasCardId",
      "m_views.TryGetValue(cue.CardId, out ViewEntry entry)",
      "entry.View.ShowPresentationHighlight(PresentationHighlightSeconds)",
    ],
    "牌桌视图播放 StackCraft 商贩卡牌高亮方法");
  if (packVendorUnlockDurationSeconds != null) {
    assertCsharpConstFloatEquals(
      tabletopViewSource,
      "PresentationHighlightSeconds",
      packVendorUnlockDurationSeconds,
      "牌桌视图商贩解锁高亮时长");
  }
}

const scenarioRunSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioRun.cs");
if (scenarioRunSource == null) {
 fail("缺少剧本单局源码，无法证明 StackCraft 卡包商贩解锁反馈由正式单局承接。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "public enum ScenarioTimePace",
    [
      "Paused = 0",
      "Normal = 1",
      "Fast = 2",
    ],
    "剧本单局 StackCraft TimePace 三档语义");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "public ScenarioTimePace CycleTimePace()",
    [
      "ProgressionMode != ActionProgressionMode.RealTime",
      "m_timePace = (ScenarioTimePace)(((int)m_timePace + 1)",
      "Enum.GetValues(typeof(ScenarioTimePace)).Length",
      "return m_timePace;",
    ],
    "剧本单局承接 StackCraft CycleTimePace 速度循环方法");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "internal void AdvanceRealTime(float deltaSeconds)",
    [
      "float timePaceMultiplier = GetTimePaceMultiplier(m_timePace);",
      "if (timePaceMultiplier <= 0f)",
      "return;",
      "double remainingSeconds = deltaSeconds * timePaceMultiplier;",
    ],
    "剧本单局用 TimePace 影响即时推进秒数方法");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private static float GetTimePaceMultiplier",
    [
      "ScenarioTimePace.Paused => 0f",
      "ScenarioTimePace.Normal => 1f",
      "ScenarioTimePace.Fast => 2f",
    ],
    "剧本单局 StackCraft TimePace 速度倍率");
  if (scenarioRunSource.includes("Time.timeScale")) {
    fail("ScenarioRun 不得用 Time.timeScale 复刻 StackCraft TimeManager；速度档位必须只影响本单局实时推进。");
  }
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void RefreshQuestState(int previousCompletedQuestCount)",
    [
      "QuestLog.RecordFact(tabletopState)",
      "QuestLog.RecordFact(new DayReachedQuestTaskFact(CurrentDay))",
      "QuestLog.RecordFact(",
      "new ContentDiscoveredQuestTaskFact(discoveredContentIds[i])",
      "while (changed);",
      "PresentPackVendorUnlocks(previousCompletedQuestCount, QuestLog.CompletedQuestCount);",
    ],
    "剧本单局任务事实刷新后触发 StackCraft 商贩解锁提示方法");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void PresentPackVendorUnlocks(",
    [
      "currentCompletedQuestCount <= previousCompletedQuestCount",
      "IReadOnlyList<TabletopCardStack> stacks = Tabletop.Cards.Stacks",
      "card is not PackVendorCard vendorCard",
      "m_contentIndex.TryGet(vendorCard.ContentId, out PackVendorDefinition vendorDefinition)",
      "previousCompletedQuestCount >= vendorDefinition.MinimumCompletedQuests",
      "currentCompletedQuestCount < vendorDefinition.MinimumCompletedQuests",
      "m_contentIndex.TryGet(vendorDefinition.OfferedPackId, out CardPackDefinition packDefinition)",
      "EventKit.Type.Send(new ScenarioSequencePresentationRequestEvent(",
      "ScenarioId",
      "\"卡包已解锁\"",
      "$\"这里可以购买{packDefinition.DisplayName}。\"",
      "PackVendorUnlockMessageSeconds",
      "vendorCard.Position",
      "vendorCard.Id",
    ],
    "剧本单局发布 StackCraft 卡包商贩解锁请求方法");
  if (packVendorUnlockDurationSeconds != null) {
    assertCsharpConstFloatEquals(
      scenarioRunSource,
      "PackVendorUnlockMessageSeconds",
      packVendorUnlockDurationSeconds,
      "剧本单局商贩解锁提示时长");
  }
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "uint authoritativeRandomSeed,\n\t\t\tModPackageSetSnapshot modPackages,",
    ["m_currencyCardIds = CurrencyCardQuery.BuildCurrencyCardIds(contentIndex);"],
    "剧本单局新开局货币卡查询入口");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private ScenarioRun(",
    ["m_currencyCardIds = CurrencyCardQuery.BuildCurrencyCardIds(contentIndex);"],
    "剧本单局读档货币卡查询入口");
  if (scenarioRunSource.includes("private static HashSet<ContentId> BuildCurrencyCardIds")) {
    fail("ScenarioRun 仍保留自己的货币卡扫描逻辑，和卡包付款语义形成第二套派生判断。");
  }
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void FeedCharacters()",
    [
      "if (m_dayCycleRules.HungerPerCharacter > 0)",
      "int hunger = m_dayCycleRules.HungerPerCharacter;",
      "while (hunger > 0 && foods.Count > 0)",
      "int consumedNutrition = Math.Min(hunger, definition.NutritionPerUse);",
      "hunger -= consumedNutrition;",
      "ApplyFeedingHealing(character, consumedNutrition, healingEffect);",
      "if (hunger > 0)",
      "tabletop.RemoveCard(character.Id);",
    ],
    "剧本单局承接 StackCraft hungerPerCharacter 进食 / 饥饿死亡结算方法");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void ApplyFeedingHealing",
    [
      "character.MaxHealth * 0.5f * consumedNutrition / m_dayCycleRules.HungerPerCharacter",
      "CharacterAttributes.SetBaseValueAndRecalculate",
    ],
    "剧本单局承接 StackCraft 进食恢复 50% 最大生命比例方法");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private int CalculateExcessCardCount()",
    [
      "cardLimitBonus = checked(cardLimitBonus + tabletop.CardLimitBonus);",
      "if (definition.CountsTowardCardLimit)",
      "cardCount = checked(cardCount + 1);",
      "m_dayCycleRules.BaseCardLimit + cardLimitBonus",
    ],
    "剧本单局承接 StackCraft baseCardLimit + 容量加成超限判断方法");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private ScenarioTabletopStats CreateTabletopStats",
    [
      "int cardLimit = checked(m_dayCycleRules.BaseCardLimit + cardLimitBonus);",
      "int nutritionNeed = checked(characterCount * m_dayCycleRules.HungerPerCharacter);",
      "totalFoodNutrition",
      "nutritionNeed",
      "cardsOwned",
      "cardLimit",
    ],
    "剧本 HUD 统计承接 StackCraft NutritionNeed / CardLimit 统计方法");
}

const scenarioDayCycleRulesSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioDayCycleRules.cs");
const scenarioDayCycleSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioDayCycle.cs");
const scenarioRunSnapshotSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioRunSnapshot.cs");
const scenarioRunEditModeTestsSource = readIfExists("Assets/Editor/Gameplay/Tests/ScenarioRunEditModeTests.cs");
const scenarioDirectorForDayCycleSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioDirector.cs");
const scenarioDirectorEditModeTestsSource = readIfExists("Assets/Editor/Gameplay/Tests/ScenarioDirectorEditModeTests.cs");
const scenarioTurnPanelForDayCycleSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/View/ScenarioTurnPanel.cs");
if (scenarioDayCycleRulesSource == null) {
  fail("缺少剧本日终规则作者源，无法证明 StackCraft baseCardLimit / hungerPerCharacter 已由正式剧本规则承接。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioDayCycleRulesSource,
    "public sealed class ScenarioDayCycleRules",
    [
      "private int m_hungerPerCharacter;",
      "private int m_baseCardLimit;",
      "public int HungerPerCharacter => m_hungerPerCharacter;",
      "public int BaseCardLimit => m_baseCardLimit;",
      "CreateRuntime()",
      "HungerPerCharacter,",
      "BaseCardLimit,",
    ],
    "剧本日终规则作者源承接 StackCraft baseCardLimit / hungerPerCharacter 字段");
}

const stackCraftDayCycleManagerSource = readIfExists("Assets/StackCraft/Scripts/Core/DayCycleManager.cs");
if (stackCraftDayCycleManagerSource == null) {
  fail("缺少 StackCraft DayCycleManager 源码，无法证明日终五阶段流程来源。");
} else {
  assertSourceContainsOrdered(
    stackCraftDayCycleManagerSource,
    [
      "public class DayCycleManager : MonoBehaviour",
      "public static DayCycleManager Instance { get; private set; }",
      "public bool IsEndingCycle { get; private set; }",
      "private readonly object dayCycleRequester = \"DayCycleRequester\";",
      "private readonly object dayCycleInputLock = \"DayCycleInputLock\";",
      "TimeManager.Instance.OnDayEnded += HandleDayEnded;",
      "TimeManager.Instance.OnDayEnded -= HandleDayEnded;",
      "CardManager.Instance.OnStatsChanged -= OnStatsChangedDuringSelling;",
    ],
    "StackCraft DayCycleManager 单例、日终状态、输入锁和事件订阅来源结构");
  assertCsharpBlockContainsOrdered(
    stackCraftDayCycleManagerSource,
    "private void HandleDayEnded",
    [
      "InputManager.Instance.AddLock(dayCycleInputLock);",
      "IsEndingCycle = true;",
      "StartCoroutine(NotificationPhase(day));",
    ],
    "StackCraft DayCycleManager 日结束进入通知阶段来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftDayCycleManagerSource,
    "private IEnumerator NotificationPhase",
    [
      "InfoPanel.Instance?.RequestInfoDisplay(",
      "InfoPriority.Modal",
      "$\"End of Day {day}\"",
      "\"Your people are hungry!\"",
      "\"Feed People\"",
      "StartCoroutine(FeedingPhase());",
    ],
    "StackCraft DayCycleManager 日终通知与玩家确认来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftDayCycleManagerSource,
    "private IEnumerator FeedingPhase",
    [
      "InfoPanel.Instance?.RequestInfoDisplay(",
      "\"Feeding People\"",
      "\"Distributing food...\"",
      "yield return CardManager.Instance.FeedCharacters();",
      "var stats = CardManager.Instance.GetStatsSnapshot();",
      "stats.TotalCharacters <= 0",
      "HandleGameOver();",
      "StartSellingPhase();",
    ],
    "StackCraft DayCycleManager 进食、全员死亡和进入超限处理来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftDayCycleManagerSource,
    "private void StartSellingPhase",
    [
      "InputManager.Instance.RemoveLock(dayCycleInputLock);",
      "CardManager.Instance.OnStatsChanged += OnStatsChangedDuringSelling;",
      "CheckSellingCondition(CardManager.Instance.GetStatsSnapshot());",
    ],
    "StackCraft DayCycleManager 超限卖卡阶段解除输入锁并监听统计变化");
  assertCsharpBlockContainsOrdered(
    stackCraftDayCycleManagerSource,
    "private void CheckSellingCondition",
    [
      "int excess = stats.ExcessCards;",
      "if (excess <= 0)",
      "CardManager.Instance.OnStatsChanged -= OnStatsChangedDuringSelling;",
      "StartCoroutine(EncounterPhase());",
      "InfoPanel.Instance?.RequestInfoDisplay(",
      "\"Sell Excess Cards\"",
      "$\"You must sell {excess} excess",
    ],
    "StackCraft DayCycleManager 超限为零才进入遭遇阶段，否则持续提示必须卖卡");
  assertCsharpBlockContainsOrdered(
    stackCraftDayCycleManagerSource,
    "private IEnumerator EncounterPhase",
    [
      "int currentDay = TimeManager.Instance.CurrentDay;",
      "var encounter = EncounterManager.Instance.GetBestEncounter(currentDay);",
      "if (encounter != null)",
      "InputManager.Instance.AddLock(dayCycleInputLock);",
      "yield return EncounterManager.Instance.ExecuteEncounter(encounter);",
      "InputManager.Instance.RemoveLock(dayCycleInputLock);",
      "PrepareForNewDay();",
    ],
    "StackCraft DayCycleManager 最多执行一个日终遭遇后进入新日准备");
  assertCsharpBlockContainsOrdered(
    stackCraftDayCycleManagerSource,
    "private void PrepareForNewDay",
    [
      "InputManager.Instance.AddLock(dayCycleInputLock);",
      "int nextDay = TimeManager.Instance.CurrentDay + 1;",
      "InfoPanel.Instance?.RequestInfoDisplay(",
      "$\"Start of Day {nextDay}\"",
      "\"Everything is ready.\"",
      "\"Start Day\"",
      "IsEndingCycle = false;",
      "InfoPanel.Instance?.ClearInfoRequest(dayCycleRequester);",
      "InputManager.Instance.RemoveLock(dayCycleInputLock);",
      "TimeManager.Instance.StartNewDay();",
      "GameDirector.Instance.SaveGame();",
    ],
    "StackCraft DayCycleManager 新日确认、解除锁、推进日期和自动保存来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftDayCycleManagerSource,
    "private void HandleGameOver",
    [
      "InputManager.Instance.AddLock(dayCycleInputLock);",
      "InfoPanel.Instance?.RequestInfoDisplay(",
      "\"Game Over\"",
      "\"You have no people left.\"",
      "\"Return to Title\"",
      "GameDirector.Instance.GameOver()",
    ],
    "StackCraft DayCycleManager 全员死亡返回标题来源链");
}
if (scenarioDayCycleSource != null) {
  assertCsharpBlockContainsOrdered(
    scenarioDayCycleSource,
    "public enum ScenarioDayCyclePhase",
    [
      "Inactive = 0",
      "AwaitingFeedingConfirmation",
      "AwaitingExcessCardResolution",
      "AwaitingNewDayConfirmation",
      "GameOver",
    ],
    "ScenarioDayCyclePhase 枚举必须接管 StackCraft DayCycleManager 的日终阶段状态");
  assertCsharpBlockContainsOrdered(
    scenarioDayCycleSource,
    "internal sealed class ScenarioDayCycle",
    [
      "internal int EndingDay { get; }",
      "internal ScenarioDayCyclePhase Phase { get; private set; }",
    ],
    "ScenarioDayCycle 运行对象必须持有日终日期和当前阶段");
  assertCsharpBlockContainsOrdered(
    scenarioDayCycleSource,
    "internal void FinishFeeding",
    [
      "RequirePhase(ScenarioDayCyclePhase.AwaitingFeedingConfirmation);",
      "!hasSurvivingCharacters",
      "ScenarioDayCyclePhase.GameOver",
      "excessCardCount > 0",
      "ScenarioDayCyclePhase.AwaitingExcessCardResolution",
      "ScenarioDayCyclePhase.AwaitingNewDayConfirmation",
    ],
    "ScenarioDayCycle 接管 StackCraft 进食后全员死亡 / 超限 / 新日确认分支");
  assertCsharpBlockContainsOrdered(
    scenarioDayCycleSource,
    "internal void FinishExcessCardResolution",
    [
      "RequirePhase(ScenarioDayCyclePhase.AwaitingExcessCardResolution);",
      "Phase = ScenarioDayCyclePhase.AwaitingNewDayConfirmation;",
    ],
    "ScenarioDayCycle 接管 StackCraft 超限处理完成后进入新日确认");
}
if (scenarioRunSource != null) {
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private int AdvanceWorldTurn()",
    [
      "ConfirmedTurnIndex++;",
      "bool startsDayCycle = m_dayCycleRules.Enabled",
      "ConfirmedTurnIndex % m_turnsPerDay == 0",
      "m_dayCycle = new ScenarioDayCycle(previousDay);",
      "PublishDayCycleChanged();",
      "EventKit.Type.Send(new ScenarioTurnConfirmedEvent(",
    ],
    "ScenarioRun 接管 StackCraft TimeManager.OnDayEnded 触发日终流程");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "internal void ContinueDayCycle()",
    [
      "case ScenarioDayCyclePhase.AwaitingFeedingConfirmation:",
      "FeedCharacters();",
      "m_dayCycle.FinishFeeding(",
      "CountCharacters() > 0",
      "CalculateExcessCardCount());",
      "ResolveDayEncounter();",
      "case ScenarioDayCyclePhase.AwaitingExcessCardResolution:",
      "FinishExcessCardResolution();",
      "case ScenarioDayCyclePhase.AwaitingNewDayConfirmation:",
      "m_dayCycle = null;",
      "RefreshQuestState();",
      "case ScenarioDayCyclePhase.GameOver:",
      "throw new InvalidOperationException",
    ],
    "ScenarioRun 接管 StackCraft DayCycleManager 进食、超限、遭遇、新日和全员死亡流程");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void FinishExcessCardResolution()",
    [
      "int excess = CalculateExcessCardCount();",
      "if (excess > 0)",
      "throw new InvalidOperationException",
      "m_dayCycle.FinishExcessCardResolution();",
      "ResolveDayEncounter();",
      "PublishDayCycleChanged();",
    ],
    "ScenarioRun 用明确异常接管 StackCraft 超限未处理时继续提示的阻断语义");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void PublishDayCycleChanged()",
    [
      "EventKit.Type.Send(new ScenarioDayCycleChangedEvent(",
      "ScenarioId",
      "DayCyclePhase",
      "ExcessCardCount",
    ],
    "ScenarioRun 发布日终阶段变化事实供正式 HUD 只读投影");
}
if (scenarioDirectorForDayCycleSource == null) {
  fail("缺少 ScenarioDirector，无法证明 StackCraft 新日后 SaveGame 已由正式剧本导演自动保存接管。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioDirectorForDayCycleSource,
    "public void ContinueDayCycle()",
    [
      "ScenarioDayCyclePhase previousPhase = run.DayCyclePhase;",
      "run.ContinueDayCycle();",
      "previousPhase == ScenarioDayCyclePhase.AwaitingNewDayConfirmation",
      "run.DayCyclePhase == ScenarioDayCyclePhase.Inactive",
      "m_activeSaveSlotId",
      "SaveActiveRunToSlot(slotId)",
    ],
    "ScenarioDirector 接管 StackCraft StartNewDay 后 SaveGame 自动保存语义");
}
if (scenarioTurnPanelForDayCycleSource == null) {
  fail("缺少 ScenarioTurnPanel，无法证明 StackCraft DayCycleManager 的 InfoPanel 按正式 HUD 投影。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioTurnPanelForDayCycleSource,
    "private void ConfirmTurn()",
    [
      "director.ActiveRun.DayCyclePhase == ScenarioDayCyclePhase.GameOver",
      "director.GameOverAsync().Forget();",
      "director.ActiveRun.DayCyclePhase != ScenarioDayCyclePhase.Inactive",
      "director.ContinueDayCycle();",
      "director.ConfirmTurn();",
    ],
    "ScenarioTurnPanel 主按钮接管 StackCraft Feed People / Start Day / Return to Title 确认入口");
  assertCsharpBlockContainsOrdered(
    scenarioTurnPanelForDayCycleSource,
    "private static string GetPrimaryActionLabel",
    [
      "ScenarioDayCyclePhase.AwaitingFeedingConfirmation => \"分配食物\"",
      "ScenarioDayCyclePhase.AwaitingExcessCardResolution => $\"处理超限 {run.ExcessCardCount} 张\"",
      "ScenarioDayCyclePhase.AwaitingNewDayConfirmation => $\"开始第 {run.CurrentDay + 1} 天\"",
      "ScenarioDayCyclePhase.GameOver => \"返回标题\"",
    ],
    "ScenarioTurnPanel 用正式 HUD 文案接管 StackCraft DayCycleManager InfoPanel 按钮文案");
}
if (scenarioRunEditModeTestsSource != null) {
  assertCsharpMethodsExist(
    scenarioRunEditModeTestsSource,
    [
      "DayCycle_WaitsForEndOfDayAndNewDayConfirmationsBeforeAdvancingDate",
      "DayCycle_FeedingConsumesNearestFoodAndKillsCharactersWhenFoodRunsOut",
      "DayCycle_EntersGameOverWhenFeedingLeavesNoCharacters",
      "DayCycle_RequiresActualTabletopCardsToBeReducedBelowTheConfiguredLimit",
    ],
    "ScenarioRunEditModeTests 覆盖 StackCraft DayCycleManager 日终阶段、进食、死亡和超限替代链");
}
if (scenarioDirectorEditModeTestsSource == null) {
  fail("缺少 ScenarioDirectorEditModeTests，无法证明新日自动保存有回归保护。");
} else {
  assertCsharpMethodContainsOrdered(
    scenarioDirectorEditModeTestsSource,
    "ContinueDayCycle_StartsNewDayAndOverwritesTheRunsAssignedSlot",
    [
      "director.ContinueDayCycle();",
      "SaveData container = GameCore.SaveSystem.ExtractSaveContainerFromFile(4);",
      "container.GetModule<ScenarioRunSnapshot>().ConfirmedTurnIndex",
    ],
    "ScenarioDirectorEditModeTests 覆盖 StackCraft StartNewDay 后 SaveGame 替代链");
}
for (const [label, sourceText] of [
  ["ScenarioDayCycle", scenarioDayCycleSource],
  ["ScenarioRun", scenarioRunSource],
  ["ScenarioDirector", scenarioDirectorForDayCycleSource],
  ["ScenarioTurnPanel", scenarioTurnPanelForDayCycleSource],
  ["ScenarioRunEditModeTests", scenarioRunEditModeTestsSource],
  ["ScenarioDirectorEditModeTests", scenarioDirectorEditModeTestsSource],
]) {
  if (sourceText == null) continue;
  for (const obsoleteToken of [
    "CryingSnow.StackCraft",
    "DayCycleManager",
    "TimeManager.Instance",
    "InputManager.Instance",
    "InfoPanel.Instance",
    "CardManager.Instance",
    "EncounterManager.Instance",
    "GameDirector.Instance",
  ]) {
    if (sourceText.includes(obsoleteToken)) {
      fail(`${label} 仍保留 StackCraft DayCycleManager 旧结构残留：${obsoleteToken}`);
    }
  }
}

const stackCraftEncounterDefinitionSource = readIfExists("Assets/StackCraft/Scripts/Encounter/EncounterDefinition.cs");
const stackCraftEncounterManagerSource = readIfExists("Assets/StackCraft/Scripts/Encounter/EncounterManager.cs");
if (stackCraftEncounterDefinitionSource == null) {
  fail("缺少 StackCraft EncounterDefinition 源码，无法证明日终遭遇候选作者源。");
} else {
  assertSourceContainsOrdered(
    stackCraftEncounterDefinitionSource,
    [
      "public enum EncounterType",
      "SpecificDay",
      "Recurring",
      "Range",
      "MinimumDay",
      "public class EncounterDefinition : ScriptableObject",
      "private string id;",
      "private string notificationMessage;",
      "private CardDefinition cardToSpawn;",
      "private int count = 1;",
      "private bool oneTimeOnly = false;",
      "private EncounterType type;",
      "private int dayValue;",
      "private int maxDayValue = 999;",
      "private int priority = 0;",
      "private float chance = 1.0f;",
      "private int maxCardsOnBoardLimit = 100;",
    ],
    "StackCraft EncounterDefinition 身份、提示、刷卡、日期、优先级、概率和牌桌上限作者字段");
  assertCsharpBlockContainsOrdered(
    stackCraftEncounterDefinitionSource,
    "public bool IsValidForDay",
    [
      "if (totalCardsOnBoard >= maxCardsOnBoardLimit) return false;",
      "if (oneTimeOnly && completedEncounters.Contains(id)) return false;",
      "if (cardToSpawn.IsAggressive && isFriendlyMode) return false;",
      "case EncounterType.SpecificDay:",
      "dayConditionMet = (currentDay == dayValue);",
      "case EncounterType.Recurring:",
      "dayConditionMet = (currentDay > 0 && currentDay % dayValue == 0);",
      "case EncounterType.MinimumDay:",
      "dayConditionMet = (currentDay >= dayValue);",
      "case EncounterType.Range:",
      "dayConditionMet = (currentDay >= dayValue && currentDay <= maxDayValue);",
      "return Random.value <= chance;",
    ],
    "StackCraft EncounterDefinition 日期过滤、一次性、友好模式、牌桌上限和概率来源链");
}
if (stackCraftEncounterManagerSource == null) {
  fail("缺少 StackCraft EncounterManager 源码，无法证明遭遇选择、生成和表现来源链。");
} else {
  assertSourceContainsOrdered(
    stackCraftEncounterManagerSource,
    [
      "public static EncounterManager Instance { get; private set; }",
      "private List<EncounterDefinition> allEncounters;",
      "private float spawnEdgePadding = 2f;",
      "private HashSet<string> completedEncounters = new();",
      "GameDirector.Instance.OnSceneDataReady += HandleSceneDataReady;",
      "GameDirector.Instance.OnBeforeSave += HandleBeforeSave;",
    ],
    "StackCraft EncounterManager 单例、候选集合、生成边距和一次性记录来源结构");
  assertCsharpBlockContainsOrdered(
    stackCraftEncounterManagerSource,
    "private void HandleSceneDataReady",
    [
      "if (wasLoaded)",
      "this.completedEncounters = sceneData.CompletedEncounters;",
    ],
    "StackCraft EncounterManager 从场景存档恢复已完成遭遇集合");
  assertCsharpBlockContainsOrdered(
    stackCraftEncounterManagerSource,
    "private void HandleBeforeSave",
    [
      "gameData.TryGetScene(out var sceneData)",
      "sceneData.CompletedEncounters = this.completedEncounters;",
    ],
    "StackCraft EncounterManager 保存已完成遭遇集合");
  assertCsharpBlockContainsOrdered(
    stackCraftEncounterManagerSource,
    "public EncounterDefinition GetBestEncounter",
    [
      "int cardCount = CardManager.Instance.AllCards.Count();",
      "bool isFriendlyMode = GameDirector.Instance.GameData.GameplayPrefs.IsFriendlyMode;",
      "allEncounters",
      "e.IsValidForDay(day, completedEncounters, cardCount, isFriendlyMode)",
      "OrderByDescending(e => e.Priority)",
      "ThenBy(e => GetTypePriority(e.Type))",
      "return sortedCandidates.First();",
    ],
    "StackCraft EncounterManager 按候选有效性、优先级和类型优先级选择唯一遭遇");
  assertCsharpBlockContainsOrdered(
    stackCraftEncounterManagerSource,
    "public IEnumerator ExecuteEncounter",
    [
      "if (encounter == null) yield break;",
      "completedEncounters.Add(encounter.Id);",
      "InfoPanel.Instance?.RequestInfoDisplay(",
      "yield return new WaitForSecondsRealtime(2f);",
      "for (int i = 0; i < encounter.Count; i++)",
      "Vector3 spawnPos = GetRandomBoardPosition();",
      "CardManager.Instance.CreateCardInstance(",
      "encounter.CardToSpawn",
      "CardStack.RefuseAll",
      "Camera.main.transform.parent.TryGetComponent<CameraController>(out var cam)",
      "yield return cam.MoveTo(spawnPos);",
      "card.PlayPuffParticle();",
      "yield return new WaitForSecondsRealtime(0.5f);",
      "InfoPanel.Instance?.ClearInfoRequest(this);",
    ],
    "StackCraft EncounterManager 通知、生成卡牌、镜头移动、烟雾和清理表现来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftEncounterManagerSource,
    "private Vector3 GetRandomBoardPosition",
    [
      "if (Board.Instance == null) return Vector3.zero;",
      "Bounds b = Board.Instance.WorldBounds;",
      "Random.Range(b.min.x + spawnEdgePadding, b.max.x - spawnEdgePadding);",
      "Random.Range(b.min.z + spawnEdgePadding, b.max.z - spawnEdgePadding);",
      "float restrictedZ = b.max.z - Board.Instance.TopMargin;",
      "z = Mathf.Min(z, restrictedZ - 1f);",
      "return new Vector3(x, 0, z);",
    ],
    "StackCraft EncounterManager 按桌面边界、边距和顶部限制随机生成位置");
  assertCsharpBlockContainsOrdered(
    stackCraftEncounterManagerSource,
    "private int GetTypePriority",
    [
      "case EncounterType.SpecificDay: return 0;",
      "case EncounterType.Recurring: return 1;",
      "case EncounterType.Range: return 2;",
      "case EncounterType.MinimumDay: return 3;",
      "default: return 4;",
    ],
    "StackCraft EncounterManager 类型优先级来源");
}
if (scenarioDayCycleRulesSource != null) {
  assertCsharpBlockContainsOrdered(
    scenarioDayCycleRulesSource,
    "public sealed class ScenarioDayEncounterRule",
    [
      "private string m_key;",
      "private string m_notificationMessage;",
      "private ContentId m_cardId;",
      "private int m_count = 1;",
      "private bool m_oneTimeOnly;",
      "private int m_minimumDay = 1;",
      "private int m_maximumDay = int.MaxValue;",
      "private int m_interval;",
      "private int m_priority;",
      "private float m_chance = 1f;",
      "private int m_maxCardsOnTabletop = 100;",
    ],
    "ScenarioDayEncounterRule 接管 StackCraft EncounterDefinition 遭遇作者字段");
  assertCsharpBlockContainsOrdered(
    scenarioDayCycleRulesSource,
    "internal ScenarioDayEncounterRuleRuntime CreateRuntime()",
    [
      "m_key",
      "m_notificationMessage",
      "m_cardId",
      "m_count",
      "m_oneTimeOnly",
      "m_minimumDay",
      "m_maximumDay",
      "m_interval",
      "m_priority",
      "m_chance",
      "m_maxCardsOnTabletop",
    ],
    "ScenarioDayEncounterRule 生成运行时只读规则");
  assertCsharpDeclarationAndBlockContainsOrdered(
    scenarioDayCycleRulesSource,
    "internal readonly struct ScenarioDayEncounterRuleRuntime",
    [
      "internal int Specificity =>",
      "MinimumDay == MaximumDay",
      "? 0",
      ": Interval > 0",
      "? 1",
      ": MaximumDay < int.MaxValue",
      "? 2",
      ": 3",
    ],
    "ScenarioDayEncounterRuleRuntime 接管 StackCraft SpecificDay / Recurring / Range / MinimumDay 类型优先级");
  assertCsharpBlockContainsOrdered(
    scenarioDayCycleRulesSource,
    "internal bool MatchesDay",
    [
      "day >= MinimumDay && day <= MaximumDay",
      "(Interval == 0 || day % Interval == 0)",
    ],
    "ScenarioDayEncounterRuleRuntime 接管 StackCraft 日期匹配语义");
}
if (scenarioRunSource != null) {
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private ScenarioRun(",
    [
      "snapshot.CompletedDayEncounterKeys == null",
      "for (int i = 0; i < snapshot.CompletedDayEncounterKeys.Count; i++)",
      "string key = snapshot.CompletedDayEncounterKeys[i];",
      "Gameplay.Actions.ActionLocalKeyUtility.IsValidKey(key)",
      "m_completedDayEncounterKeys.Add(key)",
    ],
    "ScenarioRun 从快照恢复 StackCraft completedEncounters 一次性遭遇集合");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "public ScenarioRunSnapshot CreateSnapshot()",
    [
      "List<string> completedDayEncounterKeys = new List<string>(m_completedDayEncounterKeys);",
      "completedDayEncounterKeys.Sort(StringComparer.Ordinal);",
      "completedDayEncounterKeys.ToArray()",
    ],
    "ScenarioRun 保存 StackCraft completedEncounters 一次性遭遇集合");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void ResolveDayEncounter()",
    [
      "ScenarioDayEncounterRuleRuntime? selected = null;",
      "for (int i = 0; i < m_dayCycleRules.Encounters.Count; i++)",
      "!candidate.MatchesDay(m_dayCycle.EndingDay)",
      "candidate.OneTimeOnly && m_completedDayEncounterKeys.Contains(candidate.Key)",
      "IsBlockedByFriendlyMode(candidate)",
      "Tabletop.Cards.CardCount >= candidate.MaxCardsOnTabletop",
      "Tabletop.NextAuthoritativeFloat() > candidate.Chance",
      "IsHigherPriorityEncounter(candidate, selected.Value)",
      "Tabletop.Cards.EnsureCanCreateCards(encounter.Count);",
      "Tabletop.CreateCardAtAuthoritativeRandomPosition(encounter.CardId);",
      "m_dayCycle.RecordEncounter(encounter.CardId, encounter.Count, encounter.NotificationMessage);",
      "QuestLog.RecordFact(new CardsCreatedQuestTaskFact(createdCardIds));",
      "RefreshQuestState();",
      "m_completedDayEncounterKeys.Add(encounter.Key)",
      "TabletopPresentationCueKind.CameraFocus",
      "TabletopPresentationCueKind.CardSmoke",
    ],
    "ScenarioRun 接管 StackCraft GetBestEncounter / ExecuteEncounter 的候选过滤、权威随机、生成、任务事实和表现提示");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private static bool IsHigherPriorityEncounter",
    [
      "candidate.Priority != current.Priority",
      "return candidate.Priority > current.Priority;",
      "return candidate.Specificity < current.Specificity;",
    ],
    "ScenarioRun 接管 StackCraft Priority 降序和类型优先级升序选择");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private bool IsBlockedByFriendlyMode",
    [
      "if (!FriendlyMode)",
      "m_contentIndex.TryGet(encounter.CardId, out CardDefinition card)",
      "for (int i = 0; i < card.TagCodes.Count; i++)",
      "TagHelper.HasTag(card.TagCodes[i], XTag.Faction_Enemy)",
      "return true;",
    ],
    "ScenarioRun 用 EX-GAS 阵营标签接管 StackCraft IsAggressive 友好模式过滤");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "internal void ContinueDayCycle()",
    [
      "case ScenarioDayCyclePhase.AwaitingFeedingConfirmation:",
      "FeedCharacters();",
      "m_dayCycle.FinishFeeding(",
      "if (m_dayCycle.Phase == ScenarioDayCyclePhase.AwaitingNewDayConfirmation)",
      "ResolveDayEncounter();",
      "case ScenarioDayCyclePhase.AwaitingExcessCardResolution:",
      "FinishExcessCardResolution();",
    ],
    "ScenarioRun 在进食 / 超限完成后执行最多一个 StackCraft 日终遭遇");
}
if (scenarioDayCycleSource == null) {
  fail("缺少 ScenarioDayCycle，无法证明日终遭遇摘要由单局日终流程记录。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioDayCycleSource,
    "internal void RecordEncounter",
    [
      "RequirePhase(ScenarioDayCyclePhase.AwaitingNewDayConfirmation);",
      "if (!cardId.IsValid)",
      "if (count <= 0)",
      "if (EncounterResult.HasValue)",
      "EncounterResult = new ScenarioDayEncounterResult(cardId, count, notificationMessage);",
    ],
    "ScenarioDayCycle 记录 StackCraft Encounter 通知摘要且禁止同一天重复提交");
}
if (scenarioRunSnapshotSource == null) {
  fail("缺少 ScenarioRunSnapshot，无法证明一次性日终遭遇进入单局快照。");
} else {
  assertCsharpDeclarationAndBlockContainsOrdered(
    scenarioRunSnapshotSource,
    "public sealed class ScenarioRunSnapshot",
    [
      "private string[] m_completedDayEncounterKeys;",
      "public IReadOnlyList<string> CompletedDayEncounterKeys => m_completedDayEncounterKeys;",
      "string[] completedDayEncounterKeys,",
      "m_completedDayEncounterKeys = completedDayEncounterKeys ?? throw new ArgumentNullException(nameof(completedDayEncounterKeys));",
    ],
    "ScenarioRunSnapshot 保存 StackCraft completedEncounters 等价一次性遭遇事实");
}
if (scenarioRunEditModeTestsSource == null) {
  fail("缺少 ScenarioRunEditModeTests，无法证明 StackCraft Encounter 替代链有回归保护。");
} else {
  assertCsharpMethodsExist(
    scenarioRunEditModeTestsSource,
    [
      "DayCycle_ExecutesAtMostOneEligibleEncounterAndRemembersOneTimeCompletion",
      "DayCycle_CreatedEncounterCardsAdvanceCardCreationQuest",
      "DayCycle_FriendlyModeSkipsEnemyTaggedEncounterAndPersistsThroughSnapshot",
    ],
    "ScenarioRunEditModeTests 覆盖日终遭遇一次性、任务事实和友好模式过滤");
}
for (const [label, sourceText] of [
  ["ScenarioDayCycleRules", scenarioDayCycleRulesSource],
  ["ScenarioRun", scenarioRunSource],
  ["ScenarioDayCycle", scenarioDayCycleSource],
  ["ScenarioRunSnapshot", scenarioRunSnapshotSource],
  ["ScenarioRunEditModeTests", scenarioRunEditModeTestsSource],
]) {
  if (sourceText == null) continue;
  for (const obsoleteToken of [
    "CryingSnow.StackCraft",
    "EncounterManager",
    "EncounterDefinition",
    "EncounterType",
    "CardManager.Instance",
    "Board.Instance",
    "Camera.main",
    "Random.value",
    "Random.Range",
    "InfoPanel.Instance",
  ]) {
    if (sourceText.includes(obsoleteToken)) {
      fail(`${label} 仍保留 StackCraft 遭遇旧结构残留：${obsoleteToken}`);
    }
  }
}

if (foundationSceneMenuSource == null) {
  fail("缺少 FoundationTestSceneMenu，无法证明快速日终测试剧本没有把 StackCraft 默认规则值误写成场景配置。");
} else {
  if (stackCraftBaseCardLimit != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftBaseCardLimit",
      stackCraftBaseCardLimit,
      "FoundationTestSceneMenu StackCraft 默认卡牌上限常量");
  }
  if (stackCraftHungerPerCharacter != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftHungerPerCharacter",
      stackCraftHungerPerCharacter,
      "FoundationTestSceneMenu StackCraft 默认饥饿需求常量");
  }
  assertCsharpFieldInitializerEquals(
    foundationSceneMenuSource,
    "StackCraftReferenceDayDurationSeconds",
    "120f",
    "FoundationTestSceneMenu StackCraft 参考整天时长常量");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureTestDayCycleScenarioAsset()",
    [
      "RequireRelative(dayCycle, \"m_enabled\").boolValue = true;",
      "RequireRelative(dayCycle, \"m_hungerPerCharacter\").intValue = 1;",
      "RequireRelative(dayCycle, \"m_baseCardLimit\").intValue = 3;",
    ],
    "快速日终测试剧本使用 1 / 3 触发 StackCraft 日终流程，不冒充 Default_Card_Settings 的 2 / 24 默认值");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void WriteStackCraftParityScenarioStats(",
    [
      "RequireProperty(serializedScenario, \"m_turnsPerDay\").intValue = 1;",
      "RequireProperty(serializedScenario, \"m_secondsPerTurn\").floatValue =",
      "StackCraftReferenceDayDurationSeconds;",
      "RequireRelative(dayCycle, \"m_hungerPerCharacter\").intValue = StackCraftHungerPerCharacter;",
      "RequireRelative(dayCycle, \"m_baseCardLimit\").intValue = StackCraftBaseCardLimit;",
    ],
    "StackCraft 同态测试剧本使用参考 120 秒日长和 Default_Card_Settings 的 2 / 24 默认 HUD 统计");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureStackCraftParityQuestAsset()",
    [
      "FoundationTestSceneHarness.TestStackCraftParityQuestContentId",
      "\"打开初始卡包\"",
      "\"多次点击初始卡包，完全打开并取得里面的所有卡牌。\"",
      "RequireProperty(serializedQuest, \"m_journalGroupName\").stringValue = \"入门\";",
      "new ActionCompletionQuestTaskDefinition()",
      "FoundationTestSceneHarness.TestOpenCardPackActionContentId",
      "RequireRelative(task, \"m_requiredCompletionCount\").intValue = 1;",
    ],
    "StackCraft 同态任务作者源必须承接 Main 场景第一条 Open Starter Pack 任务");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureTestScenarioAssets()",
    [
      "EnsureStackCraftParityQuestAsset();",
      "TestStackCraftParityScenarioPath",
      "FoundationTestSceneHarness.TestStackCraftParityQuestContentId",
      "WriteStackCraftParityScenarioStats(TestStackCraftParityScenarioPath);",
    ],
    "StackCraft 同态剧本必须引用同态专用任务，不能继续复用普通地基测试任务");
}

if (testDayCycleScenarioText == null) {
  fail("缺少地基日终测试剧本资产，无法验证快速日终测试配置。");
} else {
  assertYamlScalarEquals(
    testDayCycleScenarioText,
    "m_hungerPerCharacter",
    "1",
    "地基日终测试剧本快速进食需求");
  assertYamlScalarEquals(
    testDayCycleScenarioText,
    "m_baseCardLimit",
    "3",
    "地基日终测试剧本快速超限上限");
}

if (testStackCraftParityScenarioText == null) {
  fail("缺少地基StackCraft同态测试剧本资产，无法验证 StackCraft 默认 HUD 统计配置。");
} else {
  assertStringArrayEquals(
    yamlUnityListScalarValues(
      testStackCraftParityScenarioText,
      "m_questIds",
      "m_value",
      "StackCraft 同态测试剧本任务列表"),
    ["test.foundation.quest.stackcraft-parity.open-starter-pack"],
    "StackCraft 同态测试剧本任务列表");
  if (stackCraftHungerPerCharacter != null) {
    assertYamlScalarEquals(
      testStackCraftParityScenarioText,
      "m_hungerPerCharacter",
      stackCraftHungerPerCharacter,
      "StackCraft 同态测试剧本默认进食需求");
  }
  if (stackCraftBaseCardLimit != null) {
    assertYamlScalarEquals(
      testStackCraftParityScenarioText,
      "m_baseCardLimit",
      stackCraftBaseCardLimit,
      "StackCraft 同态测试剧本默认卡牌上限");
  }
}

if (testStackCraftParityQuestText == null) {
  fail("缺少地基StackCraft同态打开初始卡包任务资产，无法验证 Main 场景初始任务 HUD 对齐。");
} else {
  assertYamlNestedScalarEquals(
    testStackCraftParityQuestText,
    "m_contentId",
    "m_value",
    "test.foundation.quest.stackcraft-parity.open-starter-pack",
    "StackCraft 同态打开初始卡包任务内容 ID");
  assertYamlScalarStringEquals(
    testStackCraftParityQuestText,
    "m_displayName",
    "打开初始卡包",
    "StackCraft 同态打开初始卡包任务标题");
  assertYamlScalarStringEquals(
    testStackCraftParityQuestText,
    "m_journalGroupName",
    "入门",
    "StackCraft 同态打开初始卡包任务分组");
  assertYamlScalarStringEquals(
    testStackCraftParityQuestText,
    "m_description",
    "多次点击初始卡包，完全打开并取得里面的所有卡牌。",
    "StackCraft 同态打开初始卡包任务描述");
  const parityQuestTask = unitySerializeReferenceBlockByType(
    testStackCraftParityQuestText,
    "ActionCompletionQuestTaskDefinition",
    "Gameplay.Quests",
    "Gameplay.Runtime",
    "StackCraft 同态打开初始卡包任务子项");
  if (parityQuestTask != null) {
    assertYamlNestedScalarEquals(
      parityQuestTask.text,
      "m_actionId",
      "m_value",
      "test.foundation.pack.open",
      "StackCraft 同态打开初始卡包任务行动完成子项");
    assertYamlScalarEquals(
      parityQuestTask.text,
      "m_requiredCompletionCount",
      "1",
      "StackCraft 同态打开初始卡包任务行动完成子项");
  }
}

const currencyCardQuerySource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Actions/CurrencyCardQuery.cs");
if (currencyCardQuerySource == null) {
 fail("缺少统一货币卡查询入口，卡包付款可能回退为任意普通卡都能付款。");
} else {
  assertCsharpBlockContainsOrdered(
    currencyCardQuerySource,
    "internal static class CurrencyCardQuery",
    [
      "internal static bool IsCurrencyCard",
      "internal static HashSet<ContentId> BuildCurrencyCardIds",
      "private static bool DeclaresCurrencyCard",
      "private static void AddCurrencyCardIds",
    ],
    "统一货币卡查询入口类结构");
  assertCsharpBlockContainsOrdered(
    currencyCardQuerySource,
    "internal static bool IsCurrencyCard",
    [
      "contentIndex == null",
      "throw new ArgumentNullException(nameof(contentIndex));",
      "!contentId.IsValid",
      "IReadOnlyList<ContentAsset> assets = contentIndex.AllAssets;",
      "DeclaresCurrencyCard(assets[i], contentId)",
      "return true;",
      "return false;",
    ],
    "统一货币卡查询判断当前内容是否为 StackCraft 货币卡方法");
  assertCsharpBlockContainsOrdered(
    currencyCardQuerySource,
    "internal static HashSet<ContentId> BuildCurrencyCardIds",
    [
      "contentIndex == null",
      "HashSet<ContentId> currencyCardIds = new HashSet<ContentId>();",
      "IReadOnlyList<ContentAsset> assets = contentIndex.AllAssets;",
      "AddCurrencyCardIds(assets[i], currencyCardIds);",
      "return currencyCardIds;",
    ],
    "统一货币卡查询构建 StackCraft 货币集合方法");
  assertCsharpBlockContainsOrdered(
    currencyCardQuerySource,
    "private static bool DeclaresCurrencyCard",
    [
      "case CardBuyerDefinition buyer:",
      "return buyer.CurrencyCardId == contentId;",
      "case ChestCardDefinition chest:",
      "return chest.CurrencyCardId == contentId;",
      "case ActionDefinition action:",
      "ContainsCurrencyCardId(action.ResultIntents, contentId)",
      "ContainsCurrencyCardId(action.ResultBranches, contentId)",
    ],
    "统一货币卡查询从收购点、箱子和出售结果推导货币方法");
  assertCsharpBlockContainsOrdered(
    currencyCardQuerySource,
    "private static void AddCurrencyCardIds(ContentAsset asset",
    [
      "case CardBuyerDefinition buyer:",
      "AddCurrencyCardId(currencyCardIds, buyer.CurrencyCardId);",
      "case ChestCardDefinition chest:",
      "AddCurrencyCardId(currencyCardIds, chest.CurrencyCardId);",
      "case ActionDefinition action:",
      "AddCurrencyCardIds(action.ResultIntents, currencyCardIds);",
      "IReadOnlyList<ActionResultBranchDefinition> branches = action.ResultBranches;",
      "AddCurrencyCardIds(branches[branchIndex].ResultIntents, currencyCardIds);",
    ],
    "统一货币卡查询收集 StackCraft 货币集合方法");
  assertCsharpBlockContainsOrdered(
    currencyCardQuerySource,
    "private static bool ContainsCurrencyCardId(",
    [
      "resultIntents[intentIndex] is SellCardsResultIntent sellIntent",
      "sellIntent.CurrencyCardId == contentId",
      "return true;",
      "return false;",
    ],
    "统一货币卡查询从出售结果意图识别货币方法");
  assertCsharpBlockContainsOrdered(
    currencyCardQuerySource,
    "private static void AddCurrencyCardId(ISet<ContentId> currencyCardIds",
    [
      "if (contentId.IsValid)",
      "currencyCardIds.Add(contentId);",
    ],
    "统一货币卡查询加入有效货币 ID 方法");
}

const actionConditionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Actions/ActionCondition.cs");
if (actionConditionSource == null) {
	fail("缺少行动条件源码，无法证明卡包付款候选按 StackCraft 货币 / 有币箱子语义过滤。");
} else {
	assertCsharpBlockContainsOrdered(
		actionConditionSource,
		"public sealed class CardPaymentSourceAvailableCondition",
		[
			"ActionSlotBinding binding = context.GetBinding(PaymentSlotKey);",
			"binding.CardIds.Count == 0",
			"for (int i = 0; i < binding.CardIds.Count; i++)",
			"context.Cards.TryGetCard(binding.CardIds[i], out TabletopCard card)",
			"card is ChestCard chest && chest.StoredCurrencyCount <= 0",
			"card is not ChestCard",
			"!CurrencyCardQuery.IsCurrencyCard(context.Content, card.ContentId)",
			"return true;",
			"context.ValidateSlotReference(PaymentSlotKey, \"ACTION_CONDITION_PAYMENT_SLOT_UNKNOWN\");",
		],
		"卡包付款候选条件按 StackCraft 货币 / 有币箱子语义过滤");
	assertCsharpBlockContainsOrdered(
		actionConditionSource,
		"public sealed class CardSaleSourceAvailableCondition",
		[
			"ActionSlotBinding binding = context.GetBinding(SoldSlotKey);",
			"binding.CardIds.Count == 0",
			"for (int i = 0; i < binding.CardIds.Count; i++)",
			"context.Cards.TryGetCard(binding.CardIds[i], out TabletopCard card)",
			"context.Content.TryGet(card.ContentId, out CardDefinition definition)",
			"行动 {context.Action.ContentId} 的出售槽位引用了非卡牌内容",
			"card is ChestCard chest && chest.StoredCurrencyCount > 0",
			"definition.SellValue <= 0",
			"return true;",
			"context.ValidateSlotReference(SoldSlotKey, \"ACTION_CONDITION_SELL_SLOT_UNKNOWN\");",
		],
		"出售候选条件按 StackCraft 可售卡 / 空箱语义过滤");
	if (actionConditionSource.includes("普通卡算 1 单位")) {
		fail("付款候选条件仍描述普通卡可当作 1 单位付款，偏离 StackCraft 货币卡 / 有币箱子语义。");
	}
}

const stackCraftGrowerDefinitionSource = readIfExists("Assets/StackCraft/Scripts/Card/Definitions/GrowerDefinition.cs");
const stackCraftResearchDefinitionSource = readIfExists("Assets/StackCraft/Scripts/Card/Definitions/ResearchDefinition.cs");
const stackCraftRecipeDefinitionSource = readIfExists("Assets/StackCraft/Scripts/Crafting/Definitions/RecipeDefinition.cs");
const stackCraftGrowthRecipeSource = readIfExists("Assets/StackCraft/Scripts/Crafting/Definitions/GrowthRecipe.cs");
const stackCraftExplorationRecipeSource = readIfExists("Assets/StackCraft/Scripts/Crafting/Definitions/ExplorationRecipe.cs");
const stackCraftResearchRecipeSource = readIfExists("Assets/StackCraft/Scripts/Crafting/Definitions/ResearchRecipe.cs");
const stackCraftTravelRecipeSource = readIfExists("Assets/StackCraft/Scripts/Crafting/Definitions/TravelRecipe.cs");
const stackCraftCraftingManagerSource = readIfExists("Assets/StackCraft/Scripts/Crafting/CraftingManager.cs");
const stackCraftCraftingTaskSource = readIfExists("Assets/StackCraft/Scripts/Crafting/CraftingTask.cs");

if (stackCraftGrowerDefinitionSource == null) {
	fail("缺少 StackCraft GrowerDefinition 源码，无法证明种植器空类型标记来源。");
} else {
	assertSourceContainsOrdered(
		stackCraftGrowerDefinitionSource,
		[
			"public class GrowerDefinition : CardDefinition",
			"// Intentionally empty.",
			"to identify the card's behavior.",
		],
		"StackCraft GrowerDefinition 空类型标记来源");
}
if (stackCraftResearchDefinitionSource == null) {
	fail("缺少 StackCraft ResearchDefinition 源码，无法证明研究卡空类型标记来源。");
} else {
	assertSourceContainsOrdered(
		stackCraftResearchDefinitionSource,
		[
			"public class ResearchDefinition : CardDefinition",
			"// Intentionally empty.",
			"to identify the card's behavior.",
		],
		"StackCraft ResearchDefinition 空类型标记来源");
}
if (stackCraftRecipeDefinitionSource == null) {
	fail("缺少 StackCraft RecipeDefinition 源码，无法证明配方基类字段、消耗模式和直接副作用来源。");
} else {
	assertSourceContainsOrdered(
		stackCraftRecipeDefinitionSource,
		[
			"public class RecipeDefinition : ScriptableObject",
			"public struct Ingredient",
			"public CardDefinition card;",
			"public int count;",
			"public IngredientConsumption consumptionMode;",
			"protected string id;",
			"protected RecipeCategory category;",
			"protected string displayName;",
			"protected List<Ingredient> requiredIngredients;",
			"protected CardDefinition resultingCard;",
			"protected bool isContinuous = false;",
			"protected bool allowExcessIngredients = false;",
			"protected float craftingDuration = 5f;",
			"protected float randomWeight = 1.0f;",
		],
		"StackCraft RecipeDefinition 配方作者字段来源结构");
	assertCsharpBlockContainsOrdered(
		stackCraftRecipeDefinitionSource,
		"public virtual void Execute",
		[
			"var rules = GetIngredientRules();",
			"stack.TopCard.PlayPuffParticle();",
			"AudioManager.Instance?.PlaySFX(AudioId.Pop);",
			"ConsumeIngredients(stack, rules);",
			"CardManager.Instance?.CreateCardInstance(",
			"CraftingManager.Instance?.NotifyCraftingFinished(resultingCard);",
		],
		"StackCraft RecipeDefinition.Execute 直接消费材料、生成卡牌和通知制作来源链");
	assertCsharpBlockContainsOrdered(
		stackCraftRecipeDefinitionSource,
		"protected void ApplyConsumptionRule",
		[
			"case IngredientConsumption.Keep:",
			"case IngredientConsumption.Consume:",
			"card.Use();",
			"if (card.UsesLeft <= 0) stack.DestroyCard(card);",
			"case IngredientConsumption.Destroy:",
			"stack.DestroyCard(card);",
		],
		"StackCraft IngredientConsumption 保留、使用次数消耗和直接销毁来源链");
}
if (stackCraftGrowthRecipeSource == null) {
	fail("缺少 StackCraft GrowthRecipe 源码，无法证明种植特殊配方来源。");
} else {
	assertCsharpBlockContainsOrdered(
		stackCraftGrowthRecipeSource,
		"public override void Execute",
		[
			"c.Definition is GrowerDefinition",
			"c.Definition == this.ResultingCard",
			"ApplyConsumptionRule(growerInstance, rule.consumptionMode, stack);",
			"var newSeedStack = stack.SplitAt(seedInstance);",
			"CardManager.Instance.RegisterStack(newSeedStack);",
			"newSeedStack.ApplyTranslation(new Vector3(seedInstance.Size.x, 0, 0));",
			"CardManager.Instance?.ResolveOverlaps();",
			"CardManager.Instance?.CreateCardInstance(ResultingCard, stack.TargetPosition.Flatten(), stack);",
		],
		"StackCraft GrowthRecipe 保留种子、消耗种植器并生成结果来源链");
}
if (stackCraftExplorationRecipeSource == null) {
	fail("缺少 StackCraft ExplorationRecipe 源码，无法证明探索特殊配方来源。");
} else {
	assertCsharpBlockContainsOrdered(
		stackCraftExplorationRecipeSource,
		"public override void Execute",
		[
			"c.Definition.Category == CardCategory.Area",
			"c.Definition.GetRandomLoot() != null",
			"var loot = areaCard.Definition.GetRandomLoot();",
			"CardManager.Instance?.CreateCardInstance(loot, stack.TargetPosition.Flatten(), stack);",
			"CraftingManager.Instance?.NotifyExplorationFinished(areaCard.Definition);",
			"ConsumeIngredients(stack, rules);",
		],
		"StackCraft ExplorationRecipe 地点随机战利品、探索事实和参与者消耗来源链");
}
if (stackCraftResearchRecipeSource == null) {
	fail("缺少 StackCraft ResearchRecipe 源码，无法证明研究随机解锁来源。");
} else {
	assertCsharpBlockContainsOrdered(
		stackCraftResearchRecipeSource,
		"public override void Execute",
		[
			"ConsumeIngredients(stack, rules);",
			"var manager = CraftingManager.Instance;",
			"manager.AllRecipes",
			"!manager.IsRecipeDiscovered(r.Id)",
			"r is not ResearchRecipe",
			"r is not GrowthRecipe",
			"r is not ExplorationRecipe",
			"r.ResultingCard != null",
			"var randomRecipe = undiscovered[Random.Range(0, undiscovered.Count)];",
			"CardManager.Instance.SpawnRecipeCard(randomRecipe, stack);",
			"manager.MarkRecipeAsDiscovered(randomRecipe);",
		],
		"StackCraft ResearchRecipe 未发现普通配方随机解锁来源链");
}
if (stackCraftTravelRecipeSource == null) {
	fail("缺少 StackCraft TravelRecipe 源码，无法证明旅行特殊配方来源。");
} else {
	assertSourceContainsOrdered(
		stackCraftTravelRecipeSource,
		[
			"public class TravelRecipe : RecipeDefinition",
			"[SerializeField] private List<string> targetScenes;",
		],
		"StackCraft TravelRecipe 目标场景作者字段来源结构");
	assertCsharpBlockContainsOrdered(
		stackCraftTravelRecipeSource,
		"public override void Execute",
		[
			"List<CardInstance> travelers = stack.Cards.ToList();",
			"ConsumeIngredients(stack, rules);",
			"GameDirector.Instance?.InitiateTravel(targetScenes, travelers);",
		],
		"StackCraft TravelRecipe 通过配方消费后请求 GameDirector 旅行来源链");
}
if (stackCraftCraftingTaskSource == null) {
	fail("缺少 StackCraft CraftingTask 源码，无法证明制作进度、暂停、取消和完成状态来源。");
} else {
	assertSourceContainsOrdered(
		stackCraftCraftingTaskSource,
		[
			"public class CraftingTask",
			"public RecipeDefinition Recipe { get; private set; }",
			"public CardStack TargetStack { get; private set; }",
			"public float Progress { get; private set; }",
			"public bool IsCanceled { get; private set; }",
			"public bool IsPaused { get; private set; }",
			"public bool IsComplete => Progress >= Recipe.CraftingDuration;",
		],
		"StackCraft CraftingTask 运行状态结构");
	assertCsharpBlockContainsOrdered(
		stackCraftCraftingTaskSource,
		"public void UpdateProgress",
		[
			"!IsComplete && !IsCanceled && !IsPaused",
			"Progress += deltaTime;",
		],
		"StackCraft CraftingTask 按秒推进且暂停 / 取消 / 完成时不推进");
	assertSourceContainsOrdered(
		stackCraftCraftingTaskSource,
		[
			"public void SetProgress(float value)",
			"Progress = value;",
			"public void Cancel()",
			"IsCanceled = true;",
			"public void Pause()",
			"IsPaused = true;",
			"public void Resume()",
			"IsPaused = false;",
			"public void Complete()",
			"Progress = Recipe.CraftingDuration;",
		],
		"StackCraft CraftingTask 进度恢复、暂停、恢复、取消和强制完成来源链");
}
if (stackCraftCraftingManagerSource == null) {
	fail("缺少 StackCraft CraftingManager 源码，无法证明制作运行链来源。");
} else {
	assertSourceContainsOrdered(
		stackCraftCraftingManagerSource,
		[
			"public static CraftingManager Instance { get; private set; }",
			"public event System.Action<string> OnRecipeDiscovered;",
			"public event System.Action<CardDefinition> OnCraftingFinished;",
			"public event System.Action<CardDefinition> OnExplorationFinished;",
			"private ProgressUI progressUIPrefab;",
			"public List<RecipeDefinition> AllRecipes { get; private set; } = new();",
			"public HashSet<string> DiscoveredRecipes { get; private set; } = new();",
			"private readonly List<CraftingTask> activeCraftingTasks = new();",
			"private readonly Dictionary<CraftingTask, ProgressUI> activeCraftingUIs = new();",
		],
		"StackCraft CraftingManager 单例、发现集合、活动制作和进度 UI 来源结构");
	assertSourceContainsOrdered(
		stackCraftCraftingManagerSource,
		[
			"AllRecipes = Resources.LoadAll<RecipeDefinition>(\"Recipes\").ToList();",
			"GameDirector.Instance.OnBeforeSave += HandleBeforeSave;",
			"DiscoveredRecipes.UnionWith(gameData.DiscoveredRecipes);",
		],
		"StackCraft CraftingManager Resources 配方扫描、发现集合和存档写回来源链");
	assertCsharpBlockContainsOrdered(
		stackCraftCraftingManagerSource,
		"void Update()",
		[
			"for (int i = activeCraftingTasks.Count - 1; i >= 0; i--)",
			"!task.IsCanceled",
			"task.UpdateProgress(Time.deltaTime);",
			"ui.UpdateUI(task);",
			"task.IsCanceled || task.IsComplete",
			"PerformCraftingAction(task);",
			"Destroy(activeCraftingUIs[task].gameObject);",
			"activeCraftingTasks.RemoveAt(i);",
		],
		"StackCraft CraftingManager 每帧推进活动制作、刷新 UI 并在完成 / 取消时清理");
	assertCsharpBlockContainsOrdered(
		stackCraftCraftingManagerSource,
		"public void CheckForRecipe",
		[
			"AllRecipes",
			"DoesStackMatchRecipe(stack, recipe)",
			"float totalWeight = matchingRecipes.Sum(recipe => recipe.RandomWeight);",
			"Random.Range(0, matchingRecipes.Count)",
			"float randomRoll = Random.Range(0f, totalWeight);",
			"randomRoll -= recipe.RandomWeight;",
			"chosenRecipe = matchingRecipes.Last();",
			"StartCraftingTask(stack, chosenRecipe);",
		],
		"StackCraft CraftingManager 自动匹配整堆配方并按 RandomWeight 选择制作");
	assertCsharpBlockContainsOrdered(
		stackCraftCraftingManagerSource,
		"private bool DoesStackMatchRecipe",
		[
			"GroupBy(c => c.BaseDefinition)",
			"recipe.RequiredIngredients",
			"recipe.AllowExcessIngredients || ingredient.card.Category == CardCategory.Resource",
			"countInStack < ingredient.count",
			"countInStack != ingredient.count",
			"!recipe.AllowExcessIngredients",
			"stackComposition.Keys.Any(cardDef => !recipeIngredientSet.Contains(cardDef))",
		],
		"StackCraft CraftingManager 配方材料匹配、资源宽松数量和额外材料规则");
	assertCsharpBlockContainsOrdered(
		stackCraftCraftingManagerSource,
		"private void StartCraftingTask",
		[
			"var newTask = new CraftingTask(recipe, stack);",
			"activeCraftingTasks.Add(newTask);",
			"stack.SetCraftingState(true);",
			"Instantiate(",
			"progressUIPrefab",
			"WorldCanvas.Instance?.transform",
			"activeCraftingUIs.Add(newTask, newUI);",
			"DiscoveredRecipes.Add(recipe.Id);",
			"OnRecipeDiscovered?.Invoke(recipe.Id);",
		],
		"StackCraft CraftingManager 启动制作、锁定堆、实例化进度 UI 并标记发现配方");
	assertCsharpBlockContainsOrdered(
		stackCraftCraftingManagerSource,
		"private void PerformCraftingAction",
		[
			"recipe.Execute(stack);",
			"stack.SetCraftingState(false);",
			"CardManager.Instance?.NotifyStatsChanged();",
			"recipe.ResultingCard.Category == CardCategory.Character",
			"recipe.ResultingCard.Category == CardCategory.Mob",
			"if (recipe.IsContinuous)",
			"recipe.HasConsumableIngredients() && stack.Cards.Count > 0",
			"CheckForRecipe(stack);",
		],
		"StackCraft CraftingManager 完成制作、通知统计、活物停止和连续 / 消耗后重复制作");
	assertCsharpBlockContainsOrdered(
		stackCraftCraftingManagerSource,
		"public void RestoreCraftingTask",
		[
			"RecipeDefinition recipe = GetRecipeById(recipeId);",
			"StartCraftingTask(stack, recipe);",
			"task.SetProgress(progress);",
			"ui.UpdateUI(task);",
		],
		"StackCraft CraftingManager 从存档恢复活动制作进度和 UI");
	assertCsharpBlockContainsOrdered(
		stackCraftCraftingManagerSource,
		"public bool CanJoinActiveCraft",
		[
			"var task = GetCraftingTask(targetStack);",
			"task.Recipe.AllowExcessIngredients",
			"task.Recipe.RequiredIngredients.Any(i => i.card == incomingCard)",
			"return isIngredient",
		],
		"StackCraft CraftingManager 允许额外材料加入活动制作堆来源链");
	assertCsharpBlockContainsOrdered(
		stackCraftCraftingManagerSource,
		"public void ValidateAndResumeTask",
		[
			"var task = GetCraftingTask(stack);",
			"DoesStackMatchRecipe(stack, task.Recipe)",
			"task.Resume();",
			"StopCraftingTask(stack);",
		],
		"StackCraft CraftingManager 移除卡牌后复核制作并恢复或停止来源链");
}

const stackCraftPackEntrySource = readIfExists("Assets/StackCraft/Scripts/Pack/PackEntry.cs");
const stackCraftPackSlotSource = readIfExists("Assets/StackCraft/Scripts/Pack/PackSlot.cs");
const stackCraftPackInstanceSource = readIfExists("Assets/StackCraft/Scripts/Pack/PackInstance.cs");
const stackCraftPackDefinitionSource = readIfExists("Assets/StackCraft/Scripts/Pack/PackDefinition.cs");
if (stackCraftPackEntrySource == null) {
	fail("缺少 StackCraft PackEntry 源码，无法证明卡包加权条目来源。");
} else {
	assertSourceContainsOrdered(
		stackCraftPackEntrySource,
		[
			"public class PackEntry",
			"public CardDefinition Card;",
			"[Tooltip(\"Higher = More Likely\")]",
			"public int Weight = 1;",
		],
		"StackCraft PackEntry 加权卡牌条目来源结构");
}
if (stackCraftPackSlotSource == null) {
	fail("缺少 StackCraft PackSlot 源码，无法证明卡包槽位抽取、配方优先和普通卡池回退来源。");
} else {
	assertSourceContainsOrdered(
		stackCraftPackSlotSource,
		[
			"public List<PackEntry> Entries;",
			"public List<RecipeDefinition> PossibleRecipes;",
			"public float RecipeChance = 0.1f;",
		],
		"StackCraft PackSlot 槽位作者字段来源结构");
	assertCsharpBlockContainsOrdered(
		stackCraftPackSlotSource,
		"public CardDefinition GetRandomCard()",
		[
			"PossibleRecipes != null && PossibleRecipes.Count > 0 && Random.value < RecipeChance",
			"var undiscoveredRecipes = PossibleRecipes",
			"!CraftingManager.Instance.IsRecipeDiscovered(recipe.Id)",
			"if (undiscoveredRecipes.Count > 0)",
			"int index = Random.Range(0, undiscoveredRecipes.Count);",
			"CraftingManager.Instance.MarkRecipeAsDiscovered(undiscoveredRecipe);",
			"return CardManager.Instance?.CreateRecipeCardDefinition(undiscoveredRecipe);",
			"if (Entries == null || Entries.Count == 0)",
			"int totalWeight = 0;",
			"totalWeight += Mathf.Max(1, entry.Weight);",
			"int roll = Random.Range(0, totalWeight);",
			"return entry.Card;",
		],
		"StackCraft PackSlot.GetRandomCard 配方优先、未发现过滤和加权普通卡回退来源链");
}
if (stackCraftPackInstanceSource == null) {
	fail("缺少 StackCraft PackInstance 源码，无法证明卡包点击逐槽打开和用尽移除来源链。");
} else {
	assertCsharpBlockContainsOrdered(
		stackCraftPackInstanceSource,
		"public bool OnClick",
		[
			"if (Stack != null) Stack.IsLocked = true;",
			"PullFromNextSlot();",
			"Vector3 groundPos = Stack.TargetPosition.Flatten();",
			"Stack.SetTargetPosition(groundPos);",
			"Stack.IsLocked = false;",
			"return true;",
		],
		"StackCraft PackInstance.OnClick 点击锁堆、抽槽和复位来源链");
	assertCsharpBlockContainsOrdered(
		stackCraftPackInstanceSource,
		"private void PullFromNextSlot()",
		[
			"if (UsesLeft <= 0)",
			"var slot = Definition.Slots[Definition.Slots.Count - UsesLeft];",
			"var cardDefinition = slot.GetRandomCard();",
			"CardManager.Instance?.CreateCardInstance(cardDefinition, Stack.TargetPosition + Vector3.up * 0.1f);",
			"Use();",
			"if (UsesLeft <= 0)",
			"Kill();",
		],
		"StackCraft PackInstance.PullFromNextSlot 按剩余次数逐槽打开并用尽移除来源链");
}
if (stackCraftPackDefinitionSource == null) {
	fail("缺少 StackCraft PackDefinition 源码，无法证明卡包售价、任务门槛和槽位作者源。");
} else {
	assertCsharpBlockContainsOrdered(
		stackCraftPackDefinitionSource,
		"public class PackDefinition : CardDefinition",
		[
			"private int buyPrice = 3;",
			"private int minQuests = 3;",
			"private List<PackSlot> slots;",
			"public int BuyPrice => buyPrice;",
			"public int MinQuests => minQuests;",
			"public List<PackSlot> Slots => slots;",
		],
		"StackCraft PackDefinition 售价、任务门槛和槽位作者源结构");
}

const cardPackDefinitionForPackFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/CardPackDefinition.cs");
if (cardPackDefinitionForPackFlowSource == null) {
	fail("缺少 CardPackDefinition，无法证明 StackCraft 卡包槽位结构已由当前 Gameplay 内容作者源接管。");
} else {
	assertCsharpBlockContainsOrdered(
		cardPackDefinitionForPackFlowSource,
		"public sealed class CardPackEntry",
		[
			"private ContentId m_cardId;",
			"private int m_weight = 1;",
			"public ContentId CardId => m_cardId;",
			"public int Weight => m_weight;",
		],
		"CardPackEntry 接管 StackCraft PackEntry 卡牌 ID 与权重字段");
	assertCsharpBlockContainsOrdered(
		cardPackDefinitionForPackFlowSource,
		"public sealed class CardPackRecipeEntry",
		[
			"private ContentId m_actionId;",
			"private ContentId m_recipeCardId;",
			"public ContentId ActionId => m_actionId;",
			"public ContentId RecipeCardId => m_recipeCardId;",
		],
		"CardPackRecipeEntry 接管 StackCraft PossibleRecipes 并显式绑定配方卡");
	assertCsharpBlockContainsOrdered(
		cardPackDefinitionForPackFlowSource,
		"public sealed class CardPackSlotDefinition",
		[
			"private CardPackEntry[] m_entries = Array.Empty<CardPackEntry>();",
			"private CardPackRecipeEntry[] m_recipeEntries = Array.Empty<CardPackRecipeEntry>();",
			"private float m_recipeChance;",
			"public IReadOnlyList<CardPackEntry> Entries =>",
			"public IReadOnlyList<CardPackRecipeEntry> RecipeEntries =>",
			"public float RecipeChance => m_recipeChance;",
		],
		"CardPackSlotDefinition 接管 StackCraft PackSlot 普通卡池、配方池和配方概率");
	assertCsharpBlockContainsOrdered(
		cardPackDefinitionForPackFlowSource,
		"public class CardPackDefinition : CardDefinition",
		[
			"private CardPackSlotDefinition[] m_slots = Array.Empty<CardPackSlotDefinition>();",
			"public IReadOnlyList<CardPackSlotDefinition> Slots =>",
			"public override int InitialUses => Slots.Count;",
			"protected override bool HasDerivedInitialUses => true;",
			"public override bool CountsTowardCardLimit => false;",
			"protected override bool HasDerivedCardLimitCounting => true;",
		],
		"CardPackDefinition 用槽位数量接管 StackCraft PackInstance UsesLeft 逐槽打开语义");
	assertCsharpBlockContainsOrdered(
		cardPackDefinitionForPackFlowSource,
		"private void ValidateSlot",
		[
			"slot.RecipeChance < 0f || slot.RecipeChance > 1f",
			"CARD_PACK_RECIPE_CHANCE_INVALID",
			"slot.Entries.Count == 0",
			"CARD_PACK_ENTRIES_EMPTY",
			"entry == null || entry.Weight <= 0 || !context.TryGet(entry.CardId, out CardDefinition _)",
			"CARD_PACK_ENTRY_INVALID",
			"HashSet<ContentId> recipeActionIds = new HashSet<ContentId>();",
			"!context.TryGet(recipe.ActionId, out ActionDefinition _)",
			"!context.TryGet(recipe.RecipeCardId, out CardDefinition _)",
			"!recipeActionIds.Add(recipe.ActionId)",
			"CARD_PACK_RECIPE_ENTRY_INVALID",
			"slot.RecipeChance > 0f && slot.RecipeEntries.Count == 0",
			"CARD_PACK_RECIPE_POOL_EMPTY",
		],
		"CardPackDefinition 校验 StackCraft 卡包槽位普通卡池、配方池和概率作者源");
}

const actionResultIntentsForPackFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Actions/ActionResultIntents.cs");
if (actionResultIntentsForPackFlowSource == null) {
	fail("缺少 ActionResultIntents，无法证明打开卡包不是第二套 PackInstance 点击脚本。");
} else {
	assertCsharpBlockContainsOrdered(
		actionResultIntentsForPackFlowSource,
		"public sealed class OpenCardPackResultIntent",
		[
			"private string m_packSlotKey;",
			"public string PackSlotKey => m_packSlotKey ?? string.Empty;",
			"protected override void ValidateResult",
			"context.ValidateSlotReference(PackSlotKey, \"ACTION_RESULT_PACK_SLOT_UNKNOWN\");",
			"context.Action.TurnCost != 0",
			"ACTION_RESULT_PACK_MUST_BE_IMMEDIATE",
			"context.Action.ParticipationSlots.Count == 1",
			"packSlot.MinimumParticipants != 1 || packSlot.MaximumParticipants != 1",
			"ACTION_RESULT_PACK_PARTICIPANT_COUNT_INVALID",
		],
		"OpenCardPackResultIntent 把 StackCraft PackInstance 点击语义声明为正式即时行动结果");
}

const actionResultSettlementSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Actions/ActionResultSettlement.cs");
if (actionResultSettlementSource == null) {
  fail("缺少行动结算源码，无法证明卡包购买不会吞掉非货币普通卡。");
} else {
  assertCsharpBlockContainsOrdered(
    actionResultSettlementSource,
    "private static void AddPackPurchase",
    [
      "string vendorSlotKey = ResolveResultSlotKey(action, intent.VendorSlotKey, \"卡包商贩槽位\");",
      "string paymentSlotKey = ResolveResultSlotKey(action, intent.PaymentSlotKey, \"卡包付款槽位\");",
      "vendorBinding.CardIds.Count != 1 || paymentBinding.CardIds.Count == 0",
      "card is not PackVendorCard vendorCard",
      "contentIndex.TryGet(card.ContentId, out PackVendorDefinition vendorDefinition)",
      "int remainingPrice = vendorCard.RemainingPrice;",
      "paymentBinding.CardIds.Count && paymentAmount < remainingPrice",
      "paymentCard is ChestCard chest",
      "AddChestCurrencyChange(chestCurrencyChanges, chest, -amountFromChest);",
      "!CurrencyCardQuery.IsCurrencyCard(contentIndex, paymentCard.ContentId)",
      "不是当前内容集合声明的货币卡",
      "removals.Add(paymentCardId);",
      "paymentAmount++;",
      "paymentAmount <= 0",
      "bool completesPurchase = paymentAmount == vendorCard.RemainingPrice;",
      "packPurchases.Add(new PackPurchaseSpec(",
      "vendorDefinition.OfferedPackId",
      "creations.Add(new CardCreationSpec(",
      "positionOffset: vendorDefinition.PackSpawnOffset",
    ],
    "卡包购买结算按 StackCraft 货币 / 有币箱子付款和卡包生成语义处理方法");
  assertCsharpBlockContainsOrdered(
    actionResultSettlementSource,
    "private static void RequirePackPurchaseCanCommit",
    [
      "vendor.PaidAmount != purchase.ExpectedPaidAmount",
      "vendor.RemainingPrice < purchase.PaymentAmount",
      "bool completes = vendor.RemainingPrice == purchase.PaymentAmount;",
      "completes != purchase.CompletesPurchase",
      "tabletop.ContentIndex.TryGet(purchase.PackId, out CardPackDefinition _)",
    ],
    "卡包购买提交前复核 StackCraft 商贩付款冻结计划方法");
	assertCsharpBlockContainsOrdered(
		actionResultSettlementSource,
		"private static void AddCardPackDraw",
		[
			"string packSlotKey = ResolveResultSlotKey(action, intent.PackSlotKey, \"卡包槽位\");",
			"ActionSlotBinding binding = FindBinding(action.ContentId, bindings, packSlotKey);",
			"binding.CardIds.Count != 1",
			"TabletopCardId packCardId = binding.CardIds[0];",
			"int slotIndex = packDefinition.Slots.Count - packCard.RemainingUses;",
			"uses.Add(packCardId);",
			"slot.RecipeEntries.Count > 0",
			"authoritativeRandom.NextFloat() < slot.RecipeChance",
			"List<CardPackRecipeEntry> availableRecipes = new List<CardPackRecipeEntry>();",
			"recipe != null && !isContentDiscovered(recipe.ActionId)",
			"CardPackRecipeEntry selected =",
			"availableRecipes[authoritativeRandom.NextInt(availableRecipes.Count)];",
			"researchDiscoveries.Add(new ResearchDiscoverySpec(",
			"return;",
			"int totalWeight = 0;",
			"totalWeight = checked(totalWeight + entry.Weight);",
			"int roll = authoritativeRandom.NextInt(totalWeight);",
			"creations.Add(new CardCreationSpec(entry.CardId, 1, packCardId));",
		],
		"打开卡包结算按 StackCraft PackSlot 配方优先、未发现过滤、普通权重回退和逐槽使用语义处理方法");
	assertCsharpBlockContainsOrdered(
		actionResultSettlementSource,
		"if (intent is SellCardsResultIntent sellIntent)",
		[
			"string soldSlotKey = ResolveResultSlotKey(action, sellIntent.SoldSlotKey, \"出售结果\");",
			"string anchorSlotKey = ResolveResultSlotKey(action, sellIntent.AnchorSlotKey, \"货币生成位置\");",
			"sellIntent.CurrencyCardId.IsValid",
			"anchorBinding.CardIds.Count != 1",
			"soldBinding.CardIds.Count == 0",
			"TabletopCardId currencyAnchorCardId = anchorBinding.CardIds[0];",
			"contentIndex.TryGet(anchorCard.ContentId, out CardBuyerDefinition buyerDefinition)",
			"int totalSellValue = 0;",
			"card is ChestCard chest && chest.StoredCurrencyCount > 0",
			"soldDefinition.SellValue <= 0",
			"totalSellValue = checked(totalSellValue + soldDefinition.SellValue);",
			"soldContentIds.Add(soldDefinition.ContentId);",
			"removals.Add(cardId);",
			"creations.Add(new CardCreationSpec(",
			"createAsSingleStack: true",
			"positionOffset: buyerDefinition.CurrencySpawnOffset",
		],
		"出售结算按 StackCraft CardBuyer 语义处理整堆售价、空箱限制和货币生成分支");
	if (actionResultSettlementSource.includes("TabletopCardId currencyAnchorCardId = soldBinding.CardIds[0];")) {
		fail("出售结算仍把货币生成锚点放在被出售卡上，偏离 StackCraft TradeZone.spawnPosition。");
	}
}

const tabletopForPackFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Tabletop.cs");
if (tabletopForPackFlowSource == null) {
	fail("缺少 Tabletop，无法证明卡包最后一次使用会通过牌桌正式移除链提交。");
} else {
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"internal void UseCard",
		[
			"if (card.RemainingUses == 1)",
			"RemoveCard(cardId);",
			"return;",
			"Cards.ConsumeUse(cardId);",
		],
		"Tabletop.UseCard 接管 StackCraft PackInstance.Use / Kill 用尽移除语义");
}

const cardPackEditModeTestsSource = readIfExists("Assets/Editor/Gameplay/Tests/CardPackEditModeTests.cs");
if (cardPackEditModeTestsSource == null) {
	fail("缺少 CardPackEditModeTests，无法证明卡包逐槽、权重和配方发现有代码级回归覆盖。");
} else {
	assertCsharpMethodsExist(
		cardPackEditModeTestsSource,
		[
			"OpenPackAction_DrawsSlotsInOrderAndRemovesPackWhenExhausted",
			"OpenPackAction_WithSameSeedProducesSameWeightedDraw",
			"OpenPackAction_DiscoversOnlyUndiscoveredRecipeBeforeFallingBackToCards",
		],
		"卡包逐槽打开、权威随机和未发现配方优先回归覆盖");
}

const contentAssetForRecipeFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/ContentAsset.cs");
const displayableContentAssetForRecipeFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/DisplayableContentAsset.cs");
const cardDefinitionForRecipeFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/CardDefinition.cs");
const actionDefinitionForRecipeFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Actions/ActionDefinition.cs");
const actionParticipationForRecipeFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Actions/ActionParticipation.cs");
const actionResultIntentBaseForRecipeFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Actions/ActionResultIntent.cs");
const actionCandidatesForCraftingFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Actions/ActionCandidates.cs");
const actionInstanceForCraftingFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Actions/ActionInstance.cs");
const actionInstanceSnapshotForCraftingFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Actions/ActionInstanceSnapshot.cs");
const tabletopActionProgressViewForCraftingFlowSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopActionProgressView.cs");
const actionInstanceEditModeTestsForCraftingFlowSource = readIfExists("Assets/Editor/Gameplay/Tests/ActionInstanceEditModeTests.cs");
const actionResultSettlementEditModeTestsForCraftingFlowSource = readIfExists("Assets/Editor/Gameplay/Tests/ActionResultSettlementEditModeTests.cs");

if (contentAssetForRecipeFlowSource == null) {
	fail("缺少 ContentAsset，无法证明 StackCraft RecipeDefinition.Id 已由唯一内容 ID 接管。");
} else {
	assertCsharpBlockContainsOrdered(
		contentAssetForRecipeFlowSource,
		"public abstract class ContentAsset",
		[
			"private ContentId m_contentId;",
			"private int[] m_tagCodes = Array.Empty<int>();",
			"public ContentId ContentId => m_contentId;",
			"public IReadOnlyList<int> TagCodes => m_tagCodes",
			"internal void ValidateContentAsset",
			"protected virtual void ValidateContent",
		],
		"ContentAsset 接管 StackCraft RecipeDefinition.Id 和旧类型标记的内容身份 / 标签职责");
}
if (displayableContentAssetForRecipeFlowSource == null) {
	fail("缺少 DisplayableContentAsset，无法证明 StackCraft RecipeDefinition.DisplayName 已由当前内容展示作者源接管。");
} else {
	assertCsharpBlockContainsOrdered(
		displayableContentAssetForRecipeFlowSource,
		"public abstract class DisplayableContentAsset",
		[
			"private string m_displayName;",
			"private string m_description;",
			"private SoftAssetReference<Sprite> m_icon;",
			"public string DisplayName =>",
			"public string Description =>",
			"public SoftAssetReference<Sprite> Icon => m_icon;",
		],
		"DisplayableContentAsset 接管 StackCraft RecipeDefinition 展示字段");
}
if (cardDefinitionForRecipeFlowSource == null) {
	fail("缺少 CardDefinition，无法证明 StackCraft Grower / Research 空类型标记已降级为卡牌作者源和标签 / 行动条件。");
} else {
	assertCsharpBlockContainsOrdered(
		cardDefinitionForRecipeFlowSource,
		"public class CardDefinition",
		[
			"public virtual int InitialUses => m_initialUses;",
			"public virtual bool CountsTowardCardLimit => m_countsTowardCardLimit;",
			"protected internal virtual TabletopCard CreateRuntimeCard",
			"protected internal virtual TabletopCard RestoreRuntimeCard",
			"protected override void ValidateContent",
		],
		"CardDefinition 只承接可实例化卡牌事实，不用 StackCraft 空派生类型承载行为");
}
if (actionDefinitionForRecipeFlowSource == null) {
	fail("缺少 ActionDefinition，无法证明 StackCraft RecipeDefinition 已由行动作者源接管。");
} else {
	assertCsharpBlockContainsOrdered(
		actionDefinitionForRecipeFlowSource,
		"public class ActionDefinition",
		[
			"private int m_turnCost = 1;",
			"private string m_journalGroupName;",
			"private ActionSlotDefinition[] m_participationSlots = Array.Empty<ActionSlotDefinition>();",
			"private ActionCondition[] m_conditions = Array.Empty<ActionCondition>();",
			"private ActionResultIntent[] m_resultIntents = Array.Empty<ActionResultIntent>();",
			"private ActionResultBranchDefinition[] m_resultBranches = Array.Empty<ActionResultBranchDefinition>();",
			"public IReadOnlyList<ActionSlotDefinition> ParticipationSlots =>",
			"public IReadOnlyList<ActionResultIntent> ResultIntents =>",
			"public IReadOnlyList<ActionResultBranchDefinition> ResultBranches =>",
			"public int TurnCost => m_turnCost;",
			"public string JournalGroupName => m_journalGroupName ?? string.Empty;",
		],
		"ActionDefinition 接管 StackCraft RecipeDefinition 材料、分类、耗时、结果和随机分支作者源");
}
if (actionParticipationForRecipeFlowSource == null) {
	fail("缺少 ActionParticipation，无法证明 StackCraft RecipeDefinition.Ingredient 已由行动参与槽位接管。");
} else {
	assertCsharpBlockContainsOrdered(
		actionParticipationForRecipeFlowSource,
		"public sealed class ActionSlotDefinition",
		[
			"private int m_minimumParticipants = 1;",
			"private int m_maximumParticipants = 1;",
			"private ContentId[] m_allowedContentIds = Array.Empty<ContentId>();",
			"private int[] m_requiredAllContentTagCodes = Array.Empty<int>();",
			"private int[] m_requiredAnyContentTagCodes = Array.Empty<int>();",
			"private int[] m_requiredNoneContentTagCodes = Array.Empty<int>();",
			"private int[] m_requiredAllAbilitySystemTagCodes = Array.Empty<int>();",
			"private int[] m_requiredAnyAbilitySystemTagCodes = Array.Empty<int>();",
			"private int[] m_requiredNoneAbilitySystemTagCodes = Array.Empty<int>();",
		],
		"ActionSlotDefinition 接管 StackCraft Ingredient 卡牌、数量和类型条件");
	assertCsharpBlockContainsOrdered(
		actionParticipationForRecipeFlowSource,
		"public static bool MatchesParticipant",
		[
			"return MatchesContent(slot, contentAsset) && MatchesAbilitySystemTags(slot, abilitySystemCell);",
		],
		"ActionParticipationEvaluator 用内容 ID / GAS 标签替代 StackCraft 空类型判断");
}
if (actionResultIntentBaseForRecipeFlowSource == null) {
	fail("缺少 ActionResultIntent，无法证明 StackCraft RecipeDefinition.Execute 已拆成只声明意图的作者源。");
} else {
	assertCsharpBlockContainsOrdered(
		actionResultIntentBaseForRecipeFlowSource,
		"public abstract class ActionResultIntent",
		[
			"internal void ValidateIntent",
			"protected virtual void ValidateResult",
		],
		"ActionResultIntent 只声明结果意图，不直接执行 StackCraft RecipeDefinition.Execute 副作用");
	assertCsharpBlockContainsOrdered(
		actionResultIntentBaseForRecipeFlowSource,
		"public sealed class ActionResultBranchDefinition",
		[
			"private string m_key;",
			"private int m_weight = 1;",
			"private ActionResultIntent[] m_resultIntents = Array.Empty<ActionResultIntent>();",
			"public string Key => m_key ?? string.Empty;",
			"public int Weight => m_weight;",
			"public IReadOnlyList<ActionResultIntent> ResultIntents =>",
		],
		"ActionResultBranchDefinition 接管 StackCraft RecipeDefinition.RandomWeight 多结果随机选择作者源");
}
if (actionResultIntentsForPackFlowSource == null) {
	fail("缺少 ActionResultIntents，无法证明 StackCraft 特殊配方结果已由当前行动结果意图接管。");
} else {
	assertCsharpBlockContainsOrdered(
		actionResultIntentsForPackFlowSource,
		"public sealed class ExploreCardsResultIntent",
		[
			"private string m_exploredSlotKey;",
			"public string ExploredSlotKey =>",
			"context.ValidateSlotReference(",
		],
		"ExploreCardsResultIntent 接管 StackCraft ExplorationRecipe 探索事实意图");
	assertCsharpBlockContainsOrdered(
		actionResultIntentsForPackFlowSource,
		"public sealed class ResearchDiscoveryResultIntent",
		[
			"private ResearchDiscoveryEntry[] m_entries = Array.Empty<ResearchDiscoveryEntry>();",
			"private string m_anchorSlotKey;",
			"public IReadOnlyList<ResearchDiscoveryEntry> Entries =>",
			"Entries.Count == 0",
			"context.Content.TryGet(entry.ActionId, out ActionDefinition _)",
			"context.Content.TryGet(entry.RecipeCardId, out CardDefinition _)",
			"context.ValidateSlotReference(",
		],
		"ResearchDiscoveryResultIntent 接管 StackCraft ResearchRecipe 未发现配方候选意图");
	assertCsharpBlockContainsOrdered(
		actionResultIntentsForPackFlowSource,
		"public sealed class RemoveCardsResultIntent",
		[
			"private string m_slotKey;",
			"public string SlotKey =>",
			"context.ValidateSlotReference(SlotKey, \"ACTION_RESULT_REMOVE_SLOT_UNKNOWN\");",
		],
		"RemoveCardsResultIntent 接管 StackCraft IngredientConsumption.Destroy 意图");
	assertCsharpBlockContainsOrdered(
		actionResultIntentsForPackFlowSource,
		"public sealed class UseCardsResultIntent",
		[
			"private string m_slotKey;",
			"public string SlotKey =>",
			"context.ValidateSlotReference(SlotKey, \"ACTION_RESULT_USE_SLOT_UNKNOWN\");",
		],
		"UseCardsResultIntent 接管 StackCraft IngredientConsumption.Consume 意图");
	assertCsharpBlockContainsOrdered(
		actionResultIntentsForPackFlowSource,
		"public sealed class CreateCardsResultIntent",
		[
			"private ContentId m_contentId;",
			"private int m_count = 1;",
			"private string m_anchorSlotKey;",
			"context.Content.TryGet(ContentId, out ContentAsset _)",
			"Count <= 0",
			"context.ValidateSlotReference(",
		],
		"CreateCardsResultIntent 接管 StackCraft CreateCard 产物意图");
}
if (actionResultSettlementSource == null) {
	fail("缺少 ActionResultSettlement，无法证明 StackCraft RecipeDefinition.Execute 副作用由牌桌原子结算接管。");
} else {
	assertCsharpBlockContainsOrdered(
		actionResultSettlementSource,
		"private static void AddExploredCards",
		[
			"string exploredSlotKey = ResolveResultSlotKey(action, intent.ExploredSlotKey, \"探索结果\");",
			"ActionSlotBinding exploredBinding = FindBinding(action.ContentId, bindings, exploredSlotKey);",
			"contentIndex.TryGet(card.ContentId, out CardDefinition _)",
			"exploredContentIds.Add(card.ContentId);",
		],
		"探索结果意图接管 StackCraft ExplorationRecipe.NotifyExplorationFinished 事实");
	assertCsharpBlockContainsOrdered(
		actionResultSettlementSource,
		"private static void AddIntent",
		[
			"intent is ExploreCardsResultIntent exploreIntent",
			"AddExploredCards(",
			"intent is UseCardsResultIntent useIntent",
			"uses.Add(cardId);",
			"intent is ResearchDiscoveryResultIntent researchIntent",
			"researchDiscoveries.Add(new ResearchDiscoverySpec(entries, anchorBinding.CardIds[0]));",
			"intent is RemoveCardsResultIntent removeIntent",
			"intent is CreateCardsResultIntent { ContentId: var contentId } createIntent",
			"creations.Add(new CardCreationSpec(createIntent.ContentId, createIntent.Count, anchorBinding.CardIds[0]));",
			"removals.Add(cardId);",
		],
		"行动结果结算接管 StackCraft IngredientConsumption、ResearchRecipe 和标准生成 / 移除语义");
	assertCsharpBlockContainsOrdered(
		actionResultSettlementSource,
		"internal static ActionSettlementResult Commit",
		[
			"TabletopPresentationCue> presentationCues",
			"List<TabletopCardId> effectiveRemovals",
			"tabletop.UseCard(plan.UseCardIds[useIndex]);",
			"tabletop.RemoveCard(plan.RemovalCardIds[k]);",
			"tabletop.CreateCardStack(",
			"return new ActionSettlementResult(",
		],
		"行动提交统一执行 StackCraft Use / Destroy / CreateCard 结果并返回剧本事实");
	assertCsharpBlockContainsOrdered(
		actionResultSettlementSource,
		"internal static ActionSettlementResult Commit",
		[
			"for (int researchIndex = 0; researchIndex < plan.ResearchDiscoveries.Count; researchIndex++)",
			"List<ResearchDiscoveryEntrySpec> available = new List<ResearchDiscoveryEntrySpec>();",
			"!isContentDiscovered(entry.ActionId)",
			"authoritativeRandom.NextInt(available.Count)",
			"selected.RecipeCardId",
			"plannedDiscoveries.Add(selected.ActionId);",
			"discoveries.Add(selected.ActionId);",
		],
		"研究提交接管 StackCraft ResearchRecipe 未发现配方过滤和随机解锁");
}
if (tabletopForPackFlowSource == null) {
	fail("缺少 Tabletop，无法证明 StackCraft RecipeDefinition.RandomWeight 已由牌桌权威随机流接管。");
} else {
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"private string SelectResultBranch",
		[
			"action.ResultBranches.Count == 0",
			"authoritativeRandom.state == 0",
			"uint totalWeight = 0u;",
			"branch.Weight <= 0",
			"totalWeight = checked(totalWeight + (uint)branch.Weight);",
			"uint roll = authoritativeRandom.NextUInt(totalWeight);",
			"return branch2.Key;",
		],
		"Tabletop.SelectResultBranch 接管 StackCraft RecipeDefinition.RandomWeight 权威随机分支选择");
}
if (actionCandidatesForCraftingFlowSource == null) {
	fail("缺少 ActionCandidates，无法证明 StackCraft CraftingManager.CheckForRecipe 自动匹配已由正式行动候选 / 行动计划链路接管。");
} else {
	assertCsharpBlockContainsOrdered(
		actionCandidatesForCraftingFlowSource,
		"internal static ActionCandidate[] FindCandidates",
		[
			"availableActions",
			"TryCreateParticipant(intent.CardId",
			"TryCreateParticipant(intent.TargetCardId",
			"seenActionIds.Add(action.ContentId) && TryCreateCandidate(action, participants, out var candidate)",
			"TryCreateDraggedTailCandidate(action, participants, draggedStackTail, out var stackTailCandidate)",
			"candidates.Add(candidate);",
		],
		"ActionCandidateResolver 接管 StackCraft CheckForRecipe 的玩家交互触发候选生成");
	assertCsharpBlockContainsOrdered(
		actionCandidatesForCraftingFlowSource,
		"private static bool TryCreateCandidate",
		[
			"IReadOnlyList<ActionSlotDefinition> slots = action.ParticipationSlots;",
			"SearchAssignments(0, participants, slots, working, ref best",
			"candidate = CreateCandidate(action, slots, best);",
		],
		"ActionCandidateResolver 用参与槽位替代 StackCraft DoesStackMatchRecipe 材料匹配");
}
if (actionInstanceForCraftingFlowSource == null) {
	fail("缺少 ActionInstance，无法证明 StackCraft CraftingTask 进度生命周期已由正式行动实例接管。");
} else {
	assertCsharpBlockContainsOrdered(
		actionInstanceForCraftingFlowSource,
		"public enum ActionInstanceState",
		[
			"Running = 0",
			"Paused = 10",
			"Completed = 20",
			"Cancelled = 30",
		],
		"ActionInstanceState 枚举必须接管 StackCraft CraftingTask 运行 / 暂停 / 完成 / 取消状态");
	assertCsharpBlockContainsOrdered(
		actionInstanceForCraftingFlowSource,
		"public sealed class ActionInstance",
		[
			"public float Progress => (TurnCost == 0) ? 1f : Mathf.Clamp01(ProgressedTurns / (float)TurnCost);",
			"public float RemainingTurns => Mathf.Max(0f, TurnCost - ProgressedTurns);",
		],
		"ActionInstance 必须在运行对象内部计算进度和剩余回合");
	assertCsharpBlockContainsOrdered(
		actionInstanceForCraftingFlowSource,
		"internal static ActionInstance Restore",
		[
			"if (!candidate.IsReady)",
			"snapshot.TurnCost <= 0 || snapshot.TurnCost != candidate.Action.TurnCost",
			"snapshot.State != ActionInstanceState.Running && snapshot.State != ActionInstanceState.Paused",
			"snapshot.ProgressedTurns < 0f",
			"snapshot.ProgressedTurns >= snapshot.TurnCost",
			"ValidateSnapshotBindings(candidate, snapshot);",
			"ValidateResultBranch(candidate, snapshot);",
		],
		"ActionInstance.Restore 接管 StackCraft RestoreCraftingTask 的进度恢复但重新复核作者源和参与对象");
	assertCsharpBlockContainsOrdered(
		actionInstanceForCraftingFlowSource,
		"internal void Advance",
		[
			"State != ActionInstanceState.Paused",
			"State != ActionInstanceState.Running",
			"ProgressedTurns = Math.Min(TurnCost, ProgressedTurns + turnUnits);",
			"State = ActionInstanceState.Completed;",
		],
		"ActionInstance.Advance 接管 StackCraft CraftingTask.UpdateProgress 的暂停 / 完成推进语义");
	assertCsharpBlockContainsOrdered(
		actionInstanceForCraftingFlowSource,
		"internal void Pause()",
		[
			"RequireState(ActionInstanceState.Running, \"暂停\");",
			"State = ActionInstanceState.Paused;",
		],
		"ActionInstance.Pause 必须只允许运行中行动进入暂停");
	assertCsharpBlockContainsOrdered(
		actionInstanceForCraftingFlowSource,
		"internal void Resume()",
		[
			"RequireState(ActionInstanceState.Paused, \"恢复\");",
			"State = ActionInstanceState.Running;",
		],
		"ActionInstance.Resume 必须只允许暂停行动恢复运行");
	assertCsharpBlockContainsOrdered(
		actionInstanceForCraftingFlowSource,
		"internal void Cancel(ActionCancellationReason reason)",
		[
			"reason == ActionCancellationReason.None",
			"CancellationReason = reason;",
			"State = ActionInstanceState.Cancelled;",
		],
		"ActionInstance.Cancel 必须要求明确取消原因并提交取消终态");
}
if (tabletopForPackFlowSource == null) {
	fail("缺少 Tabletop，无法证明 StackCraft CraftingManager 活动制作列表已由当前牌桌活动行动集合接管。");
} else {
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"internal ActionInstance StartAction",
		[
			"RequireActive();",
			"return StartActionInstance",
			"CreateCandidateFromRequest(request)",
		],
		"Tabletop.StartAction 接管 StackCraft StartCraftingTask 的正式启动入口");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"public ActionPlan CreateActionPlan",
		[
			"ValidateActionPlan(plan, requireComplete: false);",
			"m_actionPlans.Add(plan);",
			"return plan;",
		],
		"Tabletop.ActionPlan 接管 StackCraft 允许额外材料 / 填充槽位的玩家确认阶段");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"public ActionInstance SubmitActionPlan",
		[
			"ValidateActionPlan(plan, requireComplete: true);",
			"StartActionInstance(",
			"CreateCandidateFromRequest(plan.CreateRequest())",
			"m_actionPlans.Remove(plan);",
			"return instance;",
		],
		"Tabletop.SubmitActionPlan 接管 StackCraft 完整材料堆进入制作任务");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"private ActionInstance StartActionInstance",
		[
			"IsActiveActionParticipant(cardIds[cardIndex])",
			"int turnCost = candidate.Action.TurnCost;",
			"string resultBranchKey = SelectResultBranch(candidate.Action, ref candidateRandom);",
			"ActionResultSettlement.Compile(",
			"ActionInstance action = new ActionInstance(candidate, turnCost, resultBranchKey, resultPlan);",
			"m_activeActions.Add(action);",
			"CommitCompletedAction(action, ref candidateRandom);",
		],
		"Tabletop.StartActionInstance 接管 StackCraft 活动制作列表、权重随机和立即 / 延迟完成分流");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"private void AdvanceActiveActions",
		[
			"for (int i = m_activeActions.Count - 1; i >= 0; i--)",
			"!AreActionParticipantsValid(action)",
			"action.Cancel(ActionCancellationReason.ParticipantInvalidated);",
			"m_activeActions.RemoveAt(i);",
			"action.Advance(turnUnits);",
			"action.State == ActionInstanceState.Completed",
			"CommitCompletedAction(action);",
		],
		"Tabletop.AdvanceActiveActions 接管 StackCraft 每帧制作推进、参与对象失效取消和完成结算清理");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"ref Unity.Mathematics.Random candidateRandom)",
		[
			"ActionResultSettlement.Commit(",
			"m_authoritativeRandom = candidateRandom;",
			"m_actionCompleted(action.ActionId, result);",
			"ActionSettled?.Invoke(action.ActionId, result);",
		],
		"Tabletop.CommitCompletedAction 接管 StackCraft PerformCraftingAction 的成功后原子结算和完成事实");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"public void PauseAction(ActionInstance action)",
		[
			"RequireActiveAction(action);",
			"action.Pause();",
		],
		"Tabletop.PauseAction 必须通过牌桌活动行动集合复核后暂停");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"public void ResumeAction(ActionInstance action)",
		[
			"RequireActiveAction(action);",
			"action.Resume();",
		],
		"Tabletop.ResumeAction 必须通过牌桌活动行动集合复核后恢复");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"public void CancelAction(ActionInstance action)",
		[
			"RequireActiveAction(action);",
			"action.Cancel(ActionCancellationReason.Requested);",
			"m_activeActions.Remove(action);",
		],
		"Tabletop.CancelAction 必须通过牌桌活动行动集合复核后取消并移除");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"public ActionInstanceSnapshot[] CreateActiveActionSnapshots",
		[
			"ActionInstanceSnapshot[] snapshots = new ActionInstanceSnapshot[m_activeActions.Count];",
			"snapshots[i] = m_activeActions[i].CreateSnapshot();",
			"return snapshots;",
		],
		"Tabletop.CreateActiveActionSnapshots 接管 StackCraft 活动制作进度存档");
	assertCsharpBlockContainsOrdered(
		tabletopForPackFlowSource,
		"private void RestoreActiveActions",
		[
			"ActionRequest request = ActionRequest.FromSnapshot(snapshot);",
			"ActionCandidate candidate = CreateCandidateFromRequest(request);",
			"ActionInstance action = ActionInstance.Restore(candidate, snapshot);",
			"ActionResultSettlement.ValidateRestoredPlan(action, this);",
			"restoredActions.Add(action);",
			"m_activeActions.AddRange(restoredActions);",
		],
		"Tabletop.RestoreActiveActions 接管 StackCraft RestoreCraftingTask 的存档恢复但保持冻结结果计划");
}
if (actionInstanceSnapshotForCraftingFlowSource == null) {
	fail("缺少 ActionInstanceSnapshot，无法证明 StackCraft CraftingTask progress 已由行动快照接管。");
} else {
	assertCsharpBlockContainsOrdered(
		actionInstanceSnapshotForCraftingFlowSource,
		"public sealed class ActionInstanceSnapshot",
		[
			"private ContentId m_actionId;",
			"private int m_turnCost;",
			"private float m_progressedTurns;",
			"private ActionInstanceState m_state;",
			"private string m_resultBranchKey;",
			"private ActionInstanceBindingSnapshot[] m_bindings;",
			"private ActionResultPlanSnapshot m_resultPlan;",
		],
		"ActionInstanceSnapshot 保存 StackCraft CraftingTask 进度恢复所需事实和冻结结果计划");
	assertCsharpBlockContainsOrdered(
		actionInstanceSnapshotForCraftingFlowSource,
		"internal ActionResultPlan CreateRuntimePlan",
		[
			"m_removalCardIds == null",
			"m_creations == null",
			"m_useCardIds == null",
			"m_researchDiscoveries == null",
			"m_packPurchases == null",
			"return new ActionResultPlan(",
		],
		"ActionResultPlanSnapshot 恢复行动开始时冻结的 StackCraft Execute 结果事实");
}
if (tabletopActionProgressViewForCraftingFlowSource == null) {
	fail("缺少 TabletopActionProgressView，无法证明 StackCraft ProgressUI 已由正式行动进度视图接管。");
} else {
	assertCsharpBlockContainsOrdered(
		tabletopActionProgressViewForCraftingFlowSource,
		"public void Show",
		[
			"NormalizedProgress = Mathf.Clamp01(normalizedProgress);",
			"IsPaused = paused;",
			"m_progressFill.fillAmount = NormalizedProgress;",
		],
		"TabletopActionProgressView 接管 StackCraft ProgressUI.UpdateUI fillAmount 语义");
	assertCsharpBlockContainsOrdered(
		tabletopActionProgressViewForCraftingFlowSource,
		"private void EnsureInitialized",
		[
			"m_progressFill.type != Image.Type.Filled",
			"throw new InvalidOperationException",
		],
		"TabletopActionProgressView 要求进度填充 Image 使用 Filled 类型");
}
if (actionInstanceEditModeTestsForCraftingFlowSource == null) {
	fail("缺少 ActionInstanceEditModeTests，无法证明制作运行链的行动生命周期有回归保护。");
} else {
	assertCsharpMethodsExist(
		actionInstanceEditModeTestsForCraftingFlowSource,
		[
			"StartActionRequest_CreatesRunningActionFromTheSelectedCandidateData",
			"PauseResumeAndCancel_KeepOneLegalLifecycleState",
			"ConfirmedWorldTurn_RemovedParticipantCancelsBeforeProgress",
			"ConfirmedWorldTurn_UsesTheActionTurnCostAsTheOnlyProgressTruth",
			"CreateActiveActionSnapshots_CapturesCurrentRunningActionFacts",
			"RestoreTabletop_ContinuesRunningActionWithFrozenResultPlan",
			"RestoreTabletop_LeavesPausedActionPausedUntilExplicitResume",
		],
		"ActionInstanceEditModeTests 覆盖 StackCraft CraftingTask 生命周期替代链");
}
if (actionResultSettlementEditModeTestsForCraftingFlowSource == null) {
	fail("缺少 ActionResultSettlementEditModeTests，无法证明制作完成结果原子提交有回归保护。");
} else {
	assertCsharpMethodsExist(
		actionResultSettlementEditModeTestsForCraftingFlowSource,
		[
			"StartAction_PublishesCompletionFactAfterSuccessfulResultCommit",
			"ConfirmedWorldTurn_DelayedActionSettlesOnlyAfterRequiredTurnsComplete",
			"StartedAction_UsesTheResultPlanCommittedAtStart",
			"StartAction_WeightedResultUsesAuthoritativeSeedAndSettlesSelectedBranch",
		],
		"ActionResultSettlementEditModeTests 覆盖 StackCraft PerformCraftingAction 替代链");
}
for (const [label, sourceText] of [
	["ContentAsset", contentAssetForRecipeFlowSource],
	["DisplayableContentAsset", displayableContentAssetForRecipeFlowSource],
	["CardDefinition", cardDefinitionForRecipeFlowSource],
	["ActionDefinition", actionDefinitionForRecipeFlowSource],
	["ActionParticipation", actionParticipationForRecipeFlowSource],
	["ActionResultIntent", actionResultIntentBaseForRecipeFlowSource],
	["ActionResultIntents", actionResultIntentsForPackFlowSource],
	["ActionResultSettlement", actionResultSettlementSource],
	["Tabletop", tabletopForPackFlowSource],
	["ActionCandidates", actionCandidatesForCraftingFlowSource],
	["ActionInstance", actionInstanceForCraftingFlowSource],
	["ActionInstanceSnapshot", actionInstanceSnapshotForCraftingFlowSource],
]) {
	if (sourceText == null) continue;
	for (const obsoleteToken of [
		"CryingSnow.StackCraft",
		"GrowerDefinition",
		"ResearchDefinition",
		"RecipeDefinition",
		"GrowthRecipe",
		"ExplorationRecipe",
		"ResearchRecipe",
		"TravelRecipe",
		"CraftingManager",
		"CraftingTask",
		"CardManager.Instance",
	]) {
		if (sourceText.includes(obsoleteToken)) {
			fail(`${label} 仍保留 StackCraft 特殊卡 / 特殊配方旧结构残留：${obsoleteToken}`);
		}
	}
}

const stackCraftChestLogicSource = readIfExists("Assets/StackCraft/Scripts/Card/Behaviors/ChestLogic.cs");
const stackCraftChestDefinitionSource = readIfExists("Assets/StackCraft/Scripts/Card/Definitions/ChestDefinition.cs");
const stackCraftWoodenChestAssetText = readIfExists("Assets/StackCraft/Resources/Cards/Specials/Chest/Card_Chest_WoodenChest.asset");
const chestCardDefinitionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/ChestCardDefinition.cs");
const chestCardSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Cards/ChestCard.cs");
const foundationChestAssetText = readIfExists("Assets/Gameplay/Tests/地基测试箱子.asset");
const chestEditModeTestsSource = readIfExists("Assets/Editor/Gameplay/Tests/ChestCardEditModeTests.cs");

if (stackCraftChestLogicSource == null) {
  fail("缺少 StackCraft ChestLogic 源码，无法证明箱子存币、取币、付款和显示数字的来源链。");
} else {
  assertSourceContainsOrdered(
    stackCraftChestLogicSource,
    [
      "public class ChestLogic : MonoBehaviour, IOnStackable, IClickable",
      "public int StoredCoins { get; private set; }",
      "this.capacity = chestDef.Capacity;",
      "currency = TradeManager.Instance?.CurrencyCard;",
      "public bool OnStack(CardStack droppedStack)",
      "IsValidDeposit(droppedStack.TopCard)",
      "DepositCoinStack(droppedStack)",
      "StoredCoins++;",
      "coinStack.DestroyCard(c);",
      "AudioManager.Instance?.PlaySFX(AudioId.Coins);",
      "public bool OnClick(Vector3 clickPosition)",
      "TryWithdrawCoin(true)",
      "public bool TryWithdrawCoin(bool spawnOnBoard)",
      "StoredCoins--;",
      "CardManager.Instance?.CreateCardInstance(currency",
      "AudioManager.Instance?.PlaySFX(AudioId.Coin);",
      "card.UpdatePriceText(StoredCoins.ToString());",
    ],
    "StackCraft ChestLogic 箱子存币、取币、显示和音效来源链");
}

let stackCraftChestDefaultCapacity = null;
if (stackCraftChestDefinitionSource == null) {
  fail("缺少 StackCraft ChestDefinition 源码，无法证明箱子容量默认作者源。");
} else {
  stackCraftChestDefaultCapacity = csharpRawInitializer(
    stackCraftChestDefinitionSource,
    "capacity",
    "StackCraft ChestDefinition.capacity 默认值");
  assertSourceContainsOrdered(
    stackCraftChestDefinitionSource,
    [
      "[CreateAssetMenu(menuName = \"StackCraft/Special Cards/Chest Card\"",
      "private int capacity = 50;",
      "public int Capacity => capacity;",
    ],
    "StackCraft ChestDefinition 箱子容量作者源结构");
}
if (stackCraftWoodenChestAssetText == null) {
  fail("缺少 StackCraft Wooden Chest 作者源资产，无法证明模板木箱真实容量。");
} else {
  assertYamlScalarEquals(
    stackCraftWoodenChestAssetText,
    "capacity",
    stackCraftChestDefaultCapacity,
    "StackCraft Wooden Chest 容量");
}

if (chestCardDefinitionSource == null) {
  fail("缺少 ChestCardDefinition，无法证明箱子效果已由当前 Gameplay 卡牌作者源接管。");
} else {
  assertCsharpBlockContainsOrdered(
    chestCardDefinitionSource,
    "public sealed class ChestCardDefinition : CardDefinition",
    [
      "private int m_capacity = 50;",
      "private ContentId m_currencyCardId;",
      "public int Capacity => m_capacity;",
      "public ContentId CurrencyCardId => m_currencyCardId;",
      "return new ChestCard(id, ContentId, Capacity);",
      "return new ChestCard(snapshot.CardId, ContentId, Capacity, snapshot.RuntimeState);",
      "CHEST_CAPACITY_INVALID",
      "CHEST_CURRENCY_INVALID",
    ],
    "ChestCardDefinition 接管 StackCraft ChestDefinition 容量和货币作者源");
}
if (chestCardSource == null) {
  fail("缺少 ChestCard，无法证明箱子本局存币状态没有落到 UI、行动条件或第二经济系统。");
} else {
  assertCsharpBlockContainsOrdered(
    chestCardSource,
    "public sealed class ChestCard : TabletopCard",
    [
      "public int Capacity { get; }",
      "public int StoredCurrencyCount { get; private set; }",
      "public int RemainingCapacity => Capacity - StoredCurrencyCount;",
      "internal void DepositCurrency(int amount)",
      "StoredCurrencyCount = checked(StoredCurrencyCount + amount);",
      "internal void WithdrawCurrency(int amount)",
      "StoredCurrencyCount -= amount;",
      "internal void ApplyCurrencyChange",
      "CreateRuntimeStateSnapshot",
      "ChestCardRuntimeStateSnapshot",
    ],
    "ChestCard 唯一拥有 StackCraft 箱子本局存币状态");
}
if (foundationChestAssetText == null) {
  fail("缺少 Foundation 箱子测试作者源，无法证明箱子玩家链有统一场景夹具。");
} else {
  assertYamlScalarEquals(
    foundationChestAssetText,
    "m_capacity",
    "2",
    "Foundation 箱子快速玩家链容量");
  assertYamlNestedScalarEquals(
    foundationChestAssetText,
    "m_currencyCardId",
    "m_value",
    "test.foundation.day-cycle.currency",
    "Foundation 箱子存储货币 ID");
}
if (foundationSceneMenuSource == null) {
  fail("缺少 FoundationTestSceneMenu，无法证明箱子测试作者源会稳定重建。");
} else {
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureChestTestAssets()",
    [
      "ScriptableObject.CreateInstance<ChestCardDefinition>()",
      "WriteCardFields(",
      "FoundationTestSceneHarness.TestChestContentId",
      "RequireProperty(serializedChest, \"m_capacity\").intValue = 2;",
      "RequireProperty(serializedChest, \"m_currencyCardId\")",
      "FoundationTestSceneHarness.TestCurrencyCardContentId;",
      "EnsureDepositCurrencyIntoChestActionAsset();",
      "EnsureWithdrawCurrencyFromChestActionAsset();",
    ],
    "FoundationTestSceneMenu 箱子作者源与存取币行动生成器");
}
if (chestEditModeTestsSource == null) {
  fail("缺少 ChestCardEditModeTests，无法证明箱子机制有代码级回归覆盖。");
} else {
  assertCsharpMethodsExist(
    chestEditModeTestsSource,
    [
      "ChestCard_StoresCurrencyAndRoundTripsThroughCardSnapshot",
      "ScenarioRun_DepositsCurrencyIntoChestUntilCapacityAndWithdrawsOneCurrency",
      "ScenarioRun_ChestPaysPackVendorWithoutRemovingChest",
      "ScenarioRun_NonEmptyChestCannotBeSold",
    ],
    "箱子存币 / 取币 / 付款 / 非空不可售回归覆盖");
}

const stackCraftEnclosureLogicSource = readIfExists("Assets/StackCraft/Scripts/Card/Behaviors/EnclosureLogic.cs");
const stackCraftEnclosureDefinitionSource = readIfExists("Assets/StackCraft/Scripts/Card/Definitions/EnclosureDefinition.cs");
const stackCraftCreaturePenText = readIfExists("Assets/StackCraft/Resources/Cards/Specials/Enclosure/Card_Enclosure_CreaturePen.asset");
const stackCraftCreatureCageText = readIfExists("Assets/StackCraft/Resources/Cards/Specials/Enclosure/Card_Enclosure_CreatureCage.asset");
const specialStackCraftCardsEditModeTestsSource = readIfExists("Assets/Editor/Gameplay/Tests/TabletopCardsEditModeTests.cs");
const cardDefinitionSourceForSpecialStackCraftCards = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/CardDefinition.cs");
const tabletopSourceForSpecialStackCraftCards = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Tabletop.cs");

if (stackCraftEnclosureLogicSource == null) {
  fail("缺少 StackCraft EnclosureLogic 源码，无法证明围栏留存容量的来源链。");
} else {
  assertSourceContainsOrdered(
    stackCraftEnclosureLogicSource,
    [
      "public class EnclosureLogic : MonoBehaviour",
      "public int Capacity { get; private set; }",
      "public void Initialize(int capacity)",
      "Capacity = capacity;",
    ],
    "StackCraft EnclosureLogic 围栏容量来源链");
}
if (stackCraftEnclosureDefinitionSource == null) {
  fail("缺少 StackCraft EnclosureDefinition 源码，无法证明围栏容量默认作者源。");
} else {
  const stackCraftEnclosureDefaultCapacity = csharpRawInitializer(
    stackCraftEnclosureDefinitionSource,
    "capacity",
    "StackCraft EnclosureDefinition.capacity 默认值");
  assertSourceContainsOrdered(
    stackCraftEnclosureDefinitionSource,
    [
      "[CreateAssetMenu(menuName = \"StackCraft/Special Cards/Enclosure Card\"",
      "private int capacity = 1;",
      "public int Capacity => capacity;",
    ],
    "StackCraft EnclosureDefinition 围栏容量作者源结构");
  if (stackCraftCreatureCageText != null) {
    assertYamlScalarEquals(
      stackCraftCreatureCageText,
      "capacity",
      stackCraftEnclosureDefaultCapacity,
      "StackCraft Creature Cage 留存容量");
  }
}
if (stackCraftCreaturePenText == null) {
  fail("缺少 StackCraft Creature Pen 作者源资产，无法证明五格围栏容量。");
} else {
  assertYamlScalarEquals(
    stackCraftCreaturePenText,
    "capacity",
    "5",
    "StackCraft Creature Pen 留存容量");
}
if (cardDefinitionSourceForSpecialStackCraftCards == null) {
  fail("缺少 CardDefinition，无法证明围栏容量已降级为卡牌作者源字段。");
} else {
  assertCsharpDeclarationAndBlockContainsOrdered(
    cardDefinitionSourceForSpecialStackCraftCards,
    "public class CardDefinition",
    [
      "private int m_automaticMovementRetentionCapacity;",
      "public int AutomaticMovementRetentionCapacity => m_automaticMovementRetentionCapacity;",
      "CARD_AUTOMATIC_MOVEMENT_RETENTION_CAPACITY_INVALID",
    ],
    "CardDefinition 接管 StackCraft Enclosure 容量为自动移动留存容量");
}
if (tabletopSourceForSpecialStackCraftCards == null) {
  fail("缺少 Tabletop 源码，无法证明围栏容量和 LimitBooster 已由牌桌聚合消费。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopSourceForSpecialStackCraftCards,
    "private bool ShouldStayInAutomaticMovementRetentionStack",
    [
      "character.AbilitySystem.HasTag(XTag.Faction_Enemy)",
      "return false;",
      "TabletopCardStack stack = card.Stack",
      "int cardIndex = stack.IndexOf(card.Id);",
      "CardDefinition enclosureDefinition = RequireCardDefinition(",
      "int capacity = enclosureDefinition.AutomaticMovementRetentionCapacity;",
      "int distanceAboveEnclosure = cardIndex - enclosureIndex;",
      "return distanceAboveEnclosure > 0 && distanceAboveEnclosure <= capacity;",
    ],
    "Tabletop 按 StackCraft Enclosure 语义保留容量内非敌对自动移动卡");
}
if (specialStackCraftCardsEditModeTestsSource == null) {
  fail("缺少 TabletopCardsEditModeTests，无法证明围栏容量和上限扩展有代码级回归覆盖。");
} else {
  assertCsharpMethodsExist(
    specialStackCraftCardsEditModeTestsSource,
    [
      "Tabletop_PlacementBoundsFollowCardLimitBonusAndReflowOnShrink",
      "Tabletop_PlacementBoundsMoveStackCraftHeaderRestrictionWithCardLimitBonus",
      "Tabletop_CardLimitBonusMovesLockedHeaderStacksWithStackCraftBoard",
      "AdvanceRealTime_AutomaticMovementRetentionKeepsCardsWithinCapacity",
    ],
    "LimitBooster 桌面扩张和 Enclosure 留存容量回归覆盖");
  assertCsharpMethodContainsOrdered(
    specialStackCraftCardsEditModeTestsSource,
    "Tabletop_PlacementBoundsFollowCardLimitBonusAndReflowOnShrink",
    [
      "\\\"m_cardLimitBonus\\\":5",
    ],
    "LimitBooster 桌面扩张回归覆盖");
  assertCsharpMethodContainsOrdered(
    specialStackCraftCardsEditModeTestsSource,
    "AdvanceRealTime_AutomaticMovementRetentionKeepsCardsWithinCapacity",
    [
      "\\\"m_automaticMovementRetentionCapacity\\\":1",
    ],
    "Enclosure 留存容量回归覆盖");
}

const stackCraftLimitBoosterDefinitionSource = readIfExists("Assets/StackCraft/Scripts/Card/Definitions/LimitBoosterDefinition.cs");
const stackCraftWarehouseText = readIfExists("Assets/StackCraft/Resources/Cards/Specials/Booster/Card_Booster_Warehouse.asset");
const stackCraftYardText = readIfExists("Assets/StackCraft/Resources/Cards/Specials/Booster/Card_Booster_Yard.asset");
const tabletopPlacementContractsSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Placement/TabletopCardPlacementContracts.cs");

if (stackCraftLimitBoosterDefinitionSource == null) {
  fail("缺少 StackCraft LimitBoosterDefinition 源码，无法证明卡牌上限加成来源。");
} else {
  const stackCraftLimitBoosterDefaultAmount = csharpRawInitializer(
    stackCraftLimitBoosterDefinitionSource,
    "boostAmount",
    "StackCraft LimitBoosterDefinition.boostAmount 默认值");
  assertSourceContainsOrdered(
    stackCraftLimitBoosterDefinitionSource,
    [
      "[CreateAssetMenu(menuName = \"StackCraft/Special Cards/Limit Booster Card\"",
      "private int boostAmount = 4;",
      "public int BoostAmount => boostAmount;",
    ],
    "StackCraft LimitBoosterDefinition 卡牌上限加成作者源结构");
  if (stackCraftYardText != null) {
    assertYamlScalarEquals(
      stackCraftYardText,
      "boostAmount",
      stackCraftLimitBoosterDefaultAmount,
      "StackCraft Yard 卡牌上限加成");
  }
}
if (stackCraftWarehouseText == null) {
  fail("缺少 StackCraft Warehouse 作者源资产，无法证明仓库上限加成。");
} else {
  assertYamlScalarEquals(
    stackCraftWarehouseText,
    "boostAmount",
    "10",
    "StackCraft Warehouse 卡牌上限加成");
}
if (cardDefinitionSourceForSpecialStackCraftCards != null) {
  assertCsharpDeclarationAndBlockContainsOrdered(
    cardDefinitionSourceForSpecialStackCraftCards,
    "public class CardDefinition",
    [
      "private int m_cardLimitBonus;",
      "public int CardLimitBonus => m_cardLimitBonus;",
      "CARD_LIMIT_BONUS_INVALID",
    ],
    "CardDefinition 接管 StackCraft LimitBooster 加成为卡牌上限字段");
}
if (tabletopSourceForSpecialStackCraftCards != null) {
  assertCsharpBlockContainsOrdered(
    tabletopSourceForSpecialStackCraftCards,
    "private int CalculateCardLimitBonus()",
    [
      "CardDefinition definition = RequireCardDefinition(cards[cardIndex].ContentId, \"计算牌桌上限加成\");",
      "cardLimitBonus = checked(cardLimitBonus + definition.CardLimitBonus);",
    ],
    "Tabletop 从当前桌面卡牌派生 StackCraft LimitBooster 总上限加成");
  assertCsharpBlockContainsOrdered(
    tabletopSourceForSpecialStackCraftCards,
    "private void RefreshPlacementRulesForCurrentCards",
    [
      "int cardLimitBonus = CalculateCardLimitBonus();",
      "m_currentPlacementRules = m_basePlacementRules.CreateForCardLimitBonus(cardLimitBonus);",
      "Cards.MoveLockedStacksWithTopRestrictedBand(previousPlacementRules, PlacementRules);",
      "Cards.ReflowPlacement(PlacementRules);",
    ],
    "Tabletop 按 StackCraft LimitBooster 刷新牌桌边界并在收缩时回流牌堆");
}
if (tabletopPlacementContractsSource == null) {
  fail("缺少牌桌放置规则源码，无法证明 LimitBooster 牌桌扩展比例已由放置规则统一计算。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopPlacementContractsSource,
    "public TabletopCardPlacementRules CreateForCardLimitBonus",
    [
      "int placementExpansionBonus = Math.Min(cardLimitBonus, MaxCardLimitBonusPlacementExpansion);",
      "Vector2 expansion = CardLimitBonusExpansionPerPoint * placementExpansionBonus;",
      "IReadOnlyList<Rect> expandedRestrictedAreas = CreateExpandedRestrictedAreas(bounds, Area.RestrictedAreas, expansion);",
      "new TabletopCardPlacementArea(expandedBounds, expandedRestrictedAreas)",
    ],
    "牌桌放置规则按 StackCraft Board / LimitBooster 语义扩展桌面和页眉禁放区");
  assertCsharpBlockContainsOrdered(
    tabletopPlacementContractsSource,
    "private static IReadOnlyList<Rect> CreateExpandedRestrictedAreas",
    [
      "TabletopCardPlacementArea.IsFullWidthTopRestrictedBand(originalBounds, restrictedArea)",
      "restrictedArea.xMin - expansion.x",
      "restrictedArea.yMin + expansion.y",
      "restrictedArea.width + expansion.x * 2f",
      "expandedRestrictedAreas.Add(restrictedArea);",
    ],
    "牌桌放置规则按 StackCraft Board 扩展顶部页眉禁放区");
}

const scenarioDirectorSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioDirector.cs");
const stackCraftScreenFaderSource = readIfExists("Assets/StackCraft/Scripts/UI/ScreenFader.cs");
const stackCraftGameDirectorSource = readIfExists("Assets/StackCraft/Scripts/Core/GameDirector.cs");
const sceneSystemSource = readIfExists("Assets/Scripts/GameCore/Runtime/Game/Systems/SceneSystem.cs");
const transitionSystemSource = readIfExists("Assets/Scripts/GameCore/Runtime/Game/Systems/TransitionSystem.cs");
const scenarioScreenEffectViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/View/ScenarioScreenEffectView.cs");
const stackCraftGameDataSource = readIfExists("Assets/StackCraft/Scripts/SaveSystem/GameData.cs");
const stackCraftSaveSystemSource = readIfExists("Assets/StackCraft/Scripts/SaveSystem/SaveSystem.cs");
const stackCraftSavedGamesUISource = readIfExists("Assets/StackCraft/Scripts/UI/Title/SavedGamesUI.cs");
const stackCraftSavedGameSlotSource = readIfExists("Assets/StackCraft/Scripts/UI/Title/SavedGameSlot.cs");
const stackCraftTitleScreenSource = readIfExists("Assets/StackCraft/Scripts/UI/Title/TitleScreen.cs");
const stackCraftGameplayPrefsUISource = readIfExists("Assets/StackCraft/Scripts/UI/Title/GameplayPrefsUI.cs");
const stackCraftGameOptionsUiSource = readIfExists("Assets/StackCraft/Scripts/UI/GameOptionsUI.cs");
const stackCraftGraphicsManagerSource = readIfExists("Assets/StackCraft/Scripts/Core/GraphicsManager.cs");
const stackCraftAudioManagerSource = readIfExists("Assets/StackCraft/Scripts/Core/AudioManager.cs");
const gameCoreSaveSystemSource = readIfExists("Assets/Scripts/GameCore/Runtime/Game/Systems/SaveSystem.cs");
const saveFileStorageRuntimeSource = readIfExists("Assets/Scripts/GameCore/Runtime/Game/Systems/SaveFileStorageRuntime.cs");
const scenarioStartOptionsSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioStartOptions.cs");
const displaySettingsSystemSource = readIfExists("Assets/Scripts/GameCore/Runtime/Game/Systems/DisplaySettingsSystem.cs");
const audioSystemSource = readIfExists("Assets/Scripts/GameCore/Runtime/Game/Systems/AudioSystem.cs");
const uiSettingsSource = readIfExists("Assets/Scripts/GameCore/Runtime/UI/Menus/Settings/UISettings.cs");
const uiSettingsVolumeSource = readIfExists("Assets/Scripts/GameCore/Runtime/UI/Menus/Settings/UISettingsVolume.cs");
const uiSettingsMasterVolumeSource = readIfExists("Assets/Scripts/GameCore/Runtime/UI/Menus/Settings/UISettingsMasterVolume.cs");
const uiSettingsChannelVolumeSource = readIfExists("Assets/Scripts/GameCore/Runtime/UI/Menus/Settings/UISettingsChannelVolume.cs");
const scenarioSavePanelSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/View/ScenarioSavePanel.cs");
const scenarioSaveSlotViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/View/ScenarioSaveSlotView.cs");
const scenarioTitlePanelSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/View/ScenarioTitlePanel.cs");
const scenarioTitleScreenSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/View/ScenarioTitleScreen.cs");
const saveSystemModuleStorageEditModeTestsSource = readIfExists("Assets/Editor/GameCore/Tests/SaveSystemModuleStorageEditModeTests.cs");
const scenarioSavePanelPlayModeTestsSource = readIfExists("Assets/Tests/PlayMode/ScenarioSavePanelPlayModeTests.cs");
const scenarioTitleScreenPlayModeTestsSource = readIfExists("Assets/Tests/PlayMode/ScenarioTitleScreenPlayModeTests.cs");
const foundationTestScenePlayModeTestsSource = readIfExists("Assets/Tests/PlayMode/FoundationTestScenePlayModeTests.cs");

if (stackCraftScreenFaderSource == null) {
  fail("缺少 StackCraft ScreenFader 源码，无法证明旧全屏淡入淡出玩家效果的来源。");
} else {
  assertSourceContainsOrdered(
    stackCraftScreenFaderSource,
    [
      "[RequireComponent(typeof(CanvasGroup))]",
      "public class ScreenFader : MonoBehaviour",
      "public static ScreenFader Instance { get; private set; }",
      "canvasGroup = GetComponent<CanvasGroup>();",
      "public IEnumerator Fade(float startAlpha, float endAlpha, float fadeDuration = 1.0f)",
      "canvasGroup.blocksRaycasts = (endAlpha > 0.01f);",
      "elapsed += Time.unscaledDeltaTime;",
      "canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);",
      "canvasGroup.alpha = endAlpha;",
    ],
    "StackCraft ScreenFader 全屏 CanvasGroup 淡入淡出来源链");
}

if (stackCraftGameDirectorSource == null) {
  fail("缺少 StackCraft GameDirector 源码，无法证明 ScreenFader 的真实消费链。");
} else {
  assertCsharpBlockContainsOrdered(
    stackCraftGameDirectorSource,
    "private IEnumerator TravelSequence",
    [
      "TimeManager.Instance.SetExternalPause(true);",
      "yield return ScreenFader.Instance?.Fade(0f, 1f);",
      "incomingTravelers = new List<CardData>(travelers);",
      "yield return LoadSceneAsync(sceneName);",
      "TimeManager.Instance.SetExternalPause(false);",
      "yield return ScreenFader.Instance?.Fade(1f, 0f);",
    ],
    "StackCraft TravelSequence 旧转场时序来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftGameDirectorSource,
    "private static AsyncOperation LoadSceneAsync",
    [
      "#if UNITY_EDITOR",
      "string scenePath = $\"Assets/StackCraft/Scenes/{sceneName}.unity\";",
      "return EditorSceneManager.LoadSceneAsyncInPlayMode(",
      "new LoadSceneParameters(LoadSceneMode.Single));",
      "#else",
      "return SceneManager.LoadSceneAsync(sceneName);",
      "#endif",
    ],
    "StackCraft 参考模板 Editor 场景加载隔离补丁来源链");
}

if (stackCraftGameDataSource == null) {
  fail("缺少 StackCraft GameData 源码，无法证明模板保存状态范围。");
} else {
  assertSourceContainsOrdered(
    stackCraftGameDataSource,
    [
      "public class GameData",
      "public int SlotNumber;",
      "public string CurrentScene;",
      "public GameplayPrefs GameplayPrefs;",
      "public Dictionary<string, SceneData> SavedScenes = new();",
      "public HashSet<string> DiscoveredCards = new();",
      "public HashSet<string> DiscoveredRecipes = new();",
      "public HashSet<string> SeenItems = new();",
      "public System.DateTime LastSaved;",
      "public bool TryGetScene(out SceneData sceneData)",
      "SavedScenes.TryGetValue(CurrentScene, out sceneData)",
      "sceneData = new SceneData(CurrentScene);",
    ],
    "StackCraft GameData 槽位、当前场景、局内发现、已读项和按场景快照来源结构");
  assertSourceContainsOrdered(
    stackCraftGameDataSource,
    [
      "public class SceneData",
      "public string SceneName;",
      "public List<StackData> SavedStacks = new();",
      "public List<CombatData> SavedCombats = new();",
      "public List<string> CompletedQuests = new();",
      "public List<QuestData> ActiveQuests = new();",
      "public List<VendorData> SavedVendors = new();",
      "public HashSet<string> CompletedEncounters = new();",
      "public TimeData SavedTime;",
      "public int QuestProgress;",
      "public void SaveStacks(List<CardStack> stacks)",
      "public void SaveCombats(List<CombatTask> activeCombats)",
      "public void SaveQuests(List<string> completed, List<QuestInstance> active)",
      "public void SaveVendors(List<PackVendor> vendors)",
    ],
    "StackCraft SceneData 保存牌堆、战斗、任务、商贩、遭遇和时间来源结构");
  assertSourceContainsOrdered(
    stackCraftGameDataSource,
    [
      "public class StackData",
      "public float[] Position;",
      "public List<CardData> Cards = new();",
      "public CraftingData ActiveCraft;",
      "new CraftingData(task.Recipe.Id, task.Progress)",
      "public class CardData",
      "public string Id;",
      "public int UsesLeft;",
      "public int CurrentHealth;",
      "public int CurrentNutrition;",
      "public int StoredCoins;",
      "public string OriginalId;",
      "public List<CardData> EquippedItems = new();",
      "public class CombatData",
      "public List<CardData> Attackers = new();",
      "public List<CardData> Defenders = new();",
      "public bool PlayerIsAttacker;",
      "public class QuestData",
      "public string QuestId;",
      "public int CurrentAmount;",
      "public class VendorData",
      "public string PackId;",
      "public int PaidAmount;",
      "public class TimeData",
      "public float CurrentTime;",
      "public int CurrentDay;",
      "public class GameplayPrefs",
      "public int DayDuration;",
      "public bool IsFriendlyMode;",
    ],
    "StackCraft GameData 嵌套 DTO 保存牌堆、卡牌、装备、战斗、制作、任务、商贩、时间和开局偏好来源结构");
  assertCsharpBlockContainsOrdered(
    stackCraftGameDataSource,
    "public class GameplayPrefs",
    [
      "public int DayDuration;",
      "public bool IsFriendlyMode;",
      "public GameplayPrefs(int dayDuration, bool isFriendlyMode)",
      "DayDuration = dayDuration;",
      "IsFriendlyMode = isFriendlyMode;",
    ],
    "StackCraft GameplayPrefs 保存日长和友好模式开局偏好来源结构");
}

if (stackCraftSaveSystemSource == null) {
  fail("缺少 StackCraft SaveSystem 源码，无法证明模板文件存取方式。");
} else {
  assertSourceContainsOrdered(
    stackCraftSaveSystemSource,
    [
      "public static class SaveSystem",
      "public static void SaveData<T>(T data, string fileName)",
      "Path.Combine(Application.persistentDataPath, fileName + \".json\")",
      "JsonConvert.SerializeObject(data, Formatting.Indented)",
      "File.WriteAllText(filePath, json)",
      "public static T LoadData<T>(string fileName)",
      "JsonConvert.DeserializeObject<T>(json)",
      "public static Dictionary<string, T> LoadAllValidData<T>()",
      "Directory.GetFiles(directoryPath, \"*.json\")",
      "string fileName = Path.GetFileNameWithoutExtension(filePath);",
      "typeof(T) == typeof(GameData)",
      "!fileName.StartsWith(\"SaveSlot\", System.StringComparison.Ordinal)",
      "data is GameData gameData",
      "string.IsNullOrWhiteSpace(gameData.CurrentScene)",
      "continue;",
      "validDataDict.Add(fileName, data)",
      "public static void DeleteSave(string fileName)",
      "File.Delete(filePath)",
    ],
    "StackCraft SaveSystem persistentDataPath JSON、SaveSlot 隔离读取和按文件名删除来源结构");
}

if (stackCraftGameDirectorSource == null) {
  fail("缺少 StackCraft GameDirector 源码，无法证明模板保存 / 读取触发入口。");
} else {
  assertSourceContainsOrdered(
    stackCraftGameDirectorSource,
    [
      "public event System.Action<SceneData, bool> OnSceneDataReady;",
      "public event System.Action<GameData> OnBeforeSave;",
      "private string titleScene = \"Title\";",
      "private string defaultScene = \"Main\";",
      "public Dictionary<string, GameData> SavedGames { get; private set; }",
      "public GameData GameData { get; private set; }",
      "SavedGames = SaveSystem.LoadAllValidData<GameData>();",
    ],
    "StackCraft GameDirector 保存事件、标题 / 默认场景和启动扫档来源结构");
  assertCsharpBlockContainsOrdered(
    stackCraftGameDirectorSource,
    "private void HandleSceneLoaded",
    [
      "GameData.CurrentScene = scene.name;",
      "bool wasLoaded = GameData.TryGetScene(out SceneData sceneData);",
      "OnSceneDataReady?.Invoke(sceneData, wasLoaded);",
    ],
    "StackCraft GameDirector 场景加载后分发 SceneData 来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftGameDirectorSource,
    "public void NewGame",
    [
      "foreach (var data in SavedGames.Values)",
      "takenSlots.Add(data.SlotNumber);",
      "int candidateSlot = 1;",
      "while (takenSlots.Contains(candidateSlot))",
      "GameData = new GameData(candidateSlot, prefs);",
      "StartCoroutine(TravelSequence(defaultScene, null));",
    ],
    "StackCraft GameDirector 新局自动分配槽位和进入默认场景来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftGameDirectorSource,
    "public void SaveGame",
    [
      "if (GameData == null) return;",
      "OnBeforeSave?.Invoke(GameData);",
      "GameData.LastSaved = System.DateTime.Now;",
      "string fileName = $\"SaveSlot{GameData.SlotNumber:D3}\";",
      "SaveSystem.SaveData<GameData>(GameData, fileName);",
      "SavedGames.TryAdd(fileName, GameData);",
    ],
    "StackCraft GameDirector 保存当前 GameData 来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftGameDirectorSource,
    "public void LoadGame",
    [
      "this.GameData = gameData;",
      "StartCoroutine(TravelSequence(gameData.CurrentScene, null));",
    ],
    "StackCraft GameDirector 读档后按 CurrentScene 旅行来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftGameDirectorSource,
    "public void DeleteGame",
    [
      "string fileName = $\"SaveSlot{gameData.SlotNumber:D3}\";",
      "SavedGames.Remove(fileName);",
      "SaveSystem.DeleteSave(fileName);",
    ],
    "StackCraft GameDirector 删除内存槽位和 JSON 文件来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftGameDirectorSource,
    "public void BackToTitle",
    [
      "SaveGame();",
      "StartCoroutine(TravelSequence(titleScene, null));",
    ],
    "StackCraft GameDirector 保存并返回标题来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftGameDirectorSource,
    "public void GameOver",
    [
      "DeleteGame(this.GameData);",
      "StartCoroutine(TravelSequence(titleScene, null));",
    ],
    "StackCraft GameDirector 游戏结束删除存档并返回标题来源链");
}

if (stackCraftSavedGamesUISource == null || stackCraftSavedGameSlotSource == null ||
    stackCraftTitleScreenSource == null) {
  fail("缺少 StackCraft 标题存档 UI 源码，无法证明读取、删除和清空存档的玩家流程来源。");
} else {
  assertCsharpBlockContainsOrdered(
    stackCraftTitleScreenSource,
    "private void Start",
    [
      "newGameButton.SetOnClick(() => gameplayPrefsUI.Open());",
      "loadGameButton.SetOnClick(() => savedGamesUI.Open());",
      "gameOptionsButton.SetOnClick(() => gameOptionsUI.Open());",
      "quitGameButton.SetOnClick(() =>",
      "modalWindow.Show(",
      "Application.Quit",
    ],
    "StackCraft TitleScreen 四个标题命令来源链");
  if (stackCraftGameplayPrefsUISource == null) {
    fail("缺少 StackCraft GameplayPrefsUI 源码，无法证明标题新局日长 / 友好模式偏好来源。");
  } else {
    assertSourceContainsOrdered(
      stackCraftGameplayPrefsUISource,
      [
        "public class GameplayPrefsUI : MonoBehaviour",
        "private TextMeshProUGUI durationLabel;",
        "private Slider durationSlider;",
        "private TextMeshProUGUI isFriendlyLabel;",
        "private Toggle isFriendlyToggle;",
        "private TextButton cancelButton;",
        "private TextButton confirmButton;",
      ],
      "StackCraft GameplayPrefsUI 日长、友好模式和确认 / 取消字段来源结构");
    assertCsharpBlockContainsOrdered(
      stackCraftGameplayPrefsUISource,
      "private void Awake",
      [
        "durationSlider.onValueChanged.AddListener",
        "int duration = (int)value;",
        "durationLabel.text = $\"Day Duration: {duration} sec\";",
        "isFriendlyToggle.onValueChanged.AddListener",
        "string state = isOn ? \"ON\" : \"OFF\";",
        "string message = isOn ? \"(No enemies will appear)\" : \"(Enemies may appear)\";",
        "isFriendlyLabel.text = $\"Friendly Mode: {state}\\n<size=23>{message}\";",
        "AudioManager.Instance?.PlaySFX(AudioId.Click);",
        "cancelButton.SetOnClick(Close);",
        "confirmButton.SetOnClick(StartNewGame);",
      ],
      "StackCraft GameplayPrefsUI 日长标签、友好模式文案、点击音效和按钮绑定来源链");
    assertCsharpBlockContainsOrdered(
      stackCraftGameplayPrefsUISource,
      "private void StartNewGame",
      [
        "int dayDuration = (int)durationSlider.value;",
        "bool isFriendlyMode = isFriendlyToggle.isOn;",
        "var prefs = new GameplayPrefs(dayDuration, isFriendlyMode);",
        "GameDirector.Instance.NewGame(prefs);",
        "Close();",
      ],
      "StackCraft GameplayPrefsUI 确认新局并把偏好交给 GameDirector.NewGame 来源链");
  }
  if (stackCraftGameOptionsUiSource == null ||
      stackCraftGraphicsManagerSource == null ||
      stackCraftAudioManagerSource == null) {
    fail("缺少 StackCraft GameOptionsUI / GraphicsManager / AudioManager 源码，无法证明模板设置面板来源。");
  } else {
    assertSourceContainsOrdered(
      stackCraftGameOptionsUiSource,
      [
        "public class GameOptionsUI : MonoBehaviour",
        "private TextButton resolutionButton;",
        "private TextButton fullscreenButton;",
        "private TextButton vSyncButton;",
        "private TextButton fpsButton;",
        "private TextButton shadowButton;",
        "private TextMeshProUGUI labelSFX;",
        "private Slider sliderSFX;",
        "private TextMeshProUGUI labelBGM;",
        "private Slider sliderBGM;",
        "private TextButton resetButton;",
        "private TextButton closeButton;",
        "private ModalWindow modalWindow;",
      ],
      "StackCraft GameOptionsUI 图形、音频、重置和关闭字段来源结构");
    assertCsharpBlockContainsOrdered(
      stackCraftGameOptionsUiSource,
      "private void Start",
      [
        "GraphicsManager.Instance.CycleScreenResolution();",
        "GraphicsManager.Instance.CycleFullscreenMode();",
        "GraphicsManager.Instance.CycleVSync();",
        "GraphicsManager.Instance.CycleFrameRateCap();",
        "GraphicsManager.Instance.CycleShadowPreset();",
        "InitButtonLabels();",
        "sliderSFX.onValueChanged.AddListener",
        "AudioManager.Instance?.SetSFXVolume(value);",
        "labelSFX.text = $\"SFX {Mathf.RoundToInt(value * 100)}%\";",
        "sliderBGM.onValueChanged.AddListener",
        "AudioManager.Instance?.SetBGMVolume(value);",
        "labelBGM.text = $\"BGM {Mathf.RoundToInt(value * 100)}%\";",
        "InitVolumeSliders();",
        "resetButton.SetOnClick",
        "modalWindow.Show(",
        "ResetAllSettings",
        "closeButton.SetOnClick(Close);",
      ],
      "StackCraft GameOptionsUI 设置按钮、音量滑条、重置确认和关闭来源链");
    assertCsharpBlockContainsOrdered(
      stackCraftGameOptionsUiSource,
      "private void ResetAllSettings",
      [
        "PlayerPrefs.DeleteAll();",
        "PlayerPrefs.Save();",
        "GraphicsManager.Instance?.InitGraphicsSettings();",
        "InitButtonLabels();",
        "AudioManager.Instance?.InitAudioMixerVolumes();",
        "InitVolumeSliders();",
      ],
      "StackCraft GameOptionsUI 重置全部 PlayerPrefs 的旧来源链");
    assertSourceContainsOrdered(
      stackCraftGraphicsManagerSource,
      [
        "public class GraphicsManager : MonoBehaviour",
        "public static GraphicsManager Instance { get; private set; }",
        "FullScreenMode.FullScreenWindow",
        "FullScreenMode.Windowed",
        "private static readonly int[] fpsCaps",
        "-1, 30, 60, 120, 144, 240",
        "private enum ShadowPreset { Off, Low, Medium, High, Ultra }",
        "private const string SCREEN_WIDTH_KEY = \"ScreenWidth\";",
        "private const string SHADOW_KEY = \"ShadowPreset\";",
      ],
      "StackCraft GraphicsManager 单例、显示设置列表和 PlayerPrefs 键来源结构");
    assertCsharpBlockContainsOrdered(
      stackCraftGraphicsManagerSource,
      "private void Update",
      [
        "Shader.SetGlobalFloat(\"_UnscaledTime\", Time.unscaledTime);",
      ],
      "StackCraft GraphicsManager 每帧同步 Shader 未缩放时间来源链");
    assertSourceContainsOrdered(
      stackCraftGraphicsManagerSource,
      [
        "public void InitGraphicsSettings()",
        "private void InitScreenResolution()",
        "private void InitPerformanceSettings()",
        "private void InitShadowQuality()",
        "public Resolution CycleScreenResolution()",
        "public void CycleFullscreenMode()",
        "public void CycleVSync()",
        "public void CycleFrameRateCap()",
        "public void CycleShadowPreset()",
        "public string GetResolutionLabel()",
        "public string GetFullscreenLabel()",
        "public string FormatVSyncLabel()",
        "public string FormatFpsLabel()",
        "public string FormatShadowLabel()",
      ],
      "StackCraft GraphicsManager 初始化、循环设置和标签格式化来源链");
    assertSourceContainsOrdered(
      stackCraftAudioManagerSource,
      [
        "public class AudioManager : MonoBehaviour",
        "public static AudioManager Instance { get; private set; }",
        "private int _SFXPoolSize = 8;",
        "private bool _randomizeSFXPitch = true;",
        "private const string SFX_VOL_KEY = \"VolumeSFX\";",
        "private const string BGM_VOL_KEY = \"VolumeBGM\";",
        "public void InitAudioMixerVolumes()",
        "public void SetSFXVolume(float value)",
        "public void SetBGMVolume(float value)",
        "private float LinearToDecibels(float value)",
        "public float GetSavedSFXVolumeSlider()",
        "public float GetSavedBGMVolumeSlider()",
        "public enum AudioId",
      ],
      "StackCraft AudioManager 单例、音效池、音量键、滑条换算和 AudioId 来源结构");
  }
  assertCsharpBlockContainsOrdered(
    stackCraftSavedGamesUISource,
    "private void Start",
    [
      "foreach (var savedGame in GameDirector.Instance.SavedGames.Values)",
      "Instantiate(slotPrefab, contentRect)",
      "slot.Initialize(savedGame, modalWindow, this);",
      "clearButton.SetOnClick(() =>",
      "modalWindow.Show(",
      "\"Delete Games\"",
      "ClearSavedGames",
      "closeButton.SetOnClick(Close);",
    ],
    "StackCraft SavedGamesUI 动态槽位、清空确认和关闭来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftSavedGamesUISource,
    "private void ClearSavedGames",
    [
      "slots.RemoveAll(slot => slot == null);",
      "slots.ForEach(slot => slot.DeleteSavedGame());",
      "slots.Clear();",
    ],
    "StackCraft SavedGamesUI 清空所有存档来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftSavedGameSlotSource,
    "public void Initialize",
    [
      "this.data = data;",
      "sb.Append($\"[Slot {data.SlotNumber:D3}] {data.CurrentScene}\");",
      "data.TryGetScene(out var sceneData)",
      "sb.Append($\" ({sceneData.QuestProgress}%)\");",
      "sb.Append($\"\\nLast Saved: {data.LastSaved}\");",
      "labelText.text = sb.ToString();",
      "GameDirector.Instance.LoadGame(data);",
      "parentUI.Close();",
      "deleteButton.SetOnClick(() =>",
      "modalWindow.Show(",
      "\"Delete Game\"",
      "DeleteSavedGame",
    ],
    "StackCraft SavedGameSlot 摘要、读取和删除确认来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftSavedGameSlotSource,
    "public void DeleteSavedGame",
    [
      "GameDirector.Instance?.DeleteGame(data);",
      "Destroy(gameObject);",
    ],
    "StackCraft SavedGameSlot 删除槽位来源链");
}

if (gameCoreSaveSystemSource == null || saveFileStorageRuntimeSource == null) {
  fail("缺少 GameCore 存档文件层源码，无法证明 StackCraft JSON 扫档已由正式 SaveKit 槽位容器替换。");
} else {
  assertCsharpDeclarationAndBlockContainsOrdered(
    gameCoreSaveSystemSource,
    "public class SaveSystem",
    [
      "public class SaveSystem : AGameSystem, IDataBlockHandler<SaveDataBlock>",
      "public static bool DeleteSaveData(int slotId)",
      "public static IReadOnlyList<SaveMeta> GetAllSaveMetadata()",
      "public static int GetMaximumSaveSlots()",
      "public static int DeleteAllSaveData()",
      "public static SaveData ExtractSaveContainerFromFile(int slotId)",
      "public static SaveData CreateSaveContainer()",
      "public static SaveMeta GetSaveMetadata(int slotId)",
      "public static bool StoreSaveDataToFile(",
      "return SaveFileStorageRuntime.StoreSaveContainer(slotId, container, displayName);",
    ],
    "GameCore SaveSystem 以整数槽位和 SaveKit 模块容器接管 StackCraft SaveSystem 文件职责");
  assertCsharpDeclarationAndBlockContainsOrdered(
    saveFileStorageRuntimeSource,
    "internal static class SaveFileStorageRuntime",
    [
      "internal static class SaveFileStorageRuntime",
      "private const int SaveKitVersion = 1;",
      "private const int SaveKitMaxSlots = 32;",
      "private const string SaveKitDirectoryName = \"GameCoreSaves\";",
      "private const string SaveKitFilePrefix = \"gamecore_\";",
      "private const string SaveKitFileExtension = \".yoki\";",
      "public static IReadOnlyList<SaveMeta> GetAllSaveMetadata()",
      "metadata.Sort((left, right) => left.SlotId.CompareTo(right.SlotId));",
      "public static int DeleteAllSaveData()",
      "public static SaveData ExtractSaveContainerFromFile(int slotId)",
      "public static bool StoreSaveContainer(",
      "SaveKit.Save(slotId, container, displayName)",
      "public static SaveData CreateSaveContainer()",
      "SaveKit.CreateSaveData()",
      "SaveKit.SetFileFormat(SaveKitFilePrefix, SaveKitFileExtension);",
      "SaveKit.SetSavePath(targetPath);",
    ],
    "GameCore SaveFileStorageRuntime 接管 StackCraft persistentDataPath JSON、扫档和删除文件职责");
  for (const obsoleteToken of [
    "LoadAllValidData",
    "SaveSlot{",
    "SaveSlot001",
    "JsonConvert",
    "Directory.GetFiles",
    "DeleteSave(",
  ]) {
    if (gameCoreSaveSystemSource.includes(obsoleteToken) ||
        saveFileStorageRuntimeSource.includes(obsoleteToken)) {
      fail(`GameCore 存档文件层仍保留 StackCraft JSON / 文件名槽位旧结构残留：${obsoleteToken}`);
    }
  }
}

if (scenarioRunSnapshotSource == null) {
  fail("缺少 ScenarioRunSnapshot，无法证明 StackCraft GameData / SceneData 状态范围已由整局快照承接。");
} else {
  assertCsharpDeclarationAndBlockContainsOrdered(
    scenarioRunSnapshotSource,
    "public sealed class ScenarioRunSnapshot",
    [
      "public sealed class ScenarioRunSnapshot",
      "private ContentId m_scenarioId;",
      "private ContentSetSnapshot m_contentSet;",
      "private ModPackageSetSnapshot m_modPackages;",
      "private ContentId m_activeRegionId;",
      "private ScenarioRegionSnapshot[] m_regions;",
      "private ContentId[] m_discoveredContentIds;",
      "private ContentId[] m_seenJournalEntryIds;",
      "private string[] m_completedDayEncounterKeys;",
      "private QuestLogSnapshot m_questLog;",
      "private ulong m_nextCardId;",
      "private int m_confirmedTurnIndex;",
      "private ActionProgressionMode m_progressionMode;",
      "private double m_realTimeElapsedSecondsInTurn;",
      "private bool m_friendlyMode;",
      "private bool m_hasDayDurationSecondsOverride;",
      "private float m_dayDurationSecondsOverride;",
    ],
    "ScenarioRunSnapshot 接管 StackCraft GameData 中的剧本、内容、地区引用、发现、任务、遭遇和时间事实");
  assertCsharpDeclarationAndBlockContainsOrdered(
    scenarioRunSnapshotSource,
    "public sealed class ScenarioRegionSnapshot",
    [
      "public sealed class ScenarioRegionSnapshot",
      "private ContentId m_regionId;",
      "private TabletopSnapshot m_tabletop;",
    ],
    "ScenarioRegionSnapshot 接管 StackCraft SceneData 中的地区牌桌事实");
  for (const obsoleteToken of [
    "GameData",
    "SceneData",
    "StackData",
    "CardData",
    "QuestData",
    "VendorData",
    "TimeData",
    "GameplayPrefs",
  ]) {
    if (scenarioRunSnapshotSource.includes(obsoleteToken)) {
      fail(`ScenarioRunSnapshot 仍保留 StackCraft 保存 DTO 残留：${obsoleteToken}`);
    }
  }
}

if (scenarioDirectorSource == null) {
  fail("缺少 ScenarioDirector，无法证明 StackCraft GameDirector 保存 / 读取 / 删除入口已由正式剧本导演接管。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "public async UniTask StartScenarioAsync(\n\t\t\tContentId scenarioId,\n\t\t\tScenarioStartOptions startOptions,\n\t\t\tuint authoritativeRandomSeed)",
    [
      "int saveSlotId = FindFirstEmptySaveSlot();",
      "ResourceSystem.LoadAssetsByAssetTagAsync<ContentAsset>(ContentAsset.YooAssetContentTag)",
      "ContentIndex.Build(contentAssets)",
      "GameManager.SceneSystem.TransitionToAsync(initialRegionDefinition.SceneAddress)",
      "new ScenarioRun(",
      "m_activeSaveSlotId = saveSlotId;",
    ],
    "ScenarioDirector 新局接管 StackCraft NewGame 槽位分配、内容加载和进场语义");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "public bool SaveActiveRunToSlot",
    [
      "ScenarioRunSnapshot snapshot = run.CreateSnapshot();",
      "SaveData container = SaveSystem.ExtractSaveContainerFromFile(slotId) ??",
      "SaveSystem.CreateSaveContainer();",
      "container.RegisterModule(snapshot);",
      "SaveSystem.StoreSaveDataToFile(",
      "CreateSaveDisplayName(run))",
      "m_activeSaveSlotId = slotId;",
    ],
    "ScenarioDirector 用整局快照接管 StackCraft SaveGame，并保留同槽其它模块");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "public async UniTask LoadRunFromSlotAsync",
    [
      "SaveData container = SaveSystem.ExtractSaveContainerFromFile(slotId);",
      "ResourceSystem.LoadAssetsByAssetTagAsync<ContentAsset>(ContentAsset.YooAssetContentTag)",
      "ContentIndex.Build(contentAssets)",
      "RestoreRunFromSaveContainer(",
      "GetActiveRegionSceneAddress(restoredRun)",
      "GameManager.SceneSystem.TransitionToAsync(targetSceneAddress)",
      "ScenarioRun previousRun = ReplaceActiveRun(restoredRun);",
      "m_activeSaveSlotId = slotId;",
      "EventKit.Type.Send(new ScenarioRunChangedEvent(previousRun, restoredRun));",
    ],
    "ScenarioDirector 读档接管 StackCraft LoadGame，并在场景切换成功后原子发布单局");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "public async UniTask GameOverAsync",
    [
      "m_activeSaveSlotId.HasValue",
      "SaveSlotExists(m_activeSaveSlotId.Value)",
      "SaveSystem.DeleteSaveData(m_activeSaveSlotId.Value)",
      "await EndScenarioAsync();",
    ],
    "ScenarioDirector 游戏结束接管 StackCraft GameOver 删除存档和结束单局语义");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "private static int FindFirstEmptySaveSlot",
    [
      "SaveSystem.GetAllSaveMetadata()",
      "occupiedSlots.Add(metadata[i].SlotId);",
      "SaveSystem.GetMaximumSaveSlots();",
      "!occupiedSlots.Contains(slotId)",
      "return slotId;",
    ],
    "ScenarioDirector 用 SaveMeta 接管 StackCraft SavedGames.Values 自动找空槽语义");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "private static ScenarioRun RestoreRunFromSaveContainer",
    [
      "ScenarioRunSnapshot snapshot = container.GetModule<ScenarioRunSnapshot>();",
      "if (snapshot == null)",
      "contentIndex.TryGet(snapshot.ScenarioId, out ScenarioDefinition definition)",
      "ScenarioRun.Restore(definition, contentIndex, currentModPackages, snapshot);",
    ],
    "ScenarioDirector 从 SaveKit 模块容器恢复整局快照，不恢复 StackCraft GameData.TryGetScene");
  for (const obsoleteToken of [
    "GameData",
    "SceneData",
    "OnBeforeSave",
    "OnSceneDataReady",
    "LoadAllValidData",
    "SaveSlot{",
    "SavedGames",
    "DeleteSave(",
  ]) {
    if (scenarioDirectorSource.includes(obsoleteToken)) {
      fail(`ScenarioDirector 仍保留 StackCraft 保存旧结构残留：${obsoleteToken}`);
    }
  }
}

if (scenarioSavePanelSource == null || scenarioSaveSlotViewSource == null ||
    scenarioTitlePanelSource == null || scenarioTitleScreenSource == null) {
  fail("缺少 Gameplay 标题 / 存档 UI 源码，无法证明 StackCraft SavedGamesUI / SavedGameSlot 已由正式 UIKit 接管。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioTitleScreenSource,
    "private IEnumerator Start",
    [
      "GameManager.StartupState",
      "GameManager.GetSystem<ScenarioDirector>()",
      "UIKit.OpenPanelAsync<ScenarioTitlePanel>(",
      "new ScenarioTitlePanelData(director, m_defaultScenarioId)",
    ],
    "ScenarioTitleScreen 接管 StackCraft TitleScreen 标题入口打开流程");
  assertCsharpBlockContainsOrdered(
    scenarioTitlePanelSource,
    "protected override void OnInit",
    [
      "m_newGameButton.onClick.AddListener(StartNewGame);",
      "m_loadGameButton.onClick.AddListener(OpenLoadPanel);",
      "m_settingsButton.onClick.AddListener(OpenSettings);",
      "m_quitButton.onClick.AddListener(ConfirmQuit);",
    ],
    "ScenarioTitlePanel 接管 StackCraft TitleScreen 四个按钮命令");
  assertCsharpBlockContainsOrdered(
    scenarioTitlePanelSource,
    "protected override void OnInit",
    [
      "m_dayDurationSlider.onValueChanged.AddListener(UpdateDayDurationLabel);",
      "UpdateDayDurationLabel(m_dayDurationSlider.value);",
    ],
    "ScenarioTitlePanel 接管 StackCraft GameplayPrefsUI 日长滑条标签首帧同步");
  assertCsharpBlockContainsOrdered(
    scenarioTitlePanelSource,
    "private ScenarioStartOptions CreateStartOptions",
    [
      "float? dayDurationSeconds = m_dayDurationSlider == null",
      "Mathf.Max(0.001f, m_dayDurationSlider.value)",
      "new ScenarioStartOptions(",
      "m_friendlyModeToggle != null && m_friendlyModeToggle.isOn",
      "dayDurationSeconds",
    ],
    "ScenarioTitlePanel 用 ScenarioStartOptions 接管 StackCraft GameplayPrefs 新局偏好");
  assertCsharpBlockContainsOrdered(
    scenarioTitlePanelSource,
    "private void UpdateDayDurationLabel",
    [
      "m_dayDurationLabel.text = $\"日长：{Mathf.RoundToInt(value)} 秒\";",
    ],
    "ScenarioTitlePanel 用自有中文 UI 文案接管 StackCraft Day Duration 标签刷新");
  assertCsharpBlockContainsOrdered(
    scenarioTitlePanelSource,
    "private void OpenLoadPanel",
    [
      "UIKit.OpenPanelAsync<ScenarioSavePanel>(",
      "new ScenarioSavePanelData(RequireDirector(), ScenarioSavePanelMode.Load)",
    ],
    "ScenarioTitlePanel 接管 StackCraft Load Saved Games 打开存档列表");
  assertCsharpBlockContainsOrdered(
    scenarioTitlePanelSource,
    "private void OpenSettings",
    [
      "UIKit.OpenPanelAsync<UISettings>(",
      "level: UILevel.Pop",
    ],
    "ScenarioTitlePanel 用 UIKit UISettings 接管 StackCraft GameOptionsUI 打开入口");
  if (displaySettingsSystemSource == null ||
      audioSystemSource == null ||
      uiSettingsSource == null ||
      uiSettingsVolumeSource == null ||
      uiSettingsMasterVolumeSource == null ||
      uiSettingsChannelVolumeSource == null) {
    fail("缺少 DisplaySettingsSystem / AudioSystem / UISettings 源码，无法证明 StackCraft 设置面板已由正式系统接管。");
  } else {
    assertCsharpDeclarationAndBlockContainsOrdered(
      displaySettingsSystemSource,
      "public sealed class DisplaySettingsSystem",
      [
        "public sealed class DisplaySettingsSystem : AGameSystem",
        "private const int DefaultTargetWidth = 1920;",
        "private const int DefaultTargetHeight = 1080;",
        "private const FullScreenMode DefaultFullscreenMode = FullScreenMode.FullScreenWindow;",
        "private const int DefaultVSync = 1;",
        "private const int DefaultFrameRateCap = -1;",
        "private const ShadowPreset DefaultShadowPreset = ShadowPreset.High;",
        "private const string PlayerPrefsPrefix = \"GameCore_DisplaySettings_\";",
        "private static readonly FullScreenMode[] FullscreenModes",
        "FullScreenMode.FullScreenWindow",
        "FullScreenMode.Windowed",
        "private static readonly int[] FrameRateCaps",
        "-1",
        "30",
        "60",
        "120",
        "144",
        "240",
      ],
      "DisplaySettingsSystem 接管 StackCraft GraphicsManager 默认值、设置列表和自有 PlayerPrefs 键");
    assertCsharpBlockContainsOrdered(
      displaySettingsSystemSource,
      "private void Update",
      [
        "m_syncUnscaledTimeToShader",
        "Shader.SetGlobalFloat(\"_UnscaledTime\", Time.unscaledTime);",
      ],
      "DisplaySettingsSystem 接管 StackCraft GraphicsManager 的 Shader 未缩放时间同步");
    assertCsharpDeclarationAndBlockContainsOrdered(
      displaySettingsSystemSource,
      "public sealed class DisplaySettingsSystem",
      [
        "public Resolution CycleScreenResolution()",
        "public void CycleFullscreenMode()",
        "public void CycleVSync()",
        "public void CycleFrameRateCap()",
        "public void CycleShadowPreset()",
        "public void ResetSettingsToDefaults()",
        "public string GetResolutionLabel()",
        "public string GetFullscreenLabel()",
        "public string GetVSyncLabel()",
        "public string GetFrameRateCapLabel()",
        "public string GetShadowPresetLabel()",
      ],
      "DisplaySettingsSystem 接管 StackCraft GraphicsManager 循环设置、重置和标签查询 API");
    assertCsharpBlockContainsOrdered(
      displaySettingsSystemSource,
      "public void ResetSettingsToDefaults",
      [
        "PlayerPrefs.DeleteKey(ScreenWidthKey);",
        "PlayerPrefs.DeleteKey(ScreenHeightKey);",
        "PlayerPrefs.DeleteKey(FullscreenModeKey);",
        "PlayerPrefs.DeleteKey(VSyncKey);",
        "PlayerPrefs.DeleteKey(FrameRateCapKey);",
        "PlayerPrefs.DeleteKey(ShadowKey);",
        "ApplyDefaultSettings();",
        "SaveSettings();",
      ],
      "DisplaySettingsSystem 重置自身显示键，不恢复 StackCraft PlayerPrefs.DeleteAll");
    assertCsharpBlockContainsOrdered(
      displaySettingsSystemSource,
      "private static void ApplyRenderPipelineShadowPreset",
      [
        "SetPropertyIfPresent(asset, \"mainLightShadowmapResolution\", resolution);",
        "SetPropertyIfPresent(asset, \"shadowDistance\", distance);",
        "SetPropertyIfPresent(asset, \"shadowCascadeCount\", cascades);",
      ],
      "DisplaySettingsSystem 接管 StackCraft URP 阴影设置但不写死 URP 类型");
    if (displaySettingsSystemSource.includes("PlayerPrefs.DeleteAll") ||
        displaySettingsSystemSource.includes("GraphicsManager.Instance") ||
        displaySettingsSystemSource.includes("CryingSnow.StackCraft")) {
      fail("DisplaySettingsSystem 仍保留 StackCraft GraphicsManager 旧结构或 DeleteAll 语义。");
    }

    assertCsharpDeclarationAndBlockContainsOrdered(
      audioSystemSource,
      "public class AudioSystem",
      [
        "public class AudioSystem : AGameSystem",
        "const string kVolumePlayerPrefsKey = \"GameCore_AudioSystem_Volume_\";",
        "const string kChannelVolumePlayerPrefsKey = kVolumePlayerPrefsKey + \"Channel_\";",
        "const string kMasterVolumePlayerPrefsKey = kVolumePlayerPrefsKey + \"Master\";",
        "public void SetMasterVolume(float volume)",
        "public void ResetSettingsToDefaults()",
        "public void SetChannelVolumeScale(EAudioChannel channel, float volume)",
        "public float GetChannelVolumeScale(EAudioChannel channel)",
      ],
      "AudioSystem 接管 StackCraft AudioManager 音量设置但保留 GameCore 通道模型");
    assertCsharpBlockContainsOrdered(
      audioSystemSource,
      "public void ResetSettingsToDefaults",
      [
        "PlayerPrefs.DeleteKey(kMasterVolumePlayerPrefsKey);",
        "PlayerPrefs.DeleteKey(kLegacyMasterVolumePlayerPrefsKey);",
        "SetMasterVolume(Constants.DefaultMasterVolume);",
        "PlayerPrefs.DeleteKey($\"{kChannelVolumePlayerPrefsKey}{channel.Key}\");",
        "PlayerPrefs.DeleteKey($\"{kLegacyChannelVolumePlayerPrefsKey}{channel.Key}\");",
        "channel.Value.SetVolumeScale(defaultScale);",
        "SaveSettings();",
      ],
      "AudioSystem 重置自身音量键，不恢复 StackCraft AudioManager PlayerPrefs.DeleteAll");
    if (audioSystemSource.includes("PlayerPrefs.DeleteAll") ||
        audioSystemSource.includes("AudioId") ||
        audioSystemSource.includes("AudioManager.Instance") ||
        audioSystemSource.includes("VolumeSFX") ||
        audioSystemSource.includes("VolumeBGM")) {
      fail("AudioSystem 仍保留 StackCraft AudioManager / AudioId / 模板音量键旧结构残留。");
    }

    assertCsharpBlockContainsOrdered(
      uiSettingsSource,
      "protected override void OnPanelInit",
      [
        "HasAllDisplaySettingsControls() && !GameManager.HasSystem<DisplaySettingsSystem>()",
        "m_masterVolume.RegisterCallbacks(OnMasterVolumeDecreased, OnMasterVolumeIncreased);",
        "m_closeButton.onClick.AddListener(CloseFromMenuStackOrSelf);",
        "RegisterDisplaySettingsCallbacks();",
        "channelVolume.RegisterCallbacks(OnChannelVolumeDecreased, OnChannelVolumeIncreased);",
      ],
      "UISettings 初始化时把 StackCraft 设置控件绑定到正式显示 / 音频系统");
    assertCsharpBlockContainsOrdered(
      uiSettingsSource,
      "private void RegisterDisplaySettingsCallbacks",
      [
        "m_resolutionButton.onClick.AddListener(CycleResolution);",
        "m_fullscreenButton.onClick.AddListener(CycleFullscreen);",
        "m_vSyncButton.onClick.AddListener(CycleVSync);",
        "m_frameRateButton.onClick.AddListener(CycleFrameRateCap);",
        "m_shadowButton.onClick.AddListener(CycleShadowPreset);",
        "m_resetSettingsButton.onClick.AddListener(ConfirmResetAllSettings);",
      ],
      "UISettings 接管 StackCraft GameOptionsUI 五个图形按钮和重置按钮绑定");
    assertCsharpDeclarationAndBlockContainsOrdered(
      uiSettingsSource,
      "public class UISettings",
      [
        "private void CycleResolution()",
        "GameManager.DisplaySettingsSystem.CycleScreenResolution();",
        "private void CycleFullscreen()",
        "GameManager.DisplaySettingsSystem.CycleFullscreenMode();",
        "private void CycleVSync()",
        "GameManager.DisplaySettingsSystem.CycleVSync();",
        "private void CycleFrameRateCap()",
        "GameManager.DisplaySettingsSystem.CycleFrameRateCap();",
        "private void CycleShadowPreset()",
        "GameManager.DisplaySettingsSystem.CycleShadowPreset();",
      ],
      "UISettings 把 StackCraft 图形设置按钮映射到 DisplaySettingsSystem");
    assertCsharpBlockContainsOrdered(
      uiSettingsSource,
      "private void ConfirmResetAllSettings",
      [
        "确定重置显示与音频设置吗？这不会删除存档、Mod 配置或其它系统偏好。",
        "GameManager.DisplaySettingsSystem.ResetSettingsToDefaults();",
        "GameManager.AudioSystem.ResetSettingsToDefaults();",
        "UpdateUI();",
      ],
      "UISettings 用安全确认重置接管 StackCraft ResetAllSettings 入口");
    assertCsharpBlockContainsOrdered(
      uiSettingsSource,
      "private void UpdateUI",
      [
        "GameManager.AudioSystem.GetMasterVolume()",
        "GameManager.AudioSystem.GetChannelVolumeScale(channelVolume.audioChannel)",
        "DisplaySettingsSystem displaySettings = GameManager.DisplaySettingsSystem;",
        "m_resolutionLabel.text = displaySettings.GetResolutionLabel();",
        "m_fullscreenLabel.text = displaySettings.GetFullscreenLabel();",
        "m_vSyncLabel.text = displaySettings.GetVSyncLabel();",
        "m_frameRateLabel.text = displaySettings.GetFrameRateCapLabel();",
        "m_shadowLabel.text = displaySettings.GetShadowPresetLabel();",
      ],
      "UISettings 刷新 StackCraft 设置面板等价标签和音量显示");
    assertCsharpDeclarationAndBlockContainsOrdered(
      uiSettingsMasterVolumeSource,
      "public class UISettingsMasterVolume",
      [
        "public class UISettingsMasterVolume : UISettingsVolume",
        "public void RegisterCallbacks(UnityAction decrease, UnityAction increase)",
        "m_decreaseButton.onClick.AddListener(m_decreaseCallback);",
        "m_increaseButton.onClick.AddListener(m_increaseCallback);",
      ],
      "UISettingsMasterVolume 接管 StackCraft SFX / BGM 滑条的主音量按钮控件");
    assertCsharpDeclarationAndBlockContainsOrdered(
      uiSettingsChannelVolumeSource,
      "public class UISettingsChannelVolume",
      [
        "public class UISettingsChannelVolume : UISettingsVolume",
        "private EAudioChannel m_audioChannel;",
        "public EAudioChannel audioChannel => m_audioChannel;",
        "public void RegisterCallbacks(UnityAction<EAudioChannel> decrease, UnityAction<EAudioChannel> increase)",
        "m_decreaseCallback = () => decrease(m_audioChannel);",
        "m_increaseCallback = () => increase(m_audioChannel);",
      ],
      "UISettingsChannelVolume 接管 StackCraft SFX / BGM 滑条的分通道音量控件");
    if (uiSettingsSource.includes("PlayerPrefs.DeleteAll") ||
        uiSettingsSource.includes("GameOptionsUI") ||
        uiSettingsSource.includes("GraphicsManager") ||
        uiSettingsSource.includes("AudioManager") ||
        uiSettingsSource.includes("VolumeSFX") ||
        uiSettingsSource.includes("VolumeBGM")) {
      fail("UISettings 仍保留 StackCraft GameOptionsUI / GraphicsManager / AudioManager 旧结构残留。");
    }
  }
  if (foundationTitleSceneMenuSource == null) {
    fail("缺少 FoundationTitleTestSceneMenu，无法证明设置面板测试入口生成链。");
  } else {
    assertCsharpBlockContainsOrdered(
      foundationTitleSceneMenuSource,
      "private static void EnsureSettingsPanelPrefab",
      [
        "GameObject root = new(\"UISettings\", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(UISettings));",
        "Button resolution = CreateSettingsOptionButton(",
        "\"Resolution\"",
        "Button fullscreen = CreateSettingsOptionButton(",
        "\"Fullscreen\"",
        "Button vSync = CreateSettingsOptionButton(",
        "\"VSync\"",
        "Button frameRate = CreateSettingsOptionButton(",
        "\"FrameRate\"",
        "Button shadow = CreateSettingsOptionButton(",
        "\"Shadow\"",
        "UISettingsMasterVolume masterVolume = CreateSettingsMasterVolumeRow",
        "EAudioChannel.GameplaySoundFX",
        "EAudioChannel.InterfaceSoundFX",
        "EAudioChannel.BackgroundMusic",
        "Button reset = CreateSavePanelButton(\"ResetSettings\"",
        "Button close = CreateSavePanelButton(\"Close\"",
      ],
      "FoundationTitleTestSceneMenu 生成 StackCraft GameOptionsUI 等价设置面板入口");
    assertCsharpBlockContainsOrdered(
      foundationTitleSceneMenuSource,
      "private static void EnsureSettingsPanelPrefab",
      [
        "serializedSettings.FindProperty(\"m_masterVolume\").objectReferenceValue = masterVolume;",
        "channelVolumes.arraySize = 3;",
        "channelVolumes.GetArrayElementAtIndex(0).objectReferenceValue = gameplaySoundVolume;",
        "channelVolumes.GetArrayElementAtIndex(1).objectReferenceValue = interfaceSoundVolume;",
        "channelVolumes.GetArrayElementAtIndex(2).objectReferenceValue = backgroundMusicVolume;",
        "serializedSettings.FindProperty(\"m_resolutionButton\").objectReferenceValue = resolution;",
        "serializedSettings.FindProperty(\"m_fullscreenButton\").objectReferenceValue = fullscreen;",
        "serializedSettings.FindProperty(\"m_vSyncButton\").objectReferenceValue = vSync;",
        "serializedSettings.FindProperty(\"m_frameRateButton\").objectReferenceValue = frameRate;",
        "serializedSettings.FindProperty(\"m_shadowButton\").objectReferenceValue = shadow;",
        "serializedSettings.FindProperty(\"m_resetSettingsButton\").objectReferenceValue = reset;",
      ],
      "FoundationTitleTestSceneMenu 写入 UISettings 正式字段引用");
  }
  if (scenarioStartOptionsSource == null) {
    fail("缺少 ScenarioStartOptions，无法证明 StackCraft GameplayPrefs 已由正式开局选项替换。");
  } else {
    assertCsharpDeclarationAndBlockContainsOrdered(
      scenarioStartOptionsSource,
      "public readonly struct ScenarioStartOptions",
      [
        "public readonly struct ScenarioStartOptions",
        "public bool FriendlyMode { get; }",
        "public float? DayDurationSecondsOverride { get; }",
        "public ScenarioStartOptions(bool friendlyMode, float? dayDurationSecondsOverride)",
        "dayDurationSecondsOverride.HasValue",
        "dayDurationSecondsOverride.Value <= 0f",
        "FriendlyMode = friendlyMode;",
        "DayDurationSecondsOverride = dayDurationSecondsOverride;",
      ],
      "ScenarioStartOptions 接管 StackCraft GameplayPrefs 日长和友好模式数据结构");
    if (scenarioStartOptionsSource.includes("GameplayPrefs")) {
      fail("ScenarioStartOptions 不得保留 StackCraft GameplayPrefs 旧 DTO 名称。");
    }
  }
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private static float ResolveSecondsPerTurn",
    [
      "if (startOptions.DayDurationSecondsOverride.HasValue)",
      "return startOptions.DayDurationSecondsOverride.Value / definition.TurnsPerDay;",
      "return definition.SecondsPerTurn;",
    ],
    "ScenarioRun 用开局日长覆盖换算每回合秒数，不恢复 StackCraft TimeManager 日长真相");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private bool IsBlockedByFriendlyMode",
    [
      "if (!FriendlyMode)",
      "m_contentIndex.TryGet(encounter.CardId, out CardDefinition card)",
      "TagHelper.HasTag(card.TagCodes[i], XTag.Faction_Enemy)",
    ],
    "ScenarioRun 用友好模式跳过敌对日终遭遇，接管 StackCraft GameplayPrefs.IsFriendlyMode 消费链");
  assertCsharpDeclarationAndBlockContainsOrdered(
    scenarioRunSnapshotSource,
    "public sealed class ScenarioRunSnapshot",
    [
      "private bool m_friendlyMode;",
      "private bool m_hasDayDurationSecondsOverride;",
      "private float m_dayDurationSecondsOverride;",
      "public ScenarioStartOptions StartOptions => new ScenarioStartOptions(",
      "m_friendlyMode",
      "m_hasDayDurationSecondsOverride ? m_dayDurationSecondsOverride : null",
      "m_friendlyMode = startOptions.FriendlyMode;",
      "m_hasDayDurationSecondsOverride = startOptions.DayDurationSecondsOverride.HasValue;",
      "m_dayDurationSecondsOverride = startOptions.DayDurationSecondsOverride ?? 0f;",
    ],
    "ScenarioRunSnapshot 持久化 StackCraft GameplayPrefs 对应的友好模式和日长开局事实");
  assertCsharpBlockContainsOrdered(
    foundationTitleSceneMenuSource,
    "private static Slider CreateTitleDayDurationSlider",
    [
      "GameObject root = new(\"DayDuration\"",
      "label = CreatePanelText(\"Label\", rootRect, font, \"日长：120 秒\", 32f);",
      "slider.minValue = 60f;",
      "slider.maxValue = 180f;",
      "slider.wholeNumbers = true;",
      "slider.value = 120f;",
    ],
    "FoundationTitleTestSceneMenu 生成标题日长滑条并写入 StackCraft GameplayPrefsUI 等价入口");
  assertCsharpBlockContainsOrdered(
    foundationTitleSceneMenuSource,
    "private static Toggle CreateTitleToggle",
    [
      "GameObject root = new(name, typeof(RectTransform), typeof(Toggle), typeof(UINavigationTarget));",
      "TextMeshProUGUI label = CreatePanelText(\"Label\", rootRect, font, text, 36f);",
      "toggle.isOn = false;",
    ],
    "FoundationTitleTestSceneMenu 生成标题友好模式开关并写入 StackCraft GameplayPrefsUI 等价入口");
  assertCsharpBlockContainsOrdered(
    scenarioSavePanelSource,
    "public void RefreshSlots",
    [
      "IReadOnlyList<SaveMeta> metadata = SaveSystem.GetAllSaveMetadata();",
      "Instantiate(m_slotTemplate, m_slotRoot)",
      "slot.Bind(metadata[i], m_mode, HandlePrimary, ConfirmDelete);",
      "m_emptyState.SetActive(!hasSaves);",
      "m_clearAllButton.interactable = hasSaves;",
      "m_createSaveButton.interactable =",
    ],
    "ScenarioSavePanel 动态槽位列表接管 StackCraft SavedGamesUI 列表生成");
  assertCsharpBlockContainsOrdered(
    scenarioSavePanelSource,
    "private void HandlePrimary",
    [
      "if (m_mode == ScenarioSavePanelMode.Save)",
      "SaveToSlot(slotId);",
      "RunOperation(LoadSlotAsync(slotId));",
    ],
    "ScenarioSavePanel 主按钮接管 StackCraft SavedGameSlot 读取 / 覆盖");
  assertCsharpBlockContainsOrdered(
    scenarioSavePanelSource,
    "private void ConfirmDelete",
    [
      "DialogConfig.Confirm(",
      "$\"确定删除槽位 {slotId + 1:D2} 吗？删除后无法恢复。\"",
      "SaveSystem.DeleteSaveData(slotId)",
      "RefreshSlots();",
    ],
    "ScenarioSavePanel 单槽删除确认接管 StackCraft SavedGameSlot 删除确认");
  assertCsharpBlockContainsOrdered(
    scenarioSavePanelSource,
    "private void ConfirmClearAll",
    [
      "DialogConfig.Confirm(",
      "\"确定删除全部存档吗？所有单局记录都会永久消失。\"",
      "SaveSystem.DeleteAllSaveData();",
      "RefreshSlots();",
    ],
    "ScenarioSavePanel 清空全部接管 StackCraft SavedGamesUI ClearSavedGames");
  assertCsharpBlockContainsOrdered(
    scenarioSavePanelSource,
    "private async UniTask SaveAndExitAsync",
    [
      "director.SaveActiveRunToSlot(slotId)",
      "await director.EndScenarioAsync();",
      "CloseSelf();",
    ],
    "ScenarioSavePanel 保存并退出接管 StackCraft BackToTitle");
  assertCsharpBlockContainsOrdered(
    scenarioSaveSlotViewSource,
    "public void Bind",
    [
      "m_slotId = metadata.SlotId;",
      "string displayName = string.IsNullOrWhiteSpace(metadata.DisplayName)",
      "m_summaryLabel.text =",
      "$\"槽位 {metadata.SlotId + 1:D2}\\n{displayName}\\n{metadata.GetLastSavedDateTime():yyyy-MM-dd HH:mm}\";",
      "m_primaryLabel.text = mode == ScenarioSavePanelMode.Save ? \"覆盖\" : \"读取\";",
      "m_primaryButton.onClick.AddListener(InvokePrimary);",
      "m_deleteButton.onClick.AddListener(InvokeDelete);",
    ],
    "ScenarioSaveSlotView 用 SaveMeta 接管 StackCraft SavedGameSlot 摘要、读取和删除按钮");
  if (scenarioTitleScreenPlayModeTestsSource == null || scenarioRunEditModeTestsSource == null) {
    fail("缺少标题 / 单局偏好回归测试，无法证明 StackCraft GameplayPrefsUI 偏好链有行为保护。");
  } else {
    assertCsharpMethodContainsOrdered(
      scenarioTitleScreenPlayModeTestsSource,
      "TitlePanel_FriendlyModeStartsScenarioWithFriendlyOption",
      [
        "FindToggle(panel, \"FriendlyMode\").isOn = true;",
        "Assert.That(director.ActiveRun.FriendlyMode, Is.True);",
      ],
      "ScenarioTitleScreenPlayModeTests 覆盖 StackCraft GameplayPrefsUI 友好模式玩家入口");
    assertCsharpMethodContainsOrdered(
      scenarioTitleScreenPlayModeTestsSource,
      "TitlePanel_DayDurationSliderStartsScenarioWithSelectedDayLength",
      [
        "FindSlider(panel, \"DayDuration\").value = 90f;",
        "director.ActiveRun.SecondsPerTurn",
        "90f / director.ActiveRun.TurnsPerDay",
      ],
      "ScenarioTitleScreenPlayModeTests 覆盖 StackCraft GameplayPrefsUI 友好模式和日长滑条玩家入口");
    assertCsharpMethodContainsOrdered(
      scenarioRunEditModeTestsSource,
      "DayCycle_FriendlyModeSkipsEnemyTaggedEncounterAndPersistsThroughSnapshot",
      [
        "new ScenarioStartOptions(friendlyMode: true)",
        "Assert.That(restoredFriendlyRun.FriendlyMode, Is.True);",
      ],
      "ScenarioRunEditModeTests 覆盖 StackCraft GameplayPrefs 友好模式规则和快照链");
    assertCsharpMethodContainsOrdered(
      scenarioRunEditModeTestsSource,
      "StartOptions_DayDurationOverrideDefinesPerTurnSeconds",
      [
        "new ScenarioStartOptions(friendlyMode: false, dayDurationSecondsOverride: 20f)",
      ],
      "ScenarioRunEditModeTests 覆盖 StackCraft GameplayPrefs 日长规则链");
    assertCsharpMethodsExist(
      scenarioRunEditModeTestsSource,
      [
        "Snapshot_PersistsDayDurationOverrideAndRestoresRealtimeProgressAgainstIt",
      ],
      "ScenarioRunEditModeTests 覆盖 StackCraft GameplayPrefs 日长快照链");
  }
  if (scenarioTitleScreenPlayModeTestsSource == null ||
      foundationTestScenePlayModeTestsSource == null) {
    fail("缺少标题 / 暂停设置面板 PlayMode 覆盖，无法证明 StackCraft GameOptionsUI 入口由当前 UIKit 链承接。");
  } else {
    assertCsharpMethodContainsOrdered(
      scenarioTitleScreenPlayModeTestsSource,
      "TitlePanel_SettingsAndQuitUseExistingUIKitPanels",
      [
        "FindButton(panel, \"Settings\").onClick.Invoke();",
        "UISettings settings = null;",
        "Assert.That(GameManager.HasSystem<DisplaySettingsSystem>(), Is.True);",
        "FindButton(settings, \"Resolution\").interactable",
        "FindButton(settings, \"Fullscreen\").interactable",
        "FindButton(settings, \"VSync\").interactable",
        "FindButton(settings, \"FrameRate\").interactable",
        "FindButton(settings, \"Shadow\").interactable",
        "FindButton(settings, \"ResetSettings\").onClick.Invoke();",
        "ConfirmationDialogPanel resetDialog = null;",
        "FindButton(settings, \"Close\").onClick.Invoke();",
      ],
      "ScenarioTitleScreenPlayModeTests 覆盖 StackCraft GameOptionsUI 标题设置入口");
    assertCsharpMethodContainsOrdered(
      foundationTestScenePlayModeTestsSource,
      "FoundationMenu_PauseSettingsAndContinueUseFormalMenuStack",
      [
        "Button settingsButton = pausePanel.GetComponentsInChildren<Button>(includeInactive: true)",
        ".Single(button => button.gameObject.name == \"Settings\");",
        "UISettings settingsPanel = null;",
        "暂停菜单点击设置后，没有压入正式设置面板。",
        "Button closeSettingsButton = settingsPanel.GetComponentsInChildren<Button>(includeInactive: true)",
        ".Single(button => button.gameObject.name == \"Close\");",
        "设置面板关闭后没有回到暂停菜单栈顶。",
      ],
      "FoundationTestScenePlayModeTests 覆盖 StackCraft PauseMenu -> GameOptionsUI 设置入口替代链");
  }
  for (const [label, sourceText] of [
    ["ScenarioTitleScreen", scenarioTitleScreenSource],
    ["ScenarioTitlePanel", scenarioTitlePanelSource],
    ["ScenarioSavePanel", scenarioSavePanelSource],
    ["ScenarioSaveSlotView", scenarioSaveSlotViewSource],
    ["UISettings", uiSettingsSource],
  ]) {
    for (const obsoleteToken of [
      "SavedGamesUI",
      "SavedGameSlot",
      "GameData",
      "SceneData",
      "SavedGames",
      "SaveSlot{",
      "LoadAllValidData",
      "DeleteSave(",
      "GameplayPrefs",
    ]) {
      if (sourceText.includes(obsoleteToken)) {
        fail(`${label} 仍保留 StackCraft 存档 UI / DTO 旧结构残留：${obsoleteToken}`);
      }
    }
  }
}

if (saveSystemModuleStorageEditModeTestsSource == null ||
    scenarioDirectorEditModeTestsSource == null ||
    scenarioSavePanelPlayModeTestsSource == null ||
    scenarioTitleScreenPlayModeTestsSource == null) {
  fail("缺少存档链回归测试源码，无法证明 StackCraft 保存 / 读取 / UI 流程有当前框架保护。");
} else {
  assertCsharpMethodContainsOrdered(
    saveSystemModuleStorageEditModeTestsSource,
    "SaveContainer_RoundTripsIndependentModulesAndSlotMetadata",
    [
      "SaveSystem.StoreSaveDataToFile(2, container, \"荒岛 · 第 4 天\")",
      "SaveSystem.ExtractSaveContainerFromFile(2)",
      "SaveSystem.GetSaveMetadata(2)",
    ],
    "GameCore SaveSystemModuleStorageEditModeTests 覆盖槽位容器和元数据职责");
  assertCsharpMethodContainsOrdered(
    saveSystemModuleStorageEditModeTestsSource,
    "SaveSlots_AreEnumeratedInSlotOrderAndDeletedThroughOneFileOwner",
    [
      "SaveSystem.GetAllSaveMetadata().Select(metadata => metadata.SlotId)",
      "SaveSystem.DeleteSaveData(5)",
      "SaveSystem.DeleteAllSaveData()",
    ],
    "GameCore SaveSystemModuleStorageEditModeTests 覆盖槽位排序和删除职责");
  assertCsharpMethodContainsOrdered(
    scenarioDirectorEditModeTestsSource,
    "SaveActiveRunToSlot_WritesWholeRunSnapshotAndDerivedMetadata",
    [
      "container.GetModule<ScenarioRunSnapshot>()",
      "container.GetModule<IndependentSaveProbe>().Value",
    ],
    "ScenarioDirectorEditModeTests 覆盖整局保存和派生元数据替代链");
  assertCsharpMethodsExist(
    scenarioDirectorEditModeTestsSource,
    [
      "ContinueDayCycle_StartsNewDayAndOverwritesTheRunsAssignedSlot",
      "RestoreRunFromSaveContainer_RejectsMissingScenarioModuleWithoutReplacingActiveRun",
      "RestoreRunFromSaveContainer_ReplacesActiveRunOnlyAfterWholeSnapshotIsValid",
    ],
    "ScenarioDirectorEditModeTests 覆盖新日自动保存和原子读取替代链");
  assertCsharpMethodContainsOrdered(
    scenarioSavePanelPlayModeTestsSource,
    "SaveOverwriteAndLoad_UsesOneDynamicSlotList",
    [
      "Assert.That(panel.DisplayedSlotCount, Is.Zero);",
      "FindButton(panel, \"CreateSave\").onClick.Invoke();",
      "FindButton(panel, \"Primary\").onClick.Invoke();",
    ],
    "ScenarioSavePanelPlayModeTests 覆盖动态列表、覆盖和读取玩家流程");
  assertCsharpMethodContainsOrdered(
    scenarioSavePanelPlayModeTestsSource,
    "DeleteConfirmClearAndSaveExit_CompleteThroughUIKit",
    [
      "FindButton(panel, \"Delete\").onClick.Invoke();",
      "FindButton(panel, \"ClearAll\").onClick.Invoke();",
      "FindButton(panel, \"SaveAndExit\").onClick.Invoke();",
    ],
    "ScenarioSavePanelPlayModeTests 覆盖删除、清空和保存退出玩家流程");
  assertCsharpMethodContainsOrdered(
    scenarioTitleScreenPlayModeTestsSource,
    "TitlePanel_ExposesTemplateCommandsThroughUIKit",
    [
      "FindButton(panel, \"LoadGame\").onClick.Invoke();",
      "ScenarioSavePanel savePanel = null;",
    ],
    "ScenarioTitleScreenPlayModeTests 覆盖 StackCraft TitleScreen 读取命令替代链");
  assertCsharpMethodContainsOrdered(
    scenarioTitleScreenPlayModeTestsSource,
    "TitlePanel_SettingsAndQuitUseExistingUIKitPanels",
    [
      "FindButton(panel, \"Settings\").onClick.Invoke();",
      "FindButton(panel, \"Quit\").onClick.Invoke();",
    ],
    "ScenarioTitleScreenPlayModeTests 覆盖 StackCraft TitleScreen 设置和退出命令替代链");
  assertCsharpMethodContainsOrdered(
    scenarioTitleScreenPlayModeTestsSource,
    "TitlePanel_NewGameStartsScenarioAndLeavesTitle",
    [
      "FindButton(panel, \"NewGame\").onClick.Invoke();",
    ],
    "ScenarioTitleScreenPlayModeTests 覆盖 StackCraft TitleScreen 新局命令替代链");
}

if (sceneSystemSource == null) {
  fail("缺少 GameCore SceneSystem 源码，无法证明 StackCraft ScreenFader 旧转场由正式场景系统承接。");
} else {
  assertCsharpBlockContainsOrdered(
    sceneSystemSource,
    "public sealed class SceneSystem : AGameSystem",
    [
      "new[] { typeof(TransitionSystem) };",
      "public async UniTask TransitionToAsync(",
      "TransitionSystem transitionSystem = GetRequiredTransitionSystem();",
      "EventKit.Type.Send(new SceneTransitionStartedEvent());",
      "await transitionSystem.FadeOutUniTaskAsync(destroyCancellationToken);",
      "SceneHandler loadedScene = await SceneKit.LoadSceneUniTaskAsync(",
      "EventKit.Type.Send(new SceneLoadedEvent());",
      "await transitionSystem.FadeInUniTaskAsync(destroyCancellationToken);",
      "EventKit.Type.Send(new SceneTransitionCompletedEvent());",
      "EventKit.Type.Send(new SceneTransitionEndedEvent());",
    ],
    "GameCore SceneSystem 承接 StackCraft TravelSequence 的正式转场时序");
  for (const obsoleteToken of [
    "ScreenFader",
    "CryingSnow.StackCraft",
    "SceneManager.LoadScene(",
    "SceneManager.LoadSceneAsync(",
  ]) {
    if (sceneSystemSource.includes(obsoleteToken)) {
      fail(`GameCore SceneSystem 仍保留 StackCraft 旧转场链路残留：${obsoleteToken}`);
    }
  }
}

if (transitionSystemSource == null) {
  fail("缺少 GameCore TransitionSystem 源码，无法证明全屏淡入淡出由正式过场表现 owner 承接。");
} else {
  assertCsharpBlockContainsOrdered(
    transitionSystemSource,
    "public class TransitionSystem : AGameSystem, ITransitionAnimationStateReceiver, ISceneTransitionUniTask",
    [
      "public async UniTask FadeOutUniTaskAsync(CancellationToken cancellationToken = default)",
      "await PlayFadeOutAsync(cancellationToken);",
      "m_progress = 0.5f;",
      "public async UniTask FadeInUniTaskAsync(CancellationToken cancellationToken = default)",
      "await PlayFadeInAsync(cancellationToken);",
      "m_progress = 1f;",
      "m_transitionInProgress = false;",
    ],
    "GameCore TransitionSystem 承接 StackCraft ScreenFader 的淡出 / 淡入表现职责");
  assertCsharpBlockContainsOrdered(
    transitionSystemSource,
    "private async UniTask PlayFadeOutAsync",
    [
      "if (m_isBlackScreen)",
      "if (!m_hasFadeOutAnimation)",
      "m_isBlackScreen = true;",
      "m_animator.SetTrigger(m_fadeOutAnimationParameter);",
      "await m_fadeOutCompletion.Task.AttachExternalCancellation(cancellationToken);",
    ],
    "GameCore TransitionSystem 淡出播放入口");
  assertCsharpBlockContainsOrdered(
    transitionSystemSource,
    "private async UniTask PlayFadeInAsync",
    [
      "if (!m_isBlackScreen)",
      "if (!m_hasFadeInAnimation)",
      "m_isBlackScreen = false;",
      "m_animator.SetTrigger(m_fadeInAnimationParameter);",
      "await m_fadeInCompletion.Task.AttachExternalCancellation(cancellationToken);",
    ],
    "GameCore TransitionSystem 淡入播放入口");
  for (const obsoleteToken of [
    "ScreenFader",
    "CryingSnow.StackCraft",
    "CanvasGroup",
    "SceneManager.LoadScene",
  ]) {
    if (transitionSystemSource.includes(obsoleteToken)) {
      fail(`GameCore TransitionSystem 仍保留 StackCraft 旧 ScreenFader 结构残留：${obsoleteToken}`);
    }
  }
}

if (scenarioScreenEffectViewSource == null) {
  fail("缺少 ScenarioScreenEffectView 源码，无法证明局内暂停灰阶 / 日终暗角没有误归入 ScreenFader。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioScreenEffectViewSource,
    "public sealed class ScenarioScreenEffectView : MonoBehaviour",
    [
      "private Volume m_volume;",
      "private float m_pauseGrayscaleFadeSeconds = 0.3f;",
      "private float m_pauseGrayscaleTarget = 1f;",
      "private float m_dayVignetteFadeSeconds = 0.5f;",
      "private float m_dayVignetteTarget = 0.45f;",
      "private ColorAdjustments m_colorAdjustments;",
      "private Vignette m_vignette;",
    ],
    "ScenarioScreenEffectView 承接 StackCraft 暂停灰阶和日终暗角配置字段");
  assertCsharpBlockContainsOrdered(
    scenarioScreenEffectViewSource,
    "private void Update()",
    [
      "float targetGrayscale = ShouldShowPauseGrayscale() ? m_pauseGrayscaleTarget : 0f;",
      "float targetVignette = ShouldShowDayVignette() ? m_dayVignetteTarget : 0f;",
      "MoveToward(m_grayscaleAmount, targetGrayscale, m_pauseGrayscaleFadeSeconds)",
      "MoveToward(m_vignetteIntensity, targetVignette, m_dayVignetteFadeSeconds)",
      "ApplyEffects();",
    ],
    "ScenarioScreenEffectView 按正式状态投影暂停灰阶和日终暗角");
  assertCsharpBlockContainsOrdered(
    scenarioScreenEffectViewSource,
    "private bool ShouldShowPauseGrayscale()",
    [
      "GameManager.HasSystem<GameStateSystem>()",
      "GameManager.GameStateSystem.currentState == EGameState.Menu",
    ],
    "ScenarioScreenEffectView 暂停灰阶只读正式菜单状态");
  assertCsharpBlockContainsOrdered(
    scenarioScreenEffectViewSource,
    "private bool ShouldShowDayVignette()",
    [
      "GameManager.TryGetSystem(out ScenarioDirector director)",
      "!director.HasActiveScenario",
      "director.ActiveRun.DayCyclePhase != ScenarioDayCyclePhase.Inactive",
    ],
    "ScenarioScreenEffectView 日终暗角只读正式剧本日程状态");
  assertCsharpBlockContainsOrdered(
    scenarioScreenEffectViewSource,
    "private void ResolveVolumeOverrides()",
    [
      "throw new InvalidOperationException(\"剧本屏幕效果缺少后处理 Volume。\")",
      "throw new InvalidOperationException(\"剧本屏幕效果的 Volume 缺少 Profile。\")",
      "throw new InvalidOperationException(\"剧本屏幕效果 Profile 缺少 ColorAdjustments。\")",
      "throw new InvalidOperationException(\"剧本屏幕效果 Profile 缺少 Vignette。\")",
    ],
    "ScenarioScreenEffectView 缺少正式后处理作者源时直接暴露错误");
  for (const obsoleteToken of [
    "ScreenFader",
    "CryingSnow.StackCraft",
    "TimeManager",
    "SetExternalPause",
    "SceneManager.LoadScene",
  ]) {
    if (scenarioScreenEffectViewSource.includes(obsoleteToken)) {
      fail(`ScenarioScreenEffectView 仍保留 StackCraft 旧 ScreenFader / TimeManager 链路残留：${obsoleteToken}`);
    }
  }
}

if (scenarioDirectorSource == null) {
  fail("缺少剧本导演源码，无法证明 StackCraft 卡包商贩解锁序列由正式生命周期入口串行播放。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "public async UniTask StartScenarioAsync(\n\t\t\tContentId scenarioId,\n\t\t\tScenarioStartOptions startOptions,\n\t\t\tuint authoritativeRandomSeed)",
    [
      "RequireRunningSystem();",
      "string sourceSceneAddress = SceneManager.GetActiveScene().name;",
      "await GameManager.SceneSystem.TransitionToAsync(initialRegionDefinition.SceneAddress);",
      "ScenarioRun run = new ScenarioRun(",
      "EventKit.Type.Send(new ScenarioRunChangedEvent(null, run));",
    ],
    "剧本导演开局切场景通过正式 SceneSystem 承接 StackCraft TravelSequence");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "public async UniTask EndScenarioAsync()",
    [
      "ReleaseActiveRun();",
      "await GameManager.SceneSystem.TransitionToAsync(returnSceneAddress);",
      "m_returnSceneAddress = string.Empty;",
    ],
    "剧本导演结束单局通过正式 SceneSystem 返回来源场景");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "public async UniTask LoadRunFromSlotAsync",
    [
      "string targetSceneAddress = GetActiveRegionSceneAddress(restoredRun);",
      "await GameManager.SceneSystem.TransitionToAsync(targetSceneAddress);",
      "RequireRunningSystem();",
      "ScenarioRun previousRun = ReplaceActiveRun(restoredRun);",
    ],
    "剧本导演读档切场景通过正式 SceneSystem 串行提交单局");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "public async UniTask TravelAsync",
    [
      "ScenarioTravelPlan travel = run.BeginTravel(targetRegionId, travelerCardIds);",
      "await GameManager.SceneSystem.TransitionToAsync(",
      "travel.TargetSceneAddress",
      "travel.Commit",
      "run.CancelTravel(travel);",
    ],
    "剧本导演旅行切场景通过正式 SceneSystem 和旅行事务承接 StackCraft TravelSequence");
  for (const obsoleteToken of [
    "ScreenFader",
    "CryingSnow.StackCraft",
    "TimeManager",
    "SetExternalPause",
    "SceneManager.LoadScene(",
    "SceneManager.LoadSceneAsync(",
  ]) {
    if (scenarioDirectorSource.includes(obsoleteToken)) {
      fail(`剧本导演仍保留 StackCraft 旧转场链路残留：${obsoleteToken}`);
    }
  }
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "public sealed class ScenarioDirector",
    [
      "private readonly Queue<ScenarioSequencePresentationRequestEvent> m_sequencePresentationQueue",
      "new Queue<ScenarioSequencePresentationRequestEvent>();",
    ],
    "剧本导演 StackCraft 商贩解锁序列播放队列字段");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "private void OnScenarioSequencePresentationRequested",
    [
      "!HasActiveScenario || request.ScenarioId != ActiveScenarioId",
      "m_sequencePresentationQueue.Enqueue(request)",
      "m_sequencePresentationCoroutine = StartCoroutine(PlaySequencePresentationQueue())",
    ],
    "剧本导演接收 StackCraft 商贩解锁序列请求方法");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "private IEnumerator PlaySequencePresentationQueue",
    [
      "while (m_sequencePresentationQueue.Count > 0)",
      "ScenarioSequencePresentationRequestEvent request = m_sequencePresentationQueue.Dequeue()",
      "AcquireSequenceLocks();",
      "EventKit.Type.Send(new ScenarioSequenceMessageEvent(",
      "request.Header",
      "request.Body",
      "request.DurationSeconds",
      "m_activeRun.Tabletop.RequestPresentationCue(TabletopPresentationCue.AtTablePosition(",
      "TabletopPresentationCueKind.CameraFocus",
      "m_activeRun.Tabletop.RequestPresentationCue(TabletopPresentationCue.AtCard(",
      "TabletopPresentationCueKind.CardHighlight",
      "float expiresAt = Time.realtimeSinceStartup + request.DurationSeconds",
      "yield return null;",
      "ReleaseSequenceLocks();",
      "m_sequencePresentationCoroutine = null;",
    ],
    "剧本导演串行播放 StackCraft 商贩解锁序列方法");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "private void AcquireSequenceLocks",
    [
      "GameManager.InputSystem.AddGameplayInputLock(SequenceInputLockRequester);",
      "m_sequenceInputLockHeld = true;",
      "GameManager.GameStateSystem.AddExternalPauseLock(SequencePauseLockRequester);",
      "m_sequencePauseLockHeld = true;",
    ],
    "剧本导演获取 StackCraft 解锁序列输入和暂停锁方法");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "private void ReleaseSequenceLocks",
    [
      "GameManager.TryGetSystem(out GameCore.InputSystem inputSystem)",
      "inputSystem.RemoveGameplayInputLock(SequenceInputLockRequester);",
      "m_sequenceInputLockHeld = false;",
      "GameManager.TryGetSystem(out GameStateSystem gameStateSystem)",
      "gameStateSystem.RemoveExternalPauseLock(SequencePauseLockRequester);",
      "m_sequencePauseLockHeld = false;",
    ],
    "剧本导演释放 StackCraft 解锁序列输入和暂停锁方法");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "private void RegisterSequencePresentationEvents",
    [
      "EventKit.Type.Register<ScenarioSequencePresentationRequestEvent>(OnScenarioSequencePresentationRequested);",
      "m_sequencePresentationSubscribed = true;",
    ],
    "剧本导演注册 StackCraft 解锁序列请求事件方法");
  assertCsharpBlockContainsOrdered(
    scenarioDirectorSource,
    "private void UnregisterSequencePresentationEvents",
    [
      "EventKit.Type.UnRegister<ScenarioSequencePresentationRequestEvent>(OnScenarioSequencePresentationRequested);",
      "m_sequencePresentationSubscribed = false;",
    ],
    "剧本导演注销 StackCraft 解锁序列请求事件方法");
}

const inputSystemSource = readIfExists("Assets/Scripts/GameCore/Runtime/Game/Systems/InputSystem.cs");
if (inputSystemSource == null) {
  fail("缺少 GameCore 输入系统源码，无法证明商贩解锁序列没有恢复 StackCraft InputManager 单例。");
} else {
  assertCsharpBlockContainsOrdered(
    inputSystemSource,
    "public class InputSystem : AGameSystem",
    [
      "private readonly HashSet<object> m_gameplayInputLocks = new();",
      "public bool IsGameplayInputLocked => m_gameplayInputLocks.Count > 0;",
      "public void AddGameplayInputLock(object requester)",
      "public void RemoveGameplayInputLock(object requester)",
      "public bool IsGameplayActionBlocked(EGameplayInputAction action)",
    ],
    "GameCore 输入系统正式 Gameplay 输入锁类结构");
  assertCsharpBlockContainsOrdered(
    inputSystemSource,
    "public override void OnSystemStop",
    [
      "m_gameplayInputLocks.Clear();",
      "ClearExternalInputActionListeners();",
    ],
    "GameCore 输入系统停止时释放 Gameplay 输入锁方法");
  assertCsharpBlockContainsOrdered(
    inputSystemSource,
    "public override void OnSystemShutdown",
    [
      "m_gameplayInputLocks.Clear();",
      "ClearExternalInputActionListeners();",
      "m_isInitialized = false;",
    ],
    "GameCore 输入系统关闭时释放 Gameplay 输入锁方法");
  assertCsharpBlockContainsOrdered(
    inputSystemSource,
    "public void AddGameplayInputLock",
    [
      "requester == null",
      "throw new ArgumentNullException(nameof(requester));",
      "!m_gameplayInputLocks.Add(requester)",
      "同一个请求方重复锁定 Gameplay 输入",
    ],
    "GameCore 输入系统申请 Gameplay 输入锁方法");
  assertCsharpBlockContainsOrdered(
    inputSystemSource,
    "public void RemoveGameplayInputLock",
    [
      "requester == null",
      "throw new ArgumentNullException(nameof(requester));",
      "!m_gameplayInputLocks.Remove(requester)",
      "请求方释放了并未持有的 Gameplay 输入锁",
    ],
    "GameCore 输入系统释放 Gameplay 输入锁方法");
  assertCsharpBlockContainsOrdered(
    inputSystemSource,
    "public bool IsGameplayActionBlocked",
    [
      "if (!m_isInitialized)",
      "return IsGameplayInputLocked;",
      "return IsGameplayInputLocked || IsBlocked(GetGameplayAction(action));",
    ],
    "GameCore 输入系统按正式锁阻挡 Gameplay Action 方法");
}

const gameStateSystemSource = readIfExists("Assets/Scripts/GameCore/Runtime/Game/Systems/GameStateSystem.cs");
if (gameStateSystemSource == null) {
  fail("缺少 GameCore 游戏状态系统源码，无法证明商贩解锁序列没有恢复 StackCraft TimeManager 单例。");
} else {
  assertCsharpBlockContainsOrdered(
    gameStateSystemSource,
    "public class GameStateSystem : AGameSystem",
    [
      "private readonly HashSet<object> m_externalPauseLocks = new();",
      "public bool IsExternallyPaused => m_externalPauseLocks.Count > 0;",
      "public void AddExternalPauseLock(object requester)",
      "public void RemoveExternalPauseLock(object requester)",
      "private void ApplyTimeScaleForCurrentState()",
    ],
    "GameCore 游戏状态系统正式外部暂停锁类结构");
  assertCsharpBlockContainsOrdered(
    gameStateSystemSource,
    "public override void OnSystemStart",
    [
      "m_stateStack.Clear();",
      "m_externalPauseLocks.Clear();",
      "AddLayer(m_startupState);",
    ],
    "GameCore 游戏状态系统启动时重建暂停状态方法");
  assertCsharpBlockContainsOrdered(
    gameStateSystemSource,
    "public override void OnSystemStop",
    [
      "m_stateStack.Clear();",
      "m_externalPauseLocks.Clear();",
      "Time.timeScale = 1.0f;",
    ],
    "GameCore 游戏状态系统停止时释放暂停锁方法");
  assertCsharpBlockContainsOrdered(
    gameStateSystemSource,
    "public void AddExternalPauseLock",
    [
      "requester == null",
      "throw new ArgumentNullException(nameof(requester));",
      "!m_externalPauseLocks.Add(requester)",
      "同一个请求方重复申请外部暂停",
      "ApplyTimeScaleForCurrentState();",
    ],
    "GameCore 游戏状态系统申请外部暂停锁方法");
  assertCsharpBlockContainsOrdered(
    gameStateSystemSource,
    "public void RemoveExternalPauseLock",
    [
      "requester == null",
      "throw new ArgumentNullException(nameof(requester));",
      "!m_externalPauseLocks.Remove(requester)",
      "请求方释放了并未持有的外部暂停锁",
      "ApplyTimeScaleForCurrentState();",
    ],
    "GameCore 游戏状态系统释放外部暂停锁方法");
  assertCsharpBlockContainsOrdered(
    gameStateSystemSource,
    "private void ApplyTimeScaleForCurrentState",
    [
      "Time.timeScale = IsExternallyPaused || m_stateStack.Contains(EGameState.Menu)",
      "? 0.0f",
      ": 1.0f;",
    ],
    "GameCore 游戏状态系统按外部暂停锁写入时间缩放方法");
}

const scenarioTurnPanelSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/View/ScenarioTurnPanel.cs");
const scenarioPausePanelSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/View/ScenarioPausePanel.cs");
const scenarioPauseInputSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/Input/ScenarioPauseInput.cs");
const scenarioJournalPanelSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Scenarios/View/ScenarioJournalPanel.cs");
const tabletopCardInfoPanelSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopCardInfoPanel.cs");
const actionDefinitionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Actions/ActionDefinition.cs");
const questDefinitionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Quests/QuestDefinition.cs");
const questLogSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Quests/QuestLog.cs");
const questProgressSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Quests/QuestProgress.cs");
const questTaskDefinitionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Quests/QuestTaskDefinition.cs");
const questTaskRuntimeStateSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Quests/QuestTaskRuntimeState.cs");
const questLogSnapshotSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Quests/QuestLogSnapshot.cs");
const questLogEditModeTestsSource = readIfExists("Assets/Editor/Gameplay/Tests/QuestLogEditModeTests.cs");
const packVendorEditModeTestsForQuestSource = readIfExists("Assets/Editor/Gameplay/Tests/PackVendorEditModeTests.cs");
const equipmentCardEditModeTestsForQuestSource = readIfExists("Assets/Editor/Gameplay/Tests/EquipmentCardEditModeTests.cs");
const stackCraftQuestSource = readIfExists("Assets/StackCraft/Scripts/Quest/Quest.cs");
const stackCraftQuestInstanceSource = readIfExists("Assets/StackCraft/Scripts/Quest/QuestInstance.cs");
const stackCraftQuestManagerSource = readIfExists("Assets/StackCraft/Scripts/Quest/QuestManager.cs");
if (tabletopCardInfoPanelSource == null) {
  fail("缺少牌桌卡牌详情面板源码，无法证明 StackCraft InfoPanel 的流程提示优先级由正式 UI 投影。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopCardInfoPanelSource,
    "public sealed class TabletopCardInfoPanel : UIPanel",
    [
      "private TMP_Text m_infoLabel;",
      "private int m_headerSize = 34;",
      "private int m_bodySize = 30;",
      "private bool m_sequenceMessageActive;",
      "private string m_sequenceHeader = string.Empty;",
      "private string m_sequenceBody = string.Empty;",
      "private float m_sequenceMessageExpiresAt;",
      "public bool IsSequenceMessageActive => m_sequenceMessageActive;",
      "public string DisplayedSequenceHeader => m_sequenceMessageActive ? m_sequenceHeader : string.Empty;",
      "public string DisplayedSequenceBody => m_sequenceMessageActive ? m_sequenceBody : string.Empty;",
      "public string DisplayedInfoText => m_infoLabel == null ? string.Empty : m_infoLabel.text;",
    ],
    "牌桌卡牌详情面板 StackCraft InfoPanel 流程提示状态字段和只读投影");
  assertCsharpBlockContainsOrdered(
    tabletopCardInfoPanelSource,
    "protected override void OnOpen",
    [
      "m_tabletopView = panelData.TabletopView",
      "m_scenarioRun = panelData.ScenarioRun",
      "m_tabletopView.ReadableCardChanged += Refresh",
      "EventKit.Type.Register<ScenarioSequenceMessageEvent>(OnScenarioSequenceMessage);",
      "m_isSubscribed = true;",
      "Refresh();",
    ],
    "牌桌卡牌详情面板打开时绑定 StackCraft InfoPanel 流程提示方法");
  assertCsharpBlockContainsOrdered(
    tabletopCardInfoPanelSource,
    "private void Refresh",
    [
      "if (m_sequenceMessageActive)",
      "DisplaySequenceMessage();",
      "m_tabletopView.TryGetReadableCard(out TabletopCard card, out var definition)",
      "DisplayedCardId = default;",
      "ClearDisplayedInfo();",
      "m_contentRoot.SetActive(false);",
      "DisplayedCardId = card.Id;",
      "ApplyDisplayedInfo(definition.DisplayName, BuildDescription(card, definition));",
      "m_contentRoot.SetActive(true);",
    ],
    "牌桌卡牌详情面板按 StackCraft InfoPanel 优先级刷新方法");
  assertCsharpBlockContainsOrdered(
    tabletopCardInfoPanelSource,
    "private void LateUpdate",
    [
      "if (m_sequenceMessageActive)",
      "Time.realtimeSinceStartup >= m_sequenceMessageExpiresAt",
      "ClearSequenceMessage();",
      "Refresh();",
      "return;",
      "DisplayedCardId.IsValid",
      "Refresh();",
    ],
    "牌桌卡牌详情面板流程提示到期恢复方法");
  assertCsharpBlockContainsOrdered(
    tabletopCardInfoPanelSource,
    "private void OnScenarioSequenceMessage",
    [
      "messageEvent.ScenarioId.Equals(m_scenarioRun.ScenarioId)",
      "m_sequenceMessageActive = true;",
      "m_sequenceHeader = messageEvent.Header;",
      "m_sequenceBody = messageEvent.Body;",
      "m_sequenceMessageExpiresAt = Time.realtimeSinceStartup + messageEvent.DurationSeconds;",
      "Refresh();",
    ],
    "牌桌卡牌详情面板接收 StackCraft InfoPanel 流程提示方法");
  assertCsharpBlockContainsOrdered(
    tabletopCardInfoPanelSource,
    "private void DisplaySequenceMessage",
    [
      "DisplayedCardId = default;",
      "ApplyDisplayedInfo(m_sequenceHeader, m_sequenceBody);",
      "m_contentRoot.SetActive(true);",
    ],
    "牌桌卡牌详情面板显示 StackCraft InfoPanel 流程提示方法");
  assertCsharpBlockContainsOrdered(
    tabletopCardInfoPanelSource,
    "private string FormatStackCraftInfoText",
    [
      "text.Append(\"<size=\")",
      "Append(m_headerSize)",
      "Append(\"[\")",
      "Append(header)",
      "Append(\"]\\n\")",
      "Append(m_bodySize)",
      "Append(body)",
    ],
    "牌桌卡牌详情面板按 StackCraft InfoPanel.UpdateInfo 生成 rich text");
  assertCsharpBlockContainsOrdered(
    tabletopCardInfoPanelSource,
    "private void ClearSequenceMessage",
    [
      "m_sequenceMessageActive = false;",
      "m_sequenceHeader = string.Empty;",
      "m_sequenceBody = string.Empty;",
      "m_sequenceMessageExpiresAt = 0f;",
    ],
    "牌桌卡牌详情面板清理 StackCraft InfoPanel 流程提示方法");
  assertCsharpBlockContainsOrdered(
    tabletopCardInfoPanelSource,
    "private void Unbind",
    [
      "EventKit.Type.UnRegister<ScenarioSequenceMessageEvent>(OnScenarioSequenceMessage);",
      "m_isSubscribed = false;",
      "m_tabletopView.ReadableCardChanged -= Refresh;",
      "m_tabletopView = null;",
      "m_scenarioRun = null;",
      "ClearSequenceMessage();",
      "DisplayedCardId = default;",
      "m_contentRoot.SetActive(false);",
      "ClearDisplayedInfo();",
    ],
    "牌桌卡牌详情面板解绑 StackCraft InfoPanel 流程提示方法");
  if (tabletopCardInfoPanelSource.includes("m_descriptionLabel")) {
    fail("牌桌卡牌详情面板仍保留第二个正文文本字段 m_descriptionLabel，不能复刻 StackCraft 单一 InfoText。");
  }
}

if (foundationSceneMenuSource == null) {
  fail("缺少 FoundationTestSceneMenu，无法证明卡牌详情面板 Prefab 会稳定重建 StackCraft InfoPanel 形态。");
} else {
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureTabletopCardInfoPanelPrefab()",
    [
      "\"TabletopCardInfoPanel\"",
      "typeof(TabletopCardInfoPanel)",
      "\"InfoPanel\"",
      "typeof(VerticalLayoutGroup)",
      "typeof(ContentSizeFitter)",
      "new Vector2(400f, 0f)",
      "new Vector2(30f, 30f)",
      "background.color = new Color(0f, 0f, 0f, 0.9019608f);",
      "layout.childAlignment = TextAnchor.UpperLeft;",
      "fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;",
      "\"InfoText\"",
      "infoText.lineSpacing = -15f;",
      "infoText.paragraphSpacing = 20f;",
      "ApplyStackCraftTextParameters(",
      "SerializedProperty infoLabelProperty = serializedPanel.FindProperty(\"m_infoLabel\");",
      "SerializedProperty headerSizeProperty = serializedPanel.FindProperty(\"m_headerSize\");",
      "SerializedProperty bodySizeProperty = serializedPanel.FindProperty(\"m_bodySize\");",
      "contentRootProperty.objectReferenceValue = contentRoot;",
      "infoLabelProperty.objectReferenceValue = infoText;",
      "headerSizeProperty.intValue = 34;",
      "bodySizeProperty.intValue = 30;",
    ],
    "FoundationTestSceneMenu StackCraft InfoPanel 左下详情面板 Prefab 生成闭包");
}

if (actionDefinitionSource == null) {
  fail("缺少行动作者源源码，无法证明 StackCraft RecipeCategory 已由 Gameplay 行动日志分组承接。");
} else {
  assertCsharpBlockContainsOrdered(
    actionDefinitionSource,
    "public class ActionDefinition : DisplayableContentAsset",
    [
      "private string m_journalGroupName;",
      "public string JournalGroupName => m_journalGroupName ?? string.Empty;",
      "ACTION_JOURNAL_GROUP_INVALID",
    ],
    "行动作者源 StackCraft Recipes 分组字段和校验");
}

if (questDefinitionSource == null) {
  fail("缺少任务作者源源码，无法证明 StackCraft QuestGroup.GroupName 已由 Gameplay 任务日志分组承接。");
} else {
  assertCsharpBlockContainsOrdered(
    questDefinitionSource,
    "public class QuestDefinition : DisplayableContentAsset",
    [
      "private string m_journalGroupName;",
      "public string JournalGroupName => m_journalGroupName ?? string.Empty;",
      "QUEST_JOURNAL_GROUP_INVALID",
    ],
    "任务作者源 StackCraft QuestGroup 分组字段和校验");
}

if (stackCraftQuestSource == null) {
  fail("缺少 StackCraft Quest 源码，无法证明任务作者源和 QuestType 来源。");
} else {
  assertSourceContainsOrdered(
    stackCraftQuestSource,
    [
      "public enum QuestType",
      "Have",
      "Obtain",
      "Discover",
      "Defeat",
      "Craft",
      "Sell",
      "Buy",
      "Equip",
      "Explore",
      "Time",
      "Day",
      "Food",
      "Coins",
      "Capacity",
      "public class Quest : ScriptableObject",
      "private string id;",
      "private string title;",
      "private string description;",
      "private QuestType type;",
      "private CardDefinition targetCard;",
      "private RecipeDefinition targetRecipe;",
      "private int targetAmount = 1;",
      "private TimePace targetPace = TimePace.Normal;",
      "private List<Quest> prerequisiteQuests;",
      "private List<Quest> questsToUnlock;",
      "public QuestType Type => type;",
      "public CardDefinition TargetCard => targetCard;",
      "public RecipeDefinition TargetRecipe => targetRecipe;",
      "public int TargetAmount => targetAmount;",
      "public TimePace TargetPace => targetPace;",
      "public List<Quest> PrerequisiteQuests => prerequisiteQuests;",
      "public List<Quest> QuestsToUnlock => questsToUnlock;",
      "private void OnValidate()",
      "id = System.Guid.NewGuid().ToString(\"N\");",
    ],
    "StackCraft Quest 作者源、QuestType、目标字段和双向解锁字段来源结构");
}
if (stackCraftQuestInstanceSource == null) {
  fail("缺少 StackCraft QuestInstance 源码，无法证明任务运行实例来源。");
} else {
  assertSourceContainsOrdered(
    stackCraftQuestInstanceSource,
    [
      "public enum QuestStatus",
      "Inactive",
      "Active",
      "Completed",
      "public class QuestInstance",
      "public Quest QuestData { get; private set; }",
      "public QuestStatus Status { get; set; }",
      "public int CurrentAmount { get; set; }",
      "public QuestInstance(Quest questData)",
      "this.Status = QuestStatus.Inactive;",
      "this.CurrentAmount = 0;",
      "public void SetProgress(int newAmount)",
      "public void AddProgress(int amountToAdd)",
      "public bool IsComplete()",
      "CurrentAmount >= QuestData.TargetAmount",
    ],
    "StackCraft QuestInstance 运行状态和整数进度来源结构");
}
if (stackCraftQuestManagerSource == null) {
  fail("缺少 StackCraft QuestManager 源码，无法证明任务激活、保存和事件分支来源。");
} else {
  assertSourceContainsOrdered(
    stackCraftQuestManagerSource,
    [
      "public class QuestGroup",
      "public string GroupName;",
      "public List<Quest> Quests = new();",
      "public class QuestManager : MonoBehaviour",
      "public static QuestManager Instance { get; private set; }",
      "public event System.Action<QuestInstance> OnQuestActivated;",
      "public event System.Action<QuestInstance> OnQuestCompleted;",
      "private List<QuestGroup> questGroups;",
      "public IEnumerable<QuestGroup> QuestGroups => questGroups;",
      "public IEnumerable<QuestInstance> AllQuests => completedQuests.Concat(activeQuests);",
      "private readonly List<QuestInstance> activeQuests = new();",
      "private readonly HashSet<string> completedQuestIDs = new();",
      "private readonly List<QuestInstance> completedQuests = new();",
      "private Dictionary<string, Quest> questLookup = new();",
    ],
    "StackCraft QuestManager 分组、运行集合、完成集合和事件来源结构");
  assertCsharpBlockContainsOrdered(
    stackCraftQuestManagerSource,
    "private void Awake",
    [
      "BuildQuestLookup();",
      "GameDirector.Instance.OnSceneDataReady += HandleSceneDataReady;",
      "GameDirector.Instance.OnBeforeSave += HandleBeforeSave;",
    ],
    "StackCraft QuestManager 场景数据和保存事件订阅来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftQuestManagerSource,
    "private void BuildQuestLookup",
    [
      "questLookup.Clear();",
      "foreach (var group in questGroups)",
      "foreach (var quest in group.Quests)",
      "questLookup.Add(quest.Id, quest);",
      "Debug.LogError($\"QuestManager: Duplicate Quest ID detected: {quest.Id} on {quest.name}\");",
    ],
    "StackCraft QuestManager 任务 ID 查重来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftQuestManagerSource,
    "private void Start",
    [
      "CardManager.Instance.OnCardCreated += HandleCardCreated;",
      "CardManager.Instance.OnCardKilled += HandleCardKilled;",
      "CardManager.Instance.OnCardEquipped += HandleCardEquipped;",
      "CardManager.Instance.OnStatsChanged += HandleStatsChanged;",
      "CraftingManager.Instance.OnRecipeDiscovered += HandleRecipeDiscovered;",
      "CraftingManager.Instance.OnCraftingFinished += HandleCraftingFinished;",
      "CraftingManager.Instance.OnExplorationFinished += HandleExplorationFinished;",
      "TradeManager.Instance.OnCardsSold += HandleCardsSold;",
      "TradeManager.Instance.OnPackPurchased += HandlePackPurchased;",
      "TimeManager.Instance.OnTimePaceChanged += HandleTimePaceChanged;",
      "TimeManager.Instance.OnDayStarted += HandleDayChanged;",
    ],
    "StackCraft QuestManager 跨 Manager 任务事实订阅来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftQuestManagerSource,
    "private void RestoreQuests",
    [
      "activeQuests.Clear();",
      "completedQuestIDs.Clear();",
      "completedQuests.Clear();",
      "completedQuestIDs.Add(id);",
      "QuestInstance completedInstance = new QuestInstance(questDef);",
      "completedInstance.Status = QuestStatus.Completed;",
      "completedInstance.SetProgress(questDef.TargetAmount);",
      "QuestInstance instance = new QuestInstance(questDef);",
      "instance.Status = QuestStatus.Active;",
      "instance.SetProgress(saveData.CurrentAmount);",
      "OnQuestActivated?.Invoke(instance);",
    ],
    "StackCraft QuestManager 任务存档恢复来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftQuestManagerSource,
    "private void ActivateQuest",
    [
      "completedQuestIDs.Contains(questData.Id)",
      "activeQuests.Any(q => q.QuestData == questData)",
      "QuestInstance newQuest = new QuestInstance(questData);",
      "newQuest.Status = QuestStatus.Active;",
      "activeQuests.Add(newQuest);",
      "OnQuestActivated?.Invoke(newQuest);",
      "newQuest.QuestData.Type == QuestType.Have",
      "HandleStatsChanged(CardManager.Instance.GetStatsSnapshot());",
      "newQuest.QuestData.Type == QuestType.Discover",
      "foreach (var recipeId in CraftingManager.Instance.DiscoveredRecipes)",
      "HandleRecipeDiscovered(recipeId);",
    ],
    "StackCraft QuestManager 激活任务和立即刷新 Have / Discover 来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftQuestManagerSource,
    "private void CheckForCompletion",
    [
      "quest.IsComplete()",
      "quest.Status = QuestStatus.Completed;",
      "activeQuests.Remove(quest);",
      "completedQuestIDs.Add(quest.QuestData.Id);",
      "completedQuests.Add(quest);",
      "foreach (var nextQuest in quest.QuestData.QuestsToUnlock)",
      "CanActivate(nextQuest)",
      "ActivateQuest(nextQuest);",
      "OnQuestCompleted?.Invoke(quest);",
    ],
    "StackCraft QuestManager 完成任务和激活后继来源链");
  assertCsharpBlockContainsOrdered(
    stackCraftQuestManagerSource,
    "private void HandleStatsChanged",
    [
      "q.QuestData.Type == QuestType.Have",
      "q.QuestData.Type == QuestType.Food",
      "q.QuestData.Type == QuestType.Coins",
      "q.QuestData.Type == QuestType.Capacity",
      "case QuestType.Have:",
      "quest.SetProgress(stats.Currency);",
      "CardManager.Instance.AllCards",
      "case QuestType.Food:",
      "quest.SetProgress(stats.TotalNutrition);",
      "case QuestType.Coins:",
      "quest.SetProgress(stats.Currency);",
      "case QuestType.Capacity:",
      "quest.SetProgress(stats.CardLimit);",
      "CheckForCompletion(quest);",
    ],
    "StackCraft QuestManager Have / Food / Coins / Capacity 状态型任务来源链");
  assertSourceContainsOrdered(
    stackCraftQuestManagerSource,
    [
      "private void HandleCardCreated",
      "QuestType.Obtain",
      "quest.AddProgress(1);",
      "private void HandleRecipeDiscovered",
      "QuestType.Discover",
      "quest.SetProgress(1);",
      "private void HandleCardKilled",
      "QuestType.Defeat",
      "quest.AddProgress(1);",
      "private void HandleCraftingFinished",
      "QuestType.Craft",
      "quest.AddProgress(1);",
      "private void HandleCardsSold",
      "QuestType.Sell",
      "targetCard != null",
      "soldGroups.TryGetValue(targetCard, out int countSold)",
      "quest.AddProgress(countSold);",
      "quest.AddProgress(totalSoldCount);",
      "private void HandlePackPurchased",
      "QuestType.Buy",
      "targetPack != null",
      "targetPack == purchasedPack",
      "quest.AddProgress(1);",
      "private void HandleCardEquipped",
      "QuestType.Equip",
      "quest.SetProgress(1);",
      "private void HandleExplorationFinished",
      "QuestType.Explore",
      "quest.SetProgress(1);",
      "private void HandleTimePaceChanged",
      "QuestType.Time",
      "quest.SetProgress(1);",
      "private void HandleDayChanged",
      "QuestType.Day",
      "quest.SetProgress(currentDay);",
    ],
    "StackCraft QuestManager 14 类 QuestType 事件分支来源链");
}
if (questLogSource == null || questProgressSource == null ||
    questTaskDefinitionSource == null || questTaskRuntimeStateSource == null ||
    questLogSnapshotSource == null) {
  fail("缺少 Gameplay 任务正式 owner 源码，无法证明 StackCraft Quest / QuestManager 已由当前任务日志接管。");
} else {
  assertCsharpBlockContainsOrdered(
    questDefinitionSource,
    "public class QuestDefinition : DisplayableContentAsset",
    [
      "private ContentId[] m_prerequisiteQuestIds",
      "private string m_journalGroupName;",
      "private QuestTaskDefinition[] m_tasks",
      "public IReadOnlyList<ContentId> PrerequisiteQuestIds",
      "public IReadOnlyList<QuestTaskDefinition> Tasks",
      "ValidatePrerequisites(context);",
      "ValidateTasks(context);",
      "ValidatePrerequisiteCycle(context);",
    ],
    "Gameplay QuestDefinition 接管 StackCraft Quest 作者源、前置任务和任务子项");
  assertCsharpDeclarationAndBlockContainsOrdered(
    questProgressSource,
    "public enum QuestStatus",
    [
      "Locked = 0",
      "Active = 10",
      "Completed = 20",
    ],
    "Gameplay QuestStatus 接管 StackCraft QuestInstance 锁定、活动和完成状态");
  assertCsharpDeclarationAndBlockContainsOrdered(
    questProgressSource,
    "public sealed class QuestProgress",
    [
      "private readonly QuestTaskRuntimeState[] m_tasks;",
      "public QuestDefinition Definition { get; }",
      "public QuestStatus Status { get; private set; } = QuestStatus.Locked;",
      "internal QuestProgressSnapshot CreateSnapshot()",
      "internal bool RecordFact(QuestTaskFact fact, out bool completed)",
      "changed |= m_tasks[i].RecordFactFromQuestLog(fact);",
      "completed = changed && AreAllTasksCompleted();",
      "internal void Activate()",
      "Status = QuestStatus.Active;",
      "internal void Complete()",
      "Status = QuestStatus.Completed;",
    ],
    "Gameplay QuestProgress 接管 StackCraft QuestInstance 运行对象和进度状态");
  assertCsharpBlockContainsOrdered(
    questLogSource,
    "public sealed class QuestLog",
    [
      "private readonly Dictionary<ContentId, QuestProgress> m_quests",
      "private readonly List<QuestProgress> m_questOrder",
      "public int CompletedQuestCount",
      "public IReadOnlyList<QuestProgress> Quests",
    ],
    "Gameplay QuestLog 接管 StackCraft QuestManager 当前单局任务集合");
  assertCsharpBlockContainsOrdered(
    questLogSource,
    "internal QuestLog(ContentId scenarioId, IEnumerable<ContentId> questIds, ContentIndex contentIndex)",
    [
      "m_scenarioId = scenarioId;",
      "m_readOnlyQuests = m_questOrder.AsReadOnly();",
      "contentIndex.TryGet(questId, out QuestDefinition definition)",
      "QuestProgress quest = new QuestProgress(definition);",
      "m_quests.TryAdd(questId, quest)",
      "m_questOrder.Add(quest);",
      "RequirePrerequisitesPresent(quest);",
    ],
    "Gameplay QuestLog 新开局构建任务集合和重复 ID 拒绝");
  assertCsharpBlockContainsOrdered(
    questLogSource,
    "internal QuestLog(\n\t\t\tContentId scenarioId,",
    [
      "snapshot?.Quests == null",
      "Dictionary<ContentId, QuestProgressSnapshot> saved",
      "saved.TryAdd(questSnapshot.QuestId, questSnapshot)",
      "QuestProgress quest = new QuestProgress(definition, questSnapshot);",
      "saved.Count > 0",
      "任务日志快照包含不属于当前剧本的任务",
    ],
    "Gameplay QuestLog 接管 StackCraft RestoreQuests 存档恢复语义");
  assertCsharpBlockContainsOrdered(
    questLogSource,
    "internal bool RecordFact(QuestTaskFact fact)",
    [
      "QuestProgress quest = m_questOrder[i];",
      "quest.Status != QuestStatus.Active",
      "!quest.RecordFact(fact, out bool completed)",
      "EventKit.Type.Send(new QuestProgressChangedEvent(",
      "completedQuests.Add(quest);",
      "CompleteQuest(completedQuests[i], statusChanges);",
      "ActivateEligibleQuests(statusChanges);",
      "PublishStatusChanges(statusChanges);",
    ],
    "Gameplay QuestLog 事实驱动进度、完成和后继激活语义");
  assertCsharpBlockContainsOrdered(
    questLogSource,
    "private void ActivateEligibleQuests",
    [
      "quest.Status != QuestStatus.Locked",
      "quest.Definition.PrerequisiteQuestIds",
      "m_quests[prerequisiteId].Status != QuestStatus.Completed",
      "ActivateQuest(quest, statusChanges);",
    ],
    "Gameplay QuestLog 用单向前置任务接管 StackCraft QuestsToUnlock 双重更新");
  assertCsharpDeclarationAndBlockContainsOrdered(
    questLogSnapshotSource,
    "public sealed class QuestLogSnapshot",
    [
      "public sealed class QuestLogSnapshot",
      "private QuestProgressSnapshot[] m_quests;",
    ],
    "Gameplay QuestLogSnapshot 接管 StackCraft SaveQuests 任务日志集合状态");
  assertCsharpDeclarationAndBlockContainsOrdered(
    questLogSnapshotSource,
    "public sealed class QuestProgressSnapshot",
    [
      "public sealed class QuestProgressSnapshot",
      "private ContentId m_questId;",
      "private QuestStatus m_status;",
      "private QuestTaskStateSnapshot[] m_tasks;",
    ],
    "Gameplay QuestProgressSnapshot 接管 StackCraft SaveQuests 单个任务状态");
  assertCsharpDeclarationAndBlockContainsOrdered(
    questLogSnapshotSource,
    "public abstract class QuestTaskStateSnapshot",
    [
      "public abstract class QuestTaskStateSnapshot",
    ],
    "Gameplay QuestTaskStateSnapshot 提供任务子项状态扩展基类");
  assertCsharpDeclarationAndBlockContainsOrdered(
    questLogSnapshotSource,
    "internal sealed class QuestTaskAmountStateSnapshot",
    [
      "internal sealed class QuestTaskAmountStateSnapshot",
      "private int m_currentAmount;",
    ],
    "Gameplay QuestTaskAmountStateSnapshot 接管 StackCraft SaveQuests 数量型子项进度");
  assertCsharpDeclarationAndBlockContainsOrdered(
    questTaskRuntimeStateSource,
    "public abstract class QuestTaskFact",
    ["public abstract class QuestTaskFact"],
    "Gameplay QuestTaskFact 基类只承载任务日志已提交事实");
  for (const factClassName of [
    "TabletopStateQuestTaskFact",
    "ActionCompletedQuestTaskFact",
    "DayReachedQuestTaskFact",
    "ContentDiscoveredQuestTaskFact",
    "CardPackPurchasedQuestTaskFact",
    "CardsSoldQuestTaskFact",
    "CardsCreatedQuestTaskFact",
    "CardsDefeatedQuestTaskFact",
    "CardsExploredQuestTaskFact",
    "ProgressionModeChangedQuestTaskFact",
    "CardEquippedQuestTaskFact",
  ]) {
    assertCsharpDeclarationAndBlockContainsOrdered(
      questTaskRuntimeStateSource,
      `public sealed class ${factClassName}`,
      [`public sealed class ${factClassName}`],
      `Gameplay ${factClassName} 接管 StackCraft QuestManager 对应任务事实载荷`);
  }
  assertCsharpDeclarationAndBlockContainsOrdered(
    questTaskRuntimeStateSource,
    "public abstract class QuestTaskRuntimeState",
    [
      "public abstract class QuestTaskRuntimeState",
      "internal bool RecordFactFromQuestLog(QuestTaskFact fact)",
      "protected abstract bool RecordFact(QuestTaskFact fact);",
    ],
    "Gameplay QuestTaskRuntimeState 接管任务事实解释入口，不建立第二事件总线");
  for (const [taskDefinitionClassName, consumedFactType] of [
    ["CardPackPurchaseQuestTaskDefinition", "CardPackPurchasedQuestTaskFact"],
    ["CardPossessionQuestTaskDefinition", "TabletopStateQuestTaskFact"],
    ["FoodNutritionQuestTaskDefinition", "TabletopStateQuestTaskFact"],
    ["CurrencyAmountQuestTaskDefinition", "TabletopStateQuestTaskFact"],
    ["CardCapacityQuestTaskDefinition", "TabletopStateQuestTaskFact"],
    ["CardSaleQuestTaskDefinition", "CardsSoldQuestTaskFact"],
    ["CardCreationQuestTaskDefinition", "CardsCreatedQuestTaskFact"],
    ["CardDefeatQuestTaskDefinition", "CardsDefeatedQuestTaskFact"],
    ["CardExplorationQuestTaskDefinition", "CardsExploredQuestTaskFact"],
    ["ProgressionModeQuestTaskDefinition", "ProgressionModeChangedQuestTaskFact"],
    ["CardEquipQuestTaskDefinition", "CardEquippedQuestTaskFact"],
    ["ActionCompletionQuestTaskDefinition", "ActionCompletedQuestTaskFact"],
    ["DayReachedQuestTaskDefinition", "DayReachedQuestTaskFact"],
    ["ContentDiscoveryQuestTaskDefinition", "ContentDiscoveredQuestTaskFact"],
  ]) {
    assertCsharpDeclarationAndBlockContainsOrdered(
      questTaskDefinitionSource,
      `public sealed class ${taskDefinitionClassName}`,
      [
        `public sealed class ${taskDefinitionClassName}`,
        `fact is not ${consumedFactType}`,
      ],
      `Gameplay ${taskDefinitionClassName} 消费 ${consumedFactType}，接管 StackCraft QuestType 对应玩家任务分支`);
  }
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void OnActionCompleted(",
    [
      "QuestLog.RecordFact(new ActionCompletedQuestTaskFact(actionId));",
      "QuestLog.RecordFact(new CardsCreatedQuestTaskFact(result.CreatedCardIds));",
      "QuestLog.RecordFact(new CardsExploredQuestTaskFact(result.ExploredContentIds));",
      "QuestLog.RecordFact(new CardPackPurchasedQuestTaskFact(result.PurchasedPackIds[i]));",
      "QuestLog.RecordFact(new CardsSoldQuestTaskFact(result.SoldContentIds));",
      "QuestLog.RecordFact(new CardEquippedQuestTaskFact(result.EquippedCardIds[i]));",
      "RefreshQuestState(previousCompletedQuestCount);",
      "EventKit.Type.Send(new ActionCompletedEvent(ScenarioId, actionId));",
    ],
    "ScenarioRun 在行动结算后用 QuestLog 接管 StackCraft Obtain / Craft / Sell / Buy / Equip / Explore / Time 事实");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void RefreshQuestState(int previousCompletedQuestCount)",
    [
      "TabletopStateQuestTaskFact tabletopState = CreateTabletopStateQuestTaskFact();",
      "changed = QuestLog.RecordFact(tabletopState);",
      "changed |= QuestLog.RecordFact(new DayReachedQuestTaskFact(CurrentDay));",
      "new ContentDiscoveredQuestTaskFact(discoveredContentIds[i])",
      "while (changed);",
    ],
    "ScenarioRun 用当前牌桌 / 日期 / 发现事实接管 StackCraft Have / Food / Coins / Capacity / Day / Discover");
  assertCsharpBlockContainsOrdered(
    scenarioRunSource,
    "private void OnCardsDefeated",
    [
      "QuestLog.RecordFact(new CardsDefeatedQuestTaskFact(defeatedCardIds));",
      "RefreshQuestState();",
    ],
    "ScenarioRun 用战斗击败事实接管 StackCraft Defeat 任务");
}
if (questLogEditModeTestsSource == null || packVendorEditModeTestsForQuestSource == null ||
    equipmentCardEditModeTestsForQuestSource == null || scenarioRunEditModeTestsSource == null) {
  fail("缺少任务系统回归测试源码，无法证明 StackCraft QuestManager 分支已有当前框架保护。");
} else {
  assertCsharpMethodsExist(
    questLogEditModeTestsSource,
    [
      "StartQuestSet_ActivatesRootsAndUnlocksDependentsAfterCompletion",
      "Quests_PreserveScenarioAuthoringOrderAsReadOnlyRuntimeObjects",
      "QuestLifecycle_PublishesCommittedStatusChangesInCausalOrder",
      "RecordedActions_AdvanceOnlyQuestsThatWereAlreadyActive",
      "RecordActionFact_AllowsModDerivedTaskWithoutQuestLogTypeRegistration",
      "CardSaleQuestTask_CountsSpecificSoldCardsFromCommittedSaleFact",
      "CardCreationQuestTask_CountsMatchingCreatedCardsFromCommittedActionFact",
      "CardDefeatQuestTask_CountsOnlyMatchingDefeatedCardsFromBattleFact",
      "CardExplorationQuestTask_CountsOnlyMatchingExploredCardsFromCommittedActionFact",
      "ProgressionModeQuestTask_CompletesWhenTargetModeFactIsRecorded",
      "CardSaleQuestTask_EmptyTargetCountsAnySoldCard",
      "TabletopStateQuestTasks_SetProgressFromCurrentStateFact",
    ],
    "QuestLogEditModeTests 覆盖 StackCraft 任务生命周期和多数 QuestType 替代链");
  assertCsharpMethodsExist(
    packVendorEditModeTestsForQuestSource,
    [
      "PackPurchaseQuestTask_OnlyCountsMatchingPurchasedPack",
      "PackPurchaseQuestTask_EmptyTargetCountsAnyPurchasedPack",
      "PackPurchaseQuestTask_EmptyTargetPassesAuthorValidation",
    ],
    "PackVendorEditModeTests 覆盖 StackCraft Buy 任务替代链");
  assertCsharpMethodContainsOrdered(
    equipmentCardEditModeTestsForQuestSource,
    "ScenarioRun_EquipCompletesEquipmentQuestFact",
    [
      "QuestProgress quest = context.Run.QuestLog.GetQuest(context.EquipQuest.ContentId);",
      "QuestStatus.Completed",
    ],
    "EquipmentCardEditModeTests 覆盖 StackCraft Equip 任务完成链");
  assertCsharpMethodContainsOrdered(
    equipmentCardEditModeTestsForQuestSource,
    "CreateEquipmentQuest",
    [
      "new CardEquipQuestTaskDefinition()",
      "m_equipmentCardId",
      "m_requiredEquipCount",
    ],
    "EquipmentCardEditModeTests 覆盖 StackCraft Equip 任务作者源链");
  assertCsharpMethodsExist(
    scenarioRunEditModeTestsSource,
    [
      "ConfirmTurn_DerivesDayAndCompletesDayReachedQuestAtBoundary",
      "DayCycle_CreatedEncounterCardsAdvanceCardCreationQuest",
      "ActivateInitialQuests_ReplaysDiscoveredContentToNewlyUnlockedDiscoveryQuest",
      "CompletedAction_UpdatesOwningQuestLogBeforePublishingFact",
    ],
    "ScenarioRunEditModeTests 覆盖 StackCraft Day / Discover / 行动完成 / 遭遇生成任务事实顺序");
}
for (const [label, sourceText] of [
  ["QuestDefinition", questDefinitionSource],
  ["QuestLog", questLogSource],
  ["QuestProgress", questProgressSource],
  ["QuestTaskDefinition", questTaskDefinitionSource],
  ["QuestTaskRuntimeState", questTaskRuntimeStateSource],
  ["QuestLogSnapshot", questLogSnapshotSource],
  ["ScenarioRun", scenarioRunSource],
]) {
  if (sourceText == null) continue;
  for (const obsoleteToken of [
    "CryingSnow.StackCraft",
    "QuestManager",
    "QuestInstance",
    "QuestType",
    "CardManager.Instance",
    "CraftingManager.Instance",
    "TradeManager.Instance",
    "TimeManager.Instance",
    "GameData",
    "OnQuestActivated",
    "OnQuestCompleted",
  ]) {
    if (sourceText.includes(obsoleteToken)) {
      fail(`${label} 仍保留 StackCraft Quest 旧结构残留：${obsoleteToken}`);
    }
  }
}

if (scenarioTurnPanelSource == null) {
  auxiliary("缺少剧本回合 HUD 源码，无法证明底部日程控件没有继续抢占 StackCraft InfoPanel 提示职责。");
} else {
  for (const obsoleteToken of [
    "DisplayedSequenceHeader",
    "DisplayedSequenceBody",
    "OnScenarioSequenceMessage",
    "GetSequenceMessageLabel()",
    "m_sequenceMessageActive",
  ]) {
    if (scenarioTurnPanelSource.includes(obsoleteToken)) {
      auxiliary(`剧本回合 HUD 仍保留流程提示显示状态，应该由卡牌详情 / InfoPanel 承接：${obsoleteToken}`);
    }
  }
}

const stackCraftTradeCardBuyerPrefabText = readIfExists("Assets/StackCraft/Prefabs/Trading/CardBuyer.prefab");
const stackCraftTradePackVendorPrefabText = readIfExists("Assets/StackCraft/Prefabs/Trading/PackVendor.prefab");
let stackCraftTradeZoneScaleLiteral = null;
let stackCraftTradeZoneSpacingLiteral = null;
let stackCraftTradeSpawnOffsetVector2 = null;
let stackCraftCardBuyerViewSizeLiteral = null;
let stackCraftPackVendorViewSizeLiteral = null;
if (stackCraftTradeManagerSource == null) {
  fail("缺少 StackCraft TradeManager 来源源码，无法从参考源派生交易区缩放、间距和生成偏移。");
} else {
  const zoneScale = csharpVectorComponents(
    stackCraftTradeManagerSource,
    "zoneScale",
    "Vector3",
    ["x", "y", "z"],
    "StackCraft TradeManager.zoneScale");
  if (zoneScale != null) {
    stackCraftTradeZoneScaleLiteral = zoneScale.get("x");
    if (zoneScale.get("x") !== zoneScale.get("z")) {
      fail(`StackCraft TradeManager.zoneScale 的 x/z 不一致，当前 Gameplay 交易区不能只用一个平面缩放常量：x=${zoneScale.get("x")} z=${zoneScale.get("z")}。`);
    }
  }

  stackCraftTradeZoneSpacingLiteral = csharpScalarInitializer(
    stackCraftTradeManagerSource,
    "spacing",
    "StackCraft TradeManager.spacing");

  const spawnOffset = csharpVectorComponents(
    stackCraftTradeManagerSource,
    "spawnOffset",
    "Vector3",
    ["x", "y", "z"],
    "StackCraft TradeManager.spawnOffset");
  stackCraftTradeSpawnOffsetVector2 = csharpVector2FromVectorComponents(
    spawnOffset,
    "x",
    "z",
    "StackCraft TradeManager.spawnOffset");
}
const stackCraftTradeBoard01PrefabText = readIfExists("Assets/StackCraft/Prefabs/Boards/Board01.prefab");
const stackCraftTradeBoard01Placement = stackCraftTradeBoard01PrefabText == null
  ? null
  : deriveBoardPlacementFromStackCraft(
    unityYamlObjects(stackCraftTradeBoard01PrefabText),
    "Board01",
    "StackCraft Board01 交易页眉");
if (stackCraftTradeZoneScaleLiteral != null && stackCraftTradeCardBuyerPrefabText != null) {
  stackCraftCardBuyerViewSizeLiteral = unityVector2LiteralFromColliderWithScale(
    unityYamlObjects(stackCraftTradeCardBuyerPrefabText),
    "CardBuyer",
    stackCraftTradeZoneScaleLiteral,
    "StackCraft CardBuyer 可见尺寸");
}
if (stackCraftTradeZoneScaleLiteral != null && stackCraftTradePackVendorPrefabText != null) {
  stackCraftPackVendorViewSizeLiteral = unityVector2LiteralFromColliderWithScale(
    unityYamlObjects(stackCraftTradePackVendorPrefabText),
    "PackVendor",
    stackCraftTradeZoneScaleLiteral,
    "StackCraft PackVendor 可见尺寸");
}
const stackCraftPackInstanceContentPrefabText = readIfExists("Assets/StackCraft/Prefabs/PackInstance.prefab");
const stackCraftPackInstanceViewSizeLiteral = stackCraftPackInstanceContentPrefabText == null
  ? null
  : unityVector2LiteralFromCollider(
    unityYamlObjects(stackCraftPackInstanceContentPrefabText),
    "PackInstance",
    "StackCraft PackInstance 可见尺寸");

const cardBuyerDefinitionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/CardBuyerDefinition.cs");
if (cardBuyerDefinitionSource == null) {
  fail("缺少 CardBuyerDefinition，收购点仍可能只是普通卡面伪装。");
} else {
  assertCsharpBlockContainsOrdered(
    cardBuyerDefinitionSource,
    "public sealed class CardBuyerDefinition : CardDefinition",
    [
      "private ContentId m_currencyCardId;",
      "private Vector2 m_currencySpawnOffset",
      "public ContentId CurrencyCardId => m_currencyCardId;",
      "public Vector2 CurrencySpawnOffset => m_currencySpawnOffset;",
      "protected internal override TabletopCard CreateRuntimeCard",
      "protected override void ValidateContent",
    ],
    "CardBuyerDefinition 声明 StackCraft CardBuyer 作者源结构");
  assertCsharpBlockContainsOrdered(
    cardBuyerDefinitionSource,
    "protected internal override TabletopCard CreateRuntimeCard",
    [
      "return new TabletopCard(id, ContentId, InitialUses);",
    ],
    "CardBuyerDefinition 创建普通牌桌收购点卡方法");
  assertCsharpBlockContainsOrdered(
    cardBuyerDefinitionSource,
    "protected override void ValidateContent",
    [
      "base.ValidateContent(context);",
      "!CurrencyCardId.IsValid || !context.TryGet(CurrencyCardId, out CardDefinition _)",
      "CARD_BUYER_CURRENCY_INVALID",
      "float.IsNaN(CurrencySpawnOffset.x)",
      "float.IsNaN(CurrencySpawnOffset.y)",
      "float.IsInfinity(CurrencySpawnOffset.x)",
      "float.IsInfinity(CurrencySpawnOffset.y)",
      "CARD_BUYER_SPAWN_OFFSET_INVALID",
    ],
    "CardBuyerDefinition 校验 StackCraft CardBuyer 货币图标和生成偏移方法");
  for (const obsoleteToken of [
    "public override Vector2 GetViewSize(Vector2 defaultCardSize)",
    "return CardBuyerViewSize",
    "CardBuyerViewSize",
    "StackCraftTradeZoneScale",
  ]) {
    if (cardBuyerDefinitionSource.includes(obsoleteToken)) {
      fail(`CardBuyerDefinition 仍保留派生类可见尺寸第二真相：${obsoleteToken}`);
    }
  }
  if (stackCraftTradeCardBuyerPrefabText == null) {
    fail("缺少 StackCraft CardBuyer Prefab，无法从参考对象派生 CardBuyerDefinition 可见尺寸。");
  }
  if (stackCraftTradeSpawnOffsetVector2 != null) {
    assertCsharpFieldInitializerEquals(
      cardBuyerDefinitionSource,
      "m_currencySpawnOffset",
      stackCraftTradeSpawnOffsetVector2,
      "CardBuyerDefinition 货币生成偏移");
  }
}

const packVendorDefinitionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/PackVendorDefinition.cs");
if (packVendorDefinitionSource == null) {
  fail("缺少 PackVendorDefinition，卡包商贩仍可能只是普通卡面伪装。");
} else {
  assertCsharpBlockContainsOrdered(
    packVendorDefinitionSource,
    "public sealed class PackVendorDefinition : CardDefinition",
    [
      "private ContentId m_offeredPackId;",
      "private int m_price = 1;",
      "private int m_minimumCompletedQuests;",
      "private Vector2 m_packSpawnOffset",
      "public ContentId OfferedPackId => m_offeredPackId;",
      "public int Price => m_price;",
      "public int MinimumCompletedQuests => m_minimumCompletedQuests;",
      "public Vector2 PackSpawnOffset => m_packSpawnOffset;",
      "public bool IsUnlocked(int completedQuestCount)",
      "protected internal override TabletopCard CreateRuntimeCard",
      "protected internal override TabletopCard RestoreRuntimeCard",
      "protected override void ValidateContent",
    ],
    "PackVendorDefinition 声明 StackCraft PackVendor 作者源结构");
  assertCsharpBlockContainsOrdered(
    packVendorDefinitionSource,
    "public bool IsUnlocked",
    [
      "return completedQuestCount >= MinimumCompletedQuests;",
    ],
    "PackVendorDefinition 按任务完成数解锁商贩方法");
  assertCsharpBlockContainsOrdered(
    packVendorDefinitionSource,
    "protected internal override TabletopCard CreateRuntimeCard",
    [
      "return new PackVendorCard(id, ContentId, Price);",
    ],
    "PackVendorDefinition 创建 StackCraft 卡包商贩运行卡方法");
  assertCsharpBlockContainsOrdered(
    packVendorDefinitionSource,
    "protected internal override TabletopCard RestoreRuntimeCard",
    [
      "return new PackVendorCard(snapshot.CardId, ContentId, Price, snapshot.RuntimeState);",
    ],
    "PackVendorDefinition 恢复 StackCraft 卡包商贩付款状态方法");
  assertCsharpBlockContainsOrdered(
    packVendorDefinitionSource,
    "protected override void ValidateContent",
    [
      "base.ValidateContent(context);",
      "!OfferedPackId.IsValid || !context.TryGet(OfferedPackId, out CardPackDefinition _)",
      "PACK_VENDOR_PACK_INVALID",
      "Price <= 0",
      "PACK_VENDOR_PRICE_INVALID",
      "MinimumCompletedQuests < 0",
      "PACK_VENDOR_QUEST_COUNT_INVALID",
      "float.IsNaN(PackSpawnOffset.x)",
      "float.IsNaN(PackSpawnOffset.y)",
      "float.IsInfinity(PackSpawnOffset.x)",
      "float.IsInfinity(PackSpawnOffset.y)",
      "PACK_VENDOR_SPAWN_OFFSET_INVALID",
    ],
    "PackVendorDefinition 校验 StackCraft 卡包商贩商品、价格、门槛和生成偏移方法");
  for (const obsoleteToken of [
    "public override Vector2 GetViewSize(Vector2 defaultCardSize)",
    "return PackVendorViewSize",
    "PackVendorViewSize",
    "StackCraftTradeZoneScale",
  ]) {
    if (packVendorDefinitionSource.includes(obsoleteToken)) {
      fail(`PackVendorDefinition 仍保留派生类可见尺寸第二真相：${obsoleteToken}`);
    }
  }
  if (stackCraftTradePackVendorPrefabText == null) {
    fail("缺少 StackCraft PackVendor Prefab，无法从参考对象派生 PackVendorDefinition 可见尺寸。");
  }
  if (stackCraftTradeSpawnOffsetVector2 != null) {
    assertCsharpFieldInitializerEquals(
      packVendorDefinitionSource,
      "m_packSpawnOffset",
      stackCraftTradeSpawnOffsetVector2,
      "PackVendorDefinition 卡包生成偏移");
  }
}

if (foundationSceneMenuSource == null) {
  fail("缺少 FoundationTestSceneMenu，无法证明收购点测试作者源会稳定重建为 CardBuyerDefinition。");
} else {
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureCardBuyerAsset()",
    [
      "ScriptableObject.CreateInstance<CardBuyerDefinition>()",
      "WriteCardFields(",
      "FoundationTestSceneHarness.TestBuyerCardContentId",
      "WriteCardViewSizeFields(",
      "StackCraftTradeZoneViewSize",
      "RequireProperty(serializedBuyer, \"m_currencyCardId\")",
      "FoundationTestSceneHarness.TestCurrencyCardContentId",
      "RequireProperty(serializedBuyer, \"m_currencySpawnOffset\").vector2Value",
      "StackCraftTradeZoneSpawnOffset",
      "serializedBuyer.ApplyModifiedPropertiesWithoutUndo()",
    ],
    "FoundationTestSceneMenu 收购点作者源生成器");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void ResolveCardSurfaceReference(",
    [
      "content is CardBuyerDefinition",
      "cardSurfacePath = CardBuyerSurfacePath",
      "cardSurfaceAddress = CardBuyerSurfaceAddress",
      "content is PackVendorDefinition",
      "cardSurfacePath = StructureCardSurfacePath",
      "cardSurfaceAddress = StructureCardSurfaceAddress",
    ],
    "FoundationTestSceneMenu StackCraft 交易卡面材质选择");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureTabletopCardViewPrefab(",
    [
      "Mesh packMesh = LoadRequiredMesh(PackMeshPath, \"StackCraft Pack.fbx 自有副本\")",
      "Material cardBuyerCurrencyIconMaterial = LoadRequiredMaterial(",
      "CardBuyerCurrencyIconMaterialPath",
      "TMP_FontAsset cardFont = LoadStackCraftSurfaceFont();",
      "EnsureStackCraftSurfaceFontFallback(cardFont, EnsureTestPanelFont());",
      "GameObject cardBuyerCurrencyIcon = GameObject.CreatePrimitive(PrimitiveType.Quad)",
      "cardBuyerCurrencyIcon.name = \"收购货币图标\"",
      "cardBuyerCurrencyIconRenderer.sharedMaterial = cardBuyerCurrencyIconMaterial",
      "cardBuyerCurrencyIcon.SetActive(false)",
      "serializedView.FindProperty(\"m_cardBuyerCurrencyIconRenderer\").objectReferenceValue",
      "cardBuyerCurrencyIconRenderer",
    ],
    "FoundationTestSceneMenu StackCraft 收购点视图 Prefab 生成器");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsurePackVendorAsset(",
    [
      "ScriptableObject.CreateInstance<PackVendorDefinition>()",
      "WriteCardFields(",
      "WriteCardViewSizeFields(",
      "StackCraftTradeZoneViewSize",
      "RequireProperty(serializedVendor, \"m_offeredPackId\")",
      "RequireProperty(serializedVendor, \"m_price\").intValue = price",
      "RequireProperty(serializedVendor, \"m_minimumCompletedQuests\").intValue = minimumCompletedQuests",
      "RequireProperty(serializedVendor, \"m_countsTowardCardLimit\").boolValue = false",
      "RequireProperty(serializedVendor, \"m_packSpawnOffset\").vector2Value",
      "StackCraftTradeZoneSpawnOffset",
    ],
    "FoundationTestSceneMenu StackCraft 卡包商贩作者源生成器");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsurePackVendorTestAssets()",
    [
      "ScriptableObject.CreateInstance<PackVendorDefinition>()",
      "FoundationTestSceneHarness.TestPackVendorContentId",
      "WriteCardViewSizeFields(",
      "StackCraftTradeZoneViewSize",
      "FoundationTestSceneHarness.TestCardPackContentId",
      "RequireProperty(serializedVendor, \"m_price\").intValue = 2",
      "RequireProperty(serializedVendor, \"m_minimumCompletedQuests\").intValue = 0",
      "RequireProperty(serializedVendor, \"m_countsTowardCardLimit\").boolValue = false",
      "RequireProperty(serializedVendor, \"m_packSpawnOffset\").vector2Value",
      "StackCraftTradeZoneSpawnOffset",
      "EnsurePackVendorAsset(",
      "FoundationTestSceneHarness.TestBeginningPackVendorContentId",
      "FoundationTestSceneHarness.TestBeginningPackContentId",
      "price: 3",
      "minimumCompletedQuests: 3",
    ],
    "FoundationTestSceneMenu 卡包商贩测试夹具生成器");
  if (stackCraftTradeSpawnOffsetVector2 != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftTradeZoneSpawnOffset",
      stackCraftTradeSpawnOffsetVector2,
      "FoundationTestSceneMenu 交易区生成偏移");
  }
  const packInstanceViewSizeConstructor = csharpTargetTypedVector2FromUnityLiteral(
    stackCraftPackInstanceViewSizeLiteral,
    "StackCraft PackInstance 生成器尺寸");
  if (packInstanceViewSizeConstructor != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftPackInstanceViewSize",
      packInstanceViewSizeConstructor,
      "FoundationTestSceneMenu 卡包可见尺寸");
  }
  const tradeZoneViewSizeConstructor = csharpTargetTypedVector2FromUnityLiteral(
    stackCraftPackVendorViewSizeLiteral,
    "StackCraft TradeZone 生成器尺寸");
  if (tradeZoneViewSizeConstructor != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftTradeZoneViewSize",
      tradeZoneViewSizeConstructor,
      "FoundationTestSceneMenu 交易区可见尺寸");
  }
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureTestSellActionAsset()",
    [
      "WriteAnyContentSlot(",
      "\"sold\"",
      "\"出售卡牌\"",
      "RequireRelative(soldSlot, \"m_maximumParticipants\").intValue = 0;",
      "WriteExactContentSlot(",
      "\"buyer\"",
      "FoundationTestSceneHarness.TestBuyerCardContentId",
      "saleCondition.managedReferenceValue = new CardSaleSourceAvailableCondition();",
      "RequireRelative(saleCondition, \"m_soldSlotKey\").stringValue = \"sold\";",
      "sellIntent.managedReferenceValue = new SellCardsResultIntent();",
      "RequireRelative(sellIntent, \"m_soldSlotKey\").stringValue = \"sold\";",
      "FoundationTestSceneHarness.TestCurrencyCardContentId",
      "RequireRelative(sellIntent, \"m_anchorSlotKey\").stringValue = \"buyer\";",
    ],
    "FoundationTestSceneMenu StackCraft CardBuyer 出售行动作者源生成器");
  assertCsharpBlockExcludes(
    foundationSceneMenuSource,
    "private static void EnsureTestSellActionAsset()",
    ["FoundationTestSceneHarness.TestSellableCardContentId);"],
    "FoundationTestSceneMenu StackCraft CardBuyer 出售行动作者源生成器");
	const saleConditionWriteCount = (foundationSceneMenuSource.match(
		/saleCondition\.managedReferenceValue = new CardSaleSourceAvailableCondition\(\);/g) || []).length;
	if (saleConditionWriteCount !== 1) {
		fail(`FoundationTestSceneMenu 应只在出售行动中写入 CardSaleSourceAvailableCondition，当前次数为 ${saleConditionWriteCount}。`);
	}
}

const foundationHarnessSource = readIfExists("Assets/Tests/Support/Runtime/FoundationTestSceneHarness.cs");
if (foundationHarnessSource == null) {
  fail("缺少 FoundationTestSceneHarness，无法证明统一测试场景按 StackCraft 交易栏布局创建商贩。");
} else {
  assertCsharpDeclarationAndBlockContainsOrdered(
    foundationHarnessSource,
    "[DisallowMultipleComponent]",
    [
      "[DisallowMultipleComponent]",
      "[RequireComponent(typeof(TabletopView))]",
      "[RequireComponent(typeof(TabletopCardDragInput))]",
      "[RequireComponent(typeof(TabletopInteraction))]",
      "public sealed class FoundationTestSceneHarness : MonoBehaviour",
    ],
    "FoundationTestSceneHarness 同对象依赖类声明");
  assertCsharpBlockContainsOrdered(
    foundationHarnessSource,
    "private void Awake()",
    [
      "m_tabletopView = RequireSiblingComponent<TabletopView>(\"牌桌视图\");",
      "m_dragInput = RequireSiblingComponent<TabletopCardDragInput>(\"牌桌拖拽输入\");",
      "m_tabletopInteraction = RequireSiblingComponent<TabletopInteraction>(\"牌桌交互\");",
    ],
    "FoundationTestSceneHarness 同对象依赖 Awake 获取链");
  for (const forbiddenToken of [
    "[SerializeField, InspectorName(\"牌桌视图\")]",
    "[SerializeField, InspectorName(\"牌桌拖拽输入\")]",
    "[SerializeField, InspectorName(\"牌桌交互\")]",
  ]) {
    if (foundationHarnessSource.includes(forbiddenToken)) {
      fail(`FoundationTestSceneHarness 不得把同对象牌桌测试组件暴露成 Inspector 手填引用：${forbiddenToken}`);
    }
  }
  assertCsharpBlockContainsOrdered(
    foundationHarnessSource,
    "public void CreatePackVendorTestCards()",
    [
      "m_scenarioRun.ContentIndex.TryGet(vendorId, out PackVendorDefinition _)",
      "m_scenarioRun.ContentIndex.TryGet(buyerId, out CardBuyerDefinition _)",
      "m_scenarioRun.ContentIndex.TryGet(currencyId, out CardDefinition _)",
      "m_scenarioRun.ContentIndex.TryGet(purchaseActionId, out ActionDefinition _)",
      "m_scenarioRun.DiscoverContent(purchaseActionId)",
      "Vector2 buyerPosition = CalculateStackCraftTradeZonePosition(",
      "StackCraftBuyerZoneIndex",
      "StackCraftPackVendorTestZoneCount",
      "Vector2 vendorPosition = CalculateStackCraftTradeZonePosition(",
      "StackCraftPackVendorZoneIndex",
      "StackCraftPackVendorTestZoneCount",
      "PackVendorBuyerId = m_tabletop.CreateCard(buyerId, buyerPosition, isPlacementLocked: true).Id",
      "PackVendorId = m_tabletop.CreateCard(vendorId, vendorPosition, isPlacementLocked: true).Id",
      "buyerPosition + StackCraftTradeZoneSpawnOffset",
      "vendorPosition + StackCraftTradeZoneSpawnOffset",
    ],
    "统一测试场景 StackCraft 交易区卡牌创建链");
  assertCsharpBlockContainsOrdered(
    foundationHarnessSource,
    "private Vector2 CalculateStackCraftTradeZonePosition(",
    [
      "if (zoneIndex < 0 || zoneIndex >= zoneCount)",
      "Rect bounds = PlacementRules.Area.Bounds;",
      "IReadOnlyList<Rect> restrictedAreas = PlacementRules.Area.RestrictedAreas;",
      "if (restrictedAreas.Count != 1)",
      "Rect headerArea = restrictedAreas[0];",
      "float totalWidth = (zoneCount - 1) * StackCraftTradeZoneSpacing;",
      "float startX = bounds.center.x - totalWidth * 0.5f;",
      "startX + zoneIndex * StackCraftTradeZoneSpacing",
      "headerArea.center.y",
    ],
    "统一测试场景 StackCraft TradeManager 布局公式");
  if (foundationHarnessSource.includes("StackCraftTradeHeaderTopMargin")) {
    fail("统一测试场景仍手填 StackCraftTradeHeaderTopMargin；交易区页眉位置必须从地区牌桌禁放区读取。");
  }
  if (stackCraftTradeZoneSpacingLiteral != null) {
    assertCsharpConstFloatEquals(
      foundationHarnessSource,
      "StackCraftTradeZoneSpacing",
      stackCraftTradeZoneSpacingLiteral,
      "统一测试场景交易区间距");
  }
  if (stackCraftTradeSpawnOffsetVector2 != null) {
    assertCsharpFieldInitializerEquals(
      foundationHarnessSource,
      "StackCraftTradeZoneSpawnOffset",
      stackCraftTradeSpawnOffsetVector2,
      "统一测试场景交易区生成偏移");
  }
  for (const forbidden of [
    "PackVendorId = m_tabletop.CreateCard(vendorId, new Vector2(0f, 2.5f)).Id",
    "PackVendorBuyerId = m_tabletop.CreateCard(buyerId, buyerPosition).Id",
    "PackVendorId = m_tabletop.CreateCard(vendorId, vendorPosition).Id",
    "FirstPackPaymentId = m_tabletop.CreateCard(currencyId, new Vector2(-2.2f, 2.5f)).Id",
    "SecondPackPaymentId = m_tabletop.CreateCard(currencyId, new Vector2(-1.3f, 2.5f)).Id",
  ]) {
    if (foundationHarnessSource.includes(forbidden)) {
      fail(`统一测试场景仍保留手写交易区坐标，未按 StackCraft 布局公式对账：${forbidden}`);
    }
  }
}

const foundationBuyerAssetText = readIfExists("Assets/Gameplay/Tests/地基日终收购点.asset");
if (foundationBuyerAssetText == null) {
  fail("缺少地基日终收购点作者源，无法证明 CardBuyer 已进入统一测试场景。");
} else {
  assertYamlScalarStringEquals(
    foundationBuyerAssetText,
    "m_EditorClassIdentifier",
    "Gameplay.Runtime::Gameplay.Content.CardBuyerDefinition",
    "地基日终收购点 CardBuyer 作者源类型");
  assertYamlNestedScalarEquals(
    foundationBuyerAssetText,
    "m_currencyCardId",
    "m_value",
    "test.foundation.day-cycle.currency",
    "地基日终收购点 CardBuyer 货币卡 ID");
  assertSoftAssetReference(
    foundationBuyerAssetText,
    "m_cardArt",
    "CardArts_Placeholder",
    guidFromMetaPath("Assets/Art/Sprites/StackCraft/CardArts/Placeholder.png.meta", "StackCraft CardBuyer 占位卡图"),
    "地基日终收购点 CardBuyer 卡图");
  assertSoftAssetReference(
    foundationBuyerAssetText,
    "m_cardSurface",
    "交易区",
    guidFromMetaPath("Assets/Art/Materials/交易区.mat.meta", "StackCraft CardBuyer 交易区表面材质"),
    "地基日终收购点 CardBuyer 表面材质");
  assertCardViewSizeAsset(
    foundationBuyerAssetText,
    "Assets/Gameplay/Tests/地基日终收购点.asset",
    stackCraftCardBuyerViewSizeLiteral,
    "StackCraft CardBuyer");
}

const tabletopCardDragSessionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Input/TabletopCardDragSession.cs");
if (tabletopCardDragSessionSource == null) {
  fail("缺少牌桌拖拽会话源码，无法证明 StackCraft 点击判定距离已由正式输入状态机承接。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopCardDragSessionSource,
    "internal sealed class TabletopCardDragSession",
    [
      "private readonly float m_clickThresholdSquared;",
      "public bool IsActive { get; private set; }",
      "public bool IsDragging { get; private set; }",
    ],
    "牌桌拖拽会话 StackCraft 点击阈值和状态字段结构");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragSessionSource,
    "public TabletopCardDragSession(float clickThreshold)",
    [
      "clickThreshold < 0f",
      "点击判定距离不能为负数或非有限值。",
      "m_clickThresholdSquared = clickThreshold * clickThreshold;",
    ],
    "牌桌拖拽会话构造函数的 StackCraft 点击阈值语义");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragSessionSource,
    "public bool Update",
    [
      "CurrentPointerTablePosition = pointerTablePosition;",
      "CurrentStackPosition = pointerTablePosition + PointerToStackTableOffset;",
      "(pointerTablePosition - PressPointerTablePosition).sqrMagnitude >=",
      "m_clickThresholdSquared",
      "IsDragging = true;",
    ],
    "牌桌拖拽会话 Update 的牌桌世界距离判定语义");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragSessionSource,
    "public TabletopCardPointerReleaseIntent End",
    [
      "Update(pointerScreenPosition, pointerTablePosition);",
      "PressPointerTablePosition",
      "CurrentPointerTablePosition",
      "CurrentStackPosition",
      "IsDragging ? targetCardId : default",
      "Reset();",
    ],
    "牌桌拖拽会话 End 的释放事实语义");
  assertCsharpBlockExcludes(
    tabletopCardDragSessionSource,
    "public bool Update",
    [
      "m_dragStartScreenDistanceSquared",
      "PressPointerScreenPosition",
      "pointerScreenPosition -",
    ],
    "牌桌拖拽会话 Update 的旧屏幕像素阈值路径");
}

const tabletopCardDragInputSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Input/TabletopCardDragInput.cs");
if (tabletopCardDragInputSource == null) {
  fail("缺少牌桌拖拽输入源码，无法证明 StackCraft 实际卡牌表现命中语义已由正式输入承接。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopCardDragInputSource,
    "public void Bind(",
    [
      "m_tabletopView = tabletopView;",
      "m_session = new TabletopCardDragSession(tabletopView.CardClickThreshold);",
      "SubscribeIfPossible();",
    ],
    "牌桌拖拽输入绑定时使用牌桌视图设置里的 StackCraft 点击阈值创建会话");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragInputSource,
    "private void OnPrimaryPointerStarted",
    [
      "TryProjectToTable(screenPosition, out var tablePosition)",
      "TryHitCardView(screenPosition, out var cardView)",
      "sourceStack.GetDraggedSegmentStartIndex(cardView.CardId)",
      "TabletopCardId previewAnchorCardId = sourceStack.Cards[draggedSegmentStartIndex].Id;",
      "m_tabletop.Cards.GetCardTablePosition(",
      "m_session.Begin(cardView.CardId, screenPosition, tablePosition, dragAnchor);",
      "m_tabletop.HoldAutomaticBehaviorForLocalInput(cardView.CardId);",
      "m_tabletopView.SetDragPreview(previewAnchorCardId, m_session.CurrentStackPosition)",
      "UpdateDropTarget(screenPosition);",
    ],
    "牌桌拖拽输入 OnPrimaryPointerStarted 的按下即拿起语义");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragInputSource,
    "private bool TryProjectToTable",
    [
      "Camera camera = GameManager.MainCamera;",
      "Ray ray = camera.ScreenPointToRay(screenPosition);",
      "TabletopCoordinateSpace.CreateTablePlane(tablePlane).Raycast(ray, out var distance)",
      "Vector3 localPoint = tablePlane.InverseTransformPoint(ray.GetPoint(distance));",
      "tablePosition = TabletopCoordinateSpace.ToTablePosition(localPoint);",
    ],
    "牌桌拖拽输入 TryProjectToTable 的正式主相机与 XZ 平面投影语义");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragInputSource,
    "private void OnPrimaryPointerCanceled",
    [
      "UpdateDropTarget(screenPosition);",
      "TabletopCardPointerReleaseIntent intent = session.End(",
      "m_currentTargetCardId);",
      "m_releaseHandler(intent);",
    ],
    "牌桌拖拽输入释放时必须把直接命中或 AttachRadius 吸附目标提交给正式交互入口");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragInputSource,
    "TabletopCardStack excludedStack,",
    [
      "Physics.RaycastAll(",
      "GetComponentInParent<TabletopCardView>()",
      "m_tabletopView.TryGetCardView(candidateView.CardId, out TabletopCardView registeredView)",
      "ReferenceEquals(candidateView, registeredView)",
      "ReferenceEquals(candidateView.TabletopCard?.Stack, excludedStack)",
      "candidateView.SortingOrder > bestSortingOrder",
    ],
    "牌桌拖拽输入 TryHitCardView 的可见卡牌射线命中语义");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragInputSource,
    "private void UpdateDropTarget",
    [
      "RefreshDropTargetHighlights(session, sourceStack)",
      "TryHitCardView(screenPosition, sourceStack, out var targetView)",
      "TryFindAttachRadiusTarget(session, sourceStack, out targetView)",
      "TrySetDropTarget(session, targetView.CardId)",
      "ClearCurrentDropTarget();",
    ],
    "牌桌拖拽输入 StackCraft 直接命中优先、AttachRadius 有效候选兜底和全量高亮刷新语义");
  assertCsharpBlockExcludes(
    tabletopCardDragInputSource,
    "private void UpdateDropTarget",
    [
      "!session.IsDragging",
    ],
    "牌桌拖拽输入候选底牌高亮不能等超过拖拽阈值后才刷新");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragInputSource,
    "private bool TryFindAttachRadiusTarget",
    [
      "m_tabletopView.TryFindNearestCardViewWithinAttachRadius(",
      "session.CurrentStackPosition",
      "sourceStack",
      "m_dropTargetHighlightCardIds",
      "return true;",
    ],
    "牌桌拖拽输入 StackCraft AttachRadius 只在当前可执行候选集合内吸附");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragInputSource,
    "private void RefreshDropTargetHighlights",
    [
      "m_dropTargetHighlightCardIds.Clear();",
      "IReadOnlyList<TabletopCardStack> stacks = m_tabletop.Cards.Stacks;",
      "ReferenceEquals(candidateStack, sourceStack)",
      "TabletopCardId bottomCardId = candidateStack.BottomCard.Id;",
      "CanHighlightDropTarget(session, bottomCardId)",
      "m_dropTargetHighlightCardIds.Add(bottomCardId)",
      "m_tabletopView.SetDropTargetHighlights(m_dropTargetHighlightCardIds);",
    ],
    "牌桌拖拽输入拖拽中高亮所有 StackCraft 可堆叠目标底牌");
  assertCsharpBlockContainsOrdered(
    tabletopCardDragInputSource,
    "private TabletopCardId GetDragPreviewAnchorCardId",
    [
      "m_tabletop.TryGetBattlePose(cardId, 0, out _)",
      "return cardId;",
      "sourceStack.GetDraggedSegmentStartIndex(cardId)",
      "return sourceStack.Cards[draggedSegmentStartIndex].Id;",
    ],
    "牌桌拖拽输入 GetDragPreviewAnchorCardId 的牌堆段锚点语义");
  for (const token of [
    "pixelDragThreshold",
    "像素拖拽阈值",
  ]) {
    if (tabletopCardDragInputSource.includes(token)) {
      fail(`牌桌拖拽输入仍把 UI 像素阈值当卡牌点击 / 拖拽判定：${token}`);
    }
  }
  if (tabletopCardDragInputSource.includes("Vector2 cardCenter = stack.Position + geometry.StackStep * cardIndex")) {
    fail("牌桌拖拽输入仍按权威堆位置猜测命中，动画期会让可见卡牌与可点击区域错位。");
  }
  for (const token of [
    "GetCurrentCardViewTablePosition",
    "ContainsTablePosition(",
  ]) {
    if (tabletopCardDragInputSource.includes(token)) {
      fail(`牌桌拖拽输入仍保留旧二维矩形命中路径：${token}`);
    }
  }
}

const tabletopCameraControllerSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopCameraController.cs");
if (tabletopCameraControllerSource == null) {
  fail("缺少牌桌镜头控制器源码，无法证明 StackCraft 透视镜头参数已吸收。");
} else {
  const stackCraftCameraControllerSource = readIfExists("Assets/StackCraft/Scripts/Core/CameraController.cs");
  const stackCraftCameraParameters = [];
  if (stackCraftCameraControllerSource == null) {
    fail("缺少 StackCraft CameraController.cs，无法从参考源码派生牌桌镜头参数。");
  } else {
    for (const [sourceField, targetField, label] of [
      ["panSpeed", "m_panSpeed", "StackCraft panSpeed"],
      ["smoothTime", "m_smoothTime", "StackCraft smoothTime"],
      ["panPadding", "m_panPadding", "StackCraft panPadding"],
      ["zoomSpeed", "m_zoomSpeed", "StackCraft zoomSpeed"],
      ["minDistance", "m_minDistance", "StackCraft minDistance"],
      ["maxDistance", "m_maxDistance", "StackCraft maxDistance"],
    ]) {
      const value = csharpScalarInitializer(stackCraftCameraControllerSource, sourceField, label);
      if (value != null) {
        stackCraftCameraParameters.push([targetField, value, label]);
      }
    }

    const focusDuration = csharpDefaultParameter(
      stackCraftCameraControllerSource,
      "MoveTo",
      "duration",
      "StackCraft CameraController.MoveTo");
    if (focusDuration != null) {
      stackCraftCameraParameters.push([
        "m_focusDurationSeconds",
        focusDuration,
        "StackCraft CameraController.MoveTo duration"]);
    }
  }

  assertCsharpBlockContainsOrdered(
    tabletopCameraControllerSource,
    "public sealed class TabletopCameraController",
    [
      "private Transform m_cameraTransform;",
      "private TabletopCardDragInput m_tabletopCardDragInput;",
      "private Tween m_focusTween;",
    ],
    "牌桌镜头控制器 StackCraft 透视镜头类结构");
  assertCsharpBlockContainsOrdered(
    tabletopCameraControllerSource,
    "private void RequireCameraBinding()",
    [
      "m_camera = m_cameraTransform.GetComponent<Camera>();",
      "m_camera.orthographic",
      "StackCraft 镜头复刻必须使用透视相机",
      "m_tabletopCardDragInput = m_tabletopView.GetComponent<TabletopCardDragInput>();",
      "避免拖卡时镜头同时平移",
    ],
    "牌桌镜头控制器 RequireCameraBinding 正式相机绑定");
  assertCsharpBlockContainsOrdered(
    tabletopCameraControllerSource,
    "private void HandlePan(",
    [
      "m_tabletopCardDragInput.IsPointerSessionActive",
      "StopDragging();",
      "return;",
      "EGameplayInputAction.MiddleClick",
      "EGameplayInputAction.Click",
      "IsPointerBlockedForPan(screenPosition)",
      "KillFocusTween();",
    ],
    "牌桌镜头控制器 HandlePan 输入占用与 StackCraft 平移语义");
  assertCsharpBlockContainsOrdered(
    tabletopCameraControllerSource,
    "private void HandleZoom(",
    [
      "ReadGameplayVector2(EGameplayInputAction.ScrollWheel)",
      "m_cameraTransform.forward * (scrollDelta.y * m_zoomSpeed)",
      "nextDistance >= m_minDistance && nextDistance <= m_maxDistance",
      "KillFocusTween();",
      "SetTargetWorldPosition(nextPosition);",
    ],
    "牌桌镜头控制器 HandleZoom 透视相机距离缩放语义");
  assertCsharpBlockContainsOrdered(
    tabletopCameraControllerSource,
    "private void FocusOnTablePosition(",
    [
      "TabletopCoordinateSpace.ToLocalPosition(tablePosition)",
      "float desiredDistance = Mathf.Lerp(m_maxDistance, m_minDistance, 0.8f);",
      "m_cameraTransform.forward * desiredDistance",
      ".DOMove(nextRootPosition, m_focusDurationSeconds)",
      ".SetUpdate(true)",
    ],
    "牌桌镜头控制器 FocusOnTablePosition StackCraft 聚焦公式");
  for (const [targetField, value, label] of stackCraftCameraParameters) {
    assertCsharpFieldInitializerEquals(
      tabletopCameraControllerSource,
      targetField,
      value,
      `牌桌镜头控制器 ${label}`);
  }
  if (/\[SerializeField\]\s*(?:\[[^\]]+\]\s*)*private\s+Camera\s+m_camera\b/.test(tabletopCameraControllerSource)) {
    fail("牌桌镜头控制器不能把主相机作为第二个 Inspector 手填字段；必须由 m_cameraTransform 自动读取 Camera 组件。");
  }
  for (const obsoleteToken of [
    "m_panMultiplier",
    "m_zoomPerScrollUnit",
    "m_minOrthographicSize",
    "m_maxOrthographicSize",
    "m_focusOrthographicSize",
    "m_targetOrthographicSize",
    "orthographicSize",
    "RequireComponent(typeof(Camera))",
    "[SerializeField]\r\n\t\t[LabelText(\"主相机\")]",
    "[SerializeField]\n\t\t[LabelText(\"主相机\")]",
  ]) {
    if (tabletopCameraControllerSource.includes(obsoleteToken)) {
      fail(`牌桌镜头控制器仍保留旧正交镜头字段或约束：${obsoleteToken}`);
    }
  }
}

const foundationSceneText = readIfExists("Assets/Scenes/FoundationTest.unity");
function assertSceneHasStackCraftStandaloneMainCamera(scenePath, sceneText, label) {
  if (sceneText == null) {
    fail(`缺少 ${scenePath}，无法证明 ${label} 主相机对齐 StackCraft。`);
    return;
  }

  const parsedScene = unityYamlObjects(sceneText);
  assertUnityGameObjectExists(parsedScene, "CameraController", `${label} StackCraft 镜头根`);
  assertUnityGameObjectExists(parsedScene, "Main Camera", `${label} 主相机`);
  assertUnityComponentExists(parsedScene, "Main Camera", 20, `${label} 主相机`);
  assertUnityComponentExists(parsedScene, "Main Camera", 81, `${label} 主相机音频监听`);

  if (foundationSceneMenuSource == null) {
    fail(`缺少 FoundationTestSceneMenu，无法从场景生成器派生 ${label} 镜头根 Transform。`);
  } else {
    const cameraRootPosition = unityVector3ValuesFromCsharpAssignment(
      foundationSceneMenuSource,
      "cameraControllerObject.transform.position",
      `${label} 镜头根位置`);
    const cameraRootRotation = unityQuaternionValuesFromCsharpEulerXAssignment(
      foundationSceneMenuSource,
      "cameraControllerObject.transform.rotation",
      `${label} 镜头根旋转`);
    if (cameraRootPosition != null) {
      assertUnityComponentInlineNumericPropertyMatches(
        parsedScene,
        "CameraController",
        4,
        "m_LocalPosition",
        cameraRootPosition,
        ["x", "y", "z"],
        `${label} StackCraft 镜头根 Transform`);
    }
    if (cameraRootRotation != null) {
      assertUnityComponentInlineNumericPropertyMatches(
        parsedScene,
        "CameraController",
        4,
        "m_LocalRotation",
        cameraRootRotation,
        ["x", "y", "z", "w"],
        `${label} StackCraft 镜头根 Transform`);
    }
  }

  const stackCraftCameraControllerPrefabText = readIfExists("Assets/StackCraft/Prefabs/Core/CameraController.prefab");
  if (stackCraftCameraControllerPrefabText == null) {
    fail(`缺少 StackCraft CameraController.prefab，无法从参考 Prefab 派生 ${label} 主相机参数。`);
    return;
  }

  const stackCraftCameraControllerPrefabYaml = unityYamlObjects(stackCraftCameraControllerPrefabText);
  assertUnityComponentPropertiesMatch(
    stackCraftCameraControllerPrefabYaml,
    "MainCamera",
    parsedScene,
    "Main Camera",
    4,
    ["m_LocalPosition", "m_LocalScale"],
    `${label} StackCraft 主相机 Transform`);
  const stackCraftMainCameraTransform = unityComponentByClass(stackCraftCameraControllerPrefabYaml, "MainCamera", 4);
  const stackCraftMainCameraRotation = stackCraftMainCameraTransform == null
    ? null
    : unityInlineObjectProperty(
      stackCraftMainCameraTransform.text,
      "m_LocalRotation",
      `${label} StackCraft CameraController.prefab MainCamera.m_LocalRotation`);
  if (stackCraftMainCameraRotation != null) {
    assertUnityComponentInlineNumericPropertyMatches(
      parsedScene,
      "Main Camera",
      4,
      "m_LocalRotation",
      stackCraftMainCameraRotation,
      ["x", "y", "z", "w"],
      `${label} StackCraft 主相机 Transform`);
  }
  assertUnityComponentPropertiesMatch(
    stackCraftCameraControllerPrefabYaml,
    "MainCamera",
    parsedScene,
    "Main Camera",
    20,
    [
      "field of view",
      "orthographic",
      "near clip plane",
      "far clip plane",
      "m_BackGroundColor",
    ],
    `${label} StackCraft 主相机 Camera`);
}

if (foundationSceneText == null) {
  fail("缺少 FoundationTest 场景，无法证明测试入口相机层级对齐 StackCraft。");
} else {
  const foundationSceneYaml = unityYamlObjects(foundationSceneText);
  assertUnityGameObjectExists(foundationSceneYaml, "CameraController", "FoundationTest StackCraft 镜头根");
  assertUnityGameObjectExists(foundationSceneYaml, "Main Camera", "FoundationTest StackCraft 主相机");
  assertUnityComponentExists(foundationSceneYaml, "Main Camera", 20, "FoundationTest StackCraft 主相机");
  assertUnityComponentExists(foundationSceneYaml, "Main Camera", 81, "FoundationTest StackCraft 主相机音频监听");
  const cameraControllerSceneParameters = [];
  const stackCraftCameraControllerForSceneSource =
    readIfExists("Assets/StackCraft/Scripts/Core/CameraController.cs");
  if (stackCraftCameraControllerForSceneSource == null) {
    fail("缺少 StackCraft CameraController.cs，无法从参考源码派生 FoundationTest 镜头控制器场景参数。");
  } else {
    for (const [sourceField, targetField, label] of [
      ["panSpeed", "m_panSpeed", "StackCraft panSpeed"],
      ["smoothTime", "m_smoothTime", "StackCraft smoothTime"],
      ["panPadding", "m_panPadding", "StackCraft panPadding"],
      ["zoomSpeed", "m_zoomSpeed", "StackCraft zoomSpeed"],
      ["minDistance", "m_minDistance", "StackCraft minDistance"],
      ["maxDistance", "m_maxDistance", "StackCraft maxDistance"],
    ]) {
      const value = csharpScalarInitializer(stackCraftCameraControllerForSceneSource, sourceField, label);
      if (value != null) {
        cameraControllerSceneParameters.push([targetField, unityNumberLiteralFromCsharp(value), label]);
      }
    }
    const focusDuration = csharpDefaultParameter(
      stackCraftCameraControllerForSceneSource,
      "MoveTo",
      "duration",
      "StackCraft CameraController.MoveTo");
    if (focusDuration != null) {
      cameraControllerSceneParameters.push([
        "m_focusDurationSeconds",
        unityNumberLiteralFromCsharp(focusDuration),
        "StackCraft CameraController.MoveTo duration"]);
    }
  }
  if (foundationSceneMenuSource == null) {
    fail("缺少 FoundationTestSceneMenu，无法从场景生成器派生 FoundationTest 镜头根 Transform。");
  } else {
    const cameraRootPosition = unityVector3ValuesFromCsharpAssignment(
      foundationSceneMenuSource,
      "cameraControllerObject.transform.position",
      "FoundationTestSceneMenu 镜头根位置");
    const cameraRootRotation = unityQuaternionValuesFromCsharpEulerXAssignment(
      foundationSceneMenuSource,
      "cameraControllerObject.transform.rotation",
      "FoundationTestSceneMenu 镜头根旋转");
    if (cameraRootPosition != null) {
      assertUnityComponentInlineNumericPropertyMatches(
        foundationSceneYaml,
        "CameraController",
        4,
        "m_LocalPosition",
        cameraRootPosition,
        ["x", "y", "z"],
        "FoundationTest StackCraft 镜头根 Transform");
    }
    if (cameraRootRotation != null) {
      assertUnityComponentInlineNumericPropertyMatches(
        foundationSceneYaml,
        "CameraController",
        4,
        "m_LocalRotation",
        cameraRootRotation,
        ["x", "y", "z", "w"],
        "FoundationTest StackCraft 镜头根 Transform");
    }
    assertUnityComponentScalarEquals(
      foundationSceneYaml,
      "CameraController",
      4,
      "m_LocalScale",
      "{x: 1, y: 1, z: 1}",
      "FoundationTest StackCraft 镜头根 Transform");
  }
  for (const requiredFieldName of [
    "m_tabletopView",
    "m_cameraTransform",
  ]) {
    assertUnityMonoBehaviourPropertyExists(
      foundationSceneYaml,
      "CameraController",
      "Gameplay.Runtime::Gameplay.Tabletop.TabletopCameraController",
      requiredFieldName,
      "FoundationTest StackCraft 镜头控制器字段存在性");
  }
  for (const [targetField, expectedValue, label] of cameraControllerSceneParameters) {
    assertUnityMonoBehaviourScalarEquals(
      foundationSceneYaml,
      "CameraController",
      "Gameplay.Runtime::Gameplay.Tabletop.TabletopCameraController",
      targetField,
      expectedValue,
      `FoundationTest StackCraft 镜头控制器 ${label}`);
  }
  assertUnityComponentFieldReferences(
    foundationSceneYaml,
    "CameraController",
    "m_cameraTransform",
    "Main Camera",
    4,
    "FoundationTest StackCraft 镜头控制器字段引用");
  for (const forbiddenHarnessField of [
    "m_tabletopView",
    "m_dragInput",
    "m_tabletopInteraction",
  ]) {
    assertUnityMonoBehaviourPropertyAbsent(
      foundationSceneYaml,
      "牌桌测试",
      "Gameplay.Foundation.TestSupport::Gameplay.Tests.Support.FoundationTestSceneHarness",
      forbiddenHarnessField,
      "FoundationTest Harness 同对象依赖");
  }
  if (foundationSceneText.includes("m_camera:")) {
    fail("FoundationTest StackCraft 镜头控制器仍保留重复主相机字段 m_camera。");
  }
  const stackCraftCameraControllerPrefabText = readIfExists("Assets/StackCraft/Prefabs/Core/CameraController.prefab");
  if (stackCraftCameraControllerPrefabText == null) {
    fail("缺少 StackCraft CameraController.prefab，无法从参考 Prefab 派生 FoundationTest 主相机参数。");
  } else {
    const stackCraftCameraControllerPrefabYaml = unityYamlObjects(stackCraftCameraControllerPrefabText);
    assertUnityComponentPropertiesMatch(
      stackCraftCameraControllerPrefabYaml,
      "MainCamera",
      foundationSceneYaml,
      "Main Camera",
      4,
      ["m_LocalPosition", "m_LocalScale"],
      "FoundationTest StackCraft 主相机 Transform");
    const stackCraftMainCameraTransform = unityComponentByClass(stackCraftCameraControllerPrefabYaml, "MainCamera", 4);
    const stackCraftMainCameraRotation = stackCraftMainCameraTransform == null
      ? null
      : unityInlineObjectProperty(
        stackCraftMainCameraTransform.text,
        "m_LocalRotation",
        "StackCraft CameraController.prefab MainCamera.m_LocalRotation");
    if (stackCraftMainCameraRotation != null) {
      assertUnityComponentInlineNumericPropertyMatches(
        foundationSceneYaml,
        "Main Camera",
        4,
        "m_LocalRotation",
        stackCraftMainCameraRotation,
        ["x", "y", "z", "w"],
        "FoundationTest StackCraft 主相机 Transform");
    }
    assertUnityComponentPropertiesMatch(
      stackCraftCameraControllerPrefabYaml,
      "MainCamera",
      foundationSceneYaml,
      "Main Camera",
      20,
      [
        "field of view",
        "orthographic",
        "near clip plane",
        "far clip plane",
        "m_BackGroundColor",
      ],
      "FoundationTest StackCraft 主相机 Camera");
  }
  for (const obsoleteToken of [
    "m_panMultiplier:",
    "m_zoomPerScrollUnit:",
    "m_minOrthographicSize:",
    "m_maxOrthographicSize:",
    "m_focusOrthographicSize:",
    "orthographic: 1",
    "orthographic size: 4.5",
  ]) {
    if (foundationSceneText.includes(obsoleteToken)) {
      fail(`FoundationTest 场景仍保留旧正交镜头参数：${obsoleteToken}`);
    }
  }
}
const gameCoreCameraShakeSource = readIfExists("Assets/Scripts/GameCore/Runtime/Animation/CameraShake.cs");
if (gameCoreCameraShakeSource == null) {
  fail("缺少 GameCore.CameraShake，无法证明 StackCraft 命中镜头震动被正式表现链承接。");
} else {
  assertCsharpBlockContainsOrdered(
    gameCoreCameraShakeSource,
    "private void OnEnable",
    [
      "EventKit.Type.Register<DamageTakenPresentationEvent>(OnDamageTakenPresentation);",
      "EventKit.Type.Register<AbilitySystemDamageResolvedPresentationEvent>(",
      "OnAbilitySystemDamageResolvedPresentation);",
    ],
    "GameCore.CameraShake 订阅 StackCraft 命中镜头震动表现事件方法");
  assertCsharpBlockContainsOrdered(
    gameCoreCameraShakeSource,
    "private void StartShake",
    [
      "float amplitude = isCriticalHit ? m_amplitude * m_criticalHitAmplitudeModifier : m_amplitude;",
      "TransformShaker.Shake(this, transform, amplitude, m_frequency, m_duration)",
    ],
    "GameCore.CameraShake 按普通 / 暴击计算 StackCraft 命中镜头震动方法");
  assertCsharpBlockContainsOrdered(
    gameCoreCameraShakeSource,
    "private void OnAbilitySystemDamageResolvedPresentation",
    [
      "cameraShakeSources.HasFlag(ECameraShakeSources.AbilitySystemDamageResolved)",
      "presentationEvent.IsMissed",
      "presentationEvent.IsSilent",
      "presentationEvent.VisualFlags.HasFlag(EEffectVisualFlags.NoCameraShake)",
      "StartShake(presentationEvent.IsCriticalHit);",
    ],
    "GameCore.CameraShake 承接 EX-GAS 伤害结算的 StackCraft 命中镜头震动方法");
}
const foundationConfigText = readIfExists("Assets/Scenes/FoundationTestConfig.asset");
if (foundationConfigText == null) {
  fail("缺少 FoundationTestConfig，无法验证镜头震动来源配置。");
} else {
  assertYamlScalarEquals(
    foundationConfigText,
    "m_cameraShakeSources",
    "4",
    "FoundationTestConfig AbilitySystemDamageResolved 镜头震动来源");
}
if (foundationSceneText != null) {
  const stackCraftCameraControllerForShakeSource = readIfExists("Assets/StackCraft/Scripts/Core/CameraController.cs");
  const stackCraftShakeStrength = stackCraftCameraControllerForShakeSource == null
    ? null
    : csharpDefaultParameter(
      stackCraftCameraControllerForShakeSource,
      "Shake",
      "strength",
      "StackCraft CameraController.Shake");
  const stackCraftShakeDuration = stackCraftCameraControllerForShakeSource == null
    ? null
    : csharpDefaultParameter(
      stackCraftCameraControllerForShakeSource,
      "Shake",
      "duration",
      "StackCraft CameraController.Shake");
  if (stackCraftCameraControllerForShakeSource == null) {
    fail("缺少 StackCraft CameraController.cs，无法从 Shake 默认参数派生镜头震动参数。");
  }
  assertUnityMonoBehaviourScalarEquals(
    unityYamlObjects(foundationSceneText),
    "Main Camera",
    "GameCore::GameCore.CameraShake",
    "m_amplitude",
    stackCraftShakeStrength == null ? null : unityNumberLiteralFromCsharp(stackCraftShakeStrength),
    "FoundationTest StackCraft 命中镜头震动幅度");
  assertUnityMonoBehaviourScalarEquals(
    unityYamlObjects(foundationSceneText),
    "Main Camera",
    "GameCore::GameCore.CameraShake",
    "m_duration",
    stackCraftShakeDuration == null ? null : unityNumberLiteralFromCsharp(stackCraftShakeDuration),
    "FoundationTest StackCraft 命中镜头震动时长");
}
const tabletopSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Tabletop.cs");
if (tabletopSource == null) {
 fail("缺少牌桌聚合根源码，无法证明投射物表现与命中结算前摇时长。");
} else {
  const stackCraftArrowProjectileForTiming = readIfExists("Assets/StackCraft/Prefabs/UI/Projectile_Arrow.prefab");
  const stackCraftMagicProjectileForTiming = readIfExists("Assets/StackCraft/Prefabs/UI/Projectile_Magic.prefab");
  const arrowDuration = stackCraftArrowProjectileForTiming == null
    ? null
    : yamlScalarPropertyValue(stackCraftArrowProjectileForTiming, "duration", "StackCraft Projectile_Arrow.duration");
  const magicDuration = stackCraftMagicProjectileForTiming == null
    ? null
    : yamlScalarPropertyValue(stackCraftMagicProjectileForTiming, "duration", "StackCraft Projectile_Magic.duration");
  if (stackCraftArrowProjectileForTiming == null) {
    fail("缺少 StackCraft 箭矢投射物 Prefab，无法从 duration 字段派生远程攻击表现前摇。");
  }
  if (stackCraftMagicProjectileForTiming == null) {
    fail("缺少 StackCraft 魔法投射物 Prefab，无法从 duration 字段派生魔法攻击表现前摇。");
  }
  if (arrowDuration != null && magicDuration != null && arrowDuration !== magicDuration) {
    fail(`StackCraft 箭矢 / 魔法投射物 duration 不一致：Arrow=${arrowDuration}，Magic=${magicDuration}；当前 Gameplay 不能用单一投射物表现前摇常量。`);
  }
  if (arrowDuration != null) {
    assertCsharpConstFloatEquals(
      tabletopSource,
      "ProjectileAttackPreActivationSeconds",
      csharpFloatLiteral(arrowDuration),
      "牌桌远程 / 魔法攻击的表现前摇");
  }
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "public bool CanStackOnto",
    [
      "TabletopCardStack sourceStack = Cards.GetStackContaining(sourceCardId);",
      "TabletopCardStack targetStack = Cards.GetStackContaining(targetCardId);",
      "CanUseTargetStackForDraggedSegment(sourceStack, sourceCardId, targetStack, targetCardId)",
      "CardDefinition sourceDefinition = RequireCardDefinition(sourceCard.ContentId, \"判断牌桌合堆来源\");",
      "CardDefinition targetBottomDefinition = RequireCardDefinition(",
      "targetStack.BottomCard.ContentId",
      "PlacementRules.StackingRules.CanStack(sourceDefinition, targetBottomDefinition)",
    ],
    "牌桌普通合堆必须按 StackCraft 目标牌堆底牌和当前地区 GAS 标签堆叠规则判定");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "public bool TryDropStackOnto",
    [
      "RequireNoBattleParticipantInDetachedTail(sourceCardId, \"拖拽合并牌堆\");",
      "RequireNoBattleParticipantInAffectedStack(targetCardId, \"拖拽合并牌堆\");",
      "if (!CanStackOnto(sourceCardId, targetCardId))",
      "TabletopCardStack targetStack = Cards.GetStackContaining(targetCardId);",
      "TabletopCardId targetBottomCardId = targetStack.BottomCard.Id;",
      "TabletopCardStack sourceStack = Cards.DetachStackAt(sourceCardId);",
      "mergedStack = Cards.MergeStackOnto(sourceStack.BottomCard.Id, targetBottomCardId);",
    ],
    "牌桌普通合堆释放必须先复核规则，再拆出拖拽牌段并合到目标底牌所在堆");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "private static bool CanUseTargetStackForDraggedSegment",
    [
      "if (!ReferenceEquals(sourceStack, targetStack))",
      "return true;",
      "int segmentStartIndex = sourceStack.GetDraggedSegmentStartIndex(sourceCardId);",
      "int targetIndex = sourceStack.IndexOf(targetCardId);",
      "return segmentStartIndex > 0 && targetIndex >= 0 && targetIndex < segmentStartIndex;",
    ],
    "牌桌同堆拖拽只允许把上方牌段合回原堆下方段");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "private ActionInstance StartActionInstance",
    [
      "if (TryFindBattleContaining(cardIds[cardIndex], out Battle battle))",
      "仍属于活动战斗 {battle.Id}",
      "if (IsActiveActionParticipant(cardIds[cardIndex]))",
      "已参与活动行动，必须先完成或取消该行动后才能启动新的普通行动。",
    ],
    "普通行动启动入口拒绝战斗参与牌和已参与活动行动牌方法");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "private int CalculateCardLimitBonus()",
    [
      "IReadOnlyList<TabletopCardStack> stacks = Cards.Stacks;",
      "CardDefinition definition = RequireCardDefinition(cards[cardIndex].ContentId, \"计算牌桌上限加成\");",
      "cardLimitBonus = checked(cardLimitBonus + definition.CardLimitBonus);",
    ],
    "牌桌从当前卡牌派生卡牌上限加成方法");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "private void RefreshPlacementRulesForCurrentCards",
    [
      "int cardLimitBonus = CalculateCardLimitBonus();",
      "bool isShrinking = cardLimitBonus < m_currentPlacementCardLimitBonus;",
      "TabletopCardPlacementRules previousPlacementRules = m_currentPlacementRules;",
      "m_currentPlacementCardLimitBonus = cardLimitBonus;",
      "m_currentPlacementRules = m_basePlacementRules.CreateForCardLimitBonus(cardLimitBonus);",
      "Cards.MoveLockedStacksWithTopRestrictedBand(previousPlacementRules, PlacementRules);",
      "if (reflowExistingStacks && isShrinking)",
      "Cards.ReflowPlacement(PlacementRules);",
    ],
    "牌桌根据卡牌上限加成刷新当前放置规则方法");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "internal void AdvanceRealTime(float deltaSeconds)",
    [
      "AdvancePeriodicCardProduction(deltaSeconds);",
      "AdvanceAutomaticMovement(deltaSeconds);",
    ],
    "牌桌真实秒推进入口接入 StackCraft CardAI 周期行为");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "private void AdvanceAutomaticMovement(float deltaSeconds)",
    [
      "CardDefinition definition = RequireCardDefinition(card.ContentId, \"推进自动移动\");",
      "if (!definition.HasAutomaticMovement)",
      "bool shouldMove = card.AdvanceAutomaticMovement(",
      "definition.AutomaticMovementIntervalSeconds",
      "CanCardMoveAutomaticallyNow(card.Id)",
      "!ShouldStayInAutomaticMovementRetentionStack(card)",
      "m_automaticMovementRequests.Add(new AutomaticMovementRequest(card.Id));",
      "TryExecuteAutomaticHostileBehavior(card, definition)",
      "TryMoveCardRandomly(card, definition)",
    ],
    "牌桌自动移动入口消费 StackCraft moveInterval 并保持敌对优先行为");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "private bool TryMoveCardRandomly",
    [
      "if (definition.AutomaticMovementMaxAttempts <= 0)",
      "if (!float.IsFinite(definition.AutomaticMovementRadius) || definition.AutomaticMovementRadius <= 0f)",
      "Vector2 basePosition = card.Position;",
      "for (int attempt = 0; attempt < definition.AutomaticMovementMaxAttempts; attempt++)",
      "float angle = m_authoritativeRandom.NextFloat(0f, math.PI * 2f);",
      "Vector2 candidatePosition = basePosition + direction * definition.AutomaticMovementRadius;",
      "IsAutomaticMovementCandidateValid(card, candidatePosition)",
      "TryPlaceSingleCard(card.Id, candidatePosition, out _)",
    ],
    "牌桌随机巡逻消费 StackCraft moveRadius / maxAttemptsPerMove 并在无效候选点重试");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "private bool TryMoveCardTowards",
    [
      "Vector2 candidatePosition = card.Position + direction.normalized * movementRadius;",
      "IsAutomaticMovementCandidateValid(card, candidatePosition)",
      "TryPlaceSingleCard(card.Id, candidatePosition, out _)",
    ],
    "牌桌敌对追击使用同一自动移动半径和候选点有效性检查");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "private bool IsAutomaticMovementCandidateValid(TabletopCard card, Vector2 position)",
    [
      "Rect footprint = PlacementRules.Geometry.CalculateFootprint(",
      "ResolveCardSize(card.ContentId, PlacementRules.Geometry.CardSize)",
      "TabletopCardPlacementArea area = PlacementRules.Area;",
      "if (!IsRectInside(area.Bounds, footprint))",
      "for (int restrictedIndex = 0; restrictedIndex < area.RestrictedAreas.Count; restrictedIndex++)",
      "if (RectanglesOverlap(footprint, area.RestrictedAreas[restrictedIndex]))",
    ],
    "牌桌自动移动候选点对齐 StackCraft Board.IsPointValid 的边界和禁放区检查");
  assertCsharpNthDeclarationAndBlockContainsOrdered(
    tabletopSource,
    "internal Tabletop(",
    0,
    [
      "Cards = new TabletopCards(cardIdSequence, ResolveCardSize);",
    ],
    "新建牌桌构造函数必须把卡牌尺寸解析器注入 TabletopCards");
  assertCsharpNthDeclarationAndBlockContainsOrdered(
    tabletopSource,
    "internal Tabletop(",
    1,
    [
      "Cards = TabletopCards.Restore(",
      "RestoreCardFromSnapshot,",
      "ResolveCardSize);",
    ],
    "恢复牌桌构造函数必须把卡牌尺寸解析器注入 TabletopCards.Restore");
  assertCsharpBlockContainsOrdered(
    tabletopSource,
    "private Vector2 ResolveCardSize",
    [
      "CardDefinition definition = RequireCardDefinition(contentId, \"解析牌桌卡牌尺寸\");",
      "return definition.GetViewSize(defaultCardSize);",
    ],
    "牌桌尺寸解析必须由卡牌内容定义接管地区默认尺寸");
}

const projectileViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopProjectileView.cs");
if (projectileViewSource == null) {
  fail("缺少投射物视图源码，无法证明投射物飞行和朝向公式。");
} else {
  assertCsharpBlockContainsOrdered(
    projectileViewSource,
    "public sealed class TabletopProjectileView",
    [
      "private SpriteRenderer m_renderer;",
      "private Sprite m_rangedSprite;",
      "private Sprite m_magicSprite;",
      "private Tween m_moveTween;",
    ],
    "投射物视图 StackCraft DOTween 依赖结构");
  assertCsharpBlockContainsOrdered(
    projectileViewSource,
    "internal void Play(",
    [
      "m_renderer.sprite = ResolveProjectileSprite(combatTypeTagCode);",
      "KillMoveTween();",
      "transform.localPosition = start;",
      "ApplyRotation(start, end);",
      "m_renderer.sortingOrder = sortingOrder;",
      "gameObject.SetActive(true);",
      "DOLocalMove(end, durationSeconds)",
      "SetEase(Ease.Linear)",
      "SetUpdate(true)",
      ".OnComplete(() =>",
      "m_isPlaying = false;",
      "gameObject.SetActive(false)",
    ],
    "投射物视图 Play StackCraft CombatProjectile.Fire 飞行链");
  assertCsharpBlockContainsOrdered(
    projectileViewSource,
    "private void ApplyRotation(",
    [
      "Vector3 direction = end - start;",
      "direction.y = 0f;",
      "direction.sqrMagnitude <= 0.000001f",
      "Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(90f, 0f, 0f)",
    ],
    "投射物视图 ApplyRotation StackCraft 朝向公式");
  for (const obsoleteToken of [
    "m_elapsedSeconds",
    "Vector3.Lerp(",
    "Mathf.Clamp01(m_elapsedSeconds / m_durationSeconds)",
  ]) {
    if (projectileViewSource.includes(obsoleteToken)) {
      fail(`投射物视图仍保留手写 Lerp 飞行近似算法，应使用 StackCraft DOTween 线性移动链：${obsoleteToken}`);
    }
  }
}

const stackCraftRegionGeometryPaths = [
  "Assets/Gameplay/Tests/地基测试地区.asset",
  "Assets/Gameplay/Tests/地基场景测试地区.asset",
  "Assets/Gameplay/Tests/地基第二场景测试地区.asset",
  "Assets/Gameplay/Tests/地基战斗测试地区.asset",
];

const stackCraftMainSceneText = readIfExists("Assets/StackCraft/Scenes/Main.unity");
const stackCraftIslandSceneText = readIfExists("Assets/StackCraft/Scenes/Island.unity");
const stackCraftBoard01PrefabText = readIfExists("Assets/StackCraft/Prefabs/Boards/Board01.prefab");
const stackCraftBoard02PrefabText = readIfExists("Assets/StackCraft/Prefabs/Boards/Board02.prefab");
const stackCraftBoard01Yaml = stackCraftBoard01PrefabText == null
  ? null
  : unityYamlObjects(stackCraftBoard01PrefabText);
const stackCraftBoard02Yaml = stackCraftBoard02PrefabText == null
  ? null
  : unityYamlObjects(stackCraftBoard02PrefabText);
const stackCraftBoard01Placement = stackCraftBoard01Yaml == null
  ? null
  : deriveBoardPlacementFromStackCraft(stackCraftBoard01Yaml, "Board01", "StackCraft Board01");
const stackCraftBoard02Placement = stackCraftBoard02Yaml == null
  ? null
  : deriveBoardPlacementFromStackCraft(stackCraftBoard02Yaml, "Board02", "StackCraft Board02");
const stackCraftRegionCardGeometry = deriveCardPlacementGeometryFromStackCraft(
  readIfExists("Assets/StackCraft/Prefabs/Cards/Card_Character.prefab"),
  stackCraftDefaultCardSettingsText,
  "StackCraft 地区卡牌几何");

if (stackCraftMainSceneText == null) {
  fail("缺少 StackCraft Main 场景，无法从参考场景派生桌面背景。");
}
if (stackCraftIslandSceneText == null) {
  fail("缺少 StackCraft Island 场景，无法从参考场景派生水面背景。");
}
if (stackCraftBoard01PrefabText == null) {
  fail("缺少 StackCraft Board01 Prefab，无法从参考 Prefab 派生牌桌边界。");
}
if (stackCraftBoard02PrefabText == null) {
  fail("缺少 StackCraft Board02 Prefab，无法从参考 Prefab 派生 Island 牌桌边界。");
}

for (const regionPath of stackCraftRegionGeometryPaths) {
  const regionText = readIfExists(regionPath);
  if (regionText == null) {
    fail(`缺少牌桌测试地区作者源：${regionPath}`);
    continue;
  }
  if (stackCraftBoard01Placement != null) {
    assertYamlRectBlockEquals(
      regionText,
      "m_bounds",
      stackCraftBoard01Placement.bounds,
      `${regionPath} StackCraft Board01 规则边界`);
    assertYamlSingleRectListEquals(
      regionText,
      "m_restrictedAreas",
      stackCraftBoard01Placement.restricted,
      `${regionPath} StackCraft Board01 页眉禁放区`);
  }
  if (stackCraftRegionCardGeometry != null) {
    assertYamlInlineVector2Equals(
      regionText,
      "m_cardSize",
      stackCraftRegionCardGeometry.cardSize,
      `${regionPath} StackCraft 卡牌可见尺寸`);
    assertYamlInlineVector2Equals(
      regionText,
      "m_cardMargin",
      stackCraftRegionCardGeometry.cardMargin,
      `${regionPath} StackCraft 卡牌占地边距`);
    assertYamlInlineVector2Equals(
      regionText,
      "m_stackStep",
      stackCraftRegionCardGeometry.stackStep,
      `${regionPath} StackCraft 牌堆步进`);
  }
  if (stackCraftOverlapResolveMaxIterations != null) {
    assertYamlScalarEquals(
      regionText,
      "m_overlapResolveMaxIterations",
      stackCraftOverlapResolveMaxIterations,
      `${regionPath} StackCraft 重叠解算迭代次数`);
  }
  if (stackCraftSpawnAttachRadius != null) {
    assertYamlScalarEquals(
      regionText,
      "m_spawnAttachRadius",
      stackCraftSpawnAttachRadius,
      `${regionPath} StackCraft 出生吸附半径`);
  }
  if (tabletopCardLimitBonusExpansionPerPoint != null) {
    assertYamlInlineVector2Equals(
      regionText,
      "m_cardLimitBonusExpansionPerPoint",
      {
        x: tabletopCardLimitBonusExpansionPerPoint.x,
        y: tabletopCardLimitBonusExpansionPerPoint.y,
      },
      `${regionPath} StackCraft Board BlendShape 每点扩展`);
  }
}

if (stackCraftBoard01Placement != null && stackCraftRegionCardGeometry != null && foundationSceneMenuSource != null) {
  for (const [fieldName, expectedInitializer] of [
    ["StackCraftBoardPlacementBounds", csharpRectConstructor(stackCraftBoard01Placement.bounds)],
    ["StackCraftBoardHeaderRestrictedArea", csharpRectConstructor(stackCraftBoard01Placement.restricted)],
    ["StackCraftBoardCardSize", csharpVector2ConstructorFromNumbers(stackCraftRegionCardGeometry.cardSize.x, stackCraftRegionCardGeometry.cardSize.y)],
    ["StackCraftBoardCardMargin", csharpVector2ConstructorFromNumbers(stackCraftRegionCardGeometry.cardMargin.x, stackCraftRegionCardGeometry.cardMargin.y)],
    ["StackCraftBoardStackStep", csharpVector2ConstructorFromNumbers(stackCraftRegionCardGeometry.stackStep.x, stackCraftRegionCardGeometry.stackStep.y)],
    ["StackCraftBoardCardLimitBonusExpansionPerPoint", csharpVector2ConstructorFromNumbers(stackCraftBoard01Placement.expansionPerPoint.x, stackCraftBoard01Placement.expansionPerPoint.y)],
    ["StackCraftBoardLocalBoundsSize", csharpVector3ConstructorFromNumbers(stackCraftBoard01Placement.localBoundsSize.x, stackCraftBoard01Placement.localBoundsSize.y, stackCraftBoard01Placement.localBoundsSize.z)],
  ]) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      fieldName,
      expectedInitializer,
      "测试场景生成器 StackCraft Board01 / 卡牌几何命名常量");
  }
  if (stackCraftOverlapResolveMaxIterations != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftBoardOverlapResolveMaxIterations",
      stackCraftOverlapResolveMaxIterations,
      "测试场景生成器 StackCraft Board01 / 卡牌几何命名常量");
  }
  if (stackCraftSpawnAttachRadius != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftBoardSpawnAttachRadius",
      csharpFloatLiteral(stackCraftSpawnAttachRadius),
      "测试场景生成器 StackCraft 出生吸附半径命名常量");
  }
  if (stackCraftAutomaticMovementIntervalSeconds != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftAutomaticMovementIntervalSeconds",
      csharpFloatLiteral(stackCraftAutomaticMovementIntervalSeconds),
      "测试场景生成器 StackCraft 自动移动间隔命名常量");
  }
  if (stackCraftAutomaticMovementRadius != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftAutomaticMovementRadius",
      csharpFloatLiteral(stackCraftAutomaticMovementRadius),
      "测试场景生成器 StackCraft 自动移动半径命名常量");
  }
  if (stackCraftAutomaticMovementMaxAttempts != null) {
    assertCsharpFieldInitializerEquals(
      foundationSceneMenuSource,
      "StackCraftAutomaticMovementMaxAttempts",
      stackCraftAutomaticMovementMaxAttempts,
      "测试场景生成器 StackCraft 自动移动尝试次数命名常量");
  }
  for (const [assignmentTarget, expectedInitializer] of [
    ["bounds.rectValue", "StackCraftBoardPlacementBounds"],
    ["restrictedAreas.arraySize", "1"],
    ["restrictedAreas.GetArrayElementAtIndex(0).rectValue", "StackCraftBoardHeaderRestrictedArea"],
    ["cardSize.vector2Value", "StackCraftBoardCardSize"],
    ["cardMargin.vector2Value", "StackCraftBoardCardMargin"],
    ["stackStep.vector2Value", "StackCraftBoardStackStep"],
    ["overlapResolveMaxIterations.intValue", "StackCraftBoardOverlapResolveMaxIterations"],
    ["spawnAttachRadius.floatValue", "StackCraftBoardSpawnAttachRadius"],
    ["cardLimitBonusExpansionPerPoint.vector2Value", "StackCraftBoardCardLimitBonusExpansionPerPoint"],
  ]) {
    assertCsharpAssignmentEquals(
      foundationSceneMenuSource,
      assignmentTarget,
      expectedInitializer,
      "测试地区生成器 StackCraft Board01 / 卡牌几何参数");
  }
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureBeginningPackBusinessAssets()",
    [
      "WriteAutomaticMovementFields(",
      "TestBeginningChickenPath",
      "intervalSeconds: StackCraftAutomaticMovementIntervalSeconds",
      "radius: StackCraftAutomaticMovementRadius",
      "maxAttempts: StackCraftAutomaticMovementMaxAttempts",
      "WriteAutomaticMovementFields(",
      "TestBeginningSlimePath",
      "intervalSeconds: StackCraftAutomaticMovementIntervalSeconds",
      "radius: StackCraftAutomaticMovementRadius",
      "maxAttempts: StackCraftAutomaticMovementMaxAttempts",
    ],
    "测试场景生成器 StackCraft 自动移动参数写入入口");
}

for (const [assetText, assetPath] of [
  [testBeginningChickenText, "Assets/Gameplay/Tests/地基开端鸡.asset"],
  [testBeginningSlimeText, "Assets/Gameplay/Tests/地基开端史莱姆.asset"],
]) {
  if (assetText == null) {
    fail(`缺少 StackCraft 自动移动代表性测试卡牌资产：${assetPath}`);
    continue;
  }
  assertYamlScalarEquals(
    assetText,
    "m_automaticMovementIntervalSeconds",
    stackCraftAutomaticMovementIntervalSeconds,
    `${assetPath} StackCraft 自动移动间隔`);
  assertYamlScalarEquals(
    assetText,
    "m_automaticMovementRadius",
    stackCraftAutomaticMovementRadius,
    `${assetPath} StackCraft 自动移动半径`);
  assertYamlScalarEquals(
    assetText,
    "m_automaticMovementMaxAttempts",
    stackCraftAutomaticMovementMaxAttempts,
    `${assetPath} StackCraft 自动移动尝试次数`);
}

if (stackCraftBoard02Placement != null && stackCraftBoard01Placement != null) {
  for (const [property, firstValue, secondValue] of [
    ["bounds", stackCraftBoard01Placement.bounds, stackCraftBoard02Placement.bounds],
    ["restricted", stackCraftBoard01Placement.restricted, stackCraftBoard02Placement.restricted],
    ["localBoundsSize", stackCraftBoard01Placement.localBoundsSize, stackCraftBoard02Placement.localBoundsSize],
  ]) {
    if (JSON.stringify(firstValue) !== JSON.stringify(secondValue)) {
      fail(`StackCraft Board01 和 Board02 的 ${property} 几何不一致，当前共享地区几何不能同时代表两者。`);
    }
  }
}

if (foundationSceneMenuSource != null) {
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static TabletopView CreateTabletopTestRoot(",
    [
      "TabletopView tabletopView = tabletopRoot.AddComponent<TabletopView>();",
      "tabletopRoot.AddComponent<TabletopCardDragInput>();",
      "tabletopRoot.AddComponent<TabletopInteraction>();",
      "FoundationTestSceneHarness controller =",
      "tabletopRoot.AddComponent<FoundationTestSceneHarness>();",
      "RequireProperty(serializedController, \"m_scenarioId\")",
      "RequireProperty(serializedController, \"m_initialLayout\")",
      "RequireProperty(serializedController, \"m_authoritativeRandomSeedOverride\")",
    ],
    "测试场景生成器只创建同对象牌桌测试组件，由 Harness 运行时自动取得");
  assertCsharpFieldInitializerEquals(
    foundationSceneMenuSource,
    "StackCraftParityAuthoritativeRandomSeed",
    "20260821u",
    "测试场景生成器 StackCraft 同态固定随机根种子");
  for (const forbiddenToken of [
    "serializedController.FindProperty(\"m_tabletopView\").objectReferenceValue = tabletopView;",
    "serializedController.FindProperty(\"m_dragInput\").objectReferenceValue = dragInput;",
    "serializedController.FindProperty(\"m_tabletopInteraction\").objectReferenceValue = tabletopInteraction;",
  ]) {
    if (foundationSceneMenuSource.includes(forbiddenToken)) {
      fail(`测试场景生成器不得向 Harness 写入同对象组件引用：${forbiddenToken}`);
    }
  }
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void WriteTestTabletopPlacement",
    [
      "placement.FindPropertyRelative(\"m_bounds\")",
      "placement.FindPropertyRelative(\"m_restrictedAreas\")",
      "placement.FindPropertyRelative(\"m_cardSize\")",
      "placement.FindPropertyRelative(\"m_cardMargin\")",
      "placement.FindPropertyRelative(\"m_stackStep\")",
      "placement.FindPropertyRelative(\"m_overlapResolveMaxIterations\")",
      "placement.FindPropertyRelative(\"m_spawnAttachRadius\")",
      "placement.FindPropertyRelative(\"m_cardLimitBonusExpansionPerPoint\")",
      "bounds.rectValue = StackCraftBoardPlacementBounds;",
      "restrictedAreas.arraySize = 1;",
      "restrictedAreas.GetArrayElementAtIndex(0).rectValue = StackCraftBoardHeaderRestrictedArea;",
      "cardSize.vector2Value = StackCraftBoardCardSize;",
      "cardMargin.vector2Value = StackCraftBoardCardMargin;",
      "stackStep.vector2Value = StackCraftBoardStackStep;",
      "overlapResolveMaxIterations.intValue = StackCraftBoardOverlapResolveMaxIterations;",
      "spawnAttachRadius.floatValue = StackCraftBoardSpawnAttachRadius;",
      "cardLimitBonusExpansionPerPoint.vector2Value = StackCraftBoardCardLimitBonusExpansionPerPoint;",
    ],
    "测试地区生成器 StackCraft Board01 牌桌放置写入入口");
}

const tabletopPlacementSolverSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Placement/TabletopCardStackPlacementSolver.cs");
if (tabletopPlacementSolverSource == null) {
  fail("缺少牌桌放置解算器源码，无法证明固定交易工位可以位于 StackCraft 页眉区域。");
} else {
  assertCsharpDeclarationAndBlockContainsOrdered(
    tabletopPlacementSolverSource,
    "public static TabletopCardStackSpatialResult Solve",
    [
      "public static TabletopCardStackSpatialResult Solve",
      "int maxIterations = TabletopCardPlacementRules.DefaultOverlapResolveMaxIterations",
      "if (maxIterations <= 0)",
      "for (iterations = 0; iterations < maxIterations; iterations++)",
    ],
    "牌桌放置解算器消费 StackCraft 重叠解算迭代参数");
  if (tabletopPlacementSolverSource.includes("MaxIterations = 64")) {
    fail("牌桌放置解算器仍保留旧硬编码 MaxIterations = 64；重叠解算迭代次数必须来自放置规则并默认对齐 StackCraft 8。");
  }
  assertCsharpBlockContainsOrdered(
    tabletopPlacementSolverSource,
    "private static bool ResolveAreaConstraints",
    [
      "if (body.IsLocked)",
      "continue;",
      "Vector2 center = ClampToBounds(area.Bounds, body.FootprintCenter, body.Size);",
      "for (int restrictedIndex = 0; restrictedIndex < area.RestrictedAreas.Count; restrictedIndex++)",
      "if (TabletopCardPlacementArea.IsFullWidthTopRestrictedBand(area.Bounds, restricted))",
      "center = MoveBelowTopRestrictedBand(center, body.Size, restricted);",
      "continue;",
    ],
    "牌桌放置解算器移动阶段对齐 StackCraft 顶部页眉禁放区下压语义");
  assertCsharpBlockContainsOrdered(
    tabletopPlacementSolverSource,
    "private static Vector2 MoveBelowTopRestrictedBand",
    [
      "float topEdge = center.y + size.y * 0.5f;",
      "if (topEdge > restricted.yMin + Epsilon)",
      "center.y = restricted.yMin - size.y * 0.5f;",
    ],
    "牌桌放置解算器顶部页眉禁放区下压公式");
  assertCsharpBlockContainsOrdered(
    tabletopPlacementSolverSource,
    "private static bool HasUnresolvedConstraints",
    [
      "if (body.IsLocked)",
      "continue;",
      "for (int restrictedIndex = 0; restrictedIndex < area.RestrictedAreas.Count; restrictedIndex++)",
    ],
    "牌桌放置解算器收敛检查阶段锁定工位跳过页眉禁放区检查");
  assertCsharpBlockContainsOrdered(
    tabletopPlacementSolverSource,
    "private static bool TryCalculateSeparation",
    [
      "float penetrationX = firstHalf.x + secondHalf.x - Mathf.Abs(deltaX);",
      "float penetrationY = firstHalf.y + secondHalf.y - Mathf.Abs(deltaY);",
      "if (penetrationX < penetrationY)",
      "separation = new Vector2(penetrationX * direction, 0f);",
      "separation = new Vector2(0f, penetrationY * direction2);",
    ],
    "牌桌放置解算器与 StackCraft CardPhysicsSolver 保持穿透相等时沿 Z/Y 轴分离");
}

const tabletopCardsSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Cards/TabletopCards.cs");
const tabletopCardStackSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Cards/TabletopCardStack.cs");
if (tabletopCardsSource == null) {
  fail("缺少牌桌卡牌集合源码，无法证明正式放置提交链使用地区放置规则的重叠解算迭代次数。");
} else {
  if (tabletopCardStackSource == null) {
    fail("缺少牌堆源码，无法证明 StackCraft 的领牌 / 底牌顺序语义。");
  } else {
    assertCsharpBlockContainsOrdered(
      tabletopCardStackSource,
      "public sealed class TabletopCardStack",
      [
        "public TabletopCard TopCard => m_cards[0];",
        "public TabletopCard BottomCard => m_cards[m_cards.Count - 1];",
        "internal void MergeDroppedStack",
        "m_cards.AddRange(source.m_cards);",
        "internal int GetDraggedSegmentStartIndex",
        "return cardIndex == 0 ? 0 : cardIndex;",
      ],
      "牌堆顺序必须对齐 StackCraft：第 0 张是拖拽领牌，最后一张是合堆目标底牌");
    for (const obsoleteToken of [
      "public TabletopCard BottomCard => m_cards[0]",
      "public TabletopCard TopCard => m_cards[m_cards.Count - 1]",
      "internal void AppendOnTop",
    ]) {
      if (tabletopCardStackSource.includes(obsoleteToken)) {
        fail(`牌堆仍保留与 StackCraft 相反的顺序或旧合堆命名：${obsoleteToken}`);
      }
    }
  }

  const solveCalls = csharpCallInvocations(
    tabletopCardsSource,
    "TabletopCardStackPlacementSolver.Solve",
    "牌桌卡牌集合放置解算调用");
  if (solveCalls.length === 0) {
    fail("牌桌卡牌集合没有调用 TabletopCardStackPlacementSolver.Solve，无法证明放置解算进入正式牌桌链路。");
  }
  for (const call of solveCalls) {
    if (call.args == null || call.args.length < 3) {
      fail(`牌桌卡牌集合第 ${lineNumber(tabletopCardsSource, call.index)} 行调用放置解算时没有传入重叠解算迭代次数实参。`);
      continue;
    }
    const maxIterationsArgument = call.args[2];
    if (normalizeCsharpExpression(maxIterationsArgument) !== normalizeCsharpExpression("placementRules.OverlapResolveMaxIterations")) {
      fail(`牌桌卡牌集合第 ${lineNumber(tabletopCardsSource, call.index)} 行调用放置解算时第三个实参不是地区规则的重叠解算迭代次数：${maxIterationsArgument}。`);
    }
  }
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "internal bool MoveLockedStacksWithTopRestrictedBand",
    [
      "previousRules.Area.TryGetFullWidthTopRestrictedBand(out Rect previousBand)",
      "currentRules.Area.TryGetFullWidthTopRestrictedBand(out Rect currentBand)",
      "Vector2 delta = currentBand.center - previousBand.center;",
      "if (!stack.IsPlacementLocked)",
      "Rect previousFootprint = CalculateFootprint(previousRules.Geometry, stack);",
      "if (!Overlaps(previousFootprint, previousBand))",
      "stack.MoveTo(stack.Position + delta);",
      "Revision++;",
    ],
    "牌桌卡牌集合随 StackCraft Board 页眉移动锁定交易区方法");
  assertCsharpDeclarationAndBlockContainsOrdered(
    tabletopCardsSource,
    "public sealed class TabletopCards",
    [
      "private readonly Func<ContentId, Vector2, Vector2> m_resolveCardSize;",
      "internal TabletopCards(",
      "Func<ContentId, Vector2, Vector2> resolveCardSize = null",
      "m_resolveCardSize = resolveCardSize ?? ResolveDefaultCardSize;",
    ],
    "TabletopCards 构造函数必须保存卡牌尺寸解析委托");
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "internal static TabletopCards Restore",
    [
      "TabletopCards restored = new TabletopCards(cardIdSequence, resolveCardSize);",
    ],
    "TabletopCards.Restore 必须延续卡牌尺寸解析委托");
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "internal TabletopCardStack CreateCardStack",
    [
      "TabletopCardId bottomCardId = CreateNextStackBottomCardId(contentId, position, count);",
      "new TabletopCardId(m_cardIdSequence.NextValue + (ulong)i)",
      "return AddCardStack(",
      "cards,",
      "solvedPositions[bottomCardId]",
    ],
    "TabletopCards 新建多张牌堆时必须按 StackCraft 追加顺序把最后一张作为底牌空间键");
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "private TabletopCardId CreateNextStackBottomCardId",
    [
      "EnsureCanCreateCards(count);",
      "return new TabletopCardId(m_cardIdSequence.NextValue + (ulong)(count - 1));",
    ],
    "TabletopCards 多张出生预检必须使用最后一张卡作为 StackCraft 底牌空间键");
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "private TabletopCardStackSpatialBody CreateSpatialBody",
    [
      "ResolveCardSize(stack.TopCard.ContentId, geometry)",
    ],
    "TabletopCards 创建现有牌堆空间体时必须使用顶牌内容尺寸");
  assertCsharpNthDeclarationAndBlockContainsOrdered(
    tabletopCardsSource,
    "private TabletopCardStackSpatialBody CreateSpatialBody",
    1,
    [
      "ResolveCardSize(topCardContentId, geometry)",
    ],
    "TabletopCards 创建候选牌堆空间体时必须使用候选顶牌内容尺寸");
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "private Rect CalculateFootprint",
    [
      "ResolveCardSize(stack.TopCard.ContentId, geometry)",
    ],
    "TabletopCards 计算牌堆占地时必须使用顶牌内容尺寸");
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "private Vector2 ResolveCardSize",
    [
      "Vector2 cardSize = m_resolveCardSize(contentId, geometry.CardSize);",
      "cardSize.x <= 0f || cardSize.y <= 0f",
      "return cardSize;",
    ],
    "TabletopCards 内部尺寸解析必须校验有限正数");
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "internal bool TryPlaceStack",
    [
      "TryCreateStackPlacementPlan(cardId, position, placementRules, out StackPlacementPlan plan)",
      "TabletopCardStack source = plan.Source;",
      "bool changed = plan.SplitIndex > 0;",
      "placedStack = source.DetachFrom(plan.SplitIndex);",
      "m_stacks.Add(placedStack);",
      "changed |= ApplySolvedPositions(plan.SolvedPositions);",
      "Revision++;",
    ],
    "牌桌卡牌集合 TryPlaceStack 必须先按拖拽卡牌创建拆堆计划，再把选中卡及其上方牌段作为一个整体放置");
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "private bool TryCreateStackPlacementPlan",
    [
      "TabletopCardStack source = GetStackContaining(cardId);",
      "int splitIndex = source.GetDraggedSegmentStartIndex(cardId);",
      "int candidateStackCount = m_stacks.Count + ((splitIndex > 0) ? 1 : 0);",
      "if (splitIndex == 0)",
      "source.BottomCard.Id",
      "position",
      "source.Cards.Count",
      "source.TopCard.ContentId",
      "source.Cards[splitIndex - 1].Id",
      "source.Position",
      "splitIndex",
      "source.TopCard.ContentId",
      "source.BottomCard.Id",
      "source.Cards.Count - splitIndex",
      "isLocked: false",
      "topCardContentId: source.Cards[splitIndex].ContentId",
    ],
    "牌桌卡牌集合 TryCreateStackPlacementPlan 必须把拖拽尾段当作单个空间对象解算，不能把中间卡和上方卡拆散");
  assertCsharpBlockContainsOrdered(
    tabletopCardsSource,
    "internal void RequireCardChangesCanBePlaced",
    [
      "TabletopCardId bottomCardId = new TabletopCardId(",
      "m_cardIdSequence.NextValue + (ulong)(totalCreationCount + creation.Count - 1));",
      "topCardContentId: creation.ContentId",
    ],
    "TabletopCards 行动产物多张牌堆预检必须使用本次产物最后一张卡作为 StackCraft 底牌空间键");
  const obsoleteSpatialBodyCalls = [
    "spatialBodies.Add(placementRules.Geometry.CreateSpatialBody(stack.BottomCard.Id",
    "bodies.Add(placementRules.Geometry.CreateSpatialBody(bottomCardId",
    "bodies.Add(placementRules.Geometry.CreateSpatialBody(card.Id",
    "Rect previousFootprint = previousRules.Geometry.CalculateFootprint(",
  ];
  for (const obsoleteCall of obsoleteSpatialBodyCalls) {
    if (tabletopCardsSource.includes(obsoleteCall)) {
      fail(`牌桌卡牌集合仍存在绕过内容尺寸解析的旧空间体 / 占地调用：${obsoleteCall}`);
    }
  }
}

if (tabletopCardsEditModeTestsSource == null) {
  fail("缺少牌桌卡牌 EditMode 测试源码，无法证明拖拽拆堆语义有对象级回归保护。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopCardsEditModeTestsSource,
    "public void DetachStackAt_SelectedCardAndCardsAboveFormNewStack",
    [
      "state.MergeStackOnto(middle.Id, bottom.Id);",
      "TabletopCardStack original = state.MergeStackOnto(top.Id, middle.Id);",
      "TabletopCardStack detached = state.DetachStackAt(middle.Id);",
      "new TabletopCard[1] { bottom }",
      "new TabletopCard[2] { middle, top }",
    ],
    "牌桌卡牌测试必须覆盖从堆中间拿起卡牌时带走该卡和上方牌段");
  assertCsharpBlockContainsOrdered(
    tabletopCardsEditModeTestsSource,
    "public void TryPlaceStack_FromMiddleCardResolvesTheDetachedTailAsOneStack",
    [
      "tabletop.MergeStackOnto(middle.Id, bottom.Id);",
      "tabletop.MergeStackOnto(top.Id, bottom.Id);",
      "bool accepted = tabletop.TryPlaceStack(middle.Id, new Vector2(4f, -3f), out placed);",
      "new TabletopCard[1] { bottom }",
      "new TabletopCard[2] { middle, top }",
      "重叠解算必须移动整堆，不能把同一堆的中间卡和顶牌拆成两个空间对象。",
    ],
    "牌桌卡牌测试必须覆盖拖拽尾段放置时作为一个整体参与放置解算");
  assertCsharpBlockContainsOrdered(
    tabletopCardsEditModeTestsSource,
    "public void TryPlaceSingleCard_FromMiddleCardMovesOnlySelectedCard",
    [
      "bool accepted = state.TryPlaceSingleCard(",
      "middle.Id,",
      "Assert.That(placed.Cards, Is.EqualTo(new TabletopCard[] { middle }));",
      "Assert.That(original.Cards, Is.EqualTo(new TabletopCard[] { bottom, top }));",
    ],
    "牌桌卡牌测试必须区分玩家拖拽尾段和自动行为单卡移动，避免两种语义互相污染");
}

const foundationVillagerText = readIfExists("Assets/Gameplay/Tests/地基测试卡牌.asset");
if (foundationVillagerText == null) {
  fail("缺少地基测试 Villager 作者源，无法证明基础卡面数值来自 StackCraft。");
} else {
  const stackCraftVillagerText = readIfExists("Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
  const xAttributeSource = readIfExists("Assets/Scripts/Gen/XAttribute.gen.cs");
  if (stackCraftVillagerText == null) {
    fail("缺少 StackCraft Villager 作者源：Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
  } else {
    assertYamlScalarStringEquals(
      foundationVillagerText,
      "m_displayName",
      "村民",
      "地基测试 Villager 显示名");
    assertYamlScalarStringEquals(
      foundationVillagerText,
      "m_description",
      "健康的村民。",
      "地基测试 Villager 描述");
  }
  if (xAttributeSource == null) {
    fail("缺少 GAS 生成属性码：Assets/Scripts/Gen/XAttribute.gen.cs");
  } else if (stackCraftVillagerText != null) {
    const villagerAttributeMappings = [
      ["Health", "maxHealth", "当前生命"],
      ["MaxHealth", "maxHealth", "最大生命"],
      ["Attack", "attack", "攻击"],
      ["Defense", "defense", "防御"],
      ["AttackSpeed", "attackSpeed", "攻速"],
      ["Accuracy", "accuracy", "命中"],
      ["Dodge", "dodge", "闪避"],
      ["CriticalChance", "criticalChance", "暴击率"],
      ["CriticalMultiplier", "criticalMultiplier", "暴击倍率"],
    ];
    assertUnityListItemCount(
      foundationVillagerText,
      "m_attributeOverrides",
      villagerAttributeMappings.length,
      "地基测试 Villager StackCraft 战斗属性覆盖");
    for (const [attributeName, stackCraftField, label] of villagerAttributeMappings) {
      const attributeCode = csharpConstIntValue(
        xAttributeSource,
        attributeName,
        `GAS XAttribute.${attributeName}`);
      const expectedValue = yamlScalarPropertyValue(
        stackCraftVillagerText,
        stackCraftField,
        `StackCraft Villager ${stackCraftField}`);
      assertAttributeOverrideEquals(
        foundationVillagerText,
        attributeCode,
        expectedValue,
        `地基测试 Villager ${label}`);
    }
  }
  if (foundationVillagerText.includes("m_attributeOverrides: []")) {
    fail("地基测试 Villager 仍使用默认 ASC 数值，会在卡面显示 100 而不是 StackCraft 原始 15。");
  }
}

for (const [localAsset, stackCraftAsset] of [
  ["Assets/Art/Sprites/CardArts/宝箱.png", "Assets/Art/Sprites/StackCraft/CardArts/TreasureChest.png"],
  ["Assets/Art/Sprites/CardArts/金币.png", "Assets/Art/Sprites/StackCraft/CardArts/Coin.png"],
]) {
  const localHash = sha256IfExists(localAsset);
  const sourceHash = sha256IfExists(stackCraftAsset);
  if (localHash == null) {
    fail(`缺少 StackCraft 表面素材中文作者入口副本：${localAsset}`);
  } else if (sourceHash == null) {
    fail(`缺少 StackCraft 表面素材原始副本：${stackCraftAsset}`);
  } else if (localHash !== sourceHash) {
    fail(`${localAsset} 与 ${stackCraftAsset} 不一致，不能用近似素材冒充 StackCraft 原始表面素材。`);
  }
}

const packSurfaceMaterialGuid = guidFromMetaPath(
  "Assets/Art/Materials/卡牌表面_卡包.mat.meta",
  "StackCraft PackInstance 卡包表面材质");
for (const packCase of [
  {
    localPath: "Assets/Gameplay/Tests/地基测试卡包.asset",
    stackCraftPath: "Assets/StackCraft/Resources/Packs/00_Pack_Starter.asset",
    artAddress: "PackArts_Starter",
    artMetaPath: "Assets/Art/Sprites/StackCraft/PackArts/Starter.png.meta",
    expectedDisplayName: "初始卡包",
    expectedDescription: "一个初始卡包。",
    label: "StackCraft Starter 卡包",
  },
  {
    localPath: "Assets/Gameplay/Tests/地基开端卡包.asset",
    stackCraftPath: "Assets/StackCraft/Resources/Packs/01_Pack_Beginning.asset",
    artAddress: "PackArts_Beginning",
    artMetaPath: "Assets/Art/Sprites/StackCraft/PackArts/Beginning.png.meta",
    expectedDisplayName: "开端卡包",
    expectedDescription: "一个开端卡包。",
    label: "StackCraft Beginning 卡包",
  },
]) {
  const localText = readIfExists(packCase.localPath);
  const stackCraftText = readIfExists(packCase.stackCraftPath);
  if (localText == null) {
    fail(`缺少 ${packCase.label} 的 CardLoop 作者源：${packCase.localPath}`);
    continue;
  }
  if (stackCraftText == null) {
    fail(`缺少 ${packCase.label} 的 StackCraft 参考作者源：${packCase.stackCraftPath}`);
    continue;
  }

  assertYamlScalarStringEquals(
    localText,
    "m_displayName",
    packCase.expectedDisplayName,
    `${packCase.localPath} ${packCase.label} 显示名`);
  assertYamlScalarStringEquals(
    localText,
    "m_description",
    packCase.expectedDescription,
    `${packCase.localPath} ${packCase.label} 描述`);
  assertSoftAssetReference(
    localText,
    "m_cardArt",
    packCase.artAddress,
    guidFromMetaPath(packCase.artMetaPath, `${packCase.label} 卡包图`),
    `${packCase.localPath} ${packCase.label} 卡包图`);
  assertSoftAssetReference(
    localText,
    "m_cardSurface",
    "卡牌表面_卡包",
    packSurfaceMaterialGuid,
    `${packCase.localPath} ${packCase.label} 卡包表面`);
  assertCardViewSizeAsset(
    localText,
    packCase.localPath,
    stackCraftPackInstanceViewSizeLiteral,
    "StackCraft PackInstance");
}

const vendorChestArtGuid = guidFromMetaPath(
  "Assets/Art/Sprites/CardArts/宝箱.png.meta",
  "StackCraft PackVendor 宝箱表面素材");
const vendorSurfaceGuid = guidFromMetaPath(
  "Assets/Art/Materials/卡牌表面_建筑.mat.meta",
  "StackCraft PackVendor 建筑表面材质");
for (const vendorCase of [
  {
    localPath: "Assets/Gameplay/Tests/地基卡包商贩.asset",
    displayName: "卡包商贩",
    expectedPrice: 2,
    expectedMinimumCompletedQuests: 0,
    label: "地基测试 Starter 商贩双付款测试夹具",
  },
  {
    localPath: "Assets/Gameplay/Tests/地基开端卡包商贩.asset",
    displayName: "开端卡包商贩",
    stackCraftPath: "Assets/StackCraft/Resources/Packs/01_Pack_Beginning.asset",
    label: "StackCraft Beginning 商贩",
  },
]) {
  const localText = readIfExists(vendorCase.localPath);
  if (localText == null) {
    fail(`缺少 ${vendorCase.label} 的 CardLoop 作者源：${vendorCase.localPath}`);
    continue;
  }

  const stackCraftText = vendorCase.stackCraftPath == null
    ? null
    : readIfExists(vendorCase.stackCraftPath);
  if (vendorCase.stackCraftPath != null && stackCraftText == null) {
    fail(`缺少 ${vendorCase.label} 的 StackCraft 参考作者源：${vendorCase.stackCraftPath}`);
    continue;
  }

  const expectedPrice = stackCraftText == null
    ? vendorCase.expectedPrice
    : yamlScalarPropertyValue(stackCraftText, "buyPrice", `${vendorCase.label}.buyPrice`);
  const expectedMinimumCompletedQuests = stackCraftText == null
    ? vendorCase.expectedMinimumCompletedQuests
    : yamlScalarPropertyValue(stackCraftText, "minQuests", `${vendorCase.label}.minQuests`);

  assertYamlScalarStringEquals(localText, "m_displayName", vendorCase.displayName, `${vendorCase.localPath} ${vendorCase.label} 显示名`);
  assertYamlScalarEquals(localText, "m_price", expectedPrice, `${vendorCase.localPath} ${vendorCase.label} 售价`);
  assertYamlScalarEquals(
    localText,
    "m_minimumCompletedQuests",
    expectedMinimumCompletedQuests,
    `${vendorCase.localPath} ${vendorCase.label} 解锁任务数`);
  assertSoftAssetReference(
    localText,
    "m_cardArt",
    "宝箱",
    vendorChestArtGuid,
    `${vendorCase.localPath} ${vendorCase.label} 宝箱卡图`);
  assertSoftAssetReference(
    localText,
    "m_cardSurface",
    "卡牌表面_建筑",
    vendorSurfaceGuid,
    `${vendorCase.localPath} ${vendorCase.label} 建筑表面`);
  assertCardViewSizeAsset(
    localText,
    vendorCase.localPath,
    stackCraftPackVendorViewSizeLiteral,
    "StackCraft PackVendor");
}

for (const packVendorAssetPath of walk("Assets")
  .map((file) => rel(file))
  .filter((file) => file.endsWith(".asset"))) {
  const packVendorAssetText = readIfExists(packVendorAssetPath);
  if (packVendorAssetText == null) continue;
  const packVendorAssetYaml = unityYamlObjects(packVendorAssetText);
  const packVendorObjects = unityMonoBehaviourObjectsByEditorClassIdentifier(
    packVendorAssetYaml,
    "Gameplay.Runtime::Gameplay.Content.PackVendorDefinition");
  if (packVendorObjects.length === 0) continue;
  if (packVendorObjects.length > 1) {
    fail(`${packVendorAssetPath} 命中多个 PackVendorDefinition 脚本对象，无法证明卡包商贩作者源唯一。`);
    continue;
  }
  assertYamlScalarEquals(
    packVendorObjects[0].text,
    "m_countsTowardCardLimit",
    "0",
    `${packVendorAssetPath} StackCraft 卡包商贩交易区不计入卡牌上限`);
}

const foundationRuntimeRootPrefabGuid = guidFromMetaPath(
  "Assets/Gameplay/Tests/牌桌/FoundationTestRuntimeRoot.prefab.meta",
  "Foundation 测试运行根 Prefab");
const foundationRuntimeRootPrefabText = readIfExists("Assets/Gameplay/Tests/牌桌/FoundationTestRuntimeRoot.prefab");
let foundationRuntimeRootPrefabFileId = null;
if (foundationRuntimeRootPrefabText == null) {
  fail("缺少 Foundation 测试运行根 Prefab：Assets/Gameplay/Tests/牌桌/FoundationTestRuntimeRoot.prefab。");
} else {
  const foundationRuntimeRootPrefabYaml = unityYamlObjects(foundationRuntimeRootPrefabText);
  foundationRuntimeRootPrefabFileId =
    unityGameObjectByName(foundationRuntimeRootPrefabYaml, "FoundationTestRuntimeRoot")?.fileId ?? null;
  if (foundationRuntimeRootPrefabFileId == null) {
    fail("Foundation 测试运行根 Prefab 缺少根对象：FoundationTestRuntimeRoot。");
  }
  for (const [editorClassIdentifier, label] of [
    ["GameCore::GameCore.SceneSystem", "正式技术场景切换系统"],
    ["GameCore::GameCore.TransitionSystem", "正式全屏转场表现系统"],
    ["Gameplay.Runtime::Gameplay.Scenarios.ScenarioDirector", "正式剧本导演"],
  ]) {
    if (unityMonoBehaviourByEditorClassIdentifier(
      foundationRuntimeRootPrefabYaml,
      "FoundationTestRuntimeRoot",
      editorClassIdentifier) == null) {
      fail(`Foundation 测试运行根缺少 ${label} 组件：${editorClassIdentifier}`);
    }
  }
  assertUnityMonoBehaviourScalarEquals(
    foundationRuntimeRootPrefabYaml,
    "FoundationTestRuntimeRoot",
    "GameCore::GameCore.TransitionSystem",
    "m_startWithBlackScreen",
    "0",
    "Foundation 测试运行根 TransitionSystem 初始黑屏状态");
  assertUnityMonoBehaviourScalarEquals(
    foundationRuntimeRootPrefabYaml,
    "FoundationTestRuntimeRoot",
    "GameCore::GameCore.TransitionSystem",
    "m_animator",
    "{fileID: 0}",
    "Foundation 测试运行根 TransitionSystem 无动画时不伪装 ScreenFader");
  assertUnityGameObjectExists(
    foundationRuntimeRootPrefabYaml,
    "剧本屏幕效果",
    "Foundation 测试运行根剧本屏幕效果对象");
  assertUnityMonoBehaviourScalarEquals(
    foundationRuntimeRootPrefabYaml,
    "剧本屏幕效果",
    "Unity.RenderPipelines.Core.Runtime::UnityEngine.Rendering.Volume",
    "m_IsGlobal",
    "1",
    "剧本屏幕效果全局 Volume");
  assertUnityMonoBehaviourScalarEquals(
    foundationRuntimeRootPrefabYaml,
    "剧本屏幕效果",
    "Unity.RenderPipelines.Core.Runtime::UnityEngine.Rendering.Volume",
    "priority",
    "100",
    "剧本屏幕效果 Volume 优先级");
  assertUnityMonoBehaviourFieldReferencesGuid(
    foundationRuntimeRootPrefabYaml,
    "剧本屏幕效果",
    "sharedProfile",
    "11400000",
    guidFromMetaPath(
      "Assets/Gameplay/Tests/牌桌/剧本屏幕效果配置.asset.meta",
      "剧本屏幕效果 Volume Profile"),
    2,
    "剧本屏幕效果 Volume Profile 作者源引用");
  assertUnityMonoBehaviourFieldReferences(
    foundationRuntimeRootPrefabYaml,
    "剧本屏幕效果",
    "m_volume",
    "剧本屏幕效果",
    "Unity.RenderPipelines.Core.Runtime::UnityEngine.Rendering.Volume",
    "ScenarioScreenEffectView Volume 字段");
  for (const [fieldName, expectedValue, label] of [
    ["m_pauseGrayscaleFadeSeconds", "0.3", "暂停灰阶淡入秒数"],
    ["m_pauseGrayscaleTarget", "1", "暂停灰阶目标"],
    ["m_dayVignetteFadeSeconds", "0.5", "日终暗角淡入秒数"],
    ["m_dayVignetteTarget", "0.45", "日终暗角目标"],
  ]) {
    assertUnityMonoBehaviourScalarEquals(
      foundationRuntimeRootPrefabYaml,
      "剧本屏幕效果",
      "Gameplay.Runtime::Gameplay.Scenarios.ScenarioScreenEffectView",
      fieldName,
      expectedValue,
      `ScenarioScreenEffectView ${label}`);
  }
}
const scenarioScreenEffectProfileText = readIfExists("Assets/Gameplay/Tests/牌桌/剧本屏幕效果配置.asset");
if (scenarioScreenEffectProfileText == null) {
  fail("缺少剧本屏幕效果 Profile 作者源：Assets/Gameplay/Tests/牌桌/剧本屏幕效果配置.asset。");
} else {
  const scenarioScreenEffectProfileYaml = unityYamlObjects(scenarioScreenEffectProfileText);
  const volumeProfile = uniqueUnityMonoBehaviourObjectByEditorClassIdentifier(
    scenarioScreenEffectProfileYaml,
    "Unity.RenderPipelines.Core.Runtime::UnityEngine.Rendering.VolumeProfile",
    "剧本屏幕效果 Profile");
  const vignette = uniqueUnityMonoBehaviourObjectByEditorClassIdentifier(
    scenarioScreenEffectProfileYaml,
    "Unity.RenderPipelines.Universal.Runtime::UnityEngine.Rendering.Universal.Vignette",
    "剧本屏幕效果 Profile Vignette");
  const colorAdjustments = uniqueUnityMonoBehaviourObjectByEditorClassIdentifier(
    scenarioScreenEffectProfileYaml,
    "Unity.RenderPipelines.Universal.Runtime::UnityEngine.Rendering.Universal.ColorAdjustments",
    "剧本屏幕效果 Profile ColorAdjustments");
  if (volumeProfile != null && vignette != null && colorAdjustments != null) {
    assertYamlScalarStringEquals(
      volumeProfile.text,
      "m_Name",
      "剧本屏幕效果配置",
      "剧本屏幕效果 Profile 根对象名称");
    const componentLines = yamlReferenceListLines(volumeProfile.text, "components") ?? [];
    const expectedComponentLines = [
      `- {fileID: ${vignette.fileId}}`,
      `- {fileID: ${colorAdjustments.fileId}}`,
    ].sort();
    const actualComponentLines = [...componentLines].sort();
    if (actualComponentLines.join("\n") !== expectedComponentLines.join("\n")) {
      fail(`剧本屏幕效果 Profile 的 components 未字段级引用 Vignette 与 ColorAdjustments：当前 ${actualComponentLines.join(" | ")}，应为 ${expectedComponentLines.join(" | ")}。`);
    }
    assertYamlScalarStringEquals(vignette.text, "m_Name", "Vignette", "剧本屏幕效果 Profile Vignette 名称");
    assertYamlScalarEquals(vignette.text, "active", "1", "剧本屏幕效果 Profile Vignette 激活状态");
    assertYamlNestedScalarEquals(vignette.text, "intensity", "m_OverrideState", "1", "剧本屏幕效果 Profile Vignette 强度 override");
    assertYamlNestedScalarEquals(vignette.text, "intensity", "m_Value", "0", "剧本屏幕效果 Profile Vignette 初始强度");
    assertYamlScalarStringEquals(colorAdjustments.text, "m_Name", "ColorAdjustments", "剧本屏幕效果 Profile ColorAdjustments 名称");
    assertYamlScalarEquals(colorAdjustments.text, "active", "1", "剧本屏幕效果 Profile ColorAdjustments 激活状态");
    assertYamlNestedScalarEquals(colorAdjustments.text, "saturation", "m_OverrideState", "1", "剧本屏幕效果 Profile ColorAdjustments 饱和度 override");
    assertYamlNestedScalarEquals(colorAdjustments.text, "saturation", "m_Value", "0", "剧本屏幕效果 Profile ColorAdjustments 初始饱和度");
  }
}
const stackCraftParitySceneText = readIfExists("Assets/Scenes/FoundationStackCraftParityTest.unity");
if (stackCraftParitySceneText == null) {
  fail("缺少 StackCraft 同态测试场景：Assets/Scenes/FoundationStackCraftParityTest.unity。");
} else {
  const stackCraftParitySceneYaml = unityYamlObjects(stackCraftParitySceneText);
  assertUnityGameObjectExists(stackCraftParitySceneYaml, "FoundationStackCraftParityTest", "StackCraft 同态测试场景根对象");
  assertUnityGameObjectExists(stackCraftParitySceneYaml, "Main Camera", "StackCraft 同态测试场景主相机");
  assertUnityComponentExists(stackCraftParitySceneYaml, "Main Camera", 20, "StackCraft 同态测试场景主相机");
  assertUnityComponentExists(stackCraftParitySceneYaml, "Main Camera", 81, "StackCraft 同态测试场景主相机音频监听");
  assertUnityMonoBehaviourNestedScalarEquals(
    stackCraftParitySceneYaml,
    "牌桌测试",
    "Gameplay.Foundation.TestSupport::Gameplay.Tests.Support.FoundationTestSceneHarness",
    "m_scenarioId",
    "m_value",
    "test.foundation.scenario.stackcraft-parity",
    "StackCraft 同态测试场景剧本 ID");
  assertUnityMonoBehaviourScalarEquals(
    stackCraftParitySceneYaml,
    "牌桌测试",
    "Gameplay.Foundation.TestSupport::Gameplay.Tests.Support.FoundationTestSceneHarness",
    "m_initialLayout",
    "1",
    "StackCraft 同态测试场景开局布局");
  assertUnityMonoBehaviourScalarEquals(
    stackCraftParitySceneYaml,
    "牌桌测试",
    "Gameplay.Foundation.TestSupport::Gameplay.Tests.Support.FoundationTestSceneHarness",
    "m_authoritativeRandomSeedOverride",
    "20260821",
    "StackCraft 同态测试场景固定随机根种子");
  for (const forbiddenHarnessField of [
    "m_tabletopView",
    "m_dragInput",
    "m_tabletopInteraction",
  ]) {
    assertUnityMonoBehaviourPropertyAbsent(
      stackCraftParitySceneYaml,
      "牌桌测试",
      "Gameplay.Foundation.TestSupport::Gameplay.Tests.Support.FoundationTestSceneHarness",
      forbiddenHarnessField,
      "StackCraft 同态测试场景 Harness 同对象依赖");
  }
  assertUnityMonoBehaviourFieldReferencesGuid(
    stackCraftParitySceneYaml,
    "FoundationStackCraftParityTest",
    "m_runtimeRootPrefab",
    foundationRuntimeRootPrefabFileId,
    foundationRuntimeRootPrefabGuid,
    3,
    "StackCraft 同态测试场景正式运行根");
  for (const forbiddenToken of [
    "m_value: test.foundation.scenario\n  m_initialLayout: 1",
    "m_initialLayout: 0",
    "m_runtimeRootPrefab: {fileID: 0}",
  ]) {
    if (stackCraftParitySceneText.includes(forbiddenToken)) {
      fail(`StackCraft 同态测试场景出现旧四卡开局或空运行根残留：${forbiddenToken}`);
    }
  }
}

const foundationTitleSceneText = readIfExists("Assets/Scenes/FoundationTitleTest.unity");
if (foundationTitleSceneText == null) {
  fail("缺少标题入口测试场景：Assets/Scenes/FoundationTitleTest.unity。");
} else {
  const foundationTitleSceneYaml = unityYamlObjects(foundationTitleSceneText);
  assertUnityGameObjectExists(foundationTitleSceneYaml, "Main Camera", "标题入口测试场景主相机");
  assertUnityComponentExists(foundationTitleSceneYaml, "Main Camera", 20, "标题入口测试场景主相机");
  assertUnityComponentExists(foundationTitleSceneYaml, "Main Camera", 81, "标题入口测试场景主相机音频监听");
}

const foundationTestSceneTextForRuntimeRoot = readIfExists("Assets/Scenes/FoundationTest.unity");
if (foundationTestSceneTextForRuntimeRoot == null) {
  fail("缺少统一地基测试场景：Assets/Scenes/FoundationTest.unity。");
} else {
  assertUnityMonoBehaviourFieldReferencesGuid(
    unityYamlObjects(foundationTestSceneTextForRuntimeRoot),
    "FoundationTest",
    "m_runtimeRootPrefab",
    foundationRuntimeRootPrefabFileId,
    foundationRuntimeRootPrefabGuid,
    3,
    "统一地基测试场景正式运行根");
}

if (foundationHarnessSource == null) {
  fail("缺少 FoundationTestSceneHarness，无法证明同态场景会生成 StackCraft Starter 卡包。");
} else {
  const stackCraftMainSceneYaml = stackCraftMainSceneText == null
    ? null
    : unityYamlObjects(stackCraftMainSceneText);
  if (stackCraftMainSceneText != null) {
    assertUnityScenePrefabInstanceSources(
      stackCraftMainSceneText,
      [
        "Assets/StackCraft/Prefabs/Boards/Board01.prefab",
        "Assets/StackCraft/Prefabs/UI/UIRoot.prefab",
        "Assets/StackCraft/Prefabs/Core/CameraController.prefab",
      ],
      "StackCraft Main 场景 Prefab 实例来源");
  }
  const stackCraftDefaultSpawnPosition = stackCraftMainSceneYaml == null
    ? null
    : unityComponentInlineObjectValues(
      stackCraftMainSceneYaml,
      "CardManager",
      114,
      "defaultSpawnPosition",
      "StackCraft Main CardManager.defaultSpawnPosition");
  const stackCraftCardManagerComponent = stackCraftMainSceneYaml == null
    ? null
    : unityComponentByClass(stackCraftMainSceneYaml, "CardManager", 114);
  const stackCraftDefaultSpawnRadius = stackCraftCardManagerComponent == null
    ? null
    : yamlScalarPropertyValue(
      stackCraftCardManagerComponent.text,
      "defaultSpawnRadius",
      "StackCraft Main CardManager.defaultSpawnRadius");
  const stackCraftDefaultStarterPackGuid = stackCraftCardManagerComponent == null
    ? null
    : assertUnitySingleReferenceListPath(
      stackCraftCardManagerComponent.text,
      "defaultSpawnCards",
      "Assets/StackCraft/Resources/Packs/00_Pack_Starter.asset",
      "StackCraft Main CardManager 默认出生卡牌列表");
  if (stackCraftCardManagerComponent != null) {
    assertStackCraftCardManagerCommonReferences(
      stackCraftCardManagerComponent.text,
      "StackCraft Main CardManager");
  }
  const stackCraftTradeManagerComponent = stackCraftMainSceneYaml == null
    ? null
    : unityComponentByClass(stackCraftMainSceneYaml, "TradeManager", 114);
  if (stackCraftTradeManagerComponent == null) {
    fail("StackCraft Main 场景缺少 TradeManager MonoBehaviour，无法字段级对账交易入口。");
  } else {
    assertStackCraftTradeManagerReferences(
      stackCraftTradeManagerComponent.text,
      "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset",
      [
        "Assets/StackCraft/Resources/Packs/01_Pack_Beginning.asset",
        "Assets/StackCraft/Resources/Packs/02_Pack_Revelations.asset",
        "Assets/StackCraft/Resources/Packs/03_Pack_Farmstead.asset",
        "Assets/StackCraft/Resources/Packs/04_Pack_HeartyMeals.asset",
        "Assets/StackCraft/Resources/Packs/05_Pack_Knowledge.asset",
        "Assets/StackCraft/Resources/Packs/06_Pack_Blacksmith.asset",
        "Assets/StackCraft/Resources/Packs/07_Pack_Adventure.asset",
        "Assets/StackCraft/Resources/Packs/08_Pack_Construction.asset",
      ],
      "StackCraft Main TradeManager");
  }
  const stackCraftEncounterManagerComponent = stackCraftMainSceneYaml == null
    ? null
    : unityComponentByClass(stackCraftMainSceneYaml, "EncounterManager", 114);
  if (stackCraftEncounterManagerComponent == null) {
    fail("StackCraft Main 场景缺少 EncounterManager MonoBehaviour，无法字段级对账遭遇列表。");
  } else {
    assertUnityReferenceListPaths(
      stackCraftEncounterManagerComponent.text,
      "allEncounters",
      [
        "Assets/StackCraft/Resources/Encounters/Encounter_Villager.asset",
        "Assets/StackCraft/Resources/Encounters/Encounter_Weekly_Slime.asset",
        "Assets/StackCraft/Resources/Encounters/Encounter_Weekly_Goblin.asset",
      ],
      "StackCraft Main EncounterManager 遭遇列表字段");
  }
  const stackCraftCombatManagerComponent = stackCraftMainSceneYaml == null
    ? null
    : unityComponentByClass(stackCraftMainSceneYaml, "CombatManager", 114);
  if (stackCraftCombatManagerComponent == null) {
    fail("StackCraft Main 场景缺少 CombatManager MonoBehaviour，无法字段级对账战斗表现 Prefab。");
  } else {
    assertStackCraftCombatManagerReferences(
      stackCraftCombatManagerComponent.text,
      "StackCraft Main CombatManager");
  }
  const stackCraftCraftingManagerComponent = stackCraftMainSceneYaml == null
    ? null
    : unityComponentByClass(stackCraftMainSceneYaml, "CraftingManager", 114);
  if (stackCraftCraftingManagerComponent == null) {
    fail("StackCraft Main 场景缺少 CraftingManager MonoBehaviour，无法字段级对账制作进度 UI。");
  } else {
    assertStackCraftCraftingManagerReferences(
      stackCraftCraftingManagerComponent.text,
      "StackCraft Main CraftingManager");
  }
  const stackCraftMainQuestManagerComponent = stackCraftMainSceneYaml == null
    ? null
    : unityComponentByClass(stackCraftMainSceneYaml, "QuestManager", 114);
  if (stackCraftMainQuestManagerComponent == null) {
    fail("StackCraft Main 场景缺少 QuestManager MonoBehaviour，无法字段级对账 Main 初始任务组。");
  } else {
    assertStackCraftQuestGroupReferences(
      stackCraftMainQuestManagerComponent.text,
      "Introduction",
      [
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_01.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_02.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_03.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_04.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_05.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_06.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_07.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_08.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_09.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_10.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_11.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_12.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_13.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_14.asset",
        "Assets/StackCraft/Resources/Quests/01_Introduction/introduction_15.asset",
      ],
      "StackCraft Main QuestManager");
  }
  assertCsharpBlockContainsOrdered(
    foundationHarnessSource,
    "public enum FoundationTestInitialLayout",
    [
      "StackDragAndActionTest = 0",
      "StackCraftStarterPack = 1",
    ],
    "FoundationTestSceneHarness StackCraft 同态布局枚举");
  assertCsharpFieldInitializerEquals(
    foundationHarnessSource,
    "TestStackCraftParityScenarioContentId",
    "\"test.foundation.scenario.stackcraft-parity\"",
    "FoundationTestSceneHarness StackCraft 同态剧本内容 ID");
  assertCsharpFieldInitializerEquals(
    foundationHarnessSource,
    "TestStackCraftParityQuestContentId",
    "\"test.foundation.quest.stackcraft-parity.open-starter-pack\"",
    "FoundationTestSceneHarness StackCraft 同态任务内容 ID");
  if (stackCraftDefaultStarterPackGuid != null) {
    assertCsharpFieldInitializerEquals(
      foundationHarnessSource,
      "TestCardPackContentId",
      "\"test.foundation.pack\"",
      "FoundationTestSceneHarness StackCraft Main 默认出生 Starter 卡包内容 ID");
  }
  assertCsharpBlockContainsOrdered(
    foundationHarnessSource,
    "private IEnumerator Start()",
    [
      "UniTask startScenarioTask = m_authoritativeRandomSeedOverride == 0u",
      "m_scenarioDirector.StartScenarioAsync(m_scenarioId)",
      "m_scenarioDirector.StartScenarioAsync(m_scenarioId, m_authoritativeRandomSeedOverride)",
      "yield return startScenarioTask.ToCoroutine();",
    ],
    "FoundationTestSceneHarness 同态测试固定随机种子入口");
  assertCsharpBlockContainsOrdered(
    foundationHarnessSource,
    "private void CreateInitialLayout",
    [
      "switch (m_initialLayout)",
      "case FoundationTestInitialLayout.StackCraftStarterPack:",
      "CreateStackCraftStarterPackLayout(starterPackId, openPackActionId);",
      "case FoundationTestInitialLayout.StackDragAndActionTest:",
      "CreateStackDragAndActionLayout(defaultCardId, defaultActionId);",
    ],
    "FoundationTestSceneHarness StackCraft 同态布局分派");
  assertCsharpBlockContainsOrdered(
    foundationHarnessSource,
    "private void CreateStackCraftStarterPackLayout",
    [
      "CardPackId.IsValid",
      "m_scenarioRun.DiscoverContent(openPackActionId);",
      "CardPackId = m_tabletop.CreateCard(",
      "starterPackId,",
      "ResolveStackCraftReferenceSpawnPosition()).Id;",
    ],
    "FoundationTestSceneHarness 按 StackCraft 参考采集坐标创建单张 Starter 卡包");
  assertCsharpFieldInitializerEquals(
    foundationHarnessSource,
    "StackCraftReferenceMetadataRelativePath",
    "\"Assets/Screenshots/StackCraftReference/stackcraft-main-reference-clean.json\"",
    "FoundationTestSceneHarness StackCraft 参考元数据路径");
  assertCsharpBlockContainsOrdered(
    foundationHarnessSource,
    "public static bool TryReadStackCraftReferenceStarterPackPosition",
    [
      "string metadataPath = Path.GetFullPath(",
      "StackCraftReferenceMetadataRelativePath",
      "ReferenceCaptureMetadata metadata = JsonUtility.FromJson<ReferenceCaptureMetadata>(metadataJson);",
      "metadata.packDisplayName",
      "metadata.usesLeft != 4",
      "Vector3 stackTarget = metadata.stackTargetPosition.ToVector3();",
      "position = new Vector2(stackTarget.x, stackTarget.z);",
    ],
    "FoundationTestSceneHarness 使用 StackCraft 参考采集元数据锁定 Starter 卡包坐标");
  if (foundationHarnessSource.includes("ResolveStackCraftDefaultSpawnPosition")) {
    fail("FoundationTestSceneHarness 仍保留旧的 StackCraft 默认出生点随机解析方法，必须读取参考采集元数据。");
  }
  if (foundationHarnessSource.includes("UnityEngine.Random.insideUnitSphere")) {
    fail("FoundationTestSceneHarness 仍用 UnityEngine.Random.insideUnitSphere 猜测 StackCraft 开局卡包位置。");
  }
}

const tabletopSourceForStackCraftSpawn = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/Tabletop.cs");
if (tabletopSourceForStackCraftSpawn == null) {
  fail("缺少正式牌桌聚合根源码：Assets/Scripts/Gameplay/Runtime/Tabletop/Tabletop.cs。");
} else if (
  tabletopSourceForStackCraftSpawn.includes("CreateCardAtAuthoritativeRandomSpawnPosition") ||
  tabletopSourceForStackCraftSpawn.includes("NextAuthoritativeFlattenedUnitSphereOffset")
) {
  fail("StackCraft 模板默认出生算法只能位于同态测试装配器；正式 Tabletop 不得保留模板专用随机出生入口。");
}

if (stackCraftIslandSceneText != null) {
  const stackCraftIslandSceneYaml = unityYamlObjects(stackCraftIslandSceneText);
  assertUnityScenePrefabInstanceSources(
    stackCraftIslandSceneText,
    [
      "Assets/StackCraft/Prefabs/Boards/Board02.prefab",
      "Assets/StackCraft/Prefabs/UI/UIRoot.prefab",
      "Assets/StackCraft/Prefabs/Core/CameraController.prefab",
    ],
    "StackCraft Island 场景 Prefab 实例来源");

  const stackCraftIslandCardManagerComponent = unityComponentByClass(stackCraftIslandSceneYaml, "CardManager", 114);
  if (stackCraftIslandCardManagerComponent == null) {
    fail("StackCraft Island 场景缺少 CardManager MonoBehaviour，无法字段级对账岛屿默认卡包。");
  } else {
    assertUnitySingleReferenceListPath(
      stackCraftIslandCardManagerComponent.text,
      "defaultSpawnCards",
      "Assets/StackCraft/Resources/Packs/10_Pack_Island.asset",
      "StackCraft Island CardManager 默认出生卡牌列表");
    assertStackCraftCardManagerCommonReferences(
      stackCraftIslandCardManagerComponent.text,
      "StackCraft Island CardManager");
  }

  const stackCraftIslandTradeManagerComponent = unityComponentByClass(stackCraftIslandSceneYaml, "TradeManager", 114);
  if (stackCraftIslandTradeManagerComponent == null) {
    fail("StackCraft Island 场景缺少 TradeManager MonoBehaviour，无法字段级对账岛屿交易入口。");
  } else {
    assertStackCraftTradeManagerReferences(
      stackCraftIslandTradeManagerComponent.text,
      "Assets/StackCraft/Resources/Cards/Currencies/Card_Coral.asset",
      ["Assets/StackCraft/Resources/Packs/11_Pack_Survival.asset"],
      "StackCraft Island TradeManager");
  }

  const stackCraftIslandEncounterManagerComponent = unityComponentByClass(stackCraftIslandSceneYaml, "EncounterManager", 114);
  if (stackCraftIslandEncounterManagerComponent == null) {
    fail("StackCraft Island 场景缺少 EncounterManager MonoBehaviour，无法字段级对账岛屿遭遇列表。");
  } else if (yamlMappingPropertyLine(stackCraftIslandEncounterManagerComponent.text, "allEncounters") !== "allEncounters: []") {
    fail("StackCraft Island EncounterManager.allEncounters 必须保持空列表，不能把 Main 场景遭遇误带入岛屿场景。");
  }

  const stackCraftIslandCombatManagerComponent = unityComponentByClass(stackCraftIslandSceneYaml, "CombatManager", 114);
  if (stackCraftIslandCombatManagerComponent == null) {
    fail("StackCraft Island 场景缺少 CombatManager MonoBehaviour，无法字段级对账岛屿战斗表现 Prefab。");
  } else {
    assertStackCraftCombatManagerReferences(
      stackCraftIslandCombatManagerComponent.text,
      "StackCraft Island CombatManager");
  }

  const stackCraftIslandCraftingManagerComponent = unityComponentByClass(stackCraftIslandSceneYaml, "CraftingManager", 114);
  if (stackCraftIslandCraftingManagerComponent == null) {
    fail("StackCraft Island 场景缺少 CraftingManager MonoBehaviour，无法字段级对账岛屿制作进度 UI。");
  } else {
    assertStackCraftCraftingManagerReferences(
      stackCraftIslandCraftingManagerComponent.text,
      "StackCraft Island CraftingManager");
  }

  const stackCraftIslandQuestManagerComponent = unityComponentByClass(stackCraftIslandSceneYaml, "QuestManager", 114);
  if (stackCraftIslandQuestManagerComponent == null) {
    fail("StackCraft Island 场景缺少 QuestManager MonoBehaviour，无法字段级对账岛屿任务组。");
  } else {
    assertStackCraftQuestGroupReferences(
      stackCraftIslandQuestManagerComponent.text,
      "The Basics",
      [
        "Assets/StackCraft/Resources/Quests/11_TheBasics/the_basics_01.asset",
        "Assets/StackCraft/Resources/Quests/11_TheBasics/the_basics_02.asset",
        "Assets/StackCraft/Resources/Quests/11_TheBasics/the_basics_03.asset",
      ],
      "StackCraft Island QuestManager");
  }
}

const openPackActionText = readIfExists("Assets/Gameplay/Tests/地基打开卡包行动.asset");
if (openPackActionText == null) {
  fail("缺少打开卡包行动作者源：Assets/Gameplay/Tests/地基打开卡包行动.asset。");
} else {
  const openPackActionLabel = "打开卡包行动 StackCraft PackInstance.OnClick 语义";
  assertYamlNestedScalarEquals(
    openPackActionText,
    "m_contentId",
    "m_value",
    "test.foundation.pack.open",
    `${openPackActionLabel} 内容 ID`);
  assertYamlScalarEquals(openPackActionText, "m_turnCost", "0", `${openPackActionLabel} 回合消耗`);
  assertYamlScalarEquals(openPackActionText, "m_canStartFromClick", "1", `${openPackActionLabel} 点击启动`);

  const packSlotBlock = yamlUnityListItemBlockByScalar(
    openPackActionText,
    "m_participationSlots",
    "m_key",
    "pack",
    `${openPackActionLabel} 参与槽`);
  if (packSlotBlock != null) {
    assertYamlBlockScalarEquals(packSlotBlock, "m_minimumParticipants", "1", `${openPackActionLabel} 卡包槽最小参与数`);
    assertYamlBlockScalarEquals(packSlotBlock, "m_maximumParticipants", "1", `${openPackActionLabel} 卡包槽最大参与数`);
    assertStringArrayEquals(
      yamlUnityListScalarValues(packSlotBlock, "m_allowedContentIds", "m_value", `${openPackActionLabel} 允许卡包内容`),
      ["test.foundation.pack", "test.foundation.pack.beginning"],
      `${openPackActionLabel} 允许卡包内容 ID`);
  }

  const openPackResultIntent = unitySerializeReferenceBlockByType(
    openPackActionText,
    "OpenCardPackResultIntent",
    "Gameplay.Tabletop.Actions",
    "Gameplay.Runtime",
    `${openPackActionLabel} 结果意图`);
  if (openPackResultIntent != null) {
    assertStringArrayEquals(
      yamlUnityListScalarValues(openPackActionText, "m_resultIntents", "rid", `${openPackActionLabel} 结果意图列表`),
      [openPackResultIntent.rid],
      `${openPackActionLabel} 结果意图引用`);
    assertYamlNestedScalarEquals(
      openPackResultIntent.text,
      "data",
      "m_packSlotKey",
      "pack",
      `${openPackActionLabel} 打开卡包结果`);
  }
}

for (const [assetPath, expectedGroupName, label] of [
  ["Assets/Gameplay/Tests/地基测试任务.asset", "基础", "任务日志分组"],
  ["Assets/Gameplay/Tests/地基测试行动.asset", "建造", "行动日志分组"],
  ["Assets/Gameplay/Tests/地基测试填槽行动.asset", "杂项", "行动日志分组"],
  ["Assets/Gameplay/Tests/地基配方种植浆果行动.asset", "畜牧", "配方日志分组"],
  ["Assets/Gameplay/Tests/地基配方建造房屋行动.asset", "建造", "配方日志分组"],
  ["Assets/Gameplay/Tests/地基配方孕育行动.asset", "畜牧", "配方日志分组"],
  ["Assets/Gameplay/Tests/地基配方制作木材行动.asset", "加工", "配方日志分组"],
  ["Assets/Gameplay/Tests/地基配方制作木棍行动.asset", "加工", "配方日志分组"],
]) {
  const text = readIfExists(assetPath);
  if (text == null) {
    fail(`缺少 ${label} 作者源：${assetPath}。`);
    continue;
  }

  assertYamlScalarStringEquals(
    text,
    "m_journalGroupName",
    expectedGroupName,
    `${assetPath} StackCraft HUD ${label}`);
}

const testContentAssets = walk("Assets/Gameplay/Tests").filter((file) => file.endsWith(".asset"));
for (const file of testContentAssets) {
  const text = tryReadText(file);
  if (text == null || !text.includes("m_cardSurface:")) continue;

  const surfaceBlock = yamlFieldBlock(text, "m_cardSurface", `${rel(file)} 卡牌表面引用`);
  if (surfaceBlock == null) continue;

  const addressValue = yamlBlockScalarValue(surfaceBlock, "Address");
  if (addressValue == null) {
    fail(`${rel(file)} 有 m_cardSurface 字段块，但没有可读 Address。`);
    continue;
  }

  const address = unquoteUnityString(addressValue);
  if (!expectedCardSurfaceAddresses.includes(address)) {
    fail(`${rel(file)} 的卡牌表面地址 ${address || "(空)"} 不在 StackCraft 分族表面地址白名单内。`);
  }
}

const cardViewPrefabText = readIfExists("Assets/Art/Prefabs/牌桌/卡牌视图.prefab");
const stackCraftCharacterCardPrefabText = readIfExists("Assets/StackCraft/Prefabs/Cards/Card_Character.prefab");
const stackCraftCardInstanceSource = readIfExists("Assets/StackCraft/Scripts/Card/CardInstance.cs");
const stackCraftConsumableCardPrefabText = readIfExists("Assets/StackCraft/Prefabs/Cards/Card_Consumable.prefab");
const stackCraftCardBuyerPrefabText = readIfExists("Assets/StackCraft/Prefabs/Trading/CardBuyer.prefab");
const stackCraftPackVendorPrefabText = readIfExists("Assets/StackCraft/Prefabs/Trading/PackVendor.prefab");
const stackCraftPackInstancePrefabText = readIfExists("Assets/StackCraft/Prefabs/PackInstance.prefab");
const stackCraftUiRootPrefabText = readIfExists("Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
const stackCraftHitUiPrefabText = readIfExists("Assets/StackCraft/Prefabs/UI/HitUI.prefab");
const stackCraftHitUiSource = readIfExists("Assets/StackCraft/Scripts/Combat/UI/HitUI.cs");
const stackCraftProgressUiPrefabText = readIfExists("Assets/StackCraft/Prefabs/UI/ProgressUI.prefab");
const stackCraftProjectileArrowPrefabText = readIfExists("Assets/StackCraft/Prefabs/UI/Projectile_Arrow.prefab");
const stackCraftProjectileMagicPrefabText = readIfExists("Assets/StackCraft/Prefabs/UI/Projectile_Magic.prefab");
const stackCraftPuffParticlePrefabText = readIfExists("Assets/StackCraft/Prefabs/VFX/PuffParticle.prefab");
const tabletopCardViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopCardView.cs");
const tabletopHitResultViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopHitResultView.cs");
const cardDefinitionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/CardDefinition.cs");
const cardPackDefinitionSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Content/CardPackDefinition.cs");
const foundationTestSceneMenuSource = readIfExists("Assets/Tests/Support/Editor/FoundationTestSceneMenu.cs");
const foundationStackCraftParityPlayModeTestSource = readIfExists("Assets/Tests/PlayMode/FoundationStackCraftParityPlayModeTests.cs");

if (foundationTestSceneMenuSource == null) {
  fail("缺少测试场景生成器源码，无法证明 StackCraft 表面 Prefab 的生成源不会回流错误字体。");
} else {
  assertCsharpFieldInitializerEquals(
    foundationTestSceneMenuSource,
    "StackCraftSurfaceFontPath",
    '"Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"',
    "测试场景生成器 StackCraft 表面字体路径常量");
  assertCsharpFieldInitializerEquals(
    foundationTestSceneMenuSource,
    "PackMeshPath",
    'GameplayModelFolder + "/卡包.fbx"',
    "测试场景生成器 StackCraft 卡包网格路径常量");
  assertCsharpFieldInitializerEquals(
    foundationTestSceneMenuSource,
    "EquipmentPanelMeshPath",
    'GameplayModelFolder + "/装备面板.fbx"',
    "测试场景生成器 StackCraft 装备面板网格路径常量");
  assertCsharpFieldInitializerEquals(
    foundationTestSceneMenuSource,
    "EquipmentPanelMaterialPath",
    'GameplayMaterialFolder + "/装备面板.mat"',
    "测试场景生成器 StackCraft 装备面板材质路径常量");
  assertCsharpBlockContainsOrdered(
    foundationTestSceneMenuSource,
    "private static TMP_FontAsset LoadStackCraftSurfaceFont()",
    [
      "AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(",
      "StackCraftSurfaceFontPath",
      "StackCraftSurfaceFontPath",
    ],
    "测试场景生成器 StackCraft LiberationSans SDF 字体加载入口");
  assertCsharpBlockContainsOrdered(
    foundationTestSceneMenuSource,
    "private static void EnsureTabletopCardViewPrefab(",
    [
      "Mesh packMesh = LoadRequiredMesh(PackMeshPath, \"StackCraft Pack.fbx 自有副本\")",
      "Mesh equipmentPanelMesh = LoadRequiredMesh(",
      "EquipmentPanelMeshPath",
      "Material equipmentPanelMaterial = LoadRequiredMaterial(",
      "EquipmentPanelMaterialPath",
      "TMP_FontAsset cardFont = LoadStackCraftSurfaceFont();",
      "EnsureStackCraftSurfaceFontFallback(cardFont, EnsureTestPanelFont());",
      "MeshFilter equipmentPanelMeshFilter = equipmentPanelObject.AddComponent<MeshFilter>();",
      "equipmentPanelMeshFilter.sharedMesh = equipmentPanelMesh;",
      "equipmentPanelRenderer.sharedMaterial = equipmentPanelMaterial;",
      "BoxCollider equipmentPanelCollider = equipmentPanelObject.AddComponent<BoxCollider>();",
      "serializedView.FindProperty(\"m_surfaceMeshFilter\").objectReferenceValue = meshFilter",
      "serializedView.FindProperty(\"m_defaultSurfaceMesh\").objectReferenceValue = cardMesh",
      "serializedView.FindProperty(\"m_packSurfaceMesh\").objectReferenceValue = packMesh",
      "serializedView.FindProperty(\"m_highlightMeshFilter\").objectReferenceValue = highlightMeshFilter",
    ],
    "测试场景生成器 StackCraft 卡牌视图 Prefab 表面资源生成链");
  assertCsharpBlockContainsOrdered(
    foundationTestSceneMenuSource,
    "private static void EnsureTabletopHitResultViewPrefab(",
    [
      "TMP_FontAsset fontAsset = LoadStackCraftSurfaceFont();",
      "EnsureStackCraftSurfaceFontFallback(fontAsset, EnsureTestPanelFont());",
      "ApplyStackCraftTextParameters(",
    ],
    "测试场景生成器命中结果中文字体与 StackCraft 文字效果生成链");
}

if (foundationStackCraftParityPlayModeTestSource == null) {
  fail("缺少 StackCraft 同态截图 PlayMode 用例，无法证明截图前会等待 PackInstance 表面投影完成。");
} else {
  assertCsharpBlockContainsOrdered(
    foundationStackCraftParityPlayModeTestSource,
    "public IEnumerator StackCraftParityScene_CapturesStarterPackInitialFrame()",
    [
      "FoundationTestSceneHarness.TryReadStackCraftReferenceStarterPackPosition(",
      "out Vector2 referencePackPosition",
      "TabletopCardStack packStack = controller.Cards.GetStackContaining(controller.CardPackId)",
      "Assert.That(packStack.Position.x, Is.EqualTo(referencePackPosition.x).Within(0.001f))",
      "Assert.That(packStack.Position.y, Is.EqualTo(referencePackPosition.y).Within(0.001f))",
      "TabletopCardView packView = WaitForSingleStarterPackView(controller.CardPackId)",
      "Assert.That(packView.DisplayedArtwork.name, Is.EqualTo(\"Starter\"))",
      "yield return WaitUntilStarterPackSurface(packView)",
      "yield return CaptureParityScreenshot(ScreenshotFileName)",
    ],
    "StackCraft 同态截图用例截图前置条件和参考坐标断言");
  assertCsharpBlockContainsOrdered(
    foundationStackCraftParityPlayModeTestSource,
    "private static IEnumerator WaitUntilStarterPackSurface(TabletopCardView view)",
    [
      "Vector2 expectedSize = new Vector2(0.9f, 1.3000002f)",
      "view.DisplayedSurfaceMaterial.name != \"卡牌表面_卡包\"",
      "Vector2.Distance(view.AppliedCardSize, expectedSize) > 0.001f",
      "BoxCollider collider = view.GetComponent<BoxCollider>()",
      "collider.size.x",
      "collider.size.z",
      "collider.size.y",
    ],
    "StackCraft 同态截图 PackInstance 表面等待条件");
}

if (tabletopCardViewSource == null) {
 fail("缺少牌桌卡牌视图源码，无法证明受击闪白 / 摇晃由正式视图承接。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "public sealed class TabletopCardView",
    [
      "private MeshFilter m_surfaceMeshFilter",
      "private Mesh m_defaultSurfaceMesh",
      "private Mesh m_packSurfaceMesh",
      "private MeshFilter m_highlightMeshFilter",
    ],
    "牌桌卡牌视图 StackCraft 表面资源字段");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "internal float DistanceToVisibleFootprint",
    [
      "TabletopCoordinateSpace.ToTablePosition(transform.localPosition)",
      "m_appliedCardSize * 0.5f",
      "Math.Abs(tablePosition.x - center.x) - halfSize.x",
      "Mathf.Sqrt(dx * dx + dy * dy)",
    ],
    "牌桌卡牌视图 StackCraft AttachRadius 可见卡面距离算法");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "private void ApplySurfaceMeshForContent(",
    [
      "Mesh targetMesh = m_defaultSurfaceMesh;",
      "Vector3 targetColliderSize = DefaultCardColliderSize;",
      "contentAsset is CardPackDefinition",
      "targetMesh = m_packSurfaceMesh;",
      "targetColliderSize = PackInstanceColliderSize;",
      "m_surfaceMeshFilter.sharedMesh = targetMesh;",
      "m_highlightMeshFilter.sharedMesh = targetMesh;",
      "ApplyUnscaledColliderFootprint(targetColliderSize)",
    ],
    "牌桌卡牌视图 StackCraft Card / Pack 独立表面网格切换");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "public void ApplyPackVendorSurface(",
    [
      "ApplyPackVendorTextLayout(",
      "m_titleLabel.text = offeredPackName;",
      "ApplyPackVendorTextLayout(",
      "\"价格：\" + remainingPrice",
      "ApplyPackVendorTextLayout(",
      "VerticalAlignmentOptions.Top",
      "BuildPackVendorTrackerText(progress)",
    ],
    "牌桌卡牌视图 StackCraft PackVendor 表面文字布局");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "private static string BuildPackVendorTrackerText(",
    [
      "\"<color=#FFD700>已完成</color>\"",
      "\"已发现：\\n\" + progress.DiscoveredCount + \"/\" + progress.TotalCount",
    ],
    "牌桌卡牌视图 StackCraft PackVendor 发现进度文本");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "private void ApplyCardPackInstanceSurface()",
    [
      "PackInstanceTitleLocalPosition",
      "TextAlignmentOptions.Center",
      "ClearSurfaceText(m_priceLabel)",
      "ClearSurfaceText(m_nutritionLabel)",
    ],
    "牌桌卡牌视图 StackCraft 卡包实例表面布局");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "public void ApplySize(",
    [
      "GetUnscaledViewFootprint(cardCollider)",
      "base.transform.localScale = new Vector3(",
      "cardSize.x / unscaledViewFootprint.x",
      "cardSize.y / unscaledViewFootprint.y",
      "cardCollider.size = new Vector3(unscaledViewFootprint.x, 0f, unscaledViewFootprint.y)",
    ],
    "牌桌卡牌视图 StackCraft 未缩放足迹尺寸投影");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "private Vector2 GetUnscaledViewFootprint(",
    [
      "m_surfaceRenderer.TryGetComponent(out MeshFilter surfaceMeshFilter)",
      "surfaceMeshFilter.sharedMesh.bounds.size",
      "new Vector2(meshSize.x, meshSize.z)",
    ],
    "牌桌卡牌视图 StackCraft 表面网格足迹回读");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "public void ApplyPose(",
    [
      "DOLocalMove(pose.LocalPosition, durationSeconds)",
      "SetEase(Ease.OutQuad)",
      "SetUpdate(true)",
      "moveTween.OnUpdate(Physics.SyncTransforms);",
    ],
    "牌桌卡牌视图 StackCraft 移动补间与暂停物理同步");
  if (stackCraftMoveEase != null && stackCraftMoveEase !== "6") {
    fail(`StackCraft Default_Card_Settings.moveEase 当前为 ${stackCraftMoveEase}，但 TabletopCardView 仍按 Ease.OutQuad 承接；需要先重新裁决移动缓动映射。`);
  }
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "public void ApplyDragPose(",
    [
      "base.transform.localPosition = pose.LocalPosition;",
      "SyncPhysicsTransformsWhenPaused();",
      "m_dragTargetLocalPosition = pose.LocalPosition;",
      "m_dragFollowSharpness = followSharpness;",
      "m_isFollowingDragTarget = true;",
    ],
    "牌桌卡牌视图 StackCraft 拖拽跟随投影");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "private void Update()",
    [
      "if (m_isFollowingDragTarget)",
      "float interpolation = 1f - Mathf.Exp((0f - m_dragFollowSharpness) * Time.unscaledDeltaTime);",
      "base.transform.localPosition = Vector3.Lerp(base.transform.localPosition, m_dragTargetLocalPosition, interpolation);",
      "if ((base.transform.localPosition - m_dragTargetLocalPosition).sqrMagnitude <= 1E-06f)",
      "base.transform.localPosition = m_dragTargetLocalPosition;",
      "m_isFollowingDragTarget = false;",
    ],
    "牌桌卡牌视图 StackCraft swaySharpness 未缩放时间拖拽跟随方法");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "public void PlayHurtFeedback()",
    [
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
    ],
    "牌桌卡牌视图 StackCraft 受击闪白与摇晃反馈");
  assertCsharpBlockContainsOrdered(
    tabletopCardViewSource,
    "private void ApplySortingOrder(",
    [
      "m_characterStatusRenderers ??=",
      "m_characterStatusRoot.GetComponentsInChildren<Renderer>(includeInactive: true)",
      "m_characterStatusRenderers[i].sortingOrder = sortingOrder + 1",
    ],
    "牌桌卡牌视图 StackCraft 角色状态子表面排序绑定");

  const stackCraftPackVendorSurfaceYaml = stackCraftPackVendorPrefabText == null
    ? null
    : unityYamlObjects(stackCraftPackVendorPrefabText);
  if (stackCraftPackVendorSurfaceYaml == null) {
    fail("缺少 StackCraft PackVendor Prefab，无法用来源对象参数对账商贩运行时表面：Assets/StackCraft/Prefabs/Trading/PackVendor.prefab");
  } else {
    for (const [sourceObjectName, constantPrefix, label] of [
      ["Title", "PackVendorTitle", "卡包商贩标题"],
      ["Tracker", "PackVendorTracker", "卡包商贩发现进度"],
      ["Price", "PackVendorPrice", "卡包商贩价格"],
    ]) {
      assertCsharpConstantFromUnityInlineProperty(
        stackCraftPackVendorSurfaceYaml,
        sourceObjectName,
        224,
        "m_LocalPosition",
        tabletopCardViewSource,
        `${constantPrefix}LocalPosition`,
        "Vector3",
        ["x", "y", "z"],
        label);
      assertCsharpConstantFromUnityInlineProperty(
        stackCraftPackVendorSurfaceYaml,
        sourceObjectName,
        224,
        "m_SizeDelta",
        tabletopCardViewSource,
        `${constantPrefix}Size`,
        "Vector2",
        ["x", "y"],
        label);
      assertCsharpConstantFromUnityScalarProperty(
        stackCraftPackVendorSurfaceYaml,
        sourceObjectName,
        114,
        "m_fontSize",
        tabletopCardViewSource,
        `${constantPrefix}FontSize`,
        label);
      assertCsharpConstantFromUnityScalarProperty(
        stackCraftPackVendorSurfaceYaml,
        sourceObjectName,
        114,
        "m_fontSizeMin",
        tabletopCardViewSource,
        "StackCraftVendorTextFontSizeMin",
        `${label}最小字号`);
      assertCsharpConstantFromUnityScalarProperty(
        stackCraftPackVendorSurfaceYaml,
        sourceObjectName,
        114,
        "m_fontSizeMax",
        tabletopCardViewSource,
        "StackCraftVendorTextFontSizeMax",
        `${label}最大字号`);
    }
  }

  if (stackCraftCharacterCardPrefabText == null) {
    fail("缺少 StackCraft 角色卡 Prefab，无法用来源对象参数对账默认卡牌 Collider：Assets/StackCraft/Prefabs/Cards/Card_Character.prefab");
  } else {
    assertCsharpConstantFromUnityInlineProperty(
      unityYamlObjects(stackCraftCharacterCardPrefabText),
      "Card_Character",
      65,
      "m_Size",
      tabletopCardViewSource,
      "DefaultCardColliderSize",
      "Vector3",
      ["x", "y", "z"],
      "默认卡牌根碰撞盒");
  }

  const stackCraftPackInstanceSurfaceYaml = stackCraftPackInstancePrefabText == null
    ? null
    : unityYamlObjects(stackCraftPackInstancePrefabText);
  if (stackCraftPackInstanceSurfaceYaml == null) {
    fail("缺少 StackCraft PackInstance Prefab，无法用来源对象参数对账卡包运行时表面：Assets/StackCraft/Prefabs/PackInstance.prefab");
  } else {
    assertCsharpConstantFromUnityInlineProperty(
      stackCraftPackInstanceSurfaceYaml,
      "Title",
      224,
      "m_LocalPosition",
      tabletopCardViewSource,
      "PackInstanceTitleLocalPosition",
      "Vector3",
      ["x", "y", "z"],
      "卡包标题");
    assertCsharpConstantFromUnityInlineProperty(
      stackCraftPackInstanceSurfaceYaml,
      "Title",
      224,
      "m_SizeDelta",
      tabletopCardViewSource,
      "PackInstanceTitleSize",
      "Vector2",
      ["x", "y"],
      "卡包标题");
    assertCsharpConstantFromUnityInlineProperty(
      stackCraftPackInstanceSurfaceYaml,
      "Title",
      114,
      "m_margin",
      tabletopCardViewSource,
      "PackInstanceTitleMargin",
      "Vector4",
      ["x", "y", "z", "w"],
      "卡包标题");
    assertCsharpConstantFromUnityInlineProperty(
      stackCraftPackInstanceSurfaceYaml,
      "Title",
      114,
      "m_fontColor",
      tabletopCardViewSource,
      "PackInstanceTitleColor",
      "Color",
      ["r", "g", "b", "a"],
      "卡包标题");
    assertCsharpConstantFromUnityScalarProperty(
      stackCraftPackInstanceSurfaceYaml,
      "Title",
      114,
      "m_fontSize",
      tabletopCardViewSource,
      "PackInstanceTitleFontSize",
      "卡包标题");
    assertCsharpConstantFromUnityScalarProperty(
      stackCraftPackInstanceSurfaceYaml,
      "Title",
      114,
      "m_fontSizeMin",
      tabletopCardViewSource,
      "PackInstanceTitleMinFontSize",
      "卡包标题");
    assertCsharpConstantFromUnityScalarProperty(
      stackCraftPackInstanceSurfaceYaml,
      "Title",
      114,
      "m_fontSizeMax",
      tabletopCardViewSource,
      "PackInstanceTitleMaxFontSize",
      "卡包标题");
    assertCsharpConstantFromUnityInlineProperty(
      stackCraftPackInstanceSurfaceYaml,
      "PackInstance",
      65,
      "m_Size",
      tabletopCardViewSource,
      "PackInstanceColliderSize",
      "Vector3",
      ["x", "y", "z"],
      "卡包根碰撞盒");
  }

  const stackCraftCardBuyerSurfaceYaml = stackCraftCardBuyerPrefabText == null
    ? null
    : unityYamlObjects(stackCraftCardBuyerPrefabText);
  if (stackCraftCardBuyerSurfaceYaml == null) {
    fail("缺少 StackCraft CardBuyer Prefab，无法用来源对象参数对账收购点运行时表面：Assets/StackCraft/Prefabs/Trading/CardBuyer.prefab");
  }
  assertSharedCsharpConstantFromUnityInlineProperties(
    [
      {
        sourceParsedYaml: stackCraftPackVendorSurfaceYaml,
        sourceObjectName: "Title",
        sourceClassId: 224,
        propertyName: "m_AnchoredPosition",
        sourcePrefabLabel: "PackVendor",
      },
      {
        sourceParsedYaml: stackCraftPackVendorSurfaceYaml,
        sourceObjectName: "Tracker",
        sourceClassId: 224,
        propertyName: "m_AnchoredPosition",
        sourcePrefabLabel: "PackVendor",
      },
      {
        sourceParsedYaml: stackCraftPackVendorSurfaceYaml,
        sourceObjectName: "Price",
        sourceClassId: 224,
        propertyName: "m_AnchoredPosition",
        sourcePrefabLabel: "PackVendor",
      },
      {
        sourceParsedYaml: stackCraftPackInstanceSurfaceYaml,
        sourceObjectName: "Title",
        sourceClassId: 224,
        propertyName: "m_AnchoredPosition",
        sourcePrefabLabel: "PackInstance",
      },
      {
        sourceParsedYaml: stackCraftCardBuyerSurfaceYaml,
        sourceObjectName: "Title",
        sourceClassId: 224,
        propertyName: "m_AnchoredPosition",
        sourcePrefabLabel: "CardBuyer",
      },
    ],
    tabletopCardViewSource,
    "StackCraftTextAnchoredPosition",
    "Vector2",
    ["x", "y"],
    "StackCraft 卡面文本锚点");

  if (foundationTestSceneMenuSource != null) {
    const stackCraftHitUiTextStyleYaml = stackCraftHitUiPrefabText == null
      ? null
      : unityYamlObjects(stackCraftHitUiPrefabText);
    assertSharedCsharpIntConstantFromUnityScalarProperties(
      [
        {
          sourceParsedYaml: stackCraftHitUiTextStyleYaml,
          sourceObjectName: "DamageLabel",
          sourceClassId: 114,
          propertyName: "m_TextStyleHashCode",
          sourcePrefabLabel: "HitUI",
        },
        {
          sourceParsedYaml: stackCraftPackVendorSurfaceYaml,
          sourceObjectName: "Title",
          sourceClassId: 114,
          propertyName: "m_TextStyleHashCode",
          sourcePrefabLabel: "PackVendor",
        },
        {
          sourceParsedYaml: stackCraftPackVendorSurfaceYaml,
          sourceObjectName: "Tracker",
          sourceClassId: 114,
          propertyName: "m_TextStyleHashCode",
          sourcePrefabLabel: "PackVendor",
        },
        {
          sourceParsedYaml: stackCraftPackVendorSurfaceYaml,
          sourceObjectName: "Price",
          sourceClassId: 114,
          propertyName: "m_TextStyleHashCode",
          sourcePrefabLabel: "PackVendor",
        },
        {
          sourceParsedYaml: stackCraftPackInstanceSurfaceYaml,
          sourceObjectName: "Title",
          sourceClassId: 114,
          propertyName: "m_TextStyleHashCode",
          sourcePrefabLabel: "PackInstance",
        },
        {
          sourceParsedYaml: stackCraftCardBuyerSurfaceYaml,
          sourceObjectName: "Title",
          sourceClassId: 114,
          propertyName: "m_TextStyleHashCode",
          sourcePrefabLabel: "CardBuyer",
        },
      ],
      foundationTestSceneMenuSource,
      "StackCraftConvertedTextStyleHashCode",
      "StackCraft TMP 文本样式 hash");
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
    "base.transform.localScale = Vector3.one;",
    "cardCollider.size = new Vector3(cardSize.x, 0f, cardSize.y)",
  ]) {
    if (tabletopCardViewSource.includes(obsoleteToken)) {
      fail(`牌桌卡牌视图仍保留手写受击动画近似算法，应使用 DOTween 参数闭包：${obsoleteToken}`);
    }
  }
}

if (cardDefinitionSource == null) {
  fail("CardDefinition 缺少卡牌可见尺寸正式入口，卡包不能按 StackCraft PackInstance 独立尺寸投影。");
} else {
  assertCsharpBlockContainsOrdered(
    cardDefinitionSource,
    "public class CardDefinition",
    [
      "private bool m_overrideViewSize;",
      "private Vector2 m_viewSize = Vector2.one;",
    ],
    "CardDefinition 牌桌可见尺寸作者源字段");
  assertCsharpBlockContainsOrdered(
    cardDefinitionSource,
    "public class CardDefinition",
    [
      "private float m_automaticMovementIntervalSeconds;",
      "private float m_automaticMovementRadius;",
      "private int m_automaticMovementMaxAttempts;",
      "private int m_automaticMovementRetentionCapacity;",
    ],
    "CardDefinition StackCraft CardAI 自动移动作者源字段");
  assertCsharpBlockContainsOrdered(
    cardDefinitionSource,
    "public Vector2 GetViewSize(Vector2 defaultCardSize)",
    ["return m_overrideViewSize ? m_viewSize : defaultCardSize;"],
    "CardDefinition 牌桌可见尺寸唯一读取入口");
  assertCsharpDeclarationAndBlockContainsOrdered(
    cardDefinitionSource,
    "public class CardDefinition",
    [
      "public float AutomaticMovementIntervalSeconds => m_automaticMovementIntervalSeconds;",
      "public float AutomaticMovementRadius => m_automaticMovementRadius;",
      "public int AutomaticMovementMaxAttempts => m_automaticMovementMaxAttempts;",
      "public int AutomaticMovementRetentionCapacity => m_automaticMovementRetentionCapacity;",
      "public bool HasAutomaticMovement =>",
    ],
    "CardDefinition StackCraft 自动移动字段只读出口");
  assertCsharpBlockContainsOrdered(
    cardDefinitionSource,
    "protected override void ValidateContent",
    [
      "CARD_AUTOMATIC_MOVEMENT_RETENTION_CAPACITY_INVALID",
      "if (HasAutomaticMovement)",
      "CARD_AUTOMATIC_MOVEMENT_INTERVAL_INVALID",
      "CARD_AUTOMATIC_MOVEMENT_RADIUS_INVALID",
      "CARD_AUTOMATIC_MOVEMENT_ATTEMPTS_INVALID",
    ],
    "CardDefinition StackCraft 自动移动作者源校验");
  assertCsharpBlockContainsOrdered(
    cardDefinitionSource,
    "protected override void ValidateContent",
    ["CARD_VIEW_SIZE_INVALID"],
    "CardDefinition 牌桌可见尺寸校验码");
  if (cardDefinitionSource.includes("public virtual Vector2 GetViewSize")) {
    fail("CardDefinition.GetViewSize 仍允许派生类覆盖，会重新制造卡牌尺寸第二真相。");
  }
}

if (cardPackDefinitionSource == null) {
  fail("缺少 CardPackDefinition 源码，无法证明卡包独立表面尺寸由卡包定义承接。");
} else {
  assertCsharpBlockContainsOrdered(
    cardPackDefinitionSource,
    "public class CardPackDefinition",
    [
      "public override bool CountsTowardCardLimit => false;",
      "protected override bool HasDerivedCardLimitCounting => true;",
    ],
    "CardPackDefinition 像 StackCraft PackInstance 一样强制不计入卡牌上限");
  assertCsharpBlockContainsOrdered(
    cardPackDefinitionSource,
    "protected override void ValidateContent",
    [
      "if (AuthoringCountsTowardCardLimit)",
      "CARD_PACK_COUNTS_TOWARD_LIMIT",
    ],
    "CardPackDefinition 校验历史作者源隐藏字段不能重新计入容量");
  for (const obsoleteToken of [
    "public override Vector2 GetViewSize(Vector2 defaultCardSize)",
    "return PackInstanceViewSize",
    "PackInstanceViewSize",
  ]) {
    if (cardPackDefinitionSource.includes(obsoleteToken)) {
      fail(`CardPackDefinition 仍保留派生类可见尺寸第二真相：${obsoleteToken}`);
    }
  }
}

if (tabletopViewSource == null) {
  fail("TabletopView 仍按普通牌桌几何统一应用卡牌尺寸，卡包无法复刻 PackInstance 独立 Collider 尺寸。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void RequestView(",
    [
      "component.Bind(value.TabletopCard, value.Definition);",
      "component.ApplySize(value.Definition.GetViewSize(m_tabletop.PlacementRules.Geometry.CardSize))",
      "ApplyCurrentPose(value);",
      "EnsureCardSurface(value.Definition);",
    ],
    "TabletopView 卡牌视图实例化时应用作者源可见尺寸");
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
const stackCraftPuffParticlePrefabGuid = unityGuid(readIfExists("Assets/StackCraft/Prefabs/VFX/PuffParticle.prefab.meta"));
const stackCraftCardOutlineShaderGuid = unityGuid(readIfExists("Assets/StackCraft/Shaders/CardOutline.shadergraph.meta"));
if (stackCraftDefaultCardSettingsText == null) {
  fail("缺少 StackCraft Default_Card_Settings.asset，无法证明 puffParticle / outlineMaterial 引用来源。");
} else {
  assertYamlReferenceLine(
    stackCraftDefaultCardSettingsText,
    "puffParticle",
    "7066159731596260550",
    stackCraftPuffParticlePrefabGuid,
    "3",
    "Assets/StackCraft/Settings/Default_Card_Settings.asset",
    "StackCraft 默认卡牌烟雾粒子引用");
  assertYamlReferenceLine(
    stackCraftDefaultCardSettingsText,
    "outlineMaterial",
    "-876546973899608171",
    stackCraftCardOutlineShaderGuid,
    "3",
    "Assets/StackCraft/Settings/Default_Card_Settings.asset",
    "StackCraft 默认候选高亮外轮廓引用");
}

assertSameFileHash(
  "Assets/StackCraft/Shaders/Card.shadergraph",
  "Assets/Art/Shaders/卡牌表面.shadergraph",
  "StackCraft Card 卡牌表面 shadergraph");
assertScriptedImporterMetaMatches(
  "Assets/StackCraft/Shaders/Card.shadergraph",
  "Assets/Art/Shaders/卡牌表面.shadergraph",
  "StackCraft Card 卡牌表面 shadergraph");

assertSameFileHash(
  "Assets/StackCraft/Shaders/CardOutline.shadergraph",
  "Assets/Art/Shaders/卡牌轮廓.shadergraph",
  "StackCraft CardOutline 外轮廓 shadergraph");
assertScriptedImporterMetaMatches(
  "Assets/StackCraft/Shaders/CardOutline.shadergraph",
  "Assets/Art/Shaders/卡牌轮廓.shadergraph",
  "StackCraft CardOutline 外轮廓 shadergraph");

assertSameFileHash(
  "Assets/StackCraft/Models/Card.fbx",
  "Assets/Art/Models/卡牌.fbx",
  "StackCraft Card 卡牌模型");
assertModelImportSettingsMatch(
  "Assets/StackCraft/Models/Card.fbx",
  "Assets/Art/Models/卡牌.fbx",
  "StackCraft Card 卡牌模型");

assertSameFileHash(
  "Assets/StackCraft/Models/Pack.fbx",
  packMeshFile,
  "StackCraft PackInstance 卡包模型");
assertModelImportSettingsMatch(
  "Assets/StackCraft/Models/Pack.fbx",
  packMeshFile,
  "StackCraft PackInstance 卡包模型");

assertSameFileHash(
  "Assets/StackCraft/Shaders/EquipmentPanel.shadergraph",
  equipmentPanelShaderFile,
  "StackCraft EquipmentPanel 装备面板 shadergraph");
assertScriptedImporterMetaMatches(
  "Assets/StackCraft/Shaders/EquipmentPanel.shadergraph",
  equipmentPanelShaderFile,
  "StackCraft EquipmentPanel 装备面板 shadergraph");

assertSameFileHash(
  "Assets/StackCraft/Models/EquipmentPanel.fbx",
  equipmentPanelMeshFile,
  "StackCraft EquipmentPanel 装备面板模型");
assertModelImportSettingsMatch(
  "Assets/StackCraft/Models/EquipmentPanel.fbx",
  equipmentPanelMeshFile,
  "StackCraft EquipmentPanel 装备面板模型");

assertSameFileHash(
  "Assets/StackCraft/Textures/EquipmentSlots.png",
  "Assets/Art/Sprites/StackCraft/EquipmentSlots.png",
  "StackCraft EquipmentSlots 装备槽贴图");

const boardShaderGuid = unityGuid(readIfExists("Assets/Art/Shaders/桌面简单光照.shadergraph.meta"));
const boardMeshGuid = unityGuid(readIfExists("Assets/Art/Models/牌桌.fbx.meta"));
const boardBodyMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/牌桌主体_01.mat.meta"));
const boardHeaderMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/牌桌页眉_01.mat.meta"));
const islandBoardBodyMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/牌桌主体_02.mat.meta"));
const islandBoardHeaderMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/牌桌页眉_02.mat.meta"));
const grassBackgroundTextureGuid = unityGuid(readIfExists("Assets/Art/Textures/草地背景.png.meta"));
const waterBackgroundTextureGuid = unityGuid(readIfExists("Assets/Art/Textures/水面背景.png.meta"));
const grassBackgroundMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/草地背景.mat.meta"));
const waterBackgroundMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/水面背景.mat.meta"));
if (boardShaderGuid == null) {
  fail("Assets/Art/Shaders/桌面简单光照.shadergraph.meta 缺少合法 GUID。");
}
if (boardMeshGuid == null) {
  fail("Assets/Art/Models/牌桌.fbx.meta 缺少合法 GUID。");
}
if (boardBodyMaterialGuid == null) {
  fail("Assets/Art/Materials/牌桌主体_01.mat.meta 缺少合法 GUID。");
}
if (boardHeaderMaterialGuid == null) {
  fail("Assets/Art/Materials/牌桌页眉_01.mat.meta 缺少合法 GUID。");
}
if (islandBoardBodyMaterialGuid == null) {
  fail("Assets/Art/Materials/牌桌主体_02.mat.meta 缺少合法 GUID。");
}
if (islandBoardHeaderMaterialGuid == null) {
  fail("Assets/Art/Materials/牌桌页眉_02.mat.meta 缺少合法 GUID。");
}
if (grassBackgroundTextureGuid == null) {
  fail("Assets/Art/Textures/草地背景.png.meta 缺少合法 GUID。");
}
if (waterBackgroundTextureGuid == null) {
  fail("Assets/Art/Textures/水面背景.png.meta 缺少合法 GUID。");
}
if (grassBackgroundMaterialGuid == null) {
  fail("Assets/Art/Materials/草地背景.mat.meta 缺少合法 GUID。");
}
if (waterBackgroundMaterialGuid == null) {
  fail("Assets/Art/Materials/水面背景.mat.meta 缺少合法 GUID。");
}
assertSameFileHash(
  "Assets/StackCraft/Shaders/SimpleLit.shadergraph",
  "Assets/Art/Shaders/桌面简单光照.shadergraph",
  "StackCraft Board 简单光照 shadergraph");
assertScriptedImporterMetaMatches(
  "Assets/StackCraft/Shaders/SimpleLit.shadergraph",
  "Assets/Art/Shaders/桌面简单光照.shadergraph",
  "StackCraft Board 简单光照 shadergraph");
assertSameFileHash(
  "Assets/StackCraft/Models/Board.fbx",
  "Assets/Art/Models/牌桌.fbx",
  "StackCraft Board 桌面模型");
assertModelImportSettingsMatch(
  "Assets/StackCraft/Models/Board.fbx",
  "Assets/Art/Models/牌桌.fbx",
  "StackCraft Board 桌面模型");
assertSameFileHash(
  "Assets/StackCraft/Textures/Backgrounds/Grass.png",
  "Assets/Art/Textures/草地背景.png",
  "StackCraft Main 草地背景贴图");
assertTextureImportVisualSettingsMatch(
  "Assets/StackCraft/Textures/Backgrounds/Grass.png",
  "Assets/Art/Textures/草地背景.png",
  "StackCraft Main 草地背景贴图",
  { requireSpriteImport: false });
assertSameFileHash(
  "Assets/StackCraft/Textures/Backgrounds/Water.png",
  "Assets/Art/Textures/水面背景.png",
  "StackCraft Island 水面背景贴图");
assertTextureImportVisualSettingsMatch(
  "Assets/StackCraft/Textures/Backgrounds/Water.png",
  "Assets/Art/Textures/水面背景.png",
  "StackCraft Island 水面背景贴图",
  { requireSpriteImport: false });

for (const [sourceMaterialFile, localMaterialFile, label] of [
  ["Assets/StackCraft/Materials/Boards/Body_01.mat", "Assets/Art/Materials/牌桌主体_01.mat", "主体"],
  ["Assets/StackCraft/Materials/Boards/Header_01.mat", "Assets/Art/Materials/牌桌页眉_01.mat", "页眉"],
  ["Assets/StackCraft/Materials/Boards/Body_02.mat", "Assets/Art/Materials/牌桌主体_02.mat", "Island 主体"],
  ["Assets/StackCraft/Materials/Boards/Header_02.mat", "Assets/Art/Materials/牌桌页眉_02.mat", "Island 页眉"],
]) {
  const sourceMaterialText = readIfExists(sourceMaterialFile);
  const localMaterialText = readIfExists(localMaterialFile);
  if (sourceMaterialText == null || localMaterialText == null) {
    fail(`缺少 StackCraft Board01 ${label}材质来源或自有副本：${sourceMaterialFile} -> ${localMaterialFile}`);
    continue;
  }
  const sourceColorLine = yamlPropertyLine(sourceMaterialText, "_Color");
  const localColorLine = yamlPropertyLine(localMaterialText, "_Color");
  if (sourceColorLine == null || localColorLine !== sourceColorLine) {
    fail(`${localMaterialFile} 的 _Color 与 StackCraft Board01 ${label}材质不一致：${localColorLine ?? "<缺失>"}，应为 ${sourceColorLine ?? "<缺失>"}`);
  }
  assertMaterialShaderGuid(
    localMaterialText,
    boardShaderGuid,
    localMaterialFile,
    "StackCraft Board 简单光照 shadergraph");
}

for (const [sourceMaterialFile, localMaterialFile, textureGuid, label] of [
  ["Assets/StackCraft/Materials/Backgrounds/Grass.mat", "Assets/Art/Materials/草地背景.mat", grassBackgroundTextureGuid, "Main 草地背景"],
  ["Assets/StackCraft/Materials/Backgrounds/Water.mat", "Assets/Art/Materials/水面背景.mat", waterBackgroundTextureGuid, "Island 水面背景"],
]) {
  const sourceMaterialText = readIfExists(sourceMaterialFile);
  const localMaterialText = readIfExists(localMaterialFile);
  if (sourceMaterialText == null || localMaterialText == null) {
    fail(`缺少 StackCraft ${label}材质来源或自有副本：${sourceMaterialFile} -> ${localMaterialFile}`);
    continue;
  }
  assertYamlMappingPropertyLinesMatch(
    sourceMaterialText,
    localMaterialText,
    sourceMaterialFile,
    localMaterialFile,
    ["m_Shader", "m_Scale", "m_Offset"],
    `${label}材质`);
  assertYamlPropertyLinesMatch(
    sourceMaterialText,
    localMaterialText,
    sourceMaterialFile,
    localMaterialFile,
    ["_Color"],
    `${label}材质`);
  assertMaterialTextureGuid(
    localMaterialText,
    "_MainTex",
    textureGuid,
    localMaterialFile,
    `${label}材质 _MainTex`);
}

const boardMetaText = readIfExists("Assets/Art/Models/牌桌.fbx.meta");
if (boardMetaText == null) {
  fail("缺少 Assets/Art/Models/牌桌.fbx.meta，无法证明 Board.fbx 材质映射闭包。");
} else {
  assertModelMaterialExternalObjectGuid(
    boardMetaText,
    "Body",
    boardBodyMaterialGuid,
    "Assets/Art/Models/牌桌.fbx.meta",
    "Board.fbx Body -> 牌桌主体_01");
  assertModelMaterialExternalObjectGuid(
    boardMetaText,
    "Header",
    boardHeaderMaterialGuid,
    "Assets/Art/Models/牌桌.fbx.meta",
    "Board.fbx Header -> 牌桌页眉_01");
}

let stackCraftBoardSurfaceHeight = null;
const foundationSceneGeneratorSource = readIfExists("Assets/Tests/Support/Editor/FoundationTestSceneMenu.cs");
if (foundationSceneGeneratorSource == null) {
  fail("缺少 FoundationTest 场景生成器，无法证明 StackCraft Board01 桌面表面会进入测试入口。");
} else {
  const stackCraftMainSceneYaml = stackCraftMainSceneText == null
    ? null
    : unityYamlObjects(stackCraftMainSceneText);
  const stackCraftBackgroundPosition = stackCraftMainSceneYaml == null
    ? null
    : csharpConstructorFromUnityComponentInlineProperty(
      stackCraftMainSceneYaml,
      "Background",
      4,
      "m_LocalPosition",
      "Vector3",
      ["x", "y", "z"],
      "StackCraft Main Background.m_LocalPosition");
  const stackCraftBackgroundEuler = stackCraftMainSceneYaml == null
    ? null
    : csharpConstructorFromUnityComponentInlineProperty(
      stackCraftMainSceneYaml,
      "Background",
      4,
      "m_LocalEulerAnglesHint",
      "Vector3",
      ["x", "y", "z"],
      "StackCraft Main Background.m_LocalEulerAnglesHint");
  const stackCraftBackgroundRotation = stackCraftBackgroundEuler == null
    ? null
    : stackCraftBackgroundEuler.replace("new Vector3", "Quaternion.Euler");
  const stackCraftBackgroundScale = stackCraftMainSceneYaml == null
    ? null
    : csharpConstructorFromUnityComponentInlineProperty(
      stackCraftMainSceneYaml,
      "Background",
      4,
      "m_LocalScale",
      "Vector3",
      ["x", "y", "z"],
      "StackCraft Main Background.m_LocalScale");
  const stackCraftBoardBoundsSize = stackCraftBoard01Placement == null
    ? null
    : `new Vector3(${csharpFloatLiteralFromUnityNumber(stackCraftBoard01Placement.localBoundsSize.x)}, ${csharpFloatLiteralFromUnityNumber(stackCraftBoard01Placement.localBoundsSize.y)}, ${csharpFloatLiteralFromUnityNumber(stackCraftBoard01Placement.localBoundsSize.z)})`;
  stackCraftBoardSurfaceHeight = csharpScalarInitializer(
    foundationSceneGeneratorSource,
    "StackCraftBoardSurfaceHeight",
    "FoundationTest 场景生成器 StackCraft 牌桌视觉底板高度");
  if (stackCraftBoardSurfaceHeight != null) {
    const boardSurfaceHeight = Number.parseFloat(unityNumberLiteralFromCsharp(stackCraftBoardSurfaceHeight));
    if (!Number.isFinite(boardSurfaceHeight) || Math.abs(boardSurfaceHeight - -0.05) > 0.000001) {
      fail(`FoundationTest 场景生成器 StackCraft 牌桌视觉底板高度必须等于 Main.unity 中 Board01 的场景覆盖值 -0.05：当前 ${stackCraftBoardSurfaceHeight}。`);
    }
  }
  assertCsharpBlockContainsOrdered(
    foundationSceneGeneratorSource,
    "private static void RebuildTabletopScene",
    [
      "CreateStackCraftBackgroundSurface(testRoot.transform);",
      "CreateStackCraftBoardSurface(testRoot.transform);",
    ],
    "FoundationTest 场景生成器在牌桌场景中创建 StackCraft 桌面表面");
  assertCsharpBlockContainsOrdered(
    foundationSceneGeneratorSource,
    "private static void RebuildMapTestScene",
    [
      "bool isIslandScene = string.Equals(",
      "WaterBackgroundMaterialPath",
      "StackCraft Island 水面背景材质自有副本",
      "IslandBoardBodyMaterialPath",
      "IslandBoardHeaderMaterialPath",
      "StackCraft Board02 主体材质自有副本",
      "StackCraft Board02 页眉材质自有副本",
      "GrassBackgroundMaterialPath",
      "StackCraft Main 草地背景材质自有副本",
      "BoardBodyMaterialPath",
      "BoardHeaderMaterialPath",
      "StackCraft Board01 主体材质自有副本",
      "StackCraft Board01 页眉材质自有副本",
    ],
    "FoundationTest 地图场景生成器按 StackCraft Main / Island 桌面表面分支创建");
  assertCsharpBlockContainsOrdered(
    foundationSceneGeneratorSource,
    "private static void CreateStackCraftBackgroundSurface(Transform parent)",
    [
      "GrassBackgroundMaterialPath",
      "StackCraft Main 草地背景材质自有副本",
    ],
    "FoundationTest 场景生成器 StackCraft 默认背景表面重载");
  assertCsharpBlockContainsOrdered(
    foundationSceneGeneratorSource,
    "string materialDescription)",
    [
      "Material backgroundMaterial = LoadRequiredMaterial(",
      "GameObject backgroundObject = GameObject.CreatePrimitive(PrimitiveType.Plane);",
      "backgroundObject.name = \"桌面背景\";",
      "backgroundObject.transform.SetParent(parent, false);",
      "ApplyStackCraftBackgroundTransform(backgroundObject.transform);",
      "Object.DestroyImmediate(collider);",
      "renderer.sharedMaterial = backgroundMaterial;",
    ],
    "FoundationTest 场景生成器 StackCraft 背景表面对象构造");
  assertCsharpBlockContainsOrdered(
    foundationSceneGeneratorSource,
    "private static void CreateStackCraftBoardSurface(Transform parent)",
    [
      "BoardBodyMaterialPath",
      "BoardHeaderMaterialPath",
      "StackCraft Board01 主体材质自有副本",
      "StackCraft Board01 页眉材质自有副本",
    ],
    "FoundationTest 场景生成器 StackCraft 默认牌桌表面重载");
  assertCsharpBlockContainsOrdered(
    foundationSceneGeneratorSource,
    "string headerMaterialDescription)",
    [
      "Mesh boardMesh = LoadRequiredMesh(BoardMeshPath, \"StackCraft Board.fbx 自有副本\");",
      "Material bodyMaterial = LoadRequiredMaterial(",
      "Material headerMaterial = LoadRequiredMaterial(",
      "GameObject boardObject = new(\"牌桌底板\");",
      "boardObject.transform.localPosition = new Vector3(0f, StackCraftBoardSurfaceHeight, 0f);",
      "SkinnedMeshRenderer renderer = boardObject.AddComponent<SkinnedMeshRenderer>();",
      "renderer.sharedMesh = boardMesh;",
      "renderer.sharedMaterials = new[] { bodyMaterial, headerMaterial };",
      "renderer.localBounds = new Bounds(Vector3.zero, StackCraftBoardLocalBoundsSize);",
    ],
    "FoundationTest 场景生成器 StackCraft 牌桌表面对象构造");
  for (const [assignmentTarget, expectedInitializer, label] of [
    ["backgroundObject.transform.localPosition", stackCraftBackgroundPosition, "桌面背景位置"],
    ["backgroundObject.transform.localRotation", stackCraftBackgroundRotation, "桌面背景旋转"],
    ["backgroundObject.transform.localScale", stackCraftBackgroundScale, "桌面背景缩放"],
    ["boardObject.transform.localPosition", "new Vector3(0f, StackCraftBoardSurfaceHeight, 0f)", "牌桌底板视觉高度"],
    ["renderer.localBounds", "new Bounds(Vector3.zero, StackCraftBoardLocalBoundsSize)", "牌桌底板本地包围盒"],
  ].filter((entry) => entry[1] != null)) {
    assertCsharpAssignmentEquals(
      foundationSceneGeneratorSource,
      assignmentTarget,
      expectedInitializer,
      `FoundationTest 场景生成器 StackCraft ${label}`);
  }
}

function assertSceneHasStackCraftBoardSurface(
  scenePath,
  sceneText,
  sourceSceneText,
  sourceBoardYaml,
  sourceBoardObjectName,
  backgroundMaterialGuid,
  bodyMaterialGuid,
  headerMaterialGuid,
  label) {
  if (sceneText == null) {
    fail(`缺少 ${scenePath}，无法证明 ${label} 场景包含 StackCraft 桌面表面。`);
    return;
  }

  const parsedScene = unityYamlObjects(sceneText);
  assertUnityGameObjectExists(parsedScene, "桌面背景", `${scenePath} ${label} 背景`);
  const sourceSceneYaml = sourceSceneText == null ? null : unityYamlObjects(sourceSceneText);
  if (sourceSceneYaml == null) {
    fail(`${scenePath} ${label} 缺少 StackCraft 来源场景，无法对账桌面背景对象。`);
  } else {
    assertUnityComponentPropertiesMatch(
      sourceSceneYaml,
      "Background",
      parsedScene,
      "桌面背景",
      4,
      ["m_LocalRotation", "m_LocalPosition", "m_LocalScale", "m_LocalEulerAnglesHint"],
      `${scenePath} ${label} 背景 Transform`);
    assertUnityComponentPropertiesMatch(
      sourceSceneYaml,
      "Background",
      parsedScene,
      "桌面背景",
      33,
      ["m_Mesh"],
      `${scenePath} ${label} 背景 MeshFilter`);
    assertUnityComponentPropertiesMatch(
      sourceSceneYaml,
      "Background",
      parsedScene,
      "桌面背景",
      23,
      ["m_CastShadows", "m_ReceiveShadows", "m_LightProbeUsage", "m_ReflectionProbeUsage"],
      `${scenePath} ${label} 背景 Renderer`);
  }
  if (backgroundMaterialGuid != null) {
    assertUnityComponentReferenceListEquals(
      parsedScene,
      "桌面背景",
      23,
      "m_Materials",
      [{ fileId: "2100000", guid: backgroundMaterialGuid, type: "2" }],
      `${scenePath} ${label} 背景 Renderer`);
  }

  assertUnityGameObjectExists(parsedScene, "牌桌底板", `${scenePath} ${label} 牌桌底板`);
  if (sourceBoardYaml == null) {
    fail(`${scenePath} ${label} 缺少 StackCraft 来源 Board Prefab，无法对账牌桌底板对象。`);
  } else {
    assertUnityComponentPropertiesMatch(
      sourceBoardYaml,
      sourceBoardObjectName,
      parsedScene,
      "牌桌底板",
      4,
      ["m_LocalRotation", "m_LocalScale", "m_LocalEulerAnglesHint"],
      `${scenePath} ${label} 牌桌底板 Transform`);
    if (stackCraftBoardSurfaceHeight != null) {
      assertUnityComponentInlineNumericPropertyMatches(
        parsedScene,
        "牌桌底板",
        4,
        "m_LocalPosition",
        new Map([
          ["x", "0"],
          ["y", unityNumberLiteralFromCsharp(stackCraftBoardSurfaceHeight)],
          ["z", "0"],
        ]),
        ["x", "y", "z"],
        `${scenePath} ${label} 牌桌底板视觉高度`);
    }
    assertUnityComponentPropertiesMatch(
      sourceBoardYaml,
      sourceBoardObjectName,
      parsedScene,
      "牌桌底板",
      137,
      ["m_CastShadows", "m_ReceiveShadows", "m_UpdateWhenOffscreen", "m_Quality", "m_AABB", "m_Center", "m_Extent", "m_DirtyAABB"],
      `${scenePath} ${label} 牌桌底板 SkinnedMeshRenderer`);
  }
  if (boardMeshGuid != null) {
    assertUnityComponentReferenceEquals(
      parsedScene,
      "牌桌底板",
      137,
      "m_Mesh",
      "8616276468678558677",
      boardMeshGuid,
      "3",
      `${scenePath} ${label} 牌桌底板 SkinnedMeshRenderer`);
  }
  if (bodyMaterialGuid != null && headerMaterialGuid != null) {
    assertUnityComponentReferenceListEquals(
      parsedScene,
      "牌桌底板",
      137,
      "m_Materials",
      [
        { fileId: "2100000", guid: bodyMaterialGuid, type: "2" },
        { fileId: "2100000", guid: headerMaterialGuid, type: "2" },
      ],
      `${scenePath} ${label} 牌桌底板 SkinnedMeshRenderer`);
  }
}

assertSceneHasStackCraftBoardSurface(
  "Assets/Scenes/FoundationTest.unity",
  foundationSceneText,
  stackCraftMainSceneText,
  stackCraftBoard01Yaml,
  "Board01",
  grassBackgroundMaterialGuid,
  boardBodyMaterialGuid,
  boardHeaderMaterialGuid,
  "StackCraft Main / Board01");
assertSceneHasStackCraftBoardSurface(
  "Assets/Scenes/FoundationMapTest.unity",
  readIfExists("Assets/Scenes/FoundationMapTest.unity"),
  stackCraftMainSceneText,
  stackCraftBoard01Yaml,
  "Board01",
  grassBackgroundMaterialGuid,
  boardBodyMaterialGuid,
  boardHeaderMaterialGuid,
  "StackCraft Main / Board01");
assertSceneHasStackCraftStandaloneMainCamera(
  "Assets/Scenes/FoundationMapTest.unity",
  readIfExists("Assets/Scenes/FoundationMapTest.unity"),
  "FoundationMapTest");
assertSceneHasStackCraftBoardSurface(
  "Assets/Scenes/FoundationSecondMapTest.unity",
  readIfExists("Assets/Scenes/FoundationSecondMapTest.unity"),
  stackCraftIslandSceneText,
  stackCraftBoard02Yaml,
  "Board02",
  waterBackgroundMaterialGuid,
  islandBoardBodyMaterialGuid,
  islandBoardHeaderMaterialGuid,
  "StackCraft Island / Board02");
assertSceneHasStackCraftStandaloneMainCamera(
  "Assets/Scenes/FoundationSecondMapTest.unity",
  readIfExists("Assets/Scenes/FoundationSecondMapTest.unity"),
  "FoundationSecondMapTest");

const cardMeshGuid = unityGuid(readIfExists("Assets/Art/Models/卡牌.fbx.meta"));
if (cardMeshGuid == null) {
  fail("Assets/Art/Models/卡牌.fbx.meta 缺少合法 GUID。");
}

const packMeshGuid = unityGuid(readIfExists(`${packMeshFile}.meta`));
if (packMeshGuid == null) {
  fail(`${packMeshFile}.meta 缺少合法 GUID。`);
}

const equipmentPanelMeshGuid = unityGuid(readIfExists("Assets/Art/Models/装备面板.fbx.meta"));
if (equipmentPanelMeshGuid == null) {
  fail("Assets/Art/Models/装备面板.fbx.meta 缺少合法 GUID。");
}

const stackCraftTmpFontGuid = unityGuid(readIfExists("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset.meta"));
if (stackCraftTmpFontGuid == null) {
  fail("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset.meta 缺少合法 GUID，无法证明 StackCraft TMP 字体来源。");
}
const foundationChineseFontGuid = unityGuid(readIfExists("Assets/Gameplay/Tests/牌桌/地基测试中文字体.asset.meta"));
if (foundationChineseFontGuid == null) {
  fail("Assets/Gameplay/Tests/牌桌/地基测试中文字体.asset.meta 缺少合法 GUID，无法检查卡牌中文内容字体。");
}
const foundationChineseFontMaterialFileId = "-1204229514610621180";
const stackCraftTmpFontMaterialFileId = "2180264";

const defaultCardSurfaceMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/卡牌表面_角色.mat.meta"));
if (defaultCardSurfaceMaterialGuid == null) {
  fail("Assets/Art/Materials/卡牌表面_角色.mat.meta 缺少合法 GUID。");
}

const equipmentPanelShaderGuid = unityGuid(readIfExists("Assets/Art/Shaders/装备面板.shadergraph.meta"));
const equipmentPanelMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/装备面板.mat.meta"));
if (equipmentPanelShaderGuid == null) {
  fail("Assets/Art/Shaders/装备面板.shadergraph.meta 缺少合法 GUID。");
}
if (equipmentPanelMaterialGuid == null) {
  fail("Assets/Art/Materials/装备面板.mat.meta 缺少合法 GUID。");
}

for (const materialFile of expectedCardSurfaceMaterialFiles) {
  const materialText = readIfExists(materialFile);
  if (materialText == null) continue;
  assertMaterialShaderGuid(
    materialText,
    cardSurfaceShaderGuid,
    materialFile,
    "StackCraft Card 卡牌表面 shadergraph");
  assertYamlPropertyLinesPresent(
    materialText,
    ["_BaseTex", "_MainTex", "_OverlayTex", "_FlashAmount", "_OverlayScale", "_OverlayOffset", "_OverlayTint"],
    materialFile,
    "StackCraft Card 材质");
}

const packCardSurfaceMaterialText = readIfExists(packCardSurfaceMaterialFile);
if (packCardSurfaceMaterialText != null) {
  const packTextureGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/Pack.png.meta"));
  const placeholderTextureGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/CardArts/Placeholder.png.meta"));
  assertMaterialShaderGuid(
    packCardSurfaceMaterialText,
    cardSurfaceShaderGuid,
    packCardSurfaceMaterialFile,
    "StackCraft Card 卡包表面 shadergraph");
  for (const [label, guid, textureProperty] of [
    ["Pack.png", packTextureGuid, "_BaseTex"],
    ["Pack.png", packTextureGuid, "_MainTex"],
    ["Placeholder.png", placeholderTextureGuid, "_OverlayTex"],
  ]) {
    if (guid == null) {
      fail(`缺少 ${label} 的合法 GUID，无法静态验证卡包材质贴图闭包。`);
    } else {
      assertMaterialTextureGuid(
        packCardSurfaceMaterialText,
        textureProperty,
        guid,
        packCardSurfaceMaterialFile,
        `StackCraft Pack.mat ${textureProperty}`);
    }
  }
  assertMaterialNameEquals(packCardSurfaceMaterialText, "卡牌表面_卡包", packCardSurfaceMaterialFile);
  assertYamlPropertyLinesPresent(packCardSurfaceMaterialText, ["_BaseTex", "_MainTex", "_OverlayTex"], packCardSurfaceMaterialFile, "StackCraft Pack.mat");
  const stackCraftPackMaterialText = readIfExists("Assets/StackCraft/Materials/Pack.mat");
  if (stackCraftPackMaterialText == null) {
    fail("缺少 StackCraft Pack.mat 来源材质，无法对账卡包表面参数。");
  } else {
    assertYamlPropertyLinesMatch(
      stackCraftPackMaterialText,
      packCardSurfaceMaterialText,
      "Assets/StackCraft/Materials/Pack.mat",
      packCardSurfaceMaterialFile,
      ["_FlashAmount", "_OverlayScale", "_Color", "_OverlayOffset", "_OverlayTint"],
      "Pack.mat");
  }
}

const equipmentPanelMaterialText = readIfExists(equipmentPanelMaterialFile);
if (equipmentPanelMaterialText != null) {
  const equipmentSlotsTextureGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/EquipmentSlots.png.meta"));
  assertMaterialShaderGuid(
    equipmentPanelMaterialText,
    equipmentPanelShaderGuid,
    equipmentPanelMaterialFile,
    "StackCraft EquipmentPanel shadergraph");
  if (equipmentSlotsTextureGuid == null) {
    fail("缺少 EquipmentSlots.png 的合法 GUID，无法静态验证装备面板材质贴图闭包。");
  } else {
    assertMaterialTextureGuid(
      equipmentPanelMaterialText,
      "_OverlayTex",
      equipmentSlotsTextureGuid,
      equipmentPanelMaterialFile,
      "StackCraft EquipmentPanel.mat _OverlayTex");
  }
  assertMaterialNameEquals(equipmentPanelMaterialText, "装备面板", equipmentPanelMaterialFile);
  assertYamlPropertyLinesPresent(equipmentPanelMaterialText, ["_OverlayTex"], equipmentPanelMaterialFile, "StackCraft EquipmentPanel.mat");
  const stackCraftEquipmentPanelMaterialText = readIfExists("Assets/StackCraft/Materials/UI/EquipmentPanel.mat");
  if (stackCraftEquipmentPanelMaterialText == null) {
    fail("缺少 StackCraft EquipmentPanel.mat 来源材质，无法对账装备面板参数。");
  } else {
    assertYamlPropertyLinesMatch(
      stackCraftEquipmentPanelMaterialText,
      equipmentPanelMaterialText,
      "Assets/StackCraft/Materials/UI/EquipmentPanel.mat",
      equipmentPanelMaterialFile,
      [
        "_CellActive0",
        "_CellActive1",
        "_CellActive2",
        "_TintCellIndex",
        "_TintCount",
        "_BaseColor",
        "_InactiveColor",
        "_TintColor2",
      ],
      "EquipmentPanel.mat");
  }
}

const cardBuyerSurfaceMaterialText = readIfExists(cardBuyerSurfaceMaterialFile);
if (cardBuyerSurfaceMaterialText != null) {
  const resourceTextureGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/Cards/Resource.png.meta"));
  const placeholderTextureGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/CardArts/Placeholder.png.meta"));
  assertMaterialShaderGuid(
    cardBuyerSurfaceMaterialText,
    cardSurfaceShaderGuid,
    cardBuyerSurfaceMaterialFile,
    "StackCraft Card 交易区表面 shadergraph");
  for (const [label, guid, textureProperty] of [
    ["Cards/Resource.png", resourceTextureGuid, "_BaseTex"],
    ["CardArts/Placeholder.png", placeholderTextureGuid, "_OverlayTex"],
  ]) {
    if (guid == null) {
      fail(`缺少 ${label} 的合法 GUID，无法静态验证收购点交易区材质贴图闭包。`);
    } else {
      assertMaterialTextureGuid(
        cardBuyerSurfaceMaterialText,
        textureProperty,
        guid,
        cardBuyerSurfaceMaterialFile,
        `StackCraft TradeZone.mat ${textureProperty}`);
    }
  }
  assertMaterialNameEquals(cardBuyerSurfaceMaterialText, "交易区", cardBuyerSurfaceMaterialFile);
  assertYamlPropertyLinesPresent(cardBuyerSurfaceMaterialText, ["_BaseTex", "_OverlayTex"], cardBuyerSurfaceMaterialFile, "StackCraft TradeZone.mat");
  const stackCraftTradeZoneMaterialText = readIfExists("Assets/StackCraft/Materials/TradeZone.mat");
  if (stackCraftTradeZoneMaterialText == null) {
    fail("缺少 StackCraft TradeZone.mat 来源材质，无法对账交易区参数。");
  } else {
    assertYamlPropertyLinesMatch(
      stackCraftTradeZoneMaterialText,
      cardBuyerSurfaceMaterialText,
      "Assets/StackCraft/Materials/TradeZone.mat",
      cardBuyerSurfaceMaterialFile,
      ["_OverlayScale", "_Color", "_OverlayOffset"],
      "TradeZone.mat");
  }
}

const cardBuyerCurrencyIconMaterialText = readIfExists(cardBuyerCurrencyIconMaterialFile);
if (cardBuyerCurrencyIconMaterialText != null) {
  const placeholderTextureGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/CardArts/Placeholder.png.meta"));
  assertMaterialShaderReference(
    cardBuyerCurrencyIconMaterialText,
    "m_Shader: {fileID: 10750, guid: 0000000000000000f000000000000000, type: 0}",
    cardBuyerCurrencyIconMaterialFile,
    "StackCraft CurrencyIcon.mat Standard shader");
  if (placeholderTextureGuid == null) {
    fail("缺少 CardArts/Placeholder.png 的合法 GUID，无法静态验证收购点货币图标材质。");
  } else {
    assertMaterialTextureGuid(
      cardBuyerCurrencyIconMaterialText,
      "_MainTex",
      placeholderTextureGuid,
      cardBuyerCurrencyIconMaterialFile,
      "StackCraft CurrencyIcon.mat _MainTex");
  }
}

if (cardViewPrefabText == null) {
  fail("缺少卡牌视图 Prefab，无法证明卡牌表面、标题和受击反馈静态承载。");
} else {
  const cardViewPrefabYaml = unityYamlObjects(cardViewPrefabText);
  const stackCraftCharacterCardYaml = stackCraftCharacterCardPrefabText == null
    ? null
    : unityYamlObjects(stackCraftCharacterCardPrefabText);
  const stackCraftConsumableCardYaml = stackCraftConsumableCardPrefabText == null
    ? null
    : unityYamlObjects(stackCraftConsumableCardPrefabText);
  const stackCraftCardBuyerYaml = stackCraftCardBuyerPrefabText == null
    ? null
    : unityYamlObjects(stackCraftCardBuyerPrefabText);
  const stackCraftCardPrefabCases = [
    ["Assets/StackCraft/Prefabs/Cards/Card_Area.prefab", "Card_Area", ["Title", "Price"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Character.prefab", "Card_Character", ["Title", "Health"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Consumable.prefab", "Card_Consumable", ["Title", "Price", "Nutrition"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Currency.prefab", "Card_Currency", ["Title"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Equipment.prefab", "Card_Equipment", ["Title", "Price"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Material.prefab", "Card_Material", ["Title", "Price"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Mob.prefab", "Card_Mob", ["Title", "Health"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Mob_Aggressive.prefab", "Card_Mob_Aggressive", ["Title", "Health"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Recipe.prefab", "Card_Recipe", ["Title", "Price"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Resource.prefab", "Card_Resource", ["Title", "Price"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Structure.prefab", "Card_Structure", ["Title", "Price"]],
    ["Assets/StackCraft/Prefabs/Cards/Card_Valuable.prefab", "Card_Valuable", ["Title", "Price"]],
  ].map(([sourcePath, rootObjectName, textObjectNames]) => {
    const sourceText = readIfExists(sourcePath);
    return {
      sourcePath,
      rootObjectName,
      textObjectNames,
      parsedYaml: sourceText == null ? null : unityYamlObjects(sourceText),
    };
  });
  if (stackCraftCharacterCardYaml == null) {
    fail("缺少 StackCraft 角色卡 Prefab，无法做卡牌标题、生命和装备面板对象级对账：Assets/StackCraft/Prefabs/Cards/Card_Character.prefab");
  }
  if (stackCraftConsumableCardYaml == null) {
    fail("缺少 StackCraft 消耗品卡 Prefab，无法做价格和营养文字对象级对账：Assets/StackCraft/Prefabs/Cards/Card_Consumable.prefab");
  }
  if (stackCraftCardBuyerYaml == null) {
    fail("缺少 StackCraft 收购点 Prefab，无法做收购货币图标对象级对账：Assets/StackCraft/Prefabs/Trading/CardBuyer.prefab");
  }
  for (const cardCase of stackCraftCardPrefabCases) {
    if (cardCase.parsedYaml == null) {
      fail(`缺少 StackCraft 卡牌类别 Prefab，无法纳入普通卡面文字全量对账：${cardCase.sourcePath}`);
      continue;
    }

    for (const textObjectName of cardCase.textObjectNames) {
      const targetObjectName = textObjectName === "Title"
        ? "标题"
        : textObjectName === "Price"
          ? "价格"
          : textObjectName === "Nutrition"
            ? "营养"
            : "生命";
      assertUnityTextObjectMatchesSource(
        cardCase.parsedYaml,
        textObjectName,
        cardViewPrefabYaml,
        targetObjectName,
        `卡牌视图 ${cardCase.sourcePath} ${textObjectName} 文字`);
    }
  }
  if (stackCraftCharacterCardYaml != null) {
    assertUnityComponentPropertiesMatch(
      stackCraftCharacterCardYaml,
      "Card_Character",
      cardViewPrefabYaml,
      "卡牌视图",
      65,
      ["m_Size"],
      "默认卡牌根碰撞盒");
    assertUnityTextObjectMatchesSource(
      stackCraftCharacterCardYaml,
      "Title",
      cardViewPrefabYaml,
      "标题",
      "卡牌视图标题文字");
    assertUnityTextObjectMatchesSource(
      stackCraftCharacterCardYaml,
      "Health",
      cardViewPrefabYaml,
      "生命",
      "角色卡生命数字");
    assertUnityComponentPropertiesMatch(
      stackCraftCharacterCardYaml,
      "EquipmentPanel",
      cardViewPrefabYaml,
      "装备面板",
      4,
      ["m_LocalRotation", "m_LocalPosition", "m_LocalScale", "m_LocalEulerAnglesHint"],
      "角色装备面板 Transform");
    assertUnityComponentPropertiesMatch(
      stackCraftCharacterCardYaml,
      "EquipmentPanel",
      cardViewPrefabYaml,
      "装备面板",
      23,
      ["m_CastShadows", "m_ReceiveShadows"],
      "角色装备面板 Renderer");
    assertUnityComponentPropertiesMatch(
      stackCraftCharacterCardYaml,
      "EquipmentPanel",
      cardViewPrefabYaml,
      "装备面板",
      65,
      ["m_Size"],
      "角色装备面板 Collider");
  }
  if (stackCraftConsumableCardYaml != null) {
    assertUnityTextObjectMatchesSource(
      stackCraftConsumableCardYaml,
      "Price",
      cardViewPrefabYaml,
      "价格",
      "卡牌价格文字");
    assertUnityTextObjectMatchesSource(
      stackCraftConsumableCardYaml,
      "Nutrition",
      cardViewPrefabYaml,
      "营养",
      "卡牌营养文字");
  }
  if (stackCraftCardBuyerYaml != null) {
    assertUnityComponentPropertiesMatch(
      stackCraftCardBuyerYaml,
      "Icon",
      cardViewPrefabYaml,
      "收购货币图标",
      4,
      ["m_LocalRotation", "m_LocalPosition", "m_LocalScale", "m_LocalEulerAnglesHint"],
      "收购货币图标 Transform");
    assertUnityComponentPropertiesMatch(
      stackCraftCardBuyerYaml,
      "Icon",
      cardViewPrefabYaml,
      "收购货币图标",
      33,
      ["m_Mesh"],
      "收购货币图标 Mesh");
    assertUnityComponentPropertiesMatch(
      stackCraftCardBuyerYaml,
      "Icon",
      cardViewPrefabYaml,
      "收购货币图标",
      23,
      ["m_CastShadows", "m_ReceiveShadows"],
      "收购货币图标 Renderer");
  }
  assertUnityComponentScalarEquals(
    cardViewPrefabYaml,
    "卡牌视图",
    114,
    "m_surfaceTextureProperty",
    "_OverlayTex",
    "卡牌视图卡面贴图属性");
  assertUnityComponentScalarEquals(
    cardViewPrefabYaml,
    "卡牌视图",
    114,
    "m_cardBuyerCurrencyTextureProperty",
    "_MainTex",
    "卡牌视图收购货币贴图属性");
  assertUnityComponentFieldReferences(
    cardViewPrefabYaml,
    "卡牌视图",
    "m_titleLabel",
    "标题",
    114,
    "卡牌视图标题字段引用");
  assertUnityComponentFieldReferences(
    cardViewPrefabYaml,
    "卡牌视图",
    "m_cardBuyerCurrencyIconRenderer",
    "收购货币图标",
    23,
    "卡牌视图收购货币图标字段引用");
  assertUnityComponentFieldReferences(
    cardViewPrefabYaml,
    "卡牌视图",
    "m_priceLabel",
    "价格",
    114,
    "卡牌视图价格字段引用");
  assertUnityComponentFieldReferences(
    cardViewPrefabYaml,
    "卡牌视图",
    "m_nutritionLabel",
    "营养",
    114,
    "卡牌视图营养字段引用");
  if (stackCraftCardInstanceSource == null) {
    fail("缺少 StackCraft CardInstance 源码，无法从 TakeDamage 派生受击闪白 / 摇晃参数：Assets/StackCraft/Scripts/Card/CardInstance.cs");
  } else {
    const hurtFeedback = csharpHurtFeedbackParameters(stackCraftCardInstanceSource, "StackCraft CardInstance.TakeDamage");
    if (hurtFeedback != null) {
      if (tabletopCardViewSource != null) {
        assertCsharpFieldInitializerEquals(
          tabletopCardViewSource,
          "m_surfaceFlashProperty",
          `"${hurtFeedback.flashProperty}"`,
          "卡牌受击闪白材质属性默认值");
      }
      assertYamlScalarStringEquals(
        cardViewPrefabText,
        "m_surfaceFlashProperty",
        hurtFeedback.flashProperty,
        "卡牌视图 Prefab 受击闪白材质属性");
      for (const [fieldName, sourceLiteral, label, isInteger] of [
        ["m_hurtFlashDelaySeconds", hurtFeedback.flashDelaySeconds, "受击闪白延迟秒数", false],
        ["m_hurtFlashTweenSeconds", hurtFeedback.flashTweenSeconds, "受击闪白单段秒数", false],
        ["m_hurtFlashLoopCount", hurtFeedback.flashLoopCount, "受击闪白循环次数", true],
        ["m_hurtPunchRotationDegrees", hurtFeedback.punchRotationDegrees, "受击摇晃角度", false],
        ["m_hurtPunchDurationSeconds", hurtFeedback.punchDurationSeconds, "受击摇晃秒数", false],
        ["m_hurtPunchVibrato", hurtFeedback.punchVibrato, "受击摇晃频率", true],
      ]) {
        if (tabletopCardViewSource != null) {
          assertCsharpFieldInitializerEquals(
            tabletopCardViewSource,
            fieldName,
            isInteger ? unityNumberLiteralFromCsharp(sourceLiteral) : csharpFloatLiteral(sourceLiteral),
            `卡牌视图 ${label}默认值`);
        }
        assertSerializedScalarFromCsharpLiteral(
          cardViewPrefabText,
          fieldName,
          sourceLiteral,
          "Assets/Art/Prefabs/牌桌/卡牌视图.prefab",
          `StackCraft CardInstance ${label}`);
      }
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
      fail(`卡牌视图 Prefab 仍保留卡牌内部命中 UI 残留：${obsoleteHitToken}`);
    }
  }
  for (const obsoleteSurfaceToken of [
    "m_usesLabel:",
    'm_Name: "\\u4F7F\\u7528\\u6B21\\u6570"',
  ]) {
    if (cardViewPrefabText.includes(obsoleteSurfaceToken)) {
      fail(`卡牌视图 Prefab 仍保留非 StackCraft 卡面文本残留：${obsoleteSurfaceToken}`);
    }
  }
  assertUnityComponentFieldReferences(
    cardViewPrefabYaml,
    "卡牌视图",
    "m_surfaceRenderer",
    "卡牌视图",
    23,
    "卡牌视图表面字段引用");
  assertUnityComponentFieldReferences(
    cardViewPrefabYaml,
    "卡牌视图",
    "m_surfaceMeshFilter",
    "卡牌视图",
    33,
    "卡牌视图表面字段引用");
  assertUnityComponentFieldReferences(
    cardViewPrefabYaml,
    "卡牌视图",
    "m_highlightMeshFilter",
    "候选高亮",
    33,
    "卡牌视图表面字段引用");
  if (cardMeshGuid != null) {
    assertUnityComponentReferenceEquals(
      cardViewPrefabYaml,
      "卡牌视图",
      114,
      "m_defaultSurfaceMesh",
      "3660652376195365350",
      cardMeshGuid,
      "3",
      "卡牌视图默认卡牌网格字段");
  }
  if (defaultCardSurfaceMaterialGuid != null) {
    assertUnityComponentReferenceListEquals(
      cardViewPrefabYaml,
      "卡牌视图",
      23,
      "m_Materials",
      [{ fileId: "2100000", guid: defaultCardSurfaceMaterialGuid, type: "2" }],
      "卡牌视图默认卡面材质");
  }
  if (packMeshGuid != null) {
    assertUnityComponentReferenceEquals(
      cardViewPrefabYaml,
      "卡牌视图",
      114,
      "m_packSurfaceMesh",
      "110299847060030689",
      packMeshGuid,
      "3",
      "卡牌视图卡包网格字段");
  }
  assertUnityTextObjectsUseFontReference(
    cardViewPrefabYaml,
    ["标题", "生命", "价格", "营养"],
    stackCraftTmpFontGuid,
    stackCraftTmpFontMaterialFileId,
    "卡牌视图 StackCraft 文字");
  if (cardViewPrefabText.includes('m_Name: "\\u8868\\u9762\\u8BE6\\u60C5"')) {
    fail("卡牌视图 Prefab 仍保留旧的“表面详情”混合文本节点，应改为价格 / 营养分区。");
  }
  if (cardViewPrefabText.includes("m_Color: {r: 0.25, g: 0.95, b: 0.45, a: 0.42}")) {
    fail("卡牌视图 Prefab 仍保留整块绿色候选遮罩，应改为模板式外轮廓。");
  }
  if (cardViewPrefabText.includes("m_artworkRenderer:") ||
      cardViewPrefabText.includes("m_artworkPadding:") ||
      cardViewPrefabText.includes('m_Name: "\\u5361\\u9762\\u63D2\\u753B"')) {
    fail("卡牌视图 Prefab 仍保留独立 SpriteRenderer 插画层；StackCraft 插画必须写入卡面材质 _OverlayTex。");
  }
  if (cardViewPrefabText.includes("m_artworkPadding: 0.86") || cardViewPrefabText.includes("m_artworkPadding: 0.62")) {
    fail("卡牌视图 Prefab 仍使用旧卡图占比 0.86/0.62，会挤压标题、价格、营养和生命数字。");
  }
  if (cardViewPrefabText.includes("m_Color: {r: 0.03, g: 0.05, b: 0.06, a: 0.92}")) {
    fail("卡牌视图 Prefab 仍保留旧黑色生命条，应改为 StackCraft 式右下生命数字。");
  }
  for (const obsoleteOutlineToken of [
    'm_Name: "\\u4E0A\\u8F6E\\u5ED3"',
    'm_Name: "\\u4E0B\\u8F6E\\u5ED3"',
    'm_Name: "\\u5DE6\\u8F6E\\u5ED3"',
    'm_Name: "\\u53F3\\u8F6E\\u5ED3"',
  ]) {
    if (cardViewPrefabText.includes(obsoleteOutlineToken)) {
      fail(`卡牌视图 Prefab 仍在用四条 Sprite 线框模拟高亮，应改为 StackCraft Mesh + CardOutline 材质：${obsoleteOutlineToken}`);
    }
  }
  const stackCraftHighlightSource = readIfExists("Assets/StackCraft/Scripts/Card/VFX/Highlight.cs");
  if (stackCraftHighlightSource == null) {
    fail("缺少 StackCraft Highlight.cs，无法证明候选高亮 Transform / MeshRenderer 来源语义。");
  } else {
    assertCsharpBlockContainsOrdered(
      stackCraftHighlightSource,
      "public Highlight(Transform parent, Mesh mesh, Material material)",
      [
        'new GameObject("Highlight")',
        "obj.transform.SetParent(parent)",
        "obj.transform.localPosition = Vector3.zero",
        "obj.transform.localScale = Vector3.one",
        "MeshFilter filter = obj.AddComponent<MeshFilter>()",
        "MeshRenderer renderer = obj.AddComponent<MeshRenderer>()",
        "renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off",
        "renderer.receiveShadows = false",
      ],
      "StackCraft Highlight 候选高亮来源构造语义");
  }
  assertUnityGameObjectExists(cardViewPrefabYaml, "候选高亮", "卡牌候选高亮");
  const highlightZeroVector = new Map([["x", "0"], ["y", "0"], ["z", "0"]]);
  const highlightOneVector = new Map([["x", "1"], ["y", "1"], ["z", "1"]]);
  const highlightIdentityRotation = new Map([["x", "0"], ["y", "0"], ["z", "0"], ["w", "1"]]);
  assertUnityComponentInlineNumericPropertyMatches(
    cardViewPrefabYaml,
    "候选高亮",
    4,
    "m_LocalPosition",
    highlightZeroVector,
    ["x", "y", "z"],
    "卡牌候选高亮 Transform");
  assertUnityComponentInlineNumericPropertyMatches(
    cardViewPrefabYaml,
    "候选高亮",
    4,
    "m_LocalScale",
    highlightOneVector,
    ["x", "y", "z"],
    "卡牌候选高亮 Transform");
  assertUnityComponentInlineNumericPropertyMatches(
    cardViewPrefabYaml,
    "候选高亮",
    4,
    "m_LocalRotation",
    highlightIdentityRotation,
    ["x", "y", "z", "w"],
    "卡牌候选高亮 Transform");
  assertUnityComponentInlineNumericPropertyMatches(
    cardViewPrefabYaml,
    "候选高亮",
    4,
    "m_LocalEulerAnglesHint",
    highlightZeroVector,
    ["x", "y", "z"],
    "卡牌候选高亮 Transform");
  if (cardMeshGuid != null) {
    assertUnityComponentReferenceEquals(
      cardViewPrefabYaml,
      "候选高亮",
      33,
      "m_Mesh",
      "3660652376195365350",
      cardMeshGuid,
      "3",
      "卡牌候选高亮 MeshFilter");
  }
  if (cardOutlineShaderGuid != null) {
    assertUnityComponentScalarEquals(
      cardViewPrefabYaml,
      "候选高亮",
      23,
      "m_CastShadows",
      "0",
      "卡牌候选高亮 Renderer 阴影");
    assertUnityComponentScalarEquals(
      cardViewPrefabYaml,
      "候选高亮",
      23,
      "m_ReceiveShadows",
      "0",
      "卡牌候选高亮 Renderer 接收阴影");
    assertUnityComponentReferenceListEquals(
      cardViewPrefabYaml,
      "候选高亮",
      23,
      "m_Materials",
      [{ fileId: "-876546973899608171", guid: cardOutlineShaderGuid, type: "3" }],
      "卡牌候选高亮 Renderer 材质");
  }
  assertUnityGameObjectExists(cardViewPrefabYaml, "装备面板", "卡牌视图 StackCraft 角色卡 EquipmentPanel");
  if (equipmentPanelMeshGuid != null) {
    assertUnityComponentReferenceEquals(
      cardViewPrefabYaml,
      "装备面板",
      33,
      "m_Mesh",
      "-3129460882798349415",
      equipmentPanelMeshGuid,
      "3",
      "角色装备面板 MeshFilter");
  }
  if (equipmentPanelMaterialGuid != null) {
    assertUnityComponentReferenceListEquals(
      cardViewPrefabYaml,
      "装备面板",
      23,
      "m_Materials",
      [{ fileId: "2100000", guid: equipmentPanelMaterialGuid, type: "2" }],
      "角色装备面板 Renderer");
  }
  const cardBuyerCurrencyIconMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/货币图标.mat.meta"));
  if (cardBuyerCurrencyIconMaterialGuid == null) {
    fail("Assets/Art/Materials/货币图标.mat.meta 缺少合法 GUID。");
  } else {
    assertUnityComponentReferenceListEquals(
      cardViewPrefabYaml,
      "收购货币图标",
      23,
      "m_Materials",
      [{ fileId: "2100000", guid: cardBuyerCurrencyIconMaterialGuid, type: "2" }],
      "收购货币图标 Renderer");
  }
  assertUnityGameObjectExists(cardViewPrefabYaml, "收购货币图标", "卡牌视图 StackCraft CardBuyer Icon");
  assertUnityComponentReferenceEquals(
    cardViewPrefabYaml,
    "收购货币图标",
    33,
    "m_Mesh",
    "10210",
    "0000000000000000e000000000000000",
    "0",
    "收购货币图标 MeshFilter");
}

const hitSpritePairs = [
  ["Assets/StackCraft/Sprites/Hit_Miss.png", "Assets/Art/Sprites/未命中图标.png", "未命中图标", "m_missSprite"],
  ["Assets/StackCraft/Sprites/Hit_Normal.png", "Assets/Art/Sprites/普通命中图标.png", "普通命中图标", "m_normalSprite"],
  ["Assets/StackCraft/Sprites/Hit_Critical.png", "Assets/Art/Sprites/暴击图标.png", "暴击图标", "m_criticalSprite"],
  ["Assets/StackCraft/Sprites/Effectiveness_Advantage.png", "Assets/Art/Sprites/优势图标.png", "优势图标", "m_advantageSprite"],
  ["Assets/StackCraft/Sprites/Effectiveness_Disadvantage.png", "Assets/Art/Sprites/劣势图标.png", "劣势图标", "m_disadvantageSprite"],
];
for (const [sourcePath, localPath, label] of hitSpritePairs) {
  assertSameFileHash(sourcePath, localPath, `StackCraft HitUI ${label}`);
  assertTextureImportVisualSettingsMatch(sourcePath, localPath, `StackCraft HitUI ${label}`);
}

const hitResultPrefabText = readIfExists("Assets/Art/Prefabs/牌桌/命中结果.prefab");
if (tabletopHitResultViewSource == null) {
  fail("缺少牌桌命中结果视图源码，无法证明 HitUI punch 动画由正式视图承接。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopHitResultViewSource,
    "public sealed class TabletopHitResultView",
    [
      "private Image m_hitImage;",
      "private TextMeshProUGUI m_damageLabel;",
      "private Tween m_punchTween;",
    ],
    "牌桌命中结果视图 StackCraft DOTween 依赖结构");
  assertCsharpBlockContainsOrdered(
    tabletopHitResultViewSource,
    "private void PlayPunchTween",
    [
      "DOPunchScale(new Vector3(m_punchScale, m_punchScale, 0f), m_punchDurationSeconds)",
      ".SetUpdate(true)",
      ".OnComplete(() =>",
      "m_isPlaying = false;",
      "gameObject.SetActive(false);",
    ],
    "牌桌命中结果视图用 DOTween 精确承接 StackCraft HitUI punch 参数方法");
  for (const obsoleteToken of [
    "m_elapsedSeconds",
    "Mathf.Sin(",
    "normalizedTime * Mathf.PI",
    "transform.localScale = m_baseScale + new Vector3(punch",
  ]) {
    if (tabletopHitResultViewSource.includes(obsoleteToken)) {
      fail(`牌桌命中结果视图仍保留手写 punch 近似算法，应使用 StackCraft DOPunchScale：${obsoleteToken}`);
    }
  }
}
if (hitResultPrefabText == null) {
  fail("缺少命中结果 Prefab，无法证明 参考模板命中结果 UI 静态承载。");
} else {
  const hitResultPrefabYaml = unityYamlObjects(hitResultPrefabText);
  if (stackCraftHitUiPrefabText == null) {
    fail("缺少 StackCraft HitUI Prefab，无法做命中结果对象级对账：Assets/StackCraft/Prefabs/UI/HitUI.prefab");
  } else {
    const stackCraftHitUiYaml = unityYamlObjects(stackCraftHitUiPrefabText);
    if (tabletopWorldSpaceRotation != null) {
      assertUnityComponentInlineNumericPropertyMatches(
        hitResultPrefabYaml,
        "命中结果",
        224,
        "m_LocalRotation",
        tabletopWorldSpaceRotation,
        ["x", "y", "z", "w"],
        "命中结果根 RectTransform");
    }
    assertUnityImageObjectMatchesSource(
      stackCraftHitUiYaml,
      "HitUI",
      hitResultPrefabYaml,
      "命中结果",
      "命中结果根图标");
    assertUnityTextObjectMatchesSource(
      stackCraftHitUiYaml,
      "DamageLabel",
      hitResultPrefabYaml,
      "DamageLabel",
      "命中结果伤害数字");
    assertUnityImageObjectMatchesSource(
      stackCraftHitUiYaml,
      "Effectiveness",
      hitResultPrefabYaml,
      "Effectiveness",
      "命中结果克制图标");
    assertUnityComponentFieldReferences(
      hitResultPrefabYaml,
      "命中结果",
      "m_hitImage",
      "命中结果",
      114,
      "命中结果视图字段引用");
    assertUnityComponentFieldReferences(
      hitResultPrefabYaml,
      "命中结果",
      "m_effectivenessImage",
      "Effectiveness",
      114,
      "命中结果视图字段引用");
    assertUnityComponentFieldReferences(
      hitResultPrefabYaml,
      "命中结果",
      "m_damageLabel",
      "DamageLabel",
      114,
      "命中结果视图字段引用");
  }
  if (stackCraftHitUiSource == null) {
    fail("缺少 StackCraft HitUI 源码，无法从 DOPunchScale 调用派生命中结果弹跳参数：Assets/StackCraft/Scripts/Combat/UI/HitUI.cs");
  } else {
    const hitUiPunch = csharpDOPunchScaleParameters(stackCraftHitUiSource, "StackCraft HitUI");
    if (hitUiPunch != null) {
      if (tabletopHitResultViewSource != null) {
        assertCsharpFieldInitializerEquals(
          tabletopHitResultViewSource,
          "m_punchScale",
          csharpFloatLiteral(hitUiPunch.scale),
          "命中结果弹跳幅度默认值");
        assertCsharpFieldInitializerEquals(
          tabletopHitResultViewSource,
          "m_punchDurationSeconds",
          csharpFloatLiteral(hitUiPunch.duration),
          "命中结果弹跳秒数默认值");
      }
      assertSerializedScalarFromCsharpLiteral(
        hitResultPrefabText,
        "m_punchScale",
        hitUiPunch.scale,
        "Assets/Art/Prefabs/牌桌/命中结果.prefab",
        "StackCraft HitUI 弹跳幅度");
      assertSerializedScalarFromCsharpLiteral(
        hitResultPrefabText,
        "m_punchDurationSeconds",
        hitUiPunch.duration,
        "Assets/Art/Prefabs/牌桌/命中结果.prefab",
        "StackCraft HitUI 弹跳秒数");
    }
  }
  assertUnityTextObjectsUseFontReference(
    hitResultPrefabYaml,
    ["DamageLabel"],
    stackCraftTmpFontGuid,
    stackCraftTmpFontMaterialFileId,
    "命中结果 StackCraft 文字");
  for (const [_sourcePath, localPath, label, fieldName] of hitSpritePairs) {
    const localSpriteGuid = unityGuid(readIfExists(`${localPath}.meta`));
    if (localSpriteGuid == null) {
      fail(`${localPath}.meta 缺少合法 GUID，无法静态验证 HitUI ${label} 引用。`);
    } else {
      assertUnityMonoBehaviourFieldReferencesGuid(
        hitResultPrefabYaml,
        "命中结果",
        fieldName,
        "21300000",
        localSpriteGuid,
        "3",
        `命中结果 Prefab 的 StackCraft HitUI ${label}字段`);
    }
  }
}

assertSameFileHash(
  "Assets/StackCraft/Sprites/Square.png",
  "Assets/Art/Sprites/StackCraft/Square.png",
  "StackCraft Square 进度条图片");
assertTextureImportVisualSettingsMatch(
  "Assets/StackCraft/Sprites/Square.png",
  "Assets/Art/Sprites/StackCraft/Square.png",
  "StackCraft Square 进度条图片");
assertSameFileHash(
  "Assets/StackCraft/Sprites/Square.png",
  "Assets/Art/Sprites/卡牌占位图.png",
  "StackCraft Square 测试卡牌占位图");
assertTextureImportVisualSettingsMatch(
  "Assets/StackCraft/Sprites/Square.png",
  "Assets/Art/Sprites/卡牌占位图.png",
  "StackCraft Square 测试卡牌占位图");
const tabletopPlaceholderCardArtGuid = unityGuid(readIfExists("Assets/Art/Sprites/卡牌占位图.png.meta"));
if (tabletopPlaceholderCardArtGuid == null) {
  fail("Assets/Art/Sprites/卡牌占位图.png.meta 缺少合法 GUID。");
}
if (collectorSettingText != null &&
  collectorSettingEntryBlock(collectorSettingText, "Assets/Art/Sprites/卡牌占位图.png") == null) {
  fail("YooAsset 收集配置缺少 StackCraft Square 测试卡牌占位图路径：Assets/Art/Sprites/卡牌占位图.png。");
} else if (collectorSettingText != null) {
  assertCollectorSettingEntry(
    collectorSettingText,
    "Assets/Art/Sprites/卡牌占位图.png",
    {
      AddressRuleName: "AddressByFileName",
      PackRuleName: "PackDirectory",
      FilterRuleName: "CollectAll",
      AssetTags: "test",
    },
    "StackCraft Square 测试卡牌占位图收集器");
}
if (foundationSceneMenuSource == null) {
  fail("缺少 FoundationTestSceneMenu，无法证明测试卡牌占位图由正式测试场景生成器消费。");
} else {
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "public static partial class FoundationTestSceneMenu",
    [
      'private const string TabletopCardArtPath = GameplaySpriteFolder + "/卡牌占位图.png";',
      'private const string TabletopCardArtAddress = "卡牌占位图";',
    ],
    "FoundationTestSceneMenu StackCraft Square 占位图作者源常量");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static TabletopViewSettings EnsureTabletopTestAssets()",
    [
      "Sprite cardSprite = LoadRequiredSprite(TabletopCardArtPath, \"项目卡牌占位图\")",
      "EnsureTabletopCardViewPrefab(cardSprite);",
      "EnsureTabletopBattleAreaViewPrefab(cardSprite);",
    ],
    "FoundationTestSceneMenu StackCraft Square 占位图消费入口");
}

const hudStatsSpritePairs = [
  ["Assets/StackCraft/Sprites/Stats_Nutrition.png", "Assets/Art/Sprites/StackCraft/Stats_Nutrition.png", "营养统计图标", "m_nutritionIcon"],
  ["Assets/StackCraft/Sprites/Stats_Currency.png", "Assets/Art/Sprites/StackCraft/Stats_Currency.png", "货币统计图标", "m_currencyIcon"],
  ["Assets/StackCraft/Sprites/Stats_Card.png", "Assets/Art/Sprites/StackCraft/Stats_Card.png", "卡牌容量统计图标", "m_cardCountIcon"],
];
for (const [sourcePath, localPath, label] of hudStatsSpritePairs) {
  assertSameFileHash(sourcePath, localPath, `StackCraft HUD ${label}`);
  assertTextureImportVisualSettingsMatch(sourcePath, localPath, `StackCraft HUD ${label}`);
}

const hudTimePaceSpritePairs = [
  ["Assets/StackCraft/Sprites/TimePace_0.png", "Assets/Art/Sprites/StackCraft/TimePace_0.png", "暂停速度图标"],
  ["Assets/StackCraft/Sprites/TimePace_1.png", "Assets/Art/Sprites/StackCraft/TimePace_1.png", "普通速度图标"],
  ["Assets/StackCraft/Sprites/TimePace_2.png", "Assets/Art/Sprites/StackCraft/TimePace_2.png", "加速速度图标"],
];
for (const [sourcePath, localPath, label] of hudTimePaceSpritePairs) {
  assertSameFileHash(sourcePath, localPath, `StackCraft HUD ${label}`);
  assertTextureImportVisualSettingsMatch(sourcePath, localPath, `StackCraft HUD ${label}`);
}

if (scenarioTurnPanelSource == null) {
  fail("缺少剧本回合 HUD 源码，无法证明 CardStatsUI 统计图标由正式 HUD 承接。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioTurnPanelSource,
    "public sealed class ScenarioTurnPanel : UIPanel",
    [
      "private Image m_paceImage;",
      "private Sprite[] m_paceIcons;",
      "private CanvasGroup m_dayTimeGroup;",
      "private CanvasGroup m_cardStatsGroup;",
      "private Image m_nutritionIcon;",
      "private TMP_Text m_nutritionLabel;",
      "private Image m_currencyIcon;",
      "private TMP_Text m_currencyLabel;",
      "private Image m_cardCountIcon;",
      "private TMP_Text m_cardCountLabel;",
      "public ScenarioTimePace DisplayedTimePace",
    ],
    "剧本回合 HUD StackCraft CardStatsUI 图标和数字字段");
  assertCsharpBlockContainsOrdered(
    scenarioTurnPanelSource,
    "private void ConfirmTurn()",
    [
      "if (director.ActiveRun.ProgressionMode == ActionProgressionMode.RealTime)",
      "director.ActiveRun.CycleTimePace();",
      "Refresh();",
      "return;",
      "director.ConfirmTurn();",
    ],
    "剧本回合 HUD 在实时模式下复用 StackCraft DayTimeUI 点击切换速度语义");
  assertCsharpBlockContainsOrdered(
    scenarioTurnPanelSource,
    "private void Refresh()",
    [
      "ScenarioTabletopStats stats = run.GetTabletopStats();",
      "DisplayedTotalFoodNutrition = stats.TotalFoodNutrition;",
      "DisplayedNutritionNeed = stats.NutritionNeed;",
      "DisplayedCurrency = stats.Currency;",
      "DisplayedCardsOwned = stats.CardsOwned;",
      "DisplayedCardLimit = stats.CardLimit;",
      "DisplayedTimePace = GetDisplayedTimePace(run);",
      "RefreshStatsLabels(stats);",
      "m_paceImage.sprite = m_paceIcons[(int)DisplayedTimePace];",
      "bool isDayHudVisible = run.DayCyclePhase == ScenarioDayCyclePhase.Inactive;",
      "SetHudGroupVisible(m_dayTimeGroup, isDayHudVisible);",
      "SetHudGroupVisible(m_cardStatsGroup, isDayHudVisible);",
    ],
    "剧本回合 HUD StackCraft CardStatsUI 统计刷新入口");
  assertCsharpBlockContainsOrdered(
    scenarioTurnPanelSource,
    "private static void SetHudGroupVisible",
    [
      "group.alpha = isVisible ? 1f : 0f;",
      "group.blocksRaycasts = isVisible;",
    ],
    "剧本回合 HUD 承接 StackCraft 日终 HUD CanvasGroup 显隐输入语义");
  assertCsharpBlockExcludes(
    scenarioTurnPanelSource,
    "private static void SetHudGroupVisible",
    [
      "interactable",
    ],
    "剧本回合 HUD 日终显隐只复刻 StackCraft alpha / blocksRaycasts 行为");
  assertCsharpBlockContainsOrdered(
    scenarioTurnPanelSource,
    "private void RefreshStatsLabels",
    [
      "m_nutritionLabel.text = $\"{stats.TotalFoodNutrition}/{stats.NutritionNeed}\";",
      "m_currencyLabel.text = $\"{stats.Currency}\";",
      "m_cardCountLabel.text = $\"{stats.CardsOwned}/{stats.CardLimit}\";",
    ],
    "剧本回合 HUD StackCraft CardStatsUI 数字文本格式");
  if (scenarioTurnPanelSource.includes("食物 {stats.TotalFoodNutrition}") ||
    scenarioTurnPanelSource.includes("货币 {stats.Currency}") ||
    scenarioTurnPanelSource.includes("卡牌 {stats.CardsOwned}")) {
    fail("剧本回合 HUD 仍把 CardStatsUI 统计混在中文句子里，不能复刻 StackCraft 图标 + 数字统计。");
  }
}

if (foundationSceneMenuSource == null) {
  fail("缺少 FoundationTestSceneMenu，无法证明剧本回合 HUD Prefab 会稳定重建 StackCraft 统计图标。");
} else {
  assertCsharpFieldInitializerEquals(
    foundationSceneMenuSource,
    "StackCraftStatsNutritionSpritePath",
    'StackCraftSpriteFolder + "/Stats_Nutrition.png"',
    "FoundationTestSceneMenu StackCraft CardStatsUI 营养图标资源常量");
  assertCsharpFieldInitializerEquals(
    foundationSceneMenuSource,
    "StackCraftStatsCurrencySpritePath",
    'StackCraftSpriteFolder + "/Stats_Currency.png"',
    "FoundationTestSceneMenu StackCraft CardStatsUI 货币图标资源常量");
  assertCsharpFieldInitializerEquals(
    foundationSceneMenuSource,
    "StackCraftStatsCardSpritePath",
    'StackCraftSpriteFolder + "/Stats_Card.png"',
    "FoundationTestSceneMenu StackCraft CardStatsUI 图标资源常量");
  assertCsharpFieldInitializerEquals(
    foundationSceneMenuSource,
    "StackCraftTimePacePausedSpritePath",
    'StackCraftSpriteFolder + "/TimePace_0.png"',
    "FoundationTestSceneMenu StackCraft DayTimeUI 暂停速度图标资源常量");
  assertCsharpFieldInitializerEquals(
    foundationSceneMenuSource,
    "StackCraftTimePaceNormalSpritePath",
    'StackCraftSpriteFolder + "/TimePace_1.png"',
    "FoundationTestSceneMenu StackCraft DayTimeUI 普通速度图标资源常量");
  assertCsharpFieldInitializerEquals(
    foundationSceneMenuSource,
    "StackCraftTimePaceFastSpritePath",
    'StackCraftSpriteFolder + "/TimePace_2.png"',
    "FoundationTestSceneMenu StackCraft DayTimeUI 加速速度图标资源常量");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureScenarioTurnPanelPrefab()",
    [
      "TMP_FontAsset statsFontAsset = LoadStackCraftSurfaceFont();",
      "Sprite squareSprite = LoadRequiredSprite(",
      "StackCraftSquareSpritePath",
      "Sprite[] paceSprites =",
      "StackCraftTimePacePausedSpritePath",
      "StackCraftTimePaceNormalSpritePath",
      "StackCraftTimePaceFastSpritePath",
      "Sprite nutritionStatsSprite = LoadRequiredSprite(",
      "StackCraftStatsNutritionSpritePath",
      "Sprite currencyStatsSprite = LoadRequiredSprite(",
      "StackCraftStatsCurrencySpritePath",
      "Sprite cardStatsSprite = LoadRequiredSprite(",
      "StackCraftStatsCardSpritePath",
      "\"ConfirmTurn\"",
      "typeof(Button)",
      "typeof(CanvasGroup)",
      "typeof(UINavigationTarget)",
      "\"TimeProgress\"",
      "progressFill.sprite = squareSprite;",
      "\"DayText\"",
      "\"第 N 天\"",
      "\"PaceImage\"",
      "paceImage.sprite = paceSprites[1];",
      "\"CardStatsUI\"",
      "typeof(HorizontalLayoutGroup)",
      "typeof(ContentSizeFitter)",
      "typeof(CanvasGroup)",
      "CreateStatsIcon(",
      "\"NutritionIcon\"",
      "CreateStatsLabel(",
      "\"NutritionLabel\"",
      "CreateStatsIcon(",
      "\"CurrencyIcon\"",
      "CreateStatsLabel(",
      "\"CurrencyLabel\"",
      "CreateStatsIcon(",
      "\"CardIcon\"",
      "CreateStatsLabel(",
      "\"CardLabel\"",
      "\"Watermark\"",
      "\"模板玩法演示\"",
      "SetAnchoredRect(",
      "watermark.rectTransform",
      "new Vector2(600f, 40f)",
      "VerticalAlignmentOptions.Top",
      "RequireProperty(serializedPanel, \"m_turnLabel\")",
      "RequireProperty(serializedPanel, \"m_dayProgressFill\")",
      "RequireProperty(serializedPanel, \"m_paceImage\")",
      "RequireProperty(serializedPanel, \"m_paceIcons\")",
      "RequireProperty(serializedPanel, \"m_dayTimeGroup\")",
      "RequireProperty(serializedPanel, \"m_cardStatsGroup\")",
      "RequireProperty(serializedPanel, \"m_nutritionIcon\")",
      "RequireProperty(serializedPanel, \"m_currencyIcon\")",
      "RequireProperty(serializedPanel, \"m_cardCountIcon\")",
      "RequireProperty(serializedPanel, \"m_confirmTurnButton\")",
      "RequireProperty(serializedPanel, \"m_progressionModeButton\").objectReferenceValue = null;",
    ],
    "FoundationTestSceneMenu StackCraft DayTimeUI / CardStatsUI HUD Prefab 生成闭包");
  assertCsharpBlockExcludes(
    foundationSceneMenuSource,
    "private static void EnsureScenarioTurnPanelPrefab()",
    [
      "\"TurnControl\"",
      "\"DayProgressBackground\"",
      "\"DayProgressFill\"",
      "\"ProgressionMode\"",
      "\"推进回合\"",
      "\"开启即时\"",
    ],
    "FoundationTestSceneMenu 剧本回合 HUD 不得再生成底部控制条");
}

const scenarioTurnPanelPrefabText = readIfExists("Assets/Art/Prefabs/UI/ScenarioTurnPanel.prefab");
if (scenarioTurnPanelPrefabText == null) {
  fail("缺少 ScenarioTurnPanel Prefab，无法证明 HUD 统计图标已写入作者源。");
} else {
  const scenarioTurnPanelPrefabYaml = unityYamlObjects(scenarioTurnPanelPrefabText);
  if (stackCraftUiRootPrefabText == null) {
    fail("缺少 StackCraft UIRoot Prefab，无法做 CardStatsUI 对象级对账：Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
  } else {
    const stackCraftUiRootYaml = unityYamlObjects(stackCraftUiRootPrefabText);
    for (const staleObjectName of [
      "TurnControl",
      "DayProgressBackground",
      "DayProgressFill",
      "ProgressionMode",
    ]) {
      if (unityGameObjectByName(scenarioTurnPanelPrefabYaml, staleObjectName) != null) {
        fail(`ScenarioTurnPanel Prefab 仍包含旧底部 HUD 对象：${staleObjectName}。`);
      }
    }
    assertUnityGameObjectExists(
      scenarioTurnPanelPrefabYaml,
      "ConfirmTurn",
      "HUD 左上 DayTimeUI 点击区域");
    assertUnityComponentPropertiesMatch(
      stackCraftUiRootYaml,
      "DayTimeUI",
      scenarioTurnPanelPrefabYaml,
      "ConfirmTurn",
      224,
      [
        "m_LocalPosition",
        "m_LocalScale",
        "m_AnchorMin",
        "m_AnchorMax",
        "m_AnchoredPosition",
        "m_SizeDelta",
        "m_Pivot",
      ],
      "HUD 左上 DayTimeUI RectTransform");
    assertUnityComponentPropertiesMatch(
      stackCraftUiRootYaml,
      "DayTimeUI",
      scenarioTurnPanelPrefabYaml,
      "ConfirmTurn",
      114,
      [
        "m_Color",
        "m_RaycastTarget",
        "m_Maskable",
        "m_Type",
        "m_PreserveAspect",
        "m_FillCenter",
        "m_FillMethod",
        "m_FillAmount",
        "m_FillClockwise",
        "m_FillOrigin",
        "m_UseSpriteMesh",
        "m_PixelsPerUnitMultiplier",
      ],
      "HUD 左上 DayTimeUI 背景");
    assertUnityComponentPropertiesMatch(
      stackCraftUiRootYaml,
      "DayTimeUI",
      scenarioTurnPanelPrefabYaml,
      "ConfirmTurn",
      225,
      ["m_Alpha", "m_Interactable", "m_BlocksRaycasts", "m_IgnoreParentGroups"],
      "HUD 左上 DayTimeUI 显隐输入状态");
    assertUnityMonoBehaviourPropertyExists(
      scenarioTurnPanelPrefabYaml,
      "ConfirmTurn",
      "GameCore::GameCore.UINavigationTarget",
      "m_submitSoundOverride",
      "HUD 左上 DayTimeUI 点击音效目标");
    assertUnityImageObjectMatchesSource(
      stackCraftUiRootYaml,
      "TimeProgress",
      scenarioTurnPanelPrefabYaml,
      "TimeProgress",
      "HUD 时间进度条");
    assertUnityComponentPropertiesMatch(
      stackCraftUiRootYaml,
      "TimeProgress",
      scenarioTurnPanelPrefabYaml,
      "TimeProgress",
      224,
      ["m_AnchorMin", "m_AnchorMax"],
      "HUD 时间进度条拉伸锚点");
    assertUnityTextObjectMatchesSource(
      stackCraftUiRootYaml,
      "DayText",
      scenarioTurnPanelPrefabYaml,
      "DayText",
      "HUD 天数文本");
    assertUnityComponentPropertiesMatch(
      stackCraftUiRootYaml,
      "DayText",
      scenarioTurnPanelPrefabYaml,
      "DayText",
      224,
      ["m_AnchorMin", "m_AnchorMax"],
      "HUD 天数文本拉伸锚点");
    assertUnityImageObjectMatchesSource(
      stackCraftUiRootYaml,
      "PaceImage",
      scenarioTurnPanelPrefabYaml,
      "PaceImage",
      "HUD 时间速度图标");
    assertUnityComponentPropertiesMatch(
      stackCraftUiRootYaml,
      "PaceImage",
      scenarioTurnPanelPrefabYaml,
      "PaceImage",
      224,
      ["m_AnchorMin", "m_AnchorMax"],
      "HUD 时间速度图标锚点");

    const progressSquareGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/Square.png.meta"));
    if (progressSquareGuid == null) {
      fail("Assets/Art/Sprites/StackCraft/Square.png.meta 缺少合法 GUID，无法静态验证 HUD 时间进度条引用。");
    } else {
      assertUnityComponentReferenceEquals(
        scenarioTurnPanelPrefabYaml,
        "TimeProgress",
        114,
        "m_Sprite",
        "21300000",
        progressSquareGuid,
        "3",
        "ScenarioTurnPanel HUD 时间进度条 Image");
    }
    const normalPaceGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/TimePace_1.png.meta"));
    if (normalPaceGuid == null) {
      fail("Assets/Art/Sprites/StackCraft/TimePace_1.png.meta 缺少合法 GUID，无法静态验证 HUD 普通速度图标引用。");
    } else {
      assertUnityComponentReferenceEquals(
        scenarioTurnPanelPrefabYaml,
        "PaceImage",
        114,
        "m_Sprite",
        "21300000",
        normalPaceGuid,
        "3",
      "ScenarioTurnPanel HUD 普通速度图标 Image");
    }
    const pausedPaceGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/TimePace_0.png.meta"));
    const fastPaceGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/TimePace_2.png.meta"));
    assertUnityComponentReferenceListEquals(
      scenarioTurnPanelPrefabYaml,
      "ScenarioTurnPanel",
      114,
      "m_paceIcons",
      [
        { fileId: "21300000", guid: pausedPaceGuid, type: "3" },
        { fileId: "21300000", guid: normalPaceGuid, type: "3" },
        { fileId: "21300000", guid: fastPaceGuid, type: "3" },
      ],
      "ScenarioTurnPanel HUD 三档速度图标列表");
    assertUnityComponentFieldReferences(
      scenarioTurnPanelPrefabYaml,
      "ScenarioTurnPanel",
      "m_turnLabel",
      "DayText",
      114,
      "ScenarioTurnPanel HUD 天数文本");
    assertUnityComponentFieldReferences(
      scenarioTurnPanelPrefabYaml,
      "ScenarioTurnPanel",
      "m_dayProgressFill",
      "TimeProgress",
      114,
      "ScenarioTurnPanel HUD 时间进度条");
    assertUnityComponentFieldReferences(
      scenarioTurnPanelPrefabYaml,
      "ScenarioTurnPanel",
      "m_paceImage",
      "PaceImage",
      114,
      "ScenarioTurnPanel HUD 时间速度图标");
    assertUnityComponentFieldReferences(
      scenarioTurnPanelPrefabYaml,
      "ScenarioTurnPanel",
      "m_dayTimeGroup",
      "ConfirmTurn",
      225,
      "ScenarioTurnPanel HUD DayTimeUI 显隐组");
    assertUnityComponentFieldReferences(
      scenarioTurnPanelPrefabYaml,
      "ScenarioTurnPanel",
      "m_cardStatsGroup",
      "CardStatsUI",
      225,
      "ScenarioTurnPanel HUD CardStatsUI 显隐组");
    assertUnityTextObjectMatchesSource(
      stackCraftUiRootYaml,
      "Watermark",
      scenarioTurnPanelPrefabYaml,
      "Watermark",
      "HUD 底部 StackCraft 演示版权文字");
    assertUnityComponentPropertiesMatch(
      stackCraftUiRootYaml,
      "Watermark",
      scenarioTurnPanelPrefabYaml,
      "Watermark",
      224,
      ["m_AnchorMin", "m_AnchorMax"],
      "HUD 底部 StackCraft 演示版权文字锚点");

    assertUnityGameObjectExists(
      scenarioTurnPanelPrefabYaml,
      "CardStatsUI",
      "HUD 统计容器");
    assertUnityComponentPropertiesMatch(
      stackCraftUiRootYaml,
      "CardStatsUI",
      scenarioTurnPanelPrefabYaml,
      "CardStatsUI",
      114,
      [
        "m_Color",
        "m_RaycastTarget",
        "m_Maskable",
        "m_Type",
        "m_PreserveAspect",
        "m_FillCenter",
        "m_FillMethod",
        "m_FillAmount",
        "m_FillClockwise",
        "m_FillOrigin",
        "m_UseSpriteMesh",
        "m_PixelsPerUnitMultiplier",
      ],
      "HUD 统计容器背景");
    assertUnityMonoBehaviourPropertiesMatchBySourceProperty(
      stackCraftUiRootYaml,
      "CardStatsUI",
      "m_Padding",
      scenarioTurnPanelPrefabYaml,
      "CardStatsUI",
      [
        "m_Padding",
        "m_ChildAlignment",
        "m_Spacing",
        "m_ChildForceExpandWidth",
        "m_ChildForceExpandHeight",
        "m_ChildControlWidth",
        "m_ChildControlHeight",
        "m_ChildScaleWidth",
        "m_ChildScaleHeight",
        "m_ReverseArrangement",
      ],
      "HUD 统计容器横向布局");
    assertUnityMonoBehaviourPropertiesMatchBySourceProperty(
      stackCraftUiRootYaml,
      "CardStatsUI",
      "m_HorizontalFit",
      scenarioTurnPanelPrefabYaml,
      "CardStatsUI",
      ["m_HorizontalFit", "m_VerticalFit"],
      "HUD 统计容器尺寸适配");
    assertUnityComponentPropertiesMatch(
      stackCraftUiRootYaml,
      "CardStatsUI",
      scenarioTurnPanelPrefabYaml,
      "CardStatsUI",
      225,
      ["m_Alpha", "m_Interactable", "m_BlocksRaycasts", "m_IgnoreParentGroups"],
      "HUD 统计容器显隐输入状态");
    for (const [sourceObjectName, targetObjectName, label] of [
      ["NutritionIcon", "NutritionIcon", "营养统计图标"],
      ["CurrencyIcon", "CurrencyIcon", "货币统计图标"],
      ["CardIcon", "CardIcon", "卡牌容量统计图标"],
    ]) {
      assertUnityImageObjectMatchesSource(
        stackCraftUiRootYaml,
        sourceObjectName,
        scenarioTurnPanelPrefabYaml,
        targetObjectName,
        `HUD ${label}`);
    }
    for (const [sourceObjectName, targetObjectName, label] of [
      ["NutritionLabel", "NutritionLabel", "营养统计数字"],
      ["CurrencyLabel", "CurrencyLabel", "货币统计数字"],
      ["CardLabel", "CardLabel", "卡牌容量统计数字"],
    ]) {
      assertUnityTextObjectMatchesSource(
        stackCraftUiRootYaml,
        sourceObjectName,
        scenarioTurnPanelPrefabYaml,
        targetObjectName,
        `HUD ${label}`);
    }
    for (const [fieldName, targetObjectName, label] of [
      ["m_nutritionIcon", "NutritionIcon", "营养统计图标"],
      ["m_nutritionLabel", "NutritionLabel", "营养统计数字"],
      ["m_currencyIcon", "CurrencyIcon", "货币统计图标"],
      ["m_currencyLabel", "CurrencyLabel", "货币统计数字"],
      ["m_cardCountIcon", "CardIcon", "卡牌容量统计图标"],
      ["m_cardCountLabel", "CardLabel", "卡牌容量统计数字"],
    ]) {
      assertUnityComponentFieldReferences(
        scenarioTurnPanelPrefabYaml,
        "ScenarioTurnPanel",
        fieldName,
        targetObjectName,
        114,
        `ScenarioTurnPanel HUD ${label}`);
    }
  }
  for (const [_sourcePath, localPath, label, fieldName] of hudStatsSpritePairs) {
    const localSpriteGuid = unityGuid(readIfExists(`${localPath}.meta`));
    if (localSpriteGuid == null) {
      fail(`${localPath}.meta 缺少合法 GUID，无法静态验证 HUD ${label} 引用。`);
      continue;
    }

    const targetObjectNameByField = {
      m_nutritionIcon: "NutritionIcon",
      m_currencyIcon: "CurrencyIcon",
      m_cardCountIcon: "CardIcon",
    };
    const targetObjectName = targetObjectNameByField[fieldName];
    if (targetObjectName == null) {
      fail(`ScenarioTurnPanel Prefab 的 HUD 字段 ${fieldName} 缺少对象级对账映射。`);
    } else {
      assertUnityComponentReferenceEquals(
        scenarioTurnPanelPrefabYaml,
        targetObjectName,
        114,
        "m_Sprite",
        "21300000",
        localSpriteGuid,
        "3",
        `ScenarioTurnPanel HUD ${label} Image`);
    }
  }
}

if (scenarioPauseInputSource == null) {
  fail("缺少剧本暂停输入源码，无法证明 StackCraft PauseMenu 的 Cancel 输入由正式输入系统承接。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioPauseInputSource,
    "public sealed class ScenarioPauseInput : MonoBehaviour",
    [
      "inputSystem.AddGameplayActionListener(",
      "EGameplayInputAction.OpenGameMenu",
      "EInputActionPhase.Performed",
      "OnOpenGameMenu);",
      "GameManager.InputSystem.RemoveGameplayActionListener(",
      "EGameplayInputAction.OpenGameMenu",
      "EInputActionPhase.Performed",
      "OnOpenGameMenu);",
    ],
    "剧本暂停输入承接 StackCraft CancelWasPressedThisFrame 的正式输入订阅");
  assertCsharpBlockContainsOrdered(
    scenarioPauseInputSource,
    "private void OnOpenGameMenu(InputAction.CallbackContext context)",
    [
      "GameManager.InputSystem.IsGameplayActionBlocked(EGameplayInputAction.OpenGameMenu)",
      "!GameManager.TryGetSystem(out ScenarioDirector director)",
      "!director.HasActiveScenario",
      "director.IsChangingScenario",
      "_ = OpenPauseMenuAsync();",
    ],
    "剧本暂停输入按正式剧本状态打开暂停菜单");
  assertCsharpBlockContainsOrdered(
    scenarioPauseInputSource,
    "private async Task OpenPauseMenuAsync()",
    [
      "await GameManager.UISystem.OpenMenuAsync(EMenu.Pause);",
      "Debug.LogException(new InvalidOperationException(\"打开剧本暂停菜单失败。\", exception), this);",
    ],
    "剧本暂停输入通过正式 UIKit 菜单栈打开暂停菜单");
  for (const obsoleteToken of [
    "StackCraftInput",
    "DayCycleManager",
    "TimeManager",
    "CryingSnow.StackCraft",
    "typeof(PauseMenu)",
    "new PauseMenu",
    "Input.Get",
  ]) {
    if (scenarioPauseInputSource.includes(obsoleteToken)) {
      fail(`剧本暂停输入仍保留 StackCraft 旧暂停入口残留：${obsoleteToken}`);
    }
  }
}

if (scenarioPausePanelSource == null) {
  fail("缺少剧本暂停菜单源码，无法证明 StackCraft PauseMenu 按正式菜单栈承接。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioPausePanelSource,
    "public sealed class ScenarioPausePanel : UIKitMenuPanelBase",
    [
      "private Button m_continueButton;",
      "private Button m_settingsButton;",
      "private Button m_saveAndExitButton;",
      "m_continueButton.onClick.AddListener(ContinueScenario);",
      "m_settingsButton.onClick.AddListener(OpenSettings);",
      "m_saveAndExitButton.onClick.AddListener(SaveAndExit);",
    ],
    "剧本暂停菜单承接 StackCraft Continue / Options / Title 三按钮职责");
  assertCsharpBlockContainsOrdered(
    scenarioPausePanelSource,
    "private void ContinueScenario()",
    [
      "CloseFromMenuStackOrSelf();",
    ],
    "剧本暂停菜单 Continue 通过正式菜单栈恢复");
  assertCsharpBlockContainsOrdered(
    scenarioPausePanelSource,
    "private void OpenSettings()",
    [
      "RunPanelTaskAndReport(GameManager.UISystem.OpenMenuAsync(EMenu.Settings), \"打开设置菜单\");",
    ],
    "剧本暂停菜单 Options 通过正式菜单栈打开设置");
  assertCsharpBlockContainsOrdered(
    scenarioPausePanelSource,
    "private async Task SaveAndExitAsync()",
    [
      "ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();",
      "director.ActiveSaveSlotId",
      "SetControlsInteractable(false);",
      "director.SaveActiveRunToSlot(slotId)",
      "await director.EndScenarioAsync();",
      "GameManager.UISystem.CloseAllMenus();",
    ],
    "剧本暂停菜单保存并返回标题走正式剧本导演和菜单栈");
  for (const obsoleteToken of [
    "GameDirector.Instance",
    "BackToTitle",
    "TimeManager",
    "SetExternalPause",
    "CryingSnow.StackCraft",
    "typeof(PauseMenu)",
    "new PauseMenu",
  ]) {
    if (scenarioPausePanelSource.includes(obsoleteToken)) {
      fail(`剧本暂停菜单仍保留 StackCraft 旧暂停链路残留：${obsoleteToken}`);
    }
  }
}

const scenarioPausePanelPrefabText = readIfExists("Assets/Art/Prefabs/UI/ScenarioPausePanel.prefab");
if (scenarioPausePanelPrefabText == null) {
  fail("缺少 ScenarioPausePanel Prefab，无法证明暂停菜单按钮绑定已写入作者源。");
} else {
  const scenarioPausePanelPrefabYaml = unityYamlObjects(scenarioPausePanelPrefabText);
  assertUnityGameObjectExists(
    scenarioPausePanelPrefabYaml,
    "ScenarioPausePanel",
    "剧本暂停菜单 Prefab 根");
  for (const objectName of ["Continue", "Settings", "SaveAndExit"]) {
    assertUnityGameObjectExists(
      scenarioPausePanelPrefabYaml,
      objectName,
      `剧本暂停菜单 ${objectName} 按钮`);
    assertUnityMonoBehaviourPropertyExists(
      scenarioPausePanelPrefabYaml,
      objectName,
      "GameCore::GameCore.UINavigationTarget",
      "m_submitSoundOverride",
      `剧本暂停菜单 ${objectName} 按钮点击音效目标`);
  }
  for (const [fieldName, targetObjectName, label] of [
    ["m_continueButton", "Continue", "继续按钮"],
    ["m_settingsButton", "Settings", "设置按钮"],
    ["m_saveAndExitButton", "SaveAndExit", "保存并退出按钮"],
  ]) {
    assertUnityMonoBehaviourFieldReferences(
      scenarioPausePanelPrefabYaml,
      "ScenarioPausePanel",
      fieldName,
      targetObjectName,
      "UnityEngine.UI::UnityEngine.UI.Button",
      `ScenarioPausePanel ${label}字段`);
  }
}

if (scenarioJournalPanelSource == null) {
  fail("缺少剧本日志 HUD 源码，无法证明 StackCraft Quests / Recipes 常驻面板由正式 UI 承接。");
} else {
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "public sealed class ScenarioJournalPanel : UIPanel",
    [
      "private const string QuestTabText = \"任务\";",
      "private const string ActionTabText = \"配方\";",
      "private const string MenuOpenLabel = \">>\";",
      "private const string MenuClosedLabel = \"<<\";",
      "private const float MenuSlideSeconds = 0.5f;",
      "private RectTransform m_menuPanel;",
      "private Toggle m_questsTabToggle;",
      "private Toggle m_actionsTabToggle;",
      "private Button m_menuToggleButton;",
      "private TMP_Text m_menuToggleLabel;",
      "private CanvasGroup m_headerGroup;",
      "private CanvasGroup m_questsViewGroup;",
      "private CanvasGroup m_actionsViewGroup;",
      "private TMP_Text m_questsContentLabel;",
      "private TMP_Text m_actionsContentLabel;",
      "private readonly Dictionary<string, bool> m_questGroupExpandedByName",
      "private readonly Dictionary<string, bool> m_actionGroupExpandedByName",
    ],
    "剧本日志 HUD StackCraft Quests / Recipes 面板字段和文案常量");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private void ApplyViewVisibility()",
    [
      "SetCanvasGroupVisible(m_headerGroup, true);",
      "SetCanvasGroupVisible(m_questsViewGroup, m_showQuests);",
      "SetCanvasGroupVisible(m_actionsViewGroup, !m_showQuests);",
    ],
    "剧本日志 HUD StackCraft QuestsView / RecipesView 显隐语义");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private void RefreshTabLabels()",
    [
      "m_questsTabLabel.text = QuestTabText;",
      "m_actionsTabLabel.text = ActionTabText;",
      "m_questsTabToggle.SetIsOnWithoutNotify(m_showQuests);",
      "m_actionsTabToggle.SetIsOnWithoutNotify(!m_showQuests);",
      "m_questsTabToggle.interactable = true;",
      "m_actionsTabToggle.interactable = true;",
    ],
    "剧本日志 HUD StackCraft Toggle 页签文字固定，不把未读红点投影到 Quests / Recipes 页签");
  if (scenarioJournalPanelSource.includes("BuildTabText(") ||
    scenarioJournalPanelSource.includes("HasUnreadVisibleQuests(") ||
    scenarioJournalPanelSource.includes("HasUnreadActions(")) {
    fail("剧本日志 HUD 仍保留页签未读红点逻辑；StackCraft 只在列表条目上显示新条目红点。");
  }
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private void SetMenuVisibility",
    [
      "m_isMenuAnimating",
      "float targetX = GetMenuTargetX(isOpen);",
      "DOAnchorPosX(targetX, MenuSlideSeconds)",
      ".SetUpdate(true)",
      "m_menuToggleLabel.text = isOpen ? MenuOpenLabel : MenuClosedLabel;",
      "if (playSound)",
      "PlayMenuToggleSound();",
    ],
    "剧本日志 HUD StackCraft MenuToggle 0.5 秒不受暂停影响的滑动和点击音效语义");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private void SnapMenuVisibility",
    [
      "position.x = GetMenuTargetX(isOpen);",
      "m_menuPanel.anchoredPosition = position;",
      "m_menuToggleLabel.text = isOpen ? MenuOpenLabel : MenuClosedLabel;",
    ],
    "剧本日志 HUD StackCraft MenuToggle 初始开关位置和文案");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private void OnScenarioDayCycleChanged",
    [
      "changedEvent.Phase == ScenarioDayCyclePhase.Inactive",
      "if (m_isMenuVisible)",
      "SetMenuVisibility(isOpen: false, animated: true, playSound: true);",
    ],
    "剧本日志 HUD StackCraft 日结自动收起右侧菜单并播放同款点击音效");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private static void PlayMenuToggleSound()",
    [
      "GameManager.Config.submitSound",
      "EventKit.Type.Send(new AudioPlaybackRequestedEvent(GameManager.Config.submitSound));",
    ],
    "剧本日志 HUD MenuToggle 音效走 GameCore 正式音频事件入口");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private void PopulateList",
    [
      "contentRoot.text = entries.Count == 0 ? emptyText : string.Empty;",
      "BuildJournalGroups(entries, expandedByName, defaultGroupName)",
      "$\"{groupName} {(isExpanded ? SymbolExpanded : SymbolCollapsed)}\"",
      "entryButton.gameObject.SetActive(isExpanded);",
    ],
    "剧本日志 HUD StackCraft 多分组头 / 条目按钮 / 空文案投影");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private static List<JournalGroup> BuildJournalGroups",
    [
      "ResolveJournalGroupName(entry.GroupName, defaultGroupName)",
      "expandedByName.Add(groupName, true);",
      "FindJournalGroup(groups, groupName)",
      "group.Entries.Add(entry);",
    ],
    "剧本日志 HUD 按作者源分组并默认展开");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private void OnEntryHoverChanged",
    [
      "ScenarioJournalEntryInfoEvent.Show",
      "m_run.MarkJournalEntrySeen(entry.Id)",
      "button.SetText(RemoveUnreadIndicator(button.Text));",
      "ClearHoveredEntryInfo();",
    ],
    "剧本日志 HUD StackCraft 条目 hover InfoPanel 与已读红点语义");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private static List<JournalEntry> BuildQuestEntries",
    [
      "SymbolBullet",
      "SymbolCompleted",
      "AppendUnreadIndicator(listText, run, questId);",
      "quest.Definition.JournalGroupName",
      "BuildQuestInfoBody(quest)",
    ],
    "剧本日志 HUD StackCraft 任务条目文本和完成标记");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private static List<JournalEntry> BuildActionEntries",
    [
      "SymbolBullet",
      "AppendUnreadIndicator(listText, run, actionId);",
      "action.JournalGroupName",
      "\"配方：\" + action.DisplayName",
      "BuildActionInfoBody(run.ContentIndex, action)",
    ],
    "剧本日志 HUD StackCraft 配方条目文本和悬浮信息标题 / 正文来源");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private static string BuildActionInfoBody",
    [
      "BuildActionIngredientSummary(contentIndex, action)",
      "? action.Description",
      ": ingredientSummary",
    ],
    "剧本日志 HUD 配方 hover 正文优先使用 StackCraft GetFormattedIngredients 同类材料摘要");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private static string BuildActionIngredientSummary",
    [
      "action.ParticipationSlots",
      "BuildActionSlotIngredientText(contentIndex, action, slots[slotIndex])",
      "text.Append(\", \");",
      "text.Append('.');",
    ],
    "剧本日志 HUD 配方材料列表用行动槽位稳定生成逗号分隔正文");
  assertCsharpBlockContainsOrdered(
    scenarioJournalPanelSource,
    "private static string BuildActionSlotIngredientText",
    [
      "slot.AllowedContentIds.Count > 0",
      "contentIndex.TryGet(contentId, out ContentAsset contentAsset)",
      "GetDisplayName(contentAsset)",
      "BuildParticipantCountText(slot)",
    ],
    "剧本日志 HUD 配方材料项从当前单局内容索引解析显示名和参与数量");
}

if (foundationSceneMenuSource == null) {
  fail("缺少 FoundationTestSceneMenu，无法证明剧本日志 HUD Prefab 会稳定重建右侧常驻形态。");
} else {
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static void EnsureScenarioJournalPanelPrefab()",
    [
      "TMP_FontAsset fontAsset = LoadStackCraftSurfaceFont();",
      "EnsureStackCraftSurfaceFontFallback(fontAsset, EnsureTestPanelFont());",
      "\"ScenarioJournalPanel\"",
      "typeof(RectTransform)",
      "typeof(Image)",
      "typeof(ScenarioJournalPanel)",
      "rootImage.color = Color.clear;",
      "rootImage.raycastTarget = false;",
      "\"MenuPanel\"",
      "menuPanel.anchorMin = new Vector2(1f, 0f);",
      "menuPanel.anchorMax = new Vector2(1f, 1f);",
      "menuPanel.offsetMin = new Vector2(-400f, 0f);",
      "menuPanel.offsetMax = Vector2.zero;",
      "\"Header\"",
      "typeof(ToggleGroup)",
      "header.sizeDelta = new Vector2(0f, 60f);",
      "headerImage.color = new Color(0.5019608f, 0.5019608f, 0.5019608f, 0.9019608f);",
      "headerToggleGroup.allowSwitchOff = false;",
      "\"QuestsToggle\"",
      "headerToggleGroup",
      "\"任务\"",
      "isOn: true",
      "\"RecipesToggle\"",
      "headerToggleGroup",
      "\"配方\"",
      "isOn: false",
      "\"MenuToggle\"",
      "TextMeshProUGUI menuToggleLabel = menuToggle.GetComponentInChildren<TextMeshProUGUI>(true);",
      "closeButton.gameObject.SetActive(false);",
      "\"QuestsView\"",
      "showByDefault: true",
      "\"QuestContent\"",
      "\"暂无任务\"",
      "\"RecipesView\"",
      "showByDefault: false",
      "\"RecipeContent\"",
      "\"暂无已发现配方\"",
      "RequireProperty(serializedPanel, \"m_menuPanel\")",
      "RequireProperty(serializedPanel, \"m_questsTabToggle\")",
      "RequireProperty(serializedPanel, \"m_actionsTabToggle\")",
      "RequireProperty(serializedPanel, \"m_menuToggleButton\")",
      "RequireProperty(serializedPanel, \"m_menuToggleLabel\")",
      "RequireProperty(serializedPanel, \"m_headerGroup\")",
      "RequireProperty(serializedPanel, \"m_questsViewGroup\")",
      "RequireProperty(serializedPanel, \"m_actionsViewGroup\")",
      "RequireProperty(serializedPanel, \"m_questsContentLabel\")",
      "RequireProperty(serializedPanel, \"m_actionsContentLabel\")",
    ],
    "FoundationTestSceneMenu StackCraft Quests / Recipes 右侧常驻 HUD Prefab 生成闭包");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static CanvasGroup CreateStackCraftJournalView",
    [
      "typeof(ScrollRect)",
      "typeof(CanvasGroup)",
      "group.interactable = true;",
      "\"Viewport\"",
      "typeof(RectTransform)",
      "typeof(Image)",
      "typeof(Mask)",
      "viewportRect.anchorMin = Vector2.zero;",
      "viewportRect.anchorMax = Vector2.one;",
      "viewportRect.pivot = new Vector2(0f, 1f);",
      "viewportRect.anchoredPosition = Vector2.zero;",
      "viewportRect.sizeDelta = Vector2.zero;",
      "viewportImage.color = Color.white;",
      "viewportMask.showMaskGraphic = false;",
      "scrollRect.viewport = viewportRect;",
      "scrollRect.movementType = ScrollRect.MovementType.Elastic;",
      "scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;",
      "scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;",
      "scrollRect.horizontalScrollbarSpacing = -3f;",
      "scrollRect.verticalScrollbarSpacing = -3f;",
    ],
    "FoundationTestSceneMenu 按 StackCraft QuestsView / RecipesView 生成 Viewport 层级");
  assertCsharpBlockContainsOrdered(
    foundationSceneMenuSource,
    "private static TextMeshProUGUI CreateStackCraftJournalContent",
    [
      "contentRect.pivot = new Vector2(0f, 1f);",
      "contentRect.anchoredPosition = Vector2.zero;",
      "contentRect.sizeDelta = Vector2.zero;",
      "layout.padding.left = 20;",
      "layout.padding.right = 20;",
      "layout.padding.top = 10;",
      "layout.padding.bottom = 10;",
      "layout.childForceExpandWidth = false;",
      "layout.spacing = 10f;",
      "scrollRect.content = contentRect;",
    ],
    "FoundationTestSceneMenu 按 StackCraft Content 生成内边距和列表布局");
}

const foundationTestSceneHarnessSource = readIfExists("Assets/Tests/Support/Runtime/FoundationTestSceneHarness.cs");
if (foundationTestSceneHarnessSource == null) {
  fail("缺少 FoundationTestSceneHarness，无法证明 StackCraft 右侧任务 / 配方面板会随测试场景自动打开。");
} else {
  assertCsharpBlockContainsOrdered(
    foundationTestSceneHarnessSource,
    "public void OpenJournalPanel()",
    [
      "UIKit.OpenPanelAsync<ScenarioJournalPanel>",
      "level: UILevel.Hud",
      "data: new ScenarioJournalPanelData(m_scenarioRun)",
    ],
    "显式打开剧本日志时使用 HUD 层");
  assertCsharpBlockContainsOrdered(
    foundationTestSceneHarnessSource,
    "private async UniTask OpenRequiredHudPanelsAsync",
    [
      "if (includeScenarioHud)",
      "UIKit.OpenPanelUniTaskAsync<ScenarioTurnPanel>",
      "level: UILevel.Hud",
      "ScenarioJournalPanel journalPanel = await UIKit.OpenPanelUniTaskAsync<ScenarioJournalPanel>",
      "level: UILevel.Hud",
      "StackCraft 右侧任务 / 配方面板",
      "UIKit.OpenPanelUniTaskAsync<TabletopCardInfoPanel>",
    ],
    "普通测试场景自动打开 StackCraft 右侧 Quests / Recipes HUD，同态开包场景可跳过剧本 HUD");
  assertCsharpBlockContainsOrdered(
    foundationTestSceneHarnessSource,
    "private IEnumerator Start()",
    [
      "bool isStackCraftParityLayout = m_initialLayout == FoundationTestInitialLayout.StackCraftStarterPack;",
      "OpenRequiredHudPanelsAsync(includeScenarioHud: !isStackCraftParityLayout)",
    ],
    "StackCraft 同态场景只打开必要卡牌信息 HUD，避免回合 / 任务扩展污染开包手感对照");
}

const uiKitSettingsAssetText = readIfExists("Assets/Settings/Resources/UIKitSettings.asset");
if (stackCraftUiRootPrefabText == null) {
  fail("缺少 StackCraft UIRoot Prefab，无法派生正式 UIKit CanvasScaler 配置。");
} else if (uiKitSettingsAssetText == null) {
  fail("缺少 Assets/Settings/Resources/UIKitSettings.asset，无法证明正式 UI 根缩放对齐 StackCraft。");
} else {
  const stackCraftUiRootYaml = unityYamlObjects(stackCraftUiRootPrefabText);
  const stackCraftUiCanvasScaler = unityComponentByClass(stackCraftUiRootYaml, "UICanvas", 114);
  if (stackCraftUiCanvasScaler == null) {
    fail("StackCraft UIRoot 缺少 UICanvas 的 CanvasScaler，无法派生 Quests / Recipes 右侧 HUD 缩放配置。");
  } else {
    for (const [sourceField, targetField, label] of [
      ["m_UiScaleMode", "ScaleMode", "缩放模式"],
      ["m_ReferenceResolution", "ReferenceResolution", "参考分辨率"],
      ["m_ScreenMatchMode", "ScreenMatchMode", "屏幕匹配模式"],
      ["m_MatchWidthOrHeight", "MatchWidthOrHeight", "宽高匹配权重"],
      ["m_ReferencePixelsPerUnit", "ReferencePixelsPerUnit", "参考像素每单位"],
      ["m_PhysicalUnit", "PhysicalUnit", "物理单位"],
      ["m_FallbackScreenDPI", "FallbackScreenDPI", "回退屏幕 DPI"],
      ["m_DefaultSpriteDPI", "DefaultSpriteDPI", "默认精灵 DPI"],
      ["m_DynamicPixelsPerUnit", "DynamicPixelsPerUnit", "动态像素每单位"],
    ]) {
      const expectedValue = unityPropertyValue(stackCraftUiCanvasScaler.text, sourceField);
      if (expectedValue == null) {
        fail(`StackCraft UICanvas CanvasScaler 缺少 ${sourceField}，无法派生 UIKitSettings 的 ${label}。`);
        continue;
      }

      assertYamlScalarEquals(
        uiKitSettingsAssetText,
        targetField,
        expectedValue,
        `正式 UIKitSettings 对齐 StackCraft UICanvas CanvasScaler ${label}`);
    }
  }
}

const scenarioJournalPanelPrefabText = readIfExists("Assets/Art/Prefabs/UI/ScenarioJournalPanel.prefab");
if (scenarioJournalPanelPrefabText == null) {
  fail("缺少 ScenarioJournalPanel Prefab，无法证明 StackCraft Quests / Recipes 右侧 HUD 已写入作者源。");
} else {
  const scenarioJournalPanelPrefabYaml = unityYamlObjects(scenarioJournalPanelPrefabText);
  const stackCraftUiRootYamlForJournal = stackCraftUiRootPrefabText == null
    ? null
    : unityYamlObjects(stackCraftUiRootPrefabText);
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "ScenarioJournalPanel",
    "剧本日志 HUD 根对象");
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "MenuPanel",
    "剧本日志 HUD 右侧菜单容器");
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "Header",
    "剧本日志 HUD 页眉");
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "QuestsToggle",
    "剧本日志 HUD 任务标签");
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "RecipesToggle",
    "剧本日志 HUD 配方标签");
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "MenuToggle",
    "剧本日志 HUD 菜单折叠按钮");
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "QuestsView",
    "剧本日志 HUD 任务视图");
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "RecipesView",
    "剧本日志 HUD 配方视图");
  const journalViewports = unityGameObjectsByName(scenarioJournalPanelPrefabYaml, "Viewport");
  if (journalViewports.length !== 2) {
    fail(`剧本日志 HUD 必须为 QuestsView / RecipesView 各生成一个 StackCraft 式 Viewport：当前 ${journalViewports.length} 个。`);
  }
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "QuestContent",
    "剧本日志 HUD 任务内容");
  assertUnityGameObjectExists(
    scenarioJournalPanelPrefabYaml,
    "RecipeContent",
    "剧本日志 HUD 配方内容");
  assertUnityGameObjectActiveState(
    scenarioJournalPanelPrefabYaml,
    "Close",
    false,
    "剧本日志 HUD 关闭按钮默认隐藏");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "ScenarioJournalPanel",
    224,
    "m_AnchorMin",
    "{x: 0, y: 0}",
    "剧本日志 HUD 根 RectTransform 左下锚点");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "ScenarioJournalPanel",
    224,
    "m_AnchorMax",
    "{x: 1, y: 1}",
    "剧本日志 HUD 根 RectTransform 右上锚点");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "ScenarioJournalPanel",
    224,
    "m_AnchoredPosition",
    "{x: 0, y: 0}",
    "剧本日志 HUD 根 RectTransform 位置");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "ScenarioJournalPanel",
    224,
    "m_SizeDelta",
    "{x: 0, y: 0}",
    "剧本日志 HUD 根 RectTransform 尺寸");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "ScenarioJournalPanel",
    114,
    "m_Color",
    "{r: 0, g: 0, b: 0, a: 0}",
    "剧本日志 HUD 根透明背景");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "ScenarioJournalPanel",
    114,
    "m_RaycastTarget",
    "0",
    "剧本日志 HUD 根不拦截桌面输入");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "MenuPanel",
    224,
    "m_AnchorMin",
    "{x: 1, y: 0}",
    "剧本日志 HUD 右侧菜单左下锚点");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "MenuPanel",
    224,
    "m_AnchorMax",
    "{x: 1, y: 1}",
    "剧本日志 HUD 右侧菜单右上锚点");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "MenuPanel",
    224,
    "m_AnchoredPosition",
    "{x: 0, y: 0}",
    "剧本日志 HUD 右侧菜单位置");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "MenuPanel",
    224,
    "m_SizeDelta",
    "{x: 400, y: 0}",
    "剧本日志 HUD 右侧菜单尺寸");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "Header",
    224,
    "m_AnchorMin",
    "{x: 0, y: 1}",
    "剧本日志 HUD 页眉左下锚点");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "Header",
    224,
    "m_AnchorMax",
    "{x: 1, y: 1}",
    "剧本日志 HUD 页眉右上锚点");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "Header",
    224,
    "m_SizeDelta",
    "{x: 0, y: 60}",
    "剧本日志 HUD 页眉尺寸");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "Header",
    114,
    "m_Color",
    "{r: 0.5019608, g: 0.5019608, b: 0.5019608, a: 0.9019608}",
    "剧本日志 HUD 页眉背景色");
  assertUnityMonoBehaviourScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "Header",
    "UnityEngine.UI::UnityEngine.UI.ToggleGroup",
    "m_AllowSwitchOff",
    "0",
    "剧本日志 HUD 页签 ToggleGroup 不允许全部关闭");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "QuestsToggle",
    114,
    "m_Color",
    "{r: 0.21933962, g: 0.6486481, b: 0.8773585, a: 1}",
    "剧本日志 HUD 任务标签背景色");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "RecipesToggle",
    114,
    "m_Color",
    "{r: 0.21933962, g: 0.6486481, b: 0.8773585, a: 1}",
    "剧本日志 HUD 配方标签背景色");
  for (const [tabName, expectedIsOn, label] of [
    ["QuestsToggle", "1", "任务标签"],
    ["RecipesToggle", "0", "配方标签"],
  ]) {
    if (unityMonoBehaviourByEditorClassIdentifier(
      scenarioJournalPanelPrefabYaml,
      tabName,
      "UnityEngine.UI::UnityEngine.UI.Button") != null) {
      fail(`剧本日志 HUD ${label} 仍使用 Button；StackCraft 参考是 Toggle + ToggleGroup。`);
    }
    assertUnityMonoBehaviourScalarEquals(
      scenarioJournalPanelPrefabYaml,
      tabName,
      "UnityEngine.UI::UnityEngine.UI.Toggle",
      "m_Interactable",
      "1",
      `剧本日志 HUD ${label} Toggle 可交互状态`);
    assertUnityMonoBehaviourScalarEquals(
      scenarioJournalPanelPrefabYaml,
      tabName,
      "UnityEngine.UI::UnityEngine.UI.Toggle",
      "m_IsOn",
      expectedIsOn,
      `剧本日志 HUD ${label} Toggle 初始选中态`);
    assertUnityMonoBehaviourScalarEquals(
      scenarioJournalPanelPrefabYaml,
      tabName,
      "UnityEngine.UI::UnityEngine.UI.Toggle",
      "m_SelectedColor",
      "{r: 1, g: 1, b: 1, a: 1}",
      `剧本日志 HUD ${label} Toggle 选中态文字颜色`);
    assertUnityMonoBehaviourFieldReferencesChild(
      scenarioJournalPanelPrefabYaml,
      tabName,
      "m_TargetGraphic",
      "Label",
      tabName,
      "Unity.TextMeshPro::TMPro.TextMeshProUGUI",
      `剧本日志 HUD ${label} Toggle 颜色过渡目标`);
    for (const [classId, fieldName, fieldLabel] of [
      [224, "m_AnchorMin", "标签文字左下锚点"],
      [224, "m_AnchorMax", "标签文字右上锚点"],
      [224, "m_AnchoredPosition", "标签文字位置"],
      [224, "m_SizeDelta", "标签文字尺寸"],
      [224, "m_Pivot", "标签文字轴心"],
      [114, "m_Color", "标签文字颜色"],
      [114, "m_RaycastTarget", "标签文字射线开关"],
      [114, "m_text", "标签文字内容"],
      [114, "m_fontSize", "标签文字字号"],
      [114, "m_fontSizeBase", "标签文字基础字号"],
      [114, "m_TextStyleHashCode", "标签文字样式"],
      [114, "m_HorizontalAlignment", "标签文字水平对齐"],
      [114, "m_VerticalAlignment", "标签文字垂直对齐"],
      [114, "m_enableKerning", "标签文字字偶距"],
    ]) {
      const sourceComponents = stackCraftUiRootYamlForJournal == null
        ? []
        : unityChildComponentsByParentName(stackCraftUiRootYamlForJournal, "Label", tabName, classId);
      if (sourceComponents.length === 0) {
        fail(`StackCraft UIRoot 缺少 ${tabName}/Label 的 Unity 组件 class ${classId}，无法派生剧本日志 HUD ${label}${fieldLabel}。`);
        continue;
      }
      if (sourceComponents.length > 1) {
        fail(`StackCraft UIRoot 命中多个 ${tabName}/Label 的 Unity 组件 class ${classId}，无法证明剧本日志 HUD ${label}${fieldLabel}唯一。`);
        continue;
      }
      const expectedValue = unityPropertyValue(sourceComponents[0].text, fieldName);
      if (expectedValue == null) {
        fail(`StackCraft ${tabName}/Label 缺少 ${fieldName}，无法派生剧本日志 HUD ${label}${fieldLabel}。`);
        continue;
      }

      assertUnityChildComponentScalarEquals(
        scenarioJournalPanelPrefabYaml,
        "Label",
        tabName,
        classId,
        fieldName,
        expectedValue,
        `剧本日志 HUD ${label}${fieldLabel}`);
    }
  }
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "MenuToggle",
    224,
    "m_AnchoredPosition",
    "{x: -60, y: -60}",
    "剧本日志 HUD 菜单按钮位置");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "MenuToggle",
    224,
    "m_SizeDelta",
    "{x: 60, y: 60}",
    "剧本日志 HUD 菜单按钮尺寸");
  for (const [classId, fieldName, expectedValue, label] of [
    [224, "m_AnchorMin", "{x: 0, y: 0}", "菜单按钮文字左下锚点"],
    [224, "m_AnchorMax", "{x: 1, y: 1}", "菜单按钮文字右上锚点"],
    [224, "m_AnchoredPosition", "{x: 0, y: 0}", "菜单按钮文字位置"],
    [224, "m_SizeDelta", "{x: 0, y: 0}", "菜单按钮文字尺寸"],
    [114, "m_text", "'>>'", "菜单按钮默认文字"],
    [114, "m_TextStyleHashCode", "-1183493901", "菜单按钮文字样式"],
    [114, "m_fontSize", "32", "菜单按钮文字字号"],
    [114, "m_fontSizeBase", "32", "菜单按钮文字基础字号"],
    [114, "m_fontSizeMin", "18", "菜单按钮文字最小字号"],
    [114, "m_fontSizeMax", "72", "菜单按钮文字最大字号"],
    [114, "m_HorizontalAlignment", "2", "菜单按钮文字水平对齐"],
    [114, "m_VerticalAlignment", "512", "菜单按钮文字垂直对齐"],
    [114, "m_enableKerning", "1", "菜单按钮文字字偶距"],
  ]) {
    assertUnityChildComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      "Label",
      "MenuToggle",
      classId,
      fieldName,
      expectedValue,
      `剧本日志 HUD ${label}`);
  }
  assertUnityMonoBehaviourFieldReferencesChild(
    scenarioJournalPanelPrefabYaml,
    "MenuToggle",
    "m_TargetGraphic",
    "Label",
    "MenuToggle",
    "Unity.TextMeshPro::TMPro.TextMeshProUGUI",
    "剧本日志 HUD 菜单折叠按钮颜色过渡目标");
  for (const [viewName, expectedAlpha, label] of [
    ["QuestsView", "1", "任务视图"],
    ["RecipesView", "0", "配方视图"],
  ]) {
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      viewName,
      224,
      "m_AnchorMin",
      "{x: 0, y: 0}",
      `剧本日志 HUD ${label}左下锚点`);
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      viewName,
      224,
      "m_AnchorMax",
      "{x: 1, y: 1}",
      `剧本日志 HUD ${label}右上锚点`);
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      viewName,
      224,
      "m_AnchoredPosition",
      "{x: 0, y: -30}",
      `剧本日志 HUD ${label}位置`);
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      viewName,
      224,
      "m_SizeDelta",
      "{x: 0, y: -60}",
      `剧本日志 HUD ${label}尺寸`);
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      viewName,
      114,
      "m_Color",
      "{r: 0, g: 0, b: 0, a: 0.9019608}",
      `剧本日志 HUD ${label}黑底颜色`);
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      viewName,
      225,
      "m_Alpha",
      expectedAlpha,
      `剧本日志 HUD ${label}默认透明度`);
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      viewName,
      225,
      "m_Interactable",
      "1",
      `剧本日志 HUD ${label}CanvasGroup 保持 StackCraft ToggleView 交互位`);
    const sourceScrollRect = stackCraftUiRootYamlForJournal == null
      ? null
      : unityComponentByProperty(stackCraftUiRootYamlForJournal, viewName, 114, "m_Content");
    if (sourceScrollRect == null) {
      fail(`StackCraft UIRoot 缺少 ${viewName} 的 ScrollRect，无法派生剧本日志 HUD ${label}滚动手感。`);
    } else {
      for (const [fieldName, fieldLabel] of [
        ["m_Horizontal", "横向滚动"],
        ["m_Vertical", "纵向滚动"],
        ["m_MovementType", "边界运动类型"],
        ["m_Elasticity", "弹性系数"],
        ["m_Inertia", "惯性"],
        ["m_DecelerationRate", "减速率"],
        ["m_ScrollSensitivity", "滚动灵敏度"],
        ["m_HorizontalScrollbarVisibility", "横向滚动条可见策略"],
        ["m_VerticalScrollbarVisibility", "纵向滚动条可见策略"],
        ["m_HorizontalScrollbarSpacing", "横向滚动条间距"],
        ["m_VerticalScrollbarSpacing", "纵向滚动条间距"],
      ]) {
        const expectedValue = unityPropertyValue(sourceScrollRect.text, fieldName);
        if (expectedValue == null) {
          fail(`StackCraft ${viewName} ScrollRect 缺少 ${fieldName}，无法派生剧本日志 HUD ${label}${fieldLabel}。`);
          continue;
        }

        assertUnityComponentByPropertyScalarEquals(
          scenarioJournalPanelPrefabYaml,
          viewName,
          114,
          "m_Content",
          fieldName,
          expectedValue,
          `剧本日志 HUD ${label}${fieldLabel}`);
      }
    }
  }
  for (const viewport of journalViewports) {
    const viewportRect = unityComponentsByClassOnGameObject(scenarioJournalPanelPrefabYaml, viewport, 224)[0] ?? null;
    const viewportImage = unityComponentsByClassOnGameObject(scenarioJournalPanelPrefabYaml, viewport, 114)
      .find((component) => unityPropertyLine(component.text, "m_Color") != null) ?? null;
    const viewportMask = unityComponentsByClassOnGameObject(scenarioJournalPanelPrefabYaml, viewport, 114)
      .find((component) => unityPropertyLine(component.text, "m_ShowMaskGraphic") != null) ?? null;
    const label = "剧本日志 HUD Viewport";
    if (viewportRect == null) {
      fail(`${label} 缺少 RectTransform。`);
    } else {
      for (const [fieldName, expectedValue] of [
        ["m_AnchorMin", "{x: 0, y: 0}"],
        ["m_AnchorMax", "{x: 1, y: 1}"],
        ["m_AnchoredPosition", "{x: 0, y: 0}"],
        ["m_SizeDelta", "{x: 0, y: 0}"],
        ["m_Pivot", "{x: 0, y: 1}"],
      ]) {
        const actualValue = unityPropertyValue(viewportRect.text, fieldName);
        if (actualValue !== expectedValue) {
          fail(`${label}.${fieldName} 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${expectedValue}。`);
        }
      }
    }
    if (viewportImage == null) {
      fail(`${label} 缺少 Image。`);
    } else {
      for (const [fieldName, expectedValue] of [
        ["m_Color", "{r: 1, g: 1, b: 1, a: 1}"],
        ["m_RaycastTarget", "1"],
      ]) {
        const actualValue = unityPropertyValue(viewportImage.text, fieldName);
        if (actualValue !== expectedValue) {
          fail(`${label}.${fieldName} 不一致：当前 ${actualValue ?? "<缺失>"}，应为 ${expectedValue}。`);
        }
      }
    }
    if (viewportMask == null) {
      fail(`${label} 缺少 Mask。`);
    } else if (unityPropertyValue(viewportMask.text, "m_ShowMaskGraphic") !== "0") {
      fail(`${label}.m_ShowMaskGraphic 必须为 0，对齐 StackCraft Viewport 遮罩。`);
    }
  }
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "QuestContent",
    114,
    "m_text",
    "暂无任务",
    "剧本日志 HUD 默认任务内容");
  assertUnityComponentScalarEquals(
    scenarioJournalPanelPrefabYaml,
    "RecipeContent",
    114,
    "m_text",
    "暂无已发现配方",
    "剧本日志 HUD 默认配方内容");
  for (const [contentName, label] of [
    ["QuestContent", "任务内容"],
    ["RecipeContent", "配方内容"],
  ]) {
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      contentName,
      224,
      "m_AnchoredPosition",
      "{x: 0, y: 0}",
      `剧本日志 HUD ${label}位置`);
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      contentName,
      224,
      "m_SizeDelta",
      "{x: 0, y: 0}",
      `剧本日志 HUD ${label}尺寸`);
    assertUnityComponentScalarEquals(
      scenarioJournalPanelPrefabYaml,
      contentName,
      224,
      "m_Pivot",
      "{x: 0, y: 1}",
      `剧本日志 HUD ${label}轴心`);
    if (stackCraftUiRootYamlForJournal == null) {
      fail(`缺少 StackCraft UIRoot Prefab，无法派生剧本日志 HUD ${label}列表布局。`);
    } else {
      assertUnityMonoBehaviourPropertiesMatchBySourceProperty(
        stackCraftUiRootYamlForJournal,
        "Content",
        "m_Padding",
        scenarioJournalPanelPrefabYaml,
        contentName,
        [
          "m_Padding",
          "m_ChildAlignment",
          "m_Spacing",
          "m_ChildForceExpandWidth",
          "m_ChildForceExpandHeight",
          "m_ChildControlWidth",
          "m_ChildControlHeight",
          "m_ChildScaleWidth",
          "m_ChildScaleHeight",
          "m_ReverseArrangement",
        ],
        `剧本日志 HUD ${label}列表布局`);
    }
  }

  assertUnityTextObjectsUseFontReference(
    scenarioJournalPanelPrefabYaml,
    ["QuestContent", "RecipeContent"],
    stackCraftTmpFontGuid,
    stackCraftTmpFontMaterialFileId,
    "剧本日志 HUD StackCraft 文字");
  if (foundationChineseFontGuid != null && scenarioJournalPanelPrefabText.includes(foundationChineseFontGuid)) {
    fail("剧本日志 HUD Prefab 仍引用地基测试中文字体；StackCraft 复刻界面必须使用参考 Prefab 的 LiberationSans SDF 字体。");
  }

  for (const [fieldName, targetObjectName, editorClassIdentifier, label] of [
    ["m_questsTabToggle", "QuestsToggle", "UnityEngine.UI::UnityEngine.UI.Toggle", "任务标签 Toggle"],
    ["m_actionsTabToggle", "RecipesToggle", "UnityEngine.UI::UnityEngine.UI.Toggle", "配方标签 Toggle"],
    ["m_menuToggleButton", "MenuToggle", "UnityEngine.UI::UnityEngine.UI.Button", "菜单折叠按钮"],
    ["m_questsContentLabel", "QuestContent", "Unity.TextMeshPro::TMPro.TextMeshProUGUI", "任务内容文本"],
    ["m_actionsContentLabel", "RecipeContent", "Unity.TextMeshPro::TMPro.TextMeshProUGUI", "配方内容文本"],
    ["m_closeButton", "Close", "UnityEngine.UI::UnityEngine.UI.Button", "关闭按钮"],
  ]) {
    assertUnityMonoBehaviourFieldReferences(
      scenarioJournalPanelPrefabYaml,
      "ScenarioJournalPanel",
      fieldName,
      targetObjectName,
      editorClassIdentifier,
      `剧本日志 HUD ${label}字段`);
  }
  assertUnityMonoBehaviourFieldReferencesChild(
    scenarioJournalPanelPrefabYaml,
    "ScenarioJournalPanel",
    "m_menuToggleLabel",
    "Label",
    "MenuToggle",
    "Unity.TextMeshPro::TMPro.TextMeshProUGUI",
    "剧本日志 HUD 菜单折叠文字字段");
  assertUnityComponentFieldReferences(
    scenarioJournalPanelPrefabYaml,
    "ScenarioJournalPanel",
    "m_menuPanel",
    "MenuPanel",
    224,
    "剧本日志 HUD 菜单容器字段");
  for (const [fieldName, targetObjectName, label] of [
    ["m_headerGroup", "Header", "页眉显隐"],
    ["m_questsViewGroup", "QuestsView", "任务视图显隐"],
    ["m_actionsViewGroup", "RecipesView", "配方视图显隐"],
  ]) {
    assertUnityComponentFieldReferences(
      scenarioJournalPanelPrefabYaml,
      "ScenarioJournalPanel",
      fieldName,
      targetObjectName,
      225,
      `剧本日志 HUD ${label}字段`);
  }
}

const tabletopCardInfoPanelPrefabText = readIfExists("Assets/Art/Prefabs/UI/TabletopCardInfoPanel.prefab");
if (tabletopCardInfoPanelPrefabText == null) {
  fail("缺少 TabletopCardInfoPanel Prefab，无法证明 StackCraft InfoPanel 左下详情面板已写入作者源。");
} else if (stackCraftUiRootPrefabText == null) {
  fail("缺少 StackCraft UIRoot Prefab，无法做 InfoPanel 对象级对账：Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
} else {
  const tabletopCardInfoPanelPrefabYaml = unityYamlObjects(tabletopCardInfoPanelPrefabText);
  const stackCraftUiRootYaml = unityYamlObjects(stackCraftUiRootPrefabText);
  assertUnityGameObjectExists(
    tabletopCardInfoPanelPrefabYaml,
    "TabletopCardInfoPanel",
    "卡牌详情 HUD 根对象");
  assertUnityGameObjectExists(
    tabletopCardInfoPanelPrefabYaml,
    "InfoPanel",
    "卡牌详情 HUD StackCraft InfoPanel 容器");
  assertUnityGameObjectExists(
    tabletopCardInfoPanelPrefabYaml,
    "InfoText",
    "卡牌详情 HUD StackCraft InfoText 文本");
  assertUnityComponentPropertiesMatch(
    stackCraftUiRootYaml,
    "InfoPanel",
    tabletopCardInfoPanelPrefabYaml,
    "InfoPanel",
    224,
    [
      "m_LocalPosition",
      "m_LocalScale",
      "m_AnchorMin",
      "m_AnchorMax",
      "m_AnchoredPosition",
      "m_SizeDelta",
      "m_Pivot",
    ],
    "卡牌详情 HUD InfoPanel RectTransform");
  assertUnityComponentPropertiesMatch(
    stackCraftUiRootYaml,
    "InfoPanel",
    tabletopCardInfoPanelPrefabYaml,
    "InfoPanel",
    114,
    [
      "m_Color",
      "m_RaycastTarget",
      "m_Maskable",
      "m_Type",
      "m_PreserveAspect",
      "m_FillCenter",
      "m_FillMethod",
      "m_FillAmount",
      "m_FillClockwise",
      "m_FillOrigin",
      "m_UseSpriteMesh",
      "m_PixelsPerUnitMultiplier",
    ],
    "卡牌详情 HUD InfoPanel 背景");
  assertUnityMonoBehaviourPropertiesMatchBySourceProperty(
    stackCraftUiRootYaml,
    "InfoPanel",
    "m_Padding",
    tabletopCardInfoPanelPrefabYaml,
    "InfoPanel",
    [
      "m_Padding",
      "m_ChildAlignment",
      "m_Spacing",
      "m_ChildForceExpandWidth",
      "m_ChildForceExpandHeight",
      "m_ChildControlWidth",
      "m_ChildControlHeight",
      "m_ChildScaleWidth",
      "m_ChildScaleHeight",
      "m_ReverseArrangement",
    ],
    "卡牌详情 HUD InfoPanel 垂直布局");
  assertUnityMonoBehaviourPropertiesMatchBySourceProperty(
    stackCraftUiRootYaml,
    "InfoPanel",
    "m_HorizontalFit",
    tabletopCardInfoPanelPrefabYaml,
    "InfoPanel",
    ["m_HorizontalFit", "m_VerticalFit"],
    "卡牌详情 HUD InfoPanel 尺寸适配");
  assertUnityTextObjectMatchesSource(
    stackCraftUiRootYaml,
    "InfoText",
    tabletopCardInfoPanelPrefabYaml,
    "InfoText",
    "卡牌详情 HUD InfoText 文字样式");
  assertUnityComponentPropertiesMatch(
    stackCraftUiRootYaml,
    "InfoText",
    tabletopCardInfoPanelPrefabYaml,
    "InfoText",
    114,
    [
      "m_lineSpacing",
      "m_paragraphSpacing",
      "m_TextWrappingMode",
    ],
    "卡牌详情 HUD InfoText 段落参数");
  const infoPanelComponent = unityMonoBehaviourByEditorClassIdentifier(
    tabletopCardInfoPanelPrefabYaml,
    "TabletopCardInfoPanel",
    "Gameplay.Runtime::Gameplay.Tabletop.TabletopCardInfoPanel");
  if (infoPanelComponent == null) {
    fail("TabletopCardInfoPanel Prefab 根对象缺少正式 TabletopCardInfoPanel 脚本组件。");
  } else {
    const infoPanelObject = unityGameObjectByName(tabletopCardInfoPanelPrefabYaml, "InfoPanel");
    const contentRootLine = infoPanelObject == null
      ? null
      : `m_contentRoot: {fileID: ${infoPanelObject.fileId}}`;
    if (contentRootLine == null ||
        unityPropertyLine(infoPanelComponent.text, "m_contentRoot") !== contentRootLine) {
      fail(`TabletopCardInfoPanel.m_contentRoot 没有指向 StackCraft InfoPanel 容器：当前 ${unityPropertyLine(infoPanelComponent.text, "m_contentRoot") ?? "<缺失>"}，应为 ${contentRootLine ?? "<缺少 InfoPanel 对象>"}。`);
    }
    if (unityPropertyValue(infoPanelComponent.text, "m_headerSize") !== "34") {
      fail(`TabletopCardInfoPanel.m_headerSize 没有对齐 StackCraft InfoPanel.headerSize：当前 ${unityPropertyValue(infoPanelComponent.text, "m_headerSize") ?? "<缺失>"}，应为 34。`);
    }
    if (unityPropertyValue(infoPanelComponent.text, "m_bodySize") !== "30") {
      fail(`TabletopCardInfoPanel.m_bodySize 没有对齐 StackCraft InfoPanel.bodySize：当前 ${unityPropertyValue(infoPanelComponent.text, "m_bodySize") ?? "<缺失>"}，应为 30。`);
    }
  }
  assertUnityMonoBehaviourFieldReferences(
    tabletopCardInfoPanelPrefabYaml,
    "TabletopCardInfoPanel",
    "m_infoLabel",
    "InfoText",
    "Unity.TextMeshPro::TMPro.TextMeshProUGUI",
    "卡牌详情 HUD InfoText 字段");
  for (const staleObjectName of ["CardInfo", "Title", "Description"]) {
    if (unityGameObjectByName(tabletopCardInfoPanelPrefabYaml, staleObjectName) != null) {
      fail(`TabletopCardInfoPanel Prefab 仍包含旧右下详情面板对象：${staleObjectName}。`);
    }
  }
}

const tabletopActionProgressViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopActionProgressView.cs");
const progressPrefabText = readIfExists("Assets/Art/Prefabs/牌桌/行动进度.prefab");
if (tabletopActionProgressViewSource == null) {
  fail("缺少牌桌行动进度视图源码，无法证明 ProgressUI.fillAmount 由正式视图承接。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopActionProgressViewSource,
    "public sealed class TabletopActionProgressView",
    [
      "private Image m_backgroundImage;",
      "private Image m_progressFill;",
    ],
    "牌桌行动进度视图 StackCraft ProgressUI UGUI 字段");
  assertCsharpBlockContainsOrdered(
    tabletopActionProgressViewSource,
    "public void Show(",
    [
      "NormalizedProgress = Mathf.Clamp01(normalizedProgress);",
      "base.gameObject.SetActive(true);",
      "base.transform.localPosition = m_displayOffset;",
      "m_progressFill.fillAmount = NormalizedProgress",
      "m_progressFill.color = m_runningColor",
      "ApplyCanvasSortingOrder(sortingOrder);",
    ],
    "牌桌行动进度视图 StackCraft ProgressUI fillAmount 刷新语义");
  assertCsharpBlockContainsOrdered(
    tabletopActionProgressViewSource,
    "private void ApplyCanvasSortingOrder(",
    [
      "canvas.renderMode = RenderMode.WorldSpace",
      "canvas.overrideSorting = true",
      "canvas.sortingOrder = sortingOrder",
    ],
    "牌桌行动进度视图 StackCraft ProgressUI WorldSpace Canvas 排序语义");
  for (const obsoleteToken of [
    "SpriteRenderer m_backgroundRenderer",
    "SpriteRenderer m_fillRenderer",
    "m_fillRenderer.transform.localScale",
    "m_fillBaseLocalScale",
    "m_stackedOffset",
    "m_pausedColor",
    "StackedIndex",
  ]) {
    if (tabletopActionProgressViewSource.includes(obsoleteToken)) {
      fail(`牌桌行动进度视图仍保留 SpriteRenderer 版进度条残留：${obsoleteToken}`);
    }
  }
}
if (progressPrefabText == null) {
  fail("缺少行动进度 Prefab，无法证明行动进度条表面。");
} else {
  const progressPrefabYaml = unityYamlObjects(progressPrefabText);
  if (stackCraftProgressUiPrefabText == null) {
    fail("缺少 StackCraft ProgressUI Prefab，无法做行动进度条对象级对账：Assets/StackCraft/Prefabs/UI/ProgressUI.prefab");
  } else {
    const stackCraftProgressUiYaml = unityYamlObjects(stackCraftProgressUiPrefabText);
    if (tabletopWorldSpaceRotation != null) {
      assertUnityComponentInlineNumericPropertyMatches(
        progressPrefabYaml,
        "行动进度",
        224,
        "m_LocalRotation",
        tabletopWorldSpaceRotation,
        ["x", "y", "z", "w"],
        "行动进度根 RectTransform");
    }
    assertUnityImageObjectMatchesSource(
      stackCraftProgressUiYaml,
      "ProgressUI",
      progressPrefabYaml,
      "行动进度",
      "行动进度背景");
    assertUnityImageObjectMatchesSource(
      stackCraftProgressUiYaml,
      "ProgressFill",
      progressPrefabYaml,
      "ProgressFill",
      "行动进度填充条");
    if (foundationSceneMenuSource != null) {
      const sourceProgressRootRect = unityComponentByClass(stackCraftProgressUiYaml, "ProgressUI", 224);
      const sourceProgressRootImage = unityComponentByClass(stackCraftProgressUiYaml, "ProgressUI", 114);
      if (sourceProgressRootRect == null) {
        fail("StackCraft ProgressUI 缺少 RectTransform，无法派生行动进度生成器根尺寸。");
      } else {
        const rootSize = unityInlineObjectProperty(
          sourceProgressRootRect.text,
          "m_SizeDelta",
          "StackCraft ProgressUI.m_SizeDelta");
        const rootSizeConstructor = rootSize == null
          ? null
          : csharpInlineConstructor(
            "Vector2",
            rootSize,
            ["x", "y"],
            "StackCraft ProgressUI.m_SizeDelta");
        if (rootSizeConstructor != null) {
          assertCsharpAssignmentEquals(
            foundationSceneMenuSource,
            "rootTransform.sizeDelta",
            rootSizeConstructor,
            "行动进度生成器根尺寸");
        }
      }
      if (sourceProgressRootImage == null) {
        fail("StackCraft ProgressUI 缺少背景 Image，无法派生行动进度生成器背景颜色。");
      } else {
        const backgroundColor = unityInlineObjectProperty(
          sourceProgressRootImage.text,
          "m_Color",
          "StackCraft ProgressUI.m_Color");
        const backgroundColorConstructor = backgroundColor == null
          ? null
          : csharpInlineConstructor(
            "Color",
            backgroundColor,
            ["r", "g", "b", "a"],
            "StackCraft ProgressUI.m_Color");
        if (backgroundColorConstructor != null) {
          assertCsharpAssignmentEquals(
            foundationSceneMenuSource,
            "backgroundImage.color",
            backgroundColorConstructor,
            "行动进度生成器背景颜色");
        }
      }
    }
    assertUnityComponentFieldReferences(
      progressPrefabYaml,
      "行动进度",
      "m_backgroundImage",
      "行动进度",
      114,
      "行动进度视图字段引用");
    assertUnityComponentFieldReferences(
      progressPrefabYaml,
      "行动进度",
      "m_progressFill",
      "ProgressFill",
      114,
      "行动进度视图字段引用");

    const progressSquareGuid = unityGuid(readIfExists("Assets/Art/Sprites/StackCraft/Square.png.meta"));
    if (progressSquareGuid == null) {
      fail("Assets/Art/Sprites/StackCraft/Square.png.meta 缺少合法 GUID。");
    } else {
      assertUnityComponentReferenceEquals(
        progressPrefabYaml,
        "ProgressFill",
        114,
        "m_Sprite",
        "21300000",
        progressSquareGuid,
        "3",
        "行动进度填充条 Image");
    }

    const sourceProgressBehaviour = unityComponentByProperty(
      stackCraftProgressUiYaml,
      "ProgressUI",
      114,
      "displayOffset");
    if (sourceProgressBehaviour == null) {
      fail("StackCraft ProgressUI 缺少 displayOffset 字段，无法派生行动进度显示偏移。");
    } else {
      const displayOffset = unityInlineObjectProperty(
        sourceProgressBehaviour.text,
        "displayOffset",
        "StackCraft ProgressUI.displayOffset");
      if (displayOffset != null) {
        const displayOffsetConstructor = csharpInlineConstructor(
          "Vector3",
          displayOffset,
          ["x", "y", "z"],
          "StackCraft ProgressUI.displayOffset");
        const displayOffsetLiteral = unityInlineObjectLiteral(
          displayOffset,
          ["x", "y", "z"],
          "StackCraft ProgressUI.displayOffset");
        if (tabletopActionProgressViewSource != null && displayOffsetConstructor != null) {
          assertCsharpFieldInitializerEquals(
            tabletopActionProgressViewSource,
            "m_displayOffset",
            displayOffsetConstructor,
            "行动进度显示偏移默认值");
        }
        if (foundationSceneMenuSource != null && displayOffsetConstructor != null) {
          assertCsharpAssignmentEquals(
            foundationSceneMenuSource,
            "serializedProgressView.FindProperty(\"m_displayOffset\").vector3Value",
            displayOffsetConstructor,
            "行动进度生成器显示偏移");
        }
        if (displayOffsetLiteral != null) {
          assertYamlScalarEquals(
            progressPrefabText,
            "m_displayOffset",
            displayOffsetLiteral,
            "行动进度 Prefab 显示偏移");
        }
      }
    }

    const sourceProgressFillImage = unityComponentByClass(stackCraftProgressUiYaml, "ProgressFill", 114);
    if (sourceProgressFillImage == null) {
      fail("StackCraft ProgressFill 缺少 Image 组件，无法派生行动进度运行颜色。");
    } else {
      const fillAmount = unityPropertyValue(
        sourceProgressFillImage.text,
        "m_FillAmount");
      if (fillAmount != null && foundationSceneMenuSource != null) {
        assertCsharpAssignmentEquals(
          foundationSceneMenuSource,
          "fillImage.fillAmount",
          csharpFloatLiteral(fillAmount),
          "行动进度生成器填充比例");
      }
      const fillColor = unityInlineObjectProperty(
        sourceProgressFillImage.text,
        "m_Color",
        "StackCraft ProgressFill.m_Color");
      if (fillColor != null) {
        const fillColorConstructor = csharpInlineConstructor(
          "Color",
          fillColor,
          ["r", "g", "b", "a"],
          "StackCraft ProgressFill.m_Color");
        const fillColorLiteral = unityInlineObjectLiteral(
          fillColor,
          ["r", "g", "b", "a"],
          "StackCraft ProgressFill.m_Color");
        if (tabletopActionProgressViewSource != null && fillColorConstructor != null) {
          assertCsharpFieldInitializerEquals(
            tabletopActionProgressViewSource,
            "m_runningColor",
            fillColorConstructor,
            "行动进度运行颜色默认值");
        }
        if (foundationSceneMenuSource != null && fillColorConstructor != null) {
          assertCsharpAssignmentEquals(
            foundationSceneMenuSource,
            "fillImage.color",
            fillColorConstructor,
            "行动进度生成器填充颜色");
        }
        if (fillColorLiteral != null) {
          assertYamlScalarEquals(
            progressPrefabText,
            "m_runningColor",
            fillColorLiteral,
            "行动进度 Prefab 运行颜色");
        }
      }
    }
  }
  assertUnityComponentScalarEquals(
    progressPrefabYaml,
    "行动进度",
    223,
    "m_RenderMode",
    "2",
    "行动进度 WorldSpace Canvas");
  for (const obsoleteToken of [
    "SpriteRenderer:",
    "m_backgroundRenderer:",
    "m_fillRenderer:",
    "m_stackedOffset:",
    "m_pausedColor:",
    tabletopPlaceholderCardArtGuid,
  ].filter((token) => token != null)) {
    if (progressPrefabText.includes(obsoleteToken)) {
      fail(`行动进度 Prefab 仍保留旧 SpriteRenderer / 占位图进度条残留：${obsoleteToken}`);
    }
  }
}

assertSameFileHash(
  "Assets/StackCraft/Textures/Puff.png",
  "Assets/Art/Textures/卡牌烟雾.png",
  "StackCraft Puff 粒子贴图");
assertTextureImportVisualSettingsMatch(
  "Assets/StackCraft/Textures/Puff.png",
  "Assets/Art/Textures/卡牌烟雾.png",
  "StackCraft Puff 粒子贴图",
  { requireSpriteImport: false });
assertSameFileHash(
  "Assets/StackCraft/Sounds/SFX/Puff.wav",
  "Assets/Audio/SFX/卡牌烟雾反馈.wav",
  "StackCraft Puff 音效");

const cardSmokeMaterialText = readIfExists("Assets/Art/Materials/卡牌烟雾材质.mat");
if (cardSmokeMaterialText == null) {
  fail("缺少卡牌烟雾材质，无法证明 StackCraft Puff.mat 材质闭包。");
} else {
  const stackCraftPuffMaterialText = readIfExists("Assets/StackCraft/Materials/Effects/Puff.mat");
  const cardSmokeTextureGuid = unityGuid(readIfExists("Assets/Art/Textures/卡牌烟雾.png.meta"));
  if (cardSmokeTextureGuid == null) {
    fail("缺少卡牌烟雾贴图 meta，无法验证卡牌烟雾材质的 _MainTex。");
  } else {
    assertMaterialTextureGuid(
      cardSmokeMaterialText,
      "_MainTex",
      cardSmokeTextureGuid,
      "Assets/Art/Materials/卡牌烟雾材质.mat",
      "StackCraft Puff.mat _MainTex");
  }
  if (stackCraftPuffMaterialText == null) {
    fail("缺少 StackCraft Puff.mat 来源材质，无法对账卡牌烟雾参数。");
  } else {
    assertYamlMappingPropertyLinesMatch(
      stackCraftPuffMaterialText,
      cardSmokeMaterialText,
      "Assets/StackCraft/Materials/Effects/Puff.mat",
      "Assets/Art/Materials/卡牌烟雾材质.mat",
      ["m_Shader"],
      "Puff.mat");
    assertYamlPropertyLinesMatch(
      stackCraftPuffMaterialText,
      cardSmokeMaterialText,
      "Assets/StackCraft/Materials/Effects/Puff.mat",
      "Assets/Art/Materials/卡牌烟雾材质.mat",
      ["_Mode", "_SrcBlend", "_DstBlend", "_ZWrite", "_ColorMode", "_LightingEnabled", "_Color"],
      "Puff.mat");
  }
}

const cardSmokeViewSource = readIfExists("Assets/Scripts/Gameplay/Runtime/Tabletop/View/TabletopCardSmokeEffectView.cs");
if (cardSmokeViewSource == null) {
  fail("缺少 TabletopCardSmokeEffectView，无法证明 StackCraft PuffParticle 生命周期闭包。");
} else {
  assertCsharpBlockContainsOrdered(
    cardSmokeViewSource,
    "internal void Play(",
    [
      "ParticleSystem.MainModule main = m_particleSystem.main;",
      "ParticleSystem.MinMaxCurve startLifetime = main.startLifetime;",
      "m_durationSeconds = Mathf.Max(0.01f, main.duration + startLifetime.constantMax);",
      "transform.localPosition = TabletopCoordinateSpace.ToLocalPosition(tablePosition);",
      "m_particleSystem.Clear(withChildren: true);",
      "m_particleSystem.Play(withChildren: true);",
      "m_isPlaying = true;",
    ],
    "TabletopCardSmokeEffectView 播放 StackCraft PuffParticle 生命周期方法");
  assertCsharpBlockContainsOrdered(
    cardSmokeViewSource,
    "private void Update",
    [
      "m_elapsedSeconds += Time.unscaledDeltaTime;",
      "if (m_elapsedSeconds >= m_durationSeconds)",
      "m_isPlaying = false;",
      "gameObject.SetActive(false);",
    ],
    "TabletopCardSmokeEffectView 按非缩放时间结束 StackCraft PuffParticle 方法");
}

if (tabletopViewSettingsSource == null) {
  fail("TabletopViewSettings 没有把 CardSmoke 表现提示映射到卡牌烟雾音效。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopViewSettingsSource,
    "internal AudioClipResolver GetPresentationAudio",
    [
      "return cue switch",
      "TabletopPresentationCueKind.CardSmoke => m_cardSmokeAudio",
      "_ => throw new ArgumentOutOfRangeException",
    ],
    "TabletopViewSettings 把 CardSmoke 表现提示映射到卡牌烟雾音效方法");
}
const cardSmokeSortingOrder = tabletopViewSettingsSource == null
  ? null
  : csharpRawInitializer(
    tabletopViewSettingsSource,
    "m_cardSmokeSortingOrder",
    "TabletopViewSettings 卡牌烟雾排序默认值");
if (tabletopViewSource == null) {
  fail("TabletopView 没有按 StackCraft PuffParticle 的“音效 + 粒子”顺序播放卡牌烟雾反馈。");
} else {
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void PlayPresentationCue(TabletopPresentationCue cue)",
    [
      "PlayAudio(m_settings.GetPresentationAudio(cue.Kind));",
      "if (cue.Kind != TabletopPresentationCueKind.CardSmoke)",
      "if (!cue.HasTablePosition)",
      "RequestCardSmokeEffect(cue.TablePosition);",
    ],
    "TabletopView 按 StackCraft PuffParticle 的音效到粒子顺序播放方法");
  assertCsharpBlockContainsOrdered(
    tabletopViewSource,
    "private void RequestCardSmokeEffect",
    [
      "ResourceSystem.InstantiateAsync<GameObject>(",
      "m_settings.CardSmokeEffectPrefab.Address",
      "m_cardSmokeEffects.Add(entry);",
      "TabletopCardSmokeEffectView component = instance.GetComponent<TabletopCardSmokeEffectView>();",
      "component.Play(tablePosition, m_settings.CardSmokeSortingOrder);",
    ],
    "TabletopView 通过 ResourceSystem 实例化并播放卡牌烟雾粒子方法");
}
if (tabletopViewSettingsAssetText == null) {
  fail("缺少牌桌测试视图设置资产，无法验证卡牌烟雾音效和粒子预制体引用。");
} else {
  const cardSmokePrefabGuid = unityGuid(readIfExists("Assets/Art/Prefabs/卡牌烟雾粒子.prefab.meta"));
  const cardSmokeAudioGuid = unityGuid(readIfExists("Assets/Gameplay/Tests/牌桌/音效/卡牌烟雾反馈音效.asset.meta"));
  assertYamlScalarEquals(
    tabletopViewSettingsAssetText,
    "m_cardSmokeSortingOrder",
    cardSmokeSortingOrder,
    "牌桌测试视图设置卡牌烟雾排序默认值");
  if (cardSmokePrefabGuid == null) {
    fail("缺少卡牌烟雾粒子 Prefab meta，无法验证视图设置引用。");
  } else {
    assertSoftAssetReference(
      tabletopViewSettingsAssetText,
      "m_cardSmokeEffectPrefab",
      "卡牌烟雾粒子",
      cardSmokePrefabGuid,
      "牌桌测试视图设置卡牌烟雾粒子 Prefab 引用");
  }
  if (cardSmokeAudioGuid == null) {
    fail("缺少卡牌烟雾反馈音效资产 meta，无法验证视图设置引用。");
  } else {
    assertYamlReferenceLine(
      tabletopViewSettingsAssetText,
      "m_cardSmokeAudio",
      "11400000",
      cardSmokeAudioGuid,
      "2",
      "Assets/Gameplay/Tests/牌桌/牌桌测试视图设置.asset",
      "牌桌测试视图设置卡牌烟雾反馈音效资产");
  }
}

const cardSmokeAudioAssetText = readIfExists("Assets/Gameplay/Tests/牌桌/音效/卡牌烟雾反馈音效.asset");
if (cardSmokeAudioAssetText == null) {
  fail("缺少卡牌烟雾反馈音效资产，无法证明 StackCraft Puff.wav 音效闭包。");
} else {
  const cardSmokeClipGuid = unityGuid(readIfExists("Assets/Audio/SFX/卡牌烟雾反馈.wav.meta"));
  if (cardSmokeClipGuid == null) {
    fail("缺少卡牌烟雾反馈 wav meta，无法验证 AudioClipResolver。");
  } else {
    assertYamlListContainsReference(
      cardSmokeAudioAssetText,
      "m_audioClips",
      "8300000",
      cardSmokeClipGuid,
      "3",
      "Assets/Gameplay/Tests/牌桌/音效/卡牌烟雾反馈音效.asset",
      "卡牌烟雾反馈 AudioClip 列表");
  }
  assertYamlScalarEquals(
    cardSmokeAudioAssetText,
    "m_targetChannel",
    "3",
    "卡牌烟雾反馈音效资产 SFX 通道");
}

const cardSmokePrefabText = readIfExists("Assets/Art/Prefabs/卡牌烟雾粒子.prefab");
if (cardSmokePrefabText == null) {
  fail("缺少卡牌烟雾粒子 Prefab，无法证明 StackCraft PuffParticle 表现闭包。");
} else {
  const cardSmokeMaterialGuid = unityGuid(readIfExists("Assets/Art/Materials/卡牌烟雾材质.mat.meta"));
  const cardSmokePrefabYaml = unityYamlObjects(cardSmokePrefabText);
  if (stackCraftPuffParticlePrefabText == null) {
    fail("缺少 StackCraft PuffParticle Prefab，无法做卡牌烟雾粒子对象级对账：Assets/StackCraft/Prefabs/VFX/PuffParticle.prefab");
  } else {
    const stackCraftPuffParticleYaml = unityYamlObjects(stackCraftPuffParticlePrefabText);
    assertUnityComponentPropertiesMatch(
      stackCraftPuffParticleYaml,
      "PuffParticle",
      cardSmokePrefabYaml,
      "卡牌烟雾粒子",
      4,
      ["m_LocalRotation", "m_LocalPosition", "m_LocalScale", "m_LocalEulerAnglesHint"],
      "卡牌烟雾粒子 Transform");
    assertUnityComponentMatchesSourceExcept(
      stackCraftPuffParticleYaml,
      "PuffParticle",
      cardSmokePrefabYaml,
      "卡牌烟雾粒子",
      198,
      ["playOnAwake"],
      "卡牌烟雾粒子 ParticleSystem");
    assertUnityComponentScalarEquals(
      cardSmokePrefabYaml,
      "卡牌烟雾粒子",
      198,
      "playOnAwake",
      "0",
      "卡牌烟雾粒子 ParticleSystem");
    assertUnityComponentPropertiesMatch(
      stackCraftPuffParticleYaml,
      "PuffParticle",
      cardSmokePrefabYaml,
      "卡牌烟雾粒子",
      199,
      ["m_RenderMode", "m_MaxParticleSize", "m_CameraVelocityScale", "m_VelocityScale", "m_LengthScale"],
      "卡牌烟雾粒子 Renderer");
    assertUnityComponentScalarEquals(
      cardSmokePrefabYaml,
      "卡牌烟雾粒子",
      199,
      "m_SortingOrder",
      cardSmokeSortingOrder,
      "卡牌烟雾粒子 Renderer 排序");
    assertUnityComponentFieldReferences(
      cardSmokePrefabYaml,
      "卡牌烟雾粒子",
      "m_particleSystem",
      "卡牌烟雾粒子",
      198,
      "卡牌烟雾粒子视图字段引用");
    assertUnityComponentFieldReferences(
      cardSmokePrefabYaml,
      "卡牌烟雾粒子",
      "m_renderer",
      "卡牌烟雾粒子",
      199,
      "卡牌烟雾粒子视图字段引用");
  }
  assertUnityGameObjectExists(cardSmokePrefabYaml, "卡牌烟雾粒子", "卡牌烟雾粒子 Prefab");
  assertUnityGameObjectActiveState(cardSmokePrefabYaml, "卡牌烟雾粒子", false, "卡牌烟雾粒子 Prefab 初始隐藏状态");
  if (cardSmokePrefabText.includes("m_RenderMode: 0")) {
    fail("卡牌烟雾粒子 Prefab 仍使用 Billboard；StackCraft PuffParticle 使用 HorizontalBillboard。");
  }
  if (cardSmokeMaterialGuid == null) {
    fail("缺少卡牌烟雾材质 meta，无法验证卡牌烟雾粒子 Prefab 的材质引用。");
  } else {
    assertUnityComponentReferenceListEquals(
      cardSmokePrefabYaml,
      "卡牌烟雾粒子",
      199,
      "m_Materials",
      [{ fileId: "2100000", guid: cardSmokeMaterialGuid, type: "2" }],
      "卡牌烟雾粒子 Renderer 材质");
  }
}

const projectileSpritePairs = [
  ["Assets/StackCraft/Sprites/Projectile_Arrow.png", "Assets/Art/Sprites/箭矢投射物.png", "箭矢投射物", "m_rangedSprite"],
  ["Assets/StackCraft/Sprites/Projectile_Magic.png", "Assets/Art/Sprites/魔法投射物.png", "魔法投射物", "m_magicSprite"],
];
for (const [sourcePath, localPath, label] of projectileSpritePairs) {
  assertSameFileHash(sourcePath, localPath, `StackCraft ${label}图片`);
  assertTextureImportVisualSettingsMatch(sourcePath, localPath, `StackCraft ${label}图片`);
}

const projectilePrefabText = readIfExists("Assets/Art/Prefabs/牌桌/投射物.prefab");
if (projectilePrefabText == null) {
  fail("缺少投射物 Prefab，无法证明箭矢 / 魔法投射物表面。");
} else {
  if (stackCraftProjectileArrowPrefabText == null) {
    fail("缺少 StackCraft 箭矢投射物 Prefab，无法做投射物对象级对账：Assets/StackCraft/Prefabs/UI/Projectile_Arrow.prefab");
  }
  if (stackCraftProjectileMagicPrefabText == null) {
    fail("缺少 StackCraft 魔法投射物 Prefab，无法确认箭矢 / 魔法投射物共享同一表现参数：Assets/StackCraft/Prefabs/UI/Projectile_Magic.prefab");
  }
  if (stackCraftProjectileArrowPrefabText != null && stackCraftProjectileMagicPrefabText != null) {
    const stackCraftProjectileArrowYaml = unityYamlObjects(stackCraftProjectileArrowPrefabText);
    const stackCraftProjectileMagicYaml = unityYamlObjects(stackCraftProjectileMagicPrefabText);
    const projectilePrefabYaml = unityYamlObjects(projectilePrefabText);
    assertUnitySpriteRendererObjectMatchesSource(
      stackCraftProjectileArrowYaml,
      "Projectile_Arrow",
      stackCraftProjectileMagicYaml,
      "Projectile_Magic",
      "StackCraft 箭矢 / 魔法投射物共享参数");
    assertUnitySpriteRendererObjectMatchesSource(
      stackCraftProjectileArrowYaml,
      "Projectile_Arrow",
      projectilePrefabYaml,
      "投射物",
      "Gameplay 投射物");
    assertUnityComponentFieldReferences(
      projectilePrefabYaml,
      "投射物",
      "m_renderer",
      "投射物",
      212,
      "投射物视图字段引用");
  }
  if (projectilePrefabText.includes("m_LocalScale: {x: 0.28, y: 0.08, z: 1}")) {
    fail("投射物仍保留旧占位缩放，未使用 StackCraft 投射物图片自身比例。");
  }
  if (projectilePrefabText.includes("m_Size: {x: 0.25, y: 0.25}")) {
    fail("投射物仍保留 0.25 × 0.25 的默认 SpriteRenderer 尺寸，未对齐 StackCraft。");
  }
  const projectilePrefabYaml = unityYamlObjects(projectilePrefabText);
  for (const [_sourcePath, localPath, label, fieldName] of projectileSpritePairs) {
    const localSpriteGuid = unityGuid(readIfExists(`${localPath}.meta`));
    if (localSpriteGuid == null) {
      fail(`${localPath}.meta 缺少合法 GUID，无法静态验证 ${label} 引用。`);
    } else {
      assertUnityMonoBehaviourFieldReferencesGuid(
        projectilePrefabYaml,
        "投射物",
        fieldName,
        "21300000",
        localSpriteGuid,
        "3",
        `投射物 Prefab 的 StackCraft ${label}字段`);
    }
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
    .map((file) => `stackcraft-script:${rel(file).replace(/\\/g, "/")}`)
    .filter((marker) => !matrix.includes(marker));
  for (const marker of missing) {
    fail(`StackCraft 脚本 ${marker.replace("stackcraft-script:", "")} 未在吸收矩阵中用精确路径登记，不能进入完整等价验收。`);
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
