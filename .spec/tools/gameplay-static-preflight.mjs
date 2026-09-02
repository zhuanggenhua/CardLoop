#!/usr/bin/env node
/**
 * gameplay-static-preflight — CardLoop Gameplay 静态预检。
 *
 * 只检查新项目文档口径、规范结构和正式源码中的旧来源残留；
 * 不启动 Unity，不替代 Unity 编译、资源回读或 PlayMode 验证。
 *
 * 用法：node .spec/tools/gameplay-static-preflight.mjs [仓库根目录]
 */
import fs from "node:fs";
import path from "node:path";

const positionalArgs = process.argv.slice(2).filter((arg) => !arg.startsWith("--"));
const root = positionalArgs[0] ? path.resolve(positionalArgs[0]) : process.cwd();

const errors = [];
const warnings = [];

const legacyProjectName = "Stack" + "Craft";
const legacyNamespace = "Crying" + "Snow";
const legacyRuntimeBaseline = "Fantasy" + "Word";
const legacyProjectPath = "dark" + "-corridor";
const legacyEngineName = "God" + "ot";
const legacyAssetRoot = "Assets/" + legacyProjectName;
const legacyFullAbsorption = "完整" + "吸收";
const legacyParityWording = "同" + "态";

const oldSourceTokens = [
  new RegExp(legacyProjectName, "i"),
  new RegExp(legacyNamespace),
  new RegExp(legacyAssetRoot, "i"),
  new RegExp(legacyRuntimeBaseline),
  new RegExp(legacyProjectPath, "i"),
  new RegExp(legacyEngineName),
  new RegExp(legacyFullAbsorption),
  new RegExp(legacyParityWording),
];

const sourceResidualTokens = [
  /\bGamePlay\b/,
  new RegExp(legacyNamespace),
  new RegExp(legacyAssetRoot, "i"),
  /Resources\.LoadAll/,
  /namespace\s+CardLoop/,
  /CardLoop\.Runtime/,
  new RegExp(legacyRuntimeBaseline),
  new RegExp(legacyProjectPath, "i"),
];

const blockedDocJunkPatterns = [
  /(^|\/)task_plan\.md$/i,
  /(^|\/)findings\.md$/i,
  /(^|\/)progress\.md$/i,
  /(^|\/)ARCHITECTURE\.html$/i,
  /(^|\/)docs\/archive(\/|$)/i,
  /full-history/i,
  /full-design/i,
  /full-report/i,
  /legacy-/i,
];

const docRoots = [
  "README.md",
  "AGENTS.md",
  ".spec/AGENTS.md",
  ".spec/rules",
  ".spec/knowledge",
  ".spec/skills",
  ".spec/tasks",
];

const sourceRoots = [
  "Assets/Scripts",
  "Assets/Editor",
  "Assets/Tests",
  "ProjectSettings",
];

const ignoredDirectories = new Set([
  ".git",
  "Library",
  "Logs",
  "Temp",
  "Obj",
  "Build",
  "Builds",
  "UserSettings",
  "node_modules",
]);

function fail(message) {
  errors.push(message);
}

function warn(message) {
  warnings.push(message);
}

function toPosix(relativePath) {
  return relativePath.replaceAll(path.sep, "/");
}

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function walkFiles(relativePath, predicate = () => true) {
  const absolutePath = path.join(root, relativePath);
  if (!fs.existsSync(absolutePath)) return [];

  const stat = fs.statSync(absolutePath);
  if (stat.isFile()) {
    const normalized = toPosix(relativePath);
    return predicate(normalized) ? [normalized] : [];
  }

  if (!stat.isDirectory()) return [];

  const result = [];
  const stack = [absolutePath];
  while (stack.length > 0) {
    const current = stack.pop();
    const entries = fs.readdirSync(current, { withFileTypes: true });
    for (const entry of entries) {
      const absolute = path.join(current, entry.name);
      const relative = toPosix(path.relative(root, absolute));
      if (entry.isDirectory()) {
        if (!ignoredDirectories.has(entry.name)) {
          stack.push(absolute);
        }
        continue;
      }
      if (entry.isFile() && predicate(relative)) {
        result.push(relative);
      }
    }
  }
  return result.sort();
}

