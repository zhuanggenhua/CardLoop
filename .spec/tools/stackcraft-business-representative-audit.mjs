#!/usr/bin/env node
/**
 * stackcraft-business-representative-audit — StackCraft 业务代表性验收。
 *
 * 只读取 StackCraft 参考资产和 CardLoop 当前作者源，不启动 Unity，不修改资源。
 * 它证明“Starter / Beginning 两个代表性卡包业务竖切已映射到 CardLoop 作者源”，
 * 不证明 StackCraft 业务数据已全量迁移。
 */
import fs from "node:fs";
import path from "node:path";

const root = process.argv[2] ? path.resolve(process.argv[2]) : process.cwd();
const errors = [];

function rel(relativePath) {
  return path.join(root, relativePath);
}

function fail(message) {
  errors.push(message);
}

function assert(condition, message) {
  if (!condition) fail(message);
}

function read(relativePath) {
  const file = rel(relativePath);
  if (!fs.existsSync(file)) {
    fail(`缺少文件：${relativePath}`);
    return "";
  }

  return fs.readFileSync(file, "utf8");
}

function walk(relativePath) {
  const absolute = rel(relativePath);
  if (!fs.existsSync(absolute)) return [];

  const out = [];
  for (const entry of fs.readdirSync(absolute, { withFileTypes: true })) {
    const child = path.join(absolute, entry.name);
    if (entry.isDirectory()) out.push(...walk(path.relative(root, child)));
    else out.push(child);
  }
  return out;
}

function countAssets(relativePath) {
  return walk(relativePath).filter((file) => file.endsWith(".asset")).length;
}

function scalar(text, name) {
  const match = text.match(new RegExp(`^\\s*${name}:\\s*(.*)$`, "m"));
  if (!match) return null;
  return match[1].trim().replace(/^"|"$/g, "");
}

function numberScalar(text, name) {
  const value = scalar(text, name);
  return value == null || value === "" ? Number.NaN : Number(value);
}

function buildStackCraftGuidNameMap() {
  const map = new Map();
  for (const metaFile of walk("Assets/StackCraft/Resources").filter((file) => file.endsWith(".asset.meta"))) {
    const metaText = fs.readFileSync(metaFile, "utf8");
    const guid = metaText.match(/^guid:\s*([0-9a-f]{32})/m)?.[1];
    if (!guid) continue;

    const assetFile = metaFile.slice(0, -".meta".length);
    const assetText = fs.readFileSync(assetFile, "utf8");
    const displayName = scalar(assetText, "displayName") ?? path.basename(assetFile, ".asset");
    map.set(guid, displayName);
  }

  return map;
}

function parseStackCraftPackSlots(text, guidNameMap) {
  const slots = [];
  let current = null;
  let currentEntry = null;
  let inRecipes = false;

  for (const line of text.split(/\r?\n/)) {
    if (/^\s*-\s+Entries:\s*$/.test(line)) {
      current = { entries: [], recipes: [], recipeChance: 0 };
      slots.push(current);
      currentEntry = null;
      inRecipes = false;
      continue;
    }

    if (!current) continue;

    if (/^\s*PossibleRecipes:/.test(line)) {
      inRecipes = true;
      currentEntry = null;
      continue;
    }

    const cardGuid = line.match(/Card:\s+\{[^}]*guid:\s*([0-9a-f]{32})/)?.[1];
    if (cardGuid && !inRecipes) {
      currentEntry = { card: guidNameMap.get(cardGuid) ?? cardGuid, weight: null };
      current.entries.push(currentEntry);
      continue;
    }

    const recipeGuid = line.match(/^    - \{[^}]*guid:\s*([0-9a-f]{32})/)?.[1];
    if (recipeGuid && inRecipes) {
      current.recipes.push(guidNameMap.get(recipeGuid) ?? recipeGuid);
      continue;
    }

    const weight = line.match(/^\s*Weight:\s*(\d+)/)?.[1];
    if (weight && currentEntry) {
      currentEntry.weight = Number(weight);
      continue;
    }

    const recipeChance = line.match(/^\s*RecipeChance:\s*([0-9.]+)/)?.[1];
    if (recipeChance) {
      current.recipeChance = Number(recipeChance);
    }
  }

  return slots;
}

