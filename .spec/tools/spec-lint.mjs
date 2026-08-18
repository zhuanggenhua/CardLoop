#!/usr/bin/env node
/**
 * spec-lint — .spec/ 组织结构一致性机械校验。改完 .spec/ 必须跑一次。
 * 用法:node .spec/tools/spec-lint.mjs [仓库根目录]   (省略参数时取当前工作目录)
 *
 * 校验项清单(本注释是 lint 能力清单的单一权威,其他文档只指回这里):
 *  1. 核心入口存在:AGENTS.md、CLAUDE.md、.spec/AGENTS.md、rules/system.md、
 *     knowledge/README.md、knowledge/lessons.md、decisions/README.md、tasks/README.md。
 *  2. CLAUDE.md force-load 完整性:.spec/AGENTS.md、knowledge/README.md 和 rules/ 下每个 .md
 *     都必须有对应 @.spec/... 行。
 *  3. 根 AGENTS.md 只做兼容入口,必须指向三份核心文档。
 *  4. 宿主适配入口必须是 Git symlink:.agents/skills、.claude/skills、.claude/agents;
 *     .codex/skills 不得继续作为项目 skill 来源。
 *  5. ADR 结构:decisions/README.md 和每条 ADR 无 frontmatter;ADR 文件名为 NNNN-<slug>.md,
 *     标题以 "# NNNN · " 开头,并登记进 decisions/README.md。
 *  6. 任务卡结构:tasks/README.md 无 frontmatter;根目录任务卡只允许 status frontmatter,
 *     枚举 pending / in_progress / completed。
 *  7. agents / skills frontmatter:只允许 name + description,且 name 与文件 / 目录名一致。
 *  8. knowledge frontmatter、链接可达、索引链可达和 standards 根索引直登。
 *  9. 废弃入口、迁移残留和禁止运行时兜底表述不得回流。
 */
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const root = process.argv[2] ? path.resolve(process.argv[2]) : process.cwd();
const specRoot = path.join(root, ".spec");
const errors = [];
const statuses = new Set(["设计中", "实施中", "已交付", "历史归档"]);
const retiredKnowledgePath = path.join("docs", "ai");

function rel(file) {
  return path.relative(root, file).replaceAll(path.sep, "/");
}

function fail(message) {
  errors.push(message);
}

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function gitOutput(args) {
  try {
    return execFileSync("git", args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
  } catch {
    return "";
  }
}

function escapeRegExp(text) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  const out = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...walk(full));
    else out.push(full);
  }
  return out;
}

function parseFrontmatter(file) {
  const text = fs.readFileSync(file, "utf8");
  const matchFrontmatter = text.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!matchFrontmatter) return null;
  const raw = matchFrontmatter[1].trim();
  const result = {};
  const lines = raw.split(/\r?\n/);
  for (const line of lines) {
    const match = line.match(/^([A-Za-z0-9_-]+):\s*(.*)$/);
    if (match) result[match[1]] = match[2].trim();
  }
  result.__raw = raw;
  return result;
}

function assertRequiredFiles() {
  const required = [
    "AGENTS.md",
    "CLAUDE.md",
    ".spec/AGENTS.md",
    ".spec/rules/system.md",
    ".spec/knowledge/README.md",
    ".spec/knowledge/lessons.md",
    ".spec/decisions/README.md",
    ".spec/tasks/README.md",
  ];
  for (const file of required) {
    if (!exists(file)) fail(`缺少必需文件：${file}`);
  }
}

function assertClaudeImports() {
  if (!exists("CLAUDE.md")) return;

  const claude = read("CLAUDE.md");
  const rulesDir = path.join(specRoot, "rules");
  const ruleImports = fs.existsSync(rulesDir)
    ? fs.readdirSync(rulesDir)
      .filter((name) => name.endsWith(".md") && name !== "README.md")
      .map((name) => `.spec/rules/${name}`)
    : [];

  for (const importPath of [".spec/AGENTS.md", ".spec/knowledge/README.md", ...ruleImports]) {
    const pattern = new RegExp(`^@${escapeRegExp(importPath)}$`, "m");
    if (!pattern.test(claude)) fail(`CLAUDE.md 缺少强制载入行：@${importPath}`);
  }
}