function readTextIfPossible(relativePath) {
  const absolutePath = path.join(root, relativePath);
  const buffer = fs.readFileSync(absolutePath);
  if (buffer.includes(0)) return null;
  return buffer.toString("utf8");
}

function lineNumber(text, index) {
  return text.slice(0, index).split(/\r?\n/).length;
}

function scanTokens(files, tokens, label) {
  for (const file of files) {
    const text = readTextIfPossible(file);
    if (text == null) continue;
    for (const token of tokens) {
      token.lastIndex = 0;
      const match = token.exec(text);
      if (match != null) {
        fail(`${label} 仍包含旧来源残留：${file}:${lineNumber(text, match.index)} -> ${match[0]}`);
      }
    }
  }
}

function assertUnitySkillsThinMirror() {
  const skillRoot = ".spec/skills/unity-skills";
  const files = walkFiles(skillRoot);
  if (!exists(`${skillRoot}/SKILL.md`)) {
    fail("缺少 Unity 官方候选 skill 的薄入口：.spec/skills/unity-skills/SKILL.md");
    return;
  }

  const extraFiles = files.filter((file) => file !== `${skillRoot}/SKILL.md`);
  if (extraFiles.length > 0) {
    fail(`.spec/skills/unity-skills 只能保留薄 SKILL.md，仍有多余文件：${extraFiles.slice(0, 20).join(", ")}${extraFiles.length > 20 ? " ..." : ""}`);
  }
}

function assertNoDocJunk() {
  const files = walkFiles(".");
  for (const file of files) {
    for (const pattern of blockedDocJunkPatterns) {
      if (pattern.test(file)) {
        fail(`不应保留中间/归档污染文档：${file}`);
        break;
      }
    }
  }
}

function assertNoLegacyNamedSpecFiles() {
  const files = walkFiles(".spec", (file) => /\.(md|mjs)$/i.test(file));
  const legacyNamePattern = new RegExp(legacyProjectName, "i");
  const legacyNamedFiles = files.filter((file) => legacyNamePattern.test(file));
  if (legacyNamedFiles.length > 0) {
    fail(`.spec 仍存在旧来源命名文件：${legacyNamedFiles.join(", ")}`);
  }
}

function assertCompileArtifactsBoundary() {
  const solutionFiles = walkFiles(".", (file) => /\.(sln|csproj)$/i.test(file));
  if (solutionFiles.length === 0) {
    warn("未找到 .sln/.csproj；C# 编译结果需等 Unity 生成工程文件或由 Unity 验证。");
  }
}

function main() {
  if (!fs.existsSync(root)) {
    fail(`仓库根目录不存在：${root}`);
  }

  const docFiles = docRoots.flatMap((entry) => walkFiles(entry, (file) => /\.(md|mjs)$/i.test(file)));
  scanTokens(docFiles, oldSourceTokens, "文档/规范");

  const sourceFiles = sourceRoots.flatMap((entry) => walkFiles(entry, (file) => /\.(cs|asmdef|json|asset|prefab|unity|uxml|uss)$/i.test(file)));
  scanTokens(sourceFiles, sourceResidualTokens, "正式源码/配置");

  assertUnitySkillsThinMirror();
  assertNoDocJunk();
  assertNoLegacyNamedSpecFiles();
  assertCompileArtifactsBoundary();

  for (const warning of warnings) {
    console.warn(`[warn] ${warning}`);
  }

  if (errors.length > 0) {
    for (const error of errors) {
      console.error(`[fail] ${error}`);
    }
    process.exitCode = 1;
    return;
  }

  console.log("gameplay-static-preflight passed: CardLoop 文档口径与静态边界已收口。");
}

main();