function parseCardLoopPackSlots(text) {
  const slots = [];
  let current = null;
  let inRecipes = false;
  let pendingCardId = null;
  let pendingActionId = null;

  for (const line of text.split(/\r?\n/)) {
    if (/^\s*-\s+m_entries:\s*$/.test(line)) {
      current = { entries: [], recipes: [], recipeChance: 0 };
      slots.push(current);
      inRecipes = false;
      pendingCardId = null;
      pendingActionId = null;
      continue;
    }

    if (!current) continue;

    if (/^\s*m_recipeEntries:/.test(line)) {
      inRecipes = true;
      pendingCardId = null;
      pendingActionId = null;
      continue;
    }

    const value = line.match(/^\s*m_value:\s*(.*)$/)?.[1]?.trim();
    if (value != null) {
      if (inRecipes) {
        if (pendingActionId == null) {
          pendingActionId = value;
        } else {
          current.recipes.push({ actionId: pendingActionId, recipeCardId: value });
          pendingActionId = null;
        }
      } else {
        pendingCardId = value;
      }
      continue;
    }

    const weight = line.match(/^\s*m_weight:\s*(\d+)/)?.[1];
    if (weight && pendingCardId != null) {
      current.entries.push({ cardId: pendingCardId, weight: Number(weight) });
      pendingCardId = null;
      continue;
    }

    const recipeChance = line.match(/^\s*m_recipeChance:\s*([0-9.]+)/)?.[1];
    if (recipeChance) {
      current.recipeChance = Number(recipeChance);
    }
  }

  return slots;
}

function assertStackCraftInventoryCounts() {
  const counts = {
    cards: countAssets("Assets/StackCraft/Resources/Cards"),
    packs: countAssets("Assets/StackCraft/Resources/Packs"),
    recipes: countAssets("Assets/StackCraft/Resources/Recipes"),
    quests: countAssets("Assets/StackCraft/Resources/Quests"),
    encounters: countAssets("Assets/StackCraft/Resources/Encounters"),
  };

  assert(counts.cards === 103, `StackCraft 卡牌数量应为 103，当前为 ${counts.cards}`);
  assert(counts.packs === 11, `StackCraft 卡包数量应为 11，当前为 ${counts.packs}`);
  assert(counts.recipes === 90, `StackCraft 配方数量应为 90，当前为 ${counts.recipes}`);
  assert(counts.quests === 66, `StackCraft 任务数量应为 66，当前为 ${counts.quests}`);
  assert(counts.encounters === 3, `StackCraft 遭遇数量应为 3，当前为 ${counts.encounters}`);

  return counts;
}

function assertStarterRepresentative(guidNameMap) {
  const source = read("Assets/StackCraft/Resources/Packs/00_Pack_Starter.asset");
  const target = read("Assets/Gameplay/Tests/地基测试卡包.asset");
  const sourceSlots = parseStackCraftPackSlots(source, guidNameMap);
  const targetSlots = parseCardLoopPackSlots(target);

  assert(scalar(source, "displayName") === "Starter", "StackCraft Starter 显示名读取失败。");
  assert(scalar(target, "m_displayName") === "Starter", "CardLoop Starter 显示名未对齐。");
  assert(scalar(target, "m_description") === scalar(source, "description"), "CardLoop Starter 描述未对齐。");
  assert(sourceSlots.length === 4, `StackCraft Starter 应有 4 个槽位，当前 ${sourceSlots.length}`);
  assert(targetSlots.length === 4, `CardLoop Starter 应有 4 个槽位，当前 ${targetSlots.length}`);

  const mapping = new Map([
    ["Villager", "test.foundation.card"],
    ["Berry Bush", "test.foundation.pack.reward.first"],
    ["Rock", "test.foundation.pack.reward.second"],
    ["Wood", "test.foundation.product"],
  ]);

  for (let i = 0; i < sourceSlots.length; i++) {
    const sourceEntry = sourceSlots[i].entries[0];
    const targetEntry = targetSlots[i].entries[0];
    assert(sourceSlots[i].entries.length === 1, `StackCraft Starter 第 ${i + 1} 槽不是固定单卡。`);
    assert(targetSlots[i].entries.length === 1, `CardLoop Starter 第 ${i + 1} 槽不是固定单卡。`);
    assert(
      targetEntry.cardId === mapping.get(sourceEntry.card),
      `Starter 第 ${i + 1} 槽映射错误：${sourceEntry.card} -> ${targetEntry.cardId}`);
    assert(targetEntry.weight > 0, `Starter 第 ${i + 1} 槽权重必须为正。`);
  }
}