function assertRootPointer() {
  const rootAgents = read("AGENTS.md");
  for (const token of [".spec/AGENTS.md", ".spec/rules/system.md", ".spec/knowledge/README.md"]) {
    if (!rootAgents.includes(token)) fail(`根 AGENTS.md 未指向 ${token}`);
  }
}

function assertDecisionAdrs() {
  const decisionsDir = path.join(specRoot, "decisions");
  const indexPath = path.join(decisionsDir, "README.md");
  if (!fs.existsSync(indexPath)) return;

  const indexText = fs.readFileSync(indexPath, "utf8");
  if (indexText.startsWith("---")) fail(".spec/decisions/README.md 不应使用 frontmatter；ADR 格式契约由正文说明");

  const decisionFiles = walk(decisionsDir)
    .filter((file) => file.endsWith(".md") && path.basename(file) !== "README.md");

  for (const file of decisionFiles) {
    const name = path.basename(file);
    const text = fs.readFileSync(file, "utf8");
    if (!/^\d{4}-[a-z0-9]+(?:-[a-z0-9]+)*\.md$/.test(name)) {
      fail(`${rel(file)} ADR 文件名必须是 NNNN-<slug>.md`);
    }
    if (text.startsWith("---")) fail(`${rel(file)} ADR 不应使用 frontmatter`);
    const number = name.slice(0, 4);
    if (!new RegExp(`^#\\s+${number}\\s+·\\s+`, "m").test(text)) {
      fail(`${rel(file)} ADR 标题必须以 "# ${number} · " 开头`);
    }
    if (!indexText.includes(`](${name})`)) fail(`${rel(file)} 未登记进 decisions/README.md 索引`);
  }
}

function assertTaskCards() {
  const tasksDir = path.join(specRoot, "tasks");
  const readme = path.join(tasksDir, "README.md");
  if (fs.existsSync(readme) && fs.readFileSync(readme, "utf8").startsWith("---")) {
    fail(".spec/tasks/README.md 不应使用 frontmatter；任务卡格式契约由正文说明");
  }
  if (!fs.existsSync(tasksDir)) return;

  const statusEnum = new Set(["pending", "in_progress", "completed"]);
  for (const entry of fs.readdirSync(tasksDir, { withFileTypes: true })) {
    if (entry.isDirectory() || !entry.name.endsWith(".md") || entry.name === "README.md") continue;
    const file = path.join(tasksDir, entry.name);
    const fm = parseFrontmatter(file);
    if (!fm) {
      fail(`${rel(file)} 任务卡缺少 frontmatter`);
      continue;
    }
    const keys = Object.keys(fm).filter((key) => key !== "__raw");
    if (keys.length !== 1 || keys[0] !== "status") fail(`${rel(file)} 任务卡 frontmatter 只允许 status`);
    if (!statusEnum.has(fm.status)) fail(`${rel(file)} status 不在枚举内：${fm.status ?? ""}`);
  }
}

