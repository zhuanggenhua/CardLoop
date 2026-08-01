#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
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
  if (!text.startsWith("---\n")) return null;
  const end = text.indexOf("\n---", 4);
  if (end < 0) return null;
  const raw = text.slice(4, end).trim();
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

function assertRootPointer() {
  const rootAgents = read("AGENTS.md");
  for (const token of [".spec/AGENTS.md", ".spec/rules/system.md", ".spec/knowledge/README.md"]) {
    if (!rootAgents.includes(token)) fail(`根 AGENTS.md 未指向 ${token}`);
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

assertRequiredFiles();
assertRootPointer();
assertNoRetiredEntrypoints();
assertSkillFrontmatter();
assertAgentFrontmatter();
assertKnowledgeFrontmatter();
assertKnowledgeLinks();
assertNoRuntimeFallbackLanguage();

if (errors.length > 0) {
  console.error("spec-lint failed:");
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log("spec-lint passed");