function assertBeginningRepresentative(guidNameMap) {
  const source = read("Assets/StackCraft/Resources/Packs/01_Pack_Beginning.asset");
  const target = read("Assets/Gameplay/Tests/地基开端卡包.asset");
  const vendor = read("Assets/Gameplay/Tests/地基开端卡包商贩.asset");
  const sourceSlots = parseStackCraftPackSlots(source, guidNameMap);
  const targetSlots = parseCardLoopPackSlots(target);

  assert(scalar(source, "displayName") === "Beginning", "StackCraft Beginning 显示名读取失败。");
  assert(scalar(target, "m_displayName") === "Beginning", "CardLoop Beginning 显示名未对齐。");
  assert(scalar(target, "m_description") === scalar(source, "description"), "CardLoop Beginning 描述未对齐。");
  assert(
    targetSlots.length === numberScalar(source, "uses"),
    "CardLoop Beginning 使用次数未对齐：CardPackDefinition 的运行时使用次数由抽取槽位数量派生。");
  assert(sourceSlots.length === 3, `StackCraft Beginning 应有 3 个槽位，当前 ${sourceSlots.length}`);
  assert(targetSlots.length === 3, `CardLoop Beginning 应有 3 个槽位，当前 ${targetSlots.length}`);

  const mapping = new Map([
    ["Stone", "test.foundation.day-cycle.sellable"],
    ["Wood", "test.foundation.product"],
    ["Berry Bush", "test.foundation.pack.reward.first"],
    ["Rock", "test.foundation.pack.reward.second"],
    ["Soil", "test.foundation.pack.beginning.soil"],
    ["Tree", "test.foundation.pack.beginning.tree"],
    ["Chicken", "test.foundation.pack.beginning.chicken"],
    ["Slime", "test.foundation.pack.beginning.slime"],
    ["Golden Key", "test.foundation.pack.beginning.golden-key"],
  ]);

  for (let i = 0; i < sourceSlots.length; i++) {
    const sourceEntries = new Map(sourceSlots[i].entries.map((entry) => [entry.card, entry.weight]));
    const targetEntries = new Map(targetSlots[i].entries.map((entry) => [entry.cardId, entry.weight]));
    for (const [sourceName, targetId] of mapping) {
      assert(sourceEntries.has(sourceName), `StackCraft Beginning 第 ${i + 1} 槽缺少 ${sourceName}`);
      assert(targetEntries.has(targetId), `CardLoop Beginning 第 ${i + 1} 槽缺少 ${sourceName} 的映射 ${targetId}`);
      assert(
        targetEntries.get(targetId) === sourceEntries.get(sourceName),
        `Beginning 第 ${i + 1} 槽 ${sourceName} 权重未对齐。`);
    }
    assert(sourceSlots[i].recipes.length === 5, `StackCraft Beginning 第 ${i + 1} 槽配方候选应为 5。`);
    assert(targetSlots[i].recipes.length === 5, `CardLoop Beginning 第 ${i + 1} 槽配方候选应为 5。`);
    assert(
      targetSlots[i].recipeChance === sourceSlots[i].recipeChance,
      `Beginning 第 ${i + 1} 槽配方概率未对齐。`);
  }

  assert(vendor.includes("m_offeredPackId:\n    m_value: test.foundation.pack.beginning"), "Beginning 商贩没有指向 Beginning 卡包。");
  assert(numberScalar(vendor, "m_price") === numberScalar(source, "buyPrice"), "Beginning 商贩价格未对齐。");
  assert(numberScalar(vendor, "m_minimumCompletedQuests") === numberScalar(source, "minQuests"), "Beginning 商贩解锁任务数未对齐。");
}

function main() {
  const counts = assertStackCraftInventoryCounts();
  const guidNameMap = buildStackCraftGuidNameMap();
  assert(guidNameMap.size > 0, "没有读取到 StackCraft 业务资产 GUID。");
  assertStarterRepresentative(guidNameMap);
  assertBeginningRepresentative(guidNameMap);

  if (errors.length > 0) {
    console.error("stackcraft-business-representative-audit failed:");
    for (const error of errors) console.error(`- ${error}`);
    process.exit(1);
  }

  console.log("stackcraft-business-representative-audit passed");
  console.log(`StackCraft inventory: cards=${counts.cards}, packs=${counts.packs}, recipes=${counts.recipes}, quests=${counts.quests}, encounters=${counts.encounters}`);
  console.log("Representative coverage: Starter pack, Beginning pack, Beginning vendor, weighted entries, recipe candidates.");
}

main();