function assertNoRetiredEntrypoints() {
  const retiredAbsolute = path.join(root, retiredKnowledgePath);
  if (fs.existsSync(retiredAbsolute)) {
    const entries = walk(retiredAbsolute);
    if (entries.length > 0) {
      fail(`废弃规范入口仍存在且非空：${retiredKnowledgePath.replaceAll(path.sep, "/")}`);
    } else {
      console.warn(`spec-lint warning: 废弃规范入口空目录仍被外部进程占用：${retiredKnowledgePath.replaceAll(path.sep, "/")}`);
    }
  }

  const scopedRoots = [
    "AGENTS.md",
    ".spec/AGENTS.md",
    ".spec/rules/system.md",
    ".spec/knowledge",
    ".spec/skills",
    ".spec/agents",
    ".spec/decisions",
    "scripts",
  ];
  const forbidden = [
    new RegExp(["docs", "ai"].join("/"), "i"),
    new RegExp(String.raw`docs\\ai`, "i"),
    new RegExp(["docs", "ai"].join("-"), "i"),
    new RegExp(["历史", "兼容"].join("")),
    new RegExp(["兼容", "说明"].join("")),
    new RegExp(["迁移", "版"].join("")),
    new RegExp(["迁移", "来源"].join("")),
    new RegExp(["已迁移", "来源"].join("")),
    new RegExp(["待用户", "决策"].join("")),
    new RegExp(["需要用户", "决策"].join("")),
    new RegExp(["暂", "保留"].join("")),
    new RegExp(["不", "删除"].join("")),
  ];

  for (const scopedRoot of scopedRoots) {
    const absolute = path.join(root, scopedRoot);
    if (!fs.existsSync(absolute)) continue;
    const files = fs.statSync(absolute).isDirectory()
      ? walk(absolute).filter((file) => file.endsWith(".md") || file.endsWith(".mjs"))
      : [absolute];
    for (const file of files) {
      const text = fs.readFileSync(file, "utf8");
      for (const pattern of forbidden) {
        pattern.lastIndex = 0;
        if (pattern.test(text)) fail(`${rel(file)} 包含废弃入口或迁移残留：${pattern}`);
      }
    }
  }
}

function assertSkillFrontmatter() {
  const skillsDir = path.join(specRoot, "skills");
  for (const skillFile of walk(skillsDir).filter((file) => path.basename(file) === "SKILL.md")) {
    const fm = parseFrontmatter(skillFile);
    const expectedName = path.basename(path.dirname(skillFile));
    if (!fm) {
      fail(`${rel(skillFile)} 缺少 YAML frontmatter`);
      continue;
    }
    const keys = Object.keys(fm).filter((key) => key !== "__raw");
    for (const key of keys) {
      if (!["name", "description"].includes(key)) fail(`${rel(skillFile)} frontmatter 只允许 name/description，发现 ${key}`);
    }
    if (fm.name !== expectedName) fail(`${rel(skillFile)} name=${fm.name}，应为 ${expectedName}`);
    if (!fm.description) fail(`${rel(skillFile)} 缺少 description`);
  }
}

function assertAgentFrontmatter() {
  const agentsDir = path.join(specRoot, "agents");
  for (const agentFile of walk(agentsDir).filter((file) => file.endsWith(".agent.md"))) {
    const fm = parseFrontmatter(agentFile);
    const expectedName = path.basename(agentFile, ".agent.md");
    if (!fm) {
      fail(`${rel(agentFile)} 缺少 YAML frontmatter`);
      continue;
    }
    const keys = Object.keys(fm).filter((key) => key !== "__raw");
    for (const key of keys) {
      if (!["name", "description"].includes(key)) fail(`${rel(agentFile)} frontmatter 只允许 name/description，发现 ${key}`);
    }
    if (fm.name !== expectedName) fail(`${rel(agentFile)} name=${fm.name}，应为 ${expectedName}`);
    if (!fm.description) fail(`${rel(agentFile)} 缺少 description`);
  }
}

function assertKnowledgeFrontmatter() {
  const files = walk(path.join(specRoot, "knowledge")).filter((file) => file.endsWith(".md"));
  for (const file of files) {
    const fm = parseFrontmatter(file);
    if (!fm) {
      fail(`${rel(file)} 缺少 YAML frontmatter`);
      continue;
    }
    if (!fm.name) fail(`${rel(file)} 缺少 name`);
    if (!fm.description) fail(`${rel(file)} 缺少 description`);
    if (fm.description && [...fm.description].length > 120) fail(`${rel(file)} description 超过 120 字`);
    const text = fs.readFileSync(file, "utf8");
    const statusMatch = text.match(/status:\s*(.+)/);
    if (statusMatch && !statuses.has(statusMatch[1].trim())) fail(`${rel(file)} status 不在枚举内：${statusMatch[1].trim()}`);
  }
}

function assertKnowledgeLinks() {
  const files = walk(path.join(specRoot, "knowledge")).filter((file) => file.endsWith(".md"));
  const linkPattern = /\[[^\]]+\]\(([^)]+)\)/g;
  for (const file of files) {
    const text = fs.readFileSync(file, "utf8");
    for (const match of text.matchAll(linkPattern)) {
      const target = match[1];
      if (/^(https?:|mailto:|#)/.test(target)) continue;
      const clean = target.split("#")[0];
      if (!clean) continue;
      const resolved = path.resolve(path.dirname(file), clean);
      if (!fs.existsSync(resolved)) fail(`${rel(file)} 链接不存在：${target}`);
    }
  }
}

function localKnowledgeRouteLinks(file) {
  const links = [];
  const frontmatter = parseFrontmatter(file);
  if (!frontmatter || !/(?:^|\n)\s*type:\s*index\s*$/m.test(frontmatter.__raw)) return links;

  const tableRows = fs.readFileSync(file, "utf8")
    .split(/\r?\n/)
    .filter((line) => line.trimStart().startsWith("|") && !/^\|[-:| ]+\|$/.test(line.trim()));
  const linkPattern = /\[[^\]]+\]\(([^)]+)\)/g;
  for (const row of tableRows) {
    for (const match of row.matchAll(linkPattern)) {
      const target = match[1];
      if (/^(https?:|mailto:|#)/.test(target)) continue;
      const clean = target.split("#")[0];
      if (!clean) continue;
      const resolved = path.resolve(path.dirname(file), clean);
      if (resolved.startsWith(path.join(specRoot, "knowledge")) && resolved.endsWith(".md")) {
        links.push(resolved);
      }
    }
  }
  return links;
}

function assertKnowledgeRouting() {
  const knowledgeRoot = path.join(specRoot, "knowledge");
  const entry = path.join(knowledgeRoot, "README.md");
  const allKnowledge = walk(knowledgeRoot).filter((file) => file.endsWith(".md"));
  const reachable = new Set();
  const pending = [entry];

  while (pending.length > 0) {
    const file = pending.pop();
    if (reachable.has(file) || !fs.existsSync(file)) continue;
    reachable.add(file);
    for (const target of localKnowledgeRouteLinks(file)) {
      if (!reachable.has(target)) pending.push(target);
    }
  }

  for (const file of allKnowledge) {
    if (!reachable.has(file)) {
      fail(`${rel(file)} 无法从 .spec/knowledge/README.md 的索引链到达，缺少渐进式读取入口`);
    }
  }

  const rootIndex = fs.readFileSync(entry, "utf8");
  for (const standard of walk(path.join(knowledgeRoot, "standards")).filter((file) => file.endsWith(".md"))) {
    const relative = path.relative(knowledgeRoot, standard).replaceAll(path.sep, "/");
    if (!rootIndex.includes(`](${relative})`)) {
      fail(`${rel(standard)} 未直接登记到知识根索引的 standards 路由表`);
    }
  }
}

function assertNoRuntimeFallbackLanguage() {
  const scopedRoots = [
    ".spec/knowledge",
    ".spec/skills",
    ".spec/agents",
  ];
  const forbidden = [
    "运行时查找只作引用缺失兜底",
    "运行时唯一 `Hero` 查找只作为引用缺失兜底",
    "运行时查找只作为引用缺失时的兜底",
    "引用缺失时的兜底",
    "引用缺失兜底",
    "兜底成功",
  ];

  for (const scopedRoot of scopedRoots) {
    const absolute = path.join(root, scopedRoot);
    if (!fs.existsSync(absolute)) continue;
    const files = fs.statSync(absolute).isDirectory()
      ? walk(absolute).filter((file) => file.endsWith(".md"))
      : [absolute];
    for (const file of files) {
      const text = fs.readFileSync(file, "utf8");
      for (const phrase of forbidden) {
        if (text.includes(phrase)) fail(`${rel(file)} 包含禁止的运行时引用兜底表述：${phrase}`);
      }
    }
  }
}

function gitIndexEntry(relativePath) {
  const output = gitOutput(["ls-files", "-s", "--", relativePath]).trim();
  if (!output) return null;
  const match = output.match(/^(\d+)\s+([0-9a-f]{40,64})\s+\d+\s+(.+)$/);
  if (!match) return null;
  return { mode: match[1], object: match[2], path: match[3] };
}

function gitBlobText(objectId) {
  return gitOutput(["cat-file", "-p", objectId]);
}

function assertGitSymlinkAdapter(relativePath, expectedTarget) {
  const entry = gitIndexEntry(relativePath);
  if (!entry) {
    fail(`${relativePath} 未登记到 Git 索引，无法作为宿主适配入口`);
    return;
  }
  if (entry.mode !== "120000") {
    fail(`${relativePath} 必须是 Git symlink(mode 120000)，当前 mode=${entry.mode}`);
  }

  const indexedTarget = gitBlobText(entry.object).trim();
  if (indexedTarget !== expectedTarget) {
    fail(`${relativePath} Git symlink 目标应为 ${expectedTarget}，当前为 ${indexedTarget || "(空)"}`);
  }

  const absolute = path.join(root, relativePath);
  if (!fs.existsSync(absolute)) {
    fail(`${relativePath} 工作区不存在`);
    return;
  }

  const stat = fs.lstatSync(absolute);
  if (stat.isSymbolicLink()) {
    const target = fs.readlinkSync(absolute).replaceAll("\\", "/");
    if (target !== expectedTarget) fail(`${relativePath} 文件系统 symlink 目标应为 ${expectedTarget}，当前为 ${target}`);
    return;
  }

  if (stat.isDirectory()) {
    fail(`${relativePath} 当前是实体目录，不能作为宿主适配入口`);
    return;
  }

  const textTarget = fs.readFileSync(absolute, "utf8").trim();
  if (textTarget !== expectedTarget) {
    fail(`${relativePath} 在 core.symlinks=false 的工作区应只包含 symlink 目标 ${expectedTarget}，当前为 ${textTarget || "(空)"}`);
  }
}

function assertHostAdapters() {
  assertGitSymlinkAdapter(".agents/skills", "../.spec/skills");
  assertGitSymlinkAdapter(".claude/skills", "../.spec/skills");
  assertGitSymlinkAdapter(".claude/agents", "../.spec/agents");

  const codexSkills = path.join(root, ".codex", "skills");
  if (fs.existsSync(codexSkills)) fail(`.codex/skills 不得作为项目 skill 来源继续存在`);

  const indexedCodexSkills = gitOutput(["ls-files", "--", ".codex/skills"]).trim();
  if (indexedCodexSkills) fail(`.codex/skills 仍在 Git 索引中：${indexedCodexSkills.split(/\r?\n/)[0]}`);
}

assertRequiredFiles();
assertClaudeImports();
assertRootPointer();
assertHostAdapters();
assertDecisionAdrs();
assertTaskCards();
assertNoRetiredEntrypoints();
assertSkillFrontmatter();
assertAgentFrontmatter();
assertKnowledgeFrontmatter();
assertKnowledgeLinks();
assertKnowledgeRouting();
assertNoRuntimeFallbackLanguage();

if (errors.length > 0) {
  console.error("spec-lint failed:");
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log("spec-lint passed");

